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
    private bool _updatingCombos;
    private List<ItemRow> _allItems = new();
    private ItemRow? _currentItem;
    private string? _currentFilePath;
    private List<PenumbraModEntry> _penumbraMods = new();

    private sealed class FileChoice
    {
        public FileChoice(string path) => Path = path;
        public string Path { get; }
        public override string ToString() => System.IO.Path.GetFileName(Path);
    }

    public MainWindow()
    {
        InitializeComponent();
        MainDisplay.StatusChanged += msg => BusyText.Text = msg;
        MainDisplay.ExportRequested = async path =>
        {
            ExtractPathBox.Text = path;
            FilePathBox.Text = path;
            OnExtractFile(null, new RoutedEventArgs());
            await Task.CompletedTask;
        };
        AddHandler(DragDrop.DragEnterEvent, OnWindowDragEnter);
        AddHandler(DragDrop.DragOverEvent, OnWindowDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnWindowDragLeave);
        AddHandler(DragDrop.DropEvent, OnWindowDrop);
        RefreshPaths();
        GamePathBox.Text = ConsoleConfig.Get().XivPath
                           ?? ConsoleConfig.ResolveDefaultXivPath()
                           ?? "";
        UpdateContinueEnabled();

        var configured = ConsoleConfig.Get().XivPath;
        if (IsValidSqPackDir(configured))
            _ = EnterWorkspaceAsync();
    }

    private void OnWindowDragEnter(object? sender, DragEventArgs e) => UpdateDropOverlay(e, show: true);

    private void OnWindowDragOver(object? sender, DragEventArgs e) => UpdateDropOverlay(e, show: true);

    private void OnWindowDragLeave(object? sender, DragEventArgs e)
    {
        // Leaving a child still bubbles; only hide when pointer left the window.
        var pos = e.GetPosition(this);
        if (pos.X < 0 || pos.Y < 0 || pos.X > Bounds.Width || pos.Y > Bounds.Height)
            DropOverlay.IsVisible = false;
    }

    private void UpdateDropOverlay(DragEventArgs e, bool show)
    {
        var paths = FileDropSupport.GetLocalPaths(e.Data);
        var kind = FileDropSupport.Classify(paths);
        if (kind == DropKind.None)
        {
            e.DragEffects = DragDropEffects.None;
            DropOverlay.IsVisible = false;
            return;
        }

        e.DragEffects = DragDropEffects.Copy;
        if (show)
        {
            DropOverlayText.Text = FileDropSupport.OverlayHint(kind, paths.Count);
            DropOverlay.IsVisible = true;
        }
    }

    private async void OnWindowDrop(object? sender, DragEventArgs e)
    {
        DropOverlay.IsVisible = false;
        var paths = FileDropSupport.GetLocalPaths(e.Data);
        var kind = FileDropSupport.Classify(paths);
        if (kind == DropKind.None)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Copy;
        await HandleDroppedPathsAsync(paths, kind);
    }

    private async Task HandleDroppedPathsAsync(IReadOnlyList<string> paths, DropKind kind)
    {
        if (kind == DropKind.GameFolder || (SetupPanel.IsVisible && paths.Any(Directory.Exists)))
        {
            var folder = paths.FirstOrDefault(p => Directory.Exists(p) && FileDropSupport.LooksLikeSqPack(p))
                         ?? paths.FirstOrDefault(Directory.Exists);
            if (folder != null)
            {
                var normalized = NormalizeToSqPack(folder);
                if (IsValidSqPackDir(normalized))
                {
                    GamePathBox.Text = normalized;
                    PathStatus.Text = $"Dropped game path: {normalized}";
                    UpdateContinueEnabled();
                    if (!SetupPanel.IsVisible)
                        ShowSetup();
                    BusyText.Text = $"Game path set from drop: {normalized}";
                    return;
                }
            }
        }

        if (kind is DropKind.Modpack or DropKind.Mixed)
        {
            var packs = FileDropSupport.ExpandModpacks(paths).ToList();
            if (packs.Count == 0)
            {
                BusyText.Text = "No modpack files in drop.";
                return;
            }

            var summary = packs.Count == 1
                ? Path.GetFileName(packs[0])
                : $"{packs.Count} modpacks\n" + string.Join("\n", packs.Take(5).Select(Path.GetFileName))
                  + (packs.Count > 5 ? $"\n… +{packs.Count - 5} more" : "");
            var action = await DropActionDialog.ChooseModpackActionAsync(this, summary);
            if (action == DropModpackAction.Cancel) return;

            if (action == DropModpackAction.ImportPenumbra)
            {
                await RunBusyAsync("Importing dropped modpack(s)…", async log =>
                {
                    foreach (var src in packs)
                    {
                        log.Report($"Import: {Path.GetFileName(src)}");
                        await TexToolsActions.ImportToPenumbraAsync(src, log);
                    }
                });
                return;
            }

            if (packs.Count == 1)
            {
                var src = packs[0];
                var dest = await PickModpackSaveAsync(
                    "Save upgraded modpack as",
                    Path.GetFileNameWithoutExtension(src) + "-upgraded" + Path.GetExtension(src));
                if (dest == null) return;
                await RunBusyAsync("Upgrading dropped modpack…", async log =>
                {
                    await TexToolsActions.UpgradeModpackAsync(src, dest, log);
                });
            }
            else
            {
                var destFolder = await NativeFolderPicker.PickFolderAsync(this, Path.GetDirectoryName(packs[0]));
                if (string.IsNullOrWhiteSpace(destFolder)) return;
                await RunBusyAsync("Upgrading dropped modpacks…", async log =>
                {
                    foreach (var src in packs)
                    {
                        var name = Path.GetFileNameWithoutExtension(src) + "-upgraded" + Path.GetExtension(src);
                        var dest = Path.Combine(destFolder, name);
                        log.Report($"Upgrade: {Path.GetFileName(src)}");
                        await TexToolsActions.UpgradeModpackAsync(src, dest, log);
                    }
                });
            }

            return;
        }

        if (kind == DropKind.Texture)
        {
            var file = paths.First(f => File.Exists(f) && FileDropSupport.TextureExts.Contains(Path.GetExtension(f)));
            if (SetupPanel.IsVisible)
            {
                BusyText.Text = "Start TexTools (set game path) before previewing textures — still showing image.";
            }
            if (SetupPanel.IsVisible)
            {
                // Keep setup; open pop-out viewer for the external file
                var win = new DisplayWindow();
                win.Show(this);
                await win.ShowExternalPathAsync(file);
            }
            else
            {
                await MainDisplay.ShowExternalAsync(file, new Progress<string>(msg => BusyText.Text = msg));
            }
        }
    }

    private async Task EnterWorkspaceAsync()
    {
        ShowWorkspaceChrome();
        RefreshLibraryInfo();
        _ = RefreshPenumbraLibraryAsync();
        OnRefreshBackups(null!, new RoutedEventArgs());
        await RunBusyAsync("Initializing…", async log =>
        {
            await GameSession.EnsureInitializedAsync(log);
            log.Report("Ready. View → Load Item List, then pick an item. Mods → Import to Penumbra for modpacks.");
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
        ExtraPanel.IsVisible = true;
    }

    private void OnShowExtraPanel(object? sender, RoutedEventArgs e)
    {
        ExtraPanel.IsVisible = !ExtraPanel.IsVisible;
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
            "Classic dark TexTools shell on Avalonia (Linux).\n" +
            "Item List + Model/Material/Texture selectors + display viewport.\n" +
            "Texture preview works; interactive Helix 3D remains Windows/WPF-only.\n" +
            "SAVE TO FFXIV → Penumbra import on Linux.\n\n" +
            "Upstream: TexTools / xivModdingFramework (GPL-3.0).",
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
        if (!await EnsurePenumbraFolderAsync())
            return;
        var src = await PickModpackOpenAsync("Import modpack to Penumbra");
        if (src == null) return;
        await RunBusyAsync("Importing to Penumbra…", async log =>
        {
            var dest = await TexToolsActions.ImportToPenumbraAsync(src, log);
            log.Report($"Done: {dest}");
            await RefreshPenumbraLibraryAsync();
        });
        ExtraPanel.IsVisible = true;
    }

    private async Task<bool> EnsurePenumbraFolderAsync()
    {
        var dir = PenumbraAPI.GetPenumbraDirectory();
        if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            return true;

        await MessageBox.ShowAsync(
            this,
            "Penumbra mod folder is not set.\n\n" +
            "Choose the folder Penumbra uses as its root (the directory that contains individual mod folders).\n\n" +
            PenumbraAPI.DescribePenumbraDiscovery(),
            "Set Penumbra folder");
        OnSetPenumbraFolder(null, new RoutedEventArgs());
        dir = PenumbraAPI.GetPenumbraDirectory();
        return !string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir);
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
            ItemsStatusBox.Text = $"{_allItems.Count} items · Cache Worker Paused";
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
                i.Display.Contains(q, StringComparison.OrdinalIgnoreCase)
                || i.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        var list = view.Take(5000).ToList();
        ItemsList.ItemsSource = list;
        CategoryTree.ItemsSource = BuildCategoryTree(list);
        if (_allItems.Count > 5000 && string.IsNullOrEmpty(q))
            ItemsStatusBox.Text = $"Showing first 5000 of {_allItems.Count} — use search to narrow.";
        else if (!string.IsNullOrEmpty(q))
            ItemsStatusBox.Text = $"{list.Count} match · Cache Worker Paused";
    }

    private static List<ItemTreeNode> BuildCategoryTree(IEnumerable<ItemRow> items)
    {
        var roots = new List<ItemTreeNode>();
        foreach (var primaryGroup in items.GroupBy(i => string.IsNullOrWhiteSpace(i.PrimaryCategory) ? "Other" : i.PrimaryCategory)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var primaryNode = new ItemTreeNode(primaryGroup.Key);
            foreach (var secondaryGroup in primaryGroup.GroupBy(i => string.IsNullOrWhiteSpace(i.SecondaryCategory) ? "General" : i.SecondaryCategory)
                         .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                var secondaryNode = new ItemTreeNode(secondaryGroup.Key);
                foreach (var item in secondaryGroup.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).Take(500))
                    secondaryNode.Children.Add(new ItemTreeNode(item.Name, item));
                primaryNode.Children.Add(secondaryNode);
            }
            roots.Add(primaryNode);
        }
        return roots;
    }

    private async void OnCategoryTreeSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (CategoryTree.SelectedItem is ItemTreeNode { Item: { } row })
            await SelectItemAsync(row);
    }

    private async void OnItemSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (ItemsList.SelectedItem is ItemRow row)
            await SelectItemAsync(row);
    }

    private async Task SelectItemAsync(ItemRow row)
    {
        _currentItem = row;
        ItemNameBox.Text = row.Name;
        await RunBusyAsync("Listing item files…", async log =>
        {
            var files = await TexToolsActions.ListItemFilesAsync(row.Item, log);
            ItemFilesList.ItemsSource = files;
            ItemsStatusBox.Text = $"{row.Name} — {files.Count} files · Cache Worker Paused";
            PopulateFileCombos(files);
            var prefer = DisplayPreview.PreferDisplayFile(files);
            if (!string.IsNullOrWhiteSpace(prefer))
                await ShowFileInViewAsync(prefer);
            else
            {
                FilePathBox.Text = "No previewable files";
                await MainDisplay.ShowPathAsync(null);
            }
        });
    }

    private void PopulateFileCombos(IReadOnlyList<string> files)
    {
        _updatingCombos = true;
        try
        {
            ModelCombo.ItemsSource = files.Where(f => f.EndsWith(".mdl", StringComparison.OrdinalIgnoreCase))
                .Select(f => new FileChoice(f)).ToList();
            MaterialCombo.ItemsSource = files.Where(f => f.EndsWith(".mtrl", StringComparison.OrdinalIgnoreCase))
                .Select(f => new FileChoice(f)).ToList();
            TextureCombo.ItemsSource = files.Where(f =>
                    f.EndsWith(".tex", StringComparison.OrdinalIgnoreCase)
                    || f.EndsWith(".atex", StringComparison.OrdinalIgnoreCase))
                .Select(f => new FileChoice(f)).ToList();

            if (ModelCombo.Items.Count > 0) ModelCombo.SelectedIndex = 0;
            if (MaterialCombo.Items.Count > 0) MaterialCombo.SelectedIndex = 0;
            if (TextureCombo.Items.Count > 0) TextureCombo.SelectedIndex = 0;
        }
        finally
        {
            _updatingCombos = false;
        }
    }

    private async Task ShowFileInViewAsync(string path)
    {
        _currentFilePath = path;
        ExtractPathBox.Text = path;
        FilePathBox.Text = path;
        ItemFilesList.SelectedItem = path;
        await MainDisplay.ShowPathAsync(path, new Progress<string>(msg => BusyText.Text = msg));
    }

    private async void OnItemFileSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (ItemFilesList.SelectedItem is not string path) return;
        if (string.Equals(path, _currentFilePath, StringComparison.Ordinal)) return;
        await ShowFileInViewAsync(path);
    }

    private async void OnModelComboChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingCombos || ModelCombo.SelectedItem is not FileChoice choice) return;
        await ShowFileInViewAsync(choice.Path);
    }

    private async void OnMaterialComboChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingCombos || MaterialCombo.SelectedItem is not FileChoice choice) return;
        await ShowFileInViewAsync(choice.Path);
    }

    private async void OnTextureComboChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingCombos || TextureCombo.SelectedItem is not FileChoice choice) return;
        await ShowFileInViewAsync(choice.Path);
    }

    private async void OnShowModel(object? sender, RoutedEventArgs e)
    {
        if (ModelCombo.SelectedItem is FileChoice c)
            await ShowFileInViewAsync(c.Path);
        else
            BusyText.Text = "No model for this item.";
    }

    private async void OnShowMaterial(object? sender, RoutedEventArgs e)
    {
        if (MaterialCombo.SelectedItem is FileChoice c)
            await ShowFileInViewAsync(c.Path);
        else
            BusyText.Text = "No material for this item.";
    }

    private async void OnShowTexture(object? sender, RoutedEventArgs e)
    {
        if (TextureCombo.SelectedItem is FileChoice c)
            await ShowFileInViewAsync(c.Path);
        else
            BusyText.Text = "No texture for this item.";
    }

    private async void OnRefreshItemView(object? sender, RoutedEventArgs e)
    {
        if (_currentItem != null)
            await SelectItemAsync(_currentItem);
        else
            BusyText.Text = "No item selected.";
    }

    private void OnOpenDisplayWindow(object? sender, RoutedEventArgs e)
    {
        var path = _currentFilePath ?? ExtractPathBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            BusyText.Text = "Select an item file first.";
            return;
        }

        if (Path.IsPathRooted(path) && File.Exists(path))
        {
            var win = new DisplayWindow();
            win.Show(this);
            _ = win.ShowExternalPathAsync(path);
            return;
        }

        var viewer = new DisplayWindow(path);
        viewer.Show(this);
    }

    private async void OnSaveAsCurrent(object? sender, RoutedEventArgs e)
    {
        var path = _currentFilePath ?? ExtractPathBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            BusyText.Text = "Nothing to save — select a model/material/texture.";
            return;
        }

        ExtractPathBox.Text = path;
        OnExtractFile(sender, e);
        await Task.CompletedTask;
    }

    private async void OnLoadExternalToDisplay(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load external file into display",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Textures / Images")
                {
                    Patterns = ["*.tex", "*.atex", "*.dds", "*.png", "*.bmp", "*.tga", "*.jpg", "*.jpeg"]
                },
                new FilePickerFileType("All") { Patterns = ["*"] },
            ],
        });
        if (files.Count == 0) return;
        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;
        FilePathBox.Text = path;
        _currentFilePath = path;
        await MainDisplay.ShowExternalAsync(path, new Progress<string>(msg => BusyText.Text = msg));
    }

    private async void OnSaveToFfxiv(object? sender, RoutedEventArgs e)
    {
        await MessageBox.ShowAsync(
            this,
            "On Linux, TexTools does not write DATs directly.\n\n" +
            "• Modpacks: Mods → Import Modpack to Penumbra\n" +
            "• Single files: use SAVE AS to extract\n\n" +
            "Opening Import to Penumbra…",
            "SAVE TO FFXIV");
        OnImportPenumbra(sender, e);
    }

    private async void OnExtractSelectedItemFile(object? sender, RoutedEventArgs e)
    {
        var path = _currentFilePath ?? ItemFilesList.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(path))
        {
            ItemsStatusBox.Text = "Select a file first.";
            return;
        }
        ExtractPathBox.Text = path;
        OnExtractFile(sender, e);
        await Task.CompletedTask;
    }

    private async void OnCopySelectedFilePath(object? sender, RoutedEventArgs e)
    {
        var path = _currentFilePath ?? ItemFilesList.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(path)) return;
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

    private async void OnSetPenumbraFolder(object? sender, RoutedEventArgs e)
    {
        var current = PenumbraAPI.GetPenumbraDirectory();
        var start = !string.IsNullOrWhiteSpace(current) && Directory.Exists(current)
            ? current
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var folder = await NativeFolderPicker.PickFolderAsync(this, start);
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return;

        ConsoleConfig.Update(c => c.PenumbraModDirectory = Path.GetFullPath(folder));
        RefreshLibraryInfo();
        await RefreshPenumbraLibraryAsync();
        AppendLibrary($"Penumbra mod directory set to:\n{Path.GetFullPath(folder)}\n\n{PenumbraAPI.DescribePenumbraDiscovery()}");
        BusyText.Text = $"Penumbra folder: {Path.GetFullPath(folder)}";
        ExtraPanel.IsVisible = true;
    }

    private async void OnRefreshPenumbraLibrary(object? sender, RoutedEventArgs e)
        => await RefreshPenumbraLibraryAsync();

    private void OnPenumbraSearchKeyUp(object? sender, KeyEventArgs e) => ApplyPenumbraFilter();

    private async Task RefreshPenumbraLibraryAsync()
    {
        try
        {
            _penumbraMods = (await PenumbraLibrary.ListModsAsync()).ToList();
            ApplyPenumbraFilter();
            RefreshLibraryInfo();
        }
        catch (Exception ex)
        {
            AppendLibrary("Penumbra library refresh failed: " + ex.Message);
        }
    }

    private void ApplyPenumbraFilter()
    {
        var q = PenumbraSearchBox.Text?.Trim() ?? "";
        IEnumerable<PenumbraModEntry> view = _penumbraMods;
        if (!string.IsNullOrEmpty(q))
        {
            view = _penumbraMods.Where(m =>
                m.Display.Contains(q, StringComparison.OrdinalIgnoreCase)
                || m.FolderPath.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
        PenumbraModsList.ItemsSource = view.ToList();
    }

    private void OnOpenSelectedPenumbraMod(object? sender, RoutedEventArgs e)
    {
        if (PenumbraModsList.SelectedItem is not PenumbraModEntry entry)
        {
            BusyText.Text = "Select a Penumbra mod in the library list.";
            return;
        }
        OpenPath(entry.FolderPath);
    }

    private async void OnReloadSelectedPenumbraMod(object? sender, RoutedEventArgs e)
    {
        if (PenumbraModsList.SelectedItem is not PenumbraModEntry entry)
        {
            BusyText.Text = "Select a Penumbra mod in the library list.";
            return;
        }
        var ok = await PenumbraLibrary.ReloadAsync(entry);
        BusyText.Text = ok
            ? $"Penumbra reload OK: {entry.Name}"
            : $"Reload failed for {entry.Name} (is the game + Penumbra API on :42069 running?).";
        AppendLibrary(BusyText.Text);
    }

    private void OnOpenTattooConverter(object? sender, RoutedEventArgs e)
    {
        var win = new TattooConverterWindow();
        win.Show(this);
    }

    private async void OnOpenColorset(object? sender, RoutedEventArgs e)
    {
        var path = (MaterialCombo.SelectedItem as FileChoice)?.Path
                   ?? _currentFilePath;
        if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".mtrl", StringComparison.OrdinalIgnoreCase))
        {
            BusyText.Text = "Select a material (.mtrl) in the Material combo first.";
            return;
        }
        var win = new ColorsetWindow();
        win.Show(this);
        await win.LoadPathAsync(path);
    }

    private void OnOpenPenumbra(object? sender, RoutedEventArgs e)
    {
        var dir = PenumbraAPI.GetPenumbraDirectory();
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            AppendLibrary("Penumbra mod directory not found.\n\n" + PenumbraAPI.DescribePenumbraDiscovery()
                          + "\n\nUse Options → Set Penumbra Mods Folder…");
            ExtraPanel.IsVisible = true;
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
            sb.AppendLine($"TexTools data: {PlatformPaths.GetTexToolsDataRoot()}");
            sb.AppendLine($"ModPacks: {PlatformPaths.GetTexToolsModPacksDirectory()}");
            sb.AppendLine($"Backups: {PlatformPaths.GetTexToolsIndexBackupsDirectory()}");
            sb.AppendLine($"launcher.ini: {PlatformPaths.GetLauncherIniPath()}");
            sb.AppendLine();
            sb.AppendLine(PenumbraAPI.DescribePenumbraDiscovery());
            LibraryOutputBox.Text = sb.ToString();
            AppendLibrary(sb.ToString());
            ExtraPanel.IsVisible = true;
            log.Report("Doctor summary written to side panel Log.");
            await Task.CompletedTask;
        });
    }

    private void RefreshLibraryInfo()
    {
        var pen = PenumbraAPI.GetPenumbraDirectory();
        LibraryInfoBox.Text =
            $"Penumbra mod directory: {(string.IsNullOrWhiteSpace(pen) ? "(not found — Options → Set Penumbra Mods Folder…)" : pen)}\n" +
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
            BusyText.Text = "Error — open = panel for Log.";
            ExtraPanel.IsVisible = true;
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
