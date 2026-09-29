using Wolf3D.Assets;
using Wolf3D.Entities;
using Wolf3D.Fonts;

namespace Wolf3D.Managers;


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

    public void DrawMenuBackground(string color)
    {
        if (!string.IsNullOrEmpty(MenuBackdrop) && assetManager.Value.Exists<GraphicAsset>(MenuBackdrop))
            DrawPic(MenuBackdrop, 0, 0);
        else
            videoManager.Bar(0, 0, 320, 200, color);
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

        font.Draw(videoManager, x, y, text, style.Color, shadow, style.Gradient ?? font.Gradient);
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
            videoManager.Bar(0, stripe.Y, 320, 24, stripe.BackingColor);
            videoManager.HorizontalLine(0, 319, stripe.Y + 22, stripe.LineColor);
        }
        else if (component is Window window)
        {
            videoManager.Bar(window.X, window.Y, window.Width, window.Height, window.Color);
            DrawOutline(window.X, window.Y, window.Width, window.Height, "BORD2COLOR", "DEACTIVE");
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

    private void DrawPic(int x, int y, GraphicAsset gfxAsset)
    {
        videoManager.MemToScreen(gfxAsset.RawData, gfxAsset.Width, gfxAsset.Height, x, y);
    }

    public void DrawPicScaledCoord(int scx, int scy, GraphicAsset gfxAsset)
    {
        videoManager.MemToScreenScaledCoord(gfxAsset.RawData, gfxAsset.Width, gfxAsset.Height, scx, scy);
    }
}
