using SDL2;
using System.Runtime.InteropServices;
using PFWolf.Constants;

namespace PFWolf.Rendering;

/// <summary>What drawing a frame needs from the game</summary>
/// <param name="LevelFadeStyle">How a level fades in (game-info level-fade)</param>
/// <param name="LevelFadeTics">How long that takes</param>
/// <param name="RestoreBorderAfterConsole">Redraws the border the console covered, once it's shut</param>
/// <param name="DrawAutomap">The automap, when it's open</param>
/// <param name="DrawStatusOverlay">A status bar drawn over the view (statusbar.yaml background overlay)</param>
/// <param name="DrawHudMessages">The HUD messages and info area</param>
/// <param name="DrawNetOverlay">Waiting for someone, or asking whether to leave</param>
/// <param name="DrawConsole">The console, when it's open</param>
/// <param name="ScreenshotPending">Whether the screenshot command asked for a shot</param>
/// <param name="ClearScreenshotPending">The shot's been taken</param>
/// <param name="TakeScreenshot">Saves the screen and says where it went</param>
/// <param name="UpdateViewQuake">Moves any quake on and sets the view's focal length for it and a scope's zoom (Rendering/ViewQuake.cs)</param>
/// <param name="ScopePicture">The scope's picture while the player looks through one (Gameplay/WeaponZoom.cs), else null</param>
/// <param name="DrawScope">Draws it over the screen, in place of the weapon in hand</param>
internal sealed record FrameRefreshHooks(
    Func<FadeStyle> LevelFadeStyle,
    Func<int> LevelFadeTics,
    Action RestoreBorderAfterConsole,
    Action DrawAutomap,
    Action DrawStatusOverlay,
    Action DrawHudMessages,
    Action DrawNetOverlay,
    Action DrawConsole,
    Func<bool> ScreenshotPending,
    Action ClearScreenshotPending,
    Func<string> TakeScreenshot,
    Action UpdateViewQuake,
    Func<string?> ScopePicture,
    Action<string> DrawScope);

