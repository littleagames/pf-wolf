using PFWolf.Assets;
using PFWolf.Fonts;
using PFWolf.Menus;

namespace PFWolf.Managers;

/// <summary>
/// Pictures and text on the screen, in the 320x200 layout; also what menus are drawn on (the
/// shared <see cref="MenuPainter"/>, as <see cref="Menus"/>)
/// </summary>
internal class GraphicManager : IMenuCanvas
{

    public GraphicManager(VideoManager videoManager, Lazy<AssetManager> assetManager, FontManager fontManager)
    {
        this.videoManager = videoManager;
        this.assetManager = assetManager;
        this.fontManager = fontManager;
        Menus = new MenuPainter(this);
    }


    private readonly VideoManager videoManager;
    private readonly Lazy<AssetManager> assetManager;
    private readonly FontManager fontManager;

    /// <summary>Draws menus (their components, rows and cursor) on this screen</summary>
    public MenuPainter Menus { get; }

    /// <summary>
    /// Graphic drawn behind the menus in place of their background color (game-info menu-backdrop)
    /// </summary>
    public string? MenuBackdrop { get; set; }

    /// <summary>
    /// Shape and colors of the band across the top of the menus (game-info menu-stripe)
    /// </summary>
    public MenuStripeInfo MenuStripe { get; set; } = new();

    /// <summary>
    /// Draws the menu stripe with its top at <paramref name="y"/>, in the given colors or the game's
    /// </summary>
    public void DrawStripe(int y, string? color = null, string? lineColor = null) => Menus.DrawStripe(y, color, lineColor);

    public void DrawMenuBackground(string color) => Menus.DrawBackground(color);

    public bool HasMargins => videoManager.HasMargins;

    public void FillScreen(string color) => videoManager.FillScreen(color);

    public void BarAcross(int top, int bottom, string color)
    {
        int screenTop = videoManager.ScreenY(top);
        videoManager.BarScaledCoord(0, screenTop, videoManager.ScreenWidth, videoManager.ScreenY(bottom) - screenTop, color);
    }

    public bool HasPicture(string name) => assetManager.Value.Exists<GraphicAsset>(name);

    public (int Width, int Height)? PictureSize(string name) => PicSize(name);

    public void DrawPicture(string name, int x, int y) => DrawPic(name, x, y);

    /// <summary>Draws one line of text; a missing font draws nothing</summary>
    public void DrawText(int x, int y, string text, TextStyle style)
    {
        var font = fontManager.Find(style.Font);
        if (font != null)
            TextDrawing.Draw(videoManager, font, x, y, text, style);
    }

    public void DrawText(int x, int y, string text, Font font, string color) => font.Draw(videoManager, x, y, text, color);

    /// <summary>Width and line height of one line of text; zero for a missing font</summary>
    public void MeasureText(string text, string fontName, out int width, out int height)
    {
        var font = fontManager.Find(fontName);
        width = font?.Measure(text) ?? 0;
        height = font?.LineHeight ?? 0;
    }

    public void Bar(int x, int y, int width, int height, string color) => videoManager.Bar(x, y, width, height, color);

    public void DrawTile8(int x, int y, int tile)
    {
        var tile8Asset = assetManager.Value.Find<Tile8Asset>("TILE8");
        if (tile8Asset == null)
            return;
        videoManager.MemToScreen(tile8Asset.RawData.Skip(tile * 64).ToArray(), 8, 8, x, y);
    }

    public void DrawPic(string graphicName, int x, int y)
    {
        if (string.IsNullOrEmpty(graphicName))
            return;

        var asset = assetManager.Value.Find<GraphicAsset>(graphicName);
        if (asset == null)
            return;

        DrawPic(x, y, asset);
    }

    /// <summary>
    /// Draws a picture; an animdefs animation shows its frame <paramref name="elapsedTics"/> after
    /// it started (a title screen's own clock), rather than by the engine's
    /// </summary>
    public void DrawPicFrame(string graphicName, int x, int y, int elapsedTics)
    {
        if (!string.IsNullOrEmpty(graphicName) && assetManager.Value.Find<GraphicAsset>(graphicName, elapsedTics) is { } asset)
            DrawPic(x, y, asset);
    }

    /// <summary>Draws part of a picture: width by height of it from (srcX, srcY), at (x, y)</summary>
    public void DrawPicRegion(string graphicName, int srcX, int srcY, int width, int height, int x, int y)
    {
        if (string.IsNullOrEmpty(graphicName) || assetManager.Value.Find<GraphicAsset>(graphicName) is not { } asset)
            return;
        width = Math.Min(width, asset.Width - srcX);
        height = Math.Min(height, asset.Height - srcY);
        if (srcX < 0 || srcY < 0 || width <= 0 || height <= 0)
            return;
        videoManager.DrawImageRegion(asset.RawData, asset.OpacityMask, asset.Width, srcX, srcY, width, height, x, y);
    }

    /// <summary>A picture's size in 320x200 pixels, or null if there's no such picture</summary>
    public (int Width, int Height)? PicSize(string graphicName) =>
        !string.IsNullOrEmpty(graphicName) && assetManager.Value.Find<GraphicAsset>(graphicName) is { } asset
            ? (asset.Width, asset.Height) : null;

    /// <summary>Whether a picture has see-through pixels (a PNG with alpha), so what's behind it shows</summary>
    public bool IsSeeThrough(string graphicName) =>
        !string.IsNullOrEmpty(graphicName) && assetManager.Value.Find<GraphicAsset>(graphicName)?.OpacityMask != null;

    /// <summary>Draws a picture leaving out the pixels of a palette index (a masked pic), or all of it with no mask</summary>
    public void DrawPic(string graphicName, int x, int y, byte? mask)
    {
        if (mask == null)
        {
            DrawPic(graphicName, x, y);
            return;
        }
        if (string.IsNullOrEmpty(graphicName) || assetManager.Value.Find<GraphicAsset>(graphicName) is not { } asset)
            return;
        videoManager.DrawImageRegion(asset.RawData, null, asset.Width, 0, 0, asset.Width, asset.Height, x, y, mask);
    }

    // A picture's see-through pixels (a PNG's alpha) let what's behind show
    private void DrawPic(int x, int y, GraphicAsset gfxAsset)
    {
        videoManager.MemToScreen(gfxAsset.RawData, gfxAsset.Width, gfxAsset.Height, x, y, gfxAsset.OpacityMask);
    }

    public void DrawPicScaledCoord(int scx, int scy, GraphicAsset gfxAsset)
    {
        videoManager.MemToScreenScaledCoord(gfxAsset.RawData, gfxAsset.Width, gfxAsset.Height, scx, scy, gfxAsset.OpacityMask);
    }

    /// <summary>Draws a picture with its top left corner at a screen position (pixels), at the UI scale.</summary>
    public void DrawPicScaledCoord(string graphicName, int scx, int scy)
    {
        if (!string.IsNullOrEmpty(graphicName) && assetManager.Value.Find<GraphicAsset>(graphicName) is { } asset)
            DrawPicScaledCoord(scx, scy, asset);
    }
}
