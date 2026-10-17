using PFWolf.Assets;
using PFWolf.Configuration;
using PFWolf.Entities.Actors;
using PFWolf.Extensions;
using PFWolf.Managers;
using PFWolf.Scripting;
using SDL2;
using System.Text;
using static SDL2.SDL;

namespace PFWolf;

// GameHost's game flow wiring: starting and playing levels, demos, saving and loading, hubs, scripts
internal static partial class GameHost
{
    // ==== WL_GAME ================================================================================

    /*
=============================================================================

                             GLOBAL VARIABLES

=============================================================================
*/
    internal static gametype gamestate { get => session.gamestate; set => session.gamestate = value; }
    static string bordercol { get => session.bordercol; set => session.bordercol = value; } // color of the Change View/Ingame border

    // The game from its first level to its end (Gameplay/MainGameLoop.cs)
    static MainGameLoop mainGameLoop = null!;       // made in InitServices

    static MainGameLoop MakeMainGameLoop() => new(_gameEngineManager, _assetManager, _videoManager, _audioManager, _inputManager,
        _inventoryManager, statusBarDrawing, playScreen, levelMusic, levelLoop, playerMovement, playerDeath, demos, deathmatch, scoreboard,
        netPlay, gameScripts, hubs, controlPanel, intermissionScreen, highScoresScreen, textPresenter, moviePlayer, session,
        settings, new MainGameLoopHooks(on => autosavePending = on, EnterLevel, LeaveLevel,
            MarkLevelStart, ApplyPendingWarpStart, AlignPlayerInElevator, ShowLevelStartMessage, name => articleReader.ShowText(name),
            LevelWon));

    static PendingMapChange? pendingMapChange { get => mainGameLoop.pendingMapChange; set => mainGameLoop.pendingMapChange = value; }

    // Demos: played, recorded and asked for from the console (Gameplay/Demos.cs)
    static Demos demos = null!;     // made in InitServices

    static Demos MakeDemos() => new(_assetManager, _gameEngineManager, _videoManager, _audioManager, ui, lineInput, demoBuffer, levelMusic, levelLoop,
        session, new DemosHooks((skill, episode, map, playerClass) => NewGame(skill, episode, map, playerClass), () => DemoSkill, DrawPlayScreen,
            SetupGameLevel, RunLevelHooks, () => demoTesting,
            DemoTestReport, () => INTROSONG));

    internal static void PlayDemo(int demonumber) => demos.PlayDemo(demonumber);
    internal static bool DemoExists(int demonumber) => demos.DemoExists(demonumber);

    // playdemo, demotest and recorddemo (Commands/DemoCommands.cs)
    static DemoCommands demoCommands = null!;       // made in InitServices

    /// <summary>Set while demotest runs: demos play without waiting, and each prints its report as it ends</summary>
    internal static bool demoTesting { get => demoCommands.demoTesting; set => demoCommands.demoTesting = value; }
    internal static string DemoTestReport(int demonumber) => demoCommands.DemoTestReport(demonumber);

    // The screen around the view: status bar, border and "get psyched" (Ui/PlayScreen.cs)
    static PlayScreen playScreen = null!;       // made in InitServices

    static PlayScreen MakePlayScreen() => new(_assetManager, _gameEngineManager, _videoManager, _graphicManager, _inputManager,
        statusBarDrawing, playerWeapons, radar, render, playArea,
        settings, session, new PlayScreenHooks(PresenterMessageBox));

    internal static void DrawPlayScreen() => playScreen.DrawPlayScreen();
    internal static void DrawPlayBorder() => playScreen.DrawPlayBorder();
    internal static void DrawPlayBorderSides() => playScreen.DrawPlayBorderSides();
    internal static void DrawStatusBorder(string color) => playScreen.DrawStatusBorder(color);
    internal static string GetMapDisplayName(string mapon) => playScreen.GetMapDisplayName(mapon);
    internal static void ShowLevelStartMessage(string script) => playScreen.ShowLevelStartMessage(script);

    // Setting up a level to play (Gameplay/LevelSetup.cs)
    static LevelSetup levelSetup = null!;       // made in InitServices

