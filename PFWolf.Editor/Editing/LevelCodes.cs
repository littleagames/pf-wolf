using PFWolf.Assets;
using PFWolf.Editor.Data;

namespace PFWolf.Editor.Editing;

/// <summary>A map-info code or a link on a level, as the Level codes panel lists it</summary>
public sealed record LevelCode(int X, int Y, ushort Value, string Kind, string Text)
{
    public override string ToString() => $"({X}, {Y})  {Text}";
}

/// <summary>
/// A number a code form asks for: a palette index, a floor, a hint's number, a tile's x or y.
/// <paramref name="Choices"/> (each starting with its number: "10  F_GRYPN1") are offered when
/// there are some.
/// </summary>
public sealed record CodeField(string Label, int Initial, int Min, int Max, IReadOnlyList<string>? Choices = null)
{
    /// <summary>The number a field's text holds: its leading whole number ("10  F_GRYPN1" is 10), or null</summary>
    public static int? Parse(string? text)
    {
        var digits = new string((text ?? "").Trim().TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var number) ? number : null;
    }

    /// <summary>What's wrong with an answer, or null when it'll do</summary>
    public string? Check(string? text)
        => Parse(text) is { } number && number >= Min && number <= Max ? null : $"{Label} is a whole number from {Min} to {Max}";
}

/// <summary>
/// What editing (or placing) a code asks for, and the tiles its answers write: the code's own
/// tile and the tile that holds its value. When <see cref="PicksTile"/> is set, the last two
/// fields are a target tile's x and y, which can be picked on the map instead.
/// </summary>
public sealed record CodeForm(string Title, string Description, IReadOnlyList<CodeField> Fields, bool PicksTile,
    Func<int[], IReadOnlyList<(int X, int Y, ushort Value)>> Writes);

/// <summary>
/// Blake Stone's level codes: listing the map-info codes and links on a level, and the forms
/// and edits that place, change and remove them. A code whose value is in the tile after it is
/// always placed, moved and removed with that tile.
/// </summary>
public static class LevelCodes
{
    /// <summary>A tile as an object-plane value: x in the high byte, y in the low one</summary>
    public static ushort Tile(int x, int y) => (ushort)((x & 0xff) << 8 | (y & 0xff));

    public static bool IsLevelWide(string kind)
        => kind.Equals(MapInfoCodes.CeilingFloorColors, StringComparison.OrdinalIgnoreCase)
           || kind.Equals(MapInfoCodes.CeilingFloorFlats, StringComparison.OrdinalIgnoreCase);

    /// <summary>Every map-info code and link on the level, row by row (the value tiles aren't listed apart)</summary>
    public static List<LevelCode> List(MapTiles tiles)
    {
        var codes = new List<LevelCode>();
        for (int y = 0; y < tiles.Height; y++)
        {
            for (int x = 0; x < tiles.Width; x++)
            {
                if (tiles.IsValueTile(x, y))
                    continue;
                int obj = tiles[1, x, y];
                if (tiles.MapInfoKind(obj) is { } kind)
                    codes.Add(new LevelCode(x, y, (ushort)obj, kind, tiles.MapInfoText(x, y, obj, kind)));
                else if (tiles.LinkFrom(x, y) is { } link)
                    codes.Add(new LevelCode(x, y, (ushort)obj, link.Kind.ToString(), tiles.LinkText(link)));
            }
        }
        return codes;
    }

    /// <summary>The map-info codes the mapdefs give, by their high byte, with a label each for the palette</summary>
    public static List<(int High, string Kind, string Label)> Kinds(MapObjectTranslationAsset defs)
        => defs.MapInfo.OrderByDescending(code => code.Key)
            .Select(code => (code.Key, code.Value, $"{KindLabel(code.Value)}  (0x{code.Key:X2}nn)"))
            .ToList();

    /// <summary>What a map-info kind is called in the palette and dialogs</summary>
    public static string KindLabel(string kind)
    {
        if (MapInfoCodes.HintText(kind) is { } text)
            return $"Hint from {text}";
        return kind.ToLowerInvariant() switch
        {
            MapInfoCodes.CeilingFloorColors => "Ceiling and floor colors",
            MapInfoCodes.CeilingFloorFlats => "Ceiling and floor flats",
            MapInfoCodes.TagLink => "Switch link (tag-link)",
            MapInfoCodes.Value => "Code with a value (a teleporter's tile)",
            _ => "Code read by what it's on (a teleporter's floor, a food unit's uses, a timer)",
        };
    }

