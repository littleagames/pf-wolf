using Wolf3D.Assets;

namespace Wolf3D.Managers;

/// <summary>
/// The player's carried items (keys, ammo, weapons), keyed by inventory item type. Class-level
/// rules come from the actordefs `inventory.*` properties: `maxamount` caps a stack,
/// `interhubamount` is how much survives a level change, and `type` folds variants together
/// (ClipBox and BlueClip are both "Clip"). Health, score and lives are plain stats, not items.
/// </summary>
internal class InventoryManager
{
    private readonly Lazy<AssetManager> _assetManager;
    private readonly Dictionary<string, int> _items = new(StringComparer.OrdinalIgnoreCase);
    private ActorMetadata? _metadata;

    public InventoryManager(Lazy<AssetManager> assetManager)
    {
        _assetManager = assetManager;
    }

    // AssetManager.GetActorMetadata builds a fresh ActorMetadata every call, so cache it.
    private ActorMetadata Metadata => _metadata ??= _assetManager.Value.GetActorMetadata();

    /// <summary>Held item counts, keyed by item type.</summary>
    public IReadOnlyDictionary<string, int> Items => _items;

    /// <summary>
    /// The item type a class is stored under: its `inventory.type` (inherited from parents),
    /// or the class itself. Every method below accepts any class name and resolves it first.
    /// </summary>
    public string GetItemType(string item) =>
        Metadata.TryGetProperty(item, "inventory.type", out var type) ? type.ToString() ?? item : item;

    public int GetIntProperty(string item, string key, int fallback) =>
        Metadata.GetIntProperty(item, key, fallback);

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

    public int GetCount(string item) => _items.GetValueOrDefault(GetItemType(item));

    public bool Has(string item) => GetCount(item) > 0;

    /// <summary>
    /// `inventory.maxamount` for the item's type. Zero or less means "uncapped", matching how
    /// the actordefs mark non-stacking base classes such as Health.
    /// </summary>
    public int GetMaxAmount(string item)
    {
        var max = Metadata.GetIntProperty(GetItemType(item), "inventory.maxamount", 1);
        return max > 0 ? max : int.MaxValue;
    }

    public bool IsFull(string item) => GetCount(item) >= GetMaxAmount(item);

    /// <summary>Adds up to <paramref name="amount"/>, clamped to the max; returns how many were actually added.</summary>
    public int Give(string item, int amount)
    {
        var type = GetItemType(item);
        if (amount <= 0)
            return 0;

        var current = _items.GetValueOrDefault(type);
        var added = Math.Min(amount, GetMaxAmount(type) - current);
        if (added <= 0)
            return 0;

        _items[type] = current + added;
        return added;
    }

    /// <summary>Removes up to <paramref name="amount"/>; returns how many were actually removed.</summary>
    public int Take(string item, int amount)
    {
        var type = GetItemType(item);
        var current = _items.GetValueOrDefault(type);
        var removed = Math.Min(amount, current);
        if (removed <= 0)
            return 0;

        if (removed == current)
            _items.Remove(type);
        else
            _items[type] = current - removed;

        return removed;
    }

    public void Clear() => _items.Clear();

    /// <summary>
    /// Level change: each item is trimmed to its `inventory.interhubamount` (keys are 0, so
    /// they're dropped; ammo keeps everything; the Inventory default of 1 keeps a weapon).
    /// </summary>
    public void ResetForNextLevel()
    {
        foreach (var item in _items.Keys.ToList())
        {
            var keep = Metadata.GetIntProperty(item, "inventory.interhubamount", 0);
            var excess = GetCount(item) - keep;
            if (excess > 0)
                Take(item, excess);
        }
    }
}