    static LevelSetup MakeLevelSetup() => new(_gameEngineManager, _mapManager, render, skyAndFlats, weather, lighting, doors, pushWalls,
        deathmatch, playersManager, playerClassRules, levelLoop, weaponBob, levelAI, session,
        new LevelSetupHooks(() => CurrentSkill,
            loadout => mapLoadouts.Apply(loadout), () => objectives.CountLevel(announce: !demoplayback && !demorecord)));

    // Level objectives (Gameplay/Objectives.cs): sabotage targets, goal enemies
    static Objectives? _objectives;
    static Objectives objectives => _objectives ??= new Objectives(_mapManager, new ObjectiveHooks(
        () => _gameEngineManager.GetGameInfo().Objectives,
        text => _hudMessageManager.Localize(text),
        message => _hudMessageManager.Show(HudMessageKind.Lock, message),
        LevelSwitches));

    // What the player has unlocked, from game to game (Gameplay/Unlocks.cs): starts-locked episodes
    static Unlocks? _unlocks;
    internal static Unlocks unlocks => _unlocks ??= new Unlocks(() => _gameEngineManager.GetConfigFilePath(Unlocks.FileName),
        () => _gameEngineManager.GetGameInfo());

    /// <summary>A_Unlock, game.unlock: unlocks a name, but not while a demo plays back</summary>
    internal static bool Unlock(string name) => !demoplayback && unlocks.Unlock(name);

    // The best done on each map, from game to game (Gameplay/LevelRecords.cs)
    static LevelRecords? _levelRecords;
    internal static LevelRecords levelRecords => _levelRecords ??= new LevelRecords(() => _gameEngineManager.GetConfigFilePath(LevelRecords.FileName));

    /// <summary>
    /// A level's finished, before its tally: its record is bettered and the scripts' level-end
    /// hooks run (which may unlock what it earned). Not while a demo plays back.
    /// </summary>
    static void LevelWon(playstatetypes how)
    {
        if (demoplayback || gamestate.mapon is not { Length: > 0 } map)
            return;
        var result = CurrentLevelResult(map);
        levelRecords.Record(map, result);
        gameScripts.RunLevelEndHooks(map, result, how switch
        {
            playstatetypes.ex_secretlevel => "secret",
            playstatetypes.ex_victorious => "victory",
            _ => "exit",
        });
    }

    /// <summary>What the level being played has come to so far</summary>
    internal static LevelResult CurrentLevelResult(string map)
    {
        int par = _gameEngineManager.GetGameInfo().Maps.TryGetValue(map, out var info) ? info.ParTime : 0;
        return new LevelResult
        {
            Kills = gamestate.killcount, KillTotal = gamestate.killtotal,
            Secrets = gamestate.secretcount, SecretTotal = gamestate.secrettotal,
            Items = gamestate.treasurecount, ItemTotal = gamestate.treasuretotal,
            Time = gamestate.TimeCount,
            Par = par,
            UnderPar = par > 0 && gamestate.TimeCount < par * 70,
            Undetected = session.playerstate.Undetected,
            StealthKills = session.playerstate.StealthKills,
            Objectives = _mapManager.objectives.ToDictionary(o => o.Key, o => (o.Value.Done, o.Value.Total), StringComparer.OrdinalIgnoreCase),
        };
    }

    // Every switch on the level: its switch walls' and its patches'
    static IEnumerable<MapSwitchTranslation> LevelSwitches()
    {
        var data = _mapManager.GetMapData();
        for (int y = 0; y < _mapManager.mapheight; y++)
            for (int x = 0; x < _mapManager.mapwidth; x++)
            {
                int tile = _mapManager.tilemap[x, y];
                if (tile != 0 && (tile & GameConstants.BIT_DOOR) == 0
                    && data.Walls.TryGetValue(tile & ~GameConstants.BIT_WALL, out var wall) && wall.Switch is { } wallSwitch)
                    yield return wallSwitch;
                if (_mapManager.GetPatch(x, y) is var patch and > 0 && data.Patches.TryGetValue(patch, out var patchDef)
                    && patchDef.Switch is { } patchSwitch)
                    yield return patchSwitch;
            }
    }

    // A map's starting loadout (Gameplay/MapLoadouts.cs)
    static MapLoadouts? _mapLoadouts;
    static MapLoadouts mapLoadouts => _mapLoadouts ??= new MapLoadouts(_inventoryManager, playerWeapons, session);

    internal static void SetupGameLevel() => levelSetup.SetupGameLevel();

