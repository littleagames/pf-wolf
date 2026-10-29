using PFWolf.Entities.Actors;

namespace PFWolf.Gameplay;

/// <summary>What the actor actions that act on the player need from the game</summary>
/// <param name="SetPendingMapChange">The map to switch to as the level ends (Gameplay/MainGameLoop.cs)</param>
/// <param name="WaitSoundDone">Waits for a sound that's playing (a switch's) to finish</param>
internal sealed record PlayerActionsHooks(
    Action<PendingMapChange> SetPendingMapChange,
    Action? WaitSoundDone = null);

/// <summary>
/// Actor actions that act on the player: an extra life (A_GiveExtraMan), an item
/// (A_GiveInventory), and a move to another map with no intermission (A_ChangeMap, which
/// switches and triggers run too: a hub level's way to each of its levels).
/// </summary>
internal sealed class PlayerActions(
    GameEngineManager engine,
    AssetManager assets,
    PlayerStats stats,
    GameSession session,
    PlayerActionsHooks hooks)
{
    internal void Register()
    {
        ActorActionRegistry.Register("A_GiveExtraMan", (Entities.Actors.Actor _) => stats.GiveExtraMan());
        ActorActionRegistry.Register("A_GiveInventory", GiveInventoryAction);
        ActorActionRegistry.Register("A_ChangeMap", ChangeMapAction);
        // A switch's or trigger's waits only for a sound that's playing (the switch's), as A_Exit does
        Entities.MapTriggerRegistry.Register("A_ChangeMap", (_, args) =>
        {
            if (!ChangeMap(args, waitTics: 0))
                return false;
            hooks.WaitSoundDone?.Invoke();
            return true;
        });
    }

    /// <summary>
    /// A_ChangeMap("MAP21"[, "keep-position"][, "entrance"]): ends the level and moves to another
    /// map with no intermission. With keep-position the player keeps their spot and facing on the
    /// new map, as when picking up the Spear of Destiny. Any other word is the entrance the map
    /// is entered by: its mapdefs player start with that entrance (on a kept hub level too).
    /// </summary>
    internal void ChangeMapAction(Entities.Actors.Actor actor, string[] args) => ChangeMap(args, waitTics: 150);

    bool ChangeMap(string[] args, int waitTics)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("A_ChangeMap: no map given.");
            return false;
        }

        var map = engine.GetGameInfo().Maps.Keys
            .FirstOrDefault(k => k.Equals(args[0].Trim(), StringComparison.OrdinalIgnoreCase));
        if (map == null)
        {
            Console.WriteLine($"A_ChangeMap: unknown map '{args[0]}'.");
            return false;
        }

        var (keepPosition, entrance) = ChangeMapOptions(args.Skip(1));
        hooks.SetPendingMapChange(new PendingMapChange(map, keepPosition, session.Player.X, session.Player.Y, session.Player.Angle, waitTics));
        session.GameState.MapOn = map;
        session.GameState.Entrance = entrance;     // "": by no entrance in particular, the level's own start
        session.PlayState = PlayState.Warped;
        return true;
    }

    /// <summary>A_ChangeMap's words after the map: keep-position, and the entrance (the first other word; "" for none)</summary>
    internal static (bool KeepPosition, string Entrance) ChangeMapOptions(IEnumerable<string> words)
    {
        var trimmed = words.Select(w => w.Trim()).Where(w => w.Length > 0).ToList();
        bool keepPosition = trimmed.Any(w => w.Equals("keep-position", StringComparison.OrdinalIgnoreCase));
        var entrance = trimmed.FirstOrDefault(w => !w.Equals("keep-position", StringComparison.OrdinalIgnoreCase)) ?? "";
        return (keepPosition, entrance);
    }

    internal void GiveInventoryAction(Entities.Actors.Actor actor, string[] args)
    {
        if (args.Length == 0)
            return;

        var itemName = args[0];
        var actorMetadata = assets.GetActorMetadata();
        if (!actorMetadata.Actors.TryGetValue(itemName, out var itemData))
        {
            Console.WriteLine($"A_GiveInventory: unknown actor '{itemName}'.");
            return;
        }

        if (actorMetadata.CreateActor(itemName, itemData) is not Inventory item)
            return;

        int amount;
        if (args.Length > 1 && int.TryParse(args[1], out var explicitAmount))
            amount = explicitAmount;
        else if (item.Properties.TryGetValue("inventory.amount", out var defaultAmount))
            amount = Convert.ToInt32(defaultAmount);
        else
            return;

        stats.TryApplyInventory(item, amount);
    }
}
