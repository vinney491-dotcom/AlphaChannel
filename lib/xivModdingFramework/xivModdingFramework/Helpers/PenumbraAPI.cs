using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using xivModdingFramework.Cache;

namespace xivModdingFramework.Helpers
{
    /// <summary>
    /// Simple thin static class that handles poking the Penumbra HTTP API
    /// Most functions just return a boolean for success status.
    /// </summary>
    public static class PenumbraAPI
    {
        /// <summary>
        /// Calls /redraw on the Penumbra API.
        /// </summary>
        /// <returns></returns>
        public static async Task<bool> Redraw()
        {
            return await Request("/redraw");

        }

        /// <summary>
        /// Calls /redraw on the Penumbra API to redraw only the local player.
        /// </summary>
        /// <returns></returns>
        public static async Task<bool> RedrawSelf()
        {
            Dictionary<string, string> args = new()
            {
                { "ObjectTableIndex", "0" }
            };
            return await Request("/redraw", args);
        }

        /// <summary>
        /// Calls /reloadmod on the Penumbra API.
        /// </summary>
        /// <returns></returns>
        public static async Task<bool> ReloadMod(string path, string name = null)
        {
            Dictionary<string, string> args = new Dictionary<string, string>();

            if (name != null)
            {
                args.Add("Name", name);
            }
            if (path != null)
            {
                args.Add("Path", path);
            }

            return await Request("/reloadmod", args);
        }

        private static HttpClient _Client = new HttpClient() { BaseAddress = new System.Uri("http://localhost:42069") };

        private static async Task<bool> Request(string urlPath, object data = null)
        {
            data = data == null ? new object() : data;
            return await Task.Run(async () => {
                try
                {
                    using StringContent jsonContent = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
                    using HttpResponseMessage response = await _Client.PostAsync("api/" + urlPath, jsonContent);

                    response.EnsureSuccessStatusCode();

                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                    //throw;
                }
            });
        }


        public static string GetQuickLauncherGameDirectory()
        {
            // Windows XIVLauncher JSON
            foreach (var root in PlatformPaths.EnumerateLauncherConfigRoots())
            {
                var jsonPath = Path.Combine(root, "launcherConfigV3.json");
                if (!File.Exists(jsonPath)) continue;
                try
                {
                    var obj = JObject.Parse(File.ReadAllText(jsonPath));
                    var st = (string)obj["GamePath"];
                    if (!string.IsNullOrWhiteSpace(st)) return st;
                }
                catch
                {
                    // try next
                }
            }

            // XIVLauncher.Core (Linux) uses launcher.ini
            foreach (var root in PlatformPaths.EnumerateLauncherConfigRoots())
            {
                var iniPath = Path.Combine(root, "launcher.ini");
                if (!File.Exists(iniPath)) continue;
                try
                {
                    foreach (var line in File.ReadLines(iniPath))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("GamePath=", StringComparison.OrdinalIgnoreCase))
                        {
                            var value = trimmed.Substring("GamePath=".Length).Trim().Trim('"');
                            if (!string.IsNullOrWhiteSpace(value)) return value;
                        }
                    }
                }
                catch
                {
                    // try next
                }
            }

