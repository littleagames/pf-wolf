using System.Globalization;
using PFWolf.Assets;

namespace PFWolf.Fonts;

/// <summary>
/// A picture of palette indices that fonts draw on, one layout pixel to a pixel: the editor's
/// previews of a font as the game draws it. Colors resolve as the game resolves them: the game
/// pack's colors.yaml names, #RRGGBB matched to the nearest palette color, or a palette index.
/// </summary>
public sealed class IndexedTextCanvas : ITextCanvas
{
    private readonly Palette _palette;
    private readonly Dictionary<string, byte> _named = new(StringComparer.Ordinal);
    private readonly Dictionary<(string, int, int, int), byte[]> _gradients = [];
    private readonly Dictionary<(string, string, int, int), string[]> _glows = [];

    /// <param name="palette">The game palette the indices are in</param>
    /// <param name="theme">The game pack's named colors (colors.yaml), if any</param>
    /// <param name="fill">The palette index the picture starts as</param>
    public IndexedTextCanvas(int width, int height, PaletteColor[] palette, ColorThemeAsset? theme = null, byte fill = 0)
    {
        Width = Math.Max(width, 1);
        Height = Math.Max(height, 1);
        Pixels = Enumerable.Repeat(fill, Width * Height).ToArray();
        _palette = new Palette { Colors = palette };
        foreach (var (name, color) in theme?.Colors ?? [])
            _named[name] = _palette.ClosestIndex(color.Red, color.Green, color.Blue);
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>The picture, row by row</summary>
    public byte[] Pixels { get; }

    /// <summary>Colors asked for that are neither a colors.yaml name, #RRGGBB nor a palette index (drawn as index 0, as the game does)</summary>
    public HashSet<string> UnknownColors { get; } = [];

    public byte ResolveColor(string color)
    {
        if (_named.TryGetValue(color, out byte index))
            return index;
        if (color.Length == 7 && color[0] == '#' && int.TryParse(color.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
            return _named[color] = _palette.ClosestIndex((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        if (byte.TryParse(color, out index))
            return index;
        UnknownColors.Add(color);
        return 0;
    }

    private (byte R, byte G, byte B) Rgb(byte index)
    {
        var c = _palette.Colors[Math.Min(index, _palette.Colors.Length - 1)];
        return (c.Red, c.Green, c.Blue);
    }

    public byte[] GetGradient(string color, int rows, int top, int bottom)
    {
        rows = Math.Max(rows, 1);
        var key = (color, rows, top, bottom);
        if (!_gradients.TryGetValue(key, out var table))
            _gradients[key] = table = FontColors.Gradient(Rgb(ResolveColor(color)), rows, top, bottom, _palette.ClosestIndex);
        return table;
    }

    public string[] GetGlowColors(string glow, string background, int radius, int strength)
    {
        radius = Math.Max(radius, 1);
        var key = (glow, background, radius, strength);
        if (!_glows.TryGetValue(key, out var colors))
            _glows[key] = colors = [.. FontColors.GlowRings(Rgb(ResolveColor(glow)), Rgb(ResolveColor(background)), radius, strength, _palette.ClosestIndex)
                .Select(ring => ring.ToString(CultureInfo.InvariantCulture))];
        return colors;
    }

    private void Put(int x, int y, byte color)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
            Pixels[y * Width + x] = color;
    }

    public void DrawImageRegion(byte[] pixels, byte[]? opacityMask, int stride, int srcX, int srcY, int width, int height,
        int x, int y, byte? key = null, string? color = null, byte[]? rowColors = null)
    {
        byte? col = color == null ? null : ResolveColor(color);
        for (int j = 0; j < height; j++)
        {
            byte? rowCol = col != null && rowColors != null ? rowColors[Math.Min(j, rowColors.Length - 1)] : col;
            for (int i = 0; i < width; i++)
            {
                int src = (srcY + j) * stride + srcX + i;
                if (src < 0 || src >= pixels.Length)
                    continue;
                byte pixel = pixels[src];
                if ((opacityMask != null && opacityMask[src] == 0) || pixel == key)
                    continue;
                Put(x + i, y + j, rowCol ?? pixel);
            }
        }
    }

    public void DrawPropString(int px, int py, string text, string fontcolor, FontAsset font, byte[]? rowColors = null)
    {
        int height = font.Height;
        byte col = ResolveColor(fontcolor);
        foreach (char ch in text)
        {
            // Past the font's 256 glyphs: leave a space's worth of room (see VgaFont.Advance)
            if (ch >= font.Width.Length)
            {
                px += font.Width[' '];
                continue;
            }

            int step = font.Width[ch];
            int location = font.Location[ch];
            for (int column = 0; column < step; column++, px++)
            {
                for (int i = 0; i < height; i++)
                {
                    int at = location + column + i * step;
                    if (at < 0 || at >= font.RawData.Length || font.RawData[at] == 0)
                        continue;
                    Put(px, py + i, rowColors != null ? rowColors[Math.Min(i, rowColors.Length - 1)] : col);
                }
            }
        }
    }
}
