using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Editor.Editing;

/// <summary>What turning a block did to one tile's value on one plane</summary>
[Flags]
public enum RemapOutcome
{
    /// <summary>Nothing about it points anywhere (or it points the same way after)</summary>
    Same = 0,

    /// <summary>Swapped for the mapdefs value that points the new way</summary>
    Turned = 1,

    /// <summary>It points somewhere, but the mapdefs have no value pointing the new way: left as it was</summary>
    Unmatched = 2,

    /// <summary>A code that reads the tile east of it (a map-info value, an enemy code's link), which the turn moves</summary>
    Paired = 4,
}

/// <summary>How many tiles of a turned block each outcome came to</summary>
public readonly record struct RemapCounts(int Turned, int Unmatched, int Paired)
{
    public RemapCounts Add(RemapOutcome outcome) => new(
        Turned + (outcome.HasFlag(RemapOutcome.Turned) ? 1 : 0),
        Unmatched + (outcome.HasFlag(RemapOutcome.Unmatched) ? 1 : 0),
        Paired + (outcome.HasFlag(RemapOutcome.Paired) ? 1 : 0));
}

/// <summary>
/// The values a rotated or flipped block of tiles needs so things in it still point the way
/// the block now does: things and player starts facing the new way, doors running the other
/// way across a quarter turn, diagonal walls with their solid corner turned, patches on the
/// turned faces. The mapdefs are searched for each; when there's none, the value stays as it
/// was (and is counted, to say so). Walls keep their ids: their faces are lit by the way they
/// look out, as the game draws them.
/// </summary>
public sealed class TileRemap
{
    private readonly MapObjectTranslationAsset _defs;
    private readonly TileTransform _transform;
    private readonly Func<string, bool>? _alwaysFaces;
    private readonly Dictionary<(int Plane, ushort Value), (ushort Value, RemapOutcome Outcome)> _cache = [];

    /// <param name="alwaysFaces">
    /// Thing classes that face a way even when the mapdefs only give one (wall sprites); null: none
    /// </param>
    public TileRemap(MapObjectTranslationAsset defs, TileTransform transform, Func<string, bool>? alwaysFaces = null)
    {
        _defs = defs;
        _transform = transform;
        _alwaysFaces = alwaysFaces;
    }

    public TileTransform Transform => _transform;

    /// <summary>The value a tile holds on the plane after the turn, and what became of it</summary>
    public ushort Remap(int plane, ushort value, out RemapOutcome outcome)
    {
        if (plane > 1 || value == 0)
        {
            outcome = RemapOutcome.Same;
            return value;
        }
        if (!_cache.TryGetValue((plane, value), out var result))
            _cache[(plane, value)] = result = plane == 0 ? Plane0(value) : Plane1(value);
        outcome = result.Outcome;
        return result.Value;
    }

    //
    // Plane 0: the tile's code (a door, a wall switch's sides, a floor code) and its patch
    //

    private (ushort, RemapOutcome) Plane0(ushort value)
    {
        int code = value & MapConstants.Plane0CodeMask, patch = value >> MapConstants.PatchShift;
        var outcome = RemapOutcome.Same;

        if (_defs.Doors.TryGetValue(code, out var door))
            (code, outcome) = Door(code, door);
        else if (code is > 0 and <= MapConstants.MaxWallId && _defs.Walls.TryGetValue(code, out var wall))
        {
            // A switch used only from some faces: the same switch from the turned faces is another wall
            if (wall.Switch is { Sides.Count: > 0 } wallSwitch && !SameFaces(wallSwitch.Sides))
                outcome = RemapOutcome.Unmatched;
        }
        else if (_defs.Floors.ActorCodes?.TryGetValue(code, out var actorCode) == true && actorCode.Link.Length > 0)
            outcome = RemapOutcome.Paired;

        if (patch != 0 && _defs.Patches.TryGetValue(patch, out var patchDef))
        {
            var (newPatch, patchOutcome) = Patch(patch, patchDef);
            patch = newPatch;
            outcome |= patchOutcome;
        }
        return ((ushort)(code | patch << MapConstants.PatchShift), outcome);
    }

    /// <summary>
    /// The door that runs the way this one does after the turn: alike in everything but its
    /// faces (the one-way side turned too), the faces turned where one is, else the nearest id
    /// (Wolf3D's are in pairs, a vertical one then a horizontal one)
    /// </summary>
    private (int, RemapOutcome) Door(int id, MapTextureTranslation door)
    {
        bool vertical = TileTransforms.SwapsAxes(_transform) ? !door.Vertical : door.Vertical;
        var opensFrom = door.OpensFrom.Length > 0 ? TileTransforms.Face(_transform, door.OpensFrom.ToLowerInvariant()) : "";
        if (vertical == door.Vertical && FacesTurnTo(door, door) && opensFrom.Equals(door.OpensFrom, StringComparison.OrdinalIgnoreCase))
            return (id, RemapOutcome.Same);

        var match = _defs.Doors
            .Where(other => other.Value.Vertical == vertical && LikeDoor(other.Value, door)
                            && other.Value.OpensFrom.Equals(opensFrom, StringComparison.OrdinalIgnoreCase))
            .OrderBy(other => FacesTurnTo(door, other.Value) ? 0 : 1)
            .ThenBy(other => Math.Abs(other.Key - id))
            .Select(other => (int?)other.Key)
            .FirstOrDefault();
        return match is { } found ? (found, found == id ? RemapOutcome.Same : RemapOutcome.Turned) : (id, RemapOutcome.Unmatched);
    }

