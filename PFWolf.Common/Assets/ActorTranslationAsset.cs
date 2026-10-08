using PFWolf.Entities.Actors;

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

    public override void Merge(Asset other)
    {
        if (other is ActorTranslationAsset otherAsset)
        {
            Merge(otherAsset);
        }
    }

    public void Merge(ActorTranslationAsset other)
    {
        foreach (var item in other.Actors)
        {
            this.Actors[item.Key] = ActorData.Combine(this.Actors.GetValueOrDefault(item.Key), item.Value);
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
    /// name, its flags are added, and its parent and radius, when given, replace those.
    /// </summary>
    public bool Extend { get; internal set; }

    /// <summary>What a class loaded over <paramref name="existing"/> makes of it (see <see cref="Extend"/>).</summary>
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
