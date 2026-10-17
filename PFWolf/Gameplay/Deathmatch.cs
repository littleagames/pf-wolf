using PFWolf.Entities.Actors;
using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>
/// How a match is played: the host's choices in the lobby. ScoreLimit is the mode's
/// (GameModes.ScoreName: frags, captures, loot lead, points, waves); 0 for none.
/// </summary>
internal sealed record NetRules(bool Monsters, int ScoreLimit, int TimeLimitMinutes, bool ItemRespawn);

// An item taken, to come back: its class, where, and when (GameState.TimeCount)
internal sealed record ItemRespawn(string ClassName, int TileX, int TileY, int At);

/// <summary>What a deathmatch needs from the game</summary>
/// <param name="IsFreeFloor">Open floor nobody stands on</param>
/// <param name="GiveAllKeys">The acting player carries every key</param>
internal sealed record DeathmatchHooks(
    Func<int, int, bool> IsFreeFloor,
    Action GiveAllKeys);

/// <summary>
/// What a mode with something to do besides fighting keeps (Gameplay/MatchModes): its things on
/// the level, its clock, and what a player joining mid-game must be sent of it. Every machine
/// runs it alike, off the level's clock and the session's random numbers.
/// </summary>
internal interface IMatchObjective
{
    /// <summary>The mode it plays</summary>
    GameMode Mode { get; }

    /// <summary>A new level of the match (after its things have spawned, before the players come in)</summary>
    void LevelSetup();

    /// <summary>Each frame of the level</summary>
    void Tick();

    /// <summary>A player of it has just died (as the acting player)</summary>
    void PlayerDied(Entities.PlayerState victim) { }

    /// <summary>The acting player has picked an item up (Heist's loot)</summary>
    void ItemTaken(Inventory item) { }

    /// <summary>Whether a dead player may come back yet (Horde: only between waves)</summary>
    bool MayRespawn(Entities.PlayerState p) => true;

    /// <summary>Where a team's players come in, if the mode has a place for them (its flag), else null</summary>
    (int x, int y)? TeamHome(int team) => null;

    /// <summary>A line for the top of the view: what's going on (whose flag's where, the wave)</summary>
    string? StatusLine() => null;

    /// <summary>Where its things are, for the `match` command: "red base 3,4; blue base 60,4"</summary>
    string? Details() => null;

    /// <summary>The final scoreboard's title, where the mode has its own ("Survived 7 waves"); else null</summary>
    string? FinalTitle() => null;

    /// <summary>Its state, for a player joining mid-game (Networking/MidGameJoin.cs)</summary>
    void Write(BinaryWriter bw);
    void Read(BinaryReader br);
}

