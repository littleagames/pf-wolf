using PFWolf.Entities.Actors;

namespace PFWolf.Gameplay;

/// <summary>
/// The `spectate` command: the camera watches the enemies one after another, or the player again.
/// </summary>
internal sealed class Spectating(
    MapManager map,
    ConsoleManager console,
    HudMessageManager hudMessages,
    HudMessageDrawing hudMessageDrawing,
    GameSession session)
{
    /*
    ====================
    =
    = Spectating
    =
    ====================
    */

    /// <summary>The actors `spectate` steps through: living enemies, in the order they think</summary>
    internal IEnumerable<Entities.Actors.Actor> SpectateCandidates() =>
        map.GetActors().Where(a => !a.IsRemoved && a is not PlayerPawn
            && a.ResolvedStates.ContainsKey("Chase") && a.Hitpoints > 0);

    internal void Cmd_Spectate(string[] args)
    {
        var mode = args.Length > 0 ? args[0] : "next";

        if (mode.Equals("player", StringComparison.OrdinalIgnoreCase))
        {
            session.camera.FollowPlayer();
            console.Print("Watching through your own eyes");
            return;
        }

        bool back = mode.Equals("prev", StringComparison.OrdinalIgnoreCase);
        bool any = back || mode.Equals("next", StringComparison.OrdinalIgnoreCase);
        var candidates = SpectateCandidates()
            .Where(a => any || a.Name.Equals(mode, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
        {
            console.Print(any ? "Nobody to watch" : $"No living {mode} to watch");
            return;
        }

        // The one after (or before) the actor watched now, round the list; the first (or last)
        // from the player's eyes
        int at = session.camera.Spectating ? candidates.IndexOf(session.camera.Target!) : -1;
        int next = at < 0
            ? (back ? candidates.Count - 1 : 0)
            : (at + (back ? -1 : 1) + candidates.Count) % candidates.Count;

        var watched = candidates[next];
        session.camera.Follow(watched);

        var text = $"Watching {hudMessageDrawing.ActorTag(watched)}";
        console.Print($"{text} ({watched.Name} at {watched.TileX},{watched.TileY}, {next + 1} of {candidates.Count})");
        hudMessages.Show(Managers.HudMessageKind.Other, text);
    }
}
