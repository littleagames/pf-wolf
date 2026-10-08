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

/// <summary>One object-plane code for a thing class: which way it faces, whether it patrols, the skill it starts at</summary>
public sealed record ThingVariant(ushort Id, int Angles, bool Patrols, int MinSkill);

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

        // One entry per class; which way it faces, its skills and patrolling pick among its codes (ThingVariants)
        foreach (var (thingClass, variants) in ThingVariants(defs).OrderBy(group => group.Value[0].Id))
        {
            var label = variants.Count == 1 ? $"{thingClass}  ({variants[0].Id})" : $"{thingClass}  ({variants.Count} ways)";
            entries.Add(new PaletteEntry(variants[0].Id, "Things", label, ThingClass: thingClass));
        }

        foreach (var (id, trigger) in defs.Triggers.OrderBy(trigger => trigger.Key))
            entries.Add(new PaletteEntry((ushort)id, "Triggers", $"{trigger.Action}{(trigger.Secret ? ", secret" : "")}  ({id})"));

        foreach (var (id, diagonal) in defs.Diagonals.OrderBy(diagonal => diagonal.Key))
            entries.Add(new PaletteEntry((ushort)id, "Diagonals", $"Diagonal {diagonal.Shape}  ({id})"));

        return entries;
    }

    /// <summary>Each thing class's object-plane codes, lowest first: one per facing, skill and patrolling it comes in</summary>
    public static Dictionary<string, List<ThingVariant>> ThingVariants(MapObjectTranslationAsset defs)
    {
        var variants = new Dictionary<string, List<ThingVariant>>(StringComparer.Ordinal);
        foreach (var (id, thing) in defs.Things.OrderBy(thing => thing.Key))
        {
            if (id is < 0 or > ushort.MaxValue)
                continue;
            if (!variants.TryGetValue(thing.Class, out var list))
                variants[thing.Class] = list = [];
            list.Add(new ThingVariant((ushort)id, thing.Angles, thing.Patrol != 0, thing.MinSkill));
        }
        return variants;
    }

    /// <summary>
    /// The code for a thing that faces, patrols and starts at the skill asked for, or the nearest
    /// it comes in: the skill and patrolling as asked if it can, then the facing
    /// </summary>
    public static ThingVariant Closest(IReadOnlyList<ThingVariant> variants, int angles, bool patrols, int minSkill)
        => variants.OrderBy(v => (v.Angles == angles ? 0 : 1) + (v.Patrols == patrols ? 0 : 2) + (v.MinSkill == minSkill ? 0 : 4)).First();

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
