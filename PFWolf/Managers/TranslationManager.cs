using PFWolf.Assets;

namespace PFWolf.Managers;

/// <summary>
/// The game pack's sprite color translations (translations.yaml), each built once against the
/// game palette into a table of 256 indices (TranslationTable), and which one an actor is drawn
/// through: its own (Actor.Translation: a mapdefs thing's `translation:`, its class's
/// `translation` property, or what a script or the console gave it), else for an enemy its
/// faction's (factions.yaml translation / class-translations), else for a player their color.
/// </summary>
internal class TranslationManager
{
    private const string PropertyName = "translation";

    public TranslationManager(Lazy<AssetManager> assetManager, Lazy<FactionManager> factionManager)
    {
        this.assetManager = assetManager;
        this.factionManager = factionManager;
    }

    private readonly Lazy<AssetManager> assetManager;
    private readonly Lazy<FactionManager> factionManager;
    private TranslationsAsset? definitions;
    private IReadOnlyList<PaletteColor>? palette;
    private readonly Dictionary<string, SpriteTranslation?> tables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> ancestries = new(StringComparer.OrdinalIgnoreCase);

    private TranslationsAsset Definitions =>
        definitions ??= assetManager.Value.FindInGamePackIfAny<TranslationsAsset>("translations") ?? new TranslationsAsset();

    /// <summary>The translations translations.yaml defines</summary>
    internal IEnumerable<string> Names => Definitions.Translations.Keys;

    internal bool Exists(string name) => Definitions.Translations.ContainsKey(name);

    /// <summary>
    /// A translation's table, or null for none (no name, or one translations.yaml doesn't have,
    /// warned about once)
    /// </summary>
    internal SpriteTranslation? Table(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        if (tables.TryGetValue(name, out var table))
            return table;

        if (!Definitions.Translations.TryGetValue(name, out var ranges))
        {
            Console.WriteLine($"No translation \"{name}\" in translations.yaml; drawing it in its own colors");
            return tables[name] = null;
        }

        var warnings = new List<string>();
        table = TranslationTable.BuildTranslation(ranges, Palette, name, warnings);
        foreach (var warning in warnings)
            Console.WriteLine(warning);
        return tables[name] = table;
    }

    private IReadOnlyList<PaletteColor> Palette
    {
        get
        {
            if (palette == null)
            {
                var name = assetManager.Value.GetGamePaletteName();
                palette = (string.IsNullOrEmpty(name) ? null : assetManager.Value.Find<Palette>(name))?.Colors ?? [];
            }
            return palette;
        }
    }

    /// <summary>
    /// What an actor spawning is drawn through of its own: <paramref name="placed"/> (a mapdefs
    /// thing's `translation:`), else its class's `translation` property; null for neither
    /// </summary>
    internal static string? SpawnTranslation(Entities.Actors.Actor actor, string? placed = null) =>
        !string.IsNullOrWhiteSpace(placed) ? placed.Trim() : ClassTranslation(actor);

    // Its class's `translation` property, or null
    internal static string? ClassTranslation(Entities.Actors.Actor actor) =>
        actor.Properties.TryGetValue(PropertyName, out var value) && value?.ToString() is { Length: > 0 } name ? name.Trim() : null;

    /// <summary>
    /// The translation an actor is drawn through, by name, or null: its own; else a player's
    /// color (or their class's `translation`); else an enemy's faction's
    /// </summary>
    internal string? TranslationOf(Entities.Actors.Actor actor)
    {
        if (actor.Translation != null)
            return actor.Translation;
        if (actor is Entities.Actors.PlayerPawn pawn)
            return pawn.State.CarryTranslation ?? pawn.State.Color ?? (pawn.State.Body is { } body ? ClassTranslation(body) : null);
        if (actor is Entities.Actors.Monster && actor.Faction != null && HasHealth(actor))
            return factionManager.Value.Table.Translation(actor.Faction, Ancestry(actor.Name));
        return null;
    }

    // An enemy with health, alive or dead: not BJ running off at the end, a projectile, ...
    private readonly Dictionary<string, bool> withHealth = new(StringComparer.OrdinalIgnoreCase);

    private bool HasHealth(Entities.Actors.Actor actor)
    {
        if (!withHealth.TryGetValue(actor.Name, out var has))
            withHealth[actor.Name] = has = actor.Properties.Keys.Any(k => k == "health" || k.StartsWith("health.", StringComparison.Ordinal));
        return has;
    }

    /// <summary>The table an actor is drawn through (TranslationOf), or null to draw it as it is</summary>
    internal SpriteTranslation? TableOf(Entities.Actors.Actor actor) => Table(TranslationOf(actor));

    // A class, then its parent, and so on (actordefs parent:)
    private List<string> Ancestry(string className)
    {
        if (ancestries.TryGetValue(className, out var chain))
            return chain;

        chain = [];
        var actors = assetManager.Value.GetActorMetadata().Actors;
        for (string? name = className; !string.IsNullOrEmpty(name) && !chain.Contains(name, StringComparer.OrdinalIgnoreCase);
             name = actors.TryGetValue(name, out var data) ? data.Parent : null)
            chain.Add(name);
        return ancestries[className] = chain;
    }

    /// <summary>Warns (once, at startup) about the translations that can't be read, and the ones classes and factions name that aren't there</summary>
    internal void CheckDefinitions(ActorMetadata metadata, FactionTable factions)
    {
        foreach (var name in Names)
            Table(name);

        var named = new List<(string Where, string Name)>();
        foreach (var className in metadata.Actors.Keys)
        {
            if (metadata.TryGetProperty(className, PropertyName, out var value) && value?.ToString() is { Length: > 0 } name)
                named.Add((className, name.Trim()));
        }
        foreach (var faction in factions.Names)
        {
            if (factions.Translation(faction) is { } name)
                named.Add(($"faction {faction}", name));
        }
        foreach (var (where, name) in named.DistinctBy(n => n.Name.ToLowerInvariant()))
        {
            if (!Exists(name))
                Console.WriteLine($"{where} is drawn through translation \"{name}\", which isn't in translations.yaml");
        }
    }
}
