using PFWolf.Assets;

namespace PFWolf.Fonts;

/// <summary>
/// Where a <see cref="Font"/> draws: the game's screen (its VideoManager) or a picture in the
/// editor (<see cref="IndexedTextCanvas"/>). Coordinates are the game's layout pixels (its 320x200
/// virtual screen); colors are names from the game pack's colors.yaml, #RRGGBB or a palette index.
/// </summary>
public interface ITextCanvas
{
    /// <summary>A color name, #RRGGBB or palette index, as the palette index it draws with</summary>
    byte ResolveColor(string color);

    /// <summary>
    /// A palette index for each of <paramref name="rows"/> rows of text, shading
    /// <paramref name="color"/> from <paramref name="top"/> percent at the first row to
    /// <paramref name="bottom"/> percent at the last (see <see cref="FontColors.Gradient"/>)
    /// </summary>
    byte[] GetGradient(string color, int rows, int top, int bottom);

    /// <summary>The color of each glow ring, nearest the text first, as palette indices in strings (see <see cref="FontColors.GlowRings"/>)</summary>
    string[] GetGlowColors(string glow, string background, int radius, int strength);

    /// <summary>
    /// Draws part of a paletted image (width by height from srcX, srcY) at (x, y), leaving out
    /// pixels the opacity mask clears and pixels of the key color. With a color, every pixel drawn
    /// is that color instead of its own, or with <paramref name="rowColors"/> too, each row that
    /// row's color. Clipped to the canvas.
    /// </summary>
    void DrawImageRegion(byte[] pixels, byte[]? opacityMask, int stride, int srcX, int srcY, int width, int height,
        int x, int y, byte? key = null, string? color = null, byte[]? rowColors = null);

    /// <summary>A line of text in a Wolf3D font at (px, py), clipped to the canvas</summary>
    /// <param name="rowColors">A palette index for each row of the glyphs, in place of <paramref name="fontcolor"/></param>
    void DrawPropString(int px, int py, string text, string fontcolor, FontAsset font, byte[]? rowColors = null);
}
