using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items.Categories;
using xivModdingFramework.Mods;
using xivModdingFramework.Textures.FileTypes;

namespace AlphaChannel.TexTools.UI;

/// <summary>
/// Image → face-paint / equipment-decal .tex → Penumbra mod folder.
/// </summary>
public static class TattooConverter
{
    public static async Task<IReadOnlyList<string>> ListFacePaintPathsAsync(IProgress<string>? log = null)
    {
        await GameSession.EnsureInitializedAsync(log);
        var tx = ModTransaction.BeginReadonlyTransaction();
        return await Character.GetDecalPaths(Character.XivDecalType.FacePaint, tx);
    }

    public static async Task<IReadOnlyList<string>> ListEquipDecalPathsAsync(IProgress<string>? log = null)
    {
        await GameSession.EnsureInitializedAsync(log);
        var tx = ModTransaction.BeginReadonlyTransaction();
        return await Character.GetDecalPaths(Character.XivDecalType.Equipment, tx);
    }

    /// <summary>
    /// Convert an external image into an uncompressed .tex for the given game path, then wrap as a Penumbra mod.
    /// </summary>
    public static async Task<string> ConvertToPenumbraAsync(
        string imagePath,
        string gameTexturePath,
        string? penumbraRoot = null,
        string? modName = null,
        IProgress<string>? log = null)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Image not found.", imagePath);

        await GameSession.EnsureInitializedAsync(log);
        penumbraRoot ??= PenumbraAPI.GetPenumbraDirectory();
        if (string.IsNullOrWhiteSpace(penumbraRoot) || !Directory.Exists(penumbraRoot))
            throw new InvalidOperationException("Penumbra mod directory not set. Use Options → Set Penumbra Mods Folder…");

        gameTexturePath = gameTexturePath.Replace('\\', '/');
        modName ??= Path.GetFileNameWithoutExtension(imagePath) + " Decal";

        log?.Report($"Converting {Path.GetFileName(imagePath)} → {gameTexturePath}");
        var prepared = await PrepareImageForTexAsync(imagePath, gameTexturePath, log);
        try
        {
            var texBytes = await SmartImport.CreateUncompressedFile(prepared, gameTexturePath);
            var tempTex = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".tex");
            await File.WriteAllBytesAsync(tempTex, texBytes);
            try
            {
                return await PenumbraLibrary.WriteSimplePenumbraModAsync(
                    penumbraRoot,
                    modName,
                    modName,
                    Environment.UserName ?? "TexTools",
                    new Dictionary<string, string> { [gameTexturePath] = tempTex },
                    log);
            }
            finally
            {
                try { File.Delete(tempTex); } catch { /* ignore */ }
            }
        }
        finally
        {
            if (!string.Equals(prepared, imagePath, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(prepared); } catch { /* ignore */ }
            }
        }
    }

    private static async Task<string> PrepareImageForTexAsync(string imagePath, string gamePath, IProgress<string>? log)
    {
        var ext = Path.GetExtension(imagePath).ToLowerInvariant();
        if (ext is ".dds")
            return imagePath;

        int targetW = 512, targetH = 512;
        try
        {
            var tx = ModTransaction.BeginReadonlyTransaction();
            if (await tx.FileExists(gamePath))
            {
                var existing = await Tex.GetXivTex(gamePath, false, tx);
                targetW = existing.Width;
                targetH = existing.Height;
                log?.Report($"Matching existing size {targetW}×{targetH}");
            }
        }
        catch
        {
            // keep defaults
        }

        using var img = await Image.LoadAsync<Rgba32>(imagePath);
        if (img.Width != targetW || img.Height != targetH)
        {
            img.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(targetW, targetH),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Lanczos3,
                PremultiplyAlpha = false,
            }));
        }

        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".tga");
        await img.SaveAsync(temp, new TgaEncoder
        {
            BitsPerPixel = TgaBitsPerPixel.Pixel32,
            Compression = TgaCompression.None,
        });
        return temp;
    }
}
