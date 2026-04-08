namespace Wolf3D.Entities.Actors;

internal class ActorSpawnData
{
    public ActorSpawnData(string actor, int angles, int patrol, int minSkill)
    {
        Actor = actor;
        Angles = angles;
        Patrol = patrol;
        MinSkill = minSkill;
    }

    public string Actor { get; }
    public int Angles { get; }
    public int Patrol { get; }
    public int MinSkill { get; }
}
