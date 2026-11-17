using PFWolf.Assets;
using PFWolf.Fonts;

namespace PFWolf.Menus;

/// <summary>
/// A picture of palette indices that menus draw on, one layout pixel to a pixel: the editor's
/// previews of a menu as the game draws it. The 320x200 layout sits in the middle of the picture;
/// a picture wider or taller than that has margins round it, as a wide screen does in the game.
/// Colors resolve as the game resolves them (see <see cref="IndexedTextCanvas"/>).
/// </summary>
public sealed class IndexedMenuCanvas : IMenuCanvas
{
    public const int LayoutWidth = 320, LayoutHeight = 200;

    private readonly IndexedTextCanvas _picture;
    private readonly ITextCanvas _layout;
    private readonly Func<string, Font?> _findFont;
    private readonly Func<string, GraphicAsset?> _findPicture;

    /// <param name="palette">The game palette the indices are in</param>
    /// <param name="theme">The game pack's named colors (colors.yaml), if any</param>
    /// <param name="findFont">A font by name, as the game's FontManager finds it, or null</param>
    /// <param name="findPicture">A picture by name, or null</param>
    /// <param name="backdrop">game-info menu-backdrop</param>
    /// <param name="stripe">game-info menu-stripe</param>
    public IndexedMenuCanvas(int width, int height, PaletteColor[] palette, ColorThemeAsset? theme,
        Func<string, Font?> findFont, Func<string, GraphicAsset?> findPicture, string? backdrop = null, MenuStripeInfo? stripe = null)
    {
        _picture = new IndexedTextCanvas(Math.Max(width, LayoutWidth), Math.Max(height, LayoutHeight), palette, theme);
        OriginX = (Width - LayoutWidth) / 2;
        OriginY = (Height - LayoutHeight) / 2;
        _layout = new OffsetCanvas(_picture, OriginX, OriginY);
        _findFont = findFont;
        _findPicture = findPicture;
        MenuBackdrop = backdrop;
        MenuStripe = stripe ?? new MenuStripeInfo();
    }

    public int Width => _picture.Width;
    public int Height => _picture.Height;

    /// <summary>Where the layout's top left is in the picture</summary>
    public int OriginX { get; }
    public int OriginY { get; }

    /// <summary>The picture, row by row</summary>
    public byte[] Pixels => _picture.Pixels;

    /// <summary>Colors asked for that are neither a colors.yaml name, #RRGGBB nor a palette index (drawn as index 0, as the game does)</summary>
    public HashSet<string> UnknownColors => _picture.UnknownColors;

    /// <summary>What the picture is drawn with, in layout coordinates (fonts draw on it)</summary>
    public ITextCanvas TextCanvas => _layout;

    public string? MenuBackdrop { get; }
    public MenuStripeInfo MenuStripe { get; }
    public bool HasMargins => Width != LayoutWidth || Height != LayoutHeight;

    /// <summary>Fills a rectangle of the picture itself (not the layout), clipped to it</summary>
    private void Fill(int x, int y, int width, int height, string color)
    {
        int x0 = Math.Max(x, 0), y0 = Math.Max(y, 0);
        int x1 = Math.Min(x + width, Width), y1 = Math.Min(y + height, Height);
        if (x0 >= x1 || y0 >= y1)
            return;
        byte index = _picture.ResolveColor(color);
        for (int row = y0; row < y1; row++)
            Array.Fill(_picture.Pixels, index, row * Width + x0, x1 - x0);
    }

    public void Bar(int x, int y, int width, int height, string color) => Fill(x + OriginX, y + OriginY, width, height, color);

    public void FillScreen(string color) => Fill(0, 0, Width, Height, color);

    public void BarAcross(int top, int bottom, string color) => Fill(0, top + OriginY, Width, bottom - top, color);

    public void DrawText(int x, int y, string text, TextStyle style)
    {
        if (_findFont(style.Font) is { } font)
            TextDrawing.Draw(_layout, font, x, y, text, style);
    }

    public void MeasureText(string text, string fontName, out int width, out int height)
    {
        var font = _findFont(fontName);
        width = font?.Measure(text) ?? 0;
        height = font?.LineHeight ?? 0;
    }

    public bool HasPicture(string name) => !string.IsNullOrEmpty(name) && _findPicture(name) != null;

    public (int Width, int Height)? PictureSize(string name)
        => !string.IsNullOrEmpty(name) && _findPicture(name) is { } picture ? (picture.Width, picture.Height) : null;

    public void DrawPicture(string name, int x, int y)
    {
        if (string.IsNullOrEmpty(name) || _findPicture(name) is not { } picture)
            return;
        _layout.DrawImageRegion(picture.RawData, picture.OpacityMask, picture.Width, 0, 0, picture.Width, picture.Height, x, y);
    }

    // The picture with the layout's origin moved: what fonts and pictures draw on
    private sealed class OffsetCanvas(IndexedTextCanvas picture, int dx, int dy) : ITextCanvas
    {
        public byte ResolveColor(string color) => picture.ResolveColor(color);

        public byte[] GetGradient(string color, int rows, int top, int bottom) => picture.GetGradient(color, rows, top, bottom);

        public string[] GetGlowColors(string glow, string background, int radius, int strength)
            => picture.GetGlowColors(glow, background, radius, strength);

        public void DrawImageRegion(byte[] pixels, byte[]? opacityMask, int stride, int srcX, int srcY, int width, int height,
            int x, int y, byte? key = null, string? color = null, byte[]? rowColors = null)
            => picture.DrawImageRegion(pixels, opacityMask, stride, srcX, srcY, width, height, x + dx, y + dy, key, color, rowColors);

        public void DrawPropString(int px, int py, string text, string fontcolor, FontAsset font, byte[]? rowColors = null)
            => picture.DrawPropString(px + dx, py + dy, text, fontcolor, font, rowColors);
    }
}
