using PFWolf.Assets;
using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/*
=============================================================================

                                  STEALTH

Going unseen and in disguise (Midnight Assault's stealthActive, k_uniform and wp_idpapers:
wl_state.c SuspectPlayer and FirstSighting, wl_agent.c ShowPapers).

Each level starts with the player unseen (PlayerState.Undetected); the first enemy to notice
them ends it, and on a stealth-only map (game-info map `stealth-only`) that's the mission failed,
as a death. Kills made unseen with a `weapon.stealthkill` weapon (the knife) or through a zoomed
scope are counted (PlayerState.StealthKills).

Unseen, wearing a disguise (an item with `inventory.disguise: true`) and with papers in hand (a
weapon with `weapon.papers: true`), an enemy that sees them only suspects them: it gives them
stealth `suspicion-tics` (10 seconds, on the HUD's meter) to show their papers (the papers' fire,
A_ShowPapers) before it notices them; shown them, it lets them be. One farther than stealth
`range` tiles away (either way) ignores them. An enemy's `monster.disguise` can be `fooled` (it
never suspects them: the dog) or `seesthrough` (it notices them as ever: the commanders, the
tank); one with `monster.papersfail` (a message: the officer) isn't fooled by the papers, and
the player's no longer unseen. Noticed, the disguise and papers are lost.

A_Disguise("Disguise", "Papers"[, "State"]), on something the player uses (the coat rack), puts
the disguise on and the papers in their pocket and makes them unseen again, unless an enemy is
already after them (stealth `alert`).

=============================================================================
*/

/// <param name="Settings">Game-info stealth</param>
/// <param name="StealthOnly">Whether the level is a stealth-only one (game-info map stealth-only)</param>
/// <param name="ShowMessage">A message to the acting player (text or a $language key)</param>
/// <param name="PlaySound">A sound the acting player makes</param>
/// <param name="PlayAttackSound">The weapon in hand's attacksound (the papers' rustle)</param>
/// <param name="InSight">Whether an actor is in an area joined to the acting player's, with a clear line to them</param>
/// <param name="Caught">The acting player is caught by the actor: the mission's failed, as a death</param>
internal sealed record StealthHooks(
    Func<StealthInfo> Settings,
    Func<bool> StealthOnly,
    Action<string> ShowMessage,
    Action<string> PlaySound,
    Action PlayAttackSound,
    Func<Entities.Actors.Actor, bool> InSight,
    Action<Entities.Actors.Actor> Caught);

internal sealed class Stealth(MapManager map, InventoryManager inventory, PlayerWeapons weapons, GameSession session, StealthHooks hooks)
{
    const int Tile = 1 << (int)Constants.MapConstants.TILESHIFT;

    /// <summary>Whether a monster in its standing or patrolling think lets the disguised player be (no sighting this tic)</summary>
    internal bool Overlooks(Entities.Actors.Monster monster)
    {
        // Anyone it could see who isn't in disguise, it sees as ever
        var pawns = map.Players.Where(p => p.State.health > 0).ToList();
        if (pawns.Count == 0 || !pawns.All(p => Disguised(p.State)))
            return false;

        var settings = hooks.Settings();
        int range = Math.Max(settings.Range, 1) * Tile;
        var pawn = pawns.MinBy(p => Math.Max(Math.Abs(p.X - monster.X), Math.Abs(p.Y - monster.Y)))!;
        if (Math.Abs(pawn.X - monster.X) > range || Math.Abs(pawn.Y - monster.Y) > range)
            return true;            // too far off to care

        int max = Math.Max(settings.SuspicionTics, 1);
        if (monster.Suspicion > 0)
        {
            var state = pawn.State;
            state.SuspicionPending = Math.Max(state.SuspicionPending, Math.Max(monster.Suspicion * 100 / max, 1));
            monster.Suspicion -= (int)session.tics;
            if (monster.Suspicion > 0)
                return true;
            monster.Suspicion = 0;          // out of patience: it notices them as ever
            return false;
        }
        if (monster.Suspicion < 0)
            return true;                    // shown their papers

        switch (DisguiseMode(monster))
        {
            case "seesthrough":
                return false;
            case "fooled":
                monster.Suspicion = -1;
                return true;
        }

        if (monster.CheckSight())
        {
            var suspected = monster.Target as Entities.Actors.PlayerPawn ?? pawn;
            if (suspected.State.SuspicionMeter == 0 && suspected.State.SuspicionPending == 0 && settings.Suspected is { Length: > 0 } message)
            {
                using var _ = session.ActAs(suspected.State);
                hooks.ShowMessage(message);
            }
            monster.Suspicion = max;
            suspected.State.SuspicionPending = Math.Max(suspected.State.SuspicionPending, 100);
        }
        return true;
    }

