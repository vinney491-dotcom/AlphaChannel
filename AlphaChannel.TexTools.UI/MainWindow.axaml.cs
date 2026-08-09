using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        UpdateContinueEnabled();

        // Skip straight to workspace when a valid path is already configured.
        var configured = ConsoleConfig.Get().XivPath;
        if (IsValidSqPackDir(configured))
            ShowWorkspace();
    }

    private void OnRefresh(object? sender, RoutedEventArgs e)
    {
        RefreshPaths();
        UpdateContinueEnabled();
    }

    private void OnContinue(object? sender, RoutedEventArgs e)
    {
        var path = NormalizeToSqPack(GamePathBox.Text?.Trim() ?? "");
        if (!IsValidSqPackDir(path))
        {
            PathStatus.Text = "Save a valid sqpack/ffxiv path first.";
            return;
        }

        if (!string.Equals(ConsoleConfig.Get().XivPath, path, StringComparison.Ordinal))
            ConsoleConfig.Update(c => c.XivPath = path);

        ShowWorkspace();
    }

    private void OnChangePath(object? sender, RoutedEventArgs e) => ShowSetup();

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
            start = Directory.GetParent(start)?.FullName ?? start;
        }

        PathStatus.Text = "Opening system folder picker (Dolphin/KDE if available)…";
        var result = await NativeFolderPicker.PickFolderAsync(this, start);
        if (string.IsNullOrWhiteSpace(result))
        {
            PathStatus.Text = "Browse cancelled.";
            return;
        }

        var normalized = NormalizeToSqPack(result);
        GamePathBox.Text = normalized;
        PathStatus.Text = Directory.Exists(normalized)
            ? $"Selected: {normalized}"
            : $"Selected path missing sqpack/ffxiv: {result}";
        UpdateContinueEnabled();
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
            var fromLauncher = NormalizeToSqPack(PenumbraAPI.GetQuickLauncherGameDirectory() ?? "");
            if (IsValidSqPackDir(fromLauncher))
            {
                PathStatus.Text =
                    $"'{path}' is not a sqpack/ffxiv folder. launcher.ini points to the real install — click “Read launcher.ini”, then Save.\nSuggested: {fromLauncher}";
                GamePathBox.Text = fromLauncher;
                UpdateContinueEnabled();
                return;
            }

            PathStatus.Text =
                $"Not a valid FFXIV sqpack folder (need *.win32.index files).\nGot: {path}";
            UpdateContinueEnabled();
            return;
        }

        ConsoleConfig.Update(c => c.XivPath = path);
        GamePathBox.Text = path;
        PathStatus.Text = $"Saved. Click Continue →";
        RefreshPaths();
        UpdateContinueEnabled();
    }

    private void OnUseXlcore(object? sender, RoutedEventArgs e)
    {
        var candidate = Path.Combine(PlatformPaths.GetLauncherConfigRoot(), "ffxiv");
        var normalized = NormalizeToSqPack(candidate);
        if (IsValidSqPackDir(normalized))
        {
            GamePathBox.Text = normalized;
            PathStatus.Text = $"Using {normalized}";
            UpdateContinueEnabled();
            return;
        }

        var fromLauncher = NormalizeToSqPack(PenumbraAPI.GetQuickLauncherGameDirectory() ?? "");
        if (IsValidSqPackDir(fromLauncher))
        {
            GamePathBox.Text = fromLauncher;
            PathStatus.Text =
                $"~/.xlcore/ffxiv has no sqpack data. Using GamePath from launcher.ini:\n{fromLauncher}";
            UpdateContinueEnabled();
            return;
        }

        GamePathBox.Text = normalized;
        PathStatus.Text = "~/.xlcore/ffxiv has no game/sqpack/ffxiv. Use Browse or Read launcher.ini.";
        UpdateContinueEnabled();
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
            ? $"From launcher.ini — click Save, then Continue →\n{normalized}"
            : $"From launcher.ini but sqpack not found at:\n{normalized}";
        UpdateContinueEnabled();
    }

    private async void OnUpgradeModpack(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select modpack to upgrade",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("TexTools / Penumbra modpacks")
                {
                    Patterns = ["*.ttmp2", "*.ttmp", "*.pmp", "*.zip"]
                },
                new FilePickerFileType("All files") { Patterns = ["*"] },
            ],
        });
        if (files.Count == 0) return;

        var src = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(src) || !File.Exists(src))
        {
            AppendOutput("Could not resolve selected file path.");
            return;
        }

        var destSuggestion = Path.Combine(
            Path.GetDirectoryName(src) ?? ".",
            Path.GetFileNameWithoutExtension(src) + "-upgraded" + Path.GetExtension(src));

        var save = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save upgraded modpack as",
            SuggestedFileName = Path.GetFileName(destSuggestion),
            DefaultExtension = Path.GetExtension(src).TrimStart('.'),
            FileTypeChoices =
            [
                new FilePickerFileType("Modpack")
                {
                    Patterns = ["*" + Path.GetExtension(src)]
                },
            ],
        });
        if (save == null) return;

        var dest = save.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(dest))
        {
            AppendOutput("Could not resolve destination path.");
            return;
        }

        AppendOutput($"Upgrading:\n  {src}\n→ {dest}\n…");
        var (code, log) = await RunConsoleToolsAsync("/upgrade", src, dest);
        AppendOutput(log);
        AppendOutput(code == 0 ? "Upgrade finished OK." : $"Upgrade exited with code {code}.");
    }

    private void OnOpenModPacks(object? sender, RoutedEventArgs e) =>
        OpenPath(PlatformPaths.GetTexToolsModPacksDirectory());

    private void OnOpenDataRoot(object? sender, RoutedEventArgs e) =>
        OpenPath(PlatformPaths.GetTexToolsDataRoot());

    private async void OnRunDoctor(object? sender, RoutedEventArgs e)
    {
        AppendOutput("Running /doctor …");
        var (code, log) = await RunConsoleToolsAsync("/doctor");
        AppendOutput(log);
        AppendOutput(code == 0 ? "Doctor OK." : $"Doctor exited with code {code}.");
    }

    private void ShowWorkspace()
    {
        var path = ConsoleConfig.Get().XivPath ?? ConsoleConfig.ResolveDefaultXivPath() ?? "";
        WorkspacePathLabel.Text = $"Game: {path}";
        SetupPanel.IsVisible = false;
        WorkspacePanel.IsVisible = true;
        OutputBox.Text =
            "Native workspace ready.\n" +
            "• Upgrade modpack — Dawntrail /upgrade via ConsoleTools\n" +
            "• Open folders — ModPacks / TexTools data\n" +
            "• Classic full UI still needs Wine (currently crashing on this GPU) or more Avalonia work.\n";
    }

    private void ShowSetup()
    {
        WorkspacePanel.IsVisible = false;
        SetupPanel.IsVisible = true;
        RefreshPaths();
        UpdateContinueEnabled();
    }

    private void UpdateContinueEnabled()
    {
        var path = NormalizeToSqPack(GamePathBox.Text?.Trim() ?? "");
        ContinueButton.IsEnabled = IsValidSqPackDir(path);
    }

    private void AppendOutput(string text)
    {
        if (string.IsNullOrWhiteSpace(OutputBox.Text))
            OutputBox.Text = text;
        else
            OutputBox.Text += "\n" + text;
    }

    private static string FindConsoleTools()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "ConsoleTools", "bin", "Release", "net8.0", "ConsoleTools")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "ConsoleTools", "bin", "Debug", "net8.0", "ConsoleTools")),
            Path.Combine(Directory.GetCurrentDirectory(), "ConsoleTools", "bin", "Release", "net8.0", "ConsoleTools"),
        };
        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
            if (File.Exists(c + ".dll")) return "dotnet|" + c + ".dll";
        }

        // Sibling of UI dll when published side-by-side
        var beside = Path.Combine(baseDir, "ConsoleTools");
        if (File.Exists(beside)) return beside;
        return candidates[0];
    }

    private static async Task<(int ExitCode, string Output)> RunConsoleToolsAsync(params string[] args)
    {
        var tool = FindConsoleTools();
        var psi = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (tool.StartsWith("dotnet|", StringComparison.Ordinal))
        {
            psi.FileName = "dotnet";
            psi.ArgumentList.Add(tool["dotnet|".Length..]);
        }
        else
        {
            psi.FileName = tool;
        }

        foreach (var a in args)
            psi.ArgumentList.Add(a);

        var xiv = ConsoleConfig.Get().XivPath ?? ConsoleConfig.ResolveDefaultXivPath();
        if (!string.IsNullOrWhiteSpace(xiv))
            psi.Environment["XIV_PATH"] = xiv;

        try
        {
            using var proc = Process.Start(psi);
            if (proc == null) return (-1, "Failed to start ConsoleTools.");
            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(stdout)) sb.AppendLine(stdout.TrimEnd());
            if (!string.IsNullOrWhiteSpace(stderr)) sb.AppendLine(stderr.TrimEnd());
            return (proc.ExitCode, sb.ToString());
        }
        catch (Exception ex)
        {
            return (-1, $"Failed to run ConsoleTools ({tool}): {ex.Message}");
        }
    }

    private static void OpenPath(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
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

    private static bool IsValidSqPackDir(string? path)
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
        sb.AppendLine($"TexTools data: {PlatformPaths.GetTexToolsDataRoot()}");
        sb.AppendLine($"ModPacks: {PlatformPaths.GetTexToolsModPacksDirectory()}");
        PathsBox.Text = sb.ToString();

        if (IsValidSqPackDir(configured))
            GamePathBox.Text = configured!;
        else if (!string.IsNullOrWhiteSpace(detected))
            GamePathBox.Text = detected;
    }
}
