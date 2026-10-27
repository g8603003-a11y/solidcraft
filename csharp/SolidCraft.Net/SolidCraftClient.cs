using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SolidCraft;

/// <summary>How to reach the app. Everything is optional: by default the running app is found by its discovery files.</summary>
public sealed record SolidCraftConnectOptions
{
    /// <summary>The app listens on 127.0.0.1 only; another host works only through a tunnel.</summary>
    public string Host { get; init; } = "127.0.0.1";

    /// <summary>The control port; <c>null</c> finds the most recently started app (<see cref="ControlDiscovery.FindPort"/>).</summary>
    public int? Port { get; init; }

    /// <summary>This launch's token; <c>null</c> reads the token file (<see cref="ControlDiscovery.FindToken"/>).</summary>
    public string? Token { get; init; }

    /// <summary>How long to wait for the TCP connection.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Folders the app may read or write for this client's requests (sent as <c>allow_paths</c> on every
    /// request). Empty: unconfined, as the person is.
    /// </summary>
    public IReadOnlyList<string> AllowPaths { get; init; } = Array.Empty<string>();
}

/// <summary>
/// A connection to the SolidCraft desktop app's control channel (docs/control-protocol.md): one line of
/// JSON per request, replies in order, and <c>design.changed</c> events after <see cref="SubscribeAsync"/>.
/// One request runs at a time per connection; open another client for parallel work (the app serves 16).
/// </summary>
public sealed class SolidCraftClient : IAsyncDisposable, IDisposable
{
    private readonly TcpClient _tcp;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly string _token;
    private readonly IReadOnlyList<string> _allowPaths;
    private readonly SemaphoreSlim _turn = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly CancellationTokenSource _closing = new();
    private readonly Task _readLoop;
    private long _nextId;
    private bool _disposed;

    private SolidCraftClient(TcpClient tcp, string token, int port, IReadOnlyList<string> allowPaths)
    {
        _tcp = tcp;
        _token = token;
        Port = port;
        _allowPaths = allowPaths;
        var stream = tcp.GetStream();
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        _reader = new StreamReader(stream, utf8, detectEncodingFromByteOrderMarks: false, bufferSize: 1 << 16);
        _writer = new StreamWriter(stream, utf8, bufferSize: 1 << 16) { AutoFlush = true, NewLine = "\n" };
        Sketch = new SketchCommands(this);
        Solid = new SolidCommands(this);
        Parameters = new ParameterCommands(this);
        Files = new FileCommands(this);
        Inspect = new InspectCommands(this);
        Ui = new UiCommands(this);
        Design = new DesignCommands(this);
        _readLoop = Task.Run(ReadLoopAsync);
    }

    /// <summary>The port this client is connected to.</summary>
    public int Port { get; }

    /// <summary>
    /// When the app's 60 s wait passes and it answers "still running", keep waiting for the real
    /// reply through <c>control.pending</c> (default true) instead of throwing
    /// <see cref="SolidCraftException"/> with <see cref="SolidCraftException.IsStillRunning"/>.
    /// </summary>
    public bool WaitForLongRunning { get; set; } = true;

    /// <summary>How often to ask <c>control.pending</c> while waiting for a long-running request.</summary>
    public TimeSpan PendingPollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>True while the connection is open.</summary>
    public bool IsConnected => !_disposed && _tcp.Connected && !_readLoop.IsCompleted;

    /// <summary>Sketch commands (<c>sketch.*</c>).</summary>
    public SketchCommands Sketch { get; }

    /// <summary>Solid modelling commands (<c>solid.*</c>, <c>body.*</c>).</summary>
    public SolidCommands Solid { get; }

    /// <summary>Parameters (<c>parameters.*</c>).</summary>
    public ParameterCommands Parameters { get; }

    /// <summary>Files: new, open, save, export, import (<c>file.*</c>, <c>doc.open</c>).</summary>
    public FileCommands Files { get; }

    /// <summary>Reading the design: measure, physical properties, bill of materials, timeline.</summary>
    public InspectCommands Inspect { get; }

    /// <summary>The window: views, camera, screenshots, renders, dialogs (<c>ui.*</c>).</summary>
    public UiCommands Ui { get; }

