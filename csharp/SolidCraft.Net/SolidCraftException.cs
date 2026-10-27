namespace SolidCraft;

/// <summary>A request the app refused or that failed (<c>{"ok": false, "error": …}</c>).</summary>
public class SolidCraftException : Exception
{
    public SolidCraftException(string message) : base(message) { }

    public SolidCraftException(string message, Exception? inner) : base(message, inner) { }

    /// <summary>The control method that failed (<c>engine.execute</c>, <c>ui.view</c>, …).</summary>
    public string? Method { get; init; }

    /// <summary>The engine command, when the method was <c>engine.execute</c>.</summary>
    public string? Command { get; init; }

    /// <summary>The app's error text, as sent.</summary>
    public string Error => Message;

    /// <summary>
    /// The request is still running in the app (the server's 60 s wait passed). It was not
    /// cancelled, so don't send it again; <see cref="SolidCraftClient.WaitForLongRunning"/> makes
    /// the client wait for the answer instead of throwing this.
    /// </summary>
    public bool IsStillRunning { get; init; }

    /// <summary>
    /// Refused at once because an earlier request on this connection is still running: nothing was run.
    /// </summary>
    public bool IsBusy { get; init; }

    /// <summary>The person pressed Stop on the command's chip in the window; nothing was applied.</summary>
    public bool IsStopped { get; init; }

    /// <summary>The token was refused: the app was restarted (new token) or the token file is stale.</summary>
    public bool IsUnauthorized { get; init; }
}

/// <summary>A step of an <c>engine.batch</c> failed; the batch stopped there.</summary>
public sealed class SolidCraftBatchException : SolidCraftException
{
    public SolidCraftBatchException(string message, BatchResult result) : base(message)
    {
        Result = result;
        Method = "engine.batch";
        Command = result.Failed?.Command;
    }

    /// <summary>What ran, what was undone, and the failing step.</summary>
    public BatchResult Result { get; }
}

/// <summary>The app could not be found or reached (not running, control channel off, no token).</summary>
public sealed class SolidCraftConnectException : SolidCraftException
{
    public SolidCraftConnectException(string message, Exception? inner = null) : base(message, inner) { }
}
