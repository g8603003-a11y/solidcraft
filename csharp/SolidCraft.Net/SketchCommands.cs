using System.Text.Json;

namespace SolidCraft;

/// <summary>
/// Sketch commands. Lengths are mm; any value may be an expression over the design's parameters
/// (<c>"W / 2"</c>). Points are <c>(x, y)</c> or an id such as <c>"l1.end"</c>, <c>"c1.center"</c>, <c>"origin"</c>.
/// Every drawing command returns the ids it created, usable in later commands.
/// </summary>
public sealed class SketchCommands
{
    private readonly SolidCraftClient _c;
    internal SketchCommands(SolidCraftClient c) => _c = c;

    /// <summary>
    /// Start a sketch on a plane: <c>"XY"</c>, <c>"XZ"</c>, <c>"YZ"</c>, a construction plane's name, or an object
    /// (<c>new { origin = …, x_dir = …, y_dir = … }</c>, or <c>new { face = new[]{x,y,z} }</c> for a body face).
    /// </summary>
    /// <param name="offset">Moves the plane along its normal; follows parameters when an expression.</param>
    public Task<JsonElement> CreateAsync(object? plane = null, Expr? offset = null, string? name = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Sketch.Create, Json.Params(("plane", plane ?? "XY"), ("offset", offset), ("name", name)), ct);

    /// <summary>Reopen a finished sketch for editing.</summary>
    public Task<JsonElement> EditAsync(string sketch, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Sketch.Edit, Json.Params(("sketch", sketch)), ct);

    /// <summary>Finish the sketch being edited.</summary>
    public Task<JsonElement> FinishAsync(CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Sketch.Finish, null, ct);

    /// <summary>Leave the sketch without keeping what changed since it was created or opened.</summary>
    public Task<JsonElement> CancelAsync(CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Sketch.Cancel, null, ct);

    /// <summary>The solver status and degrees of freedom of the sketch being edited.</summary>
    public Task<JsonElement> SolveAsync(CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Sketch.Solve, null, ct);

    /// <summary>Points, curves, constraints and dimensions of a sketch (the active one by default).</summary>
    public Task<JsonElement> InspectAsync(string? sketch = null, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Sketch.Inspect, Json.Params(("sketch", sketch)), ct);

    // ---- Drawing ---------------------------------------------------------------------------

    /// <summary>A polyline through the points (two points: one line). <paramref name="closed"/> joins the last point to the first.</summary>
    public Task<SketchResult> LineAsync(IEnumerable<Point2> points, bool closed = false, bool construction = false, CancellationToken ct = default) =>
        Draw(Commands.Sketch.Line, Json.Params(("points", points.ToArray()), ("closed", closed ? (object)true : null), ("construction", construction ? (object)true : null)), ct);

    /// <summary>One line from <paramref name="a"/> to <paramref name="b"/>.</summary>
    public Task<SketchResult> LineAsync(Point2 a, Point2 b, bool construction = false, CancellationToken ct = default) =>
        LineAsync(new[] { a, b }, false, construction, ct);

    /// <summary>A rectangle by two opposite corners. The result's <c>Sides</c> and <c>Corners</c> name its lines and points.</summary>
    public Task<SketchResult> RectangleAsync(Point2 p0, Point2 p1, bool construction = false, CancellationToken ct = default) =>
        Draw(Commands.Sketch.RectangleTwoPoint, Json.Params(("p0", p0), ("p1", p1), ("construction", construction ? (object)true : null)), ct);

    /// <summary>A rectangle by its centre and one corner (adds two construction diagonals; the centre point is the fifth point).</summary>
    public Task<SketchResult> RectangleCenterAsync(Point2 center, Point2 corner, bool construction = false, CancellationToken ct = default) =>
        Draw(Commands.Sketch.RectangleCenter, Json.Params(("center", center), ("corner", corner), ("construction", construction ? (object)true : null)), ct);

    /// <summary>A circle by centre and radius (creates <c>c1</c> and <c>c1.center</c>).</summary>
    public Task<SketchResult> CircleAsync(Point2 center, Expr radius, bool construction = false, CancellationToken ct = default) =>
        Draw(Commands.Sketch.CircleCenter, Json.Params(("center", center), ("radius", radius), ("construction", construction ? (object)true : null)), ct);

    /// <summary>A circle by centre and diameter.</summary>
    public Task<SketchResult> CircleDiameterAsync(Point2 center, Expr diameter, bool construction = false, CancellationToken ct = default) =>
        Draw(Commands.Sketch.CircleCenter, Json.Params(("center", center), ("diameter", diameter), ("construction", construction ? (object)true : null)), ct);

