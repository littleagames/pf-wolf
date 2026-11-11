using PFWolf.Entities.Actors;

namespace PFWolf.Assets;

/// <summary>
/// The actordefs classes, merged from the pk3s, with what it takes to build live actors from them
/// </summary>
internal class ActorMetadata
{
    public Dictionary<string, ActorData> Actors { get; internal set; } = [];

    private readonly Dictionary<string, Dictionary<string, ActorStateFrame>> _resolvedStatesCache = [];

    internal void AddActors(Dictionary<string, ActorData> dictionary)
    {
        foreach (var (key, value) in dictionary)
        {
            Actors[key] = ActorData.Combine(Actors.GetValueOrDefault(key), value);
        }
    }

    internal Entities.Actors.Actor CreateActor(string name, ActorData actor)
    {
        // A class's own properties and flags, the first time it's made (before its parents' are
        // merged in), to be checked against the kind of actor it turns out to be
        List<string>? ownSettings = _settingsChecked.Add(name) ? [.. actor.Properties.Keys, .. actor.Flags] : null;

        // The kind of actor it is: the C# class of its name, else the nearest parent's that has one
        var actorType = Type.GetType($"PFWolf.Entities.Actors.{name}");
        var ancestors = ActorInheritance.Ancestors(name, actor, Actors);
        actorType ??= ancestors.Select(ancestor => Type.GetType($"PFWolf.Entities.Actors.{ancestor.Name}"))
            .FirstOrDefault(type => type != null && typeof(Entities.Actors.Actor).IsAssignableFrom(type));
        ActorInheritance.Inherit(actor, [.. ancestors.Select(ancestor => ancestor.Data)]);

        // Inventory itself is abstract: a class made straight from it is an item all the same, a
        // CustomInventory (else it would be a plain actor, never picked up)
        if (actorType == typeof(Entities.Actors.Inventory))
            actorType = typeof(Entities.Actors.CustomInventory);

        // TODO: Eventually they will move into the pk3 scripts folder
        if (actorType == null || actorType.IsAbstract || !typeof(Entities.Actors.Actor).IsAssignableFrom(actorType))
        {
            actorType = typeof(Entities.Actors.Actor); // Default to base Actor if type not found
        }

        var actorInstance = (Entities.Actors.Actor)Activator.CreateInstance(actorType)!;
        actorInstance.Name = name;
        actorInstance.Properties = actor.Properties;
        actorInstance.Radius = actor.Radius;
        actorInstance.Flags = actor.Flags;
        actorInstance.States = actor.States;

        var resolvedStates = GetResolvedStates(name, actor);
        actorInstance.ResolvedStates = resolvedStates;
        resolvedStates.TryGetValue("Spawn", out var spawnState);
        // Mirrors legacy NewState (WL_STATE.C): entering a state arms TicCount from
        // its TicTime, so DoActor's tic countdown starts correctly. Nothing runs yet: a 0-tic
        // Spawn frame ends on the actor's first tic.
        if (spawnState != null)
            actorInstance.ArmState(spawnState);

        if (ownSettings != null)
            foreach (var (setting, needs) in MisplacedSettings(ownSettings, actorInstance))
                WarningLog.Write(MisplacedMessage(name, setting, needs));

        return actorInstance;
    }

    // The classes whose own properties and flags are checked
    private readonly HashSet<string> _settingsChecked = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The properties and flags among <paramref name="settings"/> that only another kind of actor
    /// reads (a BlakeMonster's monster.attackrange, SMART), each with the class it needs: on
    /// <paramref name="actor"/> they do nothing. A ~FLAG, taking one away, is never misplaced.
    /// </summary>
    internal static IEnumerable<(string Setting, string Needs)> MisplacedSettings(IEnumerable<string> settings, Entities.Actors.Actor actor) =>
        from setting in settings
        where !setting.StartsWith('~')
        let needs = Definitions.ActorProperties.Find(setting)?.Needs ?? Definitions.ActorFlags.Find(setting)?.Needs
        where needs != null && !(Type.GetType($"PFWolf.Entities.Actors.{needs}") is { } type && type.IsInstanceOfType(actor))
        select (setting, needs);

    /// <summary>The warning for a property or flag that does nothing on its class</summary>
    internal static string MisplacedMessage(string className, string setting, string needs) =>
        $"Actor {className}: {setting} is only for a {needs} (parent: {needs}), so it does nothing here";

    /// <summary>
    /// Looks a property up on the named actor, falling back through its parent chain, without
    /// building an actor instance -- for callers (InventoryManager) that only need class-level
    /// data such as `inventory.maxamount`.
    /// </summary>
    internal bool TryGetProperty(string actorName, string key, out object value)
    {
        // Bounded depth instead of a visited set: this runs per item per frame from the
        // inventory code, and a parent cycle would otherwise loop forever.
        var current = actorName;
        for (var depth = 0; depth < 32 && !string.IsNullOrWhiteSpace(current) && Actors.TryGetValue(current, out var data); depth++)
        {
            if (data.Properties.TryGetValue(key, out value!))
                return true;
            current = data.Parent;
        }

        value = null!;
        return false;
    }

    internal int GetIntProperty(string actorName, string key, int fallback) =>
        TryGetProperty(actorName, key, out var value) ? Convert.ToInt32(value) : fallback;

    private Dictionary<string, ActorStateFrame> GetResolvedStates(string name, ActorData actor)
    {
        if (_resolvedStatesCache.TryGetValue(name, out var cached))
            return cached;

        var resolved = ActorStateResolver.Resolve(actor.States, (state, entries, marker) =>
        {
            if (_warnedStates.Add(entries))
                WarningLog.Write(ActorStateResolver.IgnoredMessage(name, state, marker));
        });
        foreach (var (state, entries) in actor.States)
            if (_modifiersChecked.Add(entries))
                foreach (var modifier in ActorStateResolver.UnknownModifiers(entries))
                    WarningLog.Write(ActorStateResolver.UnknownModifierMessage(name, state, modifier));
        _resolvedStatesCache[name] = resolved;
        return resolved;
    }

    // The state lists warned about, and those whose modifiers are checked: a class shares its
    // parent's, so a mistake in one is told once
    private readonly HashSet<List<StateData>> _warnedStates = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<List<StateData>> _modifiersChecked = new(ReferenceEqualityComparer.Instance);

}
