using PFWolf.Assets;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/*
=============================================================================

                                KILL CHAINS

Kills in quick succession (Midnight Assault's ma_killchain.c). A player's kill within game-info
kill-chain `tics` of their last carries their chain on; otherwise it starts a new one. From two
kills on, each shows the chain's message (`messages`, the last for any longer chain) and drops its
prize where the enemy fell (`prizes`: one of the chain's list, at random). A kill made unseen
(Gameplay/Stealth.cs) also gives the prize of a chain of 1 up to `stealth-prize`, at random (1
gives nothing).

Only kills that count (the kill ratio's) make chains. An enemy whose class has `killchain: false`
(the commanders) isn't part of any; `killchain: noprize` (the dog) carries the chain on but drops
nothing.

A prize is an item class dropped there, "Item if Weapon" only while the player holds the weapon
(nothing otherwise), or "heal N", healing them N points.

=============================================================================
*/

/// <param name="Settings">Game-info kill-chain</param>
/// <param name="ShowMessage">A message to the acting player (text or a $language key)</param>
/// <param name="Holds">Whether the acting player holds an item (a weapon)</param>
/// <param name="Drop">Drops an item class on a tile</param>
/// <param name="Heal">Heals the acting player some points</param>
internal sealed record KillChainHooks(
    Func<KillChainInfo> Settings,
    Action<string> ShowMessage,
    Func<string, bool> Holds,
    Action<int, int, string> Drop,
    Action<int> Heal);

internal sealed class KillChains(GameSession session, KillChainHooks hooks)
{
    /// <summary>A player's kill of a counted enemy on that tile; unseen if it's a stealth kill</summary>
    internal void Killed(Entities.Actors.Actor enemy, Entities.PlayerState killer, int tilex, int tiley, bool unseen)
    {
        var settings = hooks.Settings();
        if (settings.Tics <= 0)
            return;
        string mode = enemy.Properties.TryGetValue("killchain", out var value) ? value?.ToString()?.Trim().ToLowerInvariant() ?? "" : "";
        if (mode == "false")
            return;

        int now = session.GameState.TimeCount;
        int since = now - killer.KillChainAt;
        killer.KillChain = killer.KillChain > 0 && since >= 0 && since < settings.Tics ? killer.KillChain + 1 : 1;
        killer.KillChainAt = now;

        using var _ = session.ActAs(killer);
        if (killer.KillChain >= 2 && settings.Messages.Count > 0)
            hooks.ShowMessage(settings.Messages[Math.Min(killer.KillChain - 2, settings.Messages.Count - 1)]);
        if (mode == "noprize")
            return;
        Prize(settings, killer.KillChain, tilex, tiley);
        if (unseen && settings.StealthPrize > 1)
            Prize(settings, 1 + session.Random.RndT() % settings.StealthPrize, tilex, tiley);
    }

    // A chain's prize (none under two kills, or for a chain with an empty list)
    void Prize(KillChainInfo settings, int chain, int tilex, int tiley)
    {
        if (chain < 2 || settings.Prizes.Count == 0)
            return;
        var choices = settings.Prizes[Math.Min(chain - 2, settings.Prizes.Count - 1)];
        if (choices.Count == 0)
            return;
        var prize = choices[session.Random.RndT() % choices.Count].Trim();

        var words = prize.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 2 && words[0].Equals("heal", StringComparison.OrdinalIgnoreCase) && int.TryParse(words[1], out var points))
            hooks.Heal(points);
        else if (words.Length == 3 && words[1].Equals("if", StringComparison.OrdinalIgnoreCase))
        {
            if (hooks.Holds(words[2]))
                hooks.Drop(tilex, tiley, words[0]);
        }
        else if (words.Length == 1)
            hooks.Drop(tilex, tiley, words[0]);
        else
            Console.WriteLine($"kill-chain: \"{prize}\" isn't an item, \"Item if Weapon\" or \"heal N\".");
    }
}
