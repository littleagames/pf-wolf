using System.Numerics;
using Wolf3D;
using Wolf3D.Assets;
using Wolf3D.Constants;

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

    // Sub-tile fixed-point world position and its containing tile, mirroring objstruct's
    // x/y and tilex/tiley (Program.WL_DEF.cs) -- kept as separate mutable fields because
    // legacy movement code (MoveObj/TryWalk) updates TileX/TileY the instant a move toward
    // a new tile begins, while X/Y trail behind and approach the new tile center gradually.
    public int X { get; internal set; }
    public int Y { get; internal set; }
    public byte TileX { get; internal set; }
    public byte TileY { get; internal set; }

    // Runtime combat/AI bookkeeping (FL_SHOOTABLE, FL_AMBUSH, FL_ATTACKMODE, etc. --
    // Program.WL_DEF.cs's objflags), distinct from the static, YAML-declared `Flags` above.
    public Program.objflags RuntimeFlags { get; internal set; }

    // Screen-space hit-testing data, recomputed every frame in Program.WL_DRAW.cs's
    // DrawScaleds (mirrors objstruct's viewx/transx, set there by TransformActor) so
    // Program.WL_AGENT.cs's GunAttack/KnifeAttack can find the closest shootable actor
    // under the crosshair regardless of which actor system it belongs to.
    public short ViewX { get; internal set; }
    public int TransX { get; internal set; } = int.MaxValue;
    public ushort ViewHeight { get; internal set; }

    internal void SetPosition(int tilex, int tiley)
    {
        Position = new Vector2(tilex, tiley);
        TileX = (byte)tilex;
        TileY = (byte)tiley;
        X = (int)((tilex << MapConstants.TILESHIFT) + MapConstants.TILEGLOBAL / 2);
        Y = (int)((tiley << MapConstants.TILESHIFT) + MapConstants.TILEGLOBAL / 2);
    }

    // Keeps the tile-snapped Position (used by the static-object render/visibility path)
    // in sync whenever AI movement code updates the fixed-point X/Y or TileX/TileY directly.
    internal void SyncPosition()
    {
        Position = new Vector2(TileX, TileY);
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
