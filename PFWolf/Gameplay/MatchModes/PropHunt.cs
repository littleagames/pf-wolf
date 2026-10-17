using System.Text.RegularExpressions;
using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay.MatchModes;

/*
=============================================================================

                                PROP HUNT

    One team hunts, the other hides as props: each prop looks to everyone
    like one of the level's decorations (a lamp, a barrel, a plant), drawn
    as it is on the map instead of their own body. A prop comes in as one at
    random and takes on the look of another by pressing use facing it. Props
    carry nothing to fight with (PropPlayer's loadout, actordefs
    prophunt.yaml); the hunters shoot them as any rival.

    Which decorations a prop can look like: the level's plain scenery (no
    enemy, item or player) with something to see, but not projectiles, wall
    sprites or any class flagged NODISGUISE (puddles, the modes' flags).

    A match is rounds. Each starts with the props hiding (the rules' hide
    time, 30 s unless the host says) while the hunters wait, kept still with
    their view blacked out; then the hunt (the hunt time, 3 minutes). A prop
    sees from behind itself (chasecam), to see what it looks like. The hunters win the round by killing every prop; the props
    win it if any is still alive when the time runs out. Each round won is a
    point for the team, and the score limit's rounds win the match. After a
    few seconds (RoundOverTics) the sides swap and everyone comes back
    afresh. The dead stay dead until then, watching the living. Red hunts
    first.

=============================================================================
*/

