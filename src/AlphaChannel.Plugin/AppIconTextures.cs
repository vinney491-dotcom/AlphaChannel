using Dalamud.Bindings.ImGui;

namespace AlphaChannel.Plugin;

// Loads white-on-transparent stencil icons from Assets/Icons and draws them tinted.
// Same pattern as Aetherphone's AppIconTextures: shape lives in alpha; RGB stays white.
internal static class AppIconTextures
{
    // Icon glyph fills this fraction of the disc/tile so it matches the art safe area.
    private const float GlyphFraction = 0.62f;

    private static readonly string IconDirectory =
        Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Assets", "Icons");

    private static readonly Dictionary<string, string?> ResolvedPaths = new(StringComparer.Ordinal);

    public static bool TryDraw(ImDrawListPtr drawList, string id, Vector2 center, float size, Vector4 tint)
    {
        var path = ResolvePath(id);
        if (path is null)
        {
            return false;
        }

        var texture = Plugin.TextureProvider.GetFromFile(path).GetWrapOrDefault();
        if (texture is null || texture.Handle == nint.Zero)
        {
            return false;
        }

        var half = size * GlyphFraction * 0.5f;
        var min = new Vector2(center.X - half, center.Y - half);
        var max = new Vector2(center.X + half, center.Y + half);
        drawList.AddImage(texture.Handle, min, max, Vector2.Zero, Vector2.One, ImGui.GetColorU32(tint));
        return true;
    }

    private static string? ResolvePath(string id)
    {
        if (ResolvedPaths.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var candidate = Path.Combine(IconDirectory, id + ".png");
        if (!File.Exists(candidate))
        {
            ResolvedPaths[id] = null;
            return null;
        }

        ResolvedPaths[id] = candidate;
        return candidate;
    }
}
