using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvColor = Avalonia.Media.Color;
using SdxHalf = SharpDX.Half;
using xivModdingFramework.Materials.DataContainers;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Mods;

namespace AlphaChannel.TexTools.UI;

public sealed class ColorsetRowView
{
    public ColorsetRowView(int index, AvColor diffuse, AvColor specular, AvColor emissive)
    {
        Index = index;
        Diffuse = diffuse;
        Specular = specular;
        Emissive = emissive;
    }

    public int Index { get; }
    public AvColor Diffuse { get; set; }
    public AvColor Specular { get; set; }
    public AvColor Emissive { get; set; }
    public string Label => $"Row {Index}";
    public override string ToString() =>
        $"{Label}  D:{Diffuse.R:X2}{Diffuse.G:X2}{Diffuse.B:X2}  S:{Specular.R:X2}{Specular.G:X2}{Specular.B:X2}  E:{Emissive.R:X2}{Emissive.G:X2}{Emissive.B:X2}";
}

/// <summary>
/// Simplified colorset read/export (Avalonia; not full classic editor parity).
/// </summary>
public static class ColorsetSimple
{
    public static async Task<(XivMtrl Mtrl, IReadOnlyList<ColorsetRowView> Rows, WriteableBitmap? Preview)> LoadAsync(
        string mtrlPath,
        IProgress<string>? log = null)
    {
        await GameSession.EnsureInitializedAsync(log);
        var tx = ModTransaction.BeginReadonlyTransaction();
        var mtrl = await Mtrl.GetXivMtrl(mtrlPath, false, tx);
        var rows = ParseRows(mtrl);
        WriteableBitmap? preview = null;
        try
        {
            preview = await BuildPreviewAsync(mtrl);
        }
        catch (Exception ex)
        {
            log?.Report("Colorset preview skipped: " + ex.Message);
        }

        return (mtrl, rows, preview);
    }

    public static IReadOnlyList<ColorsetRowView> ParseRows(XivMtrl mtrl)
    {
        var data = mtrl.ColorSetData;
        if (data == null || data.Count == 0)
            return Array.Empty<ColorsetRowView>();

        var valuesPerRow = data.Count >= 1024 ? 32 : 16;
        var rowCount = data.Count >= 1024 ? 32 : 16;
        var list = new List<ColorsetRowView>(rowCount);

        for (var r = 0; r < rowCount; r++)
        {
            var baseIdx = r * valuesPerRow;
            if (baseIdx + 11 >= data.Count) break;
            list.Add(new ColorsetRowView(
                r,
                HalfToColor(data[baseIdx], data[baseIdx + 1], data[baseIdx + 2], data[baseIdx + 3]),
                HalfToColor(data[baseIdx + 4], data[baseIdx + 5], data[baseIdx + 6], data[baseIdx + 7]),
                HalfToColor(data[baseIdx + 8], data[baseIdx + 9], data[baseIdx + 10], data[baseIdx + 11])));
        }

        return list;
    }

    public static async Task ExportColorsetPngAsync(XivMtrl mtrl, string destPng)
    {
        var bmp = await BuildPreviewAsync(mtrl, scale: 16);
        if (bmp == null) throw new InvalidOperationException("No colorset data.");
        bmp.Save(destPng);
    }

    private static async Task<WriteableBitmap?> BuildPreviewAsync(XivMtrl mtrl, int scale = 8)
    {
        _ = await Mtrl.GetColorsetXivTex(mtrl);
        var rows = ParseRows(mtrl);
        if (rows.Count == 0) return null;

        var pw = 4 * scale;
        var ph = rows.Count * scale;
        var bitmap = new WriteableBitmap(
            new Avalonia.PixelSize(pw, ph),
            new Avalonia.Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);
        using var fb = bitmap.Lock();
        var stride = pw * 4;
        var buf = new byte[stride * ph];

        for (var r = 0; r < rows.Count; r++)
        {
            // Diffuse | Specular | Emissive strips
            DrawCell(buf, stride, pw, ph, 0, r, scale, rows[r].Diffuse);
            DrawCell(buf, stride, pw, ph, 1, r, scale, rows[r].Specular);
            DrawCell(buf, stride, pw, ph, 2, r, scale, rows[r].Emissive);
            DrawCell(buf, stride, pw, ph, 3, r, scale, rows[r].Diffuse);
        }

        System.Runtime.InteropServices.Marshal.Copy(buf, 0, fb.Address, buf.Length);
        return bitmap;
    }

    private static void DrawCell(byte[] buf, int stride, int pw, int ph, int col, int row, int scale, AvColor c)
    {
        for (var y = row * scale; y < Math.Min((row + 1) * scale, ph); y++)
        {
            for (var x = col * scale; x < Math.Min((col + 1) * scale, pw); x++)
            {
                var i = y * stride + x * 4;
                buf[i] = c.B;
                buf[i + 1] = c.G;
                buf[i + 2] = c.R;
                buf[i + 3] = 255;
            }
        }
    }

    private static AvColor HalfToColor(SdxHalf r, SdxHalf g, SdxHalf b, SdxHalf a)
    {
        static byte Ch(SdxHalf h)
        {
            var v = (float)h;
            if (float.IsNaN(v) || float.IsInfinity(v)) v = 0;
            v = Math.Clamp(v, 0f, 1f);
            return (byte)Math.Round(v * 255f);
        }

        return AvColor.FromArgb(Ch(a), Ch(r), Ch(g), Ch(b));
    }
}
