using PFWolf.Configuration;
using PFWolf.Entities.Actors;
using PFWolf.Extensions;
using PFWolf.Managers;
using static SDL2.SDL;

namespace PFWolf;

internal partial class Program
{
    internal static bool madenoise; // true when shooting or screaming

    internal static playstatetypes playstate;

    internal static int DebugOk;

    // The acting player's pawn and controls, and this frame's tics (Gameplay/GameSession.cs)
    internal static Entities.Actors.PlayerPawn player => session.player;
    internal static PlayerInput playerinput => session.playerinput;
    internal static uint tics { get => session.tics; set => session.tics = value; }

    internal static byte singlestep, godmode, noclip, ammocheat, mapreveal;
    internal static int extravbls;

    //
    // control info
    //
    internal static bool mouseenabled { get => controlSettings.mouseenabled; set => controlSettings.mouseenabled = value; }
    internal static bool joystickenabled { get => controlSettings.joystickenabled; set => controlSettings.joystickenabled = value; }

    /// <summary>The keys, mouse buttons and joystick buttons bound to each button and automap key (saved to controls.cfg).</summary>
    internal static readonly ControlBindings controls = new();

    internal static int viewsize;

    internal static bool demorecord, demoplayback;
    // A demo's controls (Gameplay/DemoBuffer.cs)
    internal static readonly DemoBuffer demoBuffer = new();
    static byte[] demoData { get => demoBuffer.demoData; set => demoBuffer.demoData = value; }
    static int demoptr { get => demoBuffer.demoptr; set => demoBuffer.demoptr = value; }
    static int lastdemoptr { get => demoBuffer.lastdemoptr; set => demoBuffer.lastdemoptr = value; }

    //
    // current user input: gathered each frame by PollControls into localcmd, which is all the
    // game itself reads (through the player's PlayerInput) (Gameplay/InputPolling.cs)
    //
    static InputPolling inputPolling = null!;       // made in InitServices

    static InputPolling MakeInputPolling() => new(_inputManager, _consoleManager, _automapManager, controls, controlSettings,
        frameRefresh, demoBuffer, session,
        new InputPollingHooks(s => playstate = s, () => netgame, () => netChatting, () => netLeavePrompt,
            () => demoplayback, () => demorecord, () => demoTesting, RouteMovementToAutomap));

    static TicCmd localcmd => inputPolling.localcmd;

    // The mouse look and controller settings (Configuration/ControlSettings.cs)
    internal static readonly ControlSettings controlSettings = new();
    internal static bool mouselook { get => controlSettings.mouselook; set => controlSettings.mouselook = value; }
    internal static bool mouseinvert { get => controlSettings.mouseinvert; set => controlSettings.mouseinvert = value; }

    internal static int joydeadzone { get => controlSettings.joydeadzone; set => controlSettings.joydeadzone = value; }
    internal static int joyturnspeed { get => controlSettings.joyturnspeed; set => controlSettings.joyturnspeed = value; }
    internal static bool joyclassicsticks { get => controlSettings.joyclassicsticks; set => controlSettings.joyclassicsticks = value; }

    internal const int JOYTURNSPEEDS = ControlSettings.JOYTURNSPEEDS;

    static int lastgamemusicoffset = 0;

    /*
    =============================================================================

                                                     LOCAL VARIABLES

    =============================================================================
    */

    // The level's song (Gameplay/LevelMusic.cs)
    static LevelMusic levelMusic = null!;       // made in InitServices

    internal static void ContinueMusic(int offs) => levelMusic.ContinueMusic(offs);

    // The keys during play (Ui/PlayKeys.cs)
    static PlayKeys playKeys = null!;       // made in InitServices

    static PlayKeys MakePlayKeys() => new(_assetManager, _videoManager, _inputManager, _audioManager, _graphicManager, _gameEngineManager,
        _consoleManager, _automapManager, _inventoryManager, menuDrawing, inputPolling, statusBarDrawing, playerWeapons, playerClassRules,
        levelMusic, frameRefresh, session,
        new PlayKeysHooks(() => demoplayback, () => demorecord, () => netgame, () => netChatting, NetChatKeys, NetCheckKeys,
            ToggleAutomap, HandleAutomapKey, () => viewsize, DrawPlayBorder, DrawPlayBorderSides, () => DebugOk = 1,
            US_ControlPanel, DrawPlayScreen, () => startgame, () => loadedgame, s => playstate = s));

    // Playing a level, a frame at a time (Gameplay/LevelLoop.cs)
    static LevelLoop levelLoop = null!;     // made in InitServices

    static LevelLoop MakeLevelLoop() => new(_inputManager, _videoManager, _consoleManager, _automapManager, _hudMessageManager, _mapManager,
        frameRefresh, inputPolling, playKeys, doors, pushWalls, playerView, lighting, gameScripts, deathmatch, netPlay, session,
        new LevelLoopHooks(() => playstate, s => playstate = s, () => startgame, () => netgame, () => demoplayback, () => demoTesting,
            () => madenoise = false, TickPlayerBodies, UpdateSoundListener, UpdateAutomap, () => autosavePending, AutoSaveGame,
            () => singlestep, () => extravbls));

    internal static void InitActorList() => levelLoop.InitActorList();

    internal const int MAXX = 320;
    internal const int MAXY = 160;

    /// <summary>Black text on the white of a <see cref="CenterWindow"/></summary>
    internal static readonly Fonts.TextStyle PromptStyle = new(SMALL_FONT, "Black", "White");

    /// <summary>A framed window of w by h tiles, centered in the play view</summary>
    internal static Fonts.TextWindow CenterWindow(int w, int h, Fonts.TextStyle style)
        => US_DrawWindow(((MAXX / 8) - w) / 2, ((MAXY / 8) - h) / 2, w, h, style);
}
