using SDL2;
using PFWolf.Assets;
using PFWolf.Extensions;
using PFWolf.Managers;

namespace PFWolf;

internal static partial class GameHost
{
    /*
=============================================================================

                             GLOBAL VARIABLES

=============================================================================
*/
    static bool ingame, fizzlein;
    internal static gametype gamestate { get => session.gamestate; set => session.gamestate = value; }
    static string bordercol = "VIEWCOLOR"; // color of the Change View/Ingame border

    // The game from its first level to its end (Gameplay/MainGameLoop.cs)
    static MainGameLoop mainGameLoop = null!;       // made in InitServices

    static MainGameLoop MakeMainGameLoop() => new(_gameEngineManager, _assetManager, _videoManager, _audioManager, _inputManager,
        _inventoryManager, statusBarDrawing, playScreen, levelMusic, levelLoop, playerMovement, playerDeath, demos, deathmatch, scoreboard,
        netPlay, gameScripts, hubs, controlPanel, intermissionScreen, highScoresScreen, textPresenter, moviePlayer, session,
        new MainGameLoopHooks(() => playstate, s => playstate = s, () => startgame, on => startgame = on, () => loadedgame,
            on => loadedgame = on, on => ingame = on, () => fizzlein = true, on => autosavePending = on, () => viewsize,
            () => demoplayback || demorecord, () => demorecord, () => netgame, () => lastgamemusicoffset, EnterLevel, LeaveLevel,
            MarkLevelStart, ApplyPendingWarpStart, AlignPlayerInElevator, ShowLevelStartMessage, name => articleReader.ShowText(name),
            LevelWon));

    static PendingMapChange? pendingMapChange { get => mainGameLoop.pendingMapChange; set => mainGameLoop.pendingMapChange = value; }

    // Demos: played, recorded and asked for from the console (Gameplay/Demos.cs)
    static Demos demos = null!;     // made in InitServices

    static Demos MakeDemos() => new(_assetManager, _gameEngineManager, _videoManager, _audioManager, ui, lineInput, demoBuffer, levelMusic, levelLoop,
        new DemosHooks((skill, episode, map, playerClass) => NewGame(skill, episode, map, playerClass), () => DemoSkill, DrawPlayScreen,
            SetupGameLevel, RunLevelHooks, () => startgame = false, on => demoplayback = on, on => demorecord = on, () => demoTesting,
            DemoTestReport, () => fizzlein = true, () => INTROSONG));

    internal static void PlayDemo(int demonumber) => demos.PlayDemo(demonumber);
    internal static bool DemoExists(int demonumber) => demos.DemoExists(demonumber);
    internal static void RecordDemo() => demos.RecordDemo();
    internal static void RecordDemo(int level, int? demonumber = null) => demos.RecordDemo(level, demonumber);

    // playdemo, demotest and recorddemo (Commands/DemoCommands.cs)
    static DemoCommands demoCommands = null!;       // made in InitServices

    /// <summary>Set while demotest runs: demos play without waiting, and each prints its report as it ends</summary>
    internal static bool demoTesting { get => demoCommands.demoTesting; set => demoCommands.demoTesting = value; }
    internal static string DemoTestReport(int demonumber) => demoCommands.DemoTestReport(demonumber);

    // The screen around the view: status bar, border and "get psyched" (Ui/PlayScreen.cs)
    static PlayScreen playScreen = null!;       // made in InitServices

    static PlayScreen MakePlayScreen() => new(_assetManager, _gameEngineManager, _videoManager, _graphicManager, _inputManager,
        statusBarDrawing, playerWeapons, radar, render, playArea,
        new PlayScreenHooks(() => viewsize, () => ingame, () => bordercol, PresenterMessageBox));

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
        new LevelSetupHooks(() => loadedgame, () => demoplayback || demorecord, () => netgame, () => netseed, () => CurrentSkill,
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
        new PlayerDeathHooks(() => gameInfoSettings, ShowObituary, DrawHudMessages, RestoreLevelStart, () => levelEntry.LevelStartItems(), s => playstate = s,
            () => viewsize, () => bordercol, () => fizzlein = true));

    internal static void A_StartDeathCam(Entities.Actors.Actor ob) => playerDeath.A_StartDeathCam(ob);
    internal static void T_DeathCam(Entities.Actors.Actor ob) => playerDeath.T_DeathCam(ob);
    static Entities.Actors.Actor? deathCamSprite => playerDeath.deathCamSprite;
}
