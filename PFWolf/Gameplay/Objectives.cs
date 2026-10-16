using System.Text.RegularExpressions;
using PFWolf.Assets;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <param name="Settings">Game-info objectives</param>
/// <param name="Localize">A message's text ($language keys looked up)</param>
/// <param name="ShowMessage">Shows a message to the acting player</param>
/// <param name="Switches">Every switch on the level (a wall's or a patch's), for the objectives they count toward</param>
internal sealed record ObjectiveHooks(
    Func<IReadOnlyDictionary<string, ObjectiveInfo>> Settings,
    Func<string, string> Localize,
    Action<string> ShowMessage,
    Func<IEnumerable<MapSwitchTranslation>> Switches);

/*
=============================================================================

                              LEVEL OBJECTIVES

Things a level counts and the player completes (Midnight Assault's sabotage targets and goal
enemies: gamestate.sabotagedtotal, endgoaltargetstotal). Game-info `objectives:` names each, with
its messages and whether the exits wait for it (blocks-exit).

As a level starts afresh each wall or patch whose switch has `objective: name`, and each actor
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
    internal ObjectiveCount Count(string name) => map.objectives.TryGetValue(name, out var count) ? count : new ObjectiveCount();

    /// <summary>
    /// Counts the level's objectives as it starts afresh: its switches' and actors' (the actors
    /// already spawned); with <paramref name="announce"/>, each one's start message
    /// </summary>
    internal void CountLevel(bool announce)
    {
        map.objectives.Clear();
        foreach (var sw in hooks.Switches())
            if (sw.Objective is { Length: > 0 } name)
                Add(name, 1);
        foreach (var actor in map.GetActors())
            if (!actor.IsRemoved && ObjectiveOf(actor) is { } name && actor is not Entities.Actors.Monster { Hitpoints: <= 0 })
                Add(name, 1);

        if (!announce)
            return;
        foreach (var (name, count) in map.objectives)
            if (Info(name) is { Start: { Length: > 0 } start } info && count.Total >= Math.Max(info.StartFrom, 1))
                hooks.ShowMessage(Format(start, count.Total));
    }

    /// <summary>A_AddObjective("name"[, n]): n more (1) to do</summary>
    internal void Add(string name, int amount)
    {
        if (string.IsNullOrWhiteSpace(name) || amount <= 0)
            return;
        name = name.Trim();
        if (!map.objectives.TryGetValue(name, out var count))
            map.objectives[name] = count = new ObjectiveCount();
        count.Total += amount;
    }

    /// <summary>One (or n) more done, with the progress message (and the done message for the last)</summary>
    internal void Complete(string name, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(name) || amount <= 0)
            return;
        name = name.Trim();
        if (!map.objectives.TryGetValue(name, out var count))
            map.objectives[name] = count = new ObjectiveCount();
        if (count.Total > 0 && count.Done >= count.Total)
            return;                 // all done already
        count.Done += amount;
        if (count.Total > 0)
            count.Done = Math.Min(count.Done, count.Total);

        var info = Info(name);
        if (info?.Progress is { Length: > 0 } progress)
            hooks.ShowMessage(Format(progress, count.Done, count.Total));
        if (count.Left == 0 && info?.Done is { Length: > 0 } done)
            hooks.ShowMessage(Format(done, count.Done, count.Total));
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
        foreach (var (name, count) in map.objectives)
        {
            if (count.Left == 0 || Info(name) is not { BlocksExit: true } info)
                continue;
            if (info.Refused is { Length: > 0 } refused)
                hooks.ShowMessage(Format(refused, count.Left));
            return false;
        }
        return true;
    }

    /// <summary>The automap's stats lines for the level's objectives</summary>
    internal IEnumerable<string> StatsLines() =>
        map.objectives.Where(o => o.Value.Total > 0 && Info(o.Key)?.Stats is { Length: > 0 })
            .Select(o => Format(Info(o.Key)!.Stats!, o.Value.Done, o.Value.Total));

    /// <summary>A_JumpIfObjectiveDone("name", "State"): the actor goes there once all of it's done</summary>
    internal void A_JumpIfObjectiveDone(Entities.Actors.Actor actor, string[] args)
    {
        if (args.Length < 2 || !IsDone(args[0].Trim()))
            return;
        if (actor.ResolvedStates.TryGetValue(args[1].Trim(), out var frame))
            actor.JumpTo(frame);
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