    // A player's death, and the death cam (Gameplay/PlayerDeath.cs)
    static PlayerDeath playerDeath = null!;     // made in InitServices

    static PlayerDeath MakePlayerDeath() => new(_assetManager, _gameEngineManager, _videoManager, _inputManager, _audioManager,
        _inventoryManager, _hudMessageManager, _mapManager, ui, frameRefresh, render, playerClassRules, playerStats, playerWeapons,
        statusBarDrawing, pushWalls, projectiles, playScreen, intermissionScreen, session, playArea,
        settings, new PlayerDeathHooks(() => gameInfoSettings, ShowObituary, DrawHudMessages, RestoreLevelStart, () => levelEntry.LevelStartItems()));

    internal static void A_StartDeathCam(Entities.Actors.Actor ob) => playerDeath.A_StartDeathCam(ob);
    internal static void T_DeathCam(Entities.Actors.Actor ob) => playerDeath.T_DeathCam(ob);
    static Entities.Actors.Actor? deathCamSprite => playerDeath.deathCamSprite;

    // ==== WL_PLAY ================================================================================

    internal static bool madenoise { get => session.madenoise; set => session.madenoise = value; }  // true when shooting or screaming

    internal static playstatetypes playstate { get => session.playstate; set => session.playstate = value; }

    internal static int DebugOk { get => session.DebugOk; set => session.DebugOk = value; }

    // The acting player's pawn and controls, and this frame's tics (Gameplay/GameSession.cs)
    internal static Entities.Actors.PlayerPawn player => session.player;

    //
    // control info
    //
    internal static bool mouseenabled { get => controlSettings.mouseenabled; set => controlSettings.mouseenabled = value; }
    internal static bool joystickenabled { get => controlSettings.joystickenabled; set => controlSettings.joystickenabled = value; }

    // The keys, mouse buttons and joystick buttons bound to each button and automap key (Configuration/GameSettings.cs)
    internal static ControlBindings controls => settings.controls;

    internal static int viewsize { get => settings.viewsize; set => settings.viewsize = value; }

    internal static bool demorecord { get => session.demorecord; set => session.demorecord = value; }
    internal static bool demoplayback { get => session.demoplayback; set => session.demoplayback = value; }
    // A demo's controls (Gameplay/DemoBuffer.cs)
    internal static readonly DemoBuffer demoBuffer = new();

    //
    // current user input: gathered each frame by PollControls into localcmd, which is all the
    // game itself reads (through the player's PlayerInput) (Gameplay/InputPolling.cs)
    //
    static InputPolling inputPolling = null!;       // made in InitServices

    static InputPolling MakeInputPolling() => new(_inputManager, _consoleManager, _automapManager, controls, controlSettings,
        frameRefresh, demoBuffer, session,
        new InputPollingHooks(() => netChatting, () => netLeavePrompt, () => demoTesting, RouteMovementToAutomap));

    static TicCmd localcmd => inputPolling.localcmd;

    // The mouse look and controller settings (Configuration/ControlSettings.cs)
    internal static ControlSettings controlSettings => settings.controlSettings;

    /*
    =============================================================================

                                                     LOCAL VARIABLES

    =============================================================================
    */

    // The level's song (Gameplay/LevelMusic.cs)
    static LevelMusic levelMusic = null!;       // made in InitServices

    // The keys during play (Ui/PlayKeys.cs)
    static PlayKeys playKeys = null!;       // made in InitServices

    static PlayKeys MakePlayKeys() => new(_assetManager, _videoManager, _inputManager, _audioManager, _graphicManager, _gameEngineManager,
        _consoleManager, _automapManager, _inventoryManager, menuDrawing, inputPolling, statusBarDrawing, playerWeapons, playerClassRules,
        levelMusic, frameRefresh, session,
        settings, new PlayKeysHooks(() => netChatting, NetChatKeys, NetCheckKeys,
            ToggleAutomap, HandleAutomapKey, DrawPlayBorder, DrawPlayBorderSides,
            US_ControlPanel, DrawPlayScreen));

    // Playing a level, a frame at a time (Gameplay/LevelLoop.cs)
    static LevelLoop levelLoop = null!;     // made in InitServices