/// <summary>
/// Players against each other. Everyone comes in (and back) at a random spot: one of their
/// team's starts in a team game (mapdefs player-starts with `team:`), else one of the map's
/// deathmatch starts (`deathmatch: true`), else any open floor, as far from rivals as can be
/// had. Each carries every key, so no DoorNumber is shut to anyone (but in Heist). A weapon on the
/// floor stays there for everyone without it yet; anything else taken comes back after
/// ItemRespawnTics (with item-respawn on). Killing another player is a frag (killing yourself
/// takes one away). The first to the score limit, or whoever leads when the time runs out,
/// ends the match: a deathmatch counts each player's frags, a team game each team's score
/// (team deathmatch: its frags; the other modes: what their objective gives it). The map's
/// enemies are only there with monsters on.
///
/// All of it runs the same on every machine (the session's random numbers, the level's clock): it's the game itself.
/// </summary>
internal sealed class Deathmatch(
    MapManager map,
    InventoryManager inventory,
    StatusBarDrawing statusBar,
    GameSession session,
    DeathmatchHooks hooks)
{
    internal static readonly NetRules DefaultRules = new(Monsters: false, ScoreLimit: 20, TimeLimitMinutes: 0, ItemRespawn: true);

    /// <summary>The rules of the match being played</summary>
    internal NetRules NetRules = DefaultRules;

    /// <summary>How long a taken item takes to come back, in a deathmatch (tics)</summary>
    internal const int ItemRespawnTics = 30 * 70;

    /// <summary>The match has been won (the score limit) or run out of time: the level ends, for the scoreboard</summary>
    internal bool MatchOver;

    /// <summary>Each team's score this match (red's first)</summary>
    internal readonly int[] TeamScores = new int[2];

    /// <summary>The modes' MatchObjectives (flags, loot, points, waves): the one for the mode being played runs</summary>
    internal readonly List<IMatchObjective> MatchObjectives = [];

    /// <summary>The mode being played's objective, if it has one</summary>
    internal IMatchObjective? Objective => MatchObjectives.FirstOrDefault(o => o.Mode == session.GameMode);

    // Items taken, to come back
    internal readonly List<ItemRespawn> ItemRespawns = [];

    /// <summary>A new match: no score yet</summary>
    internal void NewMatch()
    {
        MatchOver = false;
        Array.Clear(TeamScores);
        foreach (var p in session.Players)
            p.Frags = p.Objective = 0;
    }

    /// <summary>A new level of a deathmatch: nothing waiting to come back, and its enemies only with monsters on</summary>
    internal void DeathmatchLevelSetup()
    {
        ItemRespawns.Clear();
        map.NoMonsters = (session.IsVersus || session.GameMode == GameMode.Horde) && !NetRules.Monsters;
    }

    /// <summary>Once the level's things are in: the objective's own setup</summary>
    internal void ObjectiveLevelSetup() => Objective?.LevelSetup();

    /// <summary>
    /// A spot for a player to come into a deathmatch at: a free start of their team's (team
    /// games), else a free deathmatch start, else any free open floor; of those, one at random
    /// among the ones at least 8 tiles from every other living rival when there are any. In a
    /// team game without team starts, the half of them nearer the team's home (its flag), when
    /// the mode has one.
    /// </summary>
    internal (int x, int y, int angle) DeathmatchSpot(PlayerPawn self)
    {
        var others = map.Players
            .Where(pawn => pawn != self && pawn.Active == ActiveType.Yes && pawn.State.Health > 0
                && (pawn.State.Team != self.State.Team || !session.IsTeamGame))
            .ToList();
        int NearestOther(int x, int y) =>
            others.Count == 0 ? int.MaxValue : others.Min(pawn => Math.Max(Math.Abs(pawn.TileX - x), Math.Abs(pawn.TileY - y)));

        int team = self.State.Team;
        bool teamStarts = session.IsTeamGame && team is 0 or 1 && map.TeamStarts[team].Count > 0;
        var starts = teamStarts ? map.TeamStarts[team] : map.DeathmatchStarts;
        List<(int x, int y, int angle)> spots = starts.Count > 0
            ? starts.Where(s => hooks.IsFreeFloor(s.TileX, s.TileY)).Select(s => (s.TileX, s.TileY, s.Angle)).ToList()
            : [];
        if (spots.Count == 0)
        {
            for (int y = 1; y < map.mapheight - 1; y++)
                for (int x = 1; x < map.mapwidth - 1; x++)
                    if (hooks.IsFreeFloor(x, y) && !map.ShootableActorsAt(x, y).Any() && map.PatrolPointAt(x, y) == null)
                        spots.Add((x, y, -1));
        }
        if (spots.Count == 0)
            return (self.TileX, self.TileY, self.Angle);

        // A team without starts of its own comes in on its side of the map: nearer its home than
        // the other team's, else the half of the spots nearest its home
        if (session.IsTeamGame && !teamStarts && team is 0 or 1 && Objective?.TeamHome(team) is { } home && spots.Count > 1)
        {
            int ToHome((int x, int y, int angle) s) => Math.Abs(s.x - home.x) + Math.Abs(s.y - home.y);
            var theirs = Objective.TeamHome(1 - team);
            var nearer = theirs is { } other
                ? spots.Where(s => ToHome(s) < Math.Abs(s.x - other.x) + Math.Abs(s.y - other.y)).ToList()
                : [];
            spots = nearer.Count > 0 ? nearer
                : spots.OrderBy(ToHome).ThenBy(s => s.y).ThenBy(s => s.x).Take((spots.Count + 1) / 2).ToList();
        }

        var far = spots.Where(s => NearestOther(s.x, s.y) >= 8).ToList();
        if (far.Count > 0)
            spots = far;

        var random = session.Random;
        var spot = spots[((random.RndT() << 8) | random.RndT()) % spots.Count];
        int angle = spot.angle >= 0 ? spot.angle : (random.RndT() & 3) * 90;     // a random way, where the spot says none
        return (spot.x, spot.y, angle);
    }

    /// <summary>The acting player carries every key (in a deathmatch, no DoorNumber is shut to anyone)</summary>
    internal void GiveDeathmatchKeys()
    {
        if (!session.GameMode.GivesAllKeys())
            return;
        hooks.GiveAllKeys();      // (GameHost.ConsoleCommands.cs)
        statusBar.DrawKeys();
    }

    /// <summary>Whether the acting player already has the weapon a pickup gives (it then stays, for someone without it)</summary>
    internal bool HasWeaponFrom(Weapon pickup)
    {
        var weaponName = pickup.Properties.TryGetValue("weapongiver.weapon", out var given)
            ? given.ToString() ?? pickup.Name
            : pickup.Name;
        return inventory.Has(weaponName);
    }

    /// <summary>An item has just been taken: it comes back where it was after a while</summary>
    internal void QueueItemRespawn(Inventory item) =>
        ItemRespawns.Add(new ItemRespawn(item.Name, (int)item.Position.X, (int)item.Position.Y, session.GameState.TimeCount + ItemRespawnTics));

    /// <summary>Each frame of a deathmatch: items coming back, the objective, and the time limit</summary>
    internal void TickDeathmatch()
    {
        var gamestate = session.GameState;
        for (int i = 0; i < ItemRespawns.Count; i++)
        {
            var item = ItemRespawns[i];
            if (gamestate.TimeCount < item.At)
                continue;
            ItemRespawns.RemoveAt(i--);
            map.SpawnThing(item.TileX, item.TileY, item.ClassName);
        }

        Objective?.Tick();

        if (NetRules.TimeLimitMinutes > 0 && gamestate.TimeCount >= NetRules.TimeLimitMinutes * 60 * 70)
            EndMatch();
    }

    /// <summary>Time left in the match (tics), or null with no time limit</summary>
    internal int? TimeLeft =>
        NetRules.TimeLimitMinutes > 0 ? Math.Max(0, NetRules.TimeLimitMinutes * 60 * 70 - session.GameState.TimeCount) : null;

    /// <summary>
    /// A player has died in a deathmatch, killed by <paramref name="killer"/>: a frag for
    /// a rival who did it (in team deathmatch, for their team too), one fewer for killing
    /// yourself; the score limit ends the match
    /// </summary>
    internal void CountFrag(Entities.PlayerState victim, Entities.Actors.Actor? killer)
    {
        var by = killer?.Shooter ?? killer;
        bool tdm = session.GameMode == GameMode.TeamDeathmatch;
        if (by is PlayerPawn rival && rival.State != victim)
        {
            if (!session.AreRivals(rival.State, victim))
                return;
            rival.State.Frags++;
            using (session.ActAs(rival.State))
                statusBar.DrawScore();
            if (tdm)
                AddTeamScore(rival.State.Team, 1);
            else if (session.GameMode == GameMode.Deathmatch && NetRules.ScoreLimit > 0 && rival.State.Frags >= NetRules.ScoreLimit)
                EndMatch();
        }
        else if (by is PlayerPawn self && self.State == victim)
        {
            victim.Frags--;
            using (session.ActAs(victim))
                statusBar.DrawScore();
            if (tdm)
                AddTeamScore(victim.Team, -1);
        }
    }

    /// <summary>A player has died with others playing (not co-op): a frag in a versus mode, and whatever it means to the objective</summary>
    internal void PlayerKilled(Entities.PlayerState victim, Entities.Actors.Actor? killer)
    {
        if (session.IsVersus)
            CountFrag(victim, killer);
        Objective?.PlayerDied(victim);
    }

    /// <summary>
    /// A team scores (a frag, a capture, loot, a point held): the score limit ends the match.
    /// In Heist it's a tug of war: the limit is a lead over the other team.
    /// </summary>
    internal void AddTeamScore(int team, int points)
    {
        if (team is not (0 or 1) || MatchOver)
            return;
        TeamScores[team] += points;
        statusBar.DrawScore();
        int limit = NetRules.ScoreLimit;
        if (limit <= 0)
            return;
        if (session.GameMode == GameMode.Heist ? TeamScores[team] - TeamScores[1 - team] >= limit : TeamScores[team] >= limit)
            EndMatch();
    }

    /// <summary>
    /// The line across the top of the view: the score (each team's, or the best frags) and
    /// the time left; and the objective's own line (whose flag's where, the wave), if any
    /// </summary>
    internal (string Score, string? News) MatchStatus()
    {
        var parts = new List<string>();
        if (session.IsTeamGame)
            parts.Add($"Red {TeamScores[0]} - {TeamScores[1]} Blue");
        else if (session.GameMode == GameMode.Deathmatch)
            parts.Add($"Frags {session.LocalPlayer.Frags}" + (NetRules.ScoreLimit > 0 ? $"/{NetRules.ScoreLimit}" : ""));
        if (session.IsTeamGame && NetRules.ScoreLimit > 0)
            parts.Add(session.GameMode == GameMode.Heist ? $"lead by {NetRules.ScoreLimit}" : $"to {NetRules.ScoreLimit}");
        if (TimeLeft is { } left && session.GameMode != GameMode.Horde)
            parts.Add($"{left / 70 / 60}:{left / 70 % 60:00}");
        return (string.Join("   ", parts), Objective?.StatusLine());
    }

    /// <summary>The team ahead, or -1 for a draw</summary>
    internal int LeadingTeam => TeamScores[0] == TeamScores[1] ? -1 : TeamScores[0] > TeamScores[1] ? 0 : 1;

    /// <summary>What the status bar's score box shows for the acting player: their frags, their team's score, or their score</summary>
    internal int ScoreShown(Entities.PlayerState p) => session.GameMode switch
    {
        GameMode.Deathmatch => p.Frags,
        _ when session.IsTeamGame && p.Team is 0 or 1 => TeamScores[p.Team],
        _ => p.Score,
    };

    /// <summary>The deathmatch is over: the level ends, and the scoreboard shows who won</summary>
    internal void EndMatch()
    {
        if (MatchOver)
            return;
        MatchOver = true;
        session.PlayState = PlayState.Abort;
    }

    /// <summary>The team with fewer players in it (red when even), for someone joining a team game</summary>
    internal static int SmallerTeam(IEnumerable<int> teams)
    {
        var list = teams.ToList();
        return list.Count(t => t == 1) < list.Count(t => t == 0) ? 1 : 0;
    }
}
