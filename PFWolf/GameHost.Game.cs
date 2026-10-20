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
    internal static GameState GameState { get => Session.GameState; set => Session.GameState = value; }
    static string BorderCol { get => Session.BorderCol; set => Session.BorderCol = value; } // color of the Change View/Ingame border

    // The game from its first level to its end (Gameplay/MainGameLoop.cs)
    static MainGameLoop mainGameLoop = null!;       // made in InitServices

    static MainGameLoop MakeMainGameLoop() => new(GameEngineManager, AssetManager, _videoManager, AudioManager, _inputManager,
        InventoryManager, statusBarDrawing, playScreen, levelMusic, levelLoop, playerMovement, playerDeath, demos, deathmatch, scoreboard,
        netPlay, gameScripts, hubs, controlPanel, intermissionScreen, highScoresScreen, textPresenter, moviePlayer, Session,
        Settings, new MainGameLoopHooks(on => AutosavePending = on, EnterLevel, LeaveLevel,
            MarkLevelStart, ApplyPendingWarpStart, AlignPlayerInElevator, ShowLevelStartMessage, name => articleReader.ShowText(name),
            LevelWon));

    static PendingMapChange? PendingMapChange { get => mainGameLoop.PendingMapChange; set => mainGameLoop.PendingMapChange = value; }

    // Demos: played, recorded and asked for from the console (Gameplay/Demos.cs)
    static Demos demos = null!;     // made in InitServices

    static Demos MakeDemos() => new(AssetManager, GameEngineManager, _videoManager, AudioManager, ui, lineInput, DemoBuffer, levelMusic, levelLoop,
        Session, new DemosHooks((skill, episode, map, playerClass) => NewGame(skill, episode, map, playerClass), () => DemoSkill, DrawPlayScreen,
            SetupGameLevel, RunLevelHooks, () => DemoTesting,
            DemoTestReport, () => IntroSong));

    internal static void PlayDemo(int demonumber) => demos.PlayDemo(demonumber);
    internal static bool DemoExists(int demonumber) => demos.DemoExists(demonumber);

    // playdemo, demotest, demotrace and recorddemo (Commands/DemoCommands.cs)
    static DemoCommands demoCommands = null!;       // made in InitServices

    // What demotrace writes down as a demo plays (Gameplay/DemoTracer.cs)
    static DemoTracer demoTracer = null!;       // made in InitServices

    /// <summary>Set while demotest runs: demos play without waiting, and each prints its report as it ends</summary>
    internal static bool DemoTesting { get => demoCommands.DemoTesting; set => demoCommands.DemoTesting = value; }
    internal static string DemoTestReport(int demonumber) => demoCommands.DemoTestReport(demonumber);

    // The screen around the view: status bar, border and "get psyched" (Ui/PlayScreen.cs)
    static PlayScreen playScreen = null!;       // made in InitServices

    static PlayScreen MakePlayScreen() => new(AssetManager, GameEngineManager, _videoManager, _graphicManager, _inputManager,
        statusBarDrawing, playerWeapons, radar, Render, playArea,
        Settings, Session, new PlayScreenHooks(PresenterMessageBox));

    internal static void DrawPlayScreen() => playScreen.DrawPlayScreen();
    internal static void DrawPlayBorder() => playScreen.DrawPlayBorder();
    internal static void DrawPlayBorderSides() => playScreen.DrawPlayBorderSides();
    internal static void DrawStatusBorder(string color) => playScreen.DrawStatusBorder(color);
    internal static string GetMapDisplayName(string mapon) => playScreen.GetMapDisplayName(mapon);
    internal static void ShowLevelStartMessage(string script) => playScreen.ShowLevelStartMessage(script);

    // Setting up a level to play (Gameplay/LevelSetup.cs)
    static LevelSetup levelSetup = null!;       // made in InitServices

    static LevelSetup MakeLevelSetup() => new(GameEngineManager, MapManager, Render, skyAndFlats, weather, lighting, doors, pushWalls,
        deathmatch, playersManager, playerClassRules, levelLoop, WeaponBob, LevelAI, Session,
        new LevelSetupHooks(() => CurrentSkill,
            loadout => MapLoadouts.Apply(loadout), () => Objectives.CountLevel(announce: !DemoPlayback && !DemoRecord, playerUse.ShowUnlockedSwitches(name => Unlocks.IsUnlocked(name)))));

    // Level objectives (Gameplay/Objectives.cs): sabotage targets, goal enemies
    static Objectives? _objectives;
    static Objectives Objectives => _objectives ??= new Objectives(MapManager, new ObjectiveHooks(
        () => GameEngineManager.GetGameInfo().Objectives,
        text => HudMessageManager.Localize(text),
        message => HudMessageManager.Show(HudMessageKind.Lock, message),
        LevelSwitches));

    // What the player has unlocked, from game to game (Gameplay/Unlocks.cs): starts-locked episodes
    static Unlocks? _unlocks;
    internal static Unlocks Unlocks => _unlocks ??= new Unlocks(() => GameEngineManager.GetConfigFilePath(Unlocks.FileName),
        () => GameEngineManager.GetGameInfo());

    /// <summary>A_Unlock, game.unlock: unlocks a name, but not while a demo plays back</summary>
    internal static bool Unlock(string name) => !DemoPlayback && Unlocks.Unlock(name);

    // The best done on each map, from game to game (Gameplay/LevelRecords.cs)
    static LevelRecords? _levelRecords;
    internal static LevelRecords LevelRecords => _levelRecords ??= new LevelRecords(() => GameEngineManager.GetConfigFilePath(LevelRecords.FileName));

    /// <summary>
    /// A level's finished, before its tally: its record is bettered and the scripts' level-end
    /// hooks run (which may unlock what it earned). Not while a demo plays back.
    /// </summary>
    static void LevelWon(PlayState how)
    {
        PreviousLevelRecord = null;
        if (DemoPlayback || GameState.MapOn is not { Length: > 0 } map)
            return;
        var result = CurrentLevelResult(map);
        PreviousLevelRecord = LevelRecords.Get(map);
        LevelRecords.Record(map, result);
        gameScripts.RunLevelEndHooks(map, result, how switch
        {
            PlayState.SecretLevel => "secret",
            PlayState.Victorious => "victory",
            _ => "exit",
        });
    }

    /// <summary>The finished level's best before this finish bettered it (the level-end screen's silver stars)</summary>
    internal static LevelResult? PreviousLevelRecord { get; private set; }

    /// <summary>What the level being played has come to so far</summary>
    internal static LevelResult CurrentLevelResult(string map)
    {
        var gameInfo = GameEngineManager.GetGameInfo();
        int par = gameInfo.Maps.TryGetValue(map, out var info) ? info.ParTime : 0;
        // game-info stealth unseen-no-kills-counts-all: an infiltration counts every kill
        bool allKills = gameInfo.Stealth.UnseenNoKillsCountsAll && IntermissionScreen.UnseenNoKills(Session.PlayerState, GameState);
        return new LevelResult
        {
            Kills = allKills ? GameState.KillTotal : GameState.KillCount, KillTotal = GameState.KillTotal,
            Secrets = GameState.SecretCount, SecretTotal = GameState.SecretTotal,
            Items = GameState.TreasureCount, ItemTotal = GameState.TreasureTotal,
            Time = GameState.TimeCount,
            Par = par,
            UnderPar = par > 0 && GameState.TimeCount < par * 70,
            Undetected = Session.PlayerState.Undetected,
            StealthKills = Session.PlayerState.StealthKills,
            Objectives = MapManager.ObjectiveCounts.ToDictionary(o => o.Key, o => (o.Value.Done, o.Value.Total), StringComparer.OrdinalIgnoreCase),
        };
    }

    // Every switch on the level: its switch walls' and its patches'
    static IEnumerable<MapSwitchTranslation> LevelSwitches()
    {
        var data = MapManager.GetMapData();
        for (int y = 0; y < MapManager.MapHeight; y++)
            for (int x = 0; x < MapManager.MapWidth; x++)
            {
                int tile = MapManager.TileMap[x, y];
                if (tile != 0 && (tile & GameConstants.BitDoor) == 0
                    && data.Walls.TryGetValue(tile & ~GameConstants.BitWall, out var wall) && wall.Switch is { } wallSwitch)
                    yield return wallSwitch;
                if (MapManager.GetPatch(x, y) is var patch and > 0 && data.Patches.TryGetValue(patch, out var patchDef)
                    && patchDef.Switch is { } patchSwitch)
                    yield return patchSwitch;
            }
    }

    // A map's starting loadout (Gameplay/MapLoadouts.cs)
    static MapLoadouts? _mapLoadouts;
    static MapLoadouts MapLoadouts => _mapLoadouts ??= new MapLoadouts(InventoryManager, playerWeapons, Session);

    internal static void SetupGameLevel() => levelSetup.SetupGameLevel();

    // A player's death, and the death cam (Gameplay/PlayerDeath.cs)
    static PlayerDeath playerDeath = null!;     // made in InitServices

    static PlayerDeath MakePlayerDeath() => new(AssetManager, GameEngineManager, _videoManager, _inputManager, AudioManager,
        InventoryManager, HudMessageManager, MapManager, ui, frameRefresh, Render, playerClassRules, playerStats, playerWeapons,
        statusBarDrawing, pushWalls, projectiles, playScreen, intermissionScreen, Session, playArea,
        Settings, new PlayerDeathHooks(() => gameInfoSettings, ShowObituary, DrawHudMessages, RestoreLevelStart, () => levelEntry.LevelStartItems()));

    internal static void StartDeathCam(Entities.Actors.Actor ob) => playerDeath.StartDeathCam(ob);
    internal static void ThinkDeathCam(Entities.Actors.Actor ob) => playerDeath.ThinkDeathCam(ob);
    static Entities.Actors.Actor? DeathCamSprite => playerDeath.DeathCamSprite;

    // ==== WL_PLAY ================================================================================

    internal static bool MadeNoise { get => Session.MadeNoise; set => Session.MadeNoise = value; }  // true when shooting or screaming

    internal static PlayState PlayState { get => Session.PlayState; set => Session.PlayState = value; }

    internal static int DebugOk { get => Session.DebugOk; set => Session.DebugOk = value; }

    // The acting player's pawn and controls, and this frame's tics (Gameplay/GameSession.cs)
    internal static Entities.Actors.PlayerPawn Player => Session.Player;

    //
    // control info
    //
    internal static bool MouseEnabled { get => ControlSettings.MouseEnabled; set => ControlSettings.MouseEnabled = value; }
    internal static bool JoystickEnabled { get => ControlSettings.JoystickEnabled; set => ControlSettings.JoystickEnabled = value; }

    // The keys, mouse buttons and joystick buttons bound to each button and automap key (Configuration/GameSettings.cs)
    internal static ControlBindings Controls => Settings.Controls;

    internal static int ViewSize { get => Settings.ViewSize; set => Settings.ViewSize = value; }

    internal static bool DemoRecord { get => Session.DemoRecord; set => Session.DemoRecord = value; }
    internal static bool DemoPlayback { get => Session.DemoPlayback; set => Session.DemoPlayback = value; }
    // A demo's controls (Gameplay/DemoBuffer.cs)
    internal static readonly DemoBuffer DemoBuffer = new();

    //
    // current user input: gathered each frame by PollControls into LocalCmd, which is all the
    // game itself reads (through the player's PlayerInput) (Gameplay/InputPolling.cs)
    //
    static InputPolling inputPolling = null!;       // made in InitServices

    static InputPolling MakeInputPolling() => new(_inputManager, _consoleManager, _automapManager, Controls, ControlSettings,
        frameRefresh, DemoBuffer, Session,
        new InputPollingHooks(() => NetChatting, () => NetLeavePrompt, () => DemoTesting, RouteMovementToAutomap), Settings.Options);

    static TicCmd LocalCmd => inputPolling.LocalCmd;

    // The mouse look and controller settings (Configuration/ControlSettings.cs)
    internal static ControlSettings ControlSettings => Settings.ControlSettings;

    /*
    =============================================================================

                                                     LOCAL VARIABLES

    =============================================================================
    */

    // The level's song (Gameplay/LevelMusic.cs)
    static LevelMusic levelMusic = null!;       // made in InitServices

    // The keys during play (Ui/PlayKeys.cs)
    static PlayKeys playKeys = null!;       // made in InitServices

    static PlayKeys MakePlayKeys() => new(AssetManager, _videoManager, _inputManager, AudioManager, _graphicManager, GameEngineManager,
        _consoleManager, _automapManager, InventoryManager, menuDrawing, inputPolling, statusBarDrawing, playerWeapons, playerClassRules,
        levelMusic, frameRefresh, Session,
        Settings, new PlayKeysHooks(() => NetChatting, NetChatKeys, NetCheckKeys,
            ToggleAutomap, HandleAutomapKey, DrawPlayBorder, DrawPlayBorderSides,
            OpenControlPanel, DrawPlayScreen));

    // Playing a level, a frame at a time (Gameplay/LevelLoop.cs)
    static LevelLoop levelLoop = null!;     // made in InitServices

    static LevelLoop MakeLevelLoop() => new(_inputManager, _videoManager, _consoleManager, _automapManager, HudMessageManager, MapManager,
        frameRefresh, inputPolling, playKeys, doors, pushWalls, TimedSwitches, FloorDamage, playerView, lighting, gameScripts, deathmatch, netPlay, LevelAI, Session,
        new LevelLoopHooks(() => DemoTesting, TickPlayerBodies, UpdateSoundListener, UpdateAutomap, () => AutosavePending, AutoSaveGame, () => ZoneAmbience.Tick(),
            () => demoTracer.AfterFrame(), () => LevelTimer.Tick()));

    // A level against the clock (Gameplay/LevelTimer.cs): game-info map time-limit
    static LevelTimer? _levelTimer;
    static LevelTimer LevelTimer => _levelTimer ??= new LevelTimer(Session, currentMapInfo, new LevelTimerHooks(
        () =>
        {
            // A level that time-limit-completes is got through by lasting (End of Destiny's)
            if (GameState.MapOn is { } timedMap && GameEngineManager.GetGameInfo().Maps.TryGetValue(timedMap, out var timedInfo)
                && timedInfo.TimeLimitCompletes)
            {
                PlayState = PlayState.Completed;
                return;
            }

            // Time's up: every player still standing dies, with nothing to blame
            foreach (var p in Session.Players.Where(p => p.Health > 0).ToList())
            {
                using var _ = Session.ActAs(p);
                p.LastAttacker = null;
                p.Health = 0;
                statusBarDrawing.DrawHealth();
                if (Session.GameMode == GameMode.Single)
                    PlayState = PlayState.Died;
                else
                    PlayerDies();
            }
        },
        () => { if (!statusBarDrawing.IsOverlay) statusBarDrawing.DrawLevelTimer(); }));

    // Zones' ambient sounds (Gameplay/ZoneAmbience.cs)
    static ZoneAmbience? _zoneAmbience;
    static ZoneAmbience ZoneAmbience => _zoneAmbience ??= new ZoneAmbience(Session, currentMapInfo, new ZoneAmbienceHooks(
        (x, y) => MapManager.GetZone(x, y),
        () => Stealth.WearingDisguise(Session.PlayerState),
        AudioManager.SetAmbient));

    // ==== WL_DEF =================================================================================

    // The game's constants (Gameplay/GameConstants.cs)
    internal const int MaxTics = GameConstants.MaxTics;
    internal const int BitDoor = GameConstants.BitDoor;
    internal const int Angles = GameConstants.Angles;
    internal const int TextureSize = GameConstants.TextureSize;

    internal static void ClearMemory() => AudioManager.StopAll();

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
    holds the game (GameState, level ratios, inventory), the level as it stands
    (WriteLevelBody), the weapon, and the hub clusters' levels kept as they were left
    (Gameplay/Hubs.cs). Saving and loading are SaveGames/GameSaves.cs; the level's part,
    SaveGames/LevelBodies.cs.

    =============================================================================
    */

    // Saving and loading the game, the autosave and the quick keys (SaveGames/GameSaves.cs)
    static GameSaves gameSaves = null!;     // made in InitServices

    static GameSaves MakeGameSaves() => new(GameEngineManager, AssetManager, _videoManager, _inputManager, _graphicManager,
        InventoryManager, MapManager, menuDrawing, playScreen, levelBodies, levelSetup, levelMusic, playersManager, weaponInHand,
        playerClassRules, playerStats, hubs, Settings, Session,
        new GameSavesHooks(saving => loadSaveScreen.Run(saving),
            () =>
            {
                StartGame = 1;
                //
                // CHANGE "READ THIS!" TO NORMAL COLOR
                //
                FindMenuItem(MainMenu, "readthis")?.Active = 1;
            }))
    {
        TimedSwitches = TimedSwitches,
    };

    static bool AutosavePending { get => gameSaves.AutosavePending; set => gameSaves.AutosavePending = value; }

    internal static void AutoSaveGame() => gameSaves.AutoSaveGame();
    internal static string FormatPlayTime(int tics) => SaveFile.FormatPlayTime(tics);

    // The menu's load and save actions: menudefs name them, so they keep their names
    internal static int RunLoadGame(int quick) => gameSaves.RunLoadGame(quick);
    internal static int RunSaveGame(int quick) => gameSaves.RunSaveGame(quick);

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

    static LevelEntry MakeLevelEntry() => new(MapManager, InventoryManager, hubs, levelSetup, playerMovement, Session,
        new LevelEntryHooks(() => VictoryRunning = false, CaptureOverhead,
            WriteLevelBody, ReadLevelBody, body => ApplyLevelBody(body)));

    internal static bool EnterLevel() => levelEntry.EnterLevel();
    internal static void LeaveLevel() => levelEntry.LeaveLevel();
    internal static void MarkLevelStart() => levelEntry.MarkLevelStart();
    internal static bool RestoreLevelStart() => levelEntry.RestoreLevelStart();
    static void AlignPlayerInElevator(int lastx, int lasty, int panelWall) => levelEntry.AlignPlayerInElevator(lastx, lasty, panelWall);

    // The hub clusters' trigger actions: elevator, teleporters, unlocking floors, the detonator
    // and the victory run (Gameplay/HubActions.cs)
    static HubActions hubActions = null!;       // made in InitServices (or by RegisterActorActions without it)

    static HubActions MakeHubActions() => new(GameEngineManager, MapManager, AudioManager, _inputManager, InventoryManager,
        HudMessageManager, hubs, elevatorScreens, playScreen, frameRefresh, playerMovement, Session,
        new HubActionsHooks(change => PendingMapChange = change, PlaySoundLocActor));

    internal static bool VictoryRunning { get => hubActions.VictoryRunning; set => hubActions.VictoryRunning = value; }

    // ==== Scripting ==============================================================================

    // The Lua scripts and the API they get (Scripting/GameScripts.cs)
    static GameScripts gameScripts = null!;     // made in InitServices

    static GameScripts MakeGameScripts() =>
        new(AssetManager, MapManager, AudioManager, HudMessageManager, _consoleManager, TranslationManager,
            projectileActions ??= MakeProjectileActions(), Session, PlaySoundLocActor)
        {
            Unlock = Unlock,
            IsUnlocked = name => Unlocks.IsUnlocked(name),
            Record = map => LevelRecords.Get(map),
            ShowText = ShowTextInLevel,
        };

    // The script API's tests install it with no services: it's made then
    internal static void InstallScriptApi(ScriptHost host) => (gameScripts ??= MakeGameScripts()).InstallScriptApi(host);

    static ScriptHost? Scripts => gameScripts?.Scripts;
    static void LoadScripts() => gameScripts.LoadScripts();
    static void RunLevelHooks(string reason) => gameScripts.RunLevelHooks(reason);
    static void ScriptsCommand(string[] args) => gameScripts.ScriptsCommand(args);
    static void LuaCommand(string[] args) => gameScripts.LuaCommand(args);
}
