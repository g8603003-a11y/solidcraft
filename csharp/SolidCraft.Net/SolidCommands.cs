using System.Text.Json;

namespace SolidCraft;

/// <summary>How a feature combines with existing bodies.</summary>
public enum Operation
{
    /// <summary>A new body.</summary>
    New,
    /// <summary>Join existing solids.</summary>
    Join,
    /// <summary>Remove material from a body.</summary>
    Cut,
    Intersect,
    /// <summary>Cut into a body, otherwise join; new in an empty component.</summary>
    Auto,
}

/// <summary>The extrude direction relative to the sketch normal.</summary>
public enum Direction
{
    Positive,
    Negative,
    /// <summary>Both ways: the distance is the total thickness, split evenly around the sketch plane.</summary>
    Symmetric,
}

/// <summary>Options of <see cref="SolidCommands.ExtrudeAsync"/>; only <c>Distance</c> or <c>ThroughAll</c> is needed.</summary>
public sealed record ExtrudeOptions
{
    public Expr? Distance { get; init; }
    public bool ThroughAll { get; init; }
    public Operation? Operation { get; init; }
    public Direction? Direction { get; init; }
    /// <summary>The sketch to extrude (default: the active or last sketch).</summary>
    public string? Sketch { get; init; }
    /// <summary>
    /// Which profiles: <c>null</c> for every drawn region (a region inside another is a hole), or an array of indices,
    /// of <c>new { point = new[]{x, y} }</c> picks, or of <c>new { curves = new[]{"l1", …} }</c>.
    /// </summary>
    public object? Profiles { get; init; }
    /// <summary>Taper angle: positive outward.</summary>
    public Expr? Taper { get; init; }
    /// <summary>Start along the sketch normal.</summary>
    public Expr? StartOffset { get; init; }
    /// <summary>Two Sides: a second distance the other way.</summary>
    public Expr? Distance2 { get; init; }
    /// <summary>Extrude a planar body face instead of a sketch profile.</summary>
    public Point3? Face { get; init; }
    /// <summary>Bodies a cut or join applies to (default: the body under the sketch, or all).</summary>
    public string[]? Targets { get; init; }
    public string? Name { get; init; }
    public string? BodyName { get; init; }
    /// <summary>Thin: a wall of this thickness along the sketch curves instead of the profiles.</summary>
    public Expr? Thickness { get; init; }
    /// <summary>Thin wall side: <c>center</c> (default), <c>inside</c>, <c>outside</c>, <c>two_sides</c>.</summary>
    public string? Side { get; init; }
}

/// <summary>Options of <see cref="SolidCommands.HoleAsync"/>: a position on a face, or a sketch of hole centres.</summary>
public sealed record HoleOptions
{
    /// <summary>A point on the face the hole starts from (numbers; or expressions through <see cref="PositionExpr"/>).</summary>
    public Point3? Position { get; init; }
    /// <summary>A position whose coordinates are expressions the hole follows, e.g. <c>["40", "width/2", "t"]</c>.</summary>
    public Expr[]? PositionExpr { get; init; }
    /// <summary>A sketch whose points (and circle centres) are the hole centres.</summary>
    public string? Sketch { get; init; }
    /// <summary>Only these points of the sketch.</summary>
    public string[]? Points { get; init; }
    public Expr? Diameter { get; init; }
    /// <summary>A standard clearance instead of a diameter: <c>"M3"</c>, <c>"M5"</c>.</summary>
    public string? Clearance { get; init; }
    /// <summary>Default: through all.</summary>
    public Expr? Depth { get; init; }
    /// <summary><c>simple</c> (default), <c>drilled</c>, <c>counterbore</c>, <c>countersink</c>.</summary>
    public string? Type { get; init; }
    public Expr? CounterboreDiameter { get; init; }
    public Expr? CounterboreDepth { get; init; }
    public Expr? CountersinkDiameter { get; init; }
    public Expr? CountersinkAngle { get; init; }
    /// <summary>Sketch points: drill to the other side of the sketch plane.</summary>
    public bool? Flip { get; init; }
    public string? Name { get; init; }
}

