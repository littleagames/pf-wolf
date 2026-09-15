using System.Xml.Linq;
using Wolf3D.Entities.Actors;

namespace Wolf3D.Assets;

internal record ActorTranslationAsset : Asset
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
            this.Actors[item.Key] = item.Value;
        }
    }
}

internal class ActorData
{
    public Dictionary<string, List<StateData>> States { get; internal set; } = [];
    public int Radius { get; internal set; }
    public HashSet<string> Flags { get; internal set; } = [];
    public string Parent { get; internal set; }
    public Dictionary<string, object> Properties { get; internal set; } = [];
}

internal class ActorMetadata
{
    public Dictionary<string, ActorData> Actors { get; internal set; } = [];

    private readonly Dictionary<string, Dictionary<string, ActorStateFrame>> _resolvedStatesCache = [];

    internal void AddActors(Dictionary<string, ActorData> dictionary)
    {
        foreach (var (key, value) in dictionary)
        {
            Actors[key] = value;
        }
    }

    internal Entities.Actors.Actor CreateActor(string name, ActorData actor)
    {
        var actorType = Type.GetType($"Entities.Actors.{name}");
        if (!string.IsNullOrWhiteSpace(actor.Parent))
        {
            if (!Actors.TryGetValue(actor.Parent, out var parentActor))
            {
                throw new Exception($"Parent actor {actor.Parent} not found");
            }

            var visitedParents = new HashSet<string> { name };
            var ancestorChain = new Stack<ActorData>();
            ancestorChain.Push(actor);

            var currentParent = parentActor;
            var currentParentName = actor.Parent;

            while (currentParent != null)
            {
                if (!visitedParents.Add(currentParentName ?? ""))
                {
                    throw new Exception("Circular reference detected");
                }

                if (actorType == null)
                {
                    var candidateType = Type.GetType($"Wolf3D.Entities.Actors.{currentParentName}");
                    if (candidateType != null && typeof(Entities.Actors.Actor).IsAssignableFrom(candidateType))
                    {
                        actorType = candidateType;
                    }
                }

                ancestorChain.Push(currentParent);

                if (string.IsNullOrWhiteSpace(currentParent.Parent) ||
                    !Actors.TryGetValue(currentParent.Parent, out var nextParent))
                {
                    currentParentName = "";
                    currentParent = null;
                }
                else
                {
                    currentParentName = currentParent.Parent;
                    currentParent = nextParent;
                }
            }

            while (ancestorChain.Count > 0)
            {
                var ancestor = ancestorChain.Pop();
                MergeActorProperties(ancestor, actor);
            }
        }

        // TODO: Eventually they will move into the pk3 scripts folder
        if (actorType == null || !typeof(Entities.Actors.Actor).IsAssignableFrom(actorType))
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
        actorInstance.CurrentState = spawnState;
        // Mirrors legacy NewState (Program.WL_STATE.cs): entering a state arms TicCount from
        // its TicTime, so DoActor's tic countdown starts correctly instead of sitting at 0
        // (which means "hold forever" -- only correct for a genuinely single-frame Spawn).
        actorInstance.TicCount = spawnState?.TicTime ?? 0;

        return actorInstance;
    }

    private Dictionary<string, ActorStateFrame> GetResolvedStates(string name, ActorData actor)
    {
        if (_resolvedStatesCache.TryGetValue(name, out var cached))
            return cached;

        var resolved = ActorStateResolver.Resolve(actor.States);
        _resolvedStatesCache[name] = resolved;
        return resolved;
    }

    private static void MergeActorProperties(ActorData source, ActorData target)
    {
        if (target.Radius != 0)
            target.Radius = source.Radius; // TODO: this overwrites the target

        // Merge dictionaries and lists
        foreach (var kvp in source.Properties)
        {
            if (!target.Properties.ContainsKey(kvp.Key))
                target.Properties[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in source.States)
        {
            if (!target.States.ContainsKey(kvp.Key))
                target.States[kvp.Key] = kvp.Value;
        }
        
        target.Flags.UnionWith(source.Flags);
    }
}

internal abstract class StateData
{
}

internal class ActorStatesData : StateData
{
    public string Sprite { get; internal set; }
    public List<string> Frames { get; internal set; } = [];
    public float TicsPerFrame { get; internal set; }
    public List<string> Modifiers { get; internal set; } = [];
    public string Action { get; internal set; }
    public string Think { get; internal set; }
    public string? NextState { get; internal set; }

    public string GetFrame(objdirtypes dir)
    {
        var frame = 0;
        if (dir == objdirtypes.nodir)
            frame = 0;
        else
            frame = (int)dir;

        return $"{Sprite}{Frames.First()}{frame}"; // e.g. DRUMA0
    }
}

internal class StopStateData : StateData
{
}
internal class LoopStateData : StateData
{
}


internal class GoToStateData : StateData
{
    public string NextState { get; set; }
    public GoToStateData(string nextState)
    {
        NextState = nextState;
    }
}