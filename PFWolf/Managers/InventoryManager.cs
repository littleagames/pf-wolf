using PFWolf.Assets;

namespace PFWolf.Managers;

/// <summary>
/// The player's carried items (keys, ammo, weapons), keyed by inventory item type. Class-level
/// rules come from the actordefs `inventory.*` properties: `maxamount` caps a stack,
/// `interhubamount` is how much survives a level change, and `type` folds variants together
/// (ClipBox and BlueClip are both "Clip"). Health, score and lives are plain stats, not items.
/// </summary>
internal class InventoryManager
{
    private readonly Lazy<AssetManager> _assetManager;
    // The acting player's items (Program.ActAs points this at theirs)
    private Dictionary<string, int> _items = new(StringComparer.OrdinalIgnoreCase);
    // Items the whole team holds together (co-op keys): see ShareKeys
    private readonly Dictionary<string, int> _shared = new(StringComparer.OrdinalIgnoreCase);
    private ActorMetadata? _metadata;
    public InventoryManager(Lazy<AssetManager> assetManager)
    {
        _assetManager = assetManager;
    }

    /// <summary>Items ruled by these actordefs rather than the asset manager's (for tests)</summary>
    internal InventoryManager(ActorMetadata metadata)
        : this(new Lazy<AssetManager>(() => throw new InvalidOperationException("This inventory has no asset manager")))
    {
        _metadata = metadata;
    }

    // AssetManager.GetActorMetadata builds a fresh ActorMetadata every call, so cache it.
    private ActorMetadata Metadata => _metadata ??= _assetManager.Value.GetActorMetadata();

    /// <summary>The acting player's own held item counts, keyed by item type (shared keys aren't in it).</summary>
    public IReadOnlyDictionary<string, int> Items => _items;

    /// <summary>Makes the methods below read and change these items: the acting player's (Program.ActAs)</summary>
    public void SetHolder(Dictionary<string, int> items) => _items = items;

    /// <summary>
    /// Co-op: keys (Key classes) are the team's, not one player's: whoever picks one up opens
    /// its doors for everyone. Off in single player, so a lone player's keys are their own.
    /// </summary>
    public bool ShareKeys { get; set; }

    /// <summary>The team's shared items (keys, in co-op)</summary>
    public IReadOnlyDictionary<string, int> SharedItems => _shared;

    // Where an item type is kept: the team's bag for a shared key, else the acting player's
    private Dictionary<string, int> BagFor(string type) =>
        ShareKeys && DerivesFrom(type, "Key") ? _shared : _items;

    /// <summary>
    /// The item type a class is stored under: its `inventory.type` (inherited from parents),
    /// or the class itself. Every method below accepts any class name and resolves it first.
    /// </summary>
    public string GetItemType(string item) =>
        Metadata.TryGetProperty(item, "inventory.type", out var type) ? type.ToString() ?? item : item;

    public int GetIntProperty(string item, string key, int fallback) =>
        Metadata.GetIntProperty(item, key, fallback);

    public string? GetStringProperty(string item, string key) =>
        GetProperty(item, key)?.ToString();

    /// <summary>A property as parsed from YAML (a string, a list or a mapping), or null.</summary>
    public object? GetProperty(string item, string key) =>
        Metadata.TryGetProperty(item, key, out var value) ? value : null;

    /// <summary>
    /// A fresh actor of an item class, with its inherited properties and states (e.g. the
    /// weapon in hand, run through its Ready/Fire states). Null for an unknown class.
    /// </summary>
    public Entities.Actors.Actor? CreateActor(string item)
    {
        var match = Metadata.Actors.Keys.FirstOrDefault(k => string.Equals(k, item, StringComparison.OrdinalIgnoreCase));
        return match != null ? Metadata.CreateActor(match, Metadata.Actors[match]) : null;
    }

    /// <summary>The actordefs class names that descend from <paramref name="baseClass"/> (e.g. "Weapon").</summary>
    public IEnumerable<string> GetClassesDerivedFrom(string baseClass) =>
        Metadata.Actors.Keys.Where(name => name != baseClass && DerivesFrom(name, baseClass));

    /// <summary>
    /// Looks up an actordefs class by name, ignoring case, and returns its exact spelling if it
    /// descends from one of <paramref name="baseClasses"/>; otherwise null.
    /// </summary>
    public string? FindClass(string name, params string[] baseClasses)
    {
        var match = Metadata.Actors.Keys.FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
        return match != null && baseClasses.Any(b => DerivesFrom(match, b)) ? match : null;
    }

