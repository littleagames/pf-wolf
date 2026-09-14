using Wolf3D;

namespace Wolf3D.Entities.Actors;

internal class ActorStateFrame
{
    public required string StateName { get; init; }
    public required string Sprite { get; init; }
    public required string FrameLetter { get; init; }
    public short TicTime { get; init; }
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