    static LevelLoop MakeLevelLoop() => new(_inputManager, _videoManager, _consoleManager, _automapManager, _hudMessageManager, _mapManager,
        frameRefresh, inputPolling, playKeys, doors, pushWalls, timedSwitches, floorDamage, playerView, lighting, gameScripts, deathmatch, netPlay, levelAI, session,
        new LevelLoopHooks(() => demoTesting, TickPlayerBodies, UpdateSoundListener, UpdateAutomap, () => autosavePending, AutoSaveGame, () => zoneAmbience.Tick()));

    // Zones' ambient sounds (Gameplay/ZoneAmbience.cs)
    static ZoneAmbience? _zoneAmbience;
    static ZoneAmbience zoneAmbience => _zoneAmbience ??= new ZoneAmbience(session, new ZoneAmbienceHooks(
        () => gamestate.mapon is { } map && _gameEngineManager.GetGameInfo().Maps.TryGetValue(map, out var info) ? info : null,
        () => _gameEngineManager.GetGameInfo().DefaultMap,
        () => session.player is { } pawn ? (pawn.TileX, pawn.TileY) : null,
        (x, y) => _mapManager.GetZone(x, y),
        () => stealth.WearingDisguise(session.playerstate),
        _audioManager.SetAmbient));

    // ==== WL_DEF =================================================================================

    // The game's constants (Gameplay/GameConstants.cs)
    internal const int MAXTICS = GameConstants.MAXTICS;
    internal const int BIT_DOOR = GameConstants.BIT_DOOR;
    internal const int ANGLES = GameConstants.ANGLES;
    internal const int TEXTURESIZE = GameConstants.TEXTURESIZE;

    internal static void ClearMemory() => _audioManager.StopAll();

    // Sounds placed where things are, heard from the camera (Gameplay/PositionalSounds.cs)
    static PositionalSounds positionalSounds = null!;       // made in InitServices

    internal static void PlaySoundLocTile(string s, int tx, int ty) => positionalSounds.PlaySoundLocTile(s, tx, ty);
    internal static void PlaySoundLocActor(string s, Entities.Actors.Actor ob) => positionalSounds.PlaySoundLocActor(s, ob);
    internal static void UpdateSoundListener() => positionalSounds.UpdateSoundListener();

    // ==== SaveGame ===============================================================================

    /*
    =============================================================================

                                    SAVE GAMES

    The file around a save (header, thumbnail, checksum) is SaveGames/SaveFile.cs's. Its body
    holds the game (gamestate, level ratios, inventory), the level as it stands
    (WriteLevelBody), the weapon, and the hub clusters' levels kept as they were left
    (Gameplay/Hubs.cs). Saving and loading are SaveGames/GameSaves.cs; the level's part,
    SaveGames/LevelBodies.cs.

    =============================================================================
    */

    // Saving and loading the game, the autosave and the quick keys (SaveGames/GameSaves.cs)
    static GameSaves gameSaves = null!;     // made in InitServices

    static GameSaves MakeGameSaves() => new(_gameEngineManager, _assetManager, _videoManager, _inputManager, _graphicManager,
        _inventoryManager, _mapManager, menuDrawing, playScreen, levelBodies, levelSetup, levelMusic, playersManager, weaponInHand,
        playerClassRules, playerStats, hubs, settings, session,
        new GameSavesHooks(saving => loadSaveScreen.Run(saving),
            () =>
            {
                StartGame = 1;
                //
                // CHANGE "READ THIS!" TO NORMAL COLOR
                //
                FindMenuItem(MainMenu, "readthis")?.active = 1;
            }));

    static bool autosavePending { get => gameSaves.autosavePending; set => gameSaves.autosavePending = value; }

    internal static void AutoSaveGame() => gameSaves.AutoSaveGame();
    internal static string FormatPlayTime(int tics) => SaveFile.FormatPlayTime(tics);

    // The menu's load and save actions: menudefs name them, so they keep their names
    internal static int CP_LoadGame(int quick) => gameSaves.CP_LoadGame(quick);
    internal static int CP_SaveGame(int quick) => gameSaves.CP_SaveGame(quick);

    // The load and save screens (Ui/LoadSaveScreen.cs, made in InitServices)
    static LoadSaveScreen loadSaveScreen = null!;
    private static LoadSaveHooks MakeLoadSaveHooks() => gameSaves.MakeLoadSaveHooks();

