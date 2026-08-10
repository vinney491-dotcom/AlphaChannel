using Dalamud.Interface.Textures.TextureWraps;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace AlphaChannel.Plugin;

// Bundled PNGs next to the assembly (Assets/). Loaded once, cached by filename.
internal sealed class AssetTextures : IDisposable
{
    private readonly Dictionary<string, IDalamudTextureWrap?> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> loading = new(StringComparer.OrdinalIgnoreCase);

    internal IDalamudTextureWrap? Get(string fileName)
    {
        if (cache.TryGetValue(fileName, out var wrap))
        {
            return wrap;
        }

        if (!loading.Add(fileName))
        {
            return null;
        }

        _ = LoadAsync(fileName);
        return null;
    }

    private async Task LoadAsync(string fileName)
    {
        try
        {
            var dir = Plugin.PluginInterface.AssemblyLocation.DirectoryName;
            if (dir is null)
            {
                return;
            }

            var path = Path.Combine(dir, "Assets", fileName);
            if (!File.Exists(path))
            {
                AepLog.Warning($"[Assets] Missing {path}");
                cache[fileName] = null;
                return;
            }

            var bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
            using var image = Image.Load(bytes);
            using var pngStream = new MemoryStream();
            await image.SaveAsync(pngStream, new PngEncoder()).ConfigureAwait(false);
            var wrap = await Plugin.TextureProvider.CreateFromImageAsync(pngStream.ToArray())
                .ConfigureAwait(false);
            cache[fileName] = wrap;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Assets] Failed to load {fileName}: {exception.Message}");
            cache[fileName] = null;
        }
        finally
        {
            loading.Remove(fileName);
        }
    }

    public void Dispose()
    {
        foreach (var wrap in cache.Values)
        {
            wrap?.Dispose();
        }

        cache.Clear();
        loading.Clear();
    }
}
