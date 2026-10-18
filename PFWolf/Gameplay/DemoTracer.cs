using System.Globalization;
using PFWolf.Assets.Demos;
using PFWolf.Entities.Actors;

namespace PFWolf.Gameplay;

/// <summary>
/// Writes down a demo as it plays, for the editor's demo browser (demotrace,
/// Commands/DemoCommands.cs): the player every frame, and every actor, door, pushwall and tile
/// as it changes. The format is PFWolf.Assets.Demos.DemoTrace's (docs/demo-viewer-plan.md).
/// </summary>
internal sealed class DemoTracer(MapManager map, Doors doors, PushWalls pushWalls, GameSession session)
{
    private TextWriter? _writer;
    private int _frame;
    private readonly Dictionary<Entities.Actors.Actor, int> _ids = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<int, string> _lastActor = [];
    private readonly HashSet<int> _gone = [];
    private readonly HashSet<Entities.Actors.Actor> _seen = new(ReferenceEqualityComparer.Instance);
    private ushort[,]? _tiles;
    private ushort[] _doorPositions = [];

    internal bool IsTracing => _writer != null;

    /// <summary>The frames written of the trace begun last</summary>
    internal int Frames => _frame;

    /// <summary>Starts writing a demo's trace; each frame of the demo that plays next is written</summary>
    internal void Begin(TextWriter writer, string label, string mapName, int frames)
    {
        _writer = writer;
        _frame = 0;
        _ids.Clear();
        _lastActor.Clear();
        _gone.Clear();
        _tiles = null;
        _doorPositions = [];
        writer.WriteLine($"{DemoTrace.Magic} {DemoTrace.Version}");
        writer.WriteLine($"demo {label} map {mapName} frames {frames}");
    }

    /// <summary>Ends the trace with how the demo ended and its demotest line</summary>
    internal void End(string how, string report)
    {
        if (_writer == null)
            return;
        _writer.WriteLine($"end {how} {report}");
        _writer.Flush();
        _writer = null;
    }

    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Writes what changed in the frame just played (PlayLoop, after everyone's thought)</summary>
    internal void AfterFrame()
    {
        if (_writer is not { } w || !session.DemoPlayback)
            return;

        w.WriteLine($"f {N(_frame)} {N((int)session.Tics)}");
        var pawn = session.Player;
        var state = session.PlayerState;
        w.WriteLine($"p {N(pawn.X)} {N(pawn.Y)} {N(pawn.Angle)} {N(state.Health)} {N(state.Score)} {Word(state.Weapon)}");

        WriteActors(w, pawn);
        WriteDoors(w);
        foreach (var wall in pushWalls.MovingWalls)
            w.WriteLine($"w {N(wall.X)} {N(wall.Y)} {N((int)wall.Dir)} {N(wall.Pos)}");
        WriteTiles(w);
        _frame++;
    }

    // A name as one word for the trace's space-split lines
    private static string Word(string? text) => string.IsNullOrEmpty(text) ? "-" : text.Replace(' ', '_');

    private void WriteActors(TextWriter w, PlayerPawn pawn)
    {
        _seen.Clear();
        foreach (var actor in map.GetActors())
        {
            if (actor.IsRemoved || actor is PlayerPawn || ReferenceEquals(actor, pawn))
                continue;
            _seen.Add(actor);
            if (!_ids.TryGetValue(actor, out var id))
                _ids[actor] = id = _ids.Count;

            var frame = actor.CurrentState;
            var line = $"{Word(actor.Name)} {N(actor.X)} {N(actor.Y)} {N(actor.Angle)} {N(actor.Hitpoints)} "
                + $"{Word(frame?.StateName)} {Word(frame == null ? null : frame.Sprite + frame.FrameLetter)}";
            if (_lastActor.TryGetValue(id, out var last) && last == line)
                continue;
            _lastActor[id] = line;
            w.WriteLine($"a {N(id)} {line}");
        }

        foreach (var (actor, id) in _ids)
        {
            if (!_seen.Contains(actor) && _gone.Add(id))
                w.WriteLine($"r {N(id)}");
        }
    }

    private void WriteDoors(TextWriter w)
    {
        if (_doorPositions.Length < doors.LastDoorObj)
            Array.Resize(ref _doorPositions, doors.LastDoorObj);
        for (int i = 0; i < doors.LastDoorObj; i++)
        {
            var door = doors.DoorObjList[i];
            if (door == null || door.Position == _doorPositions[i])
                continue;
            _doorPositions[i] = door.Position;
            w.WriteLine($"d {N(door.TileX)} {N(door.TileY)} {(door.Vertical ? 1 : 0)} {N(door.Position)}");
        }
    }

    // Tiles that changed since the level's first frame: pushwalls, walls a switch or script
    // changed, the tiles a moving pushwall covers
    private void WriteTiles(TextWriter w)
    {
        var tiles = map.TileMap;
        if (_tiles == null || _tiles.GetLength(0) != tiles.GetLength(0) || _tiles.GetLength(1) != tiles.GetLength(1))
        {
            _tiles = (ushort[,])tiles.Clone();
            return;
        }

        for (int x = 0; x < tiles.GetLength(0); x++)
        {
            for (int y = 0; y < tiles.GetLength(1); y++)
            {
                var tile = tiles[x, y];
                if (tile == _tiles[x, y])
                    continue;
                _tiles[x, y] = tile;
                var (kind, id) = tile == 0 ? ("floor", 0)
                    : (tile & GameConstants.BitDoor) != 0 ? ("door", 0)
                    : ("wall", tile & ~GameConstants.BitWall);
                w.WriteLine($"t {N(x)} {N(y)} {kind} {N(id)}");
            }
        }
    }
}
