// xivModdingFramework — AlphaChannel Linux port additions
// Copyright © 2018 Rafael Gonzalez / TexTools contributors — GPL-3.0
//
// Path helpers for Windows XIVLauncher and Linux XIVLauncher.Core (xlcore).

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace xivModdingFramework.Helpers
{
    /// <summary>
    /// Cross-platform locations for launcher config, Penumbra, and TexTools user data.
    /// </summary>
    public static class PlatformPaths
    {
        public static bool IsWindows =>
#if NETSTANDARD2_0
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#else
            OperatingSystem.IsWindows();
#endif

        public static bool IsLinux =>
#if NETSTANDARD2_0
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
#else
            OperatingSystem.IsLinux();
#endif

        public static bool IsMacOS =>
#if NETSTANDARD2_0
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
#else
            OperatingSystem.IsMacOS();
#endif

        /// <summary>
        /// Directory that contains launcherConfigV3.json / dalamudConfig.json style files.
        /// Windows: %AppData%/XIVLauncher
        /// Linux (XIVLauncher.Core): ~/.xlcore
        /// macOS: ~/Library/Application Support/XIV on Mac (common XLCore layout) or ~/.xlcore
        /// </summary>
        public static string GetLauncherConfigRoot()
        {
            if (IsLinux)
            {
                var xlcore = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".xlcore");
                if (Directory.Exists(xlcore))
                {
                    return xlcore;
                }
            }

            if (IsMacOS)
            {
                var macXl = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support", "XIV on Mac");
                if (Directory.Exists(macXl))
                {
                    return macXl;
                }

                var xlcore = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".xlcore");
                if (Directory.Exists(xlcore))
                {
                    return xlcore;
                }
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XIVLauncher");
        }

        public static string GetLauncherConfigV3Path() =>
            Path.Combine(GetLauncherConfigRoot(), "launcherConfigV3.json");

        public static string GetDalamudConfigPath() =>
            Path.Combine(GetLauncherConfigRoot(), "dalamudConfig.json");

        public static string GetPenumbraConfigPath() =>
            Path.Combine(GetLauncherConfigRoot(), "pluginConfigs", "Penumbra.json");

        /// <summary>
        /// Default TexTools user data root (Saved / ModPacks / Index_Backups).
        /// Windows: Documents/TexTools
        /// Linux/macOS: ~/.local/share/TexTools (XDG-ish) with Documents/TexTools fallback if present.
        /// </summary>
        public static string GetTexToolsDataRoot()
        {
            var documents = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "TexTools");
            if (Directory.Exists(documents) || IsWindows)
            {
                return documents;
            }

            var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (string.IsNullOrWhiteSpace(xdg))
            {
                xdg = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".local", "share");
            }

            return Path.Combine(xdg, "TexTools");
        }

        public static string GetTexToolsSavedDirectory() =>
            Path.Combine(GetTexToolsDataRoot(), "Saved");

        public static string GetTexToolsIndexBackupsDirectory() =>
            Path.Combine(GetTexToolsDataRoot(), "Index_Backups");

        public static string GetTexToolsModPacksDirectory() =>
            Path.Combine(GetTexToolsDataRoot(), "ModPacks");

        /// <summary>
        /// Drive/volume root for filesystem size limits. Accepts full paths on all platforms.
        /// </summary>
        public static string GetPathRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return IsWindows ? "C:\\" : "/";
            }

            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(root))
                {
                    return root;
                }
            }
            catch
            {
                // Fall through.
            }

            return IsWindows ? path.Substring(0, 1) : "/";
        }
    }
}
