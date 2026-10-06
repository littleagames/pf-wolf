using PFWolf.Entities.Actors;

namespace PFWolf.Entities;

/// <summary>
/// A mapdefs trigger or switch being set off: the tile it sits on, the direction it was used
/// from, who used it, and its tile's tag (0 for none), which says what its actions act on.
/// (Actors.Actor spelled out: a bare `Actor` in the Wolf3D namespaces is the legacy
/// Wolf3D.Actor wall/door base class.)
/// </summary>
internal record TriggerActivation(int TileX, int TileY, controldirs Dir, Actors.Actor Activator, ushort Tag);

/// <summary>
/// Dispatches the `action` call strings on mapdefs triggers and switches (e.g. `A_PushWall`) to
/// C# handlers, parsed the same way as actor actions. A handler returns whether the trigger
/// actually went off (a pushwall blocked on the far side doesn't), which is when a secret trigger
/// counts as found. A switch runs its actions whatever they return.
/// </summary>
internal static class MapTriggerRegistry
{
    internal delegate bool TriggerAction(TriggerActivation activation, string[] args);

    private static readonly Dictionary<string, TriggerAction> _actions = [];

    internal static void Register(string name, TriggerAction action) => _actions[name] = action;

    internal static bool Invoke(string? actionCall, TriggerActivation activation)
    {
        if (string.IsNullOrWhiteSpace(actionCall))
            return false;

        var (name, args) = ActorActionRegistry.Parse(actionCall);
        if (_actions.TryGetValue(name, out var action))
            return action(activation, args);

        Console.WriteLine($"No handler registered for trigger action '{name}'.");
        return false;
    }
}
