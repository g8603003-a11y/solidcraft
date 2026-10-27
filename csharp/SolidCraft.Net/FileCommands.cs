using System.Text.Json;

namespace SolidCraft;

/// <summary>Designs on disk: new, open, save, export, import. Paths are on the computer SolidCraft runs on.</summary>
public sealed class FileCommands
{
    private readonly SolidCraftClient _c;
    internal FileCommands(SolidCraftClient c) => _c = c;

    /// <summary>Start a new, empty design (in a new tab).</summary>
    public Task<JsonElement> NewAsync(string? name = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.File.New, Json.Params(("name", name)), ct);

    /// <summary>
    /// Open a <c>.solidcraft</c> design, or import a STEP/IGES/3MF/STL/OBJ/glTF/DXF/SVG file as a new design.
    /// Goes through the window's file loading, so it can take a while for a big file.
    /// </summary>
    public Task<JsonElement> OpenAsync(string path, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Doc.Open, Json.Params(("path", path)), ct);

    /// <summary>Save the design to its file (or to <paramref name="path"/>). <paramref name="overwrite"/> replaces a file another program changed meanwhile.</summary>
    public Task<JsonElement> SaveAsync(string? path = null, bool? overwrite = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.File.Save, Json.Params(("path", path), ("overwrite", overwrite)), ct);

    /// <summary>Save the design under a new name; the tab follows the new file.</summary>
    public Task<JsonElement> SaveAsAsync(string path, bool? overwrite = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.File.SaveAs, Json.Params(("path", path), ("overwrite", overwrite)), ct);

    /// <summary>
    /// Export bodies: <paramref name="format"/> <c>step</c>, <c>stl</c>, <c>stla</c>, <c>obj</c>, <c>3mf</c>, <c>3mf-tagged</c>, <c>urdf</c>
    /// or <c>json</c> (default from the extension). <paramref name="quality"/> for meshes: <c>standard</c>, <c>fine</c>, <c>finest</c>.
    /// </summary>
    public Task<JsonElement> ExportAsync(string path, string? format = null, IEnumerable<string>? bodies = null, string? quality = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.File.Export, Json.Params(("path", path), ("format", format), ("bodies", bodies?.ToArray()), ("quality", quality)), ct);

    /// <summary>Export to STEP.</summary>
    public Task<JsonElement> ExportStepAsync(string path, IEnumerable<string>? bodies = null, CancellationToken ct = default) =>
        ExportAsync(path, "step", bodies, null, ct);

    /// <summary>Export a mesh (STL by default); <paramref name="quality"/>: <c>standard</c>, <c>fine</c>, <c>finest</c>.</summary>
    public Task<JsonElement> ExportStlAsync(string path, IEnumerable<string>? bodies = null, string? quality = null, CancellationToken ct = default) =>
        ExportAsync(path, "stl", bodies, quality, ct);

    /// <summary>Insert a STEP, IGES, FreeCAD or Rhino file as a new component in the design.</summary>
    public Task<JsonElement> InsertStepAsync(string path, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.File.InsertStep, Json.Params(("path", path)), ct);

    /// <summary>Insert a 3MF, STL, OBJ or glTF file as mesh bodies in a new component.</summary>
    public Task<JsonElement> InsertMeshAsync(string path, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.File.InsertMesh, Json.Params(("path", path)), ct);

    /// <summary>Export the sheet-metal flat pattern of a body as DXF.</summary>
    public Task<JsonElement> ExportFlatPatternDxfAsync(string path, string? body = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Sheet.ExportDxf, Json.Params(("path", path), ("body", body)), ct);
}

/// <summary>Reading the design: measurements, physical properties, bill of materials, interference.</summary>
public sealed class InspectCommands
{
    private readonly SolidCraftClient _c;
    internal InspectCommands(SolidCraftClient c) => _c = c;