/// <summary>
/// One frame of the 3D view: the tics since the last (CalcTics), the view from the camera
/// (Setup3DView, with its pitch), then the lights, the floor and ceiling, the walls, the sprites
/// and the weapon in hand, and over them the automap, messages, fps counter and console
/// (ThreeDRefresh).
/// </summary>
internal sealed class FrameRefresh(
    VideoManager video,
    GraphicManager graphics,
    MapManager map,
    ConsoleManager console,
    Lighting lighting,
    ActorLights actorLights,
    Weather weather,
    SkyAndFlats skyAndFlats,
    RayTrace rayTrace,
    SpriteDrawing spriteDrawing,
    PlayerView playerView,
    RenderState render,
    GameSession session,
    FrameRefreshHooks hooks)
{
    internal int lasttimecount;
    internal int frameon;
    internal bool fpscounter = false;

    internal int fps_frames = 0, fps_time = 0, fps = 0;

    /*
=====================
=
= CalcTics
=
=====================
*/

    internal void CalcTics()
    {
        //
        // calculate tics since last refresh for adaptive timing
        //
        if (lasttimecount > (int)GameEngineManager.GetTimeCount())
            lasttimecount = (int)GameEngineManager.GetTimeCount();    // if the game was paused a LONG time

        uint curtime = SDL.SDL_GetTicks();
        session.tics = (uint)((curtime * 7) / 100 - lasttimecount); // TODO: Rounding?
        if (session.tics == 0)
        {
            // wait until end of current tic
            GameEngineManager.DelayMs((uint)(((lasttimecount + 1) * 100) / 7 - curtime)); // TODO: Rounding error?
            session.tics = 1;
        }

        lasttimecount += (int)session.tics;

        if (session.tics > GameConstants.MaxTics)
            session.tics = GameConstants.MaxTics;
    }

    /*
    ====================
    =
    = Pitch (y-shearing)
    =
    = Looking up or down doesn't tilt the view: it moves the horizon (centery) down or up the
    = screen, by as many pixels as the pitch's tangent at the projection's focal length (scale,
    = in pixels). Walls stay upright, so everything draws as before, only measured from the
    = moved horizon. It's kept on the screen, which limits how far the view can look.
    =
    ====================
    */

    /// <summary>The steepest pitch, in degrees, that keeps the horizon a row inside the view</summary>
    internal double MaxPitch() =>
        render.scale > 0 ? Math.Atan((render.basecentery - 1) / (double)render.scale) * 180 / Math.PI : 0;

    internal void SetupPitch()
    {
        double max = MaxPitch();
        render.viewpitch = Math.Clamp(render.viewpitch, -max, max);
        int offset = (int)Math.Round(render.scale * Math.Tan(render.viewpitch * Math.PI / 180));
        offset = Math.Clamp(offset, 1 - render.basecentery, render.basecentery - 1);
        render.centery = (short)(render.basecentery + offset);
    }

    internal void Setup3DView()
    {
        var camera = session.camera;
        render.viewangle = camera.Angle;
        render.midangle = (short)(render.viewangle * (GameConstants.FineAngles / GameConstants.Angles));

        render.viewpitch = camera.Pitch;
        render.vieweyez = Math.Clamp(camera.EyeZ, GameConstants.MinEye, GameConstants.MaxEye);
        SetupPitch();

        render.viewsin = TrigTables.sintable[render.viewangle];
        render.viewcos = TrigTables.costable[render.viewangle];

        render.viewx = camera.X - MathUtils.FixedMul(render.focallength, render.viewcos);
        render.viewy = camera.Y + MathUtils.FixedMul(render.focallength, render.viewsin);

        render.focaltx = (short)(render.viewx >> (int)MapConstants.TileShift);
        render.focalty = (short)(render.viewy >> (int)MapConstants.TileShift);

        render.xpartialdown = (uint)(render.viewx & (MapConstants.TileGlobal - 1));
        render.xpartialup = (uint)(render.xpartialdown ^ (MapConstants.TileGlobal - 1));
        render.ypartialdown = (uint)(render.viewy & (MapConstants.TileGlobal - 1));
        render.ypartialup = (uint)(render.ypartialdown ^ (MapConstants.TileGlobal - 1));
    }

    internal void ThreeDRefresh()
    {
        var camera = session.camera;

        // Create a Span<T> from the multidimensional array's data reference
        Span<bool> data = MemoryMarshal.CreateSpan(
            ref map.spotvis[0, 0], // Reference to the first element
            map.spotvis.Length     // Total number of elements
        );

        // Fill the span with the value
        data.Fill(false);

        // Detect all sprites over camera fix. Demos need it too: Wolf4SDL 1.7c, which plays
        // the games' own demos through, always had it (enemies beside the player stay seen)
        if (map.tilemap[camera.TileX, camera.TileY] == 0 ||
         (map.tilemap[camera.TileX, camera.TileY] & GameConstants.BitDoor) != 0)
            map.spotvis[camera.TileX, camera.TileY] = true;


        render.vbuf = 0;
        render.vbufPtr = video.LockSurface();
        if (render.vbufPtr == IntPtr.Zero) return;

        render.vbuf += (int)render.screenofs;

        hooks.UpdateViewQuake();        // the focal length a quake pulses, before the view's set up from it
        Setup3DView();
        lighting.UpdateLightGrid();
        actorLights.CastActorLights();
        map.AnimateFlats(session.gamestate.TimeCount);     // animated floors and ceilings, on the level's clock as walls are

        //
        // follow the walls from there to the right, drawing as we go
        //
        skyAndFlats.VGAClearScreen();

        render.markseen = camera.OnPlayer;     // the automap only learns what the player sees
        weather.SetupWeather();
        rayTrace.WallRefresh();

        if (render.markseen)
            map.MarkSeenFromSpotvis();      // floor the rays crossed, for the automap

        //
        // draw all the scaled images
        //
        spriteDrawing.DrawScaleds();                  // draw scaled stuff
        playerView.PlayerSight();                  // what the player sees, when the camera is elsewhere

        // Looking through a scope, its picture takes the weapon in hand's place
        var scope = hooks.ScopePicture();
        if (scope == null)
            spriteDrawing.DrawPlayerWeapon();    // draw player's hands

        video.UnlockSurface();
        render.vbuf = 0;

        if (scope != null)
            hooks.DrawScope(scope);

        // For the next save's thumbnail, before the automap or console is drawn over the view
        video.KeepViewCopy(render.viewscreenx, render.viewscreeny, render.viewwidth, render.viewheight);

        //
        // show screen and time last cycle
        //

        if (session.fizzlein)
        {
            video.Transition(hooks.LevelFadeStyle(), 0, 0, video.screenWidth, video.screenHeight, (uint)hooks.LevelFadeTics());
            session.fizzlein = false;

            lasttimecount = (int)GameEngineManager.GetTimeCount();          // don't make a big tic count
        }
        else
        {
            hooks.RestoreBorderAfterConsole();

            hooks.DrawAutomap();

            hooks.DrawStatusOverlay();    // over the view and the automap, not in the save thumbnail
            hooks.DrawHudMessages();      // over the automap too, but not in the save thumbnail
            hooks.DrawNetOverlay();       // waiting for someone, or asking whether to leave (Ui/NetOverlay.cs)

            if (fpscounter)
            {
                video.Bar(0, 0, 40, 10, session.bordercol);
                graphics.DrawText(4, 1, $"{fps} fps", new Fonts.TextStyle(UiFonts.SmallFont, "Grey", "VIEWCOLOR"));
            }

            // Taken here, before the console is drawn, so it never shows in the shot.
            if (hooks.ScreenshotPending())
            {
                hooks.ClearScreenshotPending();
                console.Print(hooks.TakeScreenshot());
            }

            hooks.DrawConsole();
            video.Update();
        }

        if (fpscounter)
        {
            fps_frames++;
            fps_time += (int)session.tics;

            if (fps_time > 35)
            {
                fps_time -= 35;
                fps = fps_frames << 1;
                fps_frames = 0;
            }
        }
    }
}
