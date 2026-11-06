using PFWolf.Assets;
using PFWolf.Gameplay;
using PFWolf.Scripting;

namespace PFWolf.Ui;

/// <summary>
/// The puzzle the player on this machine is working (Gameplay/Puzzles.cs), drawn over the view
/// each frame: the view dimmed, the game's frame round the puzzle's panel with its title
/// (game-info puzzle-panel), then the puzzle's own draw inside it (Scripting/GameScripts.Puzzles.cs)
/// </summary>
/// <param name="viewOnScreen">The 3D view, in screen pixels (dimmed)</param>
/// <param name="viewLayout">The 3D view, in layout pixels (the panel's centered in it)</param>
/// <param name="localize">A $NAME language key's text, or the text</param>
internal sealed class PuzzlePanel(
    VideoManager video,
    GraphicManager graphics,
    Puzzles puzzles,
    GameScripts scripts,
    Func<PuzzlePanelInfo> panelInfo,
    Func<(int X, int Y, int Width, int Height)> viewOnScreen,
    Func<(int X, int Y, int Width, int Height)> viewLayout,
    Func<string, string> localize)
{
    // Layout pixels between the frame's edge and the puzzle's panel (and the title)
    internal const int Margin = 4;

    internal void Draw()
    {
        if (puzzles.LocalPuzzle is not { } puzzle)
            return;
        var panel = panelInfo();
        var definition = puzzle.Definition;

        if (panel.Dim < 1)
        {
            var (sx, sy, sw, sh) = viewOnScreen();
            video.ShadeRegionScaledCoord(sx, sy, sw, sh, video.GetDarkenTable((float)Math.Clamp(panel.Dim, 0, 1)));
        }

        var title = definition.Title.Length > 0 ? localize(definition.Title) : "";
        int titleWidth = 0, titleHeight = 0;
        if (title.Length > 0)
        {
            graphics.MeasureText(title, panel.TitleFont, out titleWidth, out titleHeight);
            titleHeight += Margin;
        }

        var (frameX, frameY, frameWidth, frameHeight) = Frame(definition, viewLayout(), titleHeight);
        switch (panel.Frame.ToLowerInvariant())
        {
            case "menu":
                video.Bar(frameX, frameY, frameWidth, frameHeight, panel.Background);
                video.HorizontalLine(frameX, frameX + frameWidth, frameY, panel.BorderShadow);
                video.VerticalLine(frameY, frameY + frameHeight, frameX, panel.BorderShadow);
                video.HorizontalLine(frameX, frameX + frameWidth, frameY + frameHeight, panel.Border);
                video.VerticalLine(frameY, frameY + frameHeight, frameX + frameWidth, panel.Border);
                break;
            case "none":
                break;
            default:
                graphics.DrawPic(panel.Frame, frameX, frameY);
                break;
        }

        if (title.Length > 0)
            graphics.DrawText(frameX + (frameWidth - titleWidth) / 2, frameY + Margin, title, new Fonts.TextStyle(panel.TitleFont, panel.TitleColor));

        scripts.DrawPuzzle(puzzle, frameX + Margin, frameY + Margin + titleHeight);
    }

    /// <summary>
    /// The frame round a puzzle's panel, in layout pixels: its panel with a margin all round and
    /// the title's line above, centered in the view
    /// </summary>
    internal static (int X, int Y, int Width, int Height) Frame(PuzzleDefinition definition, (int X, int Y, int Width, int Height) view, int titleHeight)
    {
        int width = definition.Width + 2 * Margin, height = definition.Height + 2 * Margin + titleHeight;
        return (view.X + (view.Width - width) / 2, view.Y + (view.Height - height) / 2, width, height);
    }
}
