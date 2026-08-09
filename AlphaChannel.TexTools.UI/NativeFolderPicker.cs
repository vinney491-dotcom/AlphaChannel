using Avalonia.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AlphaChannel.TexTools.UI;

/// <summary>
/// Prefer the desktop's native folder dialog (Dolphin/KDE via kdialog) over our
/// custom window or Wine's Windows common dialog.
/// </summary>
public static class NativeFolderPicker
{
    public static async Task<string?> PickFolderAsync(Window owner, string? startPath = null)
    {
        startPath = NormalizeStart(startPath);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var native = await Task.Run(() => TryLinuxNativeDialog(startPath)).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(native) && Directory.Exists(native))
                return Path.GetFullPath(native);
        }

        var browser = new FolderBrowserWindow(startPath);
        return await browser.ShowDialog<string?>(owner);
    }

    private static string NormalizeStart(string? startPath)
    {
        if (!string.IsNullOrWhiteSpace(startPath) && Directory.Exists(startPath))
            return Path.GetFullPath(startPath);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Directory.Exists(home) ? home : "/";
    }

    private static string? TryLinuxNativeDialog(string startPath)
    {
        // KDE / CachyOS / Dolphin: kdialog uses the native Plasma file dialog
        // (same stack as Dolphin — shows hidden folders, no Wine sandbox).
        var fromKdialog = RunCapture(
            "kdialog",
            $"--getexistingdirectory {Quote(startPath)} {Quote("Select FFXIV install folder")}");
        if (!string.IsNullOrWhiteSpace(fromKdialog))
            return fromKdialog.Trim();

        // GNOME / generic portal-ish fallback
        var fromZenity = RunCapture(
            "zenity",
            $"--file-selection --directory --filename={Quote(startPath + Path.DirectorySeparatorChar)} --title={Quote("Select FFXIV install folder")}");
        if (!string.IsNullOrWhiteSpace(fromZenity))
            return fromZenity.Trim();

        return null;
    }

    private static string Quote(string value) =>
        "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static string? RunCapture(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc == null) return null;
            var stdout = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(120_000);
            if (proc.ExitCode != 0) return null;
            return stdout;
        }
        catch
        {
            return null;
        }
    }
}
