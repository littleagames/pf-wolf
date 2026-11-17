using PFWolf.Assets;
using PFWolf.Fonts;

namespace PFWolf.Menus;

/// <summary>
/// What a <see cref="MenuPainter"/> draws menus on: the game's screen (its GraphicManager) or a
/// picture in the editor. Coordinates are the game's 320x200 layout pixels; on a wider screen the
/// layout is centered and the room round it is the screen's margins.
/// </summary>
public interface IMenuCanvas : ITextSurface
{
    /// <summary>The picture drawn behind the menus in place of their background color (game-info menu-backdrop)</summary>
    string? MenuBackdrop { get; }

    /// <summary>The shape and colors of the band across the top of the menus (game-info menu-stripe)</summary>
    MenuStripeInfo MenuStripe { get; }

    /// <summary>Whether the screen has room beyond the 320x200 layout</summary>
    bool HasMargins { get; }

    /// <summary>Fills the whole screen, the layout and any room round it</summary>
    void FillScreen(string color);

    /// <summary>Fills a band across the whole screen, however wide, from layout line top down to (not including) bottom</summary>
    void BarAcross(int top, int bottom, string color);

    /// <summary>Whether there's a picture of this name</summary>
    bool HasPicture(string name);

    /// <summary>A picture's size, or null if there's no such picture</summary>
    (int Width, int Height)? PictureSize(string name);

    /// <summary>Draws a picture with its top left at (x, y); a missing picture draws nothing</summary>
    void DrawPicture(string name, int x, int y);
}
