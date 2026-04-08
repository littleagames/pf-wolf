namespace Wolf3D.Entities.Actors;

internal class ActorMetadata
{
    public Dictionary<string, ActorData> Actors { get; internal set; } = [];

    internal void AddActors(Dictionary<string, ActorData> dictionary)
    {
        foreach (var (key, value) in dictionary)
        {
            Actors[key] = value;
        }
    }
}
