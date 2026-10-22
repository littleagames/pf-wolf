using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Editor.Data;

/// <summary>What makes an object-plane value name another tile</summary>
public enum ObjectLinkKind
{
    /// <summary>A map-info tag-link (Blake Stone's 0xF8nn on a barrier switch): the switch and the tile share a tag</summary>
    TagLink,
    /// <summary>A switch whose walls.yaml switch has `link: object-plane` (Planet Strike's barrier switches)</summary>
    SwitchLink,
    /// <summary>A floor code whose trigger runs A_SmartSwitch: it works the tile its value names</summary>
    SmartSwitch,
    /// <summary>A wall whose switch or trigger runs A_Teleport: the tile on this floor it takes the player to</summary>
    Teleport,
    /// <summary>A floors actor-code with `link: east`: the enemy's death works the tile the value east of it names</summary>
    ActorLink,
}

/// <summary>
/// A tile whose object-plane data names another tile, the way the game reads it: from tile
/// (<see cref="X"/>, <see cref="Y"/>), by the value on (<see cref="ValueX"/>, <see cref="ValueY"/>)
/// (that tile, or the one east of it), to (<see cref="ToX"/>, <see cref="ToY"/>), x in the
/// value's high byte and y in its low byte. <see cref="Floor"/> is set when the target is on
/// another floor of the mission (a tag-link's), which the game leaves alone.
/// </summary>
public sealed record ObjectLink(ObjectLinkKind Kind, int X, int Y, int ValueX, int ValueY, int ToX, int ToY, int? Floor = null)
{
    public bool OnThisFloor => Floor == null;

    /// <summary>What the link does, for the status bar ("barrier switch link", "smart switch")</summary>
    public string Description => Kind switch
    {
        ObjectLinkKind.TagLink => "switch link (map-info tag-link)",
        ObjectLinkKind.SwitchLink => "switch link (by its own value)",
        ObjectLinkKind.SmartSwitch => "smart switch",
        ObjectLinkKind.Teleport => "teleporter",
        _ => "enemy's death link",
    };
}

public sealed partial class MapTiles : Editing.IObjectPairs
{
    /// <summary>Whether the tile holds a code or makes a link whose value is in the tile after it (see <see cref="IsValueTile"/>)</summary>
    public bool HasValueTile(int x, int y)
        => x + 1 < Width
           && (MapInfoKind(this[1, x, y]) is { } kind && MapInfoCodes.HasValue(kind) || LinkFrom(x, y) is { } link && link.ValueX == x + 1);

    /// <summary>Switch and trigger actions that read their tile's object-plane value as data, not as a thing</summary>
    private static readonly string[] ValueReadingActions = ["A_SmartSwitch", "A_Teleport", "A_Vend"];

    /// <summary>The level's floor in its mission (game-info floor-number), which map-info links name</summary>
    public int FloorNumber => Properties.FloorNumber ?? 0;

    /// <summary>The link the tile makes, or null when it makes none</summary>
    public ObjectLink? LinkFrom(int x, int y)
    {
        int obj = this[1, x, y];
        if (obj == 0)
            return null;

        // A map-info tag-link: the value is in the tile after it, and the code's low byte is the floor (255 this one)
        if (MapInfoKind(obj) is { } kind && kind.Equals(MapInfoCodes.TagLink, StringComparison.OrdinalIgnoreCase))
        {
            if (x + 1 >= Width)
                return null;
            int value = this[1, x + 1, y], floor = obj & 0xff;
            return new ObjectLink(ObjectLinkKind.TagLink, x, y, x + 1, y, value >> 8, value & 0xff,
                floor == 0xff || floor == FloorNumber ? null : floor);
        }

        var tileKind = KindAt(x, y);
        if (tileKind == TileKind.Wall)
        {
            var switches = SwitchesAt(x, y).ToList();
            if (switches.Any(sw => sw.Link.Equals("object-plane", StringComparison.OrdinalIgnoreCase)))
                return new ObjectLink(ObjectLinkKind.SwitchLink, x, y, x, y, obj >> 8, obj & 0xff);
            if (switches.Any(sw => sw.Actions.Any(action => IsAction(action, "A_Teleport")))
                || (Trigger(x, y) is { } trigger && IsAction(trigger.Action, "A_Teleport")))
                return TeleportLink(x, y, obj);
            return null;
        }

        if (tileKind == TileKind.Floor)
        {
            if (Floors.Triggers?.TryGetValue(this[0, x, y], out var action) == true && IsAction(action, "A_SmartSwitch"))
                return new ObjectLink(ObjectLinkKind.SmartSwitch, x, y, x, y, obj >> 8, obj & 0xff);
            if (Floors.ActorCodes?.TryGetValue(this[0, x, y], out var code) == true
                && code.Link.Equals("east", StringComparison.OrdinalIgnoreCase) && x + 1 < Width && this[1, x + 1, y] is var target and not 0)
                return new ObjectLink(ObjectLinkKind.ActorLink, x, y, x + 1, y, target >> 8, target & 0xff);
        }
        return null;
    }

