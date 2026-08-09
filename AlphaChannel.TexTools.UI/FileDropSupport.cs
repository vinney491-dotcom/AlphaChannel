using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace AlphaChannel.TexTools.UI;

internal enum DropKind
{
    None,
    Modpack,
    Texture,
    GameFolder,
    Mixed,
}

internal static class FileDropSupport
{
    public static readonly HashSet<string> ModpackExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ttmp2", ".ttmp", ".pmp", ".zip",
    };

    public static readonly HashSet<string> TextureExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tex", ".atex", ".dds", ".png", ".bmp", ".tga", ".jpg", ".jpeg",
    };

    public static IReadOnlyList<string> GetLocalPaths(IDataObject data)
    {
        var list = new List<string>();
        try
        {
            var names = data.GetFileNames();
            if (names != null)
            {
                foreach (var n in names)
                {
                    if (!string.IsNullOrWhiteSpace(n) && (File.Exists(n) || Directory.Exists(n)))
                        list.Add(n);
                }
            }
        }
        catch
        {
            // ignore
        }

        if (list.Count == 0)
        {
            try
            {
                var files = data.GetFiles();
                if (files != null)
                {
                    foreach (var f in files)
                    {
                        var path = f is IStorageItem item
                            ? (item.TryGetLocalPath() ?? item.Path.LocalPath)
                            : null;
                        if (!string.IsNullOrWhiteSpace(path) && (File.Exists(path) || Directory.Exists(path)))
                            list.Add(path);
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        return list;
    }

    public static DropKind Classify(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return DropKind.None;

        var hasMod = false;
        var hasTex = false;
        var hasGame = false;
        var other = false;

        foreach (var p in paths)
        {
            if (Directory.Exists(p))
            {
                if (LooksLikeSqPack(p))
                    hasGame = true;
                else if (Directory.EnumerateFiles(p).Any(f => ModpackExts.Contains(Path.GetExtension(f))))
                    hasMod = true;
                else
                    other = true;
                continue;
            }

            var ext = Path.GetExtension(p);
            if (ModpackExts.Contains(ext))
                hasMod = true;
            else if (TextureExts.Contains(ext))
                hasTex = true;
            else
                other = true;
        }

        var kinds = (hasMod ? 1 : 0) + (hasTex ? 1 : 0) + (hasGame ? 1 : 0);
        if (kinds == 0) return DropKind.None;
        if (kinds > 1 || (other && kinds >= 1 && paths.Count > 1)) return DropKind.Mixed;
        if (hasGame) return DropKind.GameFolder;
        if (hasMod) return DropKind.Modpack;
        if (hasTex) return DropKind.Texture;
        return DropKind.None;
    }

    public static bool LooksLikeSqPack(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return false;
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (full.EndsWith($"{Path.DirectorySeparatorChar}sqpack{Path.DirectorySeparatorChar}ffxiv", StringComparison.OrdinalIgnoreCase)
            || full.EndsWith("/sqpack/ffxiv", StringComparison.OrdinalIgnoreCase)
            || full.EndsWith("\\sqpack\\ffxiv", StringComparison.OrdinalIgnoreCase))
            return Directory.EnumerateFiles(full, "*.index*").Any();

        var nested = Path.Combine(full, "game", "sqpack", "ffxiv");
        if (Directory.Exists(nested) && Directory.EnumerateFiles(nested, "*.index*").Any())
            return true;

        nested = Path.Combine(full, "sqpack", "ffxiv");
        return Directory.Exists(nested) && Directory.EnumerateFiles(nested, "*.index*").Any();
    }

    public static string? NormalizeSqPack(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return null;
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (full.EndsWith("sqpack/ffxiv", StringComparison.OrdinalIgnoreCase)
            || full.EndsWith($"sqpack{Path.DirectorySeparatorChar}ffxiv", StringComparison.OrdinalIgnoreCase))
            return full;

        var nested = Path.Combine(full, "game", "sqpack", "ffxiv");
        if (Directory.Exists(nested)) return nested;
        nested = Path.Combine(full, "sqpack", "ffxiv");
        if (Directory.Exists(nested)) return nested;
        return null;
    }

    public static IEnumerable<string> ExpandModpacks(IEnumerable<string> paths)
    {
        foreach (var p in paths)
        {
            if (Directory.Exists(p))
            {
                foreach (var f in Directory.EnumerateFiles(p)
                             .Where(f => ModpackExts.Contains(Path.GetExtension(f)))
                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                    yield return f;
            }
            else if (ModpackExts.Contains(Path.GetExtension(p)))
            {
                yield return p;
            }
        }
    }

    public static string OverlayHint(DropKind kind, int count) => kind switch
    {
        DropKind.Modpack => count == 1
            ? "Drop to import/upgrade modpack"
            : $"Drop to handle {count} modpacks",
        DropKind.Texture => count == 1
            ? "Drop to preview texture/image"
            : $"Drop to preview first of {count} images",
        DropKind.GameFolder => "Drop to set FFXIV game path",
        DropKind.Mixed => "Drop mixed files (modpacks preferred)",
        _ => "Drop modpacks, textures, or game folder",
    };
}
