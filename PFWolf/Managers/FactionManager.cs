using PFWolf.Assets;

namespace PFWolf.Managers;

/// <summary>
/// The sides actors are on (the game pack's factions.yaml, read as a FactionTable) and how each
/// treats the others. An actor's side is set as it spawns (Actor.Faction): a mapdefs thing's
/// `faction:`, else its class's `faction` property, else the pack's monster-faction for an
/// enemy (Monster); a player's is their class's `faction`, else player-faction. Without a
/// factions.yaml every enemy is a monster and every player one of the players, hostile to each
/// other, which is how the games have always played.
/// </summary>
internal class FactionManager
{
    private const string PropertyName = "faction";

    public FactionManager(Lazy<AssetManager> assetManager, Lazy<InventoryManager> inventoryManager)
    {
        this.assetManager = assetManager;
        this.inventoryManager = inventoryManager;
    }

    private readonly Lazy<AssetManager> assetManager;
    private readonly Lazy<InventoryManager> inventoryManager;
    private FactionTable? table;

    // Faction names already warned about as unknown, so each is warned about once
    private readonly HashSet<string> warnedUnknown = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The running pack's factions (read the first time they're needed)</summary>
    internal FactionTable Table
    {
        get
        {
            if (table == null)
            {
                table = new FactionTable(assetManager.Value.FindInGamePackIfAny<FactionsAsset>("factions"));
                foreach (var warning in table.Warnings)
                    Console.WriteLine(warning);
            }
            return table;
        }
    }

    /// <summary>
    /// The side an actor spawning is on: <paramref name="placed"/> (a mapdefs thing's `faction:`)
    /// when given, else its class's `faction`, else monster-faction for an enemy; null for
    /// anything else (a decoration, a pickup)
    /// </summary>
    internal string? SpawnFaction(Entities.Actors.Actor actor, string? placed = null)
    {
        if (!string.IsNullOrWhiteSpace(placed))
            return Checked(placed.Trim(), $"a map thing ({actor.Name})");
        if (ClassFaction(actor.Properties) is { } own)
            return Checked(own, actor.Name);
        return actor is Entities.Actors.Monster ? Table.MonsterFaction : null;
    }

    /// <summary>
    /// The side an actor another brings into being is on (a thrown projectile, an alien out of
    /// its canister): its class's own `faction` if it has one, else the side of the actor it
    /// came from, else as <see cref="SpawnFaction"/>
    /// </summary>
    internal string? InheritedFaction(Entities.Actors.Actor actor, Entities.Actors.Actor from) =>
        ClassFaction(actor.Properties) is { } own ? Checked(own, actor.Name) : from.Faction ?? SpawnFaction(actor);

    /// <summary>The side a player of a class is on: the class's `faction`, else player-faction</summary>
    internal string PlayerFaction(string? playerClass)
    {
        if (!string.IsNullOrEmpty(playerClass) && inventoryManager.Value.GetStringProperty(playerClass, PropertyName) is { Length: > 0 } own)
            return Checked(own.Trim(), playerClass);
        return Table.PlayerFaction;
    }

    /// <summary>How <paramref name="from"/> treats <paramref name="to"/>, by their sides</summary>
    internal FactionRelation Relation(Entities.Actors.Actor from, Entities.Actors.Actor to) =>
        Table.Relation(from.Faction, to.Faction);

    internal bool IsHostile(Entities.Actors.Actor from, Entities.Actors.Actor to) =>
        Relation(from, to) == FactionRelation.Hostile;

    internal bool IsAlly(Entities.Actors.Actor from, Entities.Actors.Actor to) =>
        Relation(from, to) == FactionRelation.Ally;

    /// <summary>
    /// Warns (once, at startup) about each actordefs class whose `faction` isn't in factions.yaml,
    /// and about anything wrong in factions.yaml itself
    /// </summary>
    internal void CheckClasses(ActorMetadata metadata)
    {
        _ = Table;      // its own warnings
        foreach (var name in metadata.Actors.Keys)
        {
            if (metadata.TryGetProperty(name, PropertyName, out var value) && value?.ToString() is { Length: > 0 } faction)
                Checked(faction.Trim(), name);
        }
    }

    // A class's own `faction` property, or null
    private static string? ClassFaction(Dictionary<string, object> properties) =>
        properties.TryGetValue(PropertyName, out var value) && value?.ToString() is { Length: > 0 } faction ? faction.Trim() : null;

    // The faction, warning once when factions.yaml doesn't have it (it's still a side of its own)
    private string Checked(string faction, string where)
    {
        if (!Table.IsKnown(faction) && warnedUnknown.Add(faction))
            Console.WriteLine($"{where} is on faction \"{faction}\", which isn't in factions.yaml ({string.Join(", ", Table.Names)})");
        return faction;
    }
}
