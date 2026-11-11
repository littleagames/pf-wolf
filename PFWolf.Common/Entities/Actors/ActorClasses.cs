namespace PFWolf.Entities.Actors;

/// <summary>
/// The actor classes the engine has in code (PFWolf.Entities.Actors): an actordefs class is the
/// nearest of itself and its parents that names one, else a plain Actor. Kept in step with the
/// engine by a test, so the editor, which can't look at the engine's types, knows them too.
/// </summary>
public static class ActorClasses
{
    /// <summary>The engine's classes, each with the one it's built on (Actor's is null)</summary>
    public static IReadOnlyDictionary<string, string?> Engine { get; } = new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["Actor"] = null,
        ["Monster"] = "Actor",
        ["BlakeMonster"] = "Monster",
        ["Informant"] = "BlakeMonster",
        ["PlayerPawn"] = "Actor",
        ["DeathCam"] = "Actor",
        ["Inventory"] = "Actor",
        ["CustomInventory"] = "Inventory",
        ["Ammo"] = "Inventory",
        ["Health"] = "Inventory",
        ["BasicArmor"] = "Inventory",
        ["Key"] = "Inventory",
        ["ScoreItem"] = "Inventory",
        ["Powerup"] = "Inventory",
        ["BackpackItem"] = "Inventory",
        ["Weapon"] = "Inventory",
    };

    /// <summary>
    /// The engine class a class is: the first of its name and its parents' (nearest first) the
    /// engine has, else Actor. Inventory itself is abstract, so a class made straight from it is a
    /// CustomInventory (as ActorMetadata.CreateActor makes it).
    /// </summary>
    public static string EngineClass(string name, IEnumerable<string> ancestorsNearestFirst)
    {
        var found = new[] { name }.Concat(ancestorsNearestFirst).FirstOrDefault(Engine.ContainsKey) ?? "Actor";
        return found == "Inventory" ? "CustomInventory" : found;
    }

    /// <summary>Whether an engine class is <paramref name="needs"/> or built on it (a BlakeMonster is a Monster)</summary>
    public static bool IsA(string engineClass, string needs)
    {
        for (string? type = engineClass; type != null; type = Engine.GetValueOrDefault(type))
            if (type.Equals(needs, StringComparison.Ordinal))
                return true;
        return false;
    }

    /// <summary>The classes the engine itself makes or looks for by name, with what for</summary>
    public static IReadOnlyDictionary<string, string> EngineUses { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DeathCam"] = "the engine: the death cam after a boss dies",
        ["Smoke"] = "the engine: a rocket's smoke trail",
        ["GreenOoze"] = "the engine: Blake Stone's ooze trail",
        ["ScanAlien"] = "the engine: what a Blake Stone pod alien hatches",
        ["Fireball"] = "the engine: the projectile a fireball trap fires",
        ["Player"] = "the engine: the player classes' base class",
        ["PropPlayer"] = "the engine: Prop Hunt's prop",
        ["ObjectiveFlag"] = "the engine: Capture the Flag's flag",
        ["BackpackItem"] = "the engine: what raises ammo limits",
        ["Powerup"] = "the engine: the power-ups' base class",
    };
}