    // Whether the door's faces, turned, are the other door's on the faces they turn to
    private bool FacesTurnTo(MapTextureTranslation door, MapTextureTranslation other)
        => new[] { "north", "south", "east", "west" }.All(face =>
            door.WallFace(face).Equals(other.WallFace(TileTransforms.Face(_transform, face)), StringComparison.OrdinalIgnoreCase));

    // Everything about a door but its faces and the way it runs
    private static bool LikeDoor(MapTextureTranslation a, MapTextureTranslation b)
        => a.Lock == b.Lock && a.LockMessage == b.LockMessage && a.LockMessageStyle == b.LockMessageStyle
           && a.AutomapColor == b.AutomapColor && a.LockedSound == b.LockedSound && a.OpenSound == b.OpenSound
           && a.CloseSound == b.CloseSound && a.TakesKey == b.TakesKey && a.Split == b.Split
           && a.WrongSideMessage == b.WrongSideMessage && a.WrongSideSound == b.WrongSideSound;

    /// <summary>The patch alike in everything but being on the turned faces</summary>
    private (int, RemapOutcome) Patch(int id, MapPatchTranslation patch)
    {
        if (patch.Sides.Count == 0 || SameFaces(patch.Sides))
            return (id, RemapOutcome.Same);

        var sides = TileTransforms.FaceSet(_transform, patch.Sides);
        var match = _defs.Patches
            .Where(other => other.Key is >= 1 and <= 255 && other.Value.Sides.Count > 0
                            && other.Value.Texture.Equals(patch.Texture, StringComparison.OrdinalIgnoreCase)
                            && other.Value.TextureEw.Equals(patch.TextureEw, StringComparison.OrdinalIgnoreCase)
                            && other.Value.Story == patch.Story
                            && (other.Value.Switch == null) == (patch.Switch == null)
                            && sides.SetEquals(other.Value.Sides))
            .OrderBy(other => Math.Abs(other.Key - id))
            .Select(other => (int?)other.Key)
            .FirstOrDefault();
        return match is { } found ? (found, RemapOutcome.Turned) : (id, RemapOutcome.Unmatched);
    }

    // Whether the faces are the same set after the turn ([east, west] under a flip, say)
    private bool SameFaces(IReadOnlyCollection<string> faces)
        => TileTransforms.FaceSet(_transform, faces).SetEquals(faces.Select(face => face.ToLowerInvariant()));

    //
    // Plane 1: things, player starts, diagonal markers, map-info codes
    //

    private (ushort, RemapOutcome) Plane1(ushort value)
    {
        if (_defs.Things.TryGetValue(value, out var thing))
            return Thing(value, thing);

        if (_defs.PlayerStarts.TryGetValue(value, out var start))
        {
            int angles = TileTransforms.Angle(_transform, start.Angles);
            if (angles == Normal(start.Angles))
                return (value, RemapOutcome.Same);
            var match = _defs.PlayerStarts
                .Where(other => other.Value.Deathmatch == start.Deathmatch && Normal(other.Value.Angles) == angles && other.Key is >= 0 and <= ushort.MaxValue)
                .OrderBy(other => Math.Abs(other.Key - value))
                .Select(other => (int?)other.Key)
                .FirstOrDefault();
            return match is { } found ? ((ushort)found, RemapOutcome.Turned) : (value, RemapOutcome.Unmatched);
        }

        if (_defs.Diagonals.TryGetValue(value, out var diagonal))
        {
            var shape = TileTransforms.Shape(_transform, diagonal.Shape);
            if (shape == diagonal.Shape)
                return (value, RemapOutcome.Same);
            var match = _defs.Diagonals
                .Where(other => other.Value.Shape == shape && other.Key is >= 0 and <= ushort.MaxValue)
                .OrderBy(other => other.Value.Texture.Equals(diagonal.Texture, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(other => Math.Abs(other.Key - value))
                .Select(other => (int?)other.Key)
                .FirstOrDefault();
            return match is { } found ? ((ushort)found, RemapOutcome.Turned) : (value, RemapOutcome.Unmatched);
        }

        if (_defs.MapInfo.TryGetValue(value >> 8, out var info) && MapInfoCodes.HasValue(info))
            return (value, RemapOutcome.Paired);

        return (value, RemapOutcome.Same);
    }

    /// <summary>
    /// The code for the same thing (class, skill, patrolling and the rest) facing the new way.
    /// A class the mapdefs only give one way doesn't face anywhere, unless it's a wall sprite.
    /// </summary>
    private (ushort, RemapOutcome) Thing(ushort value, MapActorTranslation thing)
    {
        int angles = TileTransforms.Angle(_transform, thing.Angles);
        if (angles == Normal(thing.Angles))
            return (value, RemapOutcome.Same);

        var alike = _defs.Things
            .Where(other => other.Key is >= 0 and <= ushort.MaxValue && LikeThing(other.Value, thing))
            .ToList();
        var match = alike.Where(other => Normal(other.Value.Angles) == angles)
            .OrderBy(other => Math.Abs(other.Key - value))
            .Select(other => (int?)other.Key)
            .FirstOrDefault();
        if (match is { } found)
            return ((ushort)found, RemapOutcome.Turned);

        bool faces = alike.Any(other => Normal(other.Value.Angles) != Normal(thing.Angles)) || _alwaysFaces?.Invoke(thing.Class) == true;
        return (value, faces ? RemapOutcome.Unmatched : RemapOutcome.Same);
    }

    private static bool LikeThing(MapActorTranslation a, MapActorTranslation b)
        => a.Class == b.Class && a.Patrol == b.Patrol && a.MinSkill == b.MinSkill && a.Else == b.Else
           && a.InWall == b.InWall && a.Faction == b.Faction && a.Translation == b.Translation;

    private static int Normal(int angles) => ((angles % 360) + 360) % 360;
}
