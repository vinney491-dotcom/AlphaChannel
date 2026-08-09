using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace AlphaChannel.TexTools.UI;

public partial class DisplayPanel : UserControl
{
    private DisplayPreviewResult? _current;
    private string? _path;
    private bool _updatingChannels;
    public event Action<string>? StatusChanged;
    public Func<string, Task>? ExportRequested { get; set; }

    public DisplayPanel()
    {
        InitializeComponent();
    }

    public bool ShowPopOut
    {
        get => PopOutButton.IsVisible;
        set => PopOutButton.IsVisible = value;
    }

    public async Task ShowPathAsync(string? internalPath, IProgress<string>? log = null)
    {
        await ShowPreviewAsync(internalPath, external: false, log);
    }

    public async Task ShowExternalAsync(string? externalPath, IProgress<string>? log = null)
    {
        await ShowPreviewAsync(externalPath, external: true, log);
    }

    private async Task ShowPreviewAsync(string? path, bool external, IProgress<string>? log)
    {
        _path = path;
        if (string.IsNullOrWhiteSpace(path))
        {
            Clear();
            return;
        }

        TitleText.Text = Path.GetFileName(path);
        PlaceholderText.Text = "Loading…";
        PlaceholderText.IsVisible = true;
        PreviewImage.IsVisible = false;
        DetailBox.IsVisible = false;
        ExportButton.IsEnabled = false;
        ChannelPanel.IsVisible = false;

        try
        {
            var result = external
                ? await DisplayPreview.LoadExternalAsync(path, log)
                : await DisplayPreview.LoadAsync(path, log);
            Apply(result);
        }
        catch (Exception ex)
        {
            Clear();
            TitleText.Text = Path.GetFileName(path);
            PlaceholderText.Text = "Failed to load preview";
            PlaceholderText.IsVisible = true;
            DetailBox.Text = ex.Message;
            DetailBox.IsVisible = true;
            InfoText.Text = ex.Message;
            StatusChanged?.Invoke($"Display error: {ex.Message}");
        }
    }

    public void Clear()
    {
        _current = null;
        _path = null;
        TitleText.Text = "Display";
        InfoText.Text = "Select a file to preview.";
        PlaceholderText.Text = "No preview";
        PlaceholderText.IsVisible = true;
        PreviewImage.Source = null;
        PreviewImage.IsVisible = false;
        DetailBox.Text = "";
        DetailBox.IsVisible = false;
        ExportButton.IsEnabled = false;
        ChannelPanel.IsVisible = false;
    }

    private void Apply(DisplayPreviewResult result)
    {
        _current = result;
        TitleText.Text = Path.GetFileName(result.Path);
        InfoText.Text = result.Info;
        ExportButton.IsEnabled = result.Kind is DisplayKind.Texture or DisplayKind.Model or DisplayKind.Material or DisplayKind.Other;

        if (result.Kind == DisplayKind.Texture && result.Image != null)
        {
            PreviewImage.Source = result.Image;
            PreviewImage.IsVisible = true;
            DetailBox.IsVisible = false;
            PlaceholderText.IsVisible = false;
            ChannelPanel.IsVisible = result.RgbaPixels != null;
        }
        else if (!string.IsNullOrWhiteSpace(result.Detail))
        {
            PreviewImage.IsVisible = false;
            DetailBox.Text = result.Detail;
            DetailBox.IsVisible = true;
            PlaceholderText.IsVisible = false;
            ChannelPanel.IsVisible = false;
        }
        else
        {
            PreviewImage.IsVisible = false;
            DetailBox.IsVisible = false;
            PlaceholderText.Text = result.Info;
            PlaceholderText.IsVisible = true;
            ChannelPanel.IsVisible = false;
        }

        StatusChanged?.Invoke(result.Info);
    }

    private void OnChannelChanged(object? sender, RoutedEventArgs e)
    {
        if (_updatingChannels || _current?.Kind != DisplayKind.Texture || _current.RgbaPixels == null)
            return;

        _updatingChannels = true;
        try
        {
            PreviewImage.Source = DisplayPreview.CreateBitmap(
                _current.RgbaPixels,
                _current.Width,
                _current.Height,
                RedBox.IsChecked == true,
                GreenBox.IsChecked == true,
                BlueBox.IsChecked == true,
                AlphaBox.IsChecked == true);
        }
        finally
        {
            _updatingChannels = false;
        }
    }

    private static bool IsExternalFile(string path)
        => Path.IsPathRooted(path) && File.Exists(path);

    private async void OnExport(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_path)) return;

        if (!IsExternalFile(_path) && ExportRequested != null)
        {
            await ExportRequested.Invoke(_path);
            return;
        }

        var ext = Path.GetExtension(_path).ToLowerInvariant();
        var suggested = Path.GetFileNameWithoutExtension(_path) + (ext is ".tex" or ".atex" or ".dds" ? ".png" : ext);
        var dest = await TopLevel.GetTopLevel(this)!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export file",
            SuggestedFileName = suggested,
        });
        var destPath = dest?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(destPath)) return;

        if (IsExternalFile(_path))
        {
            if (ext is ".tex" or ".atex" or ".dds")
            {
                var preview = await DisplayPreview.LoadExternalAsync(_path);
                if (preview.RgbaPixels != null)
                {
                    // Re-load via framework SaveAs for tex/dds → png
                    if (ext is ".tex" or ".atex")
                    {
                        var tex = xivModdingFramework.Textures.DataContainers.XivTex.FromUncompressedTex(
                            await File.ReadAllBytesAsync(_path));
                        await tex.SaveAs(destPath);
                    }
                    else
                    {
                        var tex = xivModdingFramework.Textures.DataContainers.XivTex.FromUncompressedTex(
                            xivModdingFramework.Textures.FileTypes.Tex.DDSToUncompressedTex(_path));
                        await tex.SaveAs(destPath);
                    }
                }
                else
                {
                    File.Copy(_path, destPath, overwrite: true);
                }
            }
            else
            {
                File.Copy(_path, destPath, overwrite: true);
            }
        }
        else
        {
            await TexToolsActions.ExtractFileAsync(_path, destPath, sqpack: false);
        }

        StatusChanged?.Invoke($"Exported → {destPath}");
    }

    private async void OnPopOut(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_path)) return;
        var win = new DisplayWindow();
        if (TopLevel.GetTopLevel(this) is Window owner)
            win.Show(owner);
        else
            win.Show();

        if (IsExternalFile(_path))
            await win.ShowExternalPathAsync(_path);
        else
            await win.Panel.ShowPathAsync(_path);
    }
}
