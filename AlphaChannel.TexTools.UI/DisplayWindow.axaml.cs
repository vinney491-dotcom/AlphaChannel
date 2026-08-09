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
        Panel.StatusChanged += msg => Title = string.IsNullOrWhiteSpace(path)
            ? "Item Viewer"
            : $"Item Viewer — {System.IO.Path.GetFileName(path)}";
        if (!string.IsNullOrWhiteSpace(path))
            _ = Panel.ShowPathAsync(path);
    }
}
