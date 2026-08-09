using System.Threading.Tasks;
using Avalonia.Controls;

namespace AlphaChannel.TexTools.UI;

public partial class DisplayWindow : Window
{
    public DisplayWindow() : this(null)
    {
    }

    public DisplayWindow(string? path)
    {
        InitializeComponent();
        Panel.ShowPopOut = false;
        Panel.StatusChanged += msg =>
        {
            if (!string.IsNullOrWhiteSpace(path))
                Title = $"Item Viewer — {System.IO.Path.GetFileName(path)}";
            else
                Title = string.IsNullOrWhiteSpace(msg) ? "Item Viewer" : $"Item Viewer — {msg}";
        };
        if (!string.IsNullOrWhiteSpace(path))
            _ = Panel.ShowPathAsync(path);
    }

    public async Task ShowExternalPathAsync(string externalPath)
    {
        Title = $"Item Viewer — {System.IO.Path.GetFileName(externalPath)}";
        await Panel.ShowExternalAsync(externalPath);
    }
}
