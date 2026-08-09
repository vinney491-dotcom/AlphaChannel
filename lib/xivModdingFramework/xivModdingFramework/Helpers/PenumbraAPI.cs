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

        public static string GetPenumbraDirectory()
        {
            var path = PlatformPaths.GetPenumbraConfigPath();
            if (!File.Exists(path))
            {
                return "";
            }

            try
            {
                var obj = JObject.Parse(File.ReadAllText(path));
                var st = (string)obj["ModDirectory"];
                return st ?? "";
            }
            catch (Exception ex)
            {
                return "";
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
