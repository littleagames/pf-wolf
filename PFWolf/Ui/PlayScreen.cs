using PFWolf.Assets;
using PFWolf.Extensions;

namespace PFWolf.Ui;

/// <summary>What drawing the play screen needs from the game</summary>
/// <param name="ViewSize">The view's size setting (21: the full screen, no border)</param>
/// <param name="InGame">Whether a level is being played (a full screen view hides the radar then)</param>
/// <param name="BorderColor">The view border's color (Change View sets another)</param>
/// <param name="ShowLevelStartMessage">A game's level start message in place of "get psyched" (Blake Stone)</param>
internal sealed record PlayScreenHooks(
    Func<int> ViewSize,
    Func<bool> InGame,
    Func<string> BorderColor,
    Action<string> ShowLevelStartMessage);

/// <summary>
/// The screen around the view: the status bar with everything on it (DrawPlayScreen), the view's
/// border (DrawPlayBorder, DrawPlayBorderSides, DrawStatusBorder), the level's name as the player
/// sees it (GetMapDisplayName), and the "get psyched" screen a level starts behind (PreloadGraphics).
/// </summary>
internal sealed class PlayScreen(
    AssetManager assets,
    GameEngineManager engine,
    VideoManager video,
    GraphicManager graphics,
    InputManager input,
    StatusBarDrawing statusBar,
    PlayerWeapons weapons,
    Radar radarDrawing,
    RenderState render,
    PlayScreenHooks hooks)
{
    internal void DrawPlayScreen()
    {
        if (statusBar.StatusBar.Get("background") is { Pic: { Length: > 0 } statusbarpic } background)
        {
            statusBar.FillBarSides(background, atTop: false, Program.STATUSLINES);
            using var _ = video.UseUiOrigin(UiAnchor.Bottom);
            graphics.DrawPic(statusbarpic, 0, 200 - Program.STATUSLINES);
        }
        statusBar.DrawTopBar();
        DrawPlayBorder();

        statusBar.DrawFace();
        statusBar.DrawHealth();
        statusBar.DrawArmor();
        statusBar.DrawLives();
        statusBar.DrawLevel();
        weapons.DrawWeapon();
        weapons.DrawAmmo();
        weapons.DrawCharge();
        statusBar.DrawKeys();
        statusBar.DrawScore();
        statusBar.DrawLocation();
        statusBar.DrawHeartMonitor(force: true);
        statusBar.DrawRadarGauge();
        if (statusBar.StatusBar.Get("radar") is { } radar && !(hooks.ViewSize() == 21 && hooks.InGame()))
            radarDrawing.DrawRadar(radar);
        statusBar.DrawInfoArea(force: true);
    }

    /// <summary>The thickness of the view border's lines: a layout pixel's worth of screen pixels, at least one</summary>
    internal int BorderLinePixels => Math.Max(1, video.ToScreenLength(1));

    internal void DrawPlayBorder()
    {
        int px = BorderLinePixels;

        if (hooks.BorderColor() != "VIEWCOLOR")
            DrawStatusBorder(hooks.BorderColor());
        else
        {
            // Beside the status bar: from each screen edge to the border's sides into the picture.
            // A bar with no border (Blake Stone's) keeps its side-color there instead.
            int sides = statusBar.StatusBar.Get("border") is { } border ? video.ScreenX(border.Sides) : 0;
            int top = video.ScreenYAboveBottom(Program.STATUSLINES);
            if (sides > 0 && Program.STATUSLINES > 0)
            {
                video.BarScaledCoord(0, top, sides, video.screenHeight - top, hooks.BorderColor());
                video.BarScaledCoord(video.screenWidth - sides, top, sides, video.screenHeight - top, hooks.BorderColor());
            }
        }

        if (render.viewheight == video.screenHeight) return;

        video.BarScaledCoord(0, Program.PlayAreaTop, video.screenWidth, Program.PlayAreaHeight, hooks.BorderColor());

        int xl = video.screenWidth / 2 - render.viewwidth / 2;
        int yl = Program.PlayAreaTop + (Program.PlayAreaHeight - render.viewheight) / 2;
        video.BarScaledCoord(xl, yl, render.viewwidth, render.viewheight, "Black");

        if (xl != 0)
        {
            // Paint game view border lines
            video.BarScaledCoord(xl - px, yl - px, render.viewwidth + px, px, "Black");                      // upper border
            video.BarScaledCoord(xl, yl + render.viewheight, render.viewwidth + px, px, hooks.BorderColor());// - 2);       // lower border
            video.BarScaledCoord(xl - px, yl - px, px, render.viewheight + px, "Black");                     // left border
            video.BarScaledCoord(xl + render.viewwidth, yl - px, px, render.viewheight + 2 * px, hooks.BorderColor());// - 2);  // right border
            video.BarScaledCoord(xl - px, yl + render.viewheight, px, px, hooks.BorderColor());// - 3);              // lower left highlight
        }
        else
        {
            // Just paint a lower border line
            video.BarScaledCoord(0, yl + render.viewheight, render.viewwidth, px, hooks.BorderColor());// - 2);       // lower border
        }
    }

    internal void DrawPlayBorderSides()
    {
        if (hooks.ViewSize() == 21) return;

        int sw = video.screenWidth;
        int sh = video.screenHeight;
        int vw = render.viewwidth;
        int vh = render.viewheight;
        int px = BorderLinePixels;

        int top = Program.PlayAreaTop;                  // below any top status bar
        int h = Program.PlayAreaHeight;
        int xl = sw / 2 - vw / 2;
        int yl = top + (h - vh) / 2;

        if (xl != 0)
        {
            video.BarScaledCoord(0, top, xl - px, h, hooks.BorderColor());                 // left side
            video.BarScaledCoord(xl + vw + px, top, sw - (xl + vw + px), h, hooks.BorderColor());          // right side, out to the screen edge
        }

        if (yl != top)
        {
            video.BarScaledCoord(0, top, sw, yl - px - top, hooks.BorderColor());                    // upper side
            video.BarScaledCoord(0, yl + vh + px, sw, top + h - (yl + vh + px), hooks.BorderColor());         // lower side, down to the status bar
        }

        if (xl != 0)
        {
            // Paint game view border lines
            video.BarScaledCoord(xl - px, yl - px, vw + px, px, "Black");                      // upper border
            video.BarScaledCoord(xl, yl + vh, vw + px, px, hooks.BorderColor());// - 2);          // lower border
            video.BarScaledCoord(xl - px, yl - px, px, vh + px, "Black");                      // left border
            video.BarScaledCoord(xl + vw, yl - px, px, vh + px * 2, hooks.BorderColor());// - 2);          // right border
            video.BarScaledCoord(xl - px, yl + vh, px, px, hooks.BorderColor());// - 3);          // lower left highlight
        }
        else
        {
            // Just paint a lower border line
            video.BarScaledCoord(0, yl + vh, vw, px, hooks.BorderColor());// - 2);       // lower border
        }
    }

    /// <summary>
    /// Paints the view border and the status bar's own border-colored areas (statusbar.yaml
    /// border rects) in <paramref name="color"/>.
    /// </summary>
    internal void DrawStatusBorder(string color)
    {
        video.BarScaledCoord(0, Program.PlayAreaTop, video.screenWidth, Program.PlayAreaHeight, color);

        foreach (var rect in statusBar.StatusBar.Get("border")?.Rects ?? [])
        {
            if (rect.Count != 4)
                continue;
            int x = video.ScreenX(rect[0]), width = video.ScreenX(rect[0] + rect[2]) - x;
            if (rect[0] <= 0)
            {
                width += x;             // out to the left edge
                x = 0;
            }
            if (rect[0] + rect[2] >= 320)
                width = video.screenWidth - x;  // out to the right edge
            // Down from the status bar's top, which sits on the screen's bottom
            int y = video.ScreenYAboveBottom(Program.STATUSLINES - rect[1]);
            video.BarScaledCoord(x, y, width, video.ScreenYAboveBottom(Program.STATUSLINES - rect[1] - rect[3]) - y, color);
        }
    }

    /// <summary>
    /// The level's name for showing to the player: game-info's name for it, else
    /// "Episode X, Floor Y" ("Floor Y" when there's one playable episode), else the map's lump name.
    /// </summary>
    internal string GetMapDisplayName(string mapon)
    {
        var gameInfo = engine.GetGameInfo();
        if (!gameInfo.Maps.TryGetValue(mapon, out var mapInfo))
            return mapon;

        var language = assets.GetText("en-us");
        if (!string.IsNullOrEmpty(mapInfo.Name))
            return mapInfo.Name.ToLanguageText(language);
        if (mapInfo.FloorNumber <= 0)
            return mapon;

        return gameInfo.Episodes.Values.Count(episode => !episode.Locked) > 1
            ? string.Format("$STR_MAPEPISODEFLOOR".ToLanguageText(language), mapInfo.Cluster, mapInfo.FloorNumber)
            : string.Format("$STR_MAPFLOOR".ToLanguageText(language), mapInfo.FloorNumber);
    }

    // GET PSYCHED: the screen a level starts behind

    /// <summary>
    /// The "get psyched" progress bar, along the bottom of a box drawn at the UI scale from a
    /// screen position; its width and height are in layout pixels
    /// </summary>
    internal bool PreloadUpdate(uint current, uint total, int boxX, int boxY, int boxW, int boxH)
    {
        // Edges at the UI scale from the box's corner, as the picture's are
        int L(int n) => video.ToScreenLength(n);
        int x = boxX + L(5);
        int y = boxY + L(boxH - 3);
        int h = L(boxH - 1) - L(boxH - 3), line = L(boxH - 2) - L(boxH - 3);
        uint w = (uint)(L(boxW - 5) - L(5));

        video.BarScaledCoord(x, y, (int)w, h, "Black");
        w = (uint)((int)w * current / total);
        if (w != 0)
        {
            video.BarScaledCoord(x, y, (int)w, h, "SECONDCOLOR");       //SECONDCOLOR 0x37);
            video.BarScaledCoord(x, y, (int)w - L(1), line, "FIRSTCOLOR"); // 0x32

        }
        video.Update();
        //      if (LastScan == sc_Escape)
        //      {
        //              input.ClearKeysDown();
        //              return(true);
        //      }
        //      else
        return (false);
    }

    internal void PreloadGraphics()
    {
        // A game with a level start message (Blake Stone) shows it instead of "get psyched"
        if (engine.GetGameInfo().LevelStartMessage is { Length: > 0 } message)
        {
            hooks.ShowLevelStartMessage(message);
            return;
        }

        statusBar.DrawLevel();

        // the play area, below any top status bar, down to the bottom one's first line
        video.BarScaledCoord(0, Program.PlayAreaTop, video.screenWidth, Program.PlayAreaAndStatusLine, hooks.BorderColor());

        // Centered over the screen above the status bar
        int boxX = (video.screenWidth - video.ToScreenLength(224)) / 2;
        int boxY = (video.ScreenYAboveBottom(Program.STATUSLINES) - video.ToScreenLength(48)) / 2;
        if (assets.Exists<GraphicAsset>("getpsyched"))     // a standalone game may have none
            graphics.DrawPicScaledCoord("getpsyched", boxX, boxY);

        video.Update();
        video.FadeIn();

        //      PM_Preload (PreloadUpdate);
        PreloadUpdate(10, 10, boxX, boxY, 28 * 8, 48);
        input.UserInput(70);
        video.FadeOut();

        DrawPlayBorder();
        video.Update();
    }
}