    // The level as it stands, as saves, hub levels and joining players hold it (SaveGames/LevelBodies.cs)
    static LevelBodies levelBodies = null!;     // made in InitServices

    static void WriteLevelBody(BinaryWriter bw) => levelBodies.WriteLevelBody(bw);
    static LevelBody ReadLevelBody(BinaryReader br) => levelBodies.ReadLevelBody(br);
    static List<Entities.Actors.Actor> ApplyLevelBody(LevelBody body, Func<int, Entities.PlayerState?>? ownerOf = null) =>
        levelBodies.ApplyLevelBody(body, ownerOf);

    // saves, autosave, save and load (Commands/SaveCommands.cs)
    static SaveCommands saveCommands = null!;       // made in InitServices

    // ==== Hubs ===================================================================================

    /*
    =============================================================================

                                    HUB CLUSTERS

    The hub state (the levels kept, the floors been on or unlocked, their stats) is
    Gameplay/Hubs.cs (made in InitServices); these forward to it. Entering and leaving a level,
    and the level start death goes back to, are Gameplay/LevelEntry.cs.

    =============================================================================
    */

    static Hubs hubs = null!;

    // The floor-select and teleporter screens (Ui/ElevatorScreens.cs, made in InitServices)
    static ElevatorScreens elevatorScreens = null!;

    // The level's overhead map, as the teleporter shows it
    internal static byte[] CaptureOverhead() => elevatorScreens.CaptureOverhead();

    static int FloorScore() => hubs.FloorScore();

    // Entering and leaving levels, and the level start death goes back to (Gameplay/LevelEntry.cs)
    static LevelEntry levelEntry = null!;       // made in InitServices

    static LevelEntry MakeLevelEntry() => new(_mapManager, _inventoryManager, hubs, levelSetup, playerMovement, session,
        new LevelEntryHooks(() => victoryRunning = false, CaptureOverhead,
            WriteLevelBody, ReadLevelBody, body => ApplyLevelBody(body)));

    internal static bool EnterLevel() => levelEntry.EnterLevel();
    internal static void LeaveLevel() => levelEntry.LeaveLevel();
    internal static void MarkLevelStart() => levelEntry.MarkLevelStart();
    internal static bool RestoreLevelStart() => levelEntry.RestoreLevelStart();
    static void AlignPlayerInElevator(int lastx, int lasty, int panelWall) => levelEntry.AlignPlayerInElevator(lastx, lasty, panelWall);

    // The hub clusters' trigger actions: elevator, teleporters, unlocking floors, the detonator
    // and the victory run (Gameplay/HubActions.cs)
    static HubActions hubActions = null!;       // made in InitServices (or by RegisterActorActions without it)

    static HubActions MakeHubActions() => new(_gameEngineManager, _mapManager, _audioManager, _inputManager, _inventoryManager,
        _hudMessageManager, hubs, elevatorScreens, playScreen, frameRefresh, playerMovement, session,
        new HubActionsHooks(change => pendingMapChange = change, PlaySoundLocActor));

    internal static bool victoryRunning { get => hubActions.victoryRunning; set => hubActions.victoryRunning = value; }

    // ==== Scripting ==============================================================================

    // The Lua scripts and the API they get (Scripting/GameScripts.cs)
    static GameScripts gameScripts = null!;     // made in InitServices

    static GameScripts MakeGameScripts() =>
        new(_assetManager, _mapManager, _audioManager, _hudMessageManager, _consoleManager, _translationManager,
            projectileActions ??= MakeProjectileActions(), session, PlaySoundLocActor)
        {
            Unlock = Unlock,
            IsUnlocked = name => unlocks.IsUnlocked(name),
            Record = map => levelRecords.Get(map),
        };

    // The script API's tests install it with no services: it's made then
    internal static void InstallScriptApi(ScriptHost host) => (gameScripts ??= MakeGameScripts()).InstallScriptApi(host);

    static ScriptHost? _scripts => gameScripts?.Scripts;
    static void LoadScripts() => gameScripts.LoadScripts();
    static void RunLevelHooks(string reason) => gameScripts.RunLevelHooks(reason);
    static void Cmd_Scripts(string[] args) => gameScripts.Cmd_Scripts(args);
    static void Cmd_Lua(string[] args) => gameScripts.Cmd_Lua(args);
}
