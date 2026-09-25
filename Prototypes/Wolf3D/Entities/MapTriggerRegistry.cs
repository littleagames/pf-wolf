using Wolf3D.Entities.Actors;

namespace Wolf3D.Entities;

/// <summary>
/// A mapdefs trigger being set off: the tile it sits on, the direction it was used from and who
/// used it. (Actors.Actor spelled out: a bare `Actor` in the Wolf3D namespaces is the legacy
/// Wolf3D.Actor wall/door base class.)
/// </summary>
internal record TriggerActivation(int TileX, int TileY, controldirs Dir, Actors.Actor Activator);

/// <summary>
/// Dispatches the `action` call strings on mapdefs triggers (e.g. `A_PushWall`) to C# handlers,
/// parsed the same way as actor actions. A handler returns whether the trigger actually went off
/// (a pushwall blocked on the far side doesn't), which is when a secret trigger counts as found.
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
