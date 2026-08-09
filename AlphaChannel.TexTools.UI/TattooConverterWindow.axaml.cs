using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace AlphaChannel.TexTools.UI;

public partial class TattooConverterWindow : Window
{
    public TattooConverterWindow()
    {
        InitializeComponent();
        _ = RefreshSlotsAsync();
    }

    private async void OnTypeChanged(object? sender, RoutedEventArgs e) => await RefreshSlotsAsync();

    private async void OnRefreshSlots(object? sender, RoutedEventArgs e) => await RefreshSlotsAsync();

    private async Task RefreshSlotsAsync()
    {
        try
        {
            StatusBox.Text = "Loading decal slots…";
            ConvertButton.IsEnabled = false;
            var face = FacePaintRadio.IsChecked == true;
            var paths = face
                ? await TattooConverter.ListFacePaintPathsAsync()
                : await TattooConverter.ListEquipDecalPathsAsync();
            PathCombo.ItemsSource = paths;
            if (paths.Count > 0)
                PathCombo.SelectedIndex = Math.Min(paths.Count - 1, face ? 0 : 0);
            StatusBox.Text = $"{paths.Count} slots found.";
            ConvertButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            StatusBox.Text = "Failed to list slots:\n" + ex.Message;
        }
    }

    private async void OnBrowseImage(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select tattoo / decal image",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Images")
                {
                    Patterns = ["*.png", "*.tga", "*.dds", "*.bmp", "*.jpg", "*.jpeg"]
                },
            ],
        });
        if (files.Count == 0) return;
        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;
        ImagePathBox.Text = path;
        if (string.IsNullOrWhiteSpace(ModNameBox.Text))
            ModNameBox.Text = Path.GetFileNameWithoutExtension(path) + " Decal";
    }

    private async void OnConvert(object? sender, RoutedEventArgs e)
    {
        var image = ImagePathBox.Text?.Trim();
        var gamePath = PathCombo.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(image) || !File.Exists(image))
        {
            StatusBox.Text = "Pick an image first.";
            return;
        }
        if (string.IsNullOrWhiteSpace(gamePath))
        {
            StatusBox.Text = "Pick a target decal slot.";
            return;
        }

        ConvertButton.IsEnabled = false;
        try
        {
            var log = new Progress<string>(msg => StatusBox.Text = (StatusBox.Text ?? "") + "\n" + msg);
            StatusBox.Text = "Working…";
            var dest = await TattooConverter.ConvertToPenumbraAsync(
                image,
                gamePath,
                modName: string.IsNullOrWhiteSpace(ModNameBox.Text) ? null : ModNameBox.Text.Trim(),
                log: log);
            StatusBox.Text += $"\nDone → {dest}";
        }
        catch (Exception ex)
        {
            StatusBox.Text = "ERROR:\n" + ex;
        }
        finally
        {
            ConvertButton.IsEnabled = true;
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