    /// <summary>Design properties, units, materials, components (<c>document.*</c>, <c>material.*</c>, <c>component.*</c>).</summary>
    public DesignCommands Design { get; }

    /// <summary>The active design changed (after <see cref="SubscribeAsync"/>). Raised on a background thread.</summary>
    public event EventHandler<DesignChangedEventArgs>? DesignChanged;

    /// <summary>Any event line the app sent (after <see cref="SubscribeAsync"/>). Raised on a background thread.</summary>
    public event EventHandler<ControlEvent>? EventReceived;

    /// <summary>The connection closed (the app quit, or the token was refused).</summary>
    public event EventHandler<Exception?>? Closed;

    /// <summary>Connect to the running app, finding its port and token when the options don't give them.</summary>
    public static async Task<SolidCraftClient> ConnectAsync(SolidCraftConnectOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new SolidCraftConnectOptions();
        var port = options.Port ?? ControlDiscovery.FindPort()
            ?? throw new SolidCraftConnectException(
                "SolidCraft is not running, or its control channel is off (Preferences > Assistant > \"Let AI agents connect\"). " +
                "No control/current.port was found in " + string.Join("; ", ControlDiscovery.ControlDirectories()));
        var token = options.Token ?? ControlDiscovery.FindToken(port)
            ?? throw new SolidCraftConnectException(
                $"no token file for port {port} in " + string.Join("; ", ControlDiscovery.ControlDirectories()) +
                ". Start SolidCraft, or set SOLIDCRAFT_CONTROL_TOKEN.");

        var tcp = new TcpClient { NoDelay = true };
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.ConnectTimeout);
            await tcp.ConnectAsync(options.Host, port, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            tcp.Dispose();
            throw new SolidCraftConnectException($"connecting to SolidCraft at {options.Host}:{port} timed out");
        }
        catch (SocketException e)
        {
            tcp.Dispose();
            var pid = ControlDiscovery.FindPid(port);
            var hint = pid is null ? "" : $" (the token file names process {pid}; if it has exited, the file is stale: delete it or restart SolidCraft)";
            throw new SolidCraftConnectException($"nothing listens at {options.Host}:{port}{hint}: {e.Message}", e);
        }
        var client = new SolidCraftClient(tcp, token, port, options.AllowPaths);
        // Authenticate now, so a stale token fails here and not at the first real call.
        try
        {
            await client.RequestAsync("document.revision", null, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        return client;
    }

    // ---- Generic requests ----------------------------------------------------------------

    /// <summary>
    /// Send one control request (<c>engine.execute</c>, <c>ui.view</c>, <c>document.inspect</c>, …) and return its
    /// <c>result</c>. Throws <see cref="SolidCraftException"/> when the app answers <c>ok: false</c>.
    /// </summary>
    public Task<JsonElement> RequestAsync(string method, object? parameters = null, CancellationToken cancellationToken = default) =>
        RequestCoreAsync(method, parameters, command: null, cancellationToken);

    /// <summary>Run an engine command (the same ones the toolbar runs) and return its result.</summary>
    public Task<JsonElement> ExecuteAsync(string command, object? parameters = null, CancellationToken cancellationToken = default)
    {
        var p = new Dictionary<string, object?>
        {
            ["command"] = command,
            ["params"] = parameters ?? new Dictionary<string, object?>(),
        };
        return RequestCoreAsync("engine.execute", p, command, cancellationToken);
    }

    /// <summary>Run an engine command and deserialize its result.</summary>
    public async Task<T> ExecuteAsync<T>(string command, object? parameters = null, CancellationToken cancellationToken = default) =>
        Json.To<T>(await ExecuteAsync(command, parameters, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Run several commands in one request and one frame (<c>engine.batch</c>, at most 10 000). The first
    /// failure stops the batch and, with rollback, the design goes back to how it was.
    /// </summary>
    public async Task<BatchResult> BatchAsync(IEnumerable<CommandCall> commands, BatchOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new BatchOptions();
        var list = commands.Select(c => new CommandCall(c.Command, c.Params ?? new Dictionary<string, object?>())).ToList();
        var p = new Dictionary<string, object?>
        {
            ["commands"] = list,
            ["rollback"] = options.Rollback,
        };
        if (options.Timings) p["timings"] = true;
        if (options.Profile) p["profile"] = true;
        if (options.CoalesceParameters) p["coalesce_parameters"] = true;
        var result = Json.To<BatchResult>(await RequestAsync("engine.batch", p, cancellationToken).ConfigureAwait(false));
        if (result.Failed is not null && options.ThrowOnFailure)
        {
            var f = result.Failed;
            var back = result.RolledBack == true ? "; the design is back as it was" : result.Undone > 0 ? $"; {result.Undone} steps undone" : "";
            throw new SolidCraftBatchException($"batch step {f.Index} ({f.Command}) failed: {f.Error}{back}", result);
        }
        return result;
    }

    /// <summary>Run commands one after another (<c>engine.script</c>); stops at the first error.</summary>
    public Task<JsonElement> ScriptAsync(IEnumerable<CommandCall> commands, CancellationToken cancellationToken = default) =>
        RequestAsync("engine.script", new Dictionary<string, object?> { ["commands"] = commands.Select(c => new CommandCall(c.Command, c.Params ?? new Dictionary<string, object?>())).ToList() }, cancellationToken);

    /// <summary>Every command: id, label, tab, panel, shortcut, parameter docs and whether it is enabled now.</summary>
    public async Task<IReadOnlyList<CommandInfo>> ListCommandsAsync(CancellationToken cancellationToken = default) =>
        Json.To<List<CommandInfo>>(await RequestAsync("engine.commands", null, cancellationToken).ConfigureAwait(false));

    /// <summary>The active design's id and revision; answered at once, without waiting behind design changes.</summary>
    public async Task<DocumentRevision> GetRevisionAsync(CancellationToken cancellationToken = default) =>
        Json.To<DocumentRevision>(await RequestAsync("document.revision", null, cancellationToken).ConfigureAwait(false));

    /// <summary>Parameters, timeline, bodies, sketches and selection of the active design (<c>document.inspect</c>).</summary>
    public Task<JsonElement> InspectDocumentAsync(bool measure = false, CancellationToken cancellationToken = default) =>
        RequestAsync("document.inspect", new Dictionary<string, object?> { ["measure"] = measure }, cancellationToken);

    /// <summary>
    /// Start receiving <see cref="DesignChanged"/> events on this connection. Returns the design as it is now.
    /// A second call on the same connection is refused by the app.
    /// </summary>
    public async Task<DocumentRevision> SubscribeAsync(CancellationToken cancellationToken = default) =>
        Json.To<DocumentRevision>(await RequestAsync("control.subscribe", null, cancellationToken).ConfigureAwait(false));

    /// <summary>Every request the app is running or has queued, from any connection.</summary>
    public async Task<IReadOnlyList<RunningRequest>> GetRunningAsync(CancellationToken cancellationToken = default)
    {
        var r = await RequestAsync("control.running", null, cancellationToken).ConfigureAwait(false);
        return r.TryGetProperty("running", out var list) ? Json.To<List<RunningRequest>>(list) : new List<RunningRequest>();
    }

    // ---- Protocol ------------------------------------------------------------------------

    private async Task<JsonElement> RequestCoreAsync(string method, object? parameters, string? command, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _turn.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var reply = await SendAsync(method, parameters, cancellationToken).ConfigureAwait(false);
            if (!IsOk(reply) && Json.Bool(reply, "running") == true && WaitForLongRunning)
            {
                reply = await WaitForPendingAsync(cancellationToken).ConfigureAwait(false);
            }
            if (IsOk(reply))
            {
                return reply.TryGetProperty("result", out var result) ? result : default;
            }
            throw Failure(method, command, reply);
        }
        finally
        {
            _turn.Release();
        }
    }

    private async Task<JsonElement> WaitForPendingAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            await Task.Delay(PendingPollInterval, cancellationToken).ConfigureAwait(false);
            var reply = await SendAsync("control.pending", null, cancellationToken).ConfigureAwait(false);
            if (!IsOk(reply))
            {
                throw Failure("control.pending", null, reply);
            }
            var result = reply.TryGetProperty("result", out var r) ? r : default;
            if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty("pending", out var pendingId) || pendingId.ValueKind == JsonValueKind.Null)
            {
                throw new SolidCraftException("the app no longer holds the reply of the long-running request (nothing is pending on this connection)") { Method = "control.pending" };
            }
            if (Json.Bool(result, "done") == true && result.TryGetProperty("reply", out var real))
            {
                return real;
            }
        }
    }

    private async Task<JsonElement> SendAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        var request = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["method"] = method,
            ["params"] = WithAllowPaths(parameters),
            ["token"] = _token,
        };
        var line = JsonSerializer.Serialize(request, Json.Options);

        try
        {
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
            await using var reg = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            return await tcs.Task.ConfigureAwait(false);
        }
        catch (IOException e)
        {
            throw new SolidCraftConnectException("the connection to SolidCraft was lost: " + e.Message, e);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private object WithAllowPaths(object? parameters)
    {
        if (_allowPaths.Count == 0)
        {
            return parameters ?? new Dictionary<string, object?>();
        }
        // Merge allow_paths into the params object.
        var node = JsonSerializer.SerializeToNode(parameters ?? new Dictionary<string, object?>(), Json.Options)?.AsObject()
            ?? new System.Text.Json.Nodes.JsonObject();
        node["allow_paths"] = new System.Text.Json.Nodes.JsonArray(_allowPaths.Select(p => (System.Text.Json.Nodes.JsonNode?)p).ToArray());
        return node;
    }

    private static bool IsOk(JsonElement reply) => Json.Bool(reply, "ok") == true;

    private static SolidCraftException Failure(string method, string? command, JsonElement reply)
    {
        var error = Json.String(reply, "error") ?? reply.ToString();
        var text = command is null ? $"{method}: {error}" : $"{command}: {error}";
        return new SolidCraftException(text)
        {
            Method = method,
            Command = command,
            IsStillRunning = Json.Bool(reply, "running") == true,
            IsBusy = Json.Bool(reply, "busy") == true,
            IsStopped = Json.Bool(reply, "stopped") == true,
            IsUnauthorized = error.StartsWith("unauthorized", StringComparison.OrdinalIgnoreCase),
        };
    }

    private async Task ReadLoopAsync()
    {
        Exception? fault = null;
        try
        {
            while (!_closing.IsCancellationRequested)
            {
                var line = await _reader.ReadLineAsync(_closing.Token).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }
                if (line.Length == 0)
                {
                    continue;
                }
                JsonDocument doc;
                try
                {
                    doc = JsonDocument.Parse(line);
                }
                catch (JsonException)
                {
                    continue;
                }
                using (doc)
                {
                    Dispatch(doc.RootElement);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            fault = e;
        }
        var closed = new SolidCraftConnectException("the connection to SolidCraft closed" + (fault is null ? "" : ": " + fault.Message), fault);
        foreach (var kv in _pending)
        {
            kv.Value.TrySetException(closed);
        }
        _pending.Clear();
        try { Closed?.Invoke(this, fault); } catch { }
    }

    private void Dispatch(JsonElement root)
    {
        if (root.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number && idEl.TryGetInt64(out var id))
        {
            if (_pending.TryRemove(id, out var tcs))
            {
                tcs.TrySetResult(root.Clone());
            }
            return;
        }
        if (root.TryGetProperty("event", out var ev) && ev.ValueKind == JsonValueKind.String)
        {
            var name = ev.GetString() ?? "";
            var p = root.TryGetProperty("params", out var pe) ? pe.Clone() : default;
            try { EventReceived?.Invoke(this, new ControlEvent(name, p)); } catch { }
            if (name == "design.changed" && p.ValueKind == JsonValueKind.Object)
            {
                try
                {
                    var rev = Json.To<DocumentRevision>(p);
                    DesignChanged?.Invoke(this, new DesignChangedEventArgs(rev));
                }
                catch { }
            }
            return;
        }
        // A reply without an id (the refusal of a bad token, a too-long line) belongs to the one request in flight.
        if (_pending.Count == 1)
        {
            foreach (var kv in _pending)
            {
                if (_pending.TryRemove(kv.Key, out var tcs))
                {
                    tcs.TrySetResult(root.Clone());
                }
            }
        }
    }

    // ---- Lifetime ------------------------------------------------------------------------

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _closing.Cancel();
        try { _tcp.Client?.Shutdown(SocketShutdown.Both); } catch { }
        _tcp.Dispose();
        try { await _readLoop.ConfigureAwait(false); } catch { }
        _closing.Dispose();
        _turn.Dispose();
        _writeLock.Dispose();
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
