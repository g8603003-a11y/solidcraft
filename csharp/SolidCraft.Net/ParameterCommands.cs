using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolidCraft;

/// <summary>One parameter as <c>parameters.list</c> reports it.</summary>
public sealed record ParameterInfo
{
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("expression")] public string? Expression { get; init; }
    [JsonPropertyName("unit")] public string? Unit { get; init; }
    [JsonPropertyName("value")] public JsonElement Value { get; init; }
    [JsonPropertyName("source")] public string? Source { get; init; }
    [JsonPropertyName("comment")] public string? Comment { get; init; }
    [JsonPropertyName("favorite")] public bool? Favorite { get; init; }
    [JsonPropertyName("error")] public string? Error { get; init; }

    /// <summary>The value as a number, when it is one.</summary>
    [JsonIgnore] public double? Number => Value.ValueKind == JsonValueKind.Number ? Value.GetDouble() : null;
}

/// <summary>User parameters: the numbers a design is driven by. Expressions carry units (<c>"80 mm"</c>, <c>"30 deg"</c>, <c>"L/2 - inset"</c>).</summary>
public sealed class ParameterCommands
{
    private readonly SolidCraftClient _c;
    internal ParameterCommands(SolidCraftClient c) => _c = c;

    /// <summary>Add a user parameter. <paramref name="unit"/>: <c>mm</c>, <c>cm</c>, <c>m</c>, <c>in</c>, <c>ft</c>, <c>deg</c>, <c>rad</c>, <c>""</c> (unitless) or <c>text</c>; default from the expression.</summary>
    public Task<JsonElement> AddAsync(string name, Expr expression, string? unit = null, string? comment = null, bool? favorite = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Parameters.Add, Json.Params(("name", name), ("expression", expression), ("unit", unit), ("comment", comment), ("favorite", favorite)), ct);

    /// <summary>Set one parameter's expression (the design rebuilds).</summary>
    public Task<JsonElement> SetAsync(string name, Expr expression, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Parameters.Change, Json.Params(("name", name), ("expression", expression)), ct);

    /// <summary>Set several existing parameters at once, checked together and rebuilt once (all or nothing).</summary>
    public Task<JsonElement> SetManyAsync(IReadOnlyDictionary<string, Expr> changes, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Parameters.Change, Json.Params(("changes", changes)), ct);

    /// <summary>Rename a parameter; expressions, feature inputs and dimensions that use it follow.</summary>
    public Task<JsonElement> RenameAsync(string name, string newName, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Parameters.Rename, Json.Params(("name", name), ("new_name", newName)), ct);

    /// <summary>Delete a parameter (refused while something uses it).</summary>
    public Task<JsonElement> DeleteAsync(string name, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Parameters.Delete, Json.Params(("name", name)), ct);

    /// <summary>The parameters with their values. <paramref name="filter"/>: <c>all</c>, <c>user</c>, <c>sketch</c>, <c>feature</c>, <c>favorites</c>.</summary>
    public async Task<IReadOnlyList<ParameterInfo>> ListAsync(string? filter = null, CancellationToken ct = default)
    {
        var r = await _c.ExecuteAsync(Commands.Parameters.List, Json.Params(("filter", filter)), ct).ConfigureAwait(false);
        return r.ValueKind == JsonValueKind.Object && r.TryGetProperty("parameters", out var list) ? Json.To<List<ParameterInfo>>(list) : new List<ParameterInfo>();
    }

    /// <summary>One parameter's current value, or <c>null</c> when there is no such parameter.</summary>
    public async Task<ParameterInfo?> GetAsync(string name, CancellationToken ct = default)
    {
        var all = await ListAsync(null, ct).ConfigureAwait(false);
        return all.FirstOrDefault(p => p.Name == name);
    }

    /// <summary>Write the parameters as CSV or JSON text (and to <paramref name="path"/> when given).</summary>
    public Task<JsonElement> ExportAsync(string? path = null, string? format = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Parameters.Export, Json.Params(("path", path), ("format", format)), ct);

    /// <summary>Import parameters from CSV/JSON text (all or nothing).</summary>
    public Task<JsonElement> ImportTextAsync(string text, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Parameters.Import, Json.Params(("text", text)), ct);

    /// <summary>Import parameters from a CSV or JSON file (all or nothing).</summary>
    public Task<JsonElement> ImportFileAsync(string path, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Parameters.Import, Json.Params(("path", path)), ct);
}
