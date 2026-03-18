namespace Wolf3D.Mappers;

internal record GameInfoMetadata
{
    public DefaultMapInfo DefaultMap { get; init; } = new();
    public Dictionary<string, SkillInfo> Skills { get; init; } = [];
    public Dictionary<string, EpisodeInfo> Episodes { get; init; } = [];
    public Dictionary<int, ClusterInfo> Clusters { get; init; } = [];
    public Dictionary<string, MapInfo> Maps { get; init; } = [];
}

internal record DefaultMapInfo
{
    public string FloorColor { get; init; } = null!;
    public string CeilingColor { get; init; } = null!;
}

internal record SkillInfo
{
    public string Name { get; init; } = null!;
    public string PicName { get; init; } = null!;

    // Not sure if I want the filtering of things here, or each tile would hold that info
    // or this would be a category that both things listen to a spawnfilters list
    public List<int> SpawnFilter { get; init; } = [];
}

internal record EpisodeInfo
{
    /// <summary>
    /// Text displayed to title the episode
    /// </summary>
    public string Name { get; init; } = null!;

    /// <summary>
    /// Map asset value of which map to start the episode
    /// </summary>
    public string StartMap { get; init; } = null!;

    /// <summary>
    /// Graphic asset that is used to display on the episode menu
    /// </summary>
    public string PicName { get; init; } = null!;

    /// <summary>
    /// Single key press to auto jump to the episode in the menu list
    /// </summary>
    public char Key { get; init; }
}

internal record ClusterInfo
{
    public string EndText { get; init; } = null!;
}

internal record MapInfo
{
    //public string Current { get; set; }
    public string Next { get; init; } = null!;
    public string? SecretNext { get; init; } = null;
    public int FloorNumber { get; init; }
    public int ParTime { get; init; } = 0;
    public string Music { get; init; } = null!;
    public short Cluster { get; init; }

    public string? FloorColor { get; init; } = null;
    public string? CeilingColor { get; init; } = null;
}

internal static class MapInfoMappings
{
    public static Dictionary<string, short> MapAssetToIndex = new Dictionary<string, short>()
    {
        // Episode 1
        { "MAP01", 0 },
        { "MAP02", 1 },
        { "MAP03", 2 },
        { "MAP04", 3 },
        { "MAP05", 4 },
        { "MAP06", 5 },
        { "MAP07", 6 },
        { "MAP08", 7 },
        { "MAP09", 8 },
        { "MAP10", 9 },

        // Episode 2
        { "MAP11", 10 },
        { "MAP12", 11 },
        { "MAP13", 12 },
        { "MAP14", 13 },
        { "MAP15", 14 },
        { "MAP16", 15 },
        { "MAP17", 16 },
        { "MAP18", 17 },
        { "MAP19", 18 },
        { "MAP20", 19 },

        // Episode 3
        { "MAP21", 20 },
        { "MAP22", 21 },
        { "MAP23", 22 },
        { "MAP24", 23 },
        { "MAP25", 24 },
        { "MAP26", 25 },
        { "MAP27", 26 },
        { "MAP28", 27 },
        { "MAP29", 28 },
        { "MAP30", 29 },

        // Episode 4
        { "MAP31", 30 },
        { "MAP32", 31 },
        { "MAP33", 32 },
        { "MAP34", 33 },
        { "MAP35", 34 },
        { "MAP36", 35 },
        { "MAP37", 36 },
        { "MAP38", 37 },
        { "MAP39", 38 },
        { "MAP40", 39 },

        // Episode 5
        { "MAP41", 40 },
        { "MAP42", 41 },
        { "MAP43", 42 },
        { "MAP44", 43 },
        { "MAP45", 44 },
        { "MAP46", 45 },
        { "MAP47", 46 },
        { "MAP48", 47 },
        { "MAP49", 48 },
        { "MAP50", 49 },

        // Episode 6
        { "MAP51", 50 },
        { "MAP52", 51 },
        { "MAP53", 52 },
        { "MAP54", 53 },
        { "MAP55", 54 },
        { "MAP56", 55 },
        { "MAP57", 56 },
        { "MAP58", 57 },
        { "MAP59", 58 },
        { "MAP60", 59 },
    };
}
