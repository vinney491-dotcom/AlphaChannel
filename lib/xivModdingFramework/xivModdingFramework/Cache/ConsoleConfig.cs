using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;

namespace xivModdingFramework.Cache
{
    /// <summary>
    /// Simple class for storing basic configuration information in the working directory.
    /// This is a basic way for framework applications to hook the Framework without having to manually
    /// supply things like game path which may already be configured by TexTools/etc.
    /// </summary>
    public class ConsoleConfig
    {
        public const string ConfigPath = "console_config.json";

        public string XivPath { get; set; } = "";

        public string Language { get; set; } = "en";

        [JsonIgnore]
        public XivLanguage XivLanguage
        {
            get
            {
                try
                {
                    return XivLanguages.GetXivLanguage(Language);
                }
                catch
                {
                    return XivLanguage.English;
                }
            }
        }


        public static void Update(Action<ConsoleConfig> action)
        {
            var c = Get();
            action(c);
            c.Save();
        }

        public static ConsoleConfig Get()
        {
            var cwd = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            var path = Path.Combine(cwd, ConfigPath);

            if (!File.Exists(path))
            {
                return new ConsoleConfig();
            }

            try
            {
                return JsonConvert.DeserializeObject<ConsoleConfig>(File.ReadAllText(path));
            }
            catch
            {
                return new ConsoleConfig();
            }
        }

        public void Save()
        {
            var cwd = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            var path = Path.Combine(cwd, ConsoleConfig.ConfigPath);
            var text = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(path, text);
        }


        public static async Task InitCacheFromConfig(bool runWorker = false)
        {
            var c = Get();
            if (string.IsNullOrWhiteSpace(c.XivPath))
            {
                c.XivPath = ResolveDefaultXivPath() ?? "";
            }
            if (string.IsNullOrWhiteSpace(c.XivPath))
            {
                throw new ArgumentException(
                    "No FFXIV path configured. Set XivPath in console_config.json, " +
                    "set the XIV_PATH / FFXIV_PATH environment variable, or install XIVLauncher.Core (~/.xlcore).");
            }
            await XivCache.SetGameInfo(new DirectoryInfo(c.XivPath), c.XivLanguage, runWorker);

            // Set a unique temp path.
            var tempDir = IOUtil.GetUniqueSubfolder(Path.GetTempPath(), "xivct");
            XivCache.FrameworkSettings.TempDirectory = tempDir;
        }

        /// <summary>
        /// Resolve sqpack/ffxiv directory from env or launcher config (Linux-friendly).
        /// </summary>
        public static string ResolveDefaultXivPath()
        {
            foreach (var key in new[] { "XIV_PATH", "FFXIV_PATH", "FFXIV_GAME_PATH" })
            {
                var env = Environment.GetEnvironmentVariable(key);
                if (!string.IsNullOrWhiteSpace(env))
                {
                    var normalized = NormalizeToSqPackFfxiv(env);
                    if (normalized != null) return normalized;
                }
            }

            var launcherGame = PenumbraAPI.GetQuickLauncherGameDirectory();
            if (!string.IsNullOrWhiteSpace(launcherGame))
            {
                var normalized = NormalizeToSqPackFfxiv(launcherGame);
                if (normalized != null) return normalized;
            }

            // Common layouts when launcher config is missing / incomplete.
            foreach (var root in PlatformPaths.EnumerateGameRootCandidates())
            {
                var normalized = NormalizeToSqPackFfxiv(root);
                if (normalized != null) return normalized;
            }

            return null;
        }

        private static string NormalizeToSqPackFfxiv(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            path = Path.GetFullPath(path.Trim().Trim('"'));

            if (Directory.Exists(path) &&
                (path.EndsWith($"{Path.DirectorySeparatorChar}sqpack{Path.DirectorySeparatorChar}ffxiv", StringComparison.OrdinalIgnoreCase)
                 || path.EndsWith("/sqpack/ffxiv", StringComparison.OrdinalIgnoreCase)
                 || path.EndsWith("\\sqpack\\ffxiv", StringComparison.OrdinalIgnoreCase)))
            {
                return path;
            }

            var sq = Path.Combine(path, "game", "sqpack", "ffxiv");
            if (Directory.Exists(sq)) return sq;

            sq = Path.Combine(path, "sqpack", "ffxiv");
            if (Directory.Exists(sq)) return sq;

            if (Directory.Exists(path)) return path;
            return null;
        }
    }
}
