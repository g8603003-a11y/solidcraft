using System.Text.Json;

namespace SolidCraft;

/// <summary>A standard view direction (<c>ui.view</c>).</summary>
public enum StandardView
{
    Front, Back, Top, Bottom, Left, Right, Iso, Home,
    /// <summary>Frame everything, keeping the direction.</summary>
    Fit,
}

/// <summary>
/// The window: views, camera, screenshots and renders, the ribbon tab, dialogs. These are control
/// methods (<c>ui.*</c>), not engine commands; they need the desktop app's window.
/// </summary>
public sealed class UiCommands
{
    private readonly SolidCraftClient _c;
    internal UiCommands(SolidCraftClient c) => _c = c;

    /// <summary>Any <c>ui.*</c> method with raw parameters.</summary>
    public Task<JsonElement> CallAsync(string method, object? parameters = null, CancellationToken ct = default) =>
        _c.RequestAsync(method.StartsWith("ui.", StringComparison.Ordinal) ? method : "ui." + method, parameters, ct);

    /// <summary>Turn to a standard view; <paramref name="animate"/> glides instead of snapping.</summary>
    public Task<JsonElement> ViewAsync(StandardView view, bool animate = false, CancellationToken ct = default) =>
        _c.RequestAsync("ui.view", Json.Params(("view", view.ToString().ToLowerInvariant()), ("animate", animate ? (object)true : null)), ct);

    /// <summary>Frame the whole design.</summary>
    public Task<JsonElement> FitAsync(CancellationToken ct = default) => ViewAsync(StandardView.Fit, false, ct);

    /// <summary>The orbit camera: target (mm), yaw/pitch/roll (radians), distance (mm); fields left null keep their values.</summary>
    public Task<JsonElement> CameraAsync(Point3? target = null, double? yaw = null, double? pitch = null, double? roll = null, double? distance = null, bool? fit = null, CancellationToken ct = default) =>
        _c.RequestAsync("ui.camera", Json.Params(("target", target), ("yaw", yaw), ("pitch", pitch), ("roll", roll), ("distance", distance), ("fit", fit)), ct);

    /// <summary>The camera as it is (pass the reply back to <see cref="CameraRawAsync"/> to restore it).</summary>
    public Task<JsonElement> GetCameraAsync(CancellationToken ct = default) => _c.RequestAsync("ui.camera", null, ct);

    /// <summary>Set the camera from a reply of <see cref="GetCameraAsync"/> or any raw object.</summary>
    public Task<JsonElement> CameraRawAsync(object camera, CancellationToken ct = default) => _c.RequestAsync("ui.camera", camera, ct);

    /// <summary>A PNG of the window as presented (fails while the window is minimised: use <see cref="RenderAsync"/>).</summary>
    public Task<JsonElement> ScreenshotAsync(string path, CancellationToken ct = default) =>
        _c.RequestAsync("ui.screenshot", Json.Params(("path", path)), ct);

    /// <summary>Render the model with the current camera to a PNG, on the app's GPU, no window needed.</summary>
    public Task<JsonElement> RenderAsync(string path, int? width = null, int? height = null, bool? grid = null, bool? axes = null, CancellationToken ct = default) =>
        _c.RequestAsync("ui.render", Json.Params(("path", path), ("width", width), ("height", height), ("grid", grid), ("axes", axes)), ct);

    /// <summary>UI state: viewport, camera, active tool or dialog, status line, hover, renderer, timings.</summary>
    public Task<JsonElement> InspectAsync(CancellationToken ct = default) => _c.RequestAsync("ui.inspect", null, ct);

    /// <summary>The selection, the open dialog's inputs and the hovered item.</summary>
    public Task<JsonElement> SelectionAsync(CancellationToken ct = default) => _c.RequestAsync("ui.selection", null, ct);

    /// <summary>Set UI state fields: <c>new { tab = "SOLID", dark = false, showGrid = true, layout = "compact" }</c>.</summary>
    public Task<JsonElement> SetAsync(object state, CancellationToken ct = default) => _c.RequestAsync("ui.set", state, ct);

    /// <summary>Show a ribbon tab (<c>SOLID</c>, <c>SURFACE</c>, <c>ASSEMBLE</c>, <c>INSPECT</c>, <c>SHEET METAL</c>…).</summary>
    public Task<JsonElement> ShowTabAsync(string tab, CancellationToken ct = default) =>
        SetAsync(new Dictionary<string, object?> { ["tab"] = tab }, ct);

    /// <summary>Switch the light or dark theme.</summary>
    public Task<JsonElement> DarkThemeAsync(bool dark, CancellationToken ct = default) =>
        SetAsync(new Dictionary<string, object?> { ["dark"] = dark }, ct);

    /// <summary>
    /// Start a command as a toolbar click would: its dialog or sketch tool opens for the person, with
    /// <paramref name="parameters"/> filled in, and nothing runs until they press OK.
    /// </summary>
    public Task<JsonElement> StartAsync(string command, object? parameters = null, CancellationToken ct = default) =>
        _c.RequestAsync("ui.start", Json.Params(("command", command), ("params", parameters)), ct);

    /// <summary>Press a key (<c>Enter</c>, <c>Escape</c>, <c>E</c>…) with modifiers.</summary>
    public Task<JsonElement> KeyAsync(string key, bool ctrl = false, bool shift = false, bool alt = false, CancellationToken ct = default) =>
        _c.RequestAsync("ui.key", Json.Params(("key", key), ("ctrl", ctrl ? (object)true : null), ("shift", shift ? (object)true : null), ("alt", alt ? (object)true : null)), ct);

    /// <summary>Type text into whatever has the focus.</summary>
    public Task<JsonElement> TextAsync(string text, CancellationToken ct = default) =>
        _c.RequestAsync("ui.text", Json.Params(("text", text)), ct);

    /// <summary>Click at a window point.</summary>
    public Task<JsonElement> ClickAsync(double x, double y, string? button = null, bool shift = false, bool ctrl = false, bool doubleClick = false, CancellationToken ct = default) =>
        _c.RequestAsync("ui.click", Json.Params(("x", x), ("y", y), ("button", button), ("shift", shift ? (object)true : null), ("ctrl", ctrl ? (object)true : null), ("double", doubleClick ? (object)true : null)), ct);

    /// <summary>Show or hide the agent cursor that acts commands out in the window.</summary>
    public Task<JsonElement> AgentCursorAsync(bool show, CancellationToken ct = default) =>
        _c.RequestAsync("ui.agent_cursor", Json.Params(("show", show)), ct);

    /// <summary>Preferences: <c>new { agent_off_thread = true }</c> keeps the window responsive while long commands run.</summary>
    public Task<JsonElement> PrefsAsync(object prefs, CancellationToken ct = default) => _c.RequestAsync("ui.prefs", prefs, ct);
}
