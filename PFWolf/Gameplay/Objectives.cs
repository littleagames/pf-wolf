using System.Text.RegularExpressions;
using PFWolf.Assets;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <param name="Settings">Game-info objectives</param>
/// <param name="Localize">A message's text ($language keys looked up)</param>
/// <param name="ShowMessage">Shows a message to the acting player</param>
/// <param name="Switches">Every switch on the level (a wall's or a patch's), for the objectives they count toward</param>
/// <param name="Completed">More of an objective's done: its name, done and total (game.onobjective)</param>
/// <param name="WallObjectives">The objectives of the level's destructible walls, one for each wall (Gameplay/DestructibleWalls.cs)</param>
/// <param name="Announce">Shows a message to every player (the level's objectives and how they're going, which everyone shares); else ShowMessage</param>
/// <param name="WallObjectiveAt">The objective of the destructible wall on a tile, if it has one (for its marker)</param>
internal sealed record ObjectiveHooks(
    Func<IReadOnlyDictionary<string, ObjectiveInfo>> Settings,
    Func<string, string> Localize,
    Action<string> ShowMessage,
    Func<IEnumerable<MapSwitchTranslation>> Switches,
    Action<string, int, int>? Completed = null,
    Func<IEnumerable<string>>? WallObjectives = null,
    Action<string>? Announce = null,
    Func<int, int, string?>? WallObjectiveAt = null);

/*
=============================================================================

                              LEVEL OBJECTIVES

Things a level counts and the player completes (Midnight Assault's sabotage targets and goal
enemies: GameState.sabotagedtotal, endgoaltargetstotal). Game-info `objectives:` names each, with
its messages and whether the exits wait for it (blocks-exit).

As a level starts afresh each wall or patch whose switch has `objective: name`, each destructible
wall with one (done as it breaks), and each actor
with an `objective` property, adds one to that objective's total, and the start message says how
many there are. One's done when such a switch is thrown, such an actor dies, or A_CompleteObjective
says so (A_AddObjective adds to the total, for a script's own); the progress message says how many
of how many, and with the last done the done message shows. While a blocks-exit objective has any
left, A_Exit and A_SecretExit refuse with its refused message. The counts are saved with the level.

=============================================================================
*/

internal sealed class Objectives(MapManager map, ObjectiveHooks hooks)
{
    /// <summary>An objective's count (none: nothing to do)</summary>
    internal ObjectiveCount Count(string name) => map.ObjectiveCounts.TryGetValue(name, out var count) ? count : new ObjectiveCount();

    /// <summary>
    /// Counts the level's objectives as it starts afresh: its switches' and actors' (the actors
    /// already spawned); with <paramref name="announce"/>, each one's start message
    /// </summary>
    /// <param name="doneBefore">Objectives one of which is done already as the level starts, once for each (a Kilroy drawn in an earlier game)</param>
    internal void CountLevel(bool announce, IEnumerable<string>? doneBefore = null)
    {
        map.ObjectiveCounts.Clear();
        foreach (var sw in hooks.Switches())
            if (sw.Objective is { Length: > 0 } name)
                Add(name, 1);
        foreach (var name in hooks.WallObjectives?.Invoke() ?? [])
            Add(name, 1);
        foreach (var name in doneBefore ?? [])
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;
            Add(name, 1);
            map.ObjectiveCounts[name.Trim()].Done++;
        }
        foreach (var actor in map.GetActors())
            if (!actor.IsRemoved && ObjectiveOf(actor) is { } name && actor is not Entities.Actors.Monster { Hitpoints: <= 0 })
                Add(name, 1);

