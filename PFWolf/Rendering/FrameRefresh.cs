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
    internal int LastTimeCount;
    internal int FrameOn;
    internal bool FpsCounter = false;

    internal int FpsFrames = 0, FpsTime = 0, Fps = 0;

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
        if (LastTimeCount > (int)GameEngineManager.GetTimeCount())
            LastTimeCount = (int)GameEngineManager.GetTimeCount();    // if the game was paused a LONG time

        uint curtime = SDL.SDL_GetTicks();
        session.Tics = (uint)((curtime * 7) / 100 - LastTimeCount); // TODO: Rounding?
        if (session.Tics == 0)
        {
            // wait until end of current tic
            GameEngineManager.DelayMs((uint)(((LastTimeCount + 1) * 100) / 7 - curtime)); // TODO: Rounding error?
            session.Tics = 1;
        }

        LastTimeCount += (int)session.Tics;

        if (session.Tics > GameConstants.MaxTics)
            session.Tics = GameConstants.MaxTics;
    }

    /// <summary>How many tics are due now that haven't been run (what CalcTics would find), without waiting</summary>
    internal int TicsDue() => (int)((SDL.SDL_GetTicks() * 7) / 100) - LastTimeCount;

    // The offset from the high-resolution clock to SDL_GetTicks' milliseconds: the largest
    // SDL's whole milliseconds have been ahead of it, which closes in on the true one from below
    double clockOffset = double.NegativeInfinity;

    /// <summary>The time in tics, finer than SDL_GetTicks' whole milliseconds: how far the frame is through a tic</summary>
    internal double NowInTics()
    {
        double ms = System.Diagnostics.Stopwatch.GetTimestamp() * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        clockOffset = Math.Max(clockOffset, SDL.SDL_GetTicks() - ms);
        return (ms + clockOffset) * 7 / 100;
    }

    /*
    ====================
    =
    = Pitch (y-shearing)
    =
    = Looking up or down doesn't tilt the view: it moves the horizon (CenterY) down or up the
    = screen, by as many pixels as the pitch's tangent at the projection's focal length (scale,
    = in pixels). Walls stay upright, so everything draws as before, only measured from the
    = moved horizon. It's kept on the screen, which limits how far the view can look.
    =
    ====================
    */

    /// <summary>The steepest pitch, in degrees, that keeps the horizon a row inside the view</summary>
    internal double MaxPitch() =>
        render.Scale > 0 ? Math.Atan((render.BaseCenterY - 1) / (double)render.Scale) * 180 / Math.PI : 0;

    internal void SetupPitch()
    {
        double max = MaxPitch();
        render.ViewPitch = Math.Clamp(render.ViewPitch, -max, max);
        int offset = (int)Math.Round(render.Scale * Math.Tan(render.ViewPitch * Math.PI / 180));
        offset = Math.Clamp(offset, 1 - render.BaseCenterY, render.BaseCenterY - 1);
        render.CenterY = (short)(render.BaseCenterY + offset);
    }

    /// <summary>Where moving things are drawn between tics (Rendering/FrameInterpolation.cs); unset, where they are</summary>
    internal FrameInterpolation? Interpolation { get; set; }

    internal void Setup3DView()
    {
        var camera = session.Camera;
        var pose = Interpolation?.CameraPose() ?? new ViewPose(camera.X, camera.Y, camera.Angle, camera.Pitch, camera.EyeZ);

        render.ViewPitch = pose.Pitch;
        render.ViewEyeZ = Math.Clamp(pose.EyeZ, GameConstants.MinEye, GameConstants.MaxEye);
        SetupPitch();

        if (pose.Angle == Math.Floor(pose.Angle))
        {
            // a whole degree, as every tic leaves it: the tables, as the original
            render.ViewAngle = (short)pose.Angle;
            render.MidAngle = (short)(render.ViewAngle * (GameConstants.FineAngles / GameConstants.Angles));
            render.ViewSin = TrigTables.SinTable[render.ViewAngle];
            render.ViewCos = TrigTables.CosTable[render.ViewAngle];
        }
        else
        {
            // between tics: the ray trace's fine angles are tenths of a degree, and sin and cos
            // come from the angle itself
            int fine = (int)Math.Round(pose.Angle * (GameConstants.FineAngles / GameConstants.Angles)) % GameConstants.FineAngles;
            render.MidAngle = (short)fine;
            render.ViewAngle = (short)(fine / (GameConstants.FineAngles / GameConstants.Angles));
            double radians = fine * (2 * Math.PI / GameConstants.FineAngles);
            render.ViewSin = (int)Math.Round(Math.Sin(radians) * MapConstants.Global1);
            render.ViewCos = (int)Math.Round(Math.Cos(radians) * MapConstants.Global1);
        }

        render.ViewX = pose.X - MathUtils.FixedMul(render.FocalLength, render.ViewCos);
        render.ViewY = pose.Y + MathUtils.FixedMul(render.FocalLength, render.ViewSin);

        render.FocalTx = (short)(render.ViewX >> (int)MapConstants.TileShift);
        render.FocalTy = (short)(render.ViewY >> (int)MapConstants.TileShift);

        render.XPartialDown = (uint)(render.ViewX & (MapConstants.TileGlobal - 1));
        render.XPartialUp = (uint)(render.XPartialDown ^ (MapConstants.TileGlobal - 1));
        render.YPartialDown = (uint)(render.ViewY & (MapConstants.TileGlobal - 1));
        render.YPartialUp = (uint)(render.YPartialDown ^ (MapConstants.TileGlobal - 1));
    }

    internal void ThreeDRefresh()
    {
        var camera = session.Camera;

        // Create a Span<T> from the multidimensional array's data reference
        Span<bool> data = MemoryMarshal.CreateSpan(
            ref map.Spotvis[0, 0], // Reference to the first element
            map.Spotvis.Length     // Total number of elements
        );

        // Fill the span with the value
        data.Fill(false);

        // Detect all sprites over camera fix. Demos need it too: Wolf4SDL 1.7c, which plays
        // the games' own demos through, always had it (enemies beside the player stay seen)
        if (map.TileMap[camera.TileX, camera.TileY] == 0 ||
         (map.TileMap[camera.TileX, camera.TileY] & GameConstants.BitDoor) != 0)
            map.Spotvis[camera.TileX, camera.TileY] = true;


        render.Vbuf = 0;
        render.VbufPtr = video.LockSurface();
        if (render.VbufPtr == IntPtr.Zero) return;

        render.Vbuf += (int)render.ScreenOfs;

        hooks.UpdateViewQuake();        // the focal length a quake pulses, before the view's set up from it
        Setup3DView();
        lighting.UpdateLightGrid();
        actorLights.CastActorLights();
        map.AnimateFlats(session.GameState.TimeCount);     // animated floors and ceilings, on the level's clock as walls are
        FrameProfiler.Mark(FrameProfiler.Section.Lights);

        //
        // follow the walls from there to the right, drawing as we go
        //
        skyAndFlats.VGAClearScreen();
        FrameProfiler.Mark(FrameProfiler.Section.Flats);

        render.MarkSeen = camera.OnPlayer;     // the automap only learns what the player sees
        weather.SetupWeather();
        rayTrace.WallRefresh();

        if (render.MarkSeen)
            map.MarkSeenFromSpotvis();      // floor the rays crossed, for the automap
        FrameProfiler.Mark(FrameProfiler.Section.Walls);

        //
        // draw all the scaled images
        //
        spriteDrawing.DrawScaleds();                  // draw scaled stuff
        playerView.PlayerSight();                  // what the player sees, when the camera is elsewhere
        FrameProfiler.Mark(FrameProfiler.Section.Sprites);

        // Looking through a scope, its picture takes the weapon in hand's place
        var scope = hooks.ScopePicture();
        if (scope == null)
            spriteDrawing.DrawPlayerWeapon();    // draw player's hands

        video.UnlockSurface();
        render.Vbuf = 0;

        if (scope != null)
            hooks.DrawScope(scope);
        FrameProfiler.Mark(FrameProfiler.Section.Weapon);

        // For the next save's thumbnail, before the automap or console is drawn over the view
        video.KeepViewCopy(render.ViewScreenX, render.ViewScreenY, render.ViewWidth, render.ViewHeight);
        FrameProfiler.Mark(FrameProfiler.Section.ViewCopy);

        //
        // show screen and time last cycle
        //

        if (session.FizzleIn)
        {
            video.Transition(hooks.LevelFadeStyle(), 0, 0, video.ScreenWidth, video.ScreenHeight, (uint)hooks.LevelFadeTics());
            session.FizzleIn = false;

            LastTimeCount = (int)GameEngineManager.GetTimeCount();          // don't make a big tic count
        }
        else
        {
            hooks.RestoreBorderAfterConsole();

            hooks.DrawAutomap();

            hooks.DrawStatusOverlay();    // over the view and the automap, not in the save thumbnail
            hooks.DrawHudMessages();      // over the automap too, but not in the save thumbnail
            hooks.DrawNetOverlay();       // waiting for someone, or asking whether to leave (Ui/NetOverlay.cs)

            if (FpsCounter)
            {
                video.Bar(0, 0, 40, 10, session.BorderCol);
                graphics.DrawText(4, 1, $"{Fps} fps", new Fonts.TextStyle(UiFonts.SmallFont, "Grey", "VIEWCOLOR"));
            }

            // Taken here, before the console is drawn, so it never shows in the shot.
            if (hooks.ScreenshotPending())
            {
                hooks.ClearScreenshotPending();
                console.Print(hooks.TakeScreenshot());
            }

            hooks.DrawConsole();
            FrameProfiler.Mark(FrameProfiler.Section.Overlays);
            video.Update();
            FrameProfiler.Mark(FrameProfiler.Section.Present);
        }

        if (FpsCounter)
        {
            FpsFrames++;
            FpsTime += (int)session.Tics;

            if (FpsTime > 35)
            {
                FpsTime -= 35;
                Fps = FpsFrames << 1;
                FpsFrames = 0;
            }
        }
    }
}
