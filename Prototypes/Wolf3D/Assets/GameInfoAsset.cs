namespace Wolf3D.Assets;

internal record GameInfoAsset : Asset
{
    public DefaultMapInfo DefaultMap { get; init; } = new();
    public Dictionary<string, SkillInfo> Skills { get; init; } = [];
    public Dictionary<string, EpisodeInfo> Episodes { get; init; } = [];
    public Dictionary<int, ClusterInfo> Clusters { get; init; } = [];
    public Dictionary<string, MapInfo> Maps { get; init; } = [];
    public List<string> EndStrings { get; init; } = [];

    public override void Merge(Asset other)
    {
        // TODO: Overwrite or merge the data
    }
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