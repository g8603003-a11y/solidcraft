using System.Runtime.InteropServices;

namespace SolidCraft;

/// <summary>
/// Finds the running SolidCraft app's control channel: its port and the token of this launch.
/// The app writes <c>control/current.port</c> and <c>control/&lt;port&gt;.token</c> in its per-user
/// data folder (see docs/control-protocol.md). Environment variables <c>SOLIDCRAFT_CONTROL_DIR</c>,
/// <c>SOLIDCRAFT_CONTROL_PORT</c> and <c>SOLIDCRAFT_CONTROL_TOKEN</c> override the files.
/// </summary>
public static class ControlDiscovery
{
    /// <summary>The port the app tries first when nothing names one.</summary>
    public const int DefaultPort = 27878;

    /// <summary>The last port the app tries before giving up (<c>DefaultPort</c> + 15).</summary>
    public const int LastDefaultPort = 27893;

    /// <summary>The folders in which the token files may be, most likely first.</summary>
    public static IReadOnlyList<string> ControlDirectories()
    {
        var dirs = new List<string>();
        var explicitDir = Environment.GetEnvironmentVariable("SOLIDCRAFT_CONTROL_DIR");
        if (!string.IsNullOrWhiteSpace(explicitDir))
        {
            dirs.Add(explicitDir);
            return dirs;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrEmpty(local))
            {
                dirs.Add(Path.Combine(local, "SolidCraft", "control"));
                // An MSIX-packaged app has its AppData redirected.
                var packages = Path.Combine(local, "Packages");
                if (Directory.Exists(packages))
                {
                    try
                    {
                        foreach (var pkg in Directory.EnumerateDirectories(packages))
                        {
                            var redirected = Path.Combine(pkg, "LocalCache", "Local", "SolidCraft", "control");
                            if (Directory.Exists(redirected))
                            {
                                dirs.Add(redirected);
                            }
                        }
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
            {
                dirs.Add(Path.Combine(home, "Library", "Application Support", "SolidCraft", "control"));
            }
        }
        else
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(xdg))
            {
                dirs.Add(Path.Combine(xdg, "solidcraft", "control"));
            }
            else if (!string.IsNullOrEmpty(home))
            {
                dirs.Add(Path.Combine(home, ".local", "share", "solidcraft", "control"));
            }
        }
        return dirs;
    }

    /// <summary>
    /// The port of the most recently started app, from <c>SOLIDCRAFT_CONTROL_PORT</c> or
    /// <c>control/current.port</c>; <c>null</c> when neither names one.
    /// </summary>
    public static int? FindPort()
    {
        var env = Environment.GetEnvironmentVariable("SOLIDCRAFT_CONTROL_PORT");
        if (int.TryParse(env, out var fromEnv) && fromEnv is > 0 and < 65536)
        {
            return fromEnv;
        }
        foreach (var dir in ControlDirectories())
        {
            var file = Path.Combine(dir, "current.port");
            if (!File.Exists(file))
            {
                continue;
            }
            try
            {
                var text = File.ReadAllText(file).Trim();
                if (int.TryParse(text, out var port) && port is > 0 and < 65536)
                {
                    return port;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        // No discovery file: the first default port whose token file exists.
        for (var port = DefaultPort; port <= LastDefaultPort; port++)
        {
            if (TokenFilePath(port) is not null)
            {
                return port;
            }
        }
        return null;
    }

    /// <summary>The token file written for the app on <paramref name="port"/>, if there is one.</summary>
    public static string? TokenFilePath(int port)
    {
        foreach (var dir in ControlDirectories())
        {
            var file = Path.Combine(dir, port + ".token");
            if (File.Exists(file))
            {
                return file;
            }
        }
        return null;
    }

    /// <summary>
    /// The token for the app on <paramref name="port"/>: <c>SOLIDCRAFT_CONTROL_TOKEN</c>, else the
    /// token file. <c>null</c> when there is neither.
    /// </summary>
    public static string? FindToken(int port)
    {
        var env = Environment.GetEnvironmentVariable("SOLIDCRAFT_CONTROL_TOKEN");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env.Trim();
        }
        var file = TokenFilePath(port);
        if (file is null)
        {
            return null;
        }
        try
        {
            var token = File.ReadAllText(file).Trim();
            return token.Length == 0 ? null : token;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// The process id recorded next to the token (<c>&lt;port&gt;.pid</c>), to tell a stale token
    /// file (the app was force-quit) from a live one. <c>null</c> when there is no pid file.
    /// </summary>
    public static int? FindPid(int port)
    {
        foreach (var dir in ControlDirectories())
        {
            var file = Path.Combine(dir, port + ".pid");
            if (!File.Exists(file))
            {
                continue;
            }
            try
            {
                var first = File.ReadAllText(file).Trim().Split(' ', '\t', '\n')[0];
                if (int.TryParse(first, out var pid))
                {
                    return pid;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return null;
    }
}
