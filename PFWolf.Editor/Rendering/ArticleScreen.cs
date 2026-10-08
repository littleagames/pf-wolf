using PFWolf.Assets;
using PFWolf.Editor.Data;

namespace PFWolf.Editor.Rendering;

/// <summary>Draws an article page on a 320x200 screen of palette indices, as the game's PageLayout draws it</summary>
public static class ArticleScreen
{
    public static IndexedPicture Draw(ArticlePage page, GameContent content, FontAsset? font, byte backColor)
    {
        const int width = ArticleLayout.ScreenWidth, height = ArticleLayout.ScreenHeight;
        var screen = new byte[width * height];
        Array.Fill(screen, backColor);

        foreach (var draw in page.Draws)
        {
            switch (draw.Kind)
            {
                case ArticleDrawKind.Picture when content.Find<GraphicAsset>(draw.Text) is { Width: > 0, Height: > 0 } picture:
                    DrawPicture(screen, picture, draw.X, draw.Y);
                    break;
                case ArticleDrawKind.Bar:
                    for (int y = Math.Max(draw.Y, 0); y < Math.Min(draw.Y + draw.Height, height); y++)
                        for (int x = Math.Max(draw.X, 0); x < Math.Min(draw.X + draw.Width, width); x++)
                            screen[y * width + x] = backColor;
                    break;
                case ArticleDrawKind.Text when font != null:
                    DrawText(screen, font, draw.X, draw.Y, draw.Text, draw.Color);
                    break;
            }
        }

        return new IndexedPicture(width, height, screen, null);
    }

    private static void DrawPicture(byte[] screen, GraphicAsset picture, int left, int top)
    {
        const int width = ArticleLayout.ScreenWidth, height = ArticleLayout.ScreenHeight;
        var mask = picture.OpacityMask is { Length: > 0 } opacity ? opacity : null;
        for (int y = 0; y < picture.Height; y++)
        {
            int sy = top + y;
            if (sy < 0 || sy >= height)
                continue;
            for (int x = 0; x < picture.Width; x++)
            {
                int sx = left + x, i = y * picture.Width + x;
                if (sx < 0 || sx >= width || i >= picture.RawData.Length || mask != null && mask[i] == 0)
                    continue;
                screen[sy * width + sx] = picture.RawData[i];
            }
        }
    }

    // VideoManager.DrawPropString: each glyph's columns, a byte per pixel row by row, drawn where it's not 0
    private static void DrawText(byte[] screen, FontAsset font, int px, int py, string text, byte color)
    {
        const int width = ArticleLayout.ScreenWidth, height = ArticleLayout.ScreenHeight;
        foreach (char ch in text)
        {
            if (ch >= font.Width.Length)
            {
                px += font.Width[' '];
                continue;
            }

            int step = font.Width[ch];
            int location = font.Location[ch];
            for (int column = 0; column < step; column++, px++)
            {
                if (px < 0 || px >= width)
                    continue;
                for (int row = 0; row < font.Height; row++)
                {
                    int y = py + row, at = location + column + row * step;
                    if (y < 0 || y >= height || at < 0 || at >= font.RawData.Length || font.RawData[at] == 0)
                        continue;
                    screen[y * width + px] = color;
                }
            }
        }
    }
}