/// <summary>Solid modelling: primitives, features from sketches, fillets, holes, patterns, mirrors, bodies.</summary>
public sealed class SolidCommands
{
    private readonly SolidCraftClient _c;
    internal SolidCommands(SolidCraftClient c) => _c = c;

    private static string? Op(Operation? op) => op?.ToString().ToLowerInvariant();
    private static string? Dir(Direction? d) => d?.ToString().ToLowerInvariant();

    /// <summary>Extrude the active (or named) sketch's profiles.</summary>
    public Task<JsonElement> ExtrudeAsync(ExtrudeOptions o, CancellationToken ct = default)
    {
        if (o.Distance is null && !o.ThroughAll)
        {
            throw new ArgumentException("an extrude needs Distance or ThroughAll", nameof(o));
        }
        var p = Json.Params(
            ("distance", o.Distance), ("through_all", o.ThroughAll ? (object)true : null), ("operation", Op(o.Operation)), ("direction", Dir(o.Direction)),
            ("sketch", o.Sketch), ("profiles", o.Profiles), ("taper", o.Taper), ("start_offset", o.StartOffset), ("distance2", o.Distance2),
            ("face", o.Face), ("targets", o.Targets), ("name", o.Name), ("body_name", o.BodyName));
        if (o.Thickness is not null)
        {
            p["thin"] = true;
            p["thickness"] = o.Thickness;
            if (o.Side is not null) p["side"] = o.Side;
        }
        return _c.ExecuteAsync(Commands.Solid.Extrude, p, ct);
    }

    /// <summary>Extrude by a distance; the common case.</summary>
    public Task<JsonElement> ExtrudeAsync(Expr distance, Operation? operation = null, string? name = null, string? sketch = null, CancellationToken ct = default) =>
        ExtrudeAsync(new ExtrudeOptions { Distance = distance, Operation = operation, Name = name, Sketch = sketch }, ct);

    /// <summary>Extrude a cut through everything.</summary>
    public Task<JsonElement> CutThroughAllAsync(string? name = null, string? sketch = null, string[]? targets = null, CancellationToken ct = default) =>
        ExtrudeAsync(new ExtrudeOptions { ThroughAll = true, Operation = Operation.Cut, Name = name, Sketch = sketch, Targets = targets }, ct);

    /// <summary>
    /// Revolve the sketch's profiles about an axis: a sketch line id, <c>"x"</c>/<c>"y"</c> (sketch axes), <c>"X"</c>/<c>"Y"</c>/<c>"Z"</c>
    /// (world), a construction axis name, or <c>new { origin = …, dir = … }</c>.
    /// </summary>
    public Task<JsonElement> RevolveAsync(object axis, Expr? angle = null, Operation? operation = null, string? name = null, string? sketch = null, object? profiles = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Solid.Revolve, Json.Params(("axis", axis), ("angle", angle), ("operation", Op(operation)), ("name", name), ("sketch", sketch), ("profiles", profiles)), ct);

    /// <summary>A box; <paramref name="center"/> or <paramref name="corner"/> places it (default: at the origin).</summary>
    public Task<JsonElement> BoxAsync(Expr length, Expr width, Expr height, Point3? center = null, Point3? corner = null, Operation? operation = null, string? name = null, string? bodyName = null, string[]? targets = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Solid.Box, Json.Params(("length", length), ("width", width), ("height", height), ("center", center), ("corner", corner), ("operation", Op(operation)), ("name", name), ("body_name", bodyName), ("targets", targets)), ct);

    /// <summary>A cylinder by radius and height; <paramref name="baseCenter"/> is the centre of its bottom, <paramref name="axis"/> <c>"X"</c>, <c>"Y"</c> or <c>"Z"</c> (default).</summary>
    public Task<JsonElement> CylinderAsync(Expr radius, Expr height, Point3? baseCenter = null, string? axis = null, Operation? operation = null, string? name = null, string? bodyName = null, string[]? targets = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Solid.Cylinder, Json.Params(("radius", radius), ("height", height), ("base", baseCenter), ("axis", axis), ("operation", Op(operation)), ("name", name), ("body_name", bodyName), ("targets", targets)), ct);

