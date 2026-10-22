namespace PFWolf.Assets;

/// <summary>How one faction treats another's members</summary>
public enum FactionRelation
{
    /// <summary>Goes after them on sight, and they're who its shots and projectiles are for</summary>
    Hostile,

    /// <summary>Leaves them be, unless they hurt it</summary>
    Neutral,

    /// <summary>On their side: never goes after them, and its shots pass them by</summary>
    Ally,
}

/// <summary>
/// A game pack's factions (gamepacks/{pack}/factions.yaml, a mod's factions.yaml): the sides
/// actors are on and how each treats the others. An actor's side is its actordefs `faction`
/// property (or a mapdefs thing's `faction:`), else player-faction for a player and
/// monster-faction for an enemy. A pack built on a base-pack starts from its file.
/// </summary>
public record FactionsAsset : Asset
{
    /// <summary>A player's faction when their class has no `faction` (players when left out)</summary>
    public string? PlayerFaction { get; set; }

    /// <summary>An enemy's (Monster's) faction when its class has no `faction` (monsters when left out)</summary>
    public string? MonsterFaction { get; set; }

    /// <summary>How a faction treats another it says nothing about: hostile, neutral or ally (hostile when left out)</summary>
    public string? DefaultRelation { get; set; }

    /// <summary>The factions, by name</summary>
    public Dictionary<string, FactionDefinition> Factions { get; set; } = [];
}

/// <summary>One faction in factions.yaml</summary>
public class FactionDefinition
{
    /// <summary>Its name as shown (a $NAME language key or the text); its key when left out</summary>
    public string? Name { get; set; }

    /// <summary>
    /// How it treats a faction its relations don't name: hostile, neutral or ally (the file's
    /// default-relation when left out). Its own members are always allies.
    /// </summary>
    public string? Default { get; set; }

    /// <summary>
    /// How it treats other factions, by name: hostile, neutral or ally. One way only: for two
    /// factions to be allies, both say so.
    /// </summary>
    public Dictionary<string, string> Relations { get; set; } = [];

    /// <summary>
    /// The sprite color translation (translations.yaml) its enemies (not players, not their
    /// projectiles) are drawn with when nothing of their own gives one: a mapdefs thing's
    /// `translation:` or their class's `translation` property
    /// </summary>
    public string? Translation { get; set; }

    /// <summary>
    /// In place of <see cref="Translation"/>, the one for a class (or a class descended from it,
    /// the nearest named winning): one recolor rarely suits every sprite, since each wears its
    /// own colors (Wolf3D's guards brown, its SS blue)
    /// </summary>
    public Dictionary<string, string> ClassTranslations { get; set; } = [];
}

/// <summary>
/// The factions as the game works them out (from a <see cref="FactionsAsset"/>, or none at all):
/// who's on what side, and how each side treats the others. Faction names ignore case.
/// </summary>
public class FactionTable
{
    public const string DefaultPlayerFaction = "players";
    public const string DefaultMonsterFaction = "monsters";

    private readonly Dictionary<string, FactionDefinition> _factions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, FactionRelation>> _relations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FactionRelation> _defaults = new(StringComparer.OrdinalIgnoreCase);
    private readonly FactionRelation _defaultRelation;

