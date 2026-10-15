using PFWolf.Assets;
using PFWolf.Rendering;

namespace PFWolf.Gameplay;

/// <summary>What setting up a level needs from the game</summary>
/// <param name="LoadedGame">Whether a saved game is being loaded (its counts and pushwalls are kept)</param>
/// <param name="DemoActive">Whether a demo is playing or recording (the random numbers start the same)</param>
/// <param name="NetGame">Whether this is a game with others (the random numbers start where the host said)</param>
/// <param name="NetSeed">Where the host said they start</param>
/// <param name="CurrentSkill">The skill being played (its enemy-health)</param>
internal sealed record LevelSetupHooks(
    Func<bool> LoadedGame,
    Func<bool> DemoActive,
    Func<bool> NetGame,
    Func<int> NetSeed,
    Func<SkillInfo> CurrentSkill);

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
    GameSession session,
    LevelSetupHooks hooks)
{
    internal void SetupGameLevel()
    {
        foreach (var p in session.players)
        {
            p.Pitch = 0;                    // each level, and each loaded game, starts looking straight ahead
            p.EyeZ = classes.StandingEyeZ(p);       // from standing height
        }
        session.camera.FollowPlayer();              // through the player's eyes
        weaponBob.Init();

        if (!hooks.LoadedGame())
        {
            session.gamestate.TimeCount =
            session.gamestate.secrettotal =
            session.gamestate.killtotal =
            session.gamestate.treasuretotal =
            session.gamestate.secretcount =
            session.gamestate.killcount =
            session.gamestate.treasurecount = 0;
            pushWalls.ClearPushWalls();
            foreach (var p in session.players)
            {
                p.WeaponSprite = null;      // the weapon in hand starts on its Ready state
                p.FaceTimes = 0;
                p.LastAttacker = null;
            }
        }

        if (hooks.DemoActive())
            session.random.InitRndT(false);
        else if (hooks.NetGame())
            session.random.rndindex = hooks.NetSeed();         // where the host said, the same on every machine
        else
            session.random.InitRndT(true);

        //
        // load the level
        //
        deathmatch.DeathmatchLevelSetup();         // with or without its enemies (Gameplay/Deathmatch.cs)
        //int mapnum = gamestate.mapon + 10 * gamestate.cluster;
        map.LoadMap(session.gamestate.mapon, session.gamestate.difficulty, hooks.CurrentSkill().EnemyHealth,
            engine.GetGameInfo().Maps.TryGetValue(session.gamestate.mapon, out var loadingMap) ? loadingMap.FloorNumber : -1);

        // The cluster follows the map, however it was reached: a new game, the next/secret
        // level, a map-change trigger or the console's "map" warp
        if (engine.GetGameInfo().Maps.TryGetValue(session.gamestate.mapon, out var mapInfo))
            session.gamestate.cluster = mapInfo.Cluster;

        render.wallstories = Math.Clamp(mapInfo?.WallHeight ?? engine.GetGameInfo().DefaultMap.WallHeight,
            1, Program.MAXWALLSTORIES);
        skyAndFlats.levelsky = mapInfo?.Sky ?? engine.GetGameInfo().DefaultMap.Sky;
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
        for (y = 0; y < map.mapheight; y++)
        {
            for (x = 0; x < map.mapwidth; x++)
            {
                // door (a plane 0 tile listed in the mapdefs doors)
                if (doors.TryGetValue(map.MAPSPOT(x, y, 0), out var doorXlat))
                    doorObjects.SpawnDoor(x, y, doorXlat.Vertical, doorXlat);
            }
        }

        //
        // spawn the player (MapManager.LoadMap already spawned every other thing and counted
        // the secret pushwalls, from the mapdefs)
        //
        if (map.PlayerStart is { } start)
            playersManager.SpawnPlayers(start.TileX, start.TileY, start.Angle);
        else
        {
            var (standx, standy) = playersManager.StandInStart();
            Console.WriteLine($"{session.gamestate.mapon} has no mapdefs player start; starting on {standx},{standy}");
            playersManager.SpawnPlayers(standx, standy, 90);
        }

        //
        // take out the ambush markers
        //
        for (y = 0; y < map.mapheight; y++)
        {
            for (x = 0; x < map.mapwidth; x++)
            {
                var tile = map.MAPSPOT(x, y, 0);

                if (tile == map.Floors.AmbushTile)
                {
                    if (map.VALIDAREA(map.MAPSPOT(x + 1, y, 0)))
                        tile = (ushort)map.MAPSPOT(x + 1, y, 0);
                    if (map.VALIDAREA(map.MAPSPOT(x, y - 1, 0)))
                        tile = (ushort)map.MAPSPOT(x, y - 1, 0);
                    if (map.VALIDAREA(map.MAPSPOT(x, y + 1, 0)))
                        tile = (ushort)map.MAPSPOT(x, y + 1, 0);
                    if (map.VALIDAREA(map.MAPSPOT(x - 1, y, 0)))
                        tile = (ushort)map.MAPSPOT(x - 1, y, 0);

                    map.SetMapSpot(x, y, 1, 0);
                }
            }
        }

        lighting.InitLevelShading(mapInfo);
        map.AI.OnLevelStart();      // wall outlets, Goldfire's spawn sites, once-a-level drops (Managers.LevelAI)

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
