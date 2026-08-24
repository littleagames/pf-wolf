using System.Numerics;

namespace Wolf3D.Assets;

internal record MenuAsset : Asset
{
    public string? Music { get; init; }
    public string? Type { get; init; }
    public Vector2? Position { get; init; }
    public int Indent { get; init; } = 0;
    public List<ComponentEntry>? Components { get; init; }
    public List<MenuItemEntry>? MenuItems { get; init; }

    public override void Merge(Asset other)
    {
        // For now, do nothing
    }
}

internal record ComponentEntry
{
    public string? Type { get; init; }
    /// <summary>
    /// Each param entry in YAML is a single-key mapping; represent as a list of dictionaries
    /// so values can be strings or numbers depending on the YAML file.
    /// </summary>
    public List<Dictionary<string, string>> Params { get; init; } = [];
}

internal record MenuItemEntry
{
    public string Type { get; init; } = null!;
    public string Text { get; init; } = null!;
    public string? ShortKey { get; init; } = null;
    public bool Enabled { get; init; } = true;
    public string? Action { get; init; } = null;
}