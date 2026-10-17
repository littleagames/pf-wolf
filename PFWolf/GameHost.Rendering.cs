using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf;

// GameHost's rendering wiring: the 3D view, sprites, lighting, weather, wall sprites, the camera and what players see
internal static partial class GameHost
{
    // ==== WL_DRAW ================================================================================

    /*
    =============================================================================

                                   LOCAL CONSTANTS

    =============================================================================
    */

    /*
    =============================================================================

                                  GLOBAL VARIABLES

    =============================================================================
    */

#if USE_FLOORCEILINGTEXT || USE_CLOUDSKY
    short[] spanstart;
#endif

    internal static short[] WallHeight { get => Render.WallHeight; set => Render.WallHeight = value; }

    //
    // math tables
    //
    internal static short[] PixelAngle { get => Render.PixelAngle; set => Render.PixelAngle = value; }

    //
    // refresh variables
    internal static int ViewX { get => Render.ViewX; set => Render.ViewX = value; }
    static readonly TextureAsset notexture = new() { RawData = new byte[TextureSize * TextureSize] };

    // The renderer's shared per-frame state (Rendering/RenderState.cs): the fields below, under their old names, are its
    internal static readonly RenderState Render = new(notexture, Lighting.NoShade);

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

    internal const int EyeDefault = GameConstants.EyeDefault, MinEye = GameConstants.MinEye, MaxEye = GameConstants.MaxEye;

    // A column's posts and the arches the trace passes under (Rendering/WallPosts.cs)
    static WallPosts wallPosts = null!;     // made in InitServices

    static WallPosts MakeWallPosts() =>
        new(_videoManager, MapManager, lighting, skyAndFlats, Render, weather, Session, WallTexture);

    static bool IsArch(int tilex, int tiley) => wallPosts.IsArch(tilex, tiley);
    static int FloorRow(int half) => wallPosts.FloorRow(half);

    // What the trace does when it hits a wall, door or diagonal (Rendering/WallHits.cs)
    static WallHits wallHits = null!;       // made in InitServices

    static WallHits MakeWallHits() => new(AssetManager, MapManager, doors, pushWalls, wallPosts, Render, Session);

    internal static TextureAsset? WorldTexture(string name) => wallHits.WorldTexture(name);
    static TextureAsset? WorldTexture(string name, int tilex, int tiley) => wallHits.WorldTexture(name, tilex, tiley);
    static TextureAsset? WallTexture(int tilex, int tiley, string face, out bool mirrored) => wallHits.WallTexture(tilex, tiley, face, out mirrored);

    // The ceiling and floor, the sky and the flats (Rendering/SkyAndFlats.cs)
    static SkyAndFlats skyAndFlats = null!;     // made in InitServices

    static SkyAndFlats MakeSkyAndFlats() =>
        new(GameEngineManager, AssetManager, _videoManager, MapManager, lighting, actorLights, Render, Session, WorldTexture);

    // The ray trace (Rendering/RayTrace.cs)
    static RayTrace rayTrace = null!;       // made in InitServices

    static RayTrace MakeRayTrace() => new(MapManager, doors, pushWalls, wallPosts, wallHits, Render);

    // The sprites and the weapon in hand (Rendering/SpriteDrawing.cs)
    static SpriteDrawing spriteDrawing = null!;     // made in InitServices

    static SpriteDrawing MakeSpriteDrawing() => new(AssetManager, MapManager, TranslationManager, lighting, spriteScaler,
        wallSpriteDrawing, weather, Render, WeaponBob, Session,
        new SpriteDrawingHooks(WeaponShapeName, IsPlayerBody, VisiblePlayerBody, DrawnSprite, SpriteExists,
            () => DeathCamSprite, () => WeaponScale));

    static void TransformActorFrom(Entities.Actors.Actor ob, int fromx, int fromy, int fromsin, int fromcos) => spriteDrawing.TransformActorFrom(ob, fromx, fromy, fromsin, fromcos);

    // One frame: its tics, the view from the camera, and drawing it all (Rendering/FrameRefresh.cs)
    static FrameRefresh frameRefresh = null!;       // made in InitServices