    /// <summary>Volume, area, centre of mass, bounding box, counts and (from the material) mass of each body.</summary>
    public async Task<IReadOnlyList<BodyMeasure>> MeasureAsync(IEnumerable<string>? bodies = null, CancellationToken ct = default)
    {
        var r = await _c.ExecuteAsync(Commands.Inspect.Measure, Json.Params(("bodies", bodies?.ToArray())), ct).ConfigureAwait(false);
        return r.ValueKind == JsonValueKind.Object && r.TryGetProperty("bodies", out var list) ? Json.To<List<BodyMeasure>>(list) : new List<BodyMeasure>();
    }

    /// <summary>The raw <c>inspect.measure</c> reply: <c>bodies</c> and <c>total</c>.</summary>
    public Task<JsonElement> MeasureRawAsync(IEnumerable<string>? bodies = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Inspect.Measure, Json.Params(("bodies", bodies?.ToArray())), ct);

    /// <summary>Material-aware mass, centre of mass and volume of a component (the active one by default) in world coordinates.</summary>
    public Task<JsonElement> ComponentPropertiesAsync(string? component = null, bool? descendants = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Component.Properties, Json.Params(("component", component), ("descendants", descendants)), ct);

    /// <summary>
    /// The bill of materials. <paramref name="levels"/>: <c>flat</c> (every part at any depth, default) or <c>top</c>;
    /// <paramref name="path"/> also writes a .csv or .json file.
    /// </summary>
    public async Task<IReadOnlyList<BomRow>> BomAsync(string? component = null, string? levels = null, bool? groupChildren = null, string? path = null, CancellationToken ct = default)
    {
        var r = await BomRawAsync(component, levels, groupChildren, path, ct).ConfigureAwait(false);
        return r.ValueKind == JsonValueKind.Object && r.TryGetProperty("rows", out var rows) ? Json.To<List<BomRow>>(rows) : new List<BomRow>();
    }

    /// <summary>The raw <c>assembly.bom</c> reply (<c>rows</c>, <c>total_occurrences</c>, <c>group_by</c>).</summary>
    public Task<JsonElement> BomRawAsync(string? component = null, string? levels = null, bool? groupChildren = null, string? path = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Assembly.Bom, Json.Params(("component", component), ("levels", levels), ("group_children", groupChildren), ("path", path)), ct);

    /// <summary>Pairs of bodies that overlap, with the overlap volume.</summary>
    public Task<JsonElement> InterferenceAsync(IEnumerable<string>? bodies = null, double? budgetSeconds = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Inspect.Interference, Json.Params(("bodies", bodies?.ToArray()), ("budget_s", budgetSeconds)), ct);

    /// <summary>The centre of mass (uniform density) of bodies and of all together.</summary>
    public Task<JsonElement> CenterOfMassAsync(IEnumerable<string>? bodies = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Inspect.CenterOfMass, Json.Params(("bodies", bodies?.ToArray())), ct);

    /// <summary>Components, their bodies, features, sketches and occurrences.</summary>
    public Task<JsonElement> ComponentsAsync(bool compact = false, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Component.List, Json.Params(("compact", compact ? (object)true : null)), ct);

    /// <summary>The faces, edges and vertices of a body with their types and positions.</summary>
    public Task<JsonElement> BodyTopologyAsync(string body, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Model.Faces, Json.Params(("body", body)), ct);

    /// <summary>The names of the bodies in the design (from <c>document.inspect</c>).</summary>
    public async Task<IReadOnlyList<string>> BodyNamesAsync(CancellationToken ct = default)
    {
        var doc = await _c.InspectDocumentAsync(false, ct).ConfigureAwait(false);
        var names = new List<string>();
        if (doc.ValueKind == JsonValueKind.Object && doc.TryGetProperty("bodies", out var bodies) && bodies.ValueKind == JsonValueKind.Array)
        {
            foreach (var b in bodies.EnumerateArray())
            {
                var n = b.ValueKind == JsonValueKind.String ? b.GetString() : Json.String(b, "name");
                if (!string.IsNullOrEmpty(n)) names.Add(n);
            }
        }
        return names;
    }
}

