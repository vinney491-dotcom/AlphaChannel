using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using xivModdingFramework.Cache;
using xivModdingFramework.Helpers;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.Textures.DataContainers;

namespace AlphaChannel.TexTools.UI;

/// <summary>
/// High-level native actions mirroring classic TexTools menus (Penumbra-first).
/// </summary>
public static class TexToolsActions
{
    public static readonly string[] ModpackPatterns =
        ["*.ttmp2", "*.ttmp", "*.pmp", "*.zip"];

    public static async Task<string> UpgradeModpackAsync(string src, string dest, IProgress<string>? log = null)
    {
        await GameSession.EnsureInitializedAsync(log);
        log?.Report($"Upgrading: {src}");
        var any = await ModpackUpgrader.UpgradeModpack(src, dest, includePartials: true, rewriteOnNoChanges: true);
        if (!File.Exists(dest) && !Directory.Exists(dest))
            File.Copy(src, dest, overwrite: true);
        log?.Report(any ? $"Saved upgraded modpack → {dest}" : $"No DT changes needed → {dest}");
        return dest;
    }

    /// <summary>
    /// Classic “Import Modpack to Penumbra”: upgrade into a unique folder under the Penumbra library.
    /// </summary>
    public static async Task<string> ImportToPenumbraAsync(string modpackPath, IProgress<string>? log = null)
    {
        await GameSession.EnsureInitializedAsync(log);

        var penumbraDir = PenumbraAPI.GetPenumbraDirectory();
        if (string.IsNullOrWhiteSpace(penumbraDir) || !Directory.Exists(penumbraDir))
        {
            throw new InvalidOperationException(
                "Penumbra mod directory not found. Install Penumbra in XIVLauncher.Core / Dalamud and set its mod folder.");
        }

        log?.Report($"Penumbra library: {penumbraDir}");
        var info = await TTMP.GetModpackInfo(modpackPath);
        var fname = IOUtil.MakePathSafe(
            string.IsNullOrWhiteSpace(info.ModPack.Name)
                ? Path.GetFileNameWithoutExtension(modpackPath)
                : info.ModPack.Name);
        if (string.IsNullOrWhiteSpace(fname))
            fname = "Modpack";

        var newPath = IOUtil.GetUniqueSubfolder(penumbraDir, fname, omitZero: true);
        var newName = Path.GetFileName(newPath);
        log?.Report($"Importing into: {newPath}");

        await ModpackUpgrader.UpgradeModpack(modpackPath, newPath, includePartials: true, rewriteOnNoChanges: true);

        var reloaded = await PenumbraAPI.ReloadMod(newName);
        log?.Report(reloaded
            ? $"Penumbra reload OK for '{newName}'."
            : $"Imported to '{newName}'. Penumbra API reload failed (is the game + Penumbra running on :42069?). Enable the mod manually.");

        return newPath;
    }

    public static async Task ResaveModpackAsync(string src, string dest, IProgress<string>? log = null)
    {
        await GameSession.EnsureInitializedAsync(log);
        log?.Report($"Resaving: {src} → {dest}");
        var data = await WizardData.FromModpack(src, enforceCompatibility: true)
                   ?? throw new InvalidOperationException("Could not load modpack.");
        await data.WriteModpack(dest, saveExtraFiles: true);
        log?.Report($"Resaved → {dest}");
    }

    public static async Task<IReadOnlyList<string>> ListRootFilesAsync(string rootId, IProgress<string>? log = null)
    {
        await GameSession.EnsureInitializedAsync(log);
        var rootInfo = XivCache.GetFileNameRootInfo(rootId, true);
        var root = new XivDependencyRoot(rootInfo);
        var files = await root.GetAllFiles();
        if (files == null || files.Count == 0)
            throw new InvalidOperationException($"No files found for root id: {rootId}");
        log?.Report($"Root {rootId}: {files.Count} files");
        return files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static async Task ExtractFileAsync(string internalPath, string dest, bool sqpack, IProgress<string>? log = null)
    {
        await GameSession.EnsureInitializedAsync(log);
        log?.Report($"Extracting: {internalPath}");

        var destDir = Path.GetDirectoryName(Path.GetFullPath(dest));
        if (!string.IsNullOrEmpty(destDir))
            Directory.CreateDirectory(destDir);

        var rtx = ModTransaction.BeginReadonlyTransaction();
        if (string.Equals(Path.GetExtension(internalPath), Path.GetExtension(dest), StringComparison.OrdinalIgnoreCase))
        {
            var data = await rtx.ReadFile(internalPath, false, sqpack);
            await File.WriteAllBytesAsync(dest, data);
        }
        else if (internalPath.EndsWith(".tex", StringComparison.OrdinalIgnoreCase)
                 || internalPath.EndsWith(".atex", StringComparison.OrdinalIgnoreCase))
        {
            var data = await rtx.ReadFile(internalPath);
            var tex = XivTex.FromUncompressedTex(data);
            await tex.SaveAs(dest);
        }
        else if (internalPath.EndsWith(".mdl", StringComparison.OrdinalIgnoreCase))
        {
            await Mdl.ExportMdlToFile(internalPath, dest, 1, null, false, rtx);
        }
        else
        {
            var data = await rtx.ReadFile(internalPath);
            await File.WriteAllBytesAsync(dest, data);
        }

        log?.Report($"Extracted → {dest}");
    }

    public static async Task BatchUpgradeFolderAsync(string folder, string destFolder, IProgress<string>? log = null)
    {
        await GameSession.EnsureInitializedAsync(log);
        Directory.CreateDirectory(destFolder);
        var files = Directory.EnumerateFiles(folder)
            .Where(f => ModpackPatterns.Any(p =>
                f.EndsWith(p.TrimStart('*'), StringComparison.OrdinalIgnoreCase)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
            throw new InvalidOperationException("No modpack files found in folder.");

        var i = 0;
        foreach (var src in files)
        {
            i++;
            var name = Path.GetFileNameWithoutExtension(src) + "-upgraded" + Path.GetExtension(src);
            var dest = Path.Combine(destFolder, name);
            log?.Report($"[{i}/{files.Count}] {Path.GetFileName(src)}");
            try
            {
                await ModpackUpgrader.UpgradeModpack(src, dest, true, true);
            }
            catch (Exception ex)
            {
                log?.Report($"  FAILED: {ex.Message}");
            }
        }
        log?.Report($"Batch complete → {destFolder}");
    }
}
