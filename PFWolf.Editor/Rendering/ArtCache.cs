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

    /// <summary>
    /// A picture whole, as the art browser shows it: every story of a wall texture or flat, a
    /// sprite by its own name with its see-through pixels clear, a VGA picture; null when there's none
    /// </summary>
    public Bitmap? Whole(ArtKind kind, string name)
    {
        var key = (kind, name.ToLowerInvariant());
        if (_whole.TryGetValue(key, out var bitmap))
            return bitmap;

        switch (kind)
        {
            case ArtKind.Texture or ArtKind.Flat when content.Find<TextureAsset>(name) is { Width: > 0, Height: > 0 } texture:
            {
                // Stored column by column
                int height = texture.Height;
                bitmap = MakeBitmap(texture.Width, height, (x, y) => texture.RawData[x * height + y], null);
                break;
            }
            case ArtKind.Sprite when content.Find<SpriteAsset>(name) is { Width: > 0, Height: > 0 } sprite:
            {
                int width = sprite.Width;
                bitmap = MakeBitmap(width, sprite.Height, (x, y) => sprite.RawData[y * width + x],
                    sprite.OpacityMask.Length > 0 ? (x, y) => sprite.OpacityMask[y * width + x] != 0 : null);
                break;
            }
            case ArtKind.Picture when content.Find<GraphicAsset>(name) is { Width: > 0, Height: > 0 } picture:
            {
                int width = picture.Width;
                var mask = picture.OpacityMask;
                bitmap = MakeBitmap(width, picture.Height, (x, y) => picture.RawData[y * width + x],
                    mask is { Length: > 0 } ? (x, y) => mask[y * width + x] != 0 : null);
                break;
            }
        }

        _whole[key] = bitmap;
        return bitmap;
    }

    private readonly Dictionary<(ArtKind, string), Bitmap?> _whole = [];

    /// <summary>
    /// A surface's whole picture as RGBA rows from the top, for the 3D view: every story of a
    /// wall texture, or a thing's sprite with its see-through pixels clear; null when there's none
    /// </summary>
    public (int Width, int Height, byte[] Rgba)? Pixels(TextureRef texture)
    {
        switch (texture.Source)
        {
            case TextureSource.Picture when content.Find<GraphicAsset>(texture.Name) is { Width: > 0, Height: > 0 } picture:
            {
                // Stored row by row
                int width = picture.Width;
                return (width, picture.Height, MakeRgba(width, picture.Height, (x, y) => picture.RawData[y * width + x], null));
            }
            case TextureSource.Picture:
                return Pixels(texture with { Source = TextureSource.Texture });
            case TextureSource.Texture when content.Find<TextureAsset>(texture.Name) is { Width: > 0, Height: > 0 } wall:
            {
                // Stored column by column
                int width = wall.Width, height = wall.Height;
                return (width, height, MakeRgba(width, height, (x, y) => wall.RawData[x * height + y], null));
            }
            case TextureSource.Sprite when content.ThingSprite(texture.Name) is { Width: > 0, Height: > 0 } sprite:
            {
                int width = sprite.Width;
                return (width, sprite.Height, MakeRgba(width, sprite.Height, (x, y) => sprite.RawData[y * width + x],
                    sprite.OpacityMask.Length > 0 ? (x, y) => sprite.OpacityMask[y * width + x] != 0 : null));
            }
            default:
                return null;
        }
    }

    private byte[] MakeRgba(int width, int height, Func<int, int, byte> index, Func<int, int, bool>? opaque)
    {
        var palette = content.Palette;
        var rgba = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (opaque != null && !opaque(x, y))
                    continue;
                var i = index(x, y);
                var color = i < palette.Length ? palette[i] : new PaletteColor(255, 0, 255);
                int at = (y * width + x) * 4;
                rgba[at] = color.Red;
                rgba[at + 1] = color.Green;
                rgba[at + 2] = color.Blue;
                rgba[at + 3] = 255;
            }
        }
        return rgba;
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
        foreach (var bitmap in _textures.Values.Concat(_sprites.Values).Concat(_whole.Values))
            bitmap?.Dispose();
        _textures.Clear();
        _sprites.Clear();
        _whole.Clear();
    }
}
