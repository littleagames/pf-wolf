using System.Globalization;

namespace PFWolf.Assets.Demos;

/// <summary>Where the player was and how they stood at the end of a frame</summary>
/// <param name="X">In tiles (the game's fixed point over 65536)</param>
/// <param name="Angle">Degrees, 0 east, counting anticlockwise (up the map is 90)</param>
public readonly record struct TracePlayer(double X, double Y, int Angle, int Health, int Score, string Weapon);

/// <summary>An actor as it was from a frame on</summary>
/// <param name="Sprite">Its frame's sprite and letter ("GARDA"); its pictures add the view, 0-8</param>
public readonly record struct TraceActorState(double X, double Y, int Angle, int Hitpoints, string State, string Sprite);

/// <summary>Something in the level that changed, with the frame it changed on</summary>
public readonly record struct TraceChange<T>(int Frame, T State);

/// <summary>A door's opening: 0 shut to 65535 wide open</summary>
public readonly record struct TraceDoor(int X, int Y, bool Vertical);

/// <summary>A pushwall sliding from the tile it's leaving, Dir (0 north, 1 east, 2 south, 3 west), Pos of 64 into the next</summary>
public readonly record struct TracePushWall(int X, int Y, int Dir, int Pos);

/// <summary>What a tile became: nothing, a wall (and its id) or a door</summary>
public enum TraceTileKind { Floor, Wall, Door }

public readonly record struct TraceTile(TraceTileKind Kind, int WallId);

/// <summary>Something worth a marker on the map, worked out from the changes</summary>
public enum TraceEventKind { Kill, Pickup, Hurt, Door, PushWall, Death, End }

public readonly record struct TraceEvent(int Frame, TraceEventKind Kind, string Text, double X, double Y);

/// <summary>
/// What the game wrote down playing a demo (`demotrace`, Commands/DemoCommands.cs): the player
/// every frame, and every actor, door, pushwall and tile as it changed. The format is
/// docs/demo-viewer-plan.md's; lines it doesn't know are skipped.
/// </summary>
public sealed class DemoTrace
{
    public const string Magic = "pfwolf-demotrace";
    public const int Version = 1;

    /// <summary>The game's fixed point: 65536 a tile</summary>
    public const double TileUnit = 65536;

    public string Label { get; private set; } = "";
    public string MapName { get; private set; } = "";

    /// <summary>The player at the end of each frame, first to last</summary>
    public List<TracePlayer> Player { get; } = [];

    /// <summary>The tics each frame took</summary>
    public List<int> Tics { get; } = [];

    public int FrameCount => Player.Count;

    /// <summary>Each actor's class, by the id the trace gave it</summary>
    public Dictionary<int, string> ActorClasses { get; } = [];

    /// <summary>Each actor's changes, oldest first; its first is where it appeared</summary>
    public Dictionary<int, List<TraceChange<TraceActorState>>> Actors { get; } = [];

    /// <summary>The frame each actor left the level on, if it did</summary>
    public Dictionary<int, int> Removed { get; } = [];

    public Dictionary<TraceDoor, List<TraceChange<int>>> Doors { get; } = [];
    public Dictionary<(int X, int Y), List<TraceChange<TraceTile>>> Tiles { get; } = [];

    /// <summary>The pushwalls on the move on each frame they moved</summary>
    public Dictionary<int, List<TracePushWall>> PushWalls { get; } = [];

    /// <summary>How the level ended for the demo (completed, died...), and the demotest line</summary>
    public string EndedHow { get; private set; } = "";
    public string Report { get; private set; } = "";

    public List<TraceEvent> Events { get; } = [];