    static FrameRefresh MakeFrameRefresh() => new(_videoManager, _graphicManager, MapManager, _consoleManager, lighting, actorLights,
        weather, skyAndFlats, rayTrace, spriteDrawing, playerView, Render, Session,
        new FrameRefreshHooks(() => LevelFadeStyle, () => LevelFadeTics,
            RestoreBorderAfterConsole, DrawAutomap, () => playScreen.DrawStatusOverlay(), DrawHudMessages, DrawNetOverlay, DrawConsole,
            () => ScreenshotPending, () => ScreenshotPending = false, TakeScreenshot, UpdateViewQuake,
            () => WeaponZoom.ScopePicture(), DrawScope));

    // A scope's picture over the whole screen (the original's H_SNIPERSCOPE at 0, 0), with black
    // past its edges on a screen wider or taller than it, so nothing outside the scope shows
    static void DrawScope(string pic)
    {
        int left = _videoManager.ScreenX(0), right = _videoManager.ScreenX(320);
        int top = _videoManager.ScreenY(0), bottom = _videoManager.ScreenY(200);
        _videoManager.BarScaledCoord(0, 0, Math.Max(left, 0), _videoManager.ScreenHeight, "Black");
        _videoManager.BarScaledCoord(right, 0, Math.Max(_videoManager.ScreenWidth - right, 0), _videoManager.ScreenHeight, "Black");
        _videoManager.BarScaledCoord(0, 0, _videoManager.ScreenWidth, Math.Max(top, 0), "Black");
        _videoManager.BarScaledCoord(0, bottom, _videoManager.ScreenWidth, Math.Max(_videoManager.ScreenHeight - bottom, 0), "Black");
        _graphicManager.DrawPic(pic, 0, 0);
    }

    // The view's quake (Rendering/ViewQuake.cs): blasts (A_Quake) and a level's own rumble (game-info shake)
    static readonly ViewQuake viewQuake = new();
    static string? quakeMap;

    static void UpdateViewQuake()
    {
        var mapon = Session.GameState.MapOn;
        if (mapon != quakeMap)
        {
            viewQuake.Reset();          // a new level starts still
            quakeMap = mapon;
        }
        int rumble = mapon != null && GameEngineManager.GetGameInfo().Maps.TryGetValue(mapon, out var map) ? map.Shake : 0;
        // a scope's zoom magnifies the view: its focal length that many times as long
        long zoom = (long)(GameConstants.FocalLength * (WeaponZoom.Magnification() - 1));
        viewSetup.SetFocalExcursion(viewQuake.Update((int)Session.Tics, rumble) + zoom);
    }

    /// <summary>A_Quake(min[, max]): shakes the view, at a power (0-200) rolled from min to max (Midnight Assault's blasts)</summary>
    static void Quake(Entities.Actors.Actor ob, string[] args)
    {
        int min = args.Length > 0 && int.TryParse(args[0], out var a0) ? a0 : 0;
        int max = args.Length > 1 && int.TryParse(args[1], out var a1) ? a1 : min;
        viewQuake.Start(max > min ? Random.Shared.Next(min, max + 1) : min);
    }

    static int LastTimeCount { get => frameRefresh.LastTimeCount; set => frameRefresh.LastTimeCount = value; }

    internal static double MaxPitch() => frameRefresh.MaxPitch();
    internal static void ThreeDRefresh() => frameRefresh.ThreeDRefresh();

    // ==== WL_SCALE ===============================================================================

    // Drawing sprites scaled onto the view (Rendering/SpriteScaler.cs)
    static SpriteScaler spriteScaler = null!;       // made in InitServices

    static SpriteScaler MakeSpriteScaler() => new(AssetManager, _videoManager, lighting, Render, FloorRow);

    internal static void ScaleColumn(short x, short toppix, int fracstep, SpriteAsset sprite, int column, byte[] shade, int shadeofs,
        bool cloaked = false, SpriteTranslation? translation = null) =>
        spriteScaler.ScaleColumn(x, toppix, fracstep, sprite, column, shade, shadeofs, cloaked, translation);

    // ==== WL_SHADE ===============================================================================

    // The level's shading, light grid and light zones (Rendering/Lighting.cs)
    static Lighting lighting = null!;       // made in InitServices

    // The level-editing console commands (Commands/LevelEditCommands.cs)
    static LevelEditCommands levelEditCommands = null!;     // made in InitServices
    static FactionCommands factionCommands = null!;     // (Commands/FactionCommands.cs)

