using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AlphaChannel.TexTools.UI;

internal enum DropModpackAction
{
    Cancel,
    ImportPenumbra,
    Upgrade,
}

internal static class DropActionDialog
{
    public static async Task<DropModpackAction> ChooseModpackActionAsync(Window owner, string summary)
    {
        var result = DropModpackAction.Cancel;
        var dialog = new Window
        {
            Title = "Dropped modpack",
            Width = 460,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var importBtn = new Button { Content = "Import to Penumbra", Classes = { "accent" }, MinWidth = 140 };
        var upgradeBtn = new Button { Content = "Upgrade…", MinWidth = 100 };
        var cancelBtn = new Button { Content = "Cancel", MinWidth = 88 };

        importBtn.Click += (_, _) => { result = DropModpackAction.ImportPenumbra; dialog.Close(); };
        upgradeBtn.Click += (_, _) => { result = DropModpackAction.Upgrade; dialog.Close(); };
        cancelBtn.Click += (_, _) => { result = DropModpackAction.Cancel; dialog.Close(); };

        dialog.Content = new DockPanel
        {
            Margin = new Avalonia.Thickness(16),
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Avalonia.Thickness(0, 16, 0, 0),
                    [DockPanel.DockProperty] = Dock.Bottom,
                    Children = { importBtn, upgradeBtn, cancelBtn },
                },
                new TextBlock
                {
                    Text = summary + "\n\nWhat should TexTools do?",
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };

        await dialog.ShowDialog(owner);
        return result;
    }
}