    /// <summary>The first code of a kind on the level (the only one the game takes), or null</summary>
    public static (int X, int Y)? FirstOfKind(MapTiles tiles, string kind)
    {
        for (int y = 0; y < tiles.Height; y++)
            for (int x = 0; x < tiles.Width; x++)
                if (!tiles.IsValueTile(x, y) && tiles.MapInfoKind(tiles[1, x, y]) is { } found && found.Equals(kind, StringComparison.OrdinalIgnoreCase))
                    return (x, y);
        return null;
    }

    /// <summary>The first pair of tiles in the top row with nothing on the object plane, for a level-wide code, or null</summary>
    public static (int X, int Y)? FreeTopRowPair(MapTiles tiles)
    {
        for (int x = 0; x + 1 < tiles.Width; x++)
            if (tiles[1, x, 0] == 0 && tiles[1, x + 1, 0] == 0)
                return (x, 0);
        return null;
    }

    /// <summary>
    /// Where a new code of this kind goes: a level-wide one where the level has one already
    /// (only the first counts), else in the first free pair of the top row; any other at the tile
    /// asked for. Null when a level-wide one has nowhere to go.
    /// </summary>
    public static (int X, int Y)? PlaceFor(MapTiles tiles, string kind, int x, int y)
        => IsLevelWide(kind) ? FirstOfKind(tiles, kind) ?? FreeTopRowPair(tiles) : (x, y);

    /// <summary>The tile a code or link keeps its value in, or null for a code whose value is its own low byte</summary>
    public static (int X, int Y)? ValueTile(MapTiles tiles, int x, int y)
    {
        if (tiles.MapInfoKind(tiles[1, x, y]) is { } kind)
            return MapInfoCodes.HasValue(kind) && x + 1 < tiles.Width ? (x + 1, y) : null;
        return tiles.LinkFrom(x, y) is { } link ? (link.ValueX, link.ValueY) : null;
    }

    /// <summary>
    /// The form for a code already on the tile (a map-info code, or a link's source), or null when
    /// the tile holds neither
    /// </summary>
    public static CodeForm? EditForm(MapTiles tiles, int x, int y)
    {
        int obj = tiles[1, x, y];
        if (tiles.IsValueTile(x, y))
            return null;
        if (tiles.MapInfoKind(obj) is { } kind)
            return CodeFormFor(tiles, kind, (ushort)obj, x, y, existing: true);
        if (tiles.LinkFrom(x, y) is { } link)
        {
            return new CodeForm($"{Capitalized(link.Description)} at ({x}, {y})",
                "The tile it works, x and y: type them or pick it on the map.",
                TargetFields(tiles, link.ToX, link.ToY), PicksTile: true,
                values => [(link.ValueX, link.ValueY, Tile(values[0], values[1]))]);
        }
        return null;
    }

    /// <summary>The form for putting a new code of a kind (by its high byte) on (x, y)</summary>
    public static CodeForm NewForm(MapTiles tiles, int high, string kind, int x, int y)
        => CodeFormFor(tiles, kind, (ushort)(high << 8), x, y, existing: false);