    /// <summary>A cylinder by diameter and height, with the base centre given as expressions (e.g. <c>["bcd/2", "0", "-1"]</c>).</summary>
    public Task<JsonElement> CylinderAsync(Expr diameter, Expr height, Expr[] baseCenter, string? axis = null, Operation? operation = null, string? name = null, string? bodyName = null, string[]? targets = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Solid.Cylinder, Json.Params(("diameter", diameter), ("height", height), ("base", baseCenter), ("axis", axis), ("operation", Op(operation)), ("name", name), ("body_name", bodyName), ("targets", targets)), ct);

    /// <summary>Round the edges nearest each point (one pick takes the whole tangent chain).</summary>
    public Task<JsonElement> FilletAsync(IEnumerable<Point3> edges, Expr radius, string? name = null, string? body = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Solid.Fillet, Json.Params(("edges", edges.ToArray()), ("radius", radius), ("name", name), ("body", body)), ct);

    /// <summary>Chamfer the edges nearest each point.</summary>
    public Task<JsonElement> ChamferAsync(IEnumerable<Point3> edges, Expr distance, Expr? distance2 = null, Expr? angle = null, string? name = null, string? body = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Solid.Chamfer, Json.Params(("edges", edges.ToArray()), ("distance", distance), ("distance2", distance2), ("angle", angle), ("name", name), ("body", body)), ct);

    /// <summary>Hollow a body, removing the faces at the given points. <paramref name="direction"/>: <c>inside</c> (default), <c>outside</c>, <c>both</c>.</summary>
    public Task<JsonElement> ShellAsync(IEnumerable<Point3> facesToRemove, Expr thickness, string? direction = null, string? name = null, string? body = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Solid.Shell, Json.Params(("faces", facesToRemove.ToArray()), ("thickness", thickness), ("direction", direction), ("name", name), ("body", body)), ct);

    /// <summary>Drill holes at a position on a face or at a sketch's points.</summary>
    public Task<JsonElement> HoleAsync(HoleOptions o, CancellationToken ct = default)
    {
        object? position = null;
        if (o.PositionExpr is not null) position = o.PositionExpr;
        else if (o.Position is not null) position = o.Position.Value;
        var p = Json.Params(
            ("position", position), ("sketch", o.Sketch), ("points", o.Points), ("diameter", o.Diameter), ("clearance", o.Clearance), ("depth", o.Depth),
            ("type", o.Type), ("cb_diameter", o.CounterboreDiameter), ("cb_depth", o.CounterboreDepth), ("cs_diameter", o.CountersinkDiameter),
            ("cs_angle", o.CountersinkAngle), ("flip", o.Flip), ("name", o.Name));
        return _c.ExecuteAsync(Commands.Solid.Hole, p, ct);
    }

    /// <summary>A simple hole of a diameter at a point on a face, through all by default.</summary>
    public Task<JsonElement> HoleAsync(Point3 position, Expr diameter, Expr? depth = null, string? name = null, CancellationToken ct = default) =>
        HoleAsync(new HoleOptions { Position = position, Diameter = diameter, Depth = depth, Name = name }, ct);

    /// <summary>Add an ISO metric thread to a cylindrical face (cosmetic unless <paramref name="modeled"/>).</summary>
    public Task<JsonElement> ThreadAsync(Point3 face, string? designation = null, Expr? length = null, bool? modeled = null, string? name = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Solid.Thread, Json.Params(("face", face), ("designation", designation), ("length", length), ("modeled", modeled), ("name", name)), ct);

    /// <summary>Repeat features along one or two directions. Spacing takes expressions; counts include the original.</summary>
    public Task<JsonElement> PatternRectangularAsync(IEnumerable<string> features, Point3 dir1, Expr count1, Expr spacing1, Point3? dir2 = null, Expr? count2 = null, Expr? spacing2 = null, string? name = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Solid.PatternRectangular, Json.Params(("features", features.ToArray()), ("dir1", dir1), ("count1", count1), ("spacing1", spacing1), ("dir2", dir2), ("count2", count2), ("spacing2", spacing2), ("name", name)), ct);

