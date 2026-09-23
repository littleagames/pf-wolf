namespace Wolf3D.Entities.Actors;

/// <summary>
/// An actor's runtime state as it is written to a save game. Class-level data (states, flags,
/// properties, radius) isn't stored: it is rebuilt from actordefs by class name on load. The
/// current frame is stored as its state group plus the frame's position within that group,
/// since frames are resolved objects with no identity of their own across a reload.
/// </summary>
internal sealed record ActorSnapshot
{
    public required string ClassName { get; init; }
    public bool IsPlayer { get; init; }
    public string? StateName { get; init; }
    public int StateFrame { get; init; }
    public short TicCount { get; init; }
    public activetypes Active { get; init; }
    public objdirtypes Dir { get; init; }
    public short Angle { get; init; }
    public short Hitpoints { get; init; }
    public int Speed { get; init; }
    public int Distance { get; init; }
    public short Temp1 { get; init; }
    public short Temp2 { get; init; }
    public bool Hidden { get; init; }
    public byte AreaNumber { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public byte TileX { get; init; }
    public byte TileY { get; init; }
    public Program.objflags RuntimeFlags { get; init; }

    public static ActorSnapshot Capture(Actor actor)
    {
        var (stateName, stateFrame) = LocateState(actor);
        return new ActorSnapshot
        {
            ClassName = actor.Name,
            IsPlayer = actor is PlayerPawn,
            StateName = stateName,
            StateFrame = stateFrame,
            TicCount = actor.TicCount,
            Active = actor.Active,
            Dir = actor.Dir,
            Angle = actor.Angle,
            Hitpoints = actor.Hitpoints,
            Speed = actor.Speed,
            Distance = actor.Distance,
            Temp1 = actor.Temp1,
            Temp2 = actor.Temp2,
            Hidden = actor.Hidden,
            AreaNumber = actor.AreaNumber,
            X = actor.X,
            Y = actor.Y,
            TileX = actor.TileX,
            TileY = actor.TileY,
            RuntimeFlags = actor.RuntimeFlags,
        };
    }

    /// <summary>Copies the saved runtime state onto a freshly created actor of the same class.</summary>
    public void ApplyTo(Actor actor)
    {
        actor.CurrentState = FindState(actor, StateName, StateFrame) ?? actor.CurrentState;
        actor.TicCount = TicCount;
        actor.Active = Active;
        actor.Dir = Dir;
        actor.Angle = Angle;
        actor.Hitpoints = Hitpoints;
        actor.Speed = Speed;
        actor.Distance = Distance;
        actor.Temp1 = Temp1;
        actor.Temp2 = Temp2;
        actor.Hidden = Hidden;
        actor.AreaNumber = AreaNumber;
        actor.X = X;
        actor.Y = Y;
        actor.TileX = TileX;
        actor.TileY = TileY;
        actor.RuntimeFlags = RuntimeFlags;
        actor.SyncPosition();
    }

    public void Write(BinaryWriter bw)
    {
        bw.Write(ClassName);
        bw.Write(IsPlayer);
        bw.Write(StateName ?? "");
        bw.Write(StateFrame);
        bw.Write(TicCount);
        bw.Write((byte)Active);
        bw.Write((byte)Dir);
        bw.Write(Angle);
        bw.Write(Hitpoints);
        bw.Write(Speed);
        bw.Write(Distance);
        bw.Write(Temp1);
        bw.Write(Temp2);
        bw.Write(Hidden);
        bw.Write(AreaNumber);
        bw.Write(X);
        bw.Write(Y);
        bw.Write(TileX);
        bw.Write(TileY);
        bw.Write((int)RuntimeFlags);
    }

    public static ActorSnapshot Read(BinaryReader br)
    {
        var className = br.ReadString();
        var isPlayer = br.ReadBoolean();
        var stateName = br.ReadString();
        return new ActorSnapshot
        {
            ClassName = className,
            IsPlayer = isPlayer,
            StateName = stateName.Length == 0 ? null : stateName,
            StateFrame = br.ReadInt32(),
            TicCount = br.ReadInt16(),
            Active = (activetypes)br.ReadByte(),
            Dir = (objdirtypes)br.ReadByte(),
            Angle = br.ReadInt16(),
            Hitpoints = br.ReadInt16(),
            Speed = br.ReadInt32(),
            Distance = br.ReadInt32(),
            Temp1 = br.ReadInt16(),
            Temp2 = br.ReadInt16(),
            Hidden = br.ReadBoolean(),
            AreaNumber = br.ReadByte(),
            X = br.ReadInt32(),
            Y = br.ReadInt32(),
            TileX = br.ReadByte(),
            TileY = br.ReadByte(),
            RuntimeFlags = (Program.objflags)br.ReadInt32(),
        };
    }

    // A group's frames are linked in order from its first frame (ActorStateResolver), so a
    // frame's position is how many Next hops it is from there while still inside the group.
    private static (string? StateName, int Frame) LocateState(Actor actor)
    {
        var current = actor.CurrentState;
        if (current == null || !actor.ResolvedStates.TryGetValue(current.StateName, out var frame))
            return (null, 0);

        var visited = new HashSet<ActorStateFrame>();
        for (var index = 0; frame != null && frame.StateName == current.StateName && visited.Add(frame); index++)
        {
            if (frame == current)
                return (current.StateName, index);
            frame = frame.Next;
        }

        return (current.StateName, 0);
    }

    private static ActorStateFrame? FindState(Actor actor, string? stateName, int frameIndex)
    {
        if (stateName == null || !actor.ResolvedStates.TryGetValue(stateName, out var frame))
            return null;

        for (var index = 0; index < frameIndex; index++)
        {
            // The actordefs changed since the save: settle for the last frame still in the group.
            if (frame.Next == null || frame.Next.StateName != stateName)
                break;
            frame = frame.Next;
        }

        return frame;
    }
}