    // A_Teleport (HubActions.TeleportAction): 0xF4nn is a floor, not a tile; 0xF5nn keeps the tile in the next tile east
    private ObjectLink? TeleportLink(int x, int y, int obj)
    {
        if (MapInfoKind(obj) is { } kind)
        {
            if (!kind.Equals(MapInfoCodes.Value, StringComparison.OrdinalIgnoreCase) || x + 1 >= Width)
                return null;
            int value = this[1, x + 1, y];
            return new ObjectLink(ObjectLinkKind.Teleport, x, y, x + 1, y, value >> 8, value & 0xff);
        }
        return new ObjectLink(ObjectLinkKind.Teleport, x, y, x, y, obj >> 8, obj & 0xff);
    }

    /// <summary>Every link on the level</summary>
    public IEnumerable<ObjectLink> Links()
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (LinkFrom(x, y) is { } link)
                    yield return link;
    }

    /// <summary>
    /// What reads a tile's object-plane value as data rather than a thing, or null when nothing
    /// does: a map-info code's value (the tile after it), a link's value, a door's lock, a
    /// vending wall's uses, a teleporter's floor
    /// </summary>
    public string? ValueUse(int x, int y)
    {
        int obj = this[1, x, y];
        if (obj == 0)
            return null;

        if (x > 0 && MapInfoKind(this[1, x - 1, y]) is { } before && MapInfoCodes.HasValue(before))
            return $"the value of the {before} code west of it";
        if (x > 0 && LinkFrom(x - 1, y) is { } westLink && westLink.ValueX == x)
            return $"the value of the {westLink.Description} west of it";
        if (LinkFrom(x, y) is { } link && link.ValueX == x)
            return link.Description;

        var kind = KindAt(x, y);
        if (kind == TileKind.Door && Defs.DoorLocks.ContainsKey(obj))
            return "door lock";
        if (kind == TileKind.Wall && SwitchesAt(x, y).Any(sw => sw.Actions.Any(action => ValueReadingActions.Any(name => IsAction(action, name)))))
            return "its switch's value";
        return null;
    }

    /// <summary>The map-info kind of a code (by its high byte), or null when it isn't one</summary>
    public string? MapInfoKind(int value) => value != 0 && Defs.MapInfo.TryGetValue(value >> 8, out var kind) ? kind : null;

    /// <summary>The switches on a tile: its wall's and its patch's</summary>
    public IEnumerable<MapSwitchTranslation> SwitchesAt(int x, int y)
    {
        if (Wall(x, y)?.Switch is { } wallSwitch)
            yield return wallSwitch;
        if (PatchDef(x, y)?.Switch is { } patchSwitch)
            yield return patchSwitch;
    }

    private static bool IsAction(string call, string name)
        => call.StartsWith(name, StringComparison.OrdinalIgnoreCase)
           && (call.Length == name.Length || call[name.Length] is '(' or ' ');

    /// <summary>
    /// What a map-info code says, for the status bar: the colors or flats it gives the level,
    /// the hint it places, the floor it goes to, or its raw value
    /// </summary>
    public string MapInfoText(int x, int y, int obj, string kind)
    {
        int low = obj & 0xff;
        int? next = x + 1 < Width ? this[1, x + 1, y] : null;
        if (kind.Equals(MapInfoCodes.CeilingFloorColors, StringComparison.OrdinalIgnoreCase))
            return next is { } colors ? $"ceiling color {PaletteText(colors >> 8)}, floor color {PaletteText(colors & 0xff)} (the tile east holds them)" : "colors, but no tile east of it to hold them";
        if (kind.Equals(MapInfoCodes.CeilingFloorFlats, StringComparison.OrdinalIgnoreCase))
            return next is { } flats ? $"ceiling flat {FlatText(Defs.Flats.Ceiling, flats >> 8)}, floor flat {FlatText(Defs.Flats.Floor, flats & 0xff)} (the tile east holds them)" : "flats, but no tile east of it to hold them";
        if (kind.Equals(MapInfoCodes.TagLink, StringComparison.OrdinalIgnoreCase))
            return LinkFrom(x, y) is { } link ? LinkText(link) : "a link, but no tile east of it to hold its target";
        if (MapInfoCodes.HintText(kind) is { } textName)
        {
            var messages = content.HintMessages(textName);
            var message = low >= 1 && low <= messages.Count ? $": \"{Shorten(messages[low - 1])}\"" : $" (it has only {messages.Count})";
            return $"hint {low} of {textName}{message}";
        }
        if (KindAt(x, y) == TileKind.Wall && (obj & 0xff00) == 0xf400 && LinkFrom(x, y) == null && SwitchesAt(x, y).Any(sw => sw.Actions.Any(action => IsAction(action, "A_Teleport"))))
            return $"teleports to floor {low}";
        if (SwitchesAt(x, y).SelectMany(sw => sw.Actions).FirstOrDefault(action => IsAction(action, "A_Vend")) is { } vend
            && VendCode(vend) == obj >> 8)
            return $"a vending wall's uses: {low}";
        if (kind.Equals(MapInfoCodes.Value, StringComparison.OrdinalIgnoreCase))
            return next is { } value ? $"a value, {value} (0x{value:X4}) in the tile east" : "a value, but no tile east of it to hold it";
        return $"map info ({kind}), {low}";
    }

    /// <summary>A link as the status bar says it: what it is and where it goes</summary>
    public string LinkText(ObjectLink link)
    {
        var target = $"({link.ToX}, {link.ToY})";
        if (!link.OnThisFloor)
            return $"{link.Description} to {target} on floor {link.Floor}";
        if (link.ToX >= Width || link.ToY >= Height)
            return $"{link.Description} to {target}, off the level";
        return $"{link.Description} to {target}: {TargetText(link.ToX, link.ToY)}";
    }

    // What's on a link's target tile, briefly
    private string TargetText(int x, int y)
    {
        if (Thing(x, y) is { } thing)
            return thing.Class;
        return KindAt(x, y) switch
        {
            TileKind.Door => $"door {this[0, x, y]}",
            TileKind.Wall => IsSwitch(x, y) ? $"switch {this[0, x, y]}" : $"wall {this[0, x, y]}",
            TileKind.Floor => "open floor",
            _ => $"plane 0 value {this[0, x, y]}",
        };
    }

    // A_Vend(item, given, ..., code): the object-plane high byte that gives its uses, the call's last argument
    private static int? VendCode(string call)
    {
        int open = call.IndexOf('('), close = call.LastIndexOf(')');
        if (open < 0 || close <= open)
            return null;
        var last = call[(open + 1)..close].Split(',').Last().Trim();
        return int.TryParse(last, out var code) ? code : null;
    }

    private string PaletteText(int index)
        => index < content.Palette.Length ? $"{index} (#{content.Palette[index].Red:X2}{content.Palette[index].Green:X2}{content.Palette[index].Blue:X2})" : index.ToString();

    private static string Shorten(string text)
    {
        var line = text.Replace('\n', ' ');
        return line.Length > 60 ? line[..57] + "..." : line;
    }
}
