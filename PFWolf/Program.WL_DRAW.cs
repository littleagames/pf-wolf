using SDL2;
using System.Runtime.InteropServices;
using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities.Actors;
using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf;

internal partial class Program
{
    /*
    =============================================================================

                                   LOCAL CONSTANTS

    =============================================================================
    */

    internal const int ACTORSIZE = 0x4000;

    /*
    =============================================================================

                                  GLOBAL VARIABLES

    =============================================================================
    */

    static int vbuf { get => render.vbuf; set => render.vbuf = value; }   // pointer index
    static IntPtr vbufPtr { get => render.vbufPtr; set => render.vbufPtr = value; }


    static int lasttimecount;
    static int frameon;
    static bool fpscounter = false;

    static int fps_frames = 0, fps_time = 0, fps = 0;

#if USE_FLOORCEILINGTEXT || USE_CLOUDSKY
    short[] spanstart;
#endif

    internal static short[] wallheight { get => render.wallheight; set => render.wallheight = value; }

    //
    // math tables
    //
    internal static short[] pixelangle { get => render.pixelangle; set => render.pixelangle = value; }
    internal static int[] finetangent = new int[FINEANGLES / 4];
    internal static int[] sintable = new int[ANGLES + ANGLES / 4];
    internal static int[] costable = new int[ANGLES];   // sintable from ANGLES/4 on (filled with it in BuildTables)

    //
    // refresh variables
    internal static int viewx { get => render.viewx; set => render.viewx = value; }
    internal static int viewy { get => render.viewy; set => render.viewy = value; }
    internal static short viewangle { get => render.viewangle; set => render.viewangle = value; }
    internal static int viewsin { get => render.viewsin; set => render.viewsin = value; }
    internal static int viewcos { get => render.viewcos; set => render.viewcos = value; }

    internal static int postx { get => render.postx; set => render.postx = value; }
    static readonly TextureAsset notexture = new() { RawData = new byte[TEXTURESIZE * TEXTURESIZE] };

    // The renderer's shared per-frame state (Rendering/RenderState.cs): the fields below, under their old names, are its
    internal static readonly RenderState render = new(notexture, Lighting.noshade);

    internal static TextureAsset postsource { get => render.postsource; set => render.postsource = value; }   // the texture of the post ScalePost draws
    internal static int postofs { get => render.postofs; set => render.postofs = value; }   // its column, times 64 (as the texture's across a tile)
    static TextureAsset? uppersource { get => render.uppersource; set => render.uppersource = value; }   // stories above the first, if not postsource (door lintels)
    static int upperofs { get => render.upperofs; set => render.upperofs = value; }

    //
    // tall walls: every wall is wallstories 64 unit stories tall, built up from the floor
    //
    internal const int MAXWALLSTORIES = 8;
    internal static int wallstories { get => render.wallstories; set => render.wallstories = value; }

    //
    // ray tracing variables
    //
    internal static short focaltx { get => render.focaltx; set => render.focaltx = value; }
    internal static short focalty { get => render.focalty; set => render.focalty = value; }
    internal static uint xpartialup { get => render.xpartialup; set => render.xpartialup = value; }
    internal static uint xpartialdown { get => render.xpartialdown; set => render.xpartialdown = value; }
    internal static uint ypartialup { get => render.ypartialup; set => render.ypartialup = value; }
    internal static uint ypartialdown { get => render.ypartialdown; set => render.ypartialdown = value; }

    internal static short midangle { get => render.midangle; set => render.midangle = value; }

    internal static ushort tilehit { get => render.tilehit; set => render.tilehit = value; }
    internal static int pixx { get => render.pixx; set => render.pixx = value; }

    internal static short xtile { get => render.xtile; set => render.xtile = value; }
    internal static short ytile { get => render.ytile; set => render.ytile = value; }
    internal static short xtilestep { get => render.xtilestep; set => render.xtilestep = value; }
    internal static short ytilestep { get => render.ytilestep; set => render.ytilestep = value; }
    internal static int xintercept { get => render.xintercept; set => render.xintercept = value; }
    internal static int yintercept { get => render.yintercept; set => render.yintercept = value; }
    internal static int xinttile { get => render.xinttile; set => render.xinttile = value; }
    internal static int yinttile { get => render.yinttile; set => render.yinttile = value; }
    internal static ushort texdelta { get => render.texdelta; set => render.texdelta = value; }


    /*
=====================
=
= CalcTics
=
=====================
*/

