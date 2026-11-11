using PFWolf.Assets;

namespace PFWolf.Entities.Actors;

/// <summary>
/// How an actordefs class gets what its parents have: their flags, and their properties, states
/// and radius where it hasn't its own, the nearest parent's first. Shared by the game, which builds
/// actors this way (ActorMetadata.CreateActor), and the editor, which shows the classes as the game
/// has them.
/// </summary>
public static class ActorInheritance
{
    /// <summary>
    /// The class's parents, nearest first, as far as they go: a parent that isn't a class ends the
    /// line there, except the class's own, which is an error, as is a class that is its own ancestor
    /// </summary>
    /// <exception cref="Exception">Its parent isn't a class, or the line comes back round to a class in it</exception>
    public static List<(string Name, ActorData Data)> Ancestors(string name, ActorData actor, IReadOnlyDictionary<string, ActorData> actors)
    {
        var ancestors = new List<(string, ActorData)>();
        if (string.IsNullOrWhiteSpace(actor.Parent))
            return ancestors;
        if (!actors.TryGetValue(actor.Parent, out var parent))
            throw new Exception($"Parent actor {actor.Parent} not found");

        var visited = new HashSet<string> { name };
        for (var (currentName, current) = (actor.Parent, (ActorData?)parent); current != null;)
        {
            if (!visited.Add(currentName ?? ""))
                throw new Exception("Circular reference detected");
            ancestors.Add((currentName ?? "", current));

            if (string.IsNullOrWhiteSpace(current.Parent) || !actors.TryGetValue(current.Parent, out var next))
                (currentName, current) = ("", null);
            else
                (currentName, current) = (current.Parent, next);
        }
        return ancestors;
    }

    /// <summary>
    /// Gives <paramref name="actor"/> what its <paramref name="ancestors"/> (nearest first) have: each
    /// property, state and the radius it hasn't got, from the nearest that has it (Key's
    /// "inventory.interhubamount" beats Inventory's), and every flag. Changes the class's data, as
    /// the game always has.
    /// </summary>
    public static void Inherit(ActorData actor, IReadOnlyList<ActorData> ancestors)
    {
        foreach (var ancestor in ancestors)
        {
            if (actor.Radius == 0)
                actor.Radius = ancestor.Radius;      // the nearest that gives one

            foreach (var (key, value) in ancestor.Properties)
                actor.Properties.TryAdd(key, value);

            foreach (var (state, frames) in ancestor.States)
                actor.States.TryAdd(state, frames);
        }

        // Flags from the root down
        for (int i = ancestors.Count - 1; i >= 0; i--)
            actor.Flags.UnionWith(ancestors[i].Flags);
    }
}
