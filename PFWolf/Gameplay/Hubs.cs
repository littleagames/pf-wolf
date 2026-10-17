using PFWolf.Assets;

namespace PFWolf.Gameplay;

/*
=============================================================================

                                HUB CLUSTERS

A cluster with `hub: true` (game-info) keeps its levels as they were left: going back to
one finds it as the player left it (Blake Stone's floors). Each level left is kept as a
save's level body, with its counts, until the game moves to another cluster or starts anew.
Saves hold the kept levels, the maps the player has been on (the elevator's unlocked
floors) and the player as they came into the level, which game-info's
death-restores-level-start puts them back to when they die.

=============================================================================
*/

/// <summary>A save's hub state: the levels kept, the maps been on, and the level's start</summary>
internal sealed record HubState(
    Dictionary<string, byte[]> Levels,
    HashSet<string> Visited,
    string? LevelMap,
    byte[]? LevelStart,
    Dictionary<string, int> FloorScores,
    int InformantTotal,
    HashSet<string> Unlocked,
    Dictionary<string, byte[]> Overheads);

/// <summary>The hub clusters' state: the levels kept, the floors been on or unlocked, and the floors' stats</summary>
internal sealed class Hubs(GameEngineManager engine, MapManager mapManager, GameSession session)
{
    /// <summary>The hub levels that have been left, as they were left, by map</summary>
    internal readonly Dictionary<string, byte[]> hubLevels = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The maps the player has been on this game: their floors are open in the elevator</summary>
    internal readonly HashSet<string> visitedMaps = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Floors unlocked without having been on them (A_UnlockFloor: Planet Strike's security cubes)</summary>
    internal readonly HashSet<string> unlockedMaps = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Each floor's overhead map (64x64 palette colors) as last seen, for the teleporter (Ui/ElevatorScreens.cs)</summary>
    internal readonly Dictionary<string, byte[]> floorOverheads = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Each floor's overall score (0 to 300) as last left, for the mission ratio (Ui/ElevatorScreens.cs)</summary>
    internal readonly Dictionary<string, int> floorScores = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The map the level in play came from</summary>
    internal string? levelMap;

    /// <summary>The player as they came into the level (gamestate, inventory), for death-restores-level-start</summary>
    internal byte[]? levelStart;

    /// <summary>How many informants the level started with</summary>
    internal int informantTotal;

    internal bool IsHubMap(string? map) =>
        map != null && engine.GetGameInfo().Maps.TryGetValue(map, out var info)
        && engine.GetGameInfo().Clusters.TryGetValue(info.Cluster, out var cluster) && cluster.Hub;

    /// <summary>The episode being played: the one whose start map is in the level's cluster</summary>
    internal EpisodeInfo? CurrentEpisode() =>
        engine.GetGameInfo().Episodes.Values.FirstOrDefault(e => ClusterOf(e.StartMap) == ClusterOf(session.gamestate.mapon));

    internal short ClusterOf(string? map) =>
        map != null && engine.GetGameInfo().Maps.TryGetValue(map, out var info) ? info.Cluster : (short)-1;

    /// <summary>The map of the level's cluster with this floor number, if any</summary>
    internal string? ClusterFloorMap(int floor)
    {
        var cluster = ClusterOf(session.gamestate.mapon);
        return engine.GetGameInfo().Maps.FirstOrDefault(m => m.Value.Cluster == cluster && m.Value.FloorNumber == floor).Key;
    }

    internal int CurrentFloorNumber() =>
        engine.GetGameInfo().Maps.TryGetValue(session.gamestate.mapon, out var info) ? info.FloorNumber : 0;

    /// <summary>Whether a floor is open in the teleporter: been on, or unlocked</summary>
    internal bool FloorOpen(string map) => visitedMaps.Contains(map) || unlockedMaps.Contains(map);

    /// <summary>A new game: nothing kept, nowhere been</summary>
    internal void ResetHubs()
    {
        hubLevels.Clear();
        visitedMaps.Clear();
        unlockedMaps.Clear();
        floorOverheads.Clear();
        floorScores.Clear();
        levelMap = null;
        levelStart = null;
    }

    internal void WriteHubState(BinaryWriter bw)
    {
        bw.Write(hubLevels.Count);
        foreach (var (map, data) in hubLevels)
        {
            bw.Write(map);
            bw.Write(data.Length);
            bw.Write(data);
        }

        bw.Write(visitedMaps.Count);
        foreach (var map in visitedMaps)
            bw.Write(map);

        bw.Write(levelMap ?? "");

        bw.Write(levelStart?.Length ?? 0);
        if (levelStart != null)
            bw.Write(levelStart);

        bw.Write(floorScores.Count);
        foreach (var (map, score) in floorScores)
        {
            bw.Write(map);
            bw.Write(score);
        }

        bw.Write(informantTotal);

        bw.Write(unlockedMaps.Count);
        foreach (var map in unlockedMaps)
            bw.Write(map);
        bw.Write(floorOverheads.Count);
        foreach (var (map, image) in floorOverheads)
        {
            bw.Write(map);
            bw.Write(image.Length);
            bw.Write(image);
        }
    }

    internal static HubState ReadHubState(BinaryReader br)
    {
        var levels = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        for (int i = br.ReadCount(); i > 0; i--)
        {
            var map = br.ReadString();
            levels[map] = SaveFile.ReadExactly(br, br.ReadCount());
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = br.ReadCount(); i > 0; i--)
            visited.Add(br.ReadString());

        var map0 = br.ReadString();
        int startLength = br.ReadCount();
        var start = startLength > 0 ? SaveFile.ReadExactly(br, startLength) : null;

        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = br.ReadCount(); i > 0; i--)
            scores[br.ReadString()] = br.ReadInt32();

        int informants = br.ReadInt32();
        var unlocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = br.ReadCount(); i > 0; i--)
            unlocked.Add(br.ReadString());
        var overheads = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        for (int i = br.ReadCount(); i > 0; i--)
        {
            var map = br.ReadString();
            overheads[map] = SaveFile.ReadExactly(br, br.ReadCount());
        }

