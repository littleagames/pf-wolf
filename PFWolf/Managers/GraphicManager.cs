using PFWolf.Assets;
using PFWolf.Entities;
using PFWolf.Fonts;

namespace PFWolf.Managers;


internal class GraphicManager
{

    public GraphicManager(VideoManager videoManager, Lazy<AssetManager> assetManager, FontManager fontManager)
    {
        this.videoManager = videoManager;
        this.assetManager = assetManager;
        this.fontManager = fontManager;
    }


    private readonly VideoManager videoManager;
    private readonly Lazy<AssetManager> assetManager;
    private readonly FontManager fontManager;

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
    public void DrawStripe(int y, string? color = null, string? lineColor = null)
    {
        // Across the whole screen, however wide
        int top = videoManager.ScreenY(y), line = videoManager.ScreenY(y + MenuStripe.LineY);
        videoManager.BarScaledCoord(0, top, videoManager.screenWidth, videoManager.ScreenY(y + MenuStripe.Height) - top, color ?? MenuStripe.Color);
        videoManager.BarScaledCoord(0, line, videoManager.screenWidth, videoManager.ScreenY(y + MenuStripe.LineY + 1) - line, lineColor ?? MenuStripe.LineColor);
    }

    public void DrawMenuBackground(string color)
    {
        // A backdrop covers the 320x200 in the middle of the screen; the color fills the rest
        bool backdrop = !string.IsNullOrEmpty(MenuBackdrop) && assetManager.Value.Exists<GraphicAsset>(MenuBackdrop);
        if (!backdrop || videoManager.HasMargins)
            videoManager.FillScreen(color);
        if (backdrop)
            DrawPic(MenuBackdrop!, 0, 0);
    }

    /// <summary>Draws one line of text; a missing font draws nothing</summary>
    public void DrawText(int x, int y, string text, TextStyle style)
    {
        var font = fontManager.Find(style.Font);
        if (font == null)
            return;

        var shadow = style.Shadow ?? font.Shadow;
        if (shadow != null && shadow != FontShadow.None && style.ShadowColor != null)
            shadow = shadow with { Color = style.ShadowColor };

        var outline = style.Outline ?? font.Outline;
        if (outline != null && outline != FontOutline.None && style.OutlineColor != null)
            outline = outline with { Color = style.OutlineColor };

        // A glow color draws the rings flat, fading from that color into itself
        var glow = style.Glow ?? font.Glow;
        var glowBackground = style.Background;
        if (glow != null && glow != FontGlow.None && style.GlowColor != null)
        {
            glow = glow with { Color = style.GlowColor };
            glowBackground = style.GlowColor;
        }

        font.Draw(videoManager, x, y, text, style.Color, shadow, style.Gradient ?? font.Gradient, outline, glow, glowBackground);
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

    public void DrawComponent(MenuComponent component)
    {
        if (component is Background bkgd)
        {
            DrawMenuBackground(bkgd.Color);
        }
        else if (component is Graphic gfx)
        {
            if (string.IsNullOrEmpty(gfx.Name))
                return;

            var gfxAsset = assetManager.Value.Find<GraphicAsset>(gfx.Name);
            if (gfxAsset == null)
                return;

            // Orientation overrides the explicit coordinate on that axis
            int x = gfx.HorizontalOrientation switch
            {
                HorizontalOrientation.Center => 160 - gfxAsset.Width / 2,
                HorizontalOrientation.Right => 320 - gfxAsset.Width,
                _ => gfx.X
            };
            int y = gfx.VerticalOrientation switch
            {
                VerticalOrientation.Center => 100 - gfxAsset.Height / 2,
                VerticalOrientation.Bottom => 200 - gfxAsset.Height,
                _ => gfx.Y
            };

            DrawPic(x, y, gfxAsset);
        }
        else if (component is Stripe stripe)
        {
            DrawStripe(stripe.Y, stripe.BackingColor, stripe.LineColor);
        }
        else if (component is Window window)
        {
            videoManager.Bar(window.X, window.Y, window.Width, window.Height, window.Color);
            DrawOutline(window.X, window.Y, window.Width, window.Height, "BORD2COLOR", "DEACTIVE");
        }
        else if (component is Fill fill)
        {
            videoManager.Bar(fill.X, fill.Y, fill.Width, fill.Height, fill.Color);
        }
    }

    private void DrawOutline(int x, int y, int w, int h, string color1, string color2)
    {
        videoManager.HorizontalLine(x, x + w, y, color2);
        videoManager.VerticalLine(y, y + h, x, color2);
        videoManager.HorizontalLine(x, x + w, y + h, color1);
        videoManager.VerticalLine(y, y + h, x + w, color1);
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

    private void DrawPic(int x, int y, GraphicAsset gfxAsset)
    {
        videoManager.MemToScreen(gfxAsset.RawData, gfxAsset.Width, gfxAsset.Height, x, y);
    }

    public void DrawPicScaledCoord(int scx, int scy, GraphicAsset gfxAsset)
    {
        videoManager.MemToScreenScaledCoord(gfxAsset.RawData, gfxAsset.Width, gfxAsset.Height, scx, scy);
    }

    /// <summary>Draws a picture with its top left corner at a screen position (pixels), at the UI scale.</summary>
    public void DrawPicScaledCoord(string graphicName, int scx, int scy)
    {
        if (!string.IsNullOrEmpty(graphicName) && assetManager.Value.Find<GraphicAsset>(graphicName) is { } asset)
            DrawPicScaledCoord(scx, scy, asset);
    }
}
