using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolidCraft;

/// <summary>The active design and how many changes it has seen (<c>document.revision</c>).</summary>
public sealed record DocumentRevision(
    [property: JsonPropertyName("document_id")] string DocumentId,
    [property: JsonPropertyName("revision")] long Revision);

/// <summary>One entry of <c>engine.commands</c>.</summary>
public sealed record CommandInfo
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("label")] public string Label { get; init; } = "";
    [JsonPropertyName("tab")] public string Tab { get; init; } = "";
    [JsonPropertyName("panel")] public string Panel { get; init; } = "";
    [JsonPropertyName("icon")] public string Icon { get; init; } = "";
    [JsonPropertyName("shortcut")] public string? Shortcut { get; init; }
    /// <summary>One-line parameter documentation.</summary>
    [JsonPropertyName("params")] public string Params { get; init; } = "";
    [JsonPropertyName("enabled")] public bool Enabled { get; init; }
    [JsonPropertyName("disabled_reason")] public string? DisabledReason { get; init; }
    [JsonPropertyName("planned")] public string? Planned { get; init; }
}

/// <summary>One step of an <c>engine.batch</c> or <c>engine.script</c>.</summary>
public sealed record CommandCall(
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("params")] object? Params = null);

/// <summary>Options of <see cref="SolidCraftClient.BatchAsync"/>.</summary>
public sealed record BatchOptions
{
    /// <summary>Undo the steps already run when one fails (default true).</summary>
    public bool Rollback { get; init; } = true;

    /// <summary>Report the time each step took.</summary>
    public bool Timings { get; init; }

    /// <summary>Also report boolean/tessellation/other time per step.</summary>
    public bool Profile { get; init; }

    /// <summary>Set consecutive plain parameter edits as one change, so the design rebuilds once.</summary>
    public bool CoalesceParameters { get; init; }

    /// <summary>Throw <see cref="SolidCraftBatchException"/> when a step fails (default true).</summary>
    public bool ThrowOnFailure { get; init; } = true;
}

/// <summary>The failing step of a batch.</summary>
public sealed record BatchFailure(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("error")] string Error);

/// <summary>How long one batch step took.</summary>
public sealed record BatchTiming(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("ms")] double Ms);

/// <summary>The reply of <c>engine.batch</c>.</summary>
public sealed record BatchResult
{
    /// <summary>Steps that ran (the failing one included).</summary>
    [JsonPropertyName("completed")] public int Completed { get; init; }

    /// <summary>Each completed step's result, in order.</summary>
    [JsonPropertyName("results")] public List<JsonElement> Results { get; init; } = new();

    /// <summary>Steps taken back after a failure (with rollback).</summary>
    [JsonPropertyName("undone")] public int Undone { get; init; }

    [JsonPropertyName("failed")] public BatchFailure? Failed { get; init; }

    /// <summary>With rollback: whether the design is back as it was; <c>false</c> only when the checkpoint could not restore it.</summary>
    [JsonPropertyName("rolled_back")] public bool? RolledBack { get; init; }

    [JsonPropertyName("timings")] public List<BatchTiming>? Timings { get; init; }

    [JsonPropertyName("total_ms")] public double? TotalMs { get; init; }

    [JsonPropertyName("note")] public string? Note { get; init; }

    /// <summary>True when every step ran.</summary>
    [JsonIgnore] public bool Succeeded => Failed is null;
}

/// <summary>A request the app is running or has queued (<c>control.running</c>).</summary>
public sealed record RunningRequest
{
    [JsonPropertyName("connection")] public long Connection { get; init; }
    [JsonPropertyName("id")] public JsonElement Id { get; init; }
    [JsonPropertyName("method")] public string Method { get; init; } = "";
    [JsonPropertyName("command")] public string? Command { get; init; }
    [JsonPropertyName("steps")] public int? Steps { get; init; }
    [JsonPropertyName("running_s")] public double RunningSeconds { get; init; }
    [JsonPropertyName("queued")] public bool Queued { get; init; }
    /// <summary>True for the asking connection's own request.</summary>
    [JsonPropertyName("self")] public bool Self { get; init; }
}

/// <summary>A line the app sends on its own after <c>control.subscribe</c> (<c>{"event": …, "params": …}</c>).</summary>
public sealed record ControlEvent(string Event, JsonElement Params);

/// <summary>The active design changed (an edit, undo, a design opened, a tab switched).</summary>
public sealed class DesignChangedEventArgs : EventArgs
{
    public DesignChangedEventArgs(DocumentRevision revision) => Revision = revision;
    public DocumentRevision Revision { get; }
}

