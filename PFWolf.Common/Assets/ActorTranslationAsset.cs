using PFWolf.Entities.Actors;
using PFWolf.Loaders;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Assets;

public record ActorTranslationAsset : Asset
{
    public ActorTranslationAsset()
    {
    }

    public ActorTranslationAsset(Dictionary<string, ActorData> actors)
    {
        Actors = actors;
    }

    public Dictionary<string, ActorData> Actors { get; set; } = [];

    /// <summary>
    /// Lays an actordefs file over the document of the classes loaded before it, as the loader
    /// combines them. A class replaces one of the same name whole (or, from a mod, merges into it
    /// key by key, see YamlTree.DeepMerge), unless it says `extend: true`: then it changes that
    /// one as ActorData.Combine does, its properties and states replacing those of the same name
    /// (or taking them out, tagged !remove), its flags added (or, written ~FLAG, taken away), and
    /// its other keys, parent and radius, replacing those; properties, states or flags tagged
    /// !replace replace the class's whole. A class tagged !remove is taken out, and one tagged
    /// !replace replaces the class whole even from a mod.
    /// </summary>
    public static void MergeYaml(YamlMappingNode target, YamlMappingNode overlay, bool deep)
    {
        foreach (var (name, value) in overlay.Children)
        {
            var existingName = YamlTree.FindKey(target, name);
            if (existingName != null && target.Children[existingName] is YamlMappingNode existing
                && value is YamlMappingNode incoming && !YamlTree.IsReplace(incoming) && IsExtend(incoming))
            {
                ExtendYaml(existing, incoming);
                continue;
            }

            var entry = new YamlMappingNode { { name, value } };
            if (deep)
                YamlTree.DeepMerge(target, entry);
            else
                YamlTree.MergeLevels(target, entry, 1);
        }
    }

    // ActorData's own keys are matched as its properties are read: ignoring case
    private const StringComparison KeyCase = StringComparison.OrdinalIgnoreCase;

    private static bool IsExtend(YamlMappingNode actor)
        => YamlTree.FindKey(actor, new YamlScalarNode("extend"), KeyCase) is { } key
            && actor.Children[key] is YamlScalarNode { Value: var value } && bool.TryParse(value, out var extend) && extend;

    private static void ExtendYaml(YamlMappingNode existing, YamlMappingNode incoming)
    {
        foreach (var (key, value) in incoming.Children)
        {
            var keyName = (key as YamlScalarNode)?.Value ?? "";
            var existingKey = YamlTree.FindKey(existing, key, KeyCase);
            if (YamlTree.IsRemove(value))
            {
                if (existingKey != null)
                    existing.Children.Remove(existingKey);
                continue;
            }

            if (YamlTree.IsReplace(value))
            {
                // All of its properties, states or flags in place of the class's
                YamlTree.Set(existing, existingKey ?? key, YamlTree.WithoutMergeTags(value));
                continue;
            }

            switch (keyName.ToLowerInvariant())
            {
                case "extend":
                    // Extended or not stays as the class it changes was
                    continue;

                case "properties" or "states" when existingKey != null
                    && existing.Children[existingKey] is YamlMappingNode existingEntries && value is YamlMappingNode incomingEntries:
                    YamlTree.MergeLevels(existingEntries, incomingEntries, 1);
                    continue;

                case "flags" when value is YamlSequenceNode incomingFlags:
                    var flags = (existingKey != null ? existing.Children[existingKey] as YamlSequenceNode : null)?
                        .Children.OfType<YamlScalarNode>().Select(flag => flag.Value!).ToList() ?? [];
                    foreach (var flag in incomingFlags.Children.OfType<YamlScalarNode>().Select(flag => flag.Value!))
                    {
                        if (flag.StartsWith('~'))
                            flags.RemoveAll(f => string.Equals(f, flag[1..], StringComparison.OrdinalIgnoreCase));
                        else if (!flags.Contains(flag))
                            flags.Add(flag);
                    }
                    YamlTree.Set(existing, existingKey ?? key,
                        new YamlSequenceNode(flags.Select(flag => new YamlScalarNode(flag))) { Style = incomingFlags.Style });
                    continue;

                default:
                    YamlTree.Set(existing, existingKey ?? key, YamlTree.WithoutMergeTags(value));
                    continue;
            }
        }
    }
}

public class ActorData
{
    public Dictionary<string, List<StateData>> States { get; internal set; } = [];
    public int Radius { get; internal set; }
    public HashSet<string> Flags { get; internal set; } = [];
    public string Parent { get; internal set; }
    public Dictionary<string, object> Properties { get; internal set; } = [];

    /// <summary>
    /// `extend: true`: rather than replacing a class of the same name loaded before it (a base
    /// pack's, say), it changes that one: its properties and states replace those of the same
    /// name, its flags are added, and its parent and radius, when given, replace those. The
    /// loader applies it to the YAML (ActorTranslationAsset.MergeYaml), where !remove, !replace
    /// and ~FLAG can also take parts away.
    /// </summary>
    public bool Extend { get; internal set; }

    /// <summary>
    /// What a class loaded over <paramref name="existing"/> makes of it (see <see cref="Extend"/>),
    /// where classes from different assets meet: a pack's actordefs over the shared ones
    /// (ActorMetadata.AddActors). Within one asset the loader merges them as YAML (MergeYaml).
    /// </summary>
    public static ActorData Combine(ActorData? existing, ActorData incoming)
    {
        if (!incoming.Extend || existing == null)
            return incoming;

        var combined = new ActorData
        {
            States = new(existing.States),
            Radius = incoming.Radius != 0 ? incoming.Radius : existing.Radius,
            Flags = [.. existing.Flags, .. incoming.Flags],
            Parent = string.IsNullOrEmpty(incoming.Parent) ? existing.Parent : incoming.Parent,
            Properties = new(existing.Properties),
            Extend = existing.Extend,
        };
        foreach (var (state, frames) in incoming.States)
            combined.States[state] = frames;
        foreach (var (key, value) in incoming.Properties)
            combined.Properties[key] = value;
        return combined;
    }
}

public abstract class StateData
{
}

public class ActorStatesData : StateData
{
    public string Sprite { get; internal set; }
    public List<string> Frames { get; internal set; } = [];
    public float TicsPerFrame { get; internal set; }
    public List<string> Modifiers { get; internal set; } = [];
    public StateLight? Light { get; internal set; }
    public string Action { get; internal set; }
    public string Think { get; internal set; }
    public string? NextState { get; internal set; }
}

public class StopStateData : StateData
{
}
public class LoopStateData : StateData
{
}


public class GoToStateData : StateData
{
    public string NextState { get; set; }
    public GoToStateData(string nextState)
    {
        NextState = nextState;
    }
}
