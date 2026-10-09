using System.Numerics;

namespace PFWolf.Assets;

public record MenuAsset : Asset
{
    public string? Music { get; init; }
    public string? Type { get; init; }
    public Vector2? Position { get; init; }
    public int Indent { get; init; } = 0;
    public string? ItemsSource { get; init; }
    public int? DefaultSelection { get; init; }
    public List<ComponentEntry>? Components { get; init; }
    public List<MenuItemEntry>? MenuItems { get; init; }
    public int? RowHeight { get; init; }
    public string? Font { get; init; }
    public string? ItemShadow { get; init; }
    public MenuCursorInfo? Cursor { get; init; }
    public bool? SpacerRows { get; init; }
    public MenuPoint? SelectionPic { get; init; }
    public MenuPoint? CheckboxOffset { get; init; }
}

/// <summary>
/// A menu's highlight bar (Blake Stone's), in place of Wolf3D's gun: a bar behind the item the
/// cursor is on, flashing on and off. Colors are color names, #RRGGBB or palette indices.
/// </summary>
public record MenuCursorInfo
{
    /// <summary>The bar's left edge</summary>
    public int X { get; init; }

    /// <summary>How far below an item's top the bar starts</summary>
    public int YOffset { get; init; }

    public int Width { get; init; } = 100;
    public int Height { get; init; } = 9;
    public string Color { get; init; } = "HIGHLIGHT";

    /// <summary>What the bar is erased to: the menu's background</summary>
    public string EraseColor { get; init; } = "BKGDCOLOR";

    /// <summary>Tics the bar stays on, and then off, as it flashes</summary>
    public int FlashTics { get; init; } = 40;
}

public record MenuPoint
{
    public int X { get; init; }
    public int Y { get; init; }
}

public record ComponentEntry
{
    public string? Type { get; init; }
    /// <summary>
    /// Each param entry in YAML is a single-key mapping; represent as a list of dictionaries
    /// so values can be strings or numbers depending on the YAML file.
    /// </summary>
    public List<Dictionary<string, string>> Params { get; init; } = [];
    /// <summary>
    /// Only draw this in these game packs ("wolf3d", "spear"); unset means every pack
    /// </summary>
    public List<string>? GamePacks { get; init; }
}

public record MenuItemEntry
{
    /// <summary>
    /// Stable name code uses to find this item, so the YAML can be reordered safely
    /// </summary>
    public string? Id { get; init; } = null;
    public string Type { get; init; } = null!;
    public string Text { get; init; } = null!;
    public string? ShortKey { get; init; } = null;
    public bool Enabled { get; init; } = true;
    public string? Action { get; init; } = null;
    public string? Music { get; init; } = null;
    /// <summary>
    /// The control a row of the Customize Controls screen binds, by its controls.cfg name ("attack", "am_zoomin")
    /// </summary>
    public string? Control { get; init; } = null;
    /// <summary>
    /// Only include this item in these game packs ("wolf3d", "spear"); unset means every pack
    /// </summary>
    public List<string>? GamePacks { get; init; } = null;
    /// <summary>The presenter text a CP_TextScreen item shows (a VGAGRAPH text)</summary>
    public string? Script { get; init; } = null;
    /// <summary>Drawn in the read-it colors (READCOLOR, READHCOLOR), like Wolf3D's "Read This!"</summary>
    public bool Highlighted { get; init; } = false;
}