        return new HubState(levels, visited, map0.Length > 0 ? map0 : null, start, scores, informants, unlocked, overheads);
    }

    internal void RestoreHubState(HubState state)
    {
        hubLevels.Clear();
        foreach (var (map, data) in state.Levels)
            hubLevels[map] = data;
        visitedMaps.Clear();
        visitedMaps.UnionWith(state.Visited);
        unlockedMaps.Clear();
        unlockedMaps.UnionWith(state.Unlocked);
        floorOverheads.Clear();
        foreach (var (map, image) in state.Overheads)
            floorOverheads[map] = image;
        floorScores.Clear();
        foreach (var (map, score) in state.FloorScores)
            floorScores[map] = score;
        levelMap = state.LevelMap ?? session.gamestate.mapon;
        levelStart = state.LevelStart;
        informantTotal = state.InformantTotal;
    }

    /*
    =============================================================================

                                FLOOR STATS

    Blake Stone's floor ratios, as the elevator shows them: points (here, the treasure taken),
    informants still alive and enemies destroyed, each a percentage (100 when the floor has
    none); the floor's score is their sum, out of 300.

    =============================================================================
    */

    internal static int Ratio(int got, int total) => total == 0 ? 100 : got * 100 / total;

    internal int InformantsAlive() =>
        mapManager.GetActors().Count(a => !a.IsRemoved && a is Entities.Actors.Informant
            && a.RuntimeFlags.HasFlag(ObjFlags.FL_SHOOTABLE));

    /// <summary>Points, informants and enemies, as (got, total) for the level in play</summary>
    internal (int Got, int Total)[] FloorStats() =>
    [
        (session.gamestate.treasurecount, session.gamestate.treasuretotal),
        (InformantsAlive(), informantTotal),
        (session.gamestate.killcount, session.gamestate.killtotal),
    ];

    internal int FloorScore() => FloorStats().Sum(s => Ratio(s.Got, s.Total));

    /// <summary>
    /// The mission's ratio: every floor of the cluster's score (the level in play's as it
    /// stands, 100 for a floor not left yet) over 300 each
    /// </summary>
    internal int MissionRatio()
    {
        var gameInfo = engine.GetGameInfo();
        var cluster = ClusterOf(session.gamestate.mapon);
        var floors = gameInfo.Maps.Where(m => m.Value.Cluster == cluster && !string.IsNullOrEmpty(m.Value.Music)).Select(m => m.Key).ToList();
        if (floors.Count == 0)
            return 100;

        int total = floors.Sum(map => map.Equals(session.gamestate.mapon, StringComparison.OrdinalIgnoreCase)
            ? FloorScore()
            : floorScores.GetValueOrDefault(map, 100));
        return total * 100 / (floors.Count * 300);
    }
}