/// <summary>What a sketch command added: ids usable in later commands.</summary>
public sealed record SketchCreated
{
    [JsonPropertyName("points")] public List<string> Points { get; init; } = new();
    [JsonPropertyName("curves")] public List<string> Curves { get; init; } = new();
    [JsonPropertyName("constraints")] public List<string> Constraints { get; init; } = new();
    [JsonPropertyName("dimensions")] public List<string> Dimensions { get; init; } = new();
    [JsonPropertyName("wires")] public List<string>? Wires { get; init; }
}

/// <summary>The reply of a sketch drawing command: the ids it created, plus the raw result.</summary>
public sealed record SketchResult(SketchCreated Created, JsonElement Raw)
{
    /// <summary>The first curve created (a line's <c>l1</c>, a circle's <c>c1</c>), or <c>null</c>.</summary>
    public string? Curve => Created.Curves.Count > 0 ? Created.Curves[0] : null;

    /// <summary>The ids of a rectangle's sides (<c>bottom</c>, <c>right</c>, <c>top</c>, <c>left</c>), when the command returned them.</summary>
    public IReadOnlyDictionary<string, string>? Sides => Json.StringMap(Raw, "sides");

    /// <summary>The ids of a rectangle's corner points, when the command returned them.</summary>
    public IReadOnlyDictionary<string, string>? Corners => Json.StringMap(Raw, "corners");
}

/// <summary>One body of <c>inspect.measure</c>.</summary>
public sealed record BodyMeasure
{
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("closed")] public bool Closed { get; init; }
    [JsonPropertyName("volume_mm3")] public double? VolumeMm3 { get; init; }
    [JsonPropertyName("area_mm2")] public double? AreaMm2 { get; init; }
    [JsonPropertyName("center_of_mass")] public double[]? CenterOfMass { get; init; }
    [JsonPropertyName("bbox")] public BoundingBox? Bbox { get; init; }
    [JsonPropertyName("faces")] public int? Faces { get; init; }
    [JsonPropertyName("edges")] public int? Edges { get; init; }
    [JsonPropertyName("vertices")] public int? Vertices { get; init; }
    [JsonPropertyName("material")] public string? Material { get; init; }
    [JsonPropertyName("density_g_cm3")] public double? DensityGCm3 { get; init; }
    [JsonPropertyName("mass_g")] public double? MassG { get; init; }
    [JsonPropertyName("error")] public string? Error { get; init; }
}

public sealed record BoundingBox(
    [property: JsonPropertyName("min")] double[] Min,
    [property: JsonPropertyName("max")] double[] Max);

/// <summary>One row of a bill of materials (<c>assembly.bom</c>).</summary>
public sealed record BomRow
{
    [JsonPropertyName("item")] public JsonElement Item { get; init; }
    [JsonPropertyName("part_number")] public string? PartNumber { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("revision")] public string? Revision { get; init; }
    [JsonPropertyName("count")] public int Count { get; init; }
    [JsonPropertyName("each")] public int? Each { get; init; }
    [JsonPropertyName("definition")] public string? Definition { get; init; }
    [JsonPropertyName("children")] public List<BomRow>? Children { get; init; }

    /// <summary>The item number as text (<c>1</c>, <c>1.2</c>).</summary>
    [JsonIgnore] public string ItemText => Item.ValueKind == JsonValueKind.String ? Item.GetString() ?? "" : Item.ToString();
}

/// <summary>
/// A number or an expression over the design's parameters (<c>10</c>, <c>"T"</c>, <c>"w/2 - wall"</c>,
/// <c>"50 mm"</c>). Serialized as a JSON number or string.
/// </summary>
[JsonConverter(typeof(ExprJsonConverter))]
public readonly struct Expr
{
    private readonly double? _number;
    private readonly string? _text;

    public Expr(double number) { _number = number; _text = null; }
    public Expr(string expression) { _text = expression; _number = null; }

    public bool IsNumber => _number.HasValue;
    public double Number => _number ?? throw new InvalidOperationException("not a number");
    public string Text => _text ?? throw new InvalidOperationException("not an expression");

    public static implicit operator Expr(double n) => new(n);
    public static implicit operator Expr(int n) => new(n);
    public static implicit operator Expr(string s) => new(s);

    public override string ToString() => _number.HasValue ? _number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : (_text ?? "");

    internal void Write(Utf8JsonWriter w)
    {
        if (_number.HasValue) w.WriteNumberValue(_number.Value);
        else w.WriteStringValue(_text ?? "");
    }
}

public sealed class ExprJsonConverter : JsonConverter<Expr>
{
    public override Expr Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Number ? new Expr(reader.GetDouble()) : new Expr(reader.GetString() ?? "");

    public override void Write(Utf8JsonWriter writer, Expr value, JsonSerializerOptions options) => value.Write(writer);
}