            return "";
        }

        /// <summary>
        /// Resolve the Penumbra mod library directory.
        /// Order: env → console_config override → Penumbra.json (all XLCore roots) → common folders.
        /// Wine-style paths (Z:\home\...) are converted when possible.
        /// </summary>
        public static string GetPenumbraDirectory()
        {
            foreach (var key in new[] { "PENUMBRA_MOD_DIR", "PENUMBRA_PATH", "PENUMBRA_MODS" })
            {
                var env = Environment.GetEnvironmentVariable(key);
                var native = NormalizeExistingDirectory(env);
                if (native != null) return native;
            }

            try
            {
                var cfg = ConsoleConfig.Get();
                var overrideDir = NormalizeExistingDirectory(cfg?.PenumbraModDirectory);
                if (overrideDir != null) return overrideDir;
            }
            catch
            {
                // ConsoleConfig may be unavailable in some hosts.
            }

            foreach (var configPath in PlatformPaths.EnumeratePenumbraConfigPaths())
            {
                if (!File.Exists(configPath)) continue;
                try
                {
                    var obj = JObject.Parse(File.ReadAllText(configPath));
                    var st = (string)obj["ModDirectory"];
                    var native = NormalizeExistingDirectory(st);
                    if (native != null) return native;
                }
                catch
                {
                    // try next
                }
            }

            // Last-resort guesses when Penumbra.json is missing or ModDirectory is unset.
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (var guess in new[]
                     {
                         Path.Combine(home, "ff14-mods"),
                         Path.Combine(home, "FFXIV Mods"),
                         Path.Combine(home, "Documents", "Penumbra"),
                         Path.Combine(home, ".xlcore", "penumbra"),
                         Path.Combine(PlatformPaths.GetLauncherConfigRoot(), "penumbra"),
                     })
            {
                var native = NormalizeExistingDirectory(guess);
                if (native != null) return native;
            }

            return "";
        }

        /// <summary>
        /// Human-readable diagnostics for why Penumbra discovery failed / succeeded.
        /// </summary>
        public static string DescribePenumbraDiscovery()
        {
            var sb = new System.Text.StringBuilder();
            var resolved = GetPenumbraDirectory();
            sb.AppendLine(string.IsNullOrWhiteSpace(resolved)
                ? "Resolved Penumbra mod directory: (none)"
                : "Resolved Penumbra mod directory: " + resolved);

            try
            {
                var ov = ConsoleConfig.Get()?.PenumbraModDirectory;
                sb.AppendLine("console_config PenumbraModDirectory: " +
                              (string.IsNullOrWhiteSpace(ov) ? "(unset)" : ov));
            }
            catch
            {
                sb.AppendLine("console_config PenumbraModDirectory: (unavailable)");
            }

            sb.AppendLine("Penumbra.json candidates:");
            foreach (var configPath in PlatformPaths.EnumeratePenumbraConfigPaths())
            {
                if (!File.Exists(configPath))
                {
                    sb.AppendLine("  missing  " + configPath);
                    continue;
                }

                try
                {
                    var obj = JObject.Parse(File.ReadAllText(configPath));
                    var st = (string)obj["ModDirectory"] ?? "";
                    var native = NormalizeExistingDirectory(st);
                    sb.AppendLine("  found    " + configPath);
                    sb.AppendLine("           ModDirectory raw: " + (string.IsNullOrWhiteSpace(st) ? "(empty)" : st));
                    sb.AppendLine("           native: " + (native ?? "(does not exist)"));
                }
                catch (Exception ex)
                {
                    sb.AppendLine("  error    " + configPath + " — " + ex.Message);
                }
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Expand ~/…, convert Wine Z:\… paths, and return a real existing directory — or null.
        /// </summary>
        public static string NormalizeExistingDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            path = path.Trim().Trim('"');

            foreach (var candidate in ExpandPathCandidates(path))
            {
                try
                {
                    if (Directory.Exists(candidate))
                        return Path.GetFullPath(candidate);
                }
                catch
                {
                    // try next
                }
            }

            return null;
        }

        private static IEnumerable<string> ExpandPathCandidates(string path)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (path.StartsWith("~/") || path.StartsWith("~\\"))
                path = Path.Combine(home, path.Substring(2));
            else if (path == "~")
                path = home;

            yield return path;

            // Wine/Proton: Z:\home\user\... → /home/user/...
            if (path.Length >= 3
                && char.IsLetter(path[0])
                && path[1] == ':'
                && (path[2] == '\\' || path[2] == '/'))
            {
                var drive = char.ToUpperInvariant(path[0]);
                var rest = path.Substring(3).Replace('\\', '/').TrimStart('/');
                if (drive == 'Z')
                {
                    yield return "/" + rest;
                    if (!string.IsNullOrEmpty(home))
                        yield return Path.Combine(home, rest); // rare mis-map
                }
            }

            // UNC-ish Wine paths sometimes show up as \\?\Z:\...
            if (path.StartsWith(@"\\?\Z:\", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("//?/Z:/", StringComparison.OrdinalIgnoreCase))
            {
                var rest = path.Substring(7).Replace('\\', '/').TrimStart('/');
                yield return "/" + rest;
            }
        }

        public static bool IsPenumbraInstalled()
        {
            var path = PlatformPaths.GetDalamudConfigPath();
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                var obj = JObject.Parse(File.ReadAllText(path));
                var profile = (JObject)obj["DefaultProfile"];
                var plugins = (JObject)profile["Plugins"];
                var pValues = (JArray)plugins["$values"];

                var penumbra = pValues.FirstOrDefault(x => (string)x["InternalName"] == "Penumbra");

                if(penumbra != null)
                {
                    // Normally we'd stop here.  But while Penumbra is in testing we need to check that part.
                    var optIns = (JObject)obj["PluginTestingOptIns"];
                    var oValues = (JArray)optIns["$values"];
                    var pTesting = oValues.FirstOrDefault(x => (string)x["InternalName"] == "Penumbra");
                    if ((string)pTesting["Branch"] == "testing-live")
                    {
                        return true;
                    }

                    return false;
                } else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
