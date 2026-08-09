using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Mods;
using xivModdingFramework.Textures.DataContainers;
using xivModdingFramework.Textures.FileTypes;

namespace AlphaChannel.TexTools.UI;

public enum DisplayKind
{
    Empty,
    Texture,
    Model,
    Material,
    Other,
}

public sealed class DisplayPreviewResult
{
    public required DisplayKind Kind { get; init; }
    public required string Path { get; init; }
    public string Info { get; init; } = "";
    public string Detail { get; init; } = "";
    /// <summary>Decoded image for the Display panel (WriteableBitmap or file Bitmap).</summary>
    public Bitmap? Image { get; init; }
    public byte[]? RgbaPixels { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public string Format { get; init; } = "";
}

public static class DisplayPreview
{
    public static async Task<DisplayPreviewResult> LoadAsync(string internalPath, IProgress<string>? log = null)
    {
        await GameSession.EnsureInitializedAsync(log);
        var ext = Path.GetExtension(internalPath).ToLowerInvariant();
        var tx = ModTransaction.BeginReadonlyTransaction();

        if (ext is ".tex" or ".atex")
        {
            log?.Report($"Loading texture: {internalPath}");
            var data = await tx.ReadFile(internalPath);
            var tex = XivTex.FromUncompressedTex(data);
            tex.FilePath = internalPath;
            var rgba = await tex.GetRawPixels(-1);
            var bitmap = CreateBitmap(rgba, tex.Width, tex.Height, red: true, green: true, blue: true, alpha: true);
            return new DisplayPreviewResult
            {
                Kind = DisplayKind.Texture,
                Path = internalPath,
                Width = tex.Width,
                Height = tex.Height,
                Format = tex.TextureFormat.ToString(),
                RgbaPixels = rgba,
                Image = bitmap,
                Info = $"{tex.Width}×{tex.Height} · {tex.TextureFormat} · {tex.MipMapCount} mipmaps"
                        + (tex.Layers > 1 ? $" · {tex.Layers} layers" : ""),
                Detail = internalPath,
            };
        }

        if (ext == ".mdl")
        {
            log?.Report($"Loading model: {internalPath}");
            var model = await Mdl.GetTTModel(internalPath, false, tx);
            var sb = new StringBuilder();
            sb.AppendLine($"Meshes: {model.MeshGroups.Count}");
            sb.AppendLine($"Vertices: {model.VertexCount}");
            sb.AppendLine($"Materials ({model.Materials.Count}):");
            foreach (var mat in model.Materials.Take(40))
                sb.AppendLine($"  • {mat}");
            if (model.Materials.Count > 40)
                sb.AppendLine($"  … +{model.Materials.Count - 40} more");

            return new DisplayPreviewResult
            {
                Kind = DisplayKind.Model,
                Path = internalPath,
                Info = $"Model · {model.MeshGroups.Count} meshes · {model.VertexCount} verts",
                Detail = sb.ToString().TrimEnd(),
            };
        }

        if (ext == ".mtrl")
        {
            log?.Report($"Loading material: {internalPath}");
            var mtrl = await Mtrl.GetXivMtrl(internalPath, false, tx);
            var sb = new StringBuilder();
            sb.AppendLine($"Shader: {mtrl.ShaderPack}");
            sb.AppendLine($"Textures ({mtrl.Textures?.Count ?? 0}):");
            if (mtrl.Textures != null)
            {
                foreach (var t in mtrl.Textures)
                {
                    var p = string.IsNullOrWhiteSpace(t.Dx11Path) ? t.TexturePath : t.Dx11Path;
                    sb.AppendLine($"  • {p}");
                }
            }
            if (mtrl.ColorSetData != null && mtrl.ColorSetData.Count > 0)
                sb.AppendLine($"Colorset: {mtrl.ColorSetData.Count} half-floats");

            return new DisplayPreviewResult
            {
                Kind = DisplayKind.Material,
                Path = internalPath,
                Info = $"Material · {mtrl.Textures?.Count ?? 0} textures · shader {mtrl.ShaderPack}",
                Detail = sb.ToString().TrimEnd(),
            };
        }

        return new DisplayPreviewResult
        {
            Kind = DisplayKind.Other,
            Path = internalPath,
            Info = $"File type {ext} — preview not available (use Extract).",
            Detail = internalPath,
        };
    }

