using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Editor.Data;

/// <summary>
/// Something the tools can put down on a plane: its value, how it's listed, and the picture it
/// shows (a wall texture or a thing class's sprite), if any
/// </summary>
public sealed record PaletteEntry(ushort Value, string Group, string Label, string? Texture = null, string? ThingClass = null)
{
    public override string ToString() => Label;
}

/// <summary>The palette for each plane, from the game pack's mapdefs</summary>
public static class PaletteEntries
{
    /// <summary>Planes 0 and 1 list what the mapdefs define; the others take any number</summary>
    public static bool HasPalette(int plane) => plane is 0 or 1;

    public static List<PaletteEntry> ForPlane(MapObjectTranslationAsset defs, int plane) => plane switch
    {
        0 => Plane0(defs),
        1 => Plane1(defs),
        _ => [],
    };

    /// <summary>What clearing a tile puts on a plane: plane 0's first area code (open floor), else 0</summary>
    public static ushort EraseValue(MapObjectTranslationAsset defs, int plane)
        => plane == 0 && defs.Floors.AreaStart is { } start ? (ushort)start : (ushort)0;

    private static List<PaletteEntry> Plane0(MapObjectTranslationAsset defs)
    {
        // Walls first, as they're what's most often put down; then doors, then the floor codes
        var entries = new List<PaletteEntry>();
        foreach (var (id, wall) in defs.Walls.Where(wall => wall.Key is > 0 and <= MapConstants.MAXWALLID).OrderBy(wall => wall.Key))
            entries.Add(new PaletteEntry((ushort)id, "Walls", $"{id}  {wall.North}{(wall.Switch != null ? "  (switch)" : "")}", Texture: wall.North));

        foreach (var (id, door) in defs.Doors.OrderBy(door => door.Key))
        {
            var details = door.Vertical ? "vertical" : "horizontal";
            if (!string.IsNullOrEmpty(door.Lock))
                details += $", {door.Lock}";
            entries.Add(new PaletteEntry((ushort)id, "Doors", $"{id}  Door, {details}", Texture: door.Vertical ? door.East : door.North));
        }

        var floors = defs.Floors;
        if (floors.AreaStart is { } start && floors.AreaCount is { } count)
        {
            for (int area = 0; area < count; area++)
                entries.Add(new PaletteEntry((ushort)(start + area), "Floor", $"Area {area}  ({start + area})"));
        }
        if (floors.Ambush is { } ambush and >= 0)
            entries.Add(new PaletteEntry((ushort)ambush, "Floor", $"Ambush  ({ambush})"));
        if (floors.SecretExit is { } secret and >= 0)
            entries.Add(new PaletteEntry((ushort)secret, "Floor", $"Secret exit  ({secret})"));
        foreach (var (code, action) in floors.Triggers ?? [])
            entries.Add(new PaletteEntry((ushort)code, "Floor", $"{action}  ({code})"));

        return entries;
    }

    private static List<PaletteEntry> Plane1(MapObjectTranslationAsset defs)
    {
        var entries = new List<PaletteEntry> { new(0, "Nothing", "Nothing  (0)") };

        foreach (var (id, start) in defs.PlayerStarts.OrderBy(start => start.Key))
            entries.Add(new PaletteEntry((ushort)id, "Player starts",
                $"{(start.Deathmatch ? "Deathmatch start" : "Player start")}, {Facing(start.Angles)}  ({id})"));

        foreach (var (id, thing) in defs.Things.OrderBy(thing => thing.Key))
        {
            var label = thing.Class;
            if (thing.Angles != 0 || thing.Patrol != 0)
                label += $", {Facing(thing.Angles)}";
            if (thing.Patrol != 0)
                label += ", patrols";
            if (thing.MinSkill > 0)
                label += $", skill {thing.MinSkill}+";
            entries.Add(new PaletteEntry((ushort)id, "Things", $"{label}  ({id})", ThingClass: thing.Class));
        }

        foreach (var (id, trigger) in defs.Triggers.OrderBy(trigger => trigger.Key))
            entries.Add(new PaletteEntry((ushort)id, "Triggers", $"{trigger.Action}{(trigger.Secret ? ", secret" : "")}  ({id})"));

        foreach (var (id, diagonal) in defs.Diagonals.OrderBy(diagonal => diagonal.Key))
            entries.Add(new PaletteEntry((ushort)id, "Diagonals", $"Diagonal {diagonal.Shape}  ({id})"));

        return entries;
    }

    /// <summary>The way an angle points: 0 east, 90 north, as mapdefs give them</summary>
    public static string Facing(int angles) => (((angles % 360) + 360) % 360) switch
    {
        0 => "east",
        45 => "north-east",
        90 => "north",
        135 => "north-west",
        180 => "west",
        225 => "south-west",
        270 => "south",
        315 => "south-east",
        var other => $"{other}°",
    };
}