    static string DisguiseMode(Entities.Actors.Monster monster) =>
        monster.Properties.TryGetValue("monster.disguise", out var mode) ? mode?.ToString()?.Trim().ToLowerInvariant() ?? "" : "";

    /// <summary>
    /// A monster has noticed someone (FirstSighting): if it's a player, they're no longer unseen,
    /// and lose their disguise. True when it's a stealth-only map's failure (the player's caught,
    /// and the monster needn't go after them).
    /// </summary>
    internal bool Spotted(Entities.Actors.Monster monster)
    {
        var pawn = monster.TargetIfAny as Entities.Actors.PlayerPawn
            ?? (map.HasRivalries ? null : map.Players.FirstOrDefault());
        if (pawn == null)
            return false;

        using var _ = session.ActAs(pawn.State);
        var playerstate = session.playerstate;
        if (hooks.StealthOnly())
        {
            if (playerstate.health > 0)
            {
                if (hooks.Settings().Caught is { Length: > 0 } caught)
                    hooks.ShowMessage(caught);
                hooks.Caught(monster);
            }
            return true;
        }

        var settings = hooks.Settings();
        bool disguised = DisguiseItems().Any();
        if (playerstate.Undetected && (disguised || playerstate.StealthKills > 0)
            && (disguised ? settings.Blown : settings.Lost) is { Length: > 0 } message)
            hooks.ShowMessage(message);

        playerstate.Undetected = false;
        playerstate.SuspicionPending = playerstate.SuspicionMeter = 0;
        TakeDisguise();
        return false;
    }

    /// <summary>A kill by a player: counted as unseen while they're unseen, with a stealth-kill weapon or a zoomed scope</summary>
    internal void Killed(Entities.Actors.PlayerPawn killer)
    {
        var state = killer.State;
        if (state.Undetected && (state.ZoomMode == ZoomMode.Zoomed
            || state.weapon is { } weapon && IsTrue(inventory.GetProperty(weapon, "weapon.stealthkill"))))
            state.StealthKills++;
    }

    /// <summary>
    /// A_ShowPapers: the papers in hand are shown (their attacksound) to every enemy in sight
    /// that suspects the player: each is satisfied, unless its monster.papersfail says it isn't
    /// fooled, when the player's no longer unseen
    /// </summary>
    internal void ShowPapers(Entities.Actors.Actor weapon)
    {
        hooks.PlayAttackSound();
        var playerstate = session.playerstate;
        foreach (var monster in map.GetActors().OfType<Entities.Actors.Monster>().ToList())
        {
            if (monster.IsRemoved || monster.Suspicion <= 0 || monster.RuntimeFlags.HasFlag(ObjFlags.AttackMode)
                || !monster.RuntimeFlags.HasFlag(ObjFlags.Shootable) || !monster.RuntimeFlags.HasFlag(ObjFlags.Visible))
                continue;
            if (!hooks.InSight(monster))
                continue;

            monster.Suspicion = -1;
            if (monster.Properties.TryGetValue("monster.papersfail", out var fail) && fail?.ToString() is { Length: > 0 } message)
            {
                hooks.ShowMessage(message);
                playerstate.Undetected = false;
            }
        }
    }

