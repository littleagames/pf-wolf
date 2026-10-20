namespace PFWolf.Assets;

/// <summary>
/// The pictures in graphicdefs/ (shared) or graphicdefs/{pack}/ (the pack's own), by name, each
/// cut out of a sprite, wall or picture, as written:
/// <code>
/// GUARDHEAD:
///   source: GARDA1          # a sprite, wall or picture
///   kind: sprite            # optional: sprite, texture or graphic (else the first there, in that order)
///   x: 20                   # the crop's top left corner in the source
///   y: 2
///   width: 24
///   height: 32
///   background: "#004040"   # optional: what see-through pixels are filled with (a color name, #RRGGBB or palette index)
/// </code>
/// AssetManager turns each into a <see cref="GraphicAsset"/> of exactly width by height once
/// every asset is loaded, so it's drawn like any other picture.
/// </summary>
public record GraphicDefinitionsAsset : Asset
{
    public GraphicDefinitionsAsset()
    {
    }

    public GraphicDefinitionsAsset(Dictionary<string, GraphicDefinition>? graphics)
    {
        foreach (var (name, graphic) in graphics ?? [])
            Graphics[name] = graphic ?? new GraphicDefinition();
    }

    public Dictionary<string, GraphicDefinition> Graphics { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>One picture in graphicdefs: a crop of another asset</summary>
public class GraphicDefinition
{
    public const string SpriteKind = "sprite", TextureKind = "texture", GraphicKind = "graphic";

    /// <summary>The sprite, wall or picture it's cut from</summary>
    public string? Source { get; set; }

    /// <summary>sprite, texture or graphic; null takes the first of those the source is</summary>
    public string? Kind { get; set; }

    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>What see-through pixels are filled with (a color name, #RRGGBB or palette index); null leaves them see-through</summary>
    public string? Background { get; set; }
}

/// <summary>
/// A picture to crop from: its size, and each pixel's color and whether it shows, however the
/// asset stores them (sprites and pictures row by row, walls column by column)
/// </summary>
public readonly record struct CropSource(int Width, int Height, Func<int, int, (byte Color, bool Shows)> PixelAt)
{
    public static CropSource From(SpriteAsset sprite)
    {
        int width = sprite.Width;
        var mask = sprite.OpacityMask;
        return new(sprite.Width, sprite.Height, (x, y) =>
        {
            int i = y * width + x;
            return (sprite.RawData[i], mask.Length <= i || mask[i] != 0);
        });
    }

    public static CropSource From(TextureAsset texture)
    {
        int height = texture.Height;
        return new(texture.Width, texture.Height, (x, y) =>
        {
            var color = texture.RawData[x * height + y];
            return (color, !(texture.Masked && color == 0xFF));
        });
    }

    public static CropSource From(GraphicAsset graphic)
    {
        int width = graphic.Width;
        var mask = graphic.OpacityMask;
        return new(graphic.Width, graphic.Height, (x, y) =>
        {
            int i = y * width + x;
            return (graphic.RawData[i], mask == null || mask[i] != 0);
        });
    }

    /// <summary>
    /// The picture width by height from (x, y): where it runs past the source, or the source
    /// doesn't show, it's see-through, or <paramref name="background"/> when given. Every pixel
    /// showing leaves it without an opacity mask.
    /// </summary>
    public GraphicAsset Crop(int x, int y, int width, int height, byte? background)
    {
        var pixels = new byte[width * height];
        var mask = new byte[width * height];
        bool seeThrough = false;
        for (int row = 0; row < height; row++)
            for (int column = 0; column < width; column++)
            {
                int sx = x + column, sy = y + row, i = row * width + column;
                var (color, shows) = sx >= 0 && sy >= 0 && sx < Width && sy < Height ? PixelAt(sx, sy) : ((byte)0, false);
                if (!shows && background is { } fill)
                    (color, shows) = (fill, true);
                pixels[i] = color;
                mask[i] = shows ? (byte)1 : (byte)0;
                seeThrough |= !shows;
            }

        return new GraphicAsset(pixels, (short)width, (short)height) { OpacityMask = seeThrough ? mask : null };
    }
}
