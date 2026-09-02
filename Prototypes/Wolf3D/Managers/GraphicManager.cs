using Wolf3D.Assets;
using Wolf3D.Entities;

namespace Wolf3D.Managers;


internal class GraphicManager
{

    public GraphicManager(VideoManager videoManager, Lazy<AssetManager> assetManager)
    {
        this.videoManager = videoManager;
        this.assetManager = assetManager;
    }


    private readonly VideoManager videoManager;
    private readonly Lazy<AssetManager> assetManager;

    public void DrawPropString(int px, int py, string s, string fontcolor, string fontName)
    {
        var fontAsset = assetManager.Value.Find<FontAsset>(fontName);
        if (fontAsset == null)
            return;

        videoManager.DrawPropString(px, py, s, fontcolor, fontAsset);
    }

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
            videoManager.Bar(0, 0, 320, 200, bkgd.Color);
        }
        else if (component is Graphic gfx)
        {
            if (string.IsNullOrEmpty(gfx.Name))
                return;

            var gfxAsset = assetManager.Value.Find<GraphicAsset>(gfx.Name);
            if (gfxAsset == null)
                return;

            //string? foundKey = GraphicsMappings.GraphicKeys.FirstOrDefault(x => x.ToLowerInvariant().Equals(gfx.Asset.ToLowerInvariant()));
            //if (foundKey != null)
            //{
                //var foundchunk = GraphicsMappings.GraphicKeys.IndexOf(foundKey);
                //if (foundchunk != -1)
                //{
                    //int picnum = (int)(foundchunk - GraphicConstants.STARTPICS);
                    //int width, height;

                    //width = pictable[picnum].width;
                    //height = pictable[picnum].height;

                    if (gfx.HorizontalOrientation == HorizontalOrientation.Center)
                        gfx.X = 160 - gfxAsset.Width / 2;
                    else if (gfx.HorizontalOrientation == HorizontalOrientation.Right)
                        gfx.X = 320 - gfxAsset.Width;

                    if (gfx.VerticalOrientation == VerticalOrientation.Center)
                        gfx.Y = 100 - gfxAsset.Height / 2;
                    if (gfx.VerticalOrientation == VerticalOrientation.Bottom)
                        gfx.Y = 200 - gfxAsset.Height;

                    DrawPic(gfx.X, gfx.Y, gfxAsset);
                //}
           // }
        }
        else if (component is Stripe stripe)
        {
            videoManager.Bar(0, stripe.Y, 320, 24, stripe.BackingColor);
            videoManager.HorizontalLine(0, 319, stripe.Y + 22, stripe.LineColor);
        }
        else if (component is Window window)
        {
            videoManager.Bar(window.X, window.Y, window.Width, window.Height, "BKGDCOLOR");
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

    public void MeasurePropString(string text, string font, out ushort width, out ushort height)
    {
        var fontAsset = assetManager.Value.Find<FontAsset>(font);
        if (fontAsset == null)
        {
            width = 0;
            height = 0;
            return;
        }

        MeasureString(text, out width, out height, fontAsset);
    }

    public static void MeasureString(string text, out ushort width, out ushort height, FontAsset font)
    {
        width = 0;
        int i;
        height = (ushort)font.Height;
        for (i = 0; i < text.Length; i++)
        {
            width += font.Width[text[i]]; // proportional width
        }
    }

}