    public FactionTable(FactionsAsset? asset = null)
    {
        asset ??= new FactionsAsset();
        PlayerFaction = string.IsNullOrWhiteSpace(asset.PlayerFaction) ? DefaultPlayerFaction : asset.PlayerFaction.Trim();
        MonsterFaction = string.IsNullOrWhiteSpace(asset.MonsterFaction) ? DefaultMonsterFaction : asset.MonsterFaction.Trim();
        _defaultRelation = ParseRelation(asset.DefaultRelation, "default-relation") ?? FactionRelation.Hostile;

        foreach (var (name, faction) in asset.Factions ?? [])
        {
            var definition = faction ?? new FactionDefinition();
            _factions[name] = definition;
            if (ParseRelation(definition.Default, $"{name}'s default") is { } defaultRelation)
                _defaults[name] = defaultRelation;

            var relations = new Dictionary<string, FactionRelation>(StringComparer.OrdinalIgnoreCase);
            foreach (var (other, relation) in definition.Relations ?? [])
            {
                if (ParseRelation(relation, $"{name}'s relation to {other}") is { } parsed)
                    relations[other] = parsed;
                if (!asset.Factions!.Keys.Contains(other, StringComparer.OrdinalIgnoreCase))
                    Warnings.Add($"factions.yaml: {name}'s relations name \"{other}\", which isn't a faction");
            }
            _relations[name] = relations;
        }

        if (_factions.Count > 0)
        {
            foreach (var (what, faction) in new[] { ("player-faction", PlayerFaction), ("monster-faction", MonsterFaction) })
            {
                if (!_factions.ContainsKey(faction))
                    Warnings.Add($"factions.yaml: {what} \"{faction}\" isn't a faction");
            }
        }
    }

    /// <summary>A player's faction when their class doesn't give one</summary>
    public string PlayerFaction { get; }

    /// <summary>An enemy's faction when its class doesn't give one</summary>
    public string MonsterFaction { get; }

    /// <summary>The factions factions.yaml lists (none without one)</summary>
    public IReadOnlyCollection<string> Names => _factions.Keys;

    /// <summary>What's wrong with the file: relations that aren't hostile, neutral or ally, factions that aren't there</summary>
    public List<string> Warnings { get; } = [];

    /// <summary>
    /// Whether a faction is one the game knows: one factions.yaml lists, or, without any listed,
    /// any at all (every name then is a side of its own)
    /// </summary>
    public bool IsKnown(string faction) => _factions.Count == 0 || _factions.ContainsKey(faction);

    /// <summary>A faction's name as shown: its `name`, else the faction itself</summary>
    public string DisplayName(string faction) =>
        _factions.TryGetValue(faction, out var definition) && !string.IsNullOrWhiteSpace(definition.Name) ? definition.Name : faction;

    /// <summary>
    /// The translation a faction's enemies are drawn with, or null: for a class with an ancestry
    /// (itself first, then its parent, and so on), the first its class-translations names, else
    /// its translation
    /// </summary>
    public string? Translation(string faction, IEnumerable<string>? classAncestry = null)
    {
        if (!_factions.TryGetValue(faction, out var definition))
            return null;
        if (definition.ClassTranslations is { Count: > 0 } byClass)
        {
            foreach (var className in classAncestry ?? [])
            {
                var match = byClass.FirstOrDefault(entry => entry.Key.Equals(className, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(match.Value))
                    return match.Value;
            }
        }
        return string.IsNullOrWhiteSpace(definition.Translation) ? null : definition.Translation;
    }

    /// <summary>
    /// How a member of <paramref name="from"/> treats one of <paramref name="to"/>: its own side
    /// are allies; then its relations, its default, and the file's default-relation. No faction
    /// (null or empty) on either side is no side at all, which no one treats as anything but neutral.
    /// </summary>
    public FactionRelation Relation(string? from, string? to)
    {
        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
            return FactionRelation.Neutral;
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            return FactionRelation.Ally;
        if (_relations.TryGetValue(from, out var relations) && relations.TryGetValue(to, out var relation))
            return relation;
        return _defaults.TryGetValue(from, out var defaultRelation) ? defaultRelation : _defaultRelation;
    }

    public bool IsHostile(string? from, string? to) => Relation(from, to) == FactionRelation.Hostile;

    public bool IsAlly(string? from, string? to) => Relation(from, to) == FactionRelation.Ally;

    private FactionRelation? ParseRelation(string? value, string what)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (Enum.TryParse<FactionRelation>(value.Trim(), ignoreCase: true, out var relation) && Enum.IsDefined(relation))
            return relation;
        Warnings.Add($"factions.yaml: {what} is \"{value}\"; it can be hostile, neutral or ally");
        return null;
    }
}
