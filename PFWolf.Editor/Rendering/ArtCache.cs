using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PFWolf.Assets;
using PFWolf.Editor.Data;

namespace PFWolf.Editor.Rendering;

/// <summary>
/// The game's wall textures and sprites as bitmaps in its palette, each made once
/// </summary>
public sealed class ArtCache(GameContent content) : IDisposable
{
    private readonly Dictionary<string, Bitmap?> _textures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Bitmap?> _sprites = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A wall texture's bottom story (what a map tile shows), or null when there's no such texture</summary>
    public Bitmap? Texture(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        if (_textures.TryGetValue(name, out var bitmap))
            return bitmap;

        if (content.Find<TextureAsset>(name) is { } texture)
        {
            // Stored column by column
            var pixels = texture.BottomStory();
            int size = TextureAsset.StorySize;
            bitmap = MakeBitmap(size, size, (x, y) => pixels[x * size + y], null);
        }

        _textures[name] = bitmap;
        return bitmap;
    }

    /// <summary>The picture a thing's class spawns with, or null when it has none</summary>
    public Bitmap? ThingSprite(string className)
    {
        if (_sprites.TryGetValue(className, out var bitmap))
            return bitmap;

        if (content.ThingSprite(className) is { Width: > 0, Height: > 0 } sprite)
        {
            int width = sprite.Width;
            bitmap = MakeBitmap(width, sprite.Height, (x, y) => sprite.RawData[y * width + x],
                sprite.OpacityMask.Length > 0 ? (x, y) => sprite.OpacityMask[y * width + x] != 0 : null);
        }

        _sprites[className] = bitmap;
        return bitmap;
    }

    private WriteableBitmap MakeBitmap(int width, int height, Func<int, int, byte> index, Func<int, int, bool>? opaque)
    {
        var palette = content.Palette;
        var argb = new int[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (opaque != null && !opaque(x, y))
                    continue;   // transparent black, premultiplied
                var i = index(x, y);
                var color = i < palette.Length ? palette[i] : new PaletteColor(255, 0, 255);
                argb[y * width + x] = (255 << 24) | (color.Red << 16) | (color.Green << 8) | color.Blue;
            }
        }

        var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var buffer = bitmap.Lock())
        {
            for (int y = 0; y < height; y++)
                Marshal.Copy(argb, y * width, buffer.Address + y * buffer.RowBytes, width);
        }
        return bitmap;
    }

    public void Dispose()
    {
        foreach (var bitmap in _textures.Values.Concat(_sprites.Values))
            bitmap?.Dispose();
        _textures.Clear();
        _sprites.Clear();
    }
}
