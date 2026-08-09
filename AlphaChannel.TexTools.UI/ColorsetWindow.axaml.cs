using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using xivModdingFramework.Materials.DataContainers;

namespace AlphaChannel.TexTools.UI;

public partial class ColorsetWindow : Window
{
    private XivMtrl? _mtrl;

    public ColorsetWindow()
    {
        InitializeComponent();
    }

    public async Task LoadPathAsync(string mtrlPath)
    {
        HeaderText.Text = mtrlPath;
        ExportButton.IsEnabled = false;
        PreviewImage.Source = null;
        RowsList.ItemsSource = null;
        try
        {
            var (mtrl, rows, preview) = await ColorsetSimple.LoadAsync(mtrlPath);
            _mtrl = mtrl;
            RowsList.ItemsSource = rows;
            PreviewImage.Source = preview;
            ExportButton.IsEnabled = rows.Count > 0;
            if (rows.Count == 0)
                HeaderText.Text = mtrlPath + "\n(No colorset data on this material.)";
        }
        catch (Exception ex)
        {
            HeaderText.Text = "Failed to load colorset:\n" + ex.Message;
        }
    }

    private async void OnExport(object? sender, RoutedEventArgs e)
    {
        if (_mtrl == null) return;
        var dest = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export colorset preview PNG",
            SuggestedFileName = Path.GetFileNameWithoutExtension(_mtrl.MTRLPath ?? "colorset") + "-colorset.png",
        });
        var path = dest?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            await ColorsetSimple.ExportColorsetPngAsync(_mtrl, path);
            HeaderText.Text = $"Exported → {path}";
        }
        catch (Exception ex)
        {
            HeaderText.Text = "Export failed:\n" + ex.Message;
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