    internal static void CalcTics()
    {
        //
        // calculate tics since last refresh for adaptive timing
        //
        if (lasttimecount > (int)GameEngineManager.GetTimeCount())
            lasttimecount = (int)GameEngineManager.GetTimeCount();    // if the game was paused a LONG time

        uint curtime = SDL.SDL_GetTicks();
        tics = (uint)((curtime * 7) / 100 - lasttimecount); // TODO: Rounding?
        if (tics == 0)
        {
            // wait until end of current tic
            GameEngineManager.DelayMs((uint)(((lasttimecount + 1) * 100) / 7 - curtime)); // TODO: Rounding error?
            tics = 1;
        }

        lasttimecount += (int)tics;

        if (tics > MAXTICS)
            tics = MAXTICS;
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

    internal static double viewpitch { get => render.viewpitch; set => render.viewpitch = value; }   // degrees, up positive; this frame's, from the camera

    /// <summary>The steepest pitch, in degrees, that keeps the horizon a row inside the view</summary>
    internal static double MaxPitch() =>
        scale > 0 ? Math.Atan((basecentery - 1) / (double)scale) * 180 / Math.PI : 0;

    static void SetupPitch()
    {
        double max = MaxPitch();
        viewpitch = Math.Clamp(viewpitch, -max, max);
        int offset = (int)Math.Round(scale * Math.Tan(viewpitch * Math.PI / 180));
        offset = Math.Clamp(offset, 1 - basecentery, basecentery - 1);
        centery = (short)(basecentery + offset);
    }

    /*
    ====================
    =
    = Eye height
    =
    = How high above the floor the view is, in texels (64 a story). Vanilla's eye is half a
    = story up. It stays under one story: wall tops are never drawn, so a view over a one
    = story wall would show the floor behind it through it.
    =
    ====================
    */

    internal const int EYEDEFAULT = TEXTURESIZE / 2, MINEYE = 4, MAXEYE = TEXTURESIZE - 4;

    internal static int vieweyez { get => render.vieweyez; set => render.vieweyez = value; }   // this frame's, from the camera

    static bool markseen { get => render.markseen; set => render.markseen = value; }   // whether this frame's walls and floor go on the automap: only the player's view

    /// <summary>How many half stories a floor or ceiling z texels up is from the eye</summary>
    static double EyeHalves(int z) => skyAndFlats.EyeHalves(z);

    internal static void Setup3DView()
    {
        viewangle = camera.Angle;
        midangle = (short)(viewangle * (FINEANGLES / ANGLES));

        viewpitch = camera.Pitch;
        vieweyez = Math.Clamp(camera.EyeZ, MINEYE, MAXEYE);
        SetupPitch();

        viewsin = sintable[viewangle];
        viewcos = costable[viewangle];

        viewx = camera.X - MathUtils.FixedMul(focallength, viewcos);
        viewy = camera.Y + MathUtils.FixedMul(focallength, viewsin);

        focaltx = (short)(viewx >> (int)MapConstants.TILESHIFT);
        focalty = (short)(viewy >> (int)MapConstants.TILESHIFT);

        xpartialdown = (uint)(viewx & (MapConstants.TILEGLOBAL - 1));
        xpartialup = (uint)(xpartialdown ^ (MapConstants.TILEGLOBAL - 1));
        ypartialdown = (uint)(viewy & (MapConstants.TILEGLOBAL - 1));
        ypartialup = (uint)(ypartialdown ^ (MapConstants.TILEGLOBAL - 1));
    }

    //==========================================================================

    /*
    ===================
    =
    = ScalePost
    =
    ===================
    */
    //
    // A column's trace can go on past a wall, when a taller one behind might show over it. So
    // rather than drawing what it hits straight away, it queues a post for each wall (and door
    // lintel) nearest first, and the queue is drawn farthest first: in one screen column, that
    // puts everything in front of what's behind it.
    //
    const int MAXPOSTS = RenderState.MAXPOSTS;
    static int postcount { get => render.postcount; set => render.postcount = value; }
    static short[] postheights => render.postheights;
    static byte[] postfirststory => render.postfirststory;
    static byte[] poststories => render.poststories;
    static TextureAsset[] postlower => render.postlower;
    static int[] postlowerofs => render.postlowerofs;
    static TextureAsset[] postupper => render.postupper;
    static int[] postupperofs => render.postupperofs;
    static short[] postunderside => render.postunderside;   // an arch's: the height where the trace leaves it, 0 for none
    static TextureAsset?[] postundertexture => render.postundertexture;
    static bool[] postunderflat => render.postunderflat;   // the underside's texture is a ceiling flat
    static byte[][] postlight => render.postlight;   // its face's light row
    static TextureAsset?[] postpatch => render.postpatch;   // a wall patch over it (mapdefs patches)
    static int[] postpatchofs => render.postpatchofs;   // the patch's column: postlowerofs, unless the wall's face is flipped
    static byte[] postpatchstory => render.postpatchstory;   // the patch's story (MapPatchTranslation.Story)

    // The open tile the trace last came from (a TileIndex): walls and doors it hits are seen from
    // there, so take its base light. A wall or door tile keeps the one before it (a wall the
    // trace passes, an open door).
    static int hittile { get => render.hittile; set => render.hittile = value; }

    internal static short postheight { get => render.postheight; set => render.postheight = value; }   // the height (as wallheight) of the post ScalePost queues
    static int hitstories { get => render.hitstories; set => render.hitstories = value; }   // how many stories tall the wall the trace just hit is
    static bool columnhit { get => render.columnhit; set => render.columnhit = value; }   // this column's trace has hit a wall: wallheight is set
    static int columntallest { get => render.columntallest; set => render.columntallest = value; }   // the most stories of any wall it has hit
    static int columntop { get => render.columntop; set => render.columntop = value; }   // the highest screen row any of them reach

    // A column's posts and the arches the trace passes under (Rendering/WallPosts.cs)
    static WallPosts wallPosts = null!;     // made in InitServices

    static WallPosts MakeWallPosts() =>
        new(_videoManager, _mapManager, lighting, skyAndFlats, render, weather, session, WallTexture);

    static void EnterTileLight(int x, int y) => wallPosts.EnterTileLight(x, y);
    static byte[] WallLight(int x, int y) => wallPosts.WallLight(x, y);
    internal static void ScalePost() => wallPosts.ScalePost();
    static void QueuePost(short height, int firststory, int stories, TextureAsset lower, int lowerofs, TextureAsset upper, int upperofs,
        short underside = 0, TextureAsset? undertexture = null, byte[]? light = null, bool underflat = false,
        TextureAsset? patch = null, int patchofs = 0, int patchstory = 0) =>
        wallPosts.QueuePost(height, firststory, stories, lower, lowerofs, upper, upperofs, underside, undertexture, light, underflat, patch, patchofs, patchstory);
    static void DrawPosts() => wallPosts.DrawPosts();
    static bool IsArch(int tilex, int tiley) => wallPosts.IsArch(tilex, tiley);
    static void OpenArch(int tilex, int tiley, int edgex, int edgey, bool vertical) => wallPosts.OpenArch(tilex, tiley, edgex, edgey, vertical);
    static void OpenFocalArch() => wallPosts.OpenFocalArch();
    static void CloseArch(int edgex, int edgey) => wallPosts.CloseArch(edgex, edgey);
    static short HeightAt(int x, int y) => wallPosts.HeightAt(x, y);
    static int VertEdge => wallPosts.VertEdge;
    static int HorizEdge => wallPosts.HorizEdge;
    static bool TracePastWall(int tilex, int tiley) => wallPosts.TracePastWall(tilex, tiley);
    static void SaveTrace() => wallPosts.SaveTrace();
    static void RestoreTrace() => wallPosts.RestoreTrace();
    internal static short CalcHeight() => wallPosts.CalcHeight();
    static int FloorRow(int half) => wallPosts.FloorRow(half);
    static string SeenFace(bool vertical) => wallPosts.SeenFace(vertical);
    static int LintelColumn(int along, bool flip) => WallPosts.LintelColumn(along, flip);

    static bool archopen { get => render.archopen; set => render.archopen = value; }
    static short archheight { get => render.archheight; set => render.archheight = value; }   // where the trace entered it (short.MaxValue: the view is inside)
    static int archstories { get => render.archstories; set => render.archstories = value; }
    static TextureAsset? archtexture { get => render.archtexture; set => render.archtexture = value; }
    static TextureAsset? archunderside { get => render.archunderside; set => render.archunderside = value; }
    static bool archunderflat { get => render.archunderflat; set => render.archunderflat = value; }   // archunderside is the tile's ceiling flat
    static int archofs { get => render.archofs; set => render.archofs = value; }
    static byte[] archlight { get => render.archlight; set => render.archlight = value; }   // its face's light row: where the trace entered it, seen from the tile before

    //
    // the trace's position, kept from before it enters a tile, to carry on past a wall it hits there
    //
    static short savedxtile { get => render.savedxtile; set => render.savedxtile = value; }
    static short savedytile { get => render.savedytile; set => render.savedytile = value; }
    static int savedxintercept { get => render.savedxintercept; set => render.savedxintercept = value; }
    static int savedyintercept { get => render.savedyintercept; set => render.savedyintercept = value; }
    static int savedxinttile { get => render.savedxinttile; set => render.savedxinttile = value; }
    static int savedyinttile { get => render.savedyinttile; set => render.savedyinttile = value; }
    static ushort savedtexdelta { get => render.savedtexdelta; set => render.savedtexdelta = value; }
    static bool pastwall { get => render.pastwall; set => render.pastwall = value; }   // tracing on past the nearest wall: tiles aren't visible


    // What the trace does when it hits a wall, door or diagonal (Rendering/WallHits.cs)
    static WallHits wallHits = null!;       // made in InitServices

    static WallHits MakeWallHits() => new(_assetManager, _mapManager, doors, pushWalls, wallPosts, render, session);

    internal static void HitVertWall() => wallHits.HitVertWall();
    internal static void HitHorizWall() => wallHits.HitHorizWall();
    internal static void HitVertDoor() => wallHits.HitVertDoor();
    internal static void HitHorizDoor() => wallHits.HitHorizDoor();
    internal static void HitDiagWall(WallShape shape, int tilex, int tiley) => wallHits.HitDiagWall(shape, tilex, tiley);
    internal static TextureAsset? WorldTexture(string name) => wallHits.WorldTexture(name);
    static TextureAsset? WorldTexture(string name, int tilex, int tiley) => wallHits.WorldTexture(name, tilex, tiley);
    static TextureAsset? WallTexture(int tilex, int tiley, string face, out bool mirrored) => wallHits.WallTexture(tilex, tiley, face, out mirrored);
    static void NoteLintel(doorobj_t door, int planex, int planey) => wallHits.NoteLintel(door, planex, planey);
    static void NoteFocalLintel(int xstep, int ystep) => wallHits.NoteFocalLintel(xstep, ystep);
    static void SeeThroughJamb(MapTextureTranslation? mapTexture, string face, int texture, int tilex, int tiley) =>
        wallHits.SeeThroughJamb(mapTexture, face, texture, tilex, tiley);
    static bool IsSolidEdge(WallShape shape, controldirs edge) => wallHits.IsSolidEdge(shape, edge);
    static bool TraceDiagonal(WallShape shape, int tilex, int tiley, long eu, long ev, long du, long dv) =>
        wallHits.TraceDiagonal(shape, tilex, tiley, eu, ev, du, dv);

    static TextureAsset? patchsource { get => render.patchsource; set => render.patchsource = value; }   // the wall patch for the post ScalePost queues, if it has one
    static int patchofs { get => render.patchofs; set => render.patchofs = value; }   // its column (never mirrored with a flipped face)
    static int patchstory { get => render.patchstory; set => render.patchstory = value; }
    static bool hitpushwall { get => render.hitpushwall; set => render.hitpushwall = value; }   // the wall being hit is a moving pushwall: its tile is pwallx, pwally

    static int diaghitu { get => render.diaghitu; set => render.diaghitu = value; }   // where the last TraceDiagonal hit, as the tile-local x (0..TILEGLOBAL)


    // The ceiling and floor, the sky and the flats (Rendering/SkyAndFlats.cs)
    static SkyAndFlats skyAndFlats = null!;     // made in InitServices

    static SkyAndFlats MakeSkyAndFlats() =>
        new(_gameEngineManager, _assetManager, _videoManager, _mapManager, lighting, actorLights, render, session, WorldTexture);

    static byte ceilingcolor => skyAndFlats.ceilingcolor;
    const byte FLATMASK = SkyAndFlats.FLATMASK;
    internal static string? levelsky { get => skyAndFlats.levelsky; set => skyAndFlats.levelsky = value; }
    internal static void VGAClearScreen() => skyAndFlats.VGAClearScreen();

    // The ray trace (Rendering/RayTrace.cs)
    static RayTrace rayTrace = null!;       // made in InitServices

    static RayTrace MakeRayTrace() => new(_mapManager, doors, pushWalls, wallPosts, wallHits, render);

    private static void WallRefresh() => rayTrace.WallRefresh();

    static SpriteDrawing spriteDrawing = null!;

    static SpriteDrawing MakeSpriteDrawing() => new(_assetManager, _mapManager, _translationManager, lighting, spriteScaler,
        wallSpriteDrawing, weather, render, weaponBob, session,
        new SpriteDrawingHooks(WeaponShapeName, IsPlayerBody, VisiblePlayerBody, DrawnSprite, SpriteExists,
            () => deathCamSprite, () => weaponScale, () => demoplayback || demorecord));

    static void TransformActor(Entities.Actors.Actor ob) => spriteDrawing.TransformActor(ob);
    static void TransformActorFrom(Entities.Actors.Actor ob, int fromx, int fromy, int fromsin, int fromcos) => spriteDrawing.TransformActorFrom(ob, fromx, fromy, fromsin, fromcos);
    internal static bool HasDirectionalSprites(string sprite, string frameLetter) => spriteDrawing.HasDirectionalSprites(sprite, frameLetter);
    internal static string ActorShapeName(string sprite, string frameLetter, Entities.Actors.Actor actor) => spriteDrawing.ActorShapeName(sprite, frameLetter, actor);
    internal static string FrontShapeName(string sprite, string frameLetter) => spriteDrawing.FrontShapeName(sprite, frameLetter);
    static void DrawScaleds() => spriteDrawing.DrawScaleds();
    static void DrawPlayerWeapon() => spriteDrawing.DrawPlayerWeapon();

    internal static void ThreeDRefresh()
    {
        // Create a Span<T> from the multidimensional array's data reference
        Span<bool> data = MemoryMarshal.CreateSpan(
            ref _mapManager.spotvis[0, 0], // Reference to the first element
            _mapManager.spotvis.Length     // Total number of elements
        );

        // Fill the span with the value
        data.Fill(false);

        // Detect all sprites over camera fix. Demos need it too: Wolf4SDL 1.7c, which plays
        // the games' own demos through, always had it (enemies beside the player stay seen)
        if (_mapManager.tilemap[camera.TileX, camera.TileY] == 0 ||
         (_mapManager.tilemap[camera.TileX, camera.TileY] & BIT_DOOR) != 0)
            _mapManager.spotvis[camera.TileX, camera.TileY] = true;


        vbuf = 0;
        vbufPtr = _videoManager.LockSurface();
        if (vbufPtr == IntPtr.Zero) return;

        vbuf += (int)screenofs;

        Setup3DView();
        UpdateLightGrid();
        CastActorLights();
        _mapManager.AnimateFlats(gamestate.TimeCount);     // animated floors and ceilings, on the level's clock as walls are

        //
        // follow the walls from there to the right, drawing as we go
        //
        VGAClearScreen();

        markseen = camera.OnPlayer;     // the automap only learns what the player sees
        SetupWeather();
        WallRefresh();

        if (markseen)
            _mapManager.MarkSeenFromSpotvis();      // floor the rays crossed, for the automap

        //
        // draw all the scaled images
        //
        DrawScaleds();                  // draw scaled stuff
        PlayerSight();                  // what the player sees, when the camera is elsewhere

        DrawPlayerWeapon();    // draw player's hands

        _videoManager.UnlockSurface();
        vbuf = 0;

        // For the next save's thumbnail, before the automap or console is drawn over the view
        _videoManager.KeepViewCopy(viewscreenx, viewscreeny, viewwidth, viewheight);

        //
        // show screen and time last cycle
        //

        if (fizzlein)
        {
            _videoManager.Transition(levelFadeStyle, 0, 0, _videoManager.screenWidth, _videoManager.screenHeight, (uint)levelFadeTics);
            fizzlein = false;

            lasttimecount = (int)GameEngineManager.GetTimeCount();          // don't make a big tic count
        }
        else
        {
            RestoreBorderAfterConsole();

            DrawAutomap();

            DrawHudMessages();      // over the automap too, but not in the save thumbnail
            DrawNetOverlay();       // waiting for someone, or asking whether to leave (Program.NetPlay.cs)

            if (fpscounter)
            {
                _videoManager.Bar(0, 0, 40, 10, bordercol);
                _graphicManager.DrawText(4, 1, $"{fps} fps", new Fonts.TextStyle(SMALL_FONT, "Grey", "VIEWCOLOR"));
            }

            // Taken here, before the console is drawn, so it never shows in the shot.
            if (screenshotPending)
            {
                screenshotPending = false;
                _consoleManager.Print(TakeScreenshot());
            }

            DrawConsole();
            _videoManager.Update();
        }

        if (fpscounter)
        {
            fps_frames++;
            fps_time += (int)tics;

            if (fps_time > 35)
            {
                fps_time -= 35;
                fps = fps_frames << 1;
                fps_frames = 0;
            }
        }
    }
}
