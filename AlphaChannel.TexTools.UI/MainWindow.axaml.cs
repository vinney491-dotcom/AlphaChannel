using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
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
    private bool _busy;
    private List<ItemRow> _allItems = new();

    public MainWindow()
    {
        InitializeComponent();
        MainDisplay.StatusChanged += msg => BusyText.Text = msg;
        MainDisplay.ExportRequested = async path =>
        {
            ExtractPathBox.Text = path;
            OnExtractFile(null, new RoutedEventArgs());
            await Task.CompletedTask;
        };
        RefreshPaths();
        GamePathBox.Text = ConsoleConfig.Get().XivPath
                           ?? ConsoleConfig.ResolveDefaultXivPath()
                           ?? "";
        UpdateContinueEnabled();

        var configured = ConsoleConfig.Get().XivPath;
        if (IsValidSqPackDir(configured))
            _ = EnterWorkspaceAsync();
    }

    private async Task EnterWorkspaceAsync()
    {
        ShowWorkspaceChrome();
        RefreshLibraryInfo();
        OnRefreshBackups(null!, new RoutedEventArgs());
        await RunBusyAsync("Initializing…", async log =>
        {
            await GameSession.EnsureInitializedAsync(log);
            log.Report("Ready. Use Modpacks → Import to Penumbra for the classic TexTools→Penumbra flow.");
        });
    }

    private void ShowWorkspaceChrome()
    {
        var path = ConsoleConfig.Get().XivPath ?? ConsoleConfig.ResolveDefaultXivPath() ?? "";
        WorkspacePathLabel.Text = $"Game: {path}";
        SetupPanel.IsVisible = false;
        WorkspacePanel.IsVisible = true;
        Title = "FFXIV TexTools";
    }

    private void ShowSetup()
    {
        WorkspacePanel.IsVisible = false;
        SetupPanel.IsVisible = true;
        GameSession.Reset();
        RefreshPaths();
        UpdateContinueEnabled();
        Title = "FFXIV TexTools — Setup";
    }

    private void OnChangePath(object? sender, RoutedEventArgs e) => ShowSetup();

    private void OnFocusItemSearch(object? sender, RoutedEventArgs e)
    {
        ItemSearchBox.Focus();
    }

    private void OnShowLogTab(object? sender, RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 5;
    }

    private void OnThemeLight(object? sender, RoutedEventArgs e)
    {
        if (Application.Current is not null)
            Application.Current.RequestedThemeVariant = ThemeVariant.Light;
    }

    private void OnThemeDark(object? sender, RoutedEventArgs e)
    {
        if (Application.Current is not null)
            Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
    }

    private async void OnAbout(object? sender, RoutedEventArgs e)
    {
        await MessageBox.ShowAsync(
            this,
            "FFXIV TexTools (AlphaChannel Linux)\n\n" +
            "Classic TexTools-style shell on Avalonia.\n" +
            "Display: texture preview (R/G/B/A), model & material info, pop-out Item Viewer.\n" +
            "Core workflows: Penumbra import, modpack upgrade, extract, item browser.\n\n" +
            "Upstream: TexTools / xivModdingFramework (GPL-3.0).\n" +
            "Interactive Helix 3D viewport remains Windows/WPF-only.",
            "About FFXIV TexTools");
    }

    private void OnSiteXivModArchive(object? sender, RoutedEventArgs e)
        => OpenExternalUrl("https://www.xivmodarchive.com/");

    private void OnSiteNexus(object? sender, RoutedEventArgs e)
        => OpenExternalUrl("https://www.nexusmods.com/finalfantasy14");

    private static void OpenExternalUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch
        {
            // ignore
        }
    }

    private void OnRefresh(object? sender, RoutedEventArgs e)
    {
        RefreshPaths();
        UpdateContinueEnabled();
    }

    private async void OnContinue(object? sender, RoutedEventArgs e)
    {
        var path = NormalizeToSqPack(GamePathBox.Text?.Trim() ?? "");
        if (!IsValidSqPackDir(path))
        {
            PathStatus.Text = "Save a valid sqpack/ffxiv path first.";
            return;
        }

        if (!string.Equals(ConsoleConfig.Get().XivPath, path, StringComparison.Ordinal))
            ConsoleConfig.Update(c => c.XivPath = path);

        await EnterWorkspaceAsync();
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
            start = Directory.GetParent(start)?.FullName ?? start;
        }

        PathStatus.Text = "Opening system folder picker…";
        var result = await NativeFolderPicker.PickFolderAsync(this, start);
        if (string.IsNullOrWhiteSpace(result))
        {
            PathStatus.Text = "Browse cancelled.";
            return;
        }

        var normalized = NormalizeToSqPack(result);
        GamePathBox.Text = normalized;
        PathStatus.Text = $"Selected: {normalized}";
        UpdateContinueEnabled();
    }

    private void OnSavePath(object? sender, RoutedEventArgs e)
    {
        var path = NormalizeToSqPack(GamePathBox.Text?.Trim() ?? "");
        if (!IsValidSqPackDir(path))
        {
            var fromLauncher = NormalizeToSqPack(PenumbraAPI.GetQuickLauncherGameDirectory() ?? "");
            if (IsValidSqPackDir(fromLauncher))
            {
                GamePathBox.Text = fromLauncher;
                PathStatus.Text = $"Invalid path. Suggested from launcher.ini: {fromLauncher}";
            }
            else
            {
                PathStatus.Text = "Not a valid sqpack/ffxiv folder.";
            }
            UpdateContinueEnabled();
            return;
        }

        ConsoleConfig.Update(c => c.XivPath = path);
        GamePathBox.Text = path;
        PathStatus.Text = "Saved. Click Continue →";
        RefreshPaths();
        UpdateContinueEnabled();
    }

    private void OnUseXlcore(object? sender, RoutedEventArgs e)
    {
        var candidate = NormalizeToSqPack(Path.Combine(PlatformPaths.GetLauncherConfigRoot(), "ffxiv"));
        var fromLauncher = NormalizeToSqPack(PenumbraAPI.GetQuickLauncherGameDirectory() ?? "");
        if (IsValidSqPackDir(candidate))
        {
            GamePathBox.Text = candidate;
            PathStatus.Text = $"Using {candidate}";
        }
        else if (IsValidSqPackDir(fromLauncher))
        {
            GamePathBox.Text = fromLauncher;
            PathStatus.Text = $"Using launcher.ini: {fromLauncher}";
        }
        else
        {
            PathStatus.Text = "No valid ~/.xlcore game data. Use Browse or Read launcher.ini.";
        }
        UpdateContinueEnabled();
    }

    private void OnReadLauncherIni(object? sender, RoutedEventArgs e)
    {
        var fromLauncher = NormalizeToSqPack(PenumbraAPI.GetQuickLauncherGameDirectory() ?? "");
        if (!IsValidSqPackDir(fromLauncher))
        {
            PathStatus.Text = "No GamePath in launcher.ini.";
            return;
        }
        GamePathBox.Text = fromLauncher;
        PathStatus.Text = $"From launcher.ini — Save, then Continue →\n{fromLauncher}";
        UpdateContinueEnabled();
    }

    private async void OnImportPenumbra(object? sender, RoutedEventArgs e)
    {
        var src = await PickModpackOpenAsync("Import modpack to Penumbra");
        if (src == null) return;
        await RunBusyAsync("Importing to Penumbra…", async log =>
        {
            var dest = await TexToolsActions.ImportToPenumbraAsync(src, log);
            log.Report($"Done: {dest}");
        });
    }

    private async void OnUpgradeModpack(object? sender, RoutedEventArgs e)
    {
        var src = await PickModpackOpenAsync("Select modpack to upgrade");
        if (src == null) return;
        var dest = await PickModpackSaveAsync(
            "Save upgraded modpack as",
            Path.GetFileNameWithoutExtension(src) + "-upgraded" + Path.GetExtension(src));
        if (dest == null) return;
        await RunBusyAsync("Upgrading…", async log =>
        {
            await TexToolsActions.UpgradeModpackAsync(src, dest, log);
        });
    }

    private async void OnResaveModpack(object? sender, RoutedEventArgs e)
    {
        var src = await PickModpackOpenAsync("Select modpack to resave");
        if (src == null) return;
        var dest = await PickModpackSaveAsync(
            "Save as (.ttmp2 / .pmp)",
            Path.GetFileNameWithoutExtension(src) + "-resaved.ttmp2");
        if (dest == null) return;
        await RunBusyAsync("Resaving…", async log =>
        {
            await TexToolsActions.ResaveModpackAsync(src, dest, log);
        });
    }

    private async void OnBatchUpgrade(object? sender, RoutedEventArgs e)
    {
        var srcFolder = await NativeFolderPicker.PickFolderAsync(this,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        if (string.IsNullOrWhiteSpace(srcFolder)) return;
        var destFolder = await NativeFolderPicker.PickFolderAsync(this, srcFolder);
        if (string.IsNullOrWhiteSpace(destFolder)) return;
        await RunBusyAsync("Batch upgrading…", async log =>
        {
            await TexToolsActions.BatchUpgradeFolderAsync(srcFolder, destFolder, log);
        });
    }

    private async void OnLoadItems(object? sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Loading items…", async log =>
        {
            _allItems = (await TexToolsActions.LoadItemBrowserAsync(log)).ToList();
            ApplyItemFilter();
            ItemsStatusBox.Text = $"{_allItems.Count} items loaded.";
        });
    }

    private void OnItemSearchKeyUp(object? sender, KeyEventArgs e) => ApplyItemFilter();

    private void ApplyItemFilter()
    {
        var q = ItemSearchBox.Text?.Trim() ?? "";
        IEnumerable<ItemRow> view = _allItems;
        if (!string.IsNullOrEmpty(q))
        {
            view = _allItems.Where(i =>
                i.Display.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
        ItemsList.ItemsSource = view.Take(2000).ToList();
        if (_allItems.Count > 2000 && string.IsNullOrEmpty(q))
            ItemsStatusBox.Text = $"Showing first 2000 of {_allItems.Count} — use search to narrow.";
    }

    private async void OnItemSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (ItemsList.SelectedItem is not ItemRow row) return;
        await RunBusyAsync("Listing item files…", async log =>
        {
            var files = await TexToolsActions.ListItemFilesAsync(row.Item, log);
            ItemFilesList.ItemsSource = files;
            ItemsStatusBox.Text = $"{row.Display} — {files.Count} files";
            var prefer = DisplayPreview.PreferDisplayFile(files);
            if (!string.IsNullOrWhiteSpace(prefer))
            {
                ExtractPathBox.Text = prefer;
                ItemFilesList.SelectedItem = prefer;
            }
            else if (files.Count > 0)
            {
                ExtractPathBox.Text = files[0];
            }
        });
    }

    private async void OnItemFileSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (ItemFilesList.SelectedItem is not string path) return;
        ExtractPathBox.Text = path;
        MainTabs.SelectedIndex = 0;
        await MainDisplay.ShowPathAsync(path, new Progress<string>(msg => BusyText.Text = msg));
    }

    private void OnOpenDisplayWindow(object? sender, RoutedEventArgs e)
    {
        var path = ItemFilesList.SelectedItem as string ?? ExtractPathBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            BusyText.Text = "Select an item file first.";
            return;
        }
        var win = new DisplayWindow(path);
        win.Show(this);
    }

    private async void OnExtractSelectedItemFile(object? sender, RoutedEventArgs e)
    {
        if (ItemFilesList.SelectedItem is not string path)
        {
            ItemsStatusBox.Text = "Select a file in the right list first.";
            return;
        }
        ExtractPathBox.Text = path;
        OnExtractFile(sender, e);
        await Task.CompletedTask;
    }

    private async void OnCopySelectedFilePath(object? sender, RoutedEventArgs e)
    {
        if (ItemFilesList.SelectedItem is not string path) return;
        ExtractPathBox.Text = path;
        try
        {
            if (Clipboard != null)
                await Clipboard.SetTextAsync(path);
            ItemsStatusBox.Text = $"Copied: {path}";
        }
        catch
        {
            ItemsStatusBox.Text = path;
        }
    }

    private async void OnWrapFile(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select file to wrap",
            AllowMultiple = false,
        });
        if (files.Count == 0) return;
        var src = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(src)) return;

        var dest = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save wrapped FFXIV file as",
            SuggestedFileName = Path.GetFileNameWithoutExtension(src) + Path.GetExtension(src),
        });
        var destPath = dest?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(destPath)) return;

        var ff = FfPathBox.Text?.Trim();
        await RunBusyAsync("Wrapping…", async log =>
        {
            await TexToolsActions.WrapFileAsync(src, destPath, string.IsNullOrWhiteSpace(ff) ? null : ff, log);
            AppendExtract($"Wrapped → {destPath}");
        });
    }

    private async void OnUnwrapFile(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select FFXIV file to unwrap",
            AllowMultiple = false,
        });
        if (files.Count == 0) return;
        var src = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(src)) return;

        var dest = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save unwrapped file as",
            SuggestedFileName = Path.GetFileNameWithoutExtension(src) + ".png",
        });
        var destPath = dest?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(destPath)) return;

        var ff = FfPathBox.Text?.Trim();
        await RunBusyAsync("Unwrapping…", async log =>
        {
            await TexToolsActions.UnwrapFileAsync(src, destPath, string.IsNullOrWhiteSpace(ff) ? null : ff, log);
            AppendExtract($"Unwrapped → {destPath}");
        });
    }

    private async void OnCreateBackups(object? sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Creating index backups…", async log =>
        {
            await TexToolsActions.CreateIndexBackupsAsync(log);
            OnRefreshBackups(sender, e);
        });
    }

    private async void OnExtractFile(object? sender, RoutedEventArgs e)
    {
        var internalPath = ExtractPathBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(internalPath))
        {
            AppendExtract("Enter an internal FFXIV path first.");
            return;
        }

        var suggested = Path.GetFileName(internalPath.Replace('/', Path.DirectorySeparatorChar));
        var dest = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save extracted file as",
            SuggestedFileName = suggested,
        });
        var destPath = dest?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(destPath)) return;

        await RunBusyAsync("Extracting…", async log =>
        {
            await TexToolsActions.ExtractFileAsync(internalPath, destPath, ExtractSqpackBox.IsChecked == true, log);
            AppendExtract($"OK → {destPath}");
        });
    }

    private async void OnListRoot(object? sender, RoutedEventArgs e)
    {
        var root = RootIdBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(root))
        {
            AppendExtract("Enter a root id (e.g. c0101h0010).");
            return;
        }

        await RunBusyAsync("Listing root…", async log =>
        {
            var files = await TexToolsActions.ListRootFilesAsync(root, log);
            ExtractOutputBox.Text = string.Join(Environment.NewLine, files);
            AppendLog($"Listed {files.Count} files for {root}");
        });
    }

    private void OnOpenPenumbra(object? sender, RoutedEventArgs e)
    {
        var dir = PenumbraAPI.GetPenumbraDirectory();
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            AppendLibrary("Penumbra mod directory not found.");
            return;
        }
        OpenPath(dir);
    }

    private void OnOpenModPacks(object? sender, RoutedEventArgs e) =>
        OpenPath(PlatformPaths.GetTexToolsModPacksDirectory());

    private void OnOpenDataRoot(object? sender, RoutedEventArgs e) =>
        OpenPath(PlatformPaths.GetTexToolsDataRoot());

    private void OnOpenBackups(object? sender, RoutedEventArgs e) =>
        OpenPath(PlatformPaths.GetTexToolsIndexBackupsDirectory());

    private void OnRefreshBackups(object? sender, RoutedEventArgs e)
    {
        var dir = PlatformPaths.GetTexToolsIndexBackupsDirectory();
        Directory.CreateDirectory(dir);
        try
        {
            var backups = ProblemChecker.GetAvailableIndexBackups(dir);
            var valid = false;
            try { valid = GameSession.IsReady && ProblemChecker.AreBackupsValid(dir); }
            catch { /* game info may be unset */ }

            BackupStatusBox.Text =
                $"Folder: {dir}\n" +
                $"Index files found: {backups.Count}\n" +
                $"Valid for current game version: {(GameSession.IsReady ? (valid ? "yes" : "no / incomplete") : "(init game first)")}";
        }
        catch (Exception ex)
        {
            BackupStatusBox.Text = $"Backup status error: {ex.Message}";
        }
    }

    private async void OnRunDoctor(object? sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Doctor…", async log =>
        {
            RefreshLibraryInfo();
            var sb = new StringBuilder();
            sb.AppendLine($"OS: Linux / .NET {Environment.Version}");
            sb.AppendLine($"Game: {ConsoleConfig.Get().XivPath}");
            sb.AppendLine($"Penumbra: {PenumbraAPI.GetPenumbraDirectory() ?? "(none)"}");
            sb.AppendLine($"TexTools data: {PlatformPaths.GetTexToolsDataRoot()}");
            sb.AppendLine($"ModPacks: {PlatformPaths.GetTexToolsModPacksDirectory()}");
            sb.AppendLine($"Backups: {PlatformPaths.GetTexToolsIndexBackupsDirectory()}");
            sb.AppendLine($"launcher.ini: {PlatformPaths.GetLauncherIniPath()}");
            LibraryOutputBox.Text = sb.ToString();
            log.Report("Doctor summary written to Library tab.");
            await Task.CompletedTask;
        });
    }

    private void RefreshLibraryInfo()
    {
        var pen = PenumbraAPI.GetPenumbraDirectory();
        LibraryInfoBox.Text =
            $"Penumbra mod directory: {(string.IsNullOrWhiteSpace(pen) ? "(not found — install Penumbra / set ModDirectory)" : pen)}\n" +
            $"TexTools ModPacks: {PlatformPaths.GetTexToolsModPacksDirectory()}\n" +
            $"Game path: {ConsoleConfig.Get().XivPath}";
    }

    private async Task<string?> PickModpackOpenAsync(string title)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Modpacks")
                {
                    Patterns = ["*.ttmp2", "*.ttmp", "*.pmp", "*.zip"]
                },
                new FilePickerFileType("All") { Patterns = ["*"] },
            ],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private async Task<string?> PickModpackSaveAsync(string title, string suggested)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggested,
            FileTypeChoices =
            [
                new FilePickerFileType("TTMP2") { Patterns = ["*.ttmp2"] },
                new FilePickerFileType("PMP") { Patterns = ["*.pmp"] },
                new FilePickerFileType("All") { Patterns = ["*"] },
            ],
        });
        return file?.TryGetLocalPath();
    }

    private async Task RunBusyAsync(string busyLabel, Func<IProgress<string>, Task> work)
    {
        if (_busy) return;
        _busy = true;
        BusyText.Text = busyLabel;
        var log = new Progress<string>(msg =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                BusyText.Text = msg;
                AppendLog(msg);
            });
        });
        try
        {
            await work(log);
        }
        catch (Exception ex)
        {
            AppendLog("ERROR: " + ex);
            BusyText.Text = "Error — see Log tab.";
            MainTabs.SelectedIndex = MainTabs.ItemCount - 1;
        }
        finally
        {
            _busy = false;
            if (BusyText.Text.StartsWith("ERROR", StringComparison.Ordinal)
                || BusyText.Text.StartsWith("Error", StringComparison.Ordinal))
            { }
            else if (!BusyText.Text.Contains("Ready", StringComparison.Ordinal))
            {
                BusyText.Text = "Ready.";
            }
        }
    }

    private void AppendLog(string text)
    {
        if (string.IsNullOrWhiteSpace(OutputBox.Text))
            OutputBox.Text = text;
        else
            OutputBox.Text += Environment.NewLine + text;
    }

    private void AppendExtract(string text)
    {
        if (string.IsNullOrWhiteSpace(ExtractOutputBox.Text))
            ExtractOutputBox.Text = text;
        else
            ExtractOutputBox.Text += Environment.NewLine + text;
        AppendLog(text);
    }

    private void AppendLibrary(string text)
    {
        LibraryOutputBox.Text = text;
        AppendLog(text);
    }

    private void UpdateContinueEnabled()
    {
        ContinueButton.IsEnabled = IsValidSqPackDir(NormalizeToSqPack(GamePathBox.Text?.Trim() ?? ""));
    }

    private static void OpenPath(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
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
        try { return Directory.EnumerateFiles(path, "*.win32.index").Any(); }
        catch { return false; }
    }

    private void RefreshPaths()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Launcher root: {PlatformPaths.GetLauncherConfigRoot()}");
        sb.AppendLine($"launcher.ini: {(File.Exists(PlatformPaths.GetLauncherIniPath()) ? "found" : "missing")}");
        sb.AppendLine($"Game path (auto): {ConsoleConfig.ResolveDefaultXivPath() ?? "(not found)"}");
        sb.AppendLine($"Configured XivPath: {ConsoleConfig.Get().XivPath}");
        sb.AppendLine($"Penumbra: {PenumbraAPI.GetPenumbraDirectory() ?? "(none)"}");
        sb.AppendLine($"TexTools data: {PlatformPaths.GetTexToolsDataRoot()}");
        PathsBox.Text = sb.ToString();

        var configured = ConsoleConfig.Get().XivPath;
        var detected = ConsoleConfig.ResolveDefaultXivPath();
        if (IsValidSqPackDir(configured))
            GamePathBox.Text = configured!;
        else if (!string.IsNullOrWhiteSpace(detected))
            GamePathBox.Text = detected;
    }
}
