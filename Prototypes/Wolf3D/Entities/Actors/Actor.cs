using System.Numerics;
using Wolf3D;
using Wolf3D.Assets;

namespace Wolf3D.Entities.Actors;

internal record Actor : Thinker
{
    public Dictionary<string, List<StateData>> States { get; internal set; } = [];
    public Dictionary<string, ActorStateFrame> ResolvedStates { get; internal set; } = [];
    public ActorStateFrame? CurrentState { get; internal set; }
    public int Radius { get; internal set; }
    public HashSet<string> Flags { get; internal set; } = [];
    public string? Parent { get; internal set; } = null;
    public Dictionary<string, object> Properties { get; internal set; } = [];
    public required string Name { get; internal set; }
    public Vector2 Position { get; private set; } = Vector2.Zero;

    // Mirrors objstruct's think/state-transition bookkeeping (Program.WL_DEF.cs) while
    // actors migrate from objlist2 to MapManager._actors. See DoActor in MapManager.
    public activetypes Active { get; internal set; } = activetypes.ac_no;
    public short TicCount { get; internal set; }
    public objdirtypes Dir { get; internal set; } = objdirtypes.nodir;
    public short Angle { get; internal set; }
    public short Hitpoints { get; internal set; }
    public int Speed { get; internal set; }
    public int Distance { get; internal set; }
    public short Temp1 { get; internal set; }
    public short Temp2 { get; internal set; }
    public bool Hidden { get; internal set; }
    public byte AreaNumber { get; internal set; }

    internal void SetPosition(int tilex, int tiley)
    {
        Position = new Vector2(tilex, tiley);
    }

    /// <summary>
    /// Walks a named state's resolved frame chain once, firing each frame's Action along the
    /// way -- for event-triggered states like "Pickup" that aren't ticked by DoActor, rather
    /// than looped/held like "Spawn". Stops as soon as a frame repeats (self-loop/terminal).
    /// </summary>
    internal void RunState(string stateName)
    {
        if (!ResolvedStates.TryGetValue(stateName, out var frame))
            return;

        var visited = new HashSet<ActorStateFrame>();
        while (frame != null && visited.Add(frame))
        {
            ActorActionRegistry.Invoke(frame.Action, this);
            frame = frame.Next;
        }
    }
}
