using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.IO;
using System.Linq;
using System.Text;
using xivModdingFramework.Cache;
using xivModdingFramework.Helpers;

namespace AlphaChannel.TexTools.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        RefreshPaths();
        GamePathBox.Text = ConsoleConfig.Get().XivPath
                           ?? ConsoleConfig.ResolveDefaultXivPath()
                           ?? "";
    }

    private void OnRefresh(object? sender, RoutedEventArgs e) => RefreshPaths();

    private void OnCliHelp(object? sender, RoutedEventArgs e)
    {
        PathsBox.Text +=
            "\n\nCLI (native):\n" +
            "  ./ConsoleTools/bin/Release/net8.0/ConsoleTools /doctor\n" +
            "  ./ConsoleTools/bin/Release/net8.0/ConsoleTools /upgrade ./mod.ttmp2 ./mod-upgraded.ttmp2\n";
    }

    private async void OnBrowseInstall(object? sender, RoutedEventArgs e)
    {
        var start = GamePathBox.Text;
        if (string.IsNullOrWhiteSpace(start) || !Directory.Exists(start))
        {
            start = PlatformPaths.GetLauncherConfigRoot();
            if (!Directory.Exists(start))
                start = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        else
        {
            // If they already have sqpack/ffxiv, start one level up for browsing.
            start = Directory.GetParent(start)?.FullName ?? start;
        }

        var browser = new FolderBrowserWindow(start);
        var result = await browser.ShowDialog<string?>(this);
        if (string.IsNullOrWhiteSpace(result))
            return;

        var normalized = NormalizeToSqPack(result);
        GamePathBox.Text = normalized;
        PathStatus.Text = Directory.Exists(normalized)
            ? $"Selected: {normalized}"
            : $"Selected path missing sqpack/ffxiv: {result}";
    }

    private void OnSavePath(object? sender, RoutedEventArgs e)
    {
        var path = GamePathBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(path))
        {
            PathStatus.Text = "Enter or browse to a path first.";
            return;
        }

        path = NormalizeToSqPack(path);
        if (!IsValidSqPackDir(path))
        {
            // Common XLCore mistake: ~/.xlcore/ffxiv is not where the game files live.
            var fromLauncher = NormalizeToSqPack(PenumbraAPI.GetQuickLauncherGameDirectory() ?? "");
            if (IsValidSqPackDir(fromLauncher))
            {
                PathStatus.Text =
                    $"'{path}' is not a sqpack/ffxiv folder. launcher.ini points to the real install — click “Read launcher.ini”, then Save.\nSuggested: {fromLauncher}";
                GamePathBox.Text = fromLauncher;
                return;
            }

            PathStatus.Text =
                $"Not a valid FFXIV sqpack folder (need *.win32.index files).\nGot: {path}\nTip: use “Read launcher.ini” or Browse to /mnt/.../game/sqpack/ffxiv";
            return;
        }

        ConsoleConfig.Update(c => c.XivPath = path);
        GamePathBox.Text = path;
        PathStatus.Text = $"Saved to console_config.json: {path}";
        RefreshPaths();
    }

    private void OnUseXlcore(object? sender, RoutedEventArgs e)
    {
        var candidate = Path.Combine(PlatformPaths.GetLauncherConfigRoot(), "ffxiv");
        var normalized = NormalizeToSqPack(candidate);
        if (IsValidSqPackDir(normalized))
        {
            GamePathBox.Text = normalized;
            PathStatus.Text = $"Using {normalized}";
            return;
        }

        // ~/.xlcore/ffxiv often exists without the game data (install is elsewhere).
        var fromLauncher = NormalizeToSqPack(PenumbraAPI.GetQuickLauncherGameDirectory() ?? "");
        if (IsValidSqPackDir(fromLauncher))
        {
            GamePathBox.Text = fromLauncher;
            PathStatus.Text =
                $"~/.xlcore/ffxiv has no sqpack data. Using GamePath from launcher.ini:\n{fromLauncher}";
            return;
        }

        GamePathBox.Text = normalized;
        PathStatus.Text = "~/.xlcore/ffxiv has no game/sqpack/ffxiv. Use Browse or Read launcher.ini.";
    }

    private void OnReadLauncherIni(object? sender, RoutedEventArgs e)
    {
        var fromLauncher = PenumbraAPI.GetQuickLauncherGameDirectory();
        if (string.IsNullOrWhiteSpace(fromLauncher))
        {
            PathStatus.Text = "No GamePath in launcher.ini / launcherConfigV3.json.";
            return;
        }

        var normalized = NormalizeToSqPack(fromLauncher);
        GamePathBox.Text = normalized;
        PathStatus.Text = IsValidSqPackDir(normalized)
            ? $"From launcher.ini — click Save:\n{normalized}"
            : $"From launcher.ini but sqpack not found at:\n{normalized}";
    }

    private static string NormalizeToSqPack(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        path = Path.GetFullPath(path.Trim().Trim('"'));
        if (Directory.Exists(Path.Combine(path, "game", "sqpack", "ffxiv")))
            return Path.Combine(path, "game", "sqpack", "ffxiv");
        if (Directory.Exists(Path.Combine(path, "sqpack", "ffxiv")))
            return Path.Combine(path, "sqpack", "ffxiv");
        return path;
    }

    private static bool IsValidSqPackDir(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return false;
        try
        {
            return Directory.EnumerateFiles(path, "*.win32.index").Any();
        }
        catch
        {
            return false;
        }
    }

    private void RefreshPaths()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"OS: {(PlatformPaths.IsLinux ? "Linux" : PlatformPaths.IsWindows ? "Windows" : PlatformPaths.IsMacOS ? "macOS" : "unknown")}");
        sb.AppendLine($"Launcher root: {PlatformPaths.GetLauncherConfigRoot()}");
        sb.AppendLine($"launcher.ini: {(File.Exists(PlatformPaths.GetLauncherIniPath()) ? "found" : "missing")}");
        sb.AppendLine($"launcherConfigV3: {(File.Exists(PlatformPaths.GetLauncherConfigV3Path()) ? "found" : "missing (normal on Linux)")}");
        var detected = ConsoleConfig.ResolveDefaultXivPath();
        var configured = ConsoleConfig.Get().XivPath;
        sb.AppendLine($"Game path (auto): {detected ?? "(not found)"}");
        sb.AppendLine($"Configured XivPath: {configured}");
        if (!string.IsNullOrWhiteSpace(configured) && !IsValidSqPackDir(configured))
        {
            sb.AppendLine("WARNING: Configured XivPath is not a valid sqpack/ffxiv folder.");
            if (!string.IsNullOrWhiteSpace(detected))
                sb.AppendLine($"Click “Read launcher.ini” then Save to use: {detected}");
        }
        else if (!string.IsNullOrWhiteSpace(configured) && !string.IsNullOrWhiteSpace(detected)
                 && !string.Equals(Path.GetFullPath(configured), Path.GetFullPath(detected), StringComparison.Ordinal))
        {
            sb.AppendLine($"NOTE: auto-detect differs from saved config. Prefer: {detected}");
        }
        sb.AppendLine($"TexTools data: {PlatformPaths.GetTexToolsDataRoot()}");
        sb.AppendLine($"ModPacks: {PlatformPaths.GetTexToolsModPacksDirectory()}");
        PathsBox.Text = sb.ToString();

        // Prefer a valid saved path; otherwise prefer auto-detect (launcher.ini).
        if (IsValidSqPackDir(configured))
            GamePathBox.Text = configured;
        else if (!string.IsNullOrWhiteSpace(detected))
            GamePathBox.Text = detected;
    }
}
