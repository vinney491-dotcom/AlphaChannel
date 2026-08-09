using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AlphaChannel.TexTools.UI;

internal static class MessageBox
{
    public static async Task ShowAsync(Window owner, string message, string title)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 480,
            Height = 280,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new DockPanel
            {
                Margin = new Avalonia.Thickness(16),
                Children =
                {
                    new Button
                    {
                        Content = "OK",
                        Width = 88,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Avalonia.Thickness(0, 12, 0, 0),
                        [DockPanel.DockProperty] = Dock.Bottom,
                    },
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Stretch,
                    },
                },
            },
        };

        var ok = (Button)((DockPanel)dialog.Content!).Children[0];
        ok.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
    }
}
