using PFWolf.Assets;

namespace PFWolf;

internal static partial class GameHost
{
    /*
    =============================================================================

                                   LOCAL CONSTANTS

    =============================================================================
    */

    internal const int ACTORSIZE = GameConstants.ACTORSIZE;

    /*
    =============================================================================

                                  GLOBAL VARIABLES

    =============================================================================
    */

#if USE_FLOORCEILINGTEXT || USE_CLOUDSKY
    short[] spanstart;
#endif

    internal static short[] wallheight { get => render.wallheight; set => render.wallheight = value; }

    //
    // math tables
    //
    internal static short[] pixelangle { get => render.pixelangle; set => render.pixelangle = value; }
    internal static int[] finetangent => TrigTables.finetangent;   // (Rendering/TrigTables.cs)
    internal static int[] sintable => TrigTables.sintable;
    internal static int[] costable => TrigTables.costable;

    //
    // refresh variables
    internal static int viewx { get => render.viewx; set => render.viewx = value; }
    static readonly TextureAsset notexture = new() { RawData = new byte[TEXTURESIZE * TEXTURESIZE] };

    // The renderer's shared per-frame state (Rendering/RenderState.cs): the fields below, under their old names, are its
    internal static readonly RenderState render = new(notexture, Lighting.noshade);

    //
    // tall walls: every wall is wallstories 64 unit stories tall, built up from the floor
    //
    internal const int MAXWALLSTORIES = GameConstants.MAXWALLSTORIES;
    internal static int wallstories { get => _mapManager.wallstories; set => _mapManager.wallstories = value; }

    internal static double viewpitch { get => render.viewpitch; set => render.viewpitch = value; }   // degrees, up positive; this frame's, from the camera

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

    internal const int EYEDEFAULT = GameConstants.EYEDEFAULT, MINEYE = GameConstants.MINEYE, MAXEYE = GameConstants.MAXEYE;

    // A column's posts and the arches the trace passes under (Rendering/WallPosts.cs)
    static WallPosts wallPosts = null!;     // made in InitServices

    static WallPosts MakeWallPosts() =>
        new(_videoManager, _mapManager, lighting, skyAndFlats, render, weather, session, WallTexture);

    static bool IsArch(int tilex, int tiley) => wallPosts.IsArch(tilex, tiley);
    static int FloorRow(int half) => wallPosts.FloorRow(half);

    // What the trace does when it hits a wall, door or diagonal (Rendering/WallHits.cs)
    static WallHits wallHits = null!;       // made in InitServices

    static WallHits MakeWallHits() => new(_assetManager, _mapManager, doors, pushWalls, wallPosts, render, session);

    internal static TextureAsset? WorldTexture(string name) => wallHits.WorldTexture(name);
    static TextureAsset? WorldTexture(string name, int tilex, int tiley) => wallHits.WorldTexture(name, tilex, tiley);
    static TextureAsset? WallTexture(int tilex, int tiley, string face, out bool mirrored) => wallHits.WallTexture(tilex, tiley, face, out mirrored);

    // The ceiling and floor, the sky and the flats (Rendering/SkyAndFlats.cs)
    static SkyAndFlats skyAndFlats = null!;     // made in InitServices

    static SkyAndFlats MakeSkyAndFlats() =>
        new(_gameEngineManager, _assetManager, _videoManager, _mapManager, lighting, actorLights, render, session, WorldTexture);

    // The ray trace (Rendering/RayTrace.cs)
    static RayTrace rayTrace = null!;       // made in InitServices

    static RayTrace MakeRayTrace() => new(_mapManager, doors, pushWalls, wallPosts, wallHits, render);

    // The sprites and the weapon in hand (Rendering/SpriteDrawing.cs)
    static SpriteDrawing spriteDrawing = null!;     // made in InitServices

    static SpriteDrawing MakeSpriteDrawing() => new(_assetManager, _mapManager, _translationManager, lighting, spriteScaler,
        wallSpriteDrawing, weather, render, weaponBob, session,
        new SpriteDrawingHooks(WeaponShapeName, IsPlayerBody, VisiblePlayerBody, DrawnSprite, SpriteExists,
            () => deathCamSprite, () => weaponScale, () => demoplayback || demorecord));

    static void TransformActorFrom(Entities.Actors.Actor ob, int fromx, int fromy, int fromsin, int fromcos) => spriteDrawing.TransformActorFrom(ob, fromx, fromy, fromsin, fromcos);
    internal static bool HasDirectionalSprites(string sprite, string frameLetter) => spriteDrawing.HasDirectionalSprites(sprite, frameLetter);
    internal static string FrontShapeName(string sprite, string frameLetter) => spriteDrawing.FrontShapeName(sprite, frameLetter);

    // One frame: its tics, the view from the camera, and drawing it all (Rendering/FrameRefresh.cs)
    static FrameRefresh frameRefresh = null!;       // made in InitServices

    static FrameRefresh MakeFrameRefresh() => new(_videoManager, _graphicManager, _mapManager, _consoleManager, lighting, actorLights,
        weather, skyAndFlats, rayTrace, spriteDrawing, playerView, render, session,
        new FrameRefreshHooks(() => fizzlein, () => fizzlein = false, () => levelFadeStyle, () => levelFadeTics,
            RestoreBorderAfterConsole, DrawAutomap, () => playScreen.DrawStatusOverlay(), DrawHudMessages, DrawNetOverlay, DrawConsole, () => bordercol,
            () => screenshotPending, () => screenshotPending = false, TakeScreenshot));

    static int lasttimecount { get => frameRefresh.lasttimecount; set => frameRefresh.lasttimecount = value; }

    internal static double MaxPitch() => frameRefresh.MaxPitch();
    internal static void ThreeDRefresh() => frameRefresh.ThreeDRefresh();
}