    private static CodeForm CodeFormFor(MapTiles tiles, string kind, ushort code, int x, int y, bool existing)
    {
        int high = code & 0xff00, low = code & 0xff;
        int value = existing && x + 1 < tiles.Width ? tiles[1, x + 1, y] : 0;
        var where = IsLevelWide(kind) ? "for the level" : $"at ({x}, {y})";
        var title = $"{KindLabel(kind)} {where}";

        // The code (its own low byte) and its value in the tile after it
        IReadOnlyList<(int, int, ushort)> CodeAndValue(int codeLow, ushort pairValue)
            => x + 1 < tiles.Width ? [(x, y, (ushort)(high | codeLow)), (x + 1, y, pairValue)] : [(x, y, (ushort)(high | codeLow))];

        switch (kind.ToLowerInvariant())
        {
            case MapInfoCodes.CeilingFloorColors:
                return new CodeForm(title, "The level's ceiling and floor colors, as palette indices (game-info's colors for the level win over them).",
                    [new CodeField("Ceiling color", existing ? value >> 8 : 0, 0, 255, PaletteChoices(tiles)),
                     new CodeField("Floor color", existing ? value & 0xff : 0, 0, 255, PaletteChoices(tiles))],
                    PicksTile: false, values => CodeAndValue(low, (ushort)(values[0] << 8 | values[1])));

            case MapInfoCodes.CeilingFloorFlats:
                return new CodeForm(title, "The flats every tile shows whose flat plane is 0, by their index in the mapdefs flats.",
                    [new CodeField("Ceiling flat", existing ? value >> 8 : 0, 0, 255, FlatChoices(tiles.Defs.Flats.Ceiling)),
                     new CodeField("Floor flat", existing ? value & 0xff : 0, 0, 255, FlatChoices(tiles.Defs.Flats.Floor))],
                    PicksTile: false, values => CodeAndValue(low, (ushort)(values[0] << 8 | values[1])));

            case MapInfoCodes.TagLink:
                return new CodeForm(title,
                    "A switch on this tile works the tile linked to (and the barriers joined to it). The floor is the floor number the target is on: 255 is this one; the game leaves links to other floors alone.",
                    [new CodeField("Floor", existing ? low : 0xff, 0, 255), .. TargetFields(tiles, existing ? value >> 8 : x, existing ? value & 0xff : y)],
                    PicksTile: true, values => CodeAndValue(values[0], Tile(values[1], values[2])));

            case MapInfoCodes.Value:
                return new CodeForm(title, "The tile after it holds a tile's x and y (a teleporter's: where it takes the player).",
                    TargetFields(tiles, existing ? value >> 8 : x, existing ? value & 0xff : y),
                    PicksTile: true, values => CodeAndValue(low, Tile(values[0], values[1])));
        }

        if (MapInfoCodes.HintText(kind) is { } textName)
        {
            var messages = tiles.Content.HintMessages(textName);
            var choices = messages.Select((message, i) => $"{i + 1}  {message.Replace('\n', ' ')}").ToList();
            return new CodeForm(title, $"The message from {textName} the actors who talk can say about the room it's in (on a wall or door: anywhere).",
                [new CodeField("Message", existing ? low : 1, 1, Math.Max(1, messages.Count), choices)],
                PicksTile: false, values => [(x, y, (ushort)(high | values[0]))]);
        }

        return new CodeForm(title, "Its low byte, read by what the tile is: a teleporter wall's floor (0xF4), a food unit's uses (0xFC), a timer (0xFA).",
            [new CodeField("Low byte", existing ? low : 0, 0, 255)],
            PicksTile: false, values => [(x, y, (ushort)(high | values[0]))]);
    }

    private static IReadOnlyList<CodeField> TargetFields(MapTiles tiles, int x, int y)
        => [new CodeField("Target x", x, 0, Math.Min(255, tiles.Width - 1)), new CodeField("Target y", y, 0, Math.Min(255, tiles.Height - 1))];

    private static List<string> PaletteChoices(MapTiles tiles)
        => tiles.Content.Palette.Select((color, i) => $"{i}  #{color.Red:X2}{color.Green:X2}{color.Blue:X2}").ToList();

    private static List<string> FlatChoices(Dictionary<int, string> flats)
        => flats.Where(flat => flat.Key is >= 0 and <= 255).OrderBy(flat => flat.Key).Select(flat => $"{flat.Key}  {flat.Value}").ToList();

    private static string Capitalized(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>Writes a form's answers as one undo step</summary>
    public static void Apply(MapDocument document, CodeForm form, int[] values, string description)
    {
        using var edit = document.BeginEdit(description);
        foreach (var (x, y, value) in form.Writes(values))
            edit.Set(1, x, y, value);
    }

    /// <summary>
    /// Takes a code off the tile, and its value with it, as one undo step. An enemy's link (its
    /// floor code's) keeps the enemy: only the value east of it goes.
    /// </summary>
    public static void Delete(MapDocument document, MapTiles tiles, int x, int y)
    {
        var valueTile = ValueTile(tiles, x, y);
        bool keepsTile = tiles.MapInfoKind(tiles[1, x, y]) == null && tiles.LinkFrom(x, y) is { Kind: ObjectLinkKind.ActorLink };
        using var edit = document.BeginEdit("Delete code");
        if (valueTile is { } tile && tile != (x, y))
            edit.Set(1, tile.X, tile.Y, 0);
        if (!keepsTile)
            edit.Set(1, x, y, 0);
    }
}