    /// <summary>Repeat features about an axis: <c>"X"</c>/<c>"Y"</c>/<c>"Z"</c>, a construction axis name, <c>new { origin, dir }</c>, or <c>new { face = new[]{x,y,z} }</c>.</summary>
    public Task<JsonElement> PatternCircularAsync(IEnumerable<string> features, object axis, Expr count, Expr? angle = null, string? name = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Solid.PatternCircular, Json.Params(("features", features.ToArray()), ("axis", axis), ("count", count), ("angle", angle), ("name", name)), ct);

    /// <summary>Mirror features across a plane: <c>"XY"</c>, <c>"XZ"</c>, <c>"YZ"</c>, a construction plane name, or <c>new { origin, normal }</c>.</summary>
    public Task<JsonElement> MirrorFeaturesAsync(IEnumerable<string> features, object plane, string? name = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Solid.Mirror, Json.Params(("features", features.ToArray()), ("plane", plane), ("name", name)), ct);

    /// <summary>Mirror finished bodies across a plane; <paramref name="combine"/> joins an overlapping mirror with its original.</summary>
    public Task<JsonElement> MirrorBodiesAsync(IEnumerable<string> bodies, object plane, bool? combine = null, string? name = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Solid.Mirror, Json.Params(("bodies", bodies.ToArray()), ("plane", plane), ("combine", combine), ("name", name)), ct);

    /// <summary>Join, cut or intersect tool bodies with a target body.</summary>
    public Task<JsonElement> CombineAsync(string target, IEnumerable<string> tools, Operation operation = Operation.Join, bool? keepTools = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Solid.Combine, Json.Params(("target", target), ("tools", tools.ToArray()), ("operation", Op(operation)), ("keep_tools", keepTools)), ct);

    /// <summary>Rename a body; features that use it follow.</summary>
    public Task<JsonElement> RenameBodyAsync(string body, string newName, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Body.Rename, Json.Params(("body", body), ("name", newName)), ct);

    /// <summary>Assign a physical material (<c>Steel</c>, <c>Aluminum</c>, <c>ABS Plastic</c>, or one made with <c>material.create</c>) to bodies.</summary>
    public Task<JsonElement> AssignMaterialAsync(IEnumerable<string> bodies, string material, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Material.Assign, Json.Params(("bodies", bodies.ToArray()), ("material", material)), ct);

    // ---- Timeline --------------------------------------------------------------------------

    /// <summary>Suppress (or unsuppress) features.</summary>
    public Task<JsonElement> SuppressAsync(IEnumerable<string> features, bool? suppressed = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Timeline.Suppress, Json.Params(("features", features.ToArray()), ("suppressed", suppressed)), ct);

    /// <summary>Delete features (a sketch takes its dependent features with it).</summary>
    public Task<JsonElement> DeleteFeaturesAsync(IEnumerable<string> features, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Timeline.Delete, Json.Params(("features", features.ToArray())), ct);

    /// <summary>Change fields of a feature's definition, as <c>timeline.inspect</c> returns them: <c>new { extent = new { distance = "30" } }</c>, or dotted keys.</summary>
    public Task<JsonElement> EditFeatureAsync(string feature, object set, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Timeline.Edit, Json.Params(("feature", feature), ("set", set)), ct);

    /// <summary>Move the rollback marker right after a feature.</summary>
    public Task<JsonElement> RollToAsync(string feature, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Timeline.RollTo, Json.Params(("feature", feature)), ct);

    /// <summary>Move the rollback marker to the end (every feature computed).</summary>
    public Task<JsonElement> RollToEndAsync(CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Timeline.RollTo, Json.Params(("position", "end")), ct);

    /// <summary>A feature's definition and state.</summary>
    public Task<JsonElement> InspectFeatureAsync(string feature, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Timeline.Inspect, Json.Params(("feature", feature)), ct);

    /// <summary>Undo the last change.</summary>
    public Task<JsonElement> UndoAsync(CancellationToken ct = default) => _c.ExecuteAsync(Commands.Edit.Undo, null, ct);

    /// <summary>Redo the last undone change.</summary>
    public Task<JsonElement> RedoAsync(CancellationToken ct = default) => _c.ExecuteAsync(Commands.Edit.Redo, null, ct);
}
