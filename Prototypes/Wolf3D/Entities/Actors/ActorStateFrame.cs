using Wolf3D;

namespace Wolf3D.Entities.Actors;

internal class ActorStateFrame
{
    public required string StateName { get; init; }
    public required string Sprite { get; init; }
    public required string FrameLetter { get; init; }
    // ZDoom-style: -1 holds the frame forever, 0 ends it the moment it's entered (its Action
    // runs and Next follows in the same tic), anything else counts down that many 70Hz tics.
    public short TicTime { get; init; }
    public bool HoldsForever => TicTime < 0;
    public List<string> Modifiers { get; init; } = [];
    public string? Think { get; init; }
    public string? Action { get; init; }
    public ActorStateFrame? Next { get; internal set; }

    public string GetShapeName(objdirtypes dir)
    {
        var frame = dir == objdirtypes.nodir ? 0 : (int)dir;
        return $"{Sprite}{FrameLetter}{frame}";
    }
}
