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

    The red team hunts. (Rounds, swapping sides, are to come.)

=============================================================================
*/

internal sealed class PropHunt(MapManager map, GameSession session, Deathmatch match, HudMessageManager hud, MatchModeHooks hooks)
    : MatchObjective(map, session, match, hud, hooks), IMatchObjective
{
    public override GameMode Mode => GameMode.PropHunt;

    /// <summary>The player class whose loadout and weapons props have (actordefs prophunt.yaml)</summary>
    internal const string PropClass = "PropPlayer";

    /// <summary>The team hunting (0 red, 1 blue); the other hides</summary>
    internal int HuntingTeam;

    /// <summary>The decoration classes on the level a prop can look like, in the order first placed</summary>
    internal readonly List<string> Disguises = [];

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

    public override void LevelSetup()
    {
        HuntingTeam = 0;
        Disguises.Clear();
        Disguises.AddRange(Map.GetActors().Where(CanDisguiseAs).Select(a => a.Name).Distinct());
    }

    /// <summary>
    /// Each frame: each player's part (a prop's loadout when they become one), and a living prop
    /// without a look gets one of the level's decorations at random
    /// </summary>
    public override void Tick()
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
                p.Disguise = Disguises[((Session.Random.RndT() << 8) | Session.Random.RndT()) % Disguises.Count];
        }
    }

    /// <summary>A prop who dies drops their look: their own body falls</summary>
    public void PlayerDied(Entities.PlayerState victim) => victim.Disguise = null;

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
            p.Disguise = decoration.Name;
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
        var count = $"{left} prop{(left == 1 ? "" : "s")} left";
        if (!IsProp(me))
            return me.Team is 0 or 1 ? $"Hunter - {count}" : count;
        return me.Disguise is { } look ? $"Prop ({LookName(look)}) - {count}" : $"Prop - {count}";
    }

    public string? Details() =>
        $"{GameModes.TeamLabel(HuntingTeam)} hunts; props: "
        + string.Join(", ", Session.Players.Where(IsProp).Select(p => $"{NameOf(p)} as {p.Disguise ?? "themselves"} holding {p.Weapon ?? "nothing"}"))
        + $"; {Disguises.Count} looks: {string.Join(", ", Disguises)}";

    public override void Write(BinaryWriter bw)
    {
        bw.Write(HuntingTeam);
        bw.Write(Disguises.Count);
        foreach (var look in Disguises)
            bw.Write(look);
    }

    public override void Read(BinaryReader br)
    {
        HuntingTeam = br.ReadInt32();
        Disguises.Clear();
        for (int i = br.ReadInt32(); i > 0; i--)
            Disguises.Add(br.ReadString());
    }
}
