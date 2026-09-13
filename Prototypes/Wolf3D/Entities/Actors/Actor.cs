using Wolf3D.Assets;

namespace Wolf3D.Entities.Actors;

internal record Actor : Thinker
{
    public Dictionary<string, List<ActorStatesData>> States { get; internal set; } = [];
    public int Radius { get; internal set; }
    public HashSet<string> Flags { get; internal set; } = [];
    public string? Parent { get; internal set; } = null;
    public Dictionary<string, object> Properties { get; internal set; } = [];
}
