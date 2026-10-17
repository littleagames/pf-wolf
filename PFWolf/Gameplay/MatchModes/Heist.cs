using PFWolf.Entities.Actors;
using PFWolf.Managers;

namespace PFWolf.Gameplay.MatchModes;

/*
=============================================================================

                                  HEIST

    Two teams loot the level, a tug of war: every piece of treasure taken
    pulls the rope its worth toward the team that took it (its `heist.value`,
    else a hundredth of its score: a cross 1, a chalice 5, a chest 10, a
    crown 50). The first team to pull it the score limit their way wins, or
    whichever has it their way when the time runs out.

    The small stuff lies about in the open; the best is behind locked doors
    and in riskier places, as the map has it. Keys aren't given: they're
    found, carried and lost with a death, and like the treasure they come
    back where they were a while after they're taken (with items coming
    back on). The map's guards are about by default.

=============================================================================
*/

internal sealed class Heist(MapManager map, GameSession session, Deathmatch match, HudMessageManager hud, MatchModeHooks hooks)
    : MatchObjective(map, session, match, hud, hooks), IMatchObjective
{
    public override GameMode Mode => GameMode.Heist;

    /// <summary>The worth of each piece of treasure on the level when it started (for the status line)</summary>
    internal int LevelLoot;

    public override void LevelSetup() =>
        LevelLoot = Map.GetActors().OfType<Inventory>().Where(item => !item.IsRemoved).Sum(LootValue);

    public override void Tick() { }

    /// <summary>
    /// What a piece of treasure is worth in a heist: its `heist.value`, else a hundredth of the
    /// score it gives (at least 1); nothing for anything that isn't treasure
    /// </summary>
    internal static int LootValue(Inventory item)
    {
        if (item.Properties.TryGetValue("heist.value", out var set) && int.TryParse(set?.ToString(), out var value))
            return Math.Max(value, 0);
        if (item is not ScoreItem)
            return 0;
        return item.Properties.TryGetValue("inventory.amount", out var amount) && int.TryParse(amount?.ToString(), out var points)
            ? Math.Max(points / 100, 1)
            : 1;
    }

    /// <summary>Treasure taken pulls the rope toward the taker's team</summary>
    public void ItemTaken(Inventory item)
    {
        var p = Session.PlayerState;
        int value = LootValue(item);
        if (p.Team is not (0 or 1) || value <= 0)
            return;
        p.Objective += value;
        if (value >= 10)
            Announce($"{NameOf(p)} grabbed {value} worth of loot for {GameModes.TeamLabel(p.Team)}");
        Match.AddTeamScore(p.Team, value);
    }

    /// <summary>Who's ahead: "Red leads by 12"</summary>
    public string? StatusLine()
    {
        int lead = Match.TeamScores[0] - Match.TeamScores[1];
        return lead == 0 ? "Even" : $"{GameModes.TeamLabel(lead > 0 ? 0 : 1)} leads by {Math.Abs(lead)}";
    }

    public override void Write(BinaryWriter bw) => bw.Write(LevelLoot);

    public override void Read(BinaryReader br) => LevelLoot = br.ReadInt32();
}
