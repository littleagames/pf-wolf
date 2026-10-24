using PFWolf.Assets;
using PFWolf.Rendering;

namespace PFWolf.Gameplay;

/// <summary>What setting up a level needs from the game</summary>
/// <param name="CurrentSkill">The skill being played (its enemy-health)</param>
/// <param name="ApplyLoadout">Gives the players the level's starting loadout (Gameplay/MapLoadouts.cs)</param>
/// <param name="CountObjectives">Counts the level's objectives and announces them (Gameplay/Objectives.cs)</param>
internal sealed record LevelSetupHooks(
    Func<SkillInfo> CurrentSkill,
    Action<MapLoadout?> ApplyLoadout,
    Action CountObjectives);

/// <summary>
/// Setting up a level to play (SetupGameLevel): the players' view and the level's counts reset,
/// the map loaded with its wall height, sky, weather and flats, then the doors and players
/// spawned, the ambush markers taken out, and its shading and level AI set up.
/// </summary>
internal sealed class LevelSetup(
    GameEngineManager engine,
    MapManager map,
    RenderState render,
    SkyAndFlats skyAndFlats,
    Weather weather,
    Lighting lighting,
    Doors doorObjects,
    PushWalls pushWalls,
    Deathmatch deathmatch,
    Players playersManager,
    PlayerClassRules classes,
    LevelLoop loop,
    WeaponBob weaponBob,
    LevelAI levelAI,
    GameSession session,
    LevelSetupHooks hooks)
{
    internal void SetupGameLevel()
    {
        foreach (var p in session.Players)
        {
            p.Pitch = 0;                    // each level, and each loaded game, starts looking straight ahead
            p.EyeZ = classes.StandingEyeZ(p);       // from standing height
        }
        session.Camera.FollowPlayer();              // through the player's eyes
        weaponBob.Init();
        session.NoShots = false;                    // the weapon can fire (A_PlayerFire)
        session.NoiseLastTic = false;               // no noise carried over from the last level

        if (!session.LoadedGame)
        {
            session.GameState.TimeCount =
            session.GameState.SecretTotal =
            session.GameState.KillTotal =
            session.GameState.TreasureTotal =
            session.GameState.SecretCount =
            session.GameState.KillCount =
            session.GameState.TreasureCount = 0;
            pushWalls.ClearPushWalls();
            foreach (var p in session.Players)
            {
                p.WeaponSprite = null;      // the weapon in hand starts on its Ready state
                p.FaceTimes = 0;
                p.LastAttacker = null;
                (p.Undetected, p.StealthKills, p.SuspicionMeter, p.SuspicionPending) = (true, 0, 0, 0);   // Gameplay/Stealth.cs
                (p.KillChain, p.KillChainAt) = (0, 0);      // Gameplay/KillChains.cs
            }
        }

        if ((session.DemoPlayback || session.DemoRecord))
            session.Random.InitRndT(false);
        else if (session.NetGame)
            session.Random.RndIndex = session.NetSeed;         // where the host said, the same on every machine
        else
            session.Random.InitRndT(true);

        //
        // load the level
        //
        deathmatch.DeathmatchLevelSetup();         // with or without its enemies (Gameplay/Deathmatch.cs)
        //int mapnum = GameState.MapOn + 10 * GameState.cluster;
        map.LoadMap(session.GameState.MapOn, session.GameState.Difficulty, hooks.CurrentSkill().EnemyHealth,
            CurrentMapInfo.For(session, engine.GetGameInfo())?.FloorNumber ?? -1);

        // The cluster follows the map, however it was reached: a new game, the next/secret
        // level, a map-change trigger or the console's "map" warp
        var mapInfo = CurrentMapInfo.For(session, engine.GetGameInfo());
        if (mapInfo != null)
            session.GameState.Cluster = mapInfo.Cluster;

        // The level's game-info entry, then what its own file says (a UWMF level's), then the default map's
        map.LevelWallStories = Math.Clamp(mapInfo?.WallHeight ?? map.LevelDefaults.WallStories ?? engine.GetGameInfo().DefaultMap.WallHeight,
            1, GameConstants.MaxWallStories);
        skyAndFlats.LevelSky = mapInfo?.Sky ?? map.LevelDefaults.Sky ?? engine.GetGameInfo().DefaultMap.Sky;
        // the floor sky and how it's drawn come together, from the map or else the default map
        (skyAndFlats.LevelFloorSky, skyAndFlats.FloorSkyMirror) = mapInfo?.FloorSky is { } floorSky
            ? (floorSky, mapInfo.FloorSkyMirror)
            : (engine.GetGameInfo().DefaultMap.FloorSky, engine.GetGameInfo().DefaultMap.FloorSkyMirror);
        skyAndFlats.SetGeneratedSky(mapInfo?.StarSky ?? engine.GetGameInfo().DefaultMap.StarSky,
            mapInfo?.CloudSky ?? engine.GetGameInfo().DefaultMap.CloudSky);
        weather.SetWeather(mapInfo?.Weather ?? engine.GetGameInfo().DefaultMap.Weather);
        map.BuildFlats(mapInfo?.DefaultFloor ?? engine.GetGameInfo().DefaultMap.DefaultFloor,
            mapInfo?.DefaultCeiling ?? engine.GetGameInfo().DefaultMap.DefaultCeiling);

        //
        // spawn doors
        //
        loop.InitActorList();                       // start spawning things with a clean slate
        doorObjects.InitDoorList();
        //InitStaticList();


        int x, y;
        var doors = map.GetMapData().Doors;
        for (y = 0; y < map.MapHeight; y++)
        {
            for (x = 0; x < map.MapWidth; x++)
            {
                // door (a plane 0 tile listed in the mapdefs doors)
                if (doors.TryGetValue(map.MapSpot(x, y, 0), out var doorXlat))
                    doorObjects.SpawnDoor(x, y, doorXlat.Vertical, doorXlat);
            }
        }

        // the mode's flags, control points or waves (Gameplay/Objectives), before the players
        // come in near their team's flag
        deathmatch.ObjectiveLevelSetup();

        //
        // spawn the player (MapManager.LoadMap already spawned every other thing and counted
        // the secret MovingWalls, from the mapdefs)
        //
        if (map.PlayerStart is { } start)
            playersManager.SpawnPlayers(start.TileX, start.TileY, start.Angle);
        else
        {
            var (standx, standy) = playersManager.StandInStart();
            Console.WriteLine($"{session.GameState.MapOn} has no mapdefs player start; starting on {standx},{standy}");
            playersManager.SpawnPlayers(standx, standy, 90);
        }

        //
        // take out the ambush markers
        //
        for (y = 0; y < map.MapHeight; y++)
        {
            for (x = 0; x < map.MapWidth; x++)
            {
                var tile = map.MapSpot(x, y, 0);

                if (tile == map.Floors.AmbushTile)
                {
                    if (map.ValidArea(map.MapSpot(x + 1, y, 0)))
                        tile = (ushort)map.MapSpot(x + 1, y, 0);
                    if (map.ValidArea(map.MapSpot(x, y - 1, 0)))
                        tile = (ushort)map.MapSpot(x, y - 1, 0);
                    if (map.ValidArea(map.MapSpot(x, y + 1, 0)))
                        tile = (ushort)map.MapSpot(x, y + 1, 0);
                    if (map.ValidArea(map.MapSpot(x - 1, y, 0)))
                        tile = (ushort)map.MapSpot(x - 1, y, 0);

                    map.SetMapSpot(x, y, 1, 0);
                }
            }
        }

        lighting.InitLevelShading(mapInfo);
        map.Scrollers.InitLevel(mapInfo);       // scrolling walls and flats (Rendering/Scrollers.cs)
        levelAI.OnLevelStart();      // wall outlets, Goldfire's spawn sites, once-a-level drops (Managers.LevelAI)

        // the level's starting loadout, as it starts afresh (game-info map loadout)
        if (!session.LoadedGame)
        {
            hooks.ApplyLoadout(mapInfo?.Loadout);
            hooks.CountObjectives();        // its objectives, counted afresh (a loaded game has its own)
        }

        //
        // load floor/ceiling textures
        //
#if USE_FLOORCEILINGTEXT && !USE_MULTIFLATS
        GetFlatTextures();
#endif

#if USE_PARALLAX
    SetParallaxStartTexture();
#endif
        //
        // have the caching manager load and purge stuff to make sure all marks
        // are in memory
        //
        //CA_LoadAllSounds();
    }
}