    public static DemoTrace Read(TextReader reader)
    {
        var trace = new DemoTrace();
        var header = reader.ReadLine();
        if (header == null || !header.StartsWith(Magic, StringComparison.Ordinal))
            throw new InvalidDataException("Not a demo trace: it doesn't start with " + Magic);

        int frame = -1;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var parts = line.Split(' ');
            try
            {
                trace.ReadLine(parts, ref frame);
            }
            catch (Exception e) when (e is FormatException or IndexOutOfRangeException or OverflowException)
            {
                // A broken line is skipped like an unknown one
            }
        }
        trace.FindEvents();
        return trace;
    }

    public static DemoTrace Read(string path)
    {
        using var reader = new StreamReader(path);
        return Read(reader);
    }

    private static int Int(string text) => int.Parse(text, CultureInfo.InvariantCulture);
    private static double InTiles(string text) => Int(text) / TileUnit;

    private void ReadLine(string[] parts, ref int frame)
    {
        switch (parts[0])
        {
            case "demo":
                Label = parts[1];
                MapName = parts[3];
                break;
            case "f":
                frame = Int(parts[1]);
                Tics.Add(Int(parts[2]));
                // A frame with no player line keeps the last
                Player.Add(Player.Count > 0 ? Player[^1] : default);
                break;
            case "p" when frame >= 0:
                Player[^1] = new TracePlayer(InTiles(parts[1]), InTiles(parts[2]), Int(parts[3]), Int(parts[4]), Int(parts[5]), parts.Length > 6 ? parts[6] : "");
                break;
            case "a" when frame >= 0:
                int id = Int(parts[1]);
                ActorClasses.TryAdd(id, parts[2]);
                if (!Actors.TryGetValue(id, out var changes))
                    Actors[id] = changes = [];
                changes.Add(new(frame, new TraceActorState(InTiles(parts[3]), InTiles(parts[4]), Int(parts[5]), Int(parts[6]), parts[7], parts[8])));
                break;
            case "r" when frame >= 0:
                Removed.TryAdd(Int(parts[1]), frame);
                break;
            case "d" when frame >= 0:
                var door = new TraceDoor(Int(parts[1]), Int(parts[2]), parts[3] == "1");
                if (!Doors.TryGetValue(door, out var positions))
                    Doors[door] = positions = [];
                positions.Add(new(frame, Int(parts[4])));
                break;
            case "w" when frame >= 0:
                if (!PushWalls.TryGetValue(frame, out var walls))
                    PushWalls[frame] = walls = [];
                walls.Add(new TracePushWall(Int(parts[1]), Int(parts[2]), Int(parts[3]), Int(parts[4])));
                break;
            case "t" when frame >= 0:
                var tile = (Int(parts[1]), Int(parts[2]));
                var kind = parts[3] switch { "wall" => TraceTileKind.Wall, "door" => TraceTileKind.Door, _ => TraceTileKind.Floor };
                if (!Tiles.TryGetValue(tile, out var tileChanges))
                    Tiles[tile] = tileChanges = [];
                tileChanges.Add(new(frame, new TraceTile(kind, parts.Length > 4 ? Int(parts[4]) : 0)));
                break;
            case "end":
                EndedHow = parts[1];
                Report = string.Join(' ', parts.Skip(2));
                break;
        }
    }

    /// <summary>The latest change at or before a frame, or null before the first</summary>
    public static T? At<T>(List<TraceChange<T>> changes, int frame) where T : struct
    {
        int low = 0, high = changes.Count - 1, found = -1;
        while (low <= high)
        {
            int middle = (low + high) / 2;
            if (changes[middle].Frame <= frame)
            {
                found = middle;
                low = middle + 1;
            }
            else
                high = middle - 1;
        }
        return found < 0 ? null : changes[found].State;
    }

    /// <summary>Every actor in the level at a frame, with how it stood</summary>
    public IEnumerable<(int Id, string Class, TraceActorState State)> ActorsAt(int frame)
    {
        foreach (var (id, changes) in Actors)
        {
            if (Removed.TryGetValue(id, out var gone) && gone <= frame)
                continue;
            if (At(changes, frame) is { } state)
                yield return (id, ActorClasses[id], state);
        }
    }

    // Kills (hit points to nothing), pickups (a still thing gone), the player hurt, doors
    // opening, pushwalls starting, and the end, for markers on the map and the timeline
    private void FindEvents()
    {
        foreach (var (id, changes) in Actors)
        {
            var name = ActorClasses[id];
            bool killed = false;
            for (int i = 1; i < changes.Count; i++)
            {
                if (changes[i - 1].State.Hitpoints > 0 && changes[i].State.Hitpoints <= 0)
                {
                    killed = true;
                    Events.Add(new(changes[i].Frame, TraceEventKind.Kill, $"Killed {name}", changes[i].State.X, changes[i].State.Y));
                }
            }

            // Gone without dying or moving: picked up (projectiles and the like move)
            if (Removed.TryGetValue(id, out var gone) && !killed
                && changes.All(change => change.State.X == changes[0].State.X && change.State.Y == changes[0].State.Y))
                Events.Add(new(gone, TraceEventKind.Pickup, $"Picked up {name}", changes[0].State.X, changes[0].State.Y));
        }

        for (int frame = 1; frame < Player.Count; frame++)
        {
            var (before, now) = (Player[frame - 1], Player[frame]);
            if (now.Health < before.Health)
                Events.Add(new(frame, now.Health <= 0 ? TraceEventKind.Death : TraceEventKind.Hurt,
                    now.Health <= 0 ? "Died" : $"Hurt {before.Health - now.Health} ({now.Health} left)", now.X, now.Y));
        }

        foreach (var (door, positions) in Doors)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                bool wasShut = i == 0 || positions[i - 1].State == 0;
                if (wasShut && positions[i].State > 0)
                    Events.Add(new(positions[i].Frame, TraceEventKind.Door, $"Door at {door.X},{door.Y} opens", door.X + 0.5, door.Y + 0.5));
            }
        }

        int lastPushFrame = -2;
        foreach (var (frame, walls) in PushWalls.OrderBy(pair => pair.Key))
        {
            if (frame != lastPushFrame + 1)
                Events.Add(new(frame, TraceEventKind.PushWall, $"Pushwall at {walls[0].X},{walls[0].Y}", walls[0].X + 0.5, walls[0].Y + 0.5));
            lastPushFrame = frame;
        }

        if (Player.Count > 0 && EndedHow.Length > 0)
            Events.Add(new(Player.Count - 1, TraceEventKind.End, $"Ended: {EndedHow}", Player[^1].X, Player[^1].Y));

        Events.Sort((a, b) => a.Frame.CompareTo(b.Frame));
    }
}