/// <summary>The design as a whole: properties (part number, revision…), units, materials, components.</summary>
public sealed class DesignCommands
{
    private readonly SolidCraftClient _c;
    internal DesignCommands(SolidCraftClient c) => _c = c;

    /// <summary>The design properties SolidCraft knows (<c>project</c>, <c>part_number</c>, <c>revision</c>, <c>author</c>, <c>organization</c>, <c>description</c>, <c>license</c>).</summary>
    public static readonly string[] PropertyKeys = { "project", "part_number", "revision", "author", "organization", "description", "license" };

    /// <summary>The design's properties: each key's text, or <c>null</c>. Other tools' metadata is under <c>metadata</c> of <see cref="MetadataAsync"/>.</summary>
    public async Task<IReadOnlyDictionary<string, string?>> PropertiesAsync(CancellationToken ct = default)
    {
        var r = await MetadataAsync(ct).ConfigureAwait(false);
        var map = new Dictionary<string, string?>();
        if (r.ValueKind == JsonValueKind.Object && r.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in props.EnumerateObject())
            {
                map[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : null;
            }
        }
        return map;
    }

    /// <summary>The raw <c>document.metadata</c> reply: <c>metadata</c> (everything saved, other tools' keys too) and <c>properties</c>.</summary>
    public Task<JsonElement> MetadataAsync(CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Document.Metadata, null, ct);

    /// <summary>Set one design property (<c>""</c> or <c>null</c> removes it).</summary>
    public Task<JsonElement> SetPropertyAsync(string key, string? value, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Document.MetadataSet, Json.Params(("key", key), ("value", value ?? "")), ct);

    /// <summary>Set several properties in one undo step (all or nothing). A reverse-DNS key such as <c>com.acme.erp</c> may hold an object of your own.</summary>
    public Task<JsonElement> SetPropertiesAsync(IReadOnlyDictionary<string, object?> values, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Document.MetadataSet, Json.Params(("values", values)), ct);

    /// <summary>Set a component's part number, description and revision.</summary>
    public Task<JsonElement> SetComponentPropertiesAsync(string component, string? partNumber = null, string? description = null, string? revision = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Component.MetadataSet, Json.Params(("component", component), ("part_number", partNumber), ("description", description), ("revision", revision)), ct);

    /// <summary>The design's display units: <c>mm</c>, <c>cm</c>, <c>m</c>, <c>in</c>, <c>ft</c>.</summary>
    public Task<JsonElement> SetUnitsAsync(string units, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Document.Units, Json.Params(("units", units)), ct);

    /// <summary>Built-in and design-owned physical materials.</summary>
    public Task<JsonElement> MaterialsAsync(CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Material.List, null, ct);

    /// <summary>A new physical material with a density in g/cm³ and an optional colour (<c>"#rrggbb"</c>).</summary>
    public Task<JsonElement> CreateMaterialAsync(string name, double densityGCm3, string? color = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Material.Create, Json.Params(("name", name), ("density_g_cm3", densityGCm3), ("color", color)), ct);

    /// <summary>A new component (activated by default), under the active one or <paramref name="parent"/>.</summary>
    public Task<JsonElement> CreateComponentAsync(string? name = null, string? parent = null, bool? activate = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Component.Create, Json.Params(("name", name), ("parent", parent), ("activate", activate)), ct);

    /// <summary>Make a component the active one (<c>"root"</c> for the design).</summary>
    public Task<JsonElement> ActivateComponentAsync(string component, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Component.Activate, Json.Params(("component", component)), ct);

    /// <summary>Rename a component.</summary>
    public Task<JsonElement> RenameComponentAsync(string component, string newName, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Component.Rename, Json.Params(("component", component), ("name", newName)), ct);

    /// <summary>Place another design file as a linked component.</summary>
    public Task<JsonElement> InsertDesignAsync(string path, string? name = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Component.InsertDesign, Json.Params(("path", path), ("name", name)), ct);
}
