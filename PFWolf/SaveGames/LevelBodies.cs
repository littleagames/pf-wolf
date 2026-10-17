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
    Dictionary<int, ZoneState> Zones);

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
    GameSession session)
{
    /// <summary>
    /// The level as it stands, as a save holds it and as a hub keeps a level that's been left:
    /// the map and its actors (MapManager.WriteLevelState), then doors, area connections, the
    /// moving MovingWalls, the player's last attacker, the automap's seen tiles and the light zones.
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

        return new LevelBody(
            level,
            doors,
            AreaConnect: SaveFile.ReadExactly(br, numareas * numareas).ToFixedArray(numareas, numareas),
            AreaByPlayer: SaveFile.ReadExactly(br, numareas),
            PushWalls: Enumerable.Range(0, br.ReadCount()).Select(_ => MovingPushWall.Read(br)).ToList(),
            LastAttacker: br.ReadInt32(),
            Seen: SaveFile.ReadExactly(br, MapManager.MapArea),
            Zones: lighting.ReadZoneLights(br));
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
        }

        session.Areas.AreaConnect = body.AreaConnect;
        session.Areas.AreaByPlayer = body.AreaByPlayer;

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

        session.PlayerState.LastAttacker = body.LastAttacker >= 0 && body.LastAttacker < actors.Count ? actors[body.LastAttacker] : null;
        session.PlayerState.FaceTimes = 0;
        return actors;
    }
}