    static Lighting MakeLighting() =>
        new(GameEngineManager, _videoManager, MapManager,
            new LightingHooks(() => HeightNumerator, () => ColorTintTables.Clear(), TickActorLights));

    private static void RegisterZoneLightActions() => (lighting ??= MakeLighting()).RegisterZoneLightActions();

    // ==== ActorLights ============================================================================

    // Light actors give off (Rendering/ActorLights.cs)
    static ActorLights actorLights = null!;     // made in InitServices

    static ActorLights MakeActorLights() => new(MapManager, doors, lighting ??= MakeLighting(), Session);

    static Dictionary<string, byte[]> ColorTintTables => actorLights.ColorTintTables;

    private static void RegisterActorLightActions() => (actorLights ??= MakeActorLights()).RegisterActorLightActions();
    static void TickActorLights(uint tics) => actorLights.TickActorLights(tics);

    // ==== Weather ================================================================================

    // Rain and snow (Rendering/Weather.cs)
    static Weather weather = null!;     // made in InitServices

    static Weather MakeWeather() => new(GameEngineManager, _videoManager, MapManager, lighting, Render, Session, FloorRow, IsArch);

    // ==== WallSprites ============================================================================

    // Wall sprites' geometry (Rendering/WallSprites.cs) and their blocking on the level
    // (Rendering/WallSprites.cs WallSpriteBlocking)
    static WallSpriteBlocking wallSpriteBlocking = null!;       // made in InitServices

    internal static bool IsWallSprite(Entities.Actors.Actor actor) => WallSprites.IsWallSprite(actor);

    static int WallSpriteFromX { get => wallSpriteBlocking.WallSpriteFromX; set => wallSpriteBlocking.WallSpriteFromX = value; }
    static int WallSpriteFromY { get => wallSpriteBlocking.WallSpriteFromY; set => wallSpriteBlocking.WallSpriteFromY = value; }
    internal static WallSpriteSpan? WallSpriteHitByBox(int xl, int yl, int xh, int yh, long x, long y, long size, bool player) =>
        wallSpriteBlocking.WallSpriteHitByBox(xl, yl, xh, yh, x, y, size, player);
    internal static bool WallSpriteBlocksLine(long x1, long y1, long x2, long y2) => wallSpriteBlocking.WallSpriteBlocksLine(x1, y1, x2, y2);

    // Drawing wall sprites (Rendering/WallSpriteDrawing.cs)
    static WallSpriteDrawing wallSpriteDrawing = null!;     // made in InitServices

    static WallSpriteDrawing MakeWallSpriteDrawing() => new(AssetManager, MapManager, lighting, Render, spriteScaler, FloorRow);

    // ==== Camera =================================================================================

    internal static void SetEyeHeight(int z) => PlayerEyeZ = Math.Clamp(z, MinEye, MaxEye);

    // The `spectate` command (Gameplay/Spectating.cs)
    static Spectating spectating = null!;       // made in InitServices

    static IEnumerable<Entities.Actors.Actor> SpectateCandidates() => spectating.SpectateCandidates();
    private static void SpectateCommand(string[] args) => spectating.SpectateCommand(args);

    // What others see of each player (Gameplay/PlayerBodies.cs)
    static PlayerBodies playerBodies = null!;       // made in InitServices

    internal static void TickPlayerBodies(uint tics) => playerBodies.TickPlayerBodies(tics);
    static Entities.Actors.Actor? VisiblePlayerBody(PlayerPawn pawn) => playerBodies.VisiblePlayerBody(pawn);
    static bool IsPlayerBody(Entities.Actors.Actor actor) => playerBodies.IsPlayerBody(actor);
    internal static string DrawnSprite(Entities.Actors.Actor actor) => playerBodies.DrawnSprite(actor);
    static bool SpriteExists(string name) => playerBodies.SpriteExists(name);

    // ==== PlayerSight ============================================================================

    // What the player touches and sees, kept apart from what the camera draws (Rendering/PlayerView.cs)
    static PlayerView playerView = null!;       // made in InitServices

    static PlayerView MakePlayerView() =>
        new(MapManager, lineOfSight, Session, new PlayerViewHooks(
            () => FocalLength, () => ViewWidth, TransformActorFrom, IsWallSprite, GetBonus));

    static (int x, int y, int sin, int cos) PlayerViewOrigin() => playerView.PlayerViewOrigin();
}