        if (!announce)
            return;
        foreach (var (name, count) in map.ObjectiveCounts)
            if (Info(name) is { Start: { Length: > 0 } start } info && count.Total >= Math.Max(info.StartFrom, 1))
                Announce(Format(start, count.Total));
    }

    // Everyone shares the level's objectives, so hears how they're going
    void Announce(string message) => (hooks.Announce ?? hooks.ShowMessage)(message);

    /// <summary>A_AddObjective("name"[, n]): n more (1) to do</summary>
    internal void Add(string name, int amount)
    {
        if (string.IsNullOrWhiteSpace(name) || amount <= 0)
            return;
        name = name.Trim();
        if (!map.ObjectiveCounts.TryGetValue(name, out var count))
            map.ObjectiveCounts[name] = count = new ObjectiveCount();
        count.Total += amount;
    }

    /// <summary>One (or n) more done, with the progress message (and the done message for the last)</summary>
    internal void Complete(string name, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(name) || amount <= 0)
            return;
        name = name.Trim();
        if (!map.ObjectiveCounts.TryGetValue(name, out var count))
            map.ObjectiveCounts[name] = count = new ObjectiveCount();
        if (count.Total > 0 && count.Done >= count.Total)
            return;                 // all done already
        count.Done += amount;
        if (count.Total > 0)
            count.Done = Math.Min(count.Done, count.Total);

        var info = Info(name);
        if (info?.Progress is { Length: > 0 } progress)
            Announce(Format(progress, count.Done, count.Total));
        if (count.Left == 0 && info?.Done is { Length: > 0 } done)
            Announce(Format(done, count.Done, count.Total));
        hooks.Completed?.Invoke(name, count.Done, count.Total);
    }

    /// <summary>An actor died: if it's an objective's (its `objective` property), one more of it is done</summary>
    internal void ActorDied(Entities.Actors.Actor actor)
    {
        if (ObjectiveOf(actor) is { } name)
            Complete(name);
    }

    /// <summary>Whether all of an objective is done (none to do counts as done)</summary>
    internal bool IsDone(string name) => Count(name).Left == 0;

    /// <summary>
    /// Whether the level can be left: every blocks-exit objective done. If not, the first one
    /// with some left says how many (its refused message).
    /// </summary>
    internal bool ExitAllowed()
    {
        var (blocked, message) = ExitBlock();
        if (message != null)
            hooks.ShowMessage(message);
        return !blocked;
    }

    /// <summary>
    /// Whether a blocks-exit objective keeps the level from being left, and its refused message
    /// (how many are left) if it has one; nothing's shown
    /// </summary>
    internal (bool Blocked, string? Message) ExitBlock()
    {
        foreach (var (name, count) in map.ObjectiveCounts)
        {
            if (count.Left == 0 || Info(name) is not { BlocksExit: true } info)
                continue;
            return (true, info.Refused is { Length: > 0 } refused ? Format(refused, count.Left) : null);
        }
        return (false, null);
    }

    /// <summary>The automap's stats lines for the level's objectives</summary>
    internal IEnumerable<string> StatsLines() =>
        map.ObjectiveCounts.Where(o => o.Value.Total > 0 && Info(o.Key)?.Stats is { Length: > 0 })
            .Select(o => Format(Info(o.Key)!.Stats!, o.Value.Done, o.Value.Total));

    /// <summary>A_JumpIfObjectiveDone("name", "State"): the actor goes there once all of it's done</summary>
    internal void JumpIfObjectiveDone(Entities.Actors.Actor actor, string[] args)
    {
        if (args.Length < 2 || !IsDone(args[0].Trim()))
            return;
        if (actor.ResolvedStates.TryGetValue(args[1].Trim(), out var frame))
            actor.JumpTo(frame);
    }

    /*
    -----------------------------------------------------------------------------
    Markers: what's left of each objective with game-info `marker: true`
    -----------------------------------------------------------------------------
    */

    // The tiles of the marked objectives still to do, found again when the level or how much is
    // done changes (a switch thrown, a wall broken)
    List<(int X, int Y, string Objective)> markedTiles = [];
    (int Level, int Done) markedFor = (-1, -1);

    /// <summary>
    /// A marker on each switch not yet thrown, destructible wall not yet broken and actor still
    /// alive that's toward an objective with game-info `marker: true` and some left to do
    /// </summary>
    internal IEnumerable<ObjectiveMarker> Markers()
    {
        var marked = hooks.Settings().Where(o => o.Value.Marker && Count(o.Key).Left > 0)
            .ToDictionary(o => o.Key, o => o.Value, StringComparer.OrdinalIgnoreCase);
        if (marked.Count == 0)
            return [];

        var key = (map.LevelSerial, map.ObjectiveCounts.Values.Sum(c => c.Done));
        if (markedFor != key)
        {
            markedTiles = ObjectiveTiles().ToList();
            markedFor = key;
        }

        var markers = new List<ObjectiveMarker>();
        foreach (var (x, y, name) in markedTiles)
            if (marked.TryGetValue(name, out var info))
                markers.Add(ObjectiveMarker.OnTile((x, y), Label(info), -1, color: info.MarkerColor));
        foreach (var actor in map.GetActors())
            if (!actor.IsRemoved && actor is not Entities.Actors.Monster { Hitpoints: <= 0 }
                && ObjectiveOf(actor) is { } name && marked.TryGetValue(name, out var info))
                markers.Add(ObjectiveMarker.OnActor(actor, Label(info), -1, info.MarkerColor));
        return markers;
    }

    string Label(ObjectiveInfo info) => info.MarkerLabel is { Length: > 0 } label ? hooks.Localize(label) : "";

    /// <summary>Every tile toward an objective now: a wall's or a patch's switch not yet thrown (thrown, it's its `to` one), a destructible wall</summary>
    internal IEnumerable<(int X, int Y, string Objective)> ObjectiveTiles()
    {
        var data = map.GetMapData();
        for (int y = 0; y < map.MapHeight; y++)
            for (int x = 0; x < map.MapWidth; x++)
            {
                int tile = map.TileMap[x, y];
                if (tile != 0 && (tile & GameConstants.BitDoor) == 0 && tile != GameConstants.BitWall
                    && data.Walls.TryGetValue(tile & ~GameConstants.BitWall, out var wall) && wall.Switch?.Objective is { Length: > 0 } wallObjective)
                    yield return (x, y, wallObjective.Trim());
                if (map.GetPatch(x, y) is var id and > 0 && data.Patches.TryGetValue(id, out var patch)
                    && patch.Switch?.Objective is { Length: > 0 } patchObjective)
                    yield return (x, y, patchObjective.Trim());
                if (hooks.WallObjectiveAt?.Invoke(x, y) is { Length: > 0 } wallsObjective)
                    yield return (x, y, wallsObjective.Trim());
            }
    }

    static string? ObjectiveOf(Entities.Actors.Actor actor) =>
        actor.Properties.TryGetValue("objective", out var name) && name?.ToString() is { Length: > 0 } text ? text.Trim() : null;

    ObjectiveInfo? Info(string name) => hooks.Settings().TryGetValue(name, out var info) ? info : null;

    // The message's text with each %d filled in turn
    string Format(string message, params int[] numbers)
    {
        int i = 0;
        return Regex.Replace(hooks.Localize(message), "%d", _ => i < numbers.Length ? numbers[i++].ToString() : "");
    }
}
