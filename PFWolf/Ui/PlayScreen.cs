using PFWolf.Assets;
using PFWolf.Extensions;

namespace PFWolf.Ui;

/// <summary>What drawing the play screen needs from the game</summary>
/// <param name="PresenterMessageBox">Shows a presenter.yaml message box script (Ui/TextPresenter.cs)</param>
internal sealed record PlayScreenHooks(
    Action<string> PresenterMessageBox);

/// <summary>
/// The screen around the view: the status bar with everything on it (DrawPlayScreen), the view's
/// border (DrawPlayBorder, DrawPlayBorderSides, DrawStatusBorder), the level's name as the player
/// sees it (GetMapDisplayName), and the "get psyched" screen a level starts behind (PreloadGraphics), or a
/// game's level start message in its place (ShowLevelStartMessage).
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
    PlayArea playArea,
    Configuration.GameSettings settings,
    GameSession session,
    PlayScreenHooks hooks)
{
    internal void DrawPlayScreen()
    {
        if (statusBar.StatusBar.Get("background") is { Pic: { Length: > 0 } statusbarpic, Overlay: false } background)
        {
            statusBar.FillBarSides(background, atTop: false, playArea.StatusLines);
            using var _ = video.UseUiOrigin(UiAnchor.Bottom);
            graphics.DrawPic(statusbarpic, 0, 200 - playArea.StatusLines);
        }
        statusBar.DrawTopBar();
        DrawPlayBorder();
        DrawStatusParts();
    }

    // Everything on the status bar but its picture
    void DrawStatusParts()
    {
        statusBar.DrawIcons();
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
        statusBar.DrawCounts();
        statusBar.DrawWeaponName();
        weapons.DrawWeaponSlots();
        statusBar.DrawPowerMeter();
        statusBar.DrawHeartMonitor(force: true);
        statusBar.DrawRadarGauge();
        if (statusBar.StatusBar.Get("radar") is { } radar && !(settings.viewsize == 21 && session.ingame))
            radarDrawing.DrawRadar(radar);
        statusBar.DrawInfoArea(force: true);
    }

    /// <summary>
    /// A status bar drawn over the view (statusbar.yaml background overlay): its picture, if it
    /// has one, and every part, over the frame just drawn. Nothing for a bar of its own, or with
    /// the view full screen.
    /// </summary>
    internal void DrawStatusOverlay()
    {
        if (!statusBar.IsOverlay || !session.ingame || settings.viewsize == 21)
            return;
        statusBar.DrawingOverlay = true;
        try
        {
            if (statusBar.StatusBarHidden)
                return;
            if (statusBar.StatusBar.Get("background") is { Pic: { Length: > 0 } pic, Height: { } height })
            {
                using var _ = video.UseUiOrigin(UiAnchor.Bottom);
                graphics.DrawPic(pic, 0, 200 - height);
            }
            DrawStatusParts();
        }
        finally
        {
            statusBar.DrawingOverlay = false;
        }
    }

    /// <summary>The thickness of the view border's lines: a layout pixel's worth of screen pixels, at least one</summary>
    internal int BorderLinePixels => Math.Max(1, video.ToScreenLength(1));

    internal void DrawPlayBorder()
    {
        int px = BorderLinePixels;

        if (session.bordercol != "VIEWCOLOR")
            DrawStatusBorder(session.bordercol);
        else
        {
            // Beside the status bar: from each screen edge to the border's sides into the picture.
            // A bar with no border (Blake Stone's) keeps its side-color there instead.
            int sides = statusBar.StatusBar.Get("border") is { } border ? video.ScreenX(border.Sides) : 0;
            int top = video.ScreenYAboveBottom(playArea.StatusLines);
            if (sides > 0 && playArea.StatusLines > 0)
            {
                video.BarScaledCoord(0, top, sides, video.screenHeight - top, session.bordercol);
                video.BarScaledCoord(video.screenWidth - sides, top, sides, video.screenHeight - top, session.bordercol);
            }
        }

        if (render.ViewHeight == video.screenHeight) return;

        video.BarScaledCoord(0, playArea.PlayAreaTop, video.screenWidth, playArea.PlayAreaHeight, session.bordercol);

        int xl = video.screenWidth / 2 - render.ViewWidth / 2;
        int yl = playArea.PlayAreaTop + (playArea.PlayAreaHeight - render.ViewHeight) / 2;
        video.BarScaledCoord(xl, yl, render.ViewWidth, render.ViewHeight, "Black");

        if (xl != 0)
        {
            // Paint game view border lines
            video.BarScaledCoord(xl - px, yl - px, render.ViewWidth + px, px, "Black");                      // upper border
            video.BarScaledCoord(xl, yl + render.ViewHeight, render.ViewWidth + px, px, session.bordercol);// - 2);       // lower border
            video.BarScaledCoord(xl - px, yl - px, px, render.ViewHeight + px, "Black");                     // left border
            video.BarScaledCoord(xl + render.ViewWidth, yl - px, px, render.ViewHeight + 2 * px, session.bordercol);// - 2);  // right border
            video.BarScaledCoord(xl - px, yl + render.ViewHeight, px, px, session.bordercol);// - 3);              // lower left highlight
        }
        else
        {
            // Just paint a lower border line
            video.BarScaledCoord(0, yl + render.ViewHeight, render.ViewWidth, px, session.bordercol);// - 2);       // lower border
        }
    }

    internal void DrawPlayBorderSides()
    {
        if (settings.viewsize == 21) return;

        int sw = video.screenWidth;
        int sh = video.screenHeight;
        int vw = render.ViewWidth;
        int vh = render.ViewHeight;
        int px = BorderLinePixels;

        int top = playArea.PlayAreaTop;                  // below any top status bar
        int h = playArea.PlayAreaHeight;
        int xl = sw / 2 - vw / 2;
        int yl = top + (h - vh) / 2;

        if (xl != 0)
        {
            video.BarScaledCoord(0, top, xl - px, h, session.bordercol);                 // left side
            video.BarScaledCoord(xl + vw + px, top, sw - (xl + vw + px), h, session.bordercol);          // right side, out to the screen edge
        }

        if (yl != top)
        {
            video.BarScaledCoord(0, top, sw, yl - px - top, session.bordercol);                    // upper side
            video.BarScaledCoord(0, yl + vh + px, sw, top + h - (yl + vh + px), session.bordercol);         // lower side, down to the status bar
        }

        if (xl != 0)
        {
            // Paint game view border lines
            video.BarScaledCoord(xl - px, yl - px, vw + px, px, "Black");                      // upper border
            video.BarScaledCoord(xl, yl + vh, vw + px, px, session.bordercol);// - 2);          // lower border
            video.BarScaledCoord(xl - px, yl - px, px, vh + px, "Black");                      // left border
            video.BarScaledCoord(xl + vw, yl - px, px, vh + px * 2, session.bordercol);// - 2);          // right border
            video.BarScaledCoord(xl - px, yl + vh, px, px, session.bordercol);// - 3);          // lower left highlight
        }
        else
        {
            // Just paint a lower border line
            video.BarScaledCoord(0, yl + vh, vw, px, session.bordercol);// - 2);       // lower border
        }
    }

    /// <summary>
    /// Paints the view border and the status bar's own border-colored areas (statusbar.yaml
    /// border rects) in <paramref name="color"/>.
    /// </summary>
    internal void DrawStatusBorder(string color)
    {
        video.BarScaledCoord(0, playArea.PlayAreaTop, video.screenWidth, playArea.PlayAreaHeight, color);

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
            int y = video.ScreenYAboveBottom(playArea.StatusLines - rect[1]);
            video.BarScaledCoord(x, y, width, video.ScreenYAboveBottom(playArea.StatusLines - rect[1] - rect[3]) - y, color);
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
        //      if (LastScan == Escape)
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
            ShowLevelStartMessage(message);
            return;
        }

        statusBar.DrawLevel();

        // the play area, below any top status bar, down to the bottom one's first line
        video.BarScaledCoord(0, playArea.PlayAreaTop, video.screenWidth, playArea.PlayAreaAndStatusLine, session.bordercol);

        // Centered over the screen above the status bar
        int boxX = (video.screenWidth - video.ToScreenLength(224)) / 2;
        int boxY = (video.ScreenYAboveBottom(playArea.StatusLines) - video.ToScreenLength(48)) / 2;
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

    /// <summary>
    /// game-info's level-start-message in presenter.yaml's message box as the level comes up
    /// (Blake Stone's "Get Ready, Blake!"), in place of the "get psyched" picture
    /// </summary>
    internal void ShowLevelStartMessage(string script)
    {
        DrawPlayScreen();       // the new level's bars, then the view blacked out under the message
        video.BarScaledCoord(0, playArea.PlayAreaTop, video.screenWidth, playArea.PlayAreaAndStatusLine, "0");
        using (video.UseUiOriginAboveBottom(playArea.StatusLines))
            hooks.PresenterMessageBox(script.Replace("\\r", "\r").Replace("\n", "\r\n"));
        video.Update();
        if (video.screenfaded)
            video.FadeIn();
        input.UserInput(35);
        video.FadeOut();
        DrawPlayScreen();
        video.Update();
    }
}