/// <summary>A sketch point: <c>[x, y]</c> in mm, or a point reference such as <c>"l1.end"</c> or <c>"origin"</c>.</summary>
[JsonConverter(typeof(Point2JsonConverter))]
public readonly struct Point2
{
    public double X { get; }
    public double Y { get; }
    /// <summary>A point id instead of coordinates.</summary>
    public string? Ref { get; }

    public Point2(double x, double y) { X = x; Y = y; Ref = null; }
    public Point2(string reference) { X = 0; Y = 0; Ref = reference; }

    public static implicit operator Point2((double X, double Y) p) => new(p.X, p.Y);
    public static implicit operator Point2(string reference) => new(reference);
    public static implicit operator Point2(double[] xy) => new(xy[0], xy[1]);

    public override string ToString() => Ref ?? $"[{X}, {Y}]";
}

public sealed class Point2JsonConverter : JsonConverter<Point2>
{
    public override Point2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String) return new Point2(reader.GetString() ?? "");
        var arr = JsonSerializer.Deserialize<double[]>(ref reader, options) ?? Array.Empty<double>();
        return new Point2(arr.Length > 0 ? arr[0] : 0, arr.Length > 1 ? arr[1] : 0);
    }

    public override void Write(Utf8JsonWriter writer, Point2 value, JsonSerializerOptions options)
    {
        if (value.Ref is not null) { writer.WriteStringValue(value.Ref); return; }
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteEndArray();
    }
}

/// <summary>A world point or direction <c>[x, y, z]</c> in mm.</summary>
[JsonConverter(typeof(Point3JsonConverter))]
public readonly struct Point3
{
    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public Point3(double x, double y, double z) { X = x; Y = y; Z = z; }

    public static implicit operator Point3((double X, double Y, double Z) p) => new(p.X, p.Y, p.Z);
    public static implicit operator Point3(double[] xyz) => new(xyz[0], xyz[1], xyz[2]);

    public static readonly Point3 Zero = new(0, 0, 0);
    public static readonly Point3 UnitX = new(1, 0, 0);
    public static readonly Point3 UnitY = new(0, 1, 0);
    public static readonly Point3 UnitZ = new(0, 0, 1);

    public override string ToString() => $"[{X}, {Y}, {Z}]";
}

public sealed class Point3JsonConverter : JsonConverter<Point3>
{
    public override Point3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var arr = JsonSerializer.Deserialize<double[]>(ref reader, options) ?? Array.Empty<double>();
        return new Point3(arr.Length > 0 ? arr[0] : 0, arr.Length > 1 ? arr[1] : 0, arr.Length > 2 ? arr[2] : 0);
    }

    public override void Write(Utf8JsonWriter writer, Point3 value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteNumberValue(value.Z);
        writer.WriteEndArray();
    }
}

/// <summary>JSON helpers shared by the client and the typed helpers.</summary>
public static class Json
{
    /// <summary>The serializer options the client uses: null properties are left out, enums as camelCase text.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>Deserialize an element, or throw a <see cref="SolidCraftException"/> naming what was expected.</summary>
    public static T To<T>(JsonElement element)
    {
        try
        {
            var value = element.Deserialize<T>(Options);
            return value ?? throw new SolidCraftException($"the reply is null; expected {typeof(T).Name}");
        }
        catch (JsonException e)
        {
            throw new SolidCraftException($"the reply does not fit {typeof(T).Name}: {e.Message}\n{element}", e);
        }
    }

    /// <summary>A property as text, or <c>null</c> when absent or not a string.</summary>
    public static string? String(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>A property as a number, or <c>null</c>.</summary>
    public static double? Number(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    /// <summary>A property as a boolean, or <c>null</c>.</summary>
    public static bool? Bool(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var v)
            ? v.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null }
            : null;

    /// <summary>An object property whose values are strings, or <c>null</c>.</summary>
    public static IReadOnlyDictionary<string, string>? StringMap(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var v) || v.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        var map = new Dictionary<string, string>();
        foreach (var p in v.EnumerateObject())
        {
            if (p.Value.ValueKind == JsonValueKind.String) map[p.Name] = p.Value.GetString() ?? "";
        }
        return map;
    }

    /// <summary>A property that is a list of strings, or an empty list.</summary>
    public static List<string> Strings(JsonElement element, string property)
    {
        var list = new List<string>();
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in v.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String) list.Add(item.GetString() ?? "");
            }
        }
        return list;
    }

    /// <summary>Build a params object from name/value pairs, leaving out <c>null</c> values.</summary>
    public static Dictionary<string, object?> Params(params (string Name, object? Value)[] pairs)
    {
        var d = new Dictionary<string, object?>();
        foreach (var (name, value) in pairs)
        {
            if (value is not null) d[name] = value;
        }
        return d;
    }
}