    /// <summary>
    /// A_Disguise("Disguise", "Papers"[, "State"]): see above. The disguise's pickupmessage and
    /// pickupsound go with it, and the thing used goes to the state.
    /// </summary>
    internal void Disguise(Entities.Actors.Actor ob, string[] args)
    {
        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]))
            return;
        if (EnemiesAfterThePlayers())
        {
            if (hooks.Settings().Alert is { Length: > 0 } alert)
                hooks.ShowMessage(alert);
            return;
        }

        var disguise = args[0].Trim();
        var papers = args[1].Trim();
        inventory.Give(disguise, 1);
        if (papers.Length > 0)
            inventory.Give(papers, 1);
        session.playerstate.Undetected = true;
        weapons.DrawWeapon();

        if (inventory.GetStringProperty(disguise, "inventory.pickupmessage") is { Length: > 0 } message)
            hooks.ShowMessage(message);
        if (inventory.GetStringProperty(disguise, "inventory.pickupsound") is { Length: > 0 } sound)
            hooks.PlaySound(sound);
        if (args.Length > 2 && ob.ResolvedStates.TryGetValue(args[2].Trim(), out var next))
            ob.JumpTo(next);
    }

    /// <summary>Each player think: the meter shows what the enemies told it last tic</summary>
    internal void Tick()
    {
        var playerstate = session.playerstate;
        playerstate.SuspicionMeter = playerstate.SuspicionPending;
        playerstate.SuspicionPending = 0;
    }

    /// <summary>Whether the player's wearing a disguise (an item with inventory.disguise), papers in hand or not</summary>
    internal bool WearingDisguise(Entities.PlayerState state) =>
        state.Items.Any(item => item.Value > 0 && IsTrue(inventory.GetProperty(item.Key, "inventory.disguise")));

    /// <summary>Whether the player's unseen, in disguise and with papers in hand</summary>
    internal bool Disguised(Entities.PlayerState state)
    {
        if (!state.Undetected || state.weapon is not { } weapon || !IsTrue(inventory.GetProperty(weapon, "weapon.papers")))
            return false;
        return state.Items.Any(item => item.Value > 0 && IsTrue(inventory.GetProperty(item.Key, "inventory.disguise")));
    }

    // Any living enemy going after someone (the original's CheckIfActorsAwake)
    bool EnemiesAfterThePlayers() =>
        map.GetActors().OfType<Entities.Actors.Monster>()
            .Any(m => !m.IsRemoved && m.Hitpoints > 0 && m.RuntimeFlags.HasFlag(ObjFlags.AttackMode));

    IEnumerable<string> DisguiseItems() =>
        session.playerstate.Items.Where(item => item.Value > 0 && IsTrue(inventory.GetProperty(item.Key, "inventory.disguise")))
            .Select(item => item.Key).ToList();

    // Off with the disguise and papers (the papers' weapon out of hand)
    void TakeDisguise()
    {
        var playerstate = session.playerstate;
        var gone = DisguiseItems()
            .Concat(playerstate.Items.Where(item => item.Value > 0 && IsTrue(inventory.GetProperty(item.Key, "weapon.papers"))).Select(item => item.Key))
            .ToList();
        if (gone.Count == 0)
            return;
        foreach (var item in gone)
            inventory.Take(item, playerstate.Items[item]);

        if (playerstate.weapon is { } weapon && !inventory.Has(weapon))
            playerstate.weapon = playerstate.chosenweapon = weapons.BestWeapon(canFire: true);
        else if (playerstate.chosenweapon is { } chosen && !inventory.Has(chosen))
            playerstate.chosenweapon = playerstate.weapon;
        weapons.DrawWeapon();
    }

    static bool IsTrue(object? value) => value is true || value != null && bool.TryParse(value.ToString(), out var b) && b;
}