    public static WriteableBitmap CreateBitmap(
        byte[] rgba,
        int width,
        int height,
        bool red,
        bool green,
        bool blue,
        bool alpha)
    {
        var pixels = (byte[])rgba.Clone();
        for (var i = 0; i < pixels.Length; i += 4)
        {
            if (!red) pixels[i] = 0;
            if (!green) pixels[i + 1] = 0;
            if (!blue) pixels[i + 2] = 0;
            if (!alpha) pixels[i + 3] = 255;
        }

        // RGBA → BGRA + optional premultiply (classic TexTools display path)
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var r = pixels[i];
            var g = pixels[i + 1];
            var b = pixels[i + 2];
            var a = pixels[i + 3];
            if (alpha)
            {
                r = (byte)(r * a / 256);
                g = (byte)(g * a / 256);
                b = (byte)(b * a / 256);
            }
            pixels[i] = b;
            pixels[i + 1] = g;
            pixels[i + 2] = r;
            pixels[i + 3] = a;
        }

        var bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            alpha ? AlphaFormat.Premul : AlphaFormat.Unpremul);

        using (var fb = bitmap.Lock())
        {
            var stride = width * 4;
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, fb.Address, Math.Min(pixels.Length, stride * height));
        }

        return bitmap;
    }

    public static string? PreferDisplayFile(System.Collections.Generic.IReadOnlyList<string> files)
    {
        static int Rank(string f)
        {
            var e = Path.GetExtension(f).ToLowerInvariant();
            return e switch
            {
                ".mdl" => 0,
                ".tex" or ".atex" => 1,
                ".mtrl" => 2,
                _ => 9,
            };
        }

        return files.OrderBy(Rank).ThenBy(f => f, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
    }

    /// <summary>
    /// Preview an external file dropped from the desktop (tex/dds/png/…).
    /// </summary>
    public static async Task<DisplayPreviewResult> LoadExternalAsync(string externalPath, IProgress<string>? log = null)
    {
        if (!File.Exists(externalPath))
            throw new FileNotFoundException("File not found.", externalPath);

        var ext = Path.GetExtension(externalPath).ToLowerInvariant();
        log?.Report($"Loading external: {externalPath}");

        if (ext is ".tex" or ".atex")
        {
            var data = await File.ReadAllBytesAsync(externalPath);
            var tex = XivTex.FromUncompressedTex(data);
            var rgba = await tex.GetRawPixels(-1);
            return new DisplayPreviewResult
            {
                Kind = DisplayKind.Texture,
                Path = externalPath,
                Width = tex.Width,
                Height = tex.Height,
                Format = tex.TextureFormat.ToString(),
                RgbaPixels = rgba,
                Image = CreateBitmap(rgba, tex.Width, tex.Height, true, true, true, true),
                Info = $"{tex.Width}×{tex.Height} · {tex.TextureFormat} · external",
                Detail = externalPath,
            };
        }

        if (ext == ".dds")
        {
            var tex = XivTex.FromUncompressedTex(Tex.DDSToUncompressedTex(externalPath));
            var rgba = await tex.GetRawPixels(-1);
            return new DisplayPreviewResult
            {
                Kind = DisplayKind.Texture,
                Path = externalPath,
                Width = tex.Width,
                Height = tex.Height,
                Format = tex.TextureFormat.ToString(),
                RgbaPixels = rgba,
                Image = CreateBitmap(rgba, tex.Width, tex.Height, true, true, true, true),
                Info = $"{tex.Width}×{tex.Height} · {tex.TextureFormat} · DDS",
                Detail = externalPath,
            };
        }

        if (ext is ".png" or ".bmp" or ".tga" or ".jpg" or ".jpeg")
        {
            await using var fs = File.OpenRead(externalPath);
            var bitmap = new Bitmap(fs);
            return new DisplayPreviewResult
            {
                Kind = DisplayKind.Texture,
                Path = externalPath,
                Width = bitmap.PixelSize.Width,
                Height = bitmap.PixelSize.Height,
                Format = ext.TrimStart('.').ToUpperInvariant(),
                Image = bitmap,
                Info = $"{bitmap.PixelSize.Width}×{bitmap.PixelSize.Height} · {ext.TrimStart('.').ToUpperInvariant()} · external",
                Detail = externalPath,
            };
        }

        throw new NotSupportedException($"Cannot preview external file type: {ext}");
    }
}