    /// <summary>An arc by centre, start and end.</summary>
    public Task<SketchResult> ArcCenterAsync(Point2 center, Point2 start, Point2 end, bool construction = false, CancellationToken ct = default) =>
        Draw(Commands.Sketch.ArcCenterPoint, Json.Params(("center", center), ("start", start), ("end", end), ("construction", construction ? (object)true : null)), ct);

    /// <summary>An arc by its two ends and a point it passes through.</summary>
    public Task<SketchResult> ArcThreePointAsync(Point2 start, Point2 end, Point2 through, bool construction = false, CancellationToken ct = default) =>
        Draw(Commands.Sketch.ArcThreePoint, Json.Params(("start", start), ("end", end), ("through", through), ("construction", construction ? (object)true : null)), ct);

    /// <summary>A stand-alone point (a hole centre, say).</summary>
    public Task<SketchResult> PointAsync(Point2 point, CancellationToken ct = default) =>
        Draw(Commands.Sketch.Point, Json.Params(("point", point)), ct);

    /// <summary>A slot by its overall ends and width.</summary>
    public Task<SketchResult> SlotAsync(Point2 p0, Point2 p1, Expr width, bool construction = false, CancellationToken ct = default) =>
        Draw(Commands.Sketch.SlotOverall, Json.Params(("p0", p0), ("p1", p1), ("width", width), ("construction", construction ? (object)true : null)), ct);

    /// <summary>A slot by its arc centres and width.</summary>
    public Task<SketchResult> SlotCenterToCenterAsync(Point2 c0, Point2 c1, Expr width, bool construction = false, CancellationToken ct = default) =>
        Draw(Commands.Sketch.SlotCenterToCenter, Json.Params(("p0", c0), ("p1", c1), ("width", width), ("construction", construction ? (object)true : null)), ct);

    /// <summary>A regular polygon inscribed in a circle (radius to the vertices).</summary>
    public Task<SketchResult> PolygonAsync(Point2 center, Expr radius, int sides, Expr? angle = null, bool construction = false, CancellationToken ct = default) =>
        Draw(Commands.Sketch.PolygonInscribed, Json.Params(("center", center), ("radius", radius), ("sides", sides), ("angle", angle), ("construction", construction ? (object)true : null)), ct);

    /// <summary>Text along the x axis, extruded later like any profile.</summary>
    public Task<SketchResult> TextAsync(string text, Point2 position, Expr height, string? font = null, CancellationToken ct = default) =>
        Draw(Commands.Sketch.Text, Json.Params(("text", text), ("position", position), ("height", height), ("font", font)), ct);

    /// <summary>
    /// Draw a shape and its driving dimensions in one undo step: <paramref name="dimensions"/> maps a role
    /// (<c>width</c>, <c>height</c>, <c>radius</c>, <c>diameter</c>, <c>length</c>, <c>angle</c>…) to a value or expression.
    /// </summary>
    public Task<SketchResult> DrawDimensionedAsync(string command, object parameters, IReadOnlyDictionary<string, Expr> dimensions, CancellationToken ct = default) =>
        Draw(Commands.Sketch.DrawDimensioned, Json.Params(("command", command), ("params", parameters), ("dimensions", dimensions)), ct);

    /// <summary>Project model edges or faces into the sketch (reference entities), following the model.</summary>
    public Task<JsonElement> ProjectAsync(object parameters, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Sketch.Project, parameters, ct);

    /// <summary>Delete sketch entities by id.</summary>
    public Task<JsonElement> DeleteAsync(IEnumerable<string> entities, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Sketch.Delete, Json.Params(("entities", entities.ToArray())), ct);

    // ---- Dimensions and constraints ---------------------------------------------------------

    /// <summary>
    /// A driving dimension on one or two entities. <paramref name="type"/>: <c>auto</c> (default), <c>distance</c>,
    /// <c>horizontal</c>, <c>vertical</c>, <c>length</c>, <c>radius</c>, <c>diameter</c>, <c>angle</c>. A bare number is in
    /// the design's units; <c>"50 mm"</c> pins a unit; <c>"width = 40"</c> also adds the parameter.
    /// </summary>
    public Task<SketchResult> DimensionAsync(IEnumerable<string> entities, Expr? value = null, string? type = null, CancellationToken ct = default) =>
        Draw(Commands.Sketch.Dimension, Json.Params(("entities", entities.ToArray()), ("value", value), ("type", type)), ct);

