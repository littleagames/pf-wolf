using PFWolf.Entities.Actors;

namespace PFWolf.Gameplay;

/// <summary>What the actor actions that act on the player need from the game</summary>
/// <param name="SetPendingMapChange">The map to switch to as the level ends (Gameplay/MainGameLoop.cs)</param>
internal sealed record PlayerActionsHooks(
    Action<PendingMapChange> SetPendingMapChange);

/// <summary>
/// Actor actions that act on the player: an extra life (A_GiveExtraMan), an item
/// (A_GiveInventory), and a move to another map with no intermission (A_ChangeMap).
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
    }

    /// <summary>
    /// A_ChangeMap("MAP21"[, "keep-position"]): ends the level and moves to another map with no
    /// intermission. With keep-position the player keeps their spot and facing on the new map,
    /// as when picking up the Spear of Destiny.
    /// </summary>
    internal void ChangeMapAction(Entities.Actors.Actor actor, string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("A_ChangeMap: no map given.");
            return;
        }

        var map = engine.GetGameInfo().Maps.Keys
            .FirstOrDefault(k => k.Equals(args[0], StringComparison.OrdinalIgnoreCase));
        if (map == null)
        {
            Console.WriteLine($"A_ChangeMap: unknown map '{args[0]}'.");
            return;
        }

        var keepPosition = args.Skip(1).Any(a => a.Equals("keep-position", StringComparison.OrdinalIgnoreCase));
        hooks.SetPendingMapChange(new PendingMapChange(map, keepPosition, session.Player.X, session.Player.Y, session.Player.Angle));
        session.GameState.MapOn = map;
        session.GameState.Entrance = "";       // by no entrance in particular: the level's own start
        session.PlayState = PlayState.Warped;
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
