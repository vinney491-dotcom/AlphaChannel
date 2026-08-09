using System;
using System.IO;
using System.Threading.Tasks;
using xivModdingFramework.Cache;
using xivModdingFramework.Helpers;

namespace AlphaChannel.TexTools.UI;

/// <summary>
/// Ensures the framework game cache is initialized once for UI operations.
/// </summary>
public static class GameSession
{
    private static readonly object Gate = new();
    private static bool _ready;
    private static string? _path;

    public static bool IsReady => _ready;

    public static string? GamePath => _path;

    public static async Task EnsureInitializedAsync(IProgress<string>? progress = null)
    {
        var path = ConsoleConfig.Get().XivPath;
        if (string.IsNullOrWhiteSpace(path))
            path = ConsoleConfig.ResolveDefaultXivPath();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            throw new InvalidOperationException("No valid FFXIV sqpack/ffxiv path configured.");

        lock (Gate)
        {
            if (_ready && string.Equals(_path, path, StringComparison.Ordinal))
                return;
        }

        progress?.Report("Initializing game cache…");
        // Worker off: faster UI startup; item lists still load on demand.
        await ConsoleConfig.InitCacheFromConfig(runWorker: false);

        lock (Gate)
        {
            _ready = true;
            _path = path;
        }
        progress?.Report($"Game cache ready: {path}");
    }

    public static void Reset()
    {
        lock (Gate)
        {
            _ready = false;
            _path = null;
        }
    }
}
