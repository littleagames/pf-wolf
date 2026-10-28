using PFWolf.Extensions;
using PFWolf.Rendering;

namespace PFWolf.SaveGames;

/// <summary>What <see cref="LevelBodies.WriteLevelBody"/> wrote, read without changing any game state.</summary>
internal sealed record LevelBody(
    LevelSnapshot Level,
    DoorObject[] Doors,
    byte[,] AreaConnect,
    byte[] AreaByPlayer,
    List<MovingPushWall> PushWalls,
    int LastAttacker,
    byte[] Seen,
    Dictionary<int, ZoneState> Zones,
    Dictionary<int, ScrollerState> Scrollers,
    byte? CeilingColor,
    byte? FloorColor,
    bool AltFlatColors,
    LevelAIState AI,
    bool NoShots);

/// <summary>
/// The level as it stands: written into a save, kept for a hub level that's been left, and sent
/// to a player joining a game mid-level (WriteLevelBody); read back without changing anything
/// (ReadLevelBody), then laid over the same level built afresh from its map (ApplyLevelBody).
/// </summary>
internal sealed class LevelBodies(
    MapManager map,
    Doors doorObjects,
    PushWalls pushWalls,
    Lighting lighting,
    LevelAI levelAI,
    GameSession session)
{
    /// <summary>
    /// The level as it stands, as a save holds it and as a hub keeps a level that's been left:
    /// the map and its actors (MapManager.WriteLevelState), then doors, area connections, the
    /// moving MovingWalls, the player's last attacker, the automap's seen tiles, the light zones,
    /// the scrollers, the level's own colors (map-info, as stepped onto, and A_Warp's swap), the
    /// wall outlets' and warp sites' waits (Managers/LevelAI.cs) and whether the player's weapon
    /// can fire (A_PlayerFire: off while Goldfire morphs).
    /// </summary>
    internal void WriteLevelBody(BinaryWriter bw)
    {
        map.WriteLevelState(bw);

        bw.Write(doorObjects.LastDoorObj);
        for (int i = 0; i < doorObjects.LastDoorObj; i++)
            doorObjects.DoorObjList[i].WriteState(bw);

        bw.Write(session.Areas.AreaByPlayer.Length);
        bw.Write(session.Areas.AreaConnect);
        bw.Write(session.Areas.AreaByPlayer);

        bw.Write(pushWalls.MovingWalls.Count);
        foreach (var wall in pushWalls.MovingWalls)
            wall.Write(bw);

        // Died() turns the player to face their killer, so keep who that is.
        // (By reference: actors are records, so IndexOf would match a look-alike by value.)
        bw.Write(map.GetSavedActors().FindIndex(a => ReferenceEquals(a, session.PlayerState.LastAttacker)));

        bw.Write(map.GetSeenBytes());

        lighting.WriteZoneLights(bw);
        map.Scrollers.Write(bw);

        bw.Write(map.MapCeilingColor.HasValue);
        bw.Write(map.MapCeilingColor ?? 0);
        bw.Write(map.MapFloorColor ?? 0);
        bw.Write(map.AltFlatColors);
        levelAI.WriteState(bw);
        bw.Write(session.NoShots);
    }

    internal LevelBody ReadLevelBody(BinaryReader br)
    {
        var level = MapManager.ReadLevelState(br);

        var doors = new DoorObject[br.ReadCount()];
        for (int i = 0; i < doors.Length; i++)
        {
            doors[i] = new DoorObject();
            doors[i].ReadState(br);
        }

        var numareas = br.ReadInt32();
        if (numareas != map.Floors.NumAreas)
            throw new InvalidDataException($"It was saved with {numareas} map areas; this game has {map.Floors.NumAreas}.");

        var areaConnect = SaveFile.ReadExactly(br, numareas * numareas).ToFixedArray(numareas, numareas);
        var areaByPlayer = SaveFile.ReadExactly(br, numareas);
        var pushWallList = Enumerable.Range(0, br.ReadCount()).Select(_ => MovingPushWall.Read(br)).ToList();
        var lastAttacker = br.ReadInt32();
        var seen = SaveFile.ReadExactly(br, level.TileMap.Length);
        var zones = lighting.ReadZoneLights(br);
        var scrollers = Scrollers.Read(br);
        bool hasColors = br.ReadBoolean();
        byte ceiling = br.ReadByte(), floor = br.ReadByte();
        bool alt = br.ReadBoolean();
        var ai = LevelAI.ReadState(br);
        bool noShots = br.ReadBoolean();

        return new LevelBody(level, doors, areaConnect, areaByPlayer, pushWallList, lastAttacker, seen, zones, scrollers,
            hasColors ? ceiling : null, hasColors ? floor : null, alt, ai, noShots);
    }

    /// <summary>
    /// Lays a level body over the level SetupGameLevel just built from the same map (with
    /// LoadedGame set, so it didn't count kills, treasure and secrets again)
    /// </summary>
    /// <param name="ownerOf">With several players (one joining mid-game), whose each saved pawn is, by its place among the actors</param>
    internal List<Entities.Actors.Actor> ApplyLevelBody(LevelBody body, Func<int, Entities.PlayerState?>? ownerOf = null)
    {
        var actors = map.RestoreLevelState(body.Level, ownerOf);

        // Door positions and orientations come from the map; only the motion is saved.
        for (int i = 0; i < Math.Min(doorObjects.LastDoorObj, body.Doors.Length); i++)
        {
            doorObjects.DoorObjList[i].Action = body.Doors[i].Action;
            doorObjects.DoorObjList[i].TicCount = body.Doors[i].TicCount;
            doorObjects.DoorObjList[i].Position = body.Doors[i].Position;
            doorObjects.DoorObjList[i].Held = body.Doors[i].Held;
            doorObjects.DoorObjList[i].Unlocked = body.Doors[i].Unlocked;
            doorObjects.DoorObjList[i].Jammed = body.Doors[i].Jammed;
        }

        session.Areas.AreaConnect = body.AreaConnect;
        session.Areas.AreaByPlayer = body.AreaByPlayer;
        map.MapCeilingColor = body.CeilingColor;
        map.MapFloorColor = body.FloorColor;
        map.AltFlatColors = body.AltFlatColors;

        // copies, so a hub level's kept body isn't moved along with the live walls
        pushWalls.MovingWalls.Clear();
        foreach (var wall in body.PushWalls)
        {
            pushWalls.MovingWalls.Add(new MovingPushWall
            {
                State = wall.State, Pos = wall.Pos, X = wall.X, Y = wall.Y, Dir = wall.Dir, Tile = wall.Tile,
            });
        }

        map.SetSeenBytes(body.Seen);
        lighting.RestoreZoneLights(body.Zones);
        // copies, as the pushwalls, so a hub level's kept body doesn't scroll along with the live level
        map.Scrollers.Restore(body.Scrollers.ToDictionary(s => s.Key, s => s.Value.Clone()));

        levelAI.ApplyState(body.AI);     // its warp sites found again among the restored actors
        session.NoShots = body.NoShots;  // a load mid-morph keeps firing off until Goldfire's done

        session.PlayerState.LastAttacker = body.LastAttacker >= 0 && body.LastAttacker < actors.Count ? actors[body.LastAttacker] : null;
        session.PlayerState.FaceTimes = 0;
        return actors;
    }
}
