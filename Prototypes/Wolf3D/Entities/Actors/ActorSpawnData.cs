namespace Wolf3D.Entities.Actors;

internal class ActorSpawnData
{
    //public ActorSpawnData(string actor, int angles, int patrol, int minSkill)
    //{
    //    Actor = actor;
    //    Angles = angles;
    //    Patrol = patrol;
    //    MinSkill = minSkill;
    //}

    public string Class { get; set; }
    public int Angles { get; set; }
    public int Patrol { get; set; }
    public int MinSkill { get; set; }
}