    /// <summary>A dimension on one entity (a line's length, a circle's diameter).</summary>
    public Task<SketchResult> DimensionAsync(string entity, Expr value, string? type = null, CancellationToken ct = default) =>
        DimensionAsync(new[] { entity }, value, type, ct);

    /// <summary>A dimension between two entities.</summary>
    public Task<SketchResult> DimensionAsync(string a, string b, Expr value, string? type = null, CancellationToken ct = default) =>
        DimensionAsync(new[] { a, b }, value, type, ct);

    /// <summary>
    /// A geometric constraint: <paramref name="kind"/> is <c>coincident</c>, <c>horizontal</c>, <c>vertical</c>,
    /// <c>parallel</c>, <c>perpendicular</c>, <c>tangent</c>, <c>equal</c>, <c>concentric</c>, <c>symmetry</c>, <c>midpoint</c>,
    /// <c>fix</c>, <c>collinear</c>… (<c>sketch.constraint.&lt;kind&gt;</c>); <paramref name="parameters"/> names its entities,
    /// usually <c>new { a = "l1", b = "l2" }</c> or <c>new { entities = new[]{…} }</c>.
    /// </summary>
    public Task<SketchResult> ConstraintAsync(string kind, object parameters, CancellationToken ct = default) =>
        Draw("sketch.constraint." + kind, parameters, ct);

    /// <summary>Two entities coincident (two points, a point on a curve, two lines joined at their closest ends).</summary>
    public Task<SketchResult> CoincidentAsync(string a, string b, CancellationToken ct = default) =>
        ConstraintAsync("coincident", Json.Params(("a", a), ("b", b)), ct);

    /// <summary>A line horizontal.</summary>
    public Task<SketchResult> HorizontalAsync(string line, CancellationToken ct = default) =>
        Draw(Commands.Sketch.ConstraintHorizontalVertical, Json.Params(("line", line), ("mode", "horizontal")), ct);

    /// <summary>A line vertical.</summary>
    public Task<SketchResult> VerticalAsync(string line, CancellationToken ct = default) =>
        Draw(Commands.Sketch.ConstraintHorizontalVertical, Json.Params(("line", line), ("mode", "vertical")), ct);

    /// <summary>Two lines parallel.</summary>
    public Task<SketchResult> ParallelAsync(string a, string b, CancellationToken ct = default) =>
        ConstraintAsync("parallel", Json.Params(("a", a), ("b", b)), ct);

    /// <summary>Two lines perpendicular.</summary>
    public Task<SketchResult> PerpendicularAsync(string a, string b, CancellationToken ct = default) =>
        ConstraintAsync("perpendicular", Json.Params(("a", a), ("b", b)), ct);

    /// <summary>Two curves tangent.</summary>
    public Task<SketchResult> TangentAsync(string a, string b, CancellationToken ct = default) =>
        ConstraintAsync("tangent", Json.Params(("a", a), ("b", b)), ct);

    /// <summary>Two lines, or two circles/arcs, equal.</summary>
    public Task<SketchResult> EqualAsync(string a, string b, CancellationToken ct = default) =>
        ConstraintAsync("equal", Json.Params(("a", a), ("b", b)), ct);

    /// <summary>Two circles/arcs concentric.</summary>
    public Task<SketchResult> ConcentricAsync(string a, string b, CancellationToken ct = default) =>
        ConstraintAsync("concentric", Json.Params(("a", a), ("b", b)), ct);

    /// <summary>Fix entities where they are (or unfix them when all are fixed).</summary>
    public Task<SketchResult> FixAsync(IEnumerable<string> entities, bool? fixedState = null, CancellationToken ct = default) =>
        ConstraintAsync("fix", Json.Params(("entities", entities.ToArray()), ("fixed", fixedState)), ct);

    /// <summary>Infer the constraints the sketch implies, then dimension until it is fully constrained.</summary>
    public Task<JsonElement> AutoConstrainAsync(bool dimensions = true, CancellationToken ct = default) =>
        _c.ExecuteAsync(Commands.Sketch.AutoConstrain, Json.Params(("dimensions", dimensions)), ct);

    private async Task<SketchResult> Draw(string command, object? parameters, CancellationToken ct)
    {
        var raw = await _c.ExecuteAsync(command, parameters, ct).ConfigureAwait(false);
        var created = raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("created", out var c) ? Json.To<SketchCreated>(c) : new SketchCreated();
        return new SketchResult(created, raw);
    }
}
