using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AlphaChannel.TexTools.UI;

public partial class FolderBrowserWindow : Window
{
    public string? SelectedPath { get; private set; }

    private string _current = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private bool _showHidden = true;

    public FolderBrowserWindow(string? startPath = null)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(startPath) && Directory.Exists(startPath))
        {
            _current = startPath;
        }
        ShowHiddenToggle.IsChecked = true;
        Navigate(_current);
    }

    private void Navigate(string path)
    {
        try
        {
            path = Path.GetFullPath(path);
            if (!Directory.Exists(path))
            {
                StatusText.Text = "Path does not exist.";
                return;
            }

            _current = path;
            PathBox.Text = _current;

            var entries = new List<string>();
            IEnumerable<string> dirs = Directory.EnumerateDirectories(_current);
            if (!_showHidden)
            {
                dirs = dirs.Where(d => !Path.GetFileName(d).StartsWith('.'));
            }

            foreach (var d in dirs.OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.GetFileName(d);
                if (name.StartsWith('.'))
                {
                    entries.Add($"[hidden] {name}");
                }
                else
                {
                    entries.Add(name);
                }
            }

            EntriesList.ItemsSource = entries;
            StatusText.Text = DescribeCurrent();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Cannot open: {ex.Message}";
        }
    }

    private string DescribeCurrent()
    {
        var sq = Path.Combine(_current, "game", "sqpack", "ffxiv");
        if (Directory.Exists(sq))
            return "Looks like a game root (has game/sqpack/ffxiv). Safe to Select.";
        if (_current.Replace('\\', '/').EndsWith("/game/sqpack/ffxiv", StringComparison.OrdinalIgnoreCase)
            || _current.EndsWith($"{Path.DirectorySeparatorChar}sqpack{Path.DirectorySeparatorChar}ffxiv", StringComparison.OrdinalIgnoreCase))
            return "Looks like the sqpack/ffxiv folder. Safe to Select.";
        return "Navigate into your FFXIV install, then Select.";
    }

    private void OnGo(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(PathBox.Text))
            Navigate(PathBox.Text.Trim());
    }

    private void OnPathKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            OnGo(sender, e);
    }

    private void OnToggleHidden(object? sender, RoutedEventArgs e)
    {
        _showHidden = ShowHiddenToggle.IsChecked == true;
        Navigate(_current);
    }

    private void OnJumpHome(object? sender, RoutedEventArgs e) =>
        Navigate(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    private void OnJumpXlcore(object? sender, RoutedEventArgs e)
    {
        var xl = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".xlcore");
        if (Directory.Exists(xl))
            Navigate(xl);
        else
            StatusText.Text = "~/.xlcore does not exist yet.";
    }

    private void OnParent(object? sender, RoutedEventArgs e)
    {
        var parent = Directory.GetParent(_current)?.FullName;
        if (!string.IsNullOrEmpty(parent))
            Navigate(parent);
    }

    private void OnEntryActivate(object? sender, TappedEventArgs e)
    {
        if (EntriesList.SelectedItem is not string item) return;
        var name = item.StartsWith("[hidden] ") ? item["[hidden] ".Length..] : item;
        Navigate(Path.Combine(_current, name));
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    private void OnSelect(object? sender, RoutedEventArgs e)
    {
        SelectedPath = _current;
        Close(SelectedPath);
    }
}