internal sealed class PropHunt(MapManager map, GameSession session, Deathmatch match, HudMessageManager hud, MatchModeHooks hooks)
    : MatchObjective(map, session, match, hud, hooks), IMatchObjective
{
    public override GameMode Mode => GameMode.PropHunt;

    /// <summary>The player class whose loadout and weapons props have (actordefs prophunt.yaml)</summary>
    internal const string PropClass = "PropPlayer";

    internal enum Phase { Hide, Hunt, RoundOver }

    /// <summary>How long the props have to hide and the hunt lasts (the rules: lobby Hide Time, Hunt Time), in tics</summary>
    internal int HideTics => Math.Max(Match.NetRules.HideSeconds, 1) * 70;
    internal int HuntTics => Math.Max(Match.NetRules.HuntMinutes, 1) * 60 * 70;

    /// <summary>How long the end of a round shows who won (tics)</summary>
    internal const int RoundOverTics = 5 * 70;

    /// <summary>The team hunting (0 red, 1 blue); the other hides</summary>
    internal int HuntingTeam;

    /// <summary>The round being played, from 1</summary>
    internal int Round;

    internal Phase CurrentPhase;

    /// <summary>When the phase ends (GameState.TimeCount)</summary>
    internal int PhaseEnds;

    /// <summary>The team that won the round just over, or -1 for nobody (a draw)</summary>
    internal int RoundWinner = -1;

    /// <summary>The decoration classes on the level a prop can look like, in the order first placed</summary>
    internal readonly List<string> Disguises = [];

    /// <summary>The most health a prop has as each of them (HealthAs)</summary>
    internal readonly Dictionary<string, int> DisguiseHealth = new(StringComparer.Ordinal);

    /// <summary>Whether a player is a prop: on the team that isn't hunting</summary>
    internal bool IsProp(Entities.PlayerState p) => p.Team is 0 or 1 && p.Team != HuntingTeam;

    /// <summary>
    /// Whether a prop can look like an actor: plain scenery (not an enemy, item or player) that
    /// shows a sprite, and isn't a projectile, a wall sprite or flagged NODISGUISE
    /// </summary>
    internal static bool CanDisguiseAs(Entities.Actors.Actor actor) =>
        actor.GetType() == typeof(Entities.Actors.Actor) && !actor.IsRemoved && !actor.Hidden
        && actor.ResolvedStates.TryGetValue("Spawn", out var spawn)
        && spawn.Sprite is { Length: > 0 } sprite && !sprite.Equals("TNT1", StringComparison.OrdinalIgnoreCase)
        && !actor.HasFlag("PROJECTILE") && !actor.HasFlag("WALLSPRITE") && !actor.HasFlag("NODISGUISE");

    /// <summary>What messages call a decoration class: its words apart ("Floor Lamp" for FloorLamp)</summary>
    internal static string LookName(string className) => Regex.Replace(className, "(?<=[a-z0-9])(?=[A-Z])", " ");

    /// <summary>
    /// The most health a prop has looking like a decoration, by its size: half for one with no
    /// radius, up to all of it (100) for a solid one's full tile (radius 32); its class's
    /// `prophunt.health` says otherwise
    /// </summary>
    internal static int HealthAs(Entities.Actors.Actor decoration) =>
        decoration.Properties.TryGetValue("prophunt.health", out var given) && int.TryParse(given?.ToString(), out var health)
            ? Math.Clamp(health, 1, 100)
            : Math.Clamp(50 + decoration.Radius * 50 / 32, 25, 100);

    /// <summary>The most health a player has looking as they do: all of it (100) as themselves</summary>
    internal int MostHealth(string? disguise) =>
        disguise != null && DisguiseHealth.TryGetValue(disguise, out var health) ? health : 100;

    /// <summary>A prop takes on a look: their health goes with it, the same share of the most they can have</summary>
    internal void Disguise(Entities.PlayerState p, string? look)
    {
        int from = MostHealth(p.Disguise), to = MostHealth(look);
        p.Disguise = look;
        if (p.Health > 0 && from != to)
            p.Health = (short)Math.Clamp(p.Health * to / from, 1, to);
        Hooks.ShowHealth?.Invoke(p);        // the number, and the face that goes by its share
    }

    public override void LevelSetup()
    {
        HuntingTeam = 0;
        Round = 1;
        RoundWinner = -1;
        Disguises.Clear();
        DisguiseHealth.Clear();
        foreach (var decoration in Map.GetActors().Where(CanDisguiseAs))
            if (DisguiseHealth.TryAdd(decoration.Name, HealthAs(decoration)))
                Disguises.Add(decoration.Name);
        StartPhase(Phase.Hide, HideTics);
    }

    void StartPhase(Phase phase, int tics)
    {
        CurrentPhase = phase;
        PhaseEnds = Session.GameState.TimeCount + tics;
    }

    /// <summary>Each frame: everyone's part in it, and the round's clock: the hunt starting, a round won, the next round</summary>
    public override void Tick()
    {
        UpdateParts();

        var time = Session.GameState.TimeCount;
        switch (CurrentPhase)
        {
            case Phase.Hide when time >= PhaseEnds:
                StartPhase(Phase.Hunt, HuntTics);
                Announce("Ready or not, here they come!", "misc/yeah");
                break;

            case Phase.Hunt:
                int props = Session.Players.Count(p => !p.Gone && p.Pawn != null && IsProp(p));
                int hiding = LivingPawns.Count(pawn => IsProp(pawn.State));
                if (props > 0 && hiding == 0)
                    EndRound(HuntingTeam, "The hunters found every prop!");
                else if (time >= PhaseEnds)
                    EndRound(hiding > 0 ? 1 - HuntingTeam : -1, hiding > 0 ? "The props stayed hidden!" : "Nobody wins the round");
                break;

            case Phase.RoundOver when time >= PhaseEnds:
                NextRound();
                break;
        }
    }

    // A round's over: a point for the team that won it (the score limit's rounds end the match),
    // and a round won for each of its players (the scoreboard's column)
    void EndRound(int winner, string text)
    {
        RoundWinner = winner;
        StartPhase(Phase.RoundOver, RoundOverTics);
        Announce(winner >= 0 ? $"{text} {GameModes.TeamLabel(winner)} wins round {Round}." : text, "misc/100percent");
        if (winner < 0)
            return;
        foreach (var p in Session.Players.Where(p => !p.Gone && p.Team == winner))
            p.Objective++;
        Match.AddTeamScore(winner, 1);
    }

    // The sides swap, and everyone comes back afresh in their new part, the props to hide
    void NextRound()
    {
        Round++;
        HuntingTeam = 1 - HuntingTeam;
        RoundWinner = -1;
        foreach (var p in Session.Players.Where(p => !p.Gone && p.Pawn != null))
        {
            p.Disguise = null;
            p.RoleClass = IsProp(p) ? PropClass : null;     // before coming back: their loadout is the part's
            Hooks.Respawn?.Invoke(p);
        }
        StartPhase(Phase.Hide, HideTics);
        Announce($"Round {Round}: {GameModes.TeamLabel(HuntingTeam)} hunts. Props, hide!");
    }

    /// <summary>
    /// Each player's part (a prop's loadout when they become one), and a living prop without a
    /// look gets one of the level's decorations at random. No prop heals past the most its look allows.
    /// </summary>
    void UpdateParts()
    {
        foreach (var p in Session.Players)
        {
            if (p.Gone || p.Pawn == null)
                continue;

            bool prop = IsProp(p);
            var role = prop ? PropClass : null;
            if (p.RoleClass != role)
            {
                p.RoleClass = role;
                if (p.Health > 0)
                    Hooks.Rearm?.Invoke(p);     // the dead get it as they come back
            }

            if (!prop || p.Health <= 0)
                p.Disguise = null;
            else if (p.Disguise == null && Disguises.Count > 0)
                Disguise(p, Disguises[((Session.Random.RndT() << 8) | Session.Random.RndT()) % Disguises.Count]);

            if (prop && p.Disguise != null && p.Health > MostHealth(p.Disguise))
            {
                p.Health = (short)MostHealth(p.Disguise);
                Hooks.ShowHealth?.Invoke(p);
            }
        }
    }

    /// <summary>A prop who dies drops their look (their own body falls), and everyone hears of it</summary>
    public void PlayerDied(Entities.PlayerState victim)
    {
        if (victim.Disguise != null && CurrentPhase == Phase.Hunt)
            Announce($"{NameOf(victim)} was found!");
        victim.Disguise = null;
    }

    /// <summary>The dead stay dead until the next round</summary>
    public bool MayRespawn(Entities.PlayerState p) => false;

    /// <summary>
    /// A hunter's shot can hit any decoration a prop could look like, during the hunt: a real
    /// one costs them (PlayerAttacks.HitShotTarget)
    /// </summary>
    public bool IsDecoy(Entities.Actors.Actor actor) =>
        CurrentPhase == Phase.Hunt && Session.PlayerState.Team == HuntingTeam
        && DisguiseHealth.ContainsKey(actor.Name) && CanDisguiseAs(actor);

    /// <summary>A disguised prop's most health is its look's (the status bar's face goes by its share of it)</summary>
    public int? MostHealth(Entities.PlayerState p) => p.Disguise != null ? MostHealth(p.Disguise) : null;

    /// <summary>A living, disguised prop sees from behind itself, to see what it looks like</summary>
    public bool ChaseCam(Entities.PlayerState p) => p.Disguise != null && p.Health > 0 && IsProp(p);

    /// <summary>The hunters wait, still and seeing nothing, while the props hide</summary>
    public bool IsWaiting(Entities.PlayerState p) => CurrentPhase == Phase.Hide && p.Team == HuntingTeam;

    /// <summary>
    /// A living prop pressing use facing a decoration they can look like (within a tile and a
    /// half, half a right angle either side of straight ahead, or on their own tile) takes on its look
    /// </summary>
    public bool PlayerUse()
    {
        var p = Session.PlayerState;
        var input = Session.PlayerInput;
        if (!IsProp(p) || p.Health <= 0 || input.IsHeld(ButtonType.Use) || DecorationAhead() is not { } decoration)
            return false;

        input.SetHeld(ButtonType.Use, true);
        if (!string.Equals(p.Disguise, decoration.Name, StringComparison.Ordinal))
        {
            Disguise(p, decoration.Name);
            if (Session.ActingIsLocal)
                Hud.Show(HudMessageKind.Other, $"Disguised as {LookName(decoration.Name)}");
        }
        return true;
    }

    // The nearest decoration a prop could look like in front of the acting player (as PlayerUse.TryUseActor finds one to use)
    Entities.Actors.Actor? DecorationAhead()
    {
        const int Angles = GameConstants.Angles;
        const long Reach = MapConstants.TileGlobal * 3 / 2;
        var player = Session.Player;
        Entities.Actors.Actor? chosen = null;
        long best = Reach;
        foreach (var actor in Map.GetActors())
        {
            if (!CanDisguiseAs(actor))
                continue;

            long dx = actor.X - player.X, dy = player.Y - actor.Y;     // y grows southward
            long dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (dist >= best)
                continue;

            bool underfoot = actor.TileX == player.TileX && actor.TileY == player.TileY;
            int toward = (int)((Math.Atan2(dy, dx) + Math.PI * 2) % (Math.PI * 2) / (Math.PI * 2) * Angles);
            int off = Math.Abs(player.Angle - toward) % Angles;
            if (!underfoot && Math.Min(off, Angles - off) > Angles / 8)
                continue;

            (chosen, best) = (actor, dist);
        }
        return chosen;
    }

    public string? StatusLine()
    {
        var me = Session.LocalPlayer;
        int left = LivingPawns.Count(pawn => IsProp(pawn.State));
        int seconds = Math.Max(0, PhaseEnds - Session.GameState.TimeCount + 69) / 70;
        var clock = $"{seconds / 60}:{seconds % 60:00}";
        var news = CurrentPhase switch
        {
            Phase.Hide => $"Round {Round}: props hiding {clock}",
            Phase.Hunt => $"Round {Round}: {left} prop{(left == 1 ? "" : "s")} left {clock}",
            _ => RoundWinner >= 0 ? $"{GameModes.TeamLabel(RoundWinner)} wins round {Round}" : $"Round {Round} is a draw",
        };
        var part = IsProp(me) ? me.Disguise is { } look ? $"Prop ({LookName(look)})" : "Prop"
            : me.Team is 0 or 1 ? "Hunter" : null;
        return part != null ? $"{part} - {news}" : news;
    }

    public string? Details() =>
        $"round {Round} ({CurrentPhase}, {Math.Max(0, PhaseEnds - Session.GameState.TimeCount) / 70}s left); {GameModes.TeamLabel(HuntingTeam)} hunts; props: "
        + string.Join(", ", Session.Players.Where(IsProp).Select(p =>
            $"{NameOf(p)} as {p.Disguise ?? "themselves"} holding {p.Weapon ?? "nothing"}, health {p.Health}/{MostHealth(p.Disguise)}"))
        + $"; {Disguises.Count} looks: {string.Join(", ", Disguises.Select(d => $"{d} ({MostHealth(d)})"))}";

    public override void Write(BinaryWriter bw)
    {
        bw.Write(HuntingTeam);
        bw.Write(Round);
        bw.Write((byte)CurrentPhase);
        bw.Write(PhaseEnds);
        bw.Write(RoundWinner);
        bw.Write(Disguises.Count);
        foreach (var look in Disguises)
        {
            bw.Write(look);
            bw.Write(MostHealth(look));
        }
    }

    public override void Read(BinaryReader br)
    {
        HuntingTeam = br.ReadInt32();
        Round = br.ReadInt32();
        CurrentPhase = (Phase)br.ReadByte();
        PhaseEnds = br.ReadInt32();
        RoundWinner = br.ReadInt32();
        Disguises.Clear();
        DisguiseHealth.Clear();
        for (int i = br.ReadInt32(); i > 0; i--)
        {
            var look = br.ReadString();
            Disguises.Add(look);
            DisguiseHealth[look] = br.ReadInt32();
        }
    }
}
