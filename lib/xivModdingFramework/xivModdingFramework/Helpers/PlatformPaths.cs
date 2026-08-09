// xivModdingFramework — AlphaChannel Linux port additions
// Copyright © 2018 Rafael Gonzalez / TexTools contributors — GPL-3.0
//
// Path helpers for Windows XIVLauncher and Linux XIVLauncher.Core (xlcore).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        /// Directory that contains launcher config / dalamudConfig style files.
        /// Windows: %AppData%/XIVLauncher
        /// Linux: first existing among XLCore locations, else ~/.xlcore
        /// </summary>
        public static string GetLauncherConfigRoot()
        {
            var existing = EnumerateLauncherConfigRoots()
                .Where(Directory.Exists)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            // Prefer a root that actually has Penumbra / Dalamud config — ~/.xlcore often
            // exists as an empty stub while the real XLCore data lives under XDG.
            foreach (var candidate in existing)
            {
                if (File.Exists(Path.Combine(candidate, "pluginConfigs", "Penumbra.json")))
                    return candidate;
            }

            foreach (var candidate in existing)
            {
                if (File.Exists(Path.Combine(candidate, "dalamudConfig.json"))
                    || File.Exists(Path.Combine(candidate, "launcher.ini"))
                    || File.Exists(Path.Combine(candidate, "launcherConfigV3.json")))
                    return candidate;
            }

            if (existing.Count > 0)
                return existing[0];

            // Preferred default even if missing (doctor / first-run messaging).
            if (IsLinux || IsMacOS)
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".xlcore");
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XIVLauncher");
        }

        /// <summary>
        /// All known XL / XLCore config roots to probe (existing or not).
        /// </summary>
        public static IEnumerable<string> EnumerateLauncherConfigRoots()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var xdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (string.IsNullOrWhiteSpace(xdgData))
            {
                xdgData = Path.Combine(home, ".local", "share");
            }

            if (IsLinux)
            {
                var xlUser = Environment.GetEnvironmentVariable("XL_USER_DIR")
                             ?? Environment.GetEnvironmentVariable("XL_USERDIR")
                             ?? Environment.GetEnvironmentVariable("XL_PATH");
                if (!string.IsNullOrWhiteSpace(xlUser))
                {
                    yield return xlUser;
                }

                yield return Path.Combine(home, ".xlcore");
                yield return Path.Combine(xdgData, "xlcore");
                yield return Path.Combine(xdgData, "dev.goats.xivlauncher");
                yield return Path.Combine(home, ".var", "app", "dev.goats.xivlauncher", "data", "xlcore");
                yield return Path.Combine(home, ".var", "app", "dev.goats.xivlauncher", "data");
                yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".xlcore");
                yield break;
            }

            if (IsMacOS)
            {
                yield return Path.Combine(home, "Library", "Application Support", "XIV on Mac");
                yield return Path.Combine(home, ".xlcore");
                yield break;
            }

            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XIVLauncher");
        }

        public static string GetLauncherConfigV3Path() =>
            Path.Combine(GetLauncherConfigRoot(), "launcherConfigV3.json");

        /// <summary>XIVLauncher.Core uses launcher.ini (not launcherConfigV3.json).</summary>
        public static string GetLauncherIniPath() =>
            Path.Combine(GetLauncherConfigRoot(), "launcher.ini");

        public static string GetDalamudConfigPath() =>
            Path.Combine(GetLauncherConfigRoot(), "dalamudConfig.json");

        public static string GetPenumbraConfigPath() =>
            EnumeratePenumbraConfigPaths().FirstOrDefault(File.Exists)
            ?? Path.Combine(GetLauncherConfigRoot(), "pluginConfigs", "Penumbra.json");

        /// <summary>
        /// All candidate Penumbra.json locations across XL / XLCore roots.
        /// </summary>
        public static IEnumerable<string> EnumeratePenumbraConfigPaths()
        {
            foreach (var root in EnumerateLauncherConfigRoots())
            {
                yield return Path.Combine(root, "pluginConfigs", "Penumbra.json");
                // Some older / alternate layouts nest under dalamud/
                yield return Path.Combine(root, "dalamud", "pluginConfigs", "Penumbra.json");
            }
        }

        /// <summary>
        /// Default TexTools user data root (Saved / ModPacks / Index_Backups).
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
        /// Common FFXIV game root candidates on this machine (may or may not exist).
        /// </summary>
        public static IEnumerable<string> EnumerateGameRootCandidates()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (var root in EnumerateLauncherConfigRoots().Where(Directory.Exists))
            {
                yield return Path.Combine(root, "ffxiv");
            }

            yield return Path.Combine(home, ".xlcore", "ffxiv");
            yield return Path.Combine(home, ".steam", "steam", "steamapps", "common", "FINAL FANTASY XIV Online");
            yield return Path.Combine(home, ".local", "share", "Steam", "steamapps", "common", "FINAL FANTASY XIV Online");
            yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam", "steamapps", "common", "FINAL FANTASY XIV Online");
        }

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

        /// <summary>
        /// Resolve a bundled Resources/... file next to the entry/executing assembly
        /// (not the process CWD). Upstream often used "./Resources/..." which breaks
        /// when ConsoleTools is launched from the repo root on Linux.
        /// </summary>
        public static string ResolveBundledResource(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return relativePath;

            relativePath = relativePath
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart('.', Path.DirectorySeparatorChar);

            foreach (var root in EnumerateAssemblyBaseDirectories())
            {
                var candidate = Path.Combine(root, relativePath);
                if (File.Exists(candidate))
                    return candidate;
            }

            // Last resort: CWD-relative (legacy Windows TexTools habit).
            var cwd = Path.GetFullPath(relativePath);
            return cwd;
        }

        private static IEnumerable<string> EnumerateAssemblyBaseDirectories()
        {
            string DirOf(System.Reflection.Assembly a)
            {
                try
                {
                    var loc = a?.Location;
                    if (string.IsNullOrWhiteSpace(loc)) return null;
                    return Path.GetDirectoryName(loc);
                }
                catch
                {
                    return null;
                }
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var list = new List<string>();
            void Add(string d)
            {
                if (string.IsNullOrWhiteSpace(d)) return;
                d = Path.GetFullPath(d);
                if (seen.Add(d)) list.Add(d);
            }

            Add(DirOf(System.Reflection.Assembly.GetEntryAssembly()));
            Add(DirOf(typeof(PlatformPaths).Assembly));
            Add(AppContext.BaseDirectory);
            Add(Directory.GetCurrentDirectory());
            return list;
        }
    }
}
