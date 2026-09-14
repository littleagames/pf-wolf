using System.Numerics;
using Wolf3D.Assets;

namespace Wolf3D.Entities.Actors;

internal record Actor : Thinker
{
    public Dictionary<string, List<ActorStatesData>> States { get; internal set; } = [];
    public int Radius { get; internal set; }
    public HashSet<string> Flags { get; internal set; } = [];
    public string? Parent { get; internal set; } = null;
    public Dictionary<string, object> Properties { get; internal set; } = [];
    public required string Name { get; internal set; }
    public Vector2 Position { get; private set; } = Vector2.Zero;

    internal void SetPosition(int tilex, int tiley)
    {
        Position = new Vector2(tilex, tiley);
    }
}
