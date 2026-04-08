namespace Wolf3D.Entities.Actors;

internal class ActorData
{
    public int Id { get; internal set; }
    public Dictionary<string, List<StateData>> States { get; internal set; } = [];
    public int Radius { get; internal set; }
    public List<string> Flags { get; internal set; } = [];
    public string Parent { get; internal set; }
    public Dictionary<string, object> Properties { get; internal set; } = [];
}