    private bool DerivesFrom(string name, string baseClass)
    {
        // Bounded depth for the same reason as ActorMetadata.TryGetProperty: a parent cycle.
        var current = name;
        for (var depth = 0; depth < 32 && !string.IsNullOrWhiteSpace(current); depth++)
        {
            if (current == baseClass)
                return true;
            current = Metadata.Actors.TryGetValue(current, out var data) ? data.Parent : null;
        }
        return false;
    }

    public int GetCount(string item)
    {
        var type = GetItemType(item);
        return BagFor(type).GetValueOrDefault(type);
    }

    public bool Has(string item) => GetCount(item) > 0;

    /// <summary>The player's actordefs class (set by Program), whose `player.maxamount` overrides items' own caps</summary>
    public Func<string?> PlayerClass { get; set; } = () => null;

    /// <summary>
    /// `inventory.maxamount` for the item's type, unless the player class's `player.maxamount`
    /// (item: amount) names that type. Zero or less means "uncapped", matching how the
    /// actordefs mark non-stacking base classes such as Health.
    /// </summary>
    public int GetMaxAmount(string item)
    {
        var type = GetItemType(item);
        var max = PlayerMaxAmount(type) ?? Metadata.GetIntProperty(type, "inventory.maxamount", 1);
        return max > 0 ? max : int.MaxValue;
    }

    // The player class's cap for an item type; its keys may name any class of that type (ClipBox for Clip)
    private int? PlayerMaxAmount(string type)
    {
        if (PlayerClass() is not { } playerClass || GetProperty(playerClass, "player.maxamount") is not IDictionary<object, object> caps)
            return null;
        foreach (var (item, amount) in caps)
        {
            if (string.Equals(GetItemType(item.ToString() ?? ""), type, StringComparison.OrdinalIgnoreCase))
                return Convert.ToInt32(amount);
        }
        return null;
    }

    public bool IsFull(string item) => GetCount(item) >= GetMaxAmount(item);

    /// <summary>Adds up to <paramref name="amount"/>, clamped to the max; returns how many were actually added.</summary>
    public int Give(string item, int amount)
    {
        var type = GetItemType(item);
        if (amount <= 0)
            return 0;

        var bag = BagFor(type);
        var current = bag.GetValueOrDefault(type);
        var added = Math.Min(amount, GetMaxAmount(type) - current);
        if (added <= 0)
            return 0;

        bag[type] = current + added;
        return added;
    }

    /// <summary>Removes up to <paramref name="amount"/>; returns how many were actually removed.</summary>
    public int Take(string item, int amount)
    {
        var type = GetItemType(item);
        var bag = BagFor(type);
        var current = bag.GetValueOrDefault(type);
        var removed = Math.Min(amount, current);
        if (removed <= 0)
            return 0;

        if (removed == current)
            bag.Remove(type);
        else
            bag[type] = current - removed;

        return removed;
    }

    /// <summary>Drops everything the acting player holds (the team's shared keys stay)</summary>
    public void Clear() => _items.Clear();

    /// <summary>Drops the team's shared keys (a new game)</summary>
    public void ClearShared() => _shared.Clear();

    /// <summary>Puts back the team's shared keys as <see cref="SharedItems"/> had them (a player joining mid-game)</summary>
    public void RestoreShared(IReadOnlyDictionary<string, int> items)
    {
        _shared.Clear();
        foreach (var (type, count) in items)
            _shared[type] = count;
    }

    /// <summary>Replaces everything held with saved counts, keyed by item type as <see cref="Items"/> is.</summary>
    public void Restore(IReadOnlyDictionary<string, int> items)
    {
        _items.Clear();
        foreach (var (type, count) in items)
        {
            if (count > 0)
                _items[type] = count;
        }
    }

    /// <summary>
    /// Level change: each item is trimmed to its `inventory.interhubamount` (keys are 0, so
    /// they're dropped; ammo keeps everything; the Inventory default of 1 keeps a weapon).
    /// </summary>
    public void ResetForNextLevel()
    {
        _shared.Clear();        // keys, which every level change drops (interhubamount 0)
        foreach (var item in _items.Keys.ToList())
        {
            var keep = Metadata.GetIntProperty(item, "inventory.interhubamount", 0);
            var excess = GetCount(item) - keep;
            if (excess > 0)
                Take(item, excess);
        }
    }
}
