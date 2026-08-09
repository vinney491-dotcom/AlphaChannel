using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using xivModdingFramework.Helpers;

namespace AlphaChannel.TexTools.UI;

public sealed class PenumbraModEntry
{
    public PenumbraModEntry(string folderPath, string name, string? author, string? version)
    {
        FolderPath = folderPath;
        Name = name;
        Author = author ?? "";
        Version = version ?? "";
    }

    public string FolderPath { get; }
    public string Name { get; }
    public string Author { get; }
    public string Version { get; }
    public string Display => string.IsNullOrWhiteSpace(Author) ? Name : $"{Name} — {Author}";

    public override string ToString() => Display;
}

/// <summary>
/// Browse / search the Penumbra mods root without changing Penumbra's on-disk format.
/// </summary>
public static class PenumbraLibrary
{
    public static async Task<IReadOnlyList<PenumbraModEntry>> ListModsAsync(string? root = null)
    {
        root ??= PenumbraAPI.GetPenumbraDirectory();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return Array.Empty<PenumbraModEntry>();

        return await Task.Run(() =>
        {
            var list = new List<PenumbraModEntry>();
            foreach (var dir in Directory.EnumerateDirectories(root).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var metaPath = Path.Combine(dir, "meta.json");
                string name = Path.GetFileName(dir);
                string? author = null;
                string? version = null;
                if (File.Exists(metaPath))
                {
                    try
                    {
                        var obj = JObject.Parse(File.ReadAllText(metaPath));
                        name = (string?)obj["Name"] ?? name;
                        author = (string?)obj["Author"];
                        version = (string?)obj["Version"];
                    }
                    catch
                    {
                        // use folder name
                    }
                }
                else if (!File.Exists(Path.Combine(dir, "default_mod.json"))
                         && !Directory.EnumerateFiles(dir, "*.json").Any())
                {
                    // Skip empty/non-mod clutter unless it looks like a TT import folder with files.
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        continue;
                }

                list.Add(new PenumbraModEntry(dir, name, author, version));
            }

            return (IReadOnlyList<PenumbraModEntry>)list;
        });
    }

    public static async Task<bool> ReloadAsync(PenumbraModEntry entry)
    {
        var folderName = Path.GetFileName(entry.FolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return await PenumbraAPI.ReloadMod(folderName);
    }

    public static async Task<string> WriteSimplePenumbraModAsync(
        string modRoot,
        string modFolderName,
        string displayName,
        string author,
        IReadOnlyDictionary<string, string> gamePathToSourceFile,
        IProgress<string>? log = null)
    {
        var dest = Path.Combine(modRoot, IOUtil.MakePathSafe(modFolderName));
        dest = IOUtil.GetUniqueSubfolder(modRoot, Path.GetFileName(dest), omitZero: true);
        Directory.CreateDirectory(dest);
        log?.Report($"Writing Penumbra mod → {dest}");

        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (gamePath, src) in gamePathToSourceFile)
        {
            var relative = gamePath.Replace('\\', '/').TrimStart('/');
            var outPath = Path.Combine(dest, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            File.Copy(src, outPath, overwrite: true);
            files[gamePath.Replace('\\', '/')] = relative;
            log?.Report($"  + {gamePath}");
        }

        var meta = new
        {
            FileVersion = 3,
            Name = displayName,
            Author = author,
            Description = "Created by AlphaChannel TexTools",
            Version = "1.0.0",
            Website = "",
            ModTags = Array.Empty<string>(),
        };
        await File.WriteAllTextAsync(Path.Combine(dest, "meta.json"), JsonConvert.SerializeObject(meta, Formatting.Indented));

        var defaultMod = new
        {
            Name = "",
            Priority = 0,
            Files = files,
            FileSwaps = new Dictionary<string, string>(),
            Manipulations = Array.Empty<object>(),
        };
        await File.WriteAllTextAsync(Path.Combine(dest, "default_mod.json"), JsonConvert.SerializeObject(defaultMod, Formatting.Indented));

        var folderName = Path.GetFileName(dest);
        var reloaded = await PenumbraAPI.ReloadMod(folderName);
        log?.Report(reloaded
            ? $"Penumbra reload OK for '{folderName}'."
            : $"Wrote '{folderName}'. Enable/reload in Penumbra if the game is running.");
        return dest;
    }
}
