namespace Wolf3D.Entities.Actors;

internal abstract class StateData
{
}

internal class ActorStatesData : StateData
{
    public string Sprite { get; internal set; }
    public List<string> Frames { get; internal set; }
    public float TicsPerFrame { get; internal set; }
    public List<string> Modifiers { get; internal set; }
    public string Action { get; internal set; }
    public string Think { get; internal set; }
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