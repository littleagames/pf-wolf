namespace Wolf3D.Assets;

internal record MapObjectTranslationAsset : Asset
{
    public Dictionary<int, MapActorTranslation> Things { get; internal set; } = new();
    public Dictionary<int, MapTextureTranslation> Walls { get; internal set; } = new();
    public Dictionary<int, MapTextureTranslation> Doors { get; internal set; } = new();
    public Dictionary<int, MapPlayerStartTranslation> PlayerStarts { get; internal set; } = new();
    public Dictionary<int, MapTriggerTranslation> Triggers { get; internal set; } = new();
    public Dictionary<int, MapDiagonalTranslation> Diagonals { get; internal set; } = new();

    public override void Merge(Asset other)
    {
        if (other is MapObjectTranslationAsset otherAsset)
        {
            foreach (var item in otherAsset.Things)
            {
                this.Things[item.Key] = item.Value;
            }

            foreach (var item in otherAsset.Walls)
            {
                this.Walls[item.Key] = item.Value;
            }

            foreach (var item in otherAsset.Doors)
            {
                this.Doors[item.Key] = item.Value;
            }

            foreach (var item in otherAsset.PlayerStarts)
            {
                this.PlayerStarts[item.Key] = item.Value;
            }

            foreach (var item in otherAsset.Triggers)
            {
                this.Triggers[item.Key] = item.Value;
            }

            foreach (var item in otherAsset.Diagonals)
            {
                this.Diagonals[item.Key] = item.Value;
            }
        }
    }
}

/// <summary>
/// An object-plane marker that turns the wall tile under it into a 45 degree wall. The wall
/// keeps its plane 0 texture id; this only gives it a shape (and optionally its diagonal face's
/// texture). A marker on anything but a wall tile is ignored.
/// </summary>
internal record MapDiagonalTranslation
{
    public Enums.WallShape Shape { get; set; }

    /// <summary>The diagonal face's texture. Empty means the wall's own North texture.</summary>
    public string Texture { get; set; } = "";
}

/// <summary>An object-plane tile the player starts the level on, facing <see cref="Angles"/>.</summary>
internal record MapPlayerStartTranslation
{
    /// <summary>0=east, 90=north, 180=west, 270=south, as for <see cref="MapActorTranslation.Angles"/>.</summary>
    public int Angles { get; set; }
}

/// <summary>
/// An object-plane tile the player sets off (e.g. a pushwall, the end-of-castle exit), running
/// <see cref="Action"/> through Entities.MapTriggerRegistry. One-shot: once the action goes off,
/// the tile is cleared.
/// </summary>
internal record MapTriggerTranslation
{
    /// <summary>The action call to run, e.g. `A_PushWall`.</summary>
    public string Action { get; set; } = "";

    /// <summary>
    /// How the player sets it off: "use" (pressing use while facing its tile, the default) or
    /// "walk" (stepping onto its tile).
    /// </summary>
    public string Activation { get; set; } = "use";

    public bool IsWalkOver => Activation.Equals("walk", StringComparison.OrdinalIgnoreCase);

    /// <summary>Counts toward the level's secret ratio (the total on load, found when the action goes off).</summary>
    public bool Secret { get; set; }
}

internal record MapActorTranslation
{
    public string Class { get; set; } = "";
    public int Angles { get; set; }
    public int Patrol { get; set; }
    public int MinSkill { get; set; }
}

internal record MapTextureTranslation
{
    public string North { get; init; } = "";
    public string South { get; init; } = "";
    public string East { get; init; } = "";
    public string West { get; init; } = "";

    /// <summary>
    /// Doors only: the inventory item class (e.g. "GoldKey") the player must carry to open
    /// this door. Empty means unlocked.
    /// </summary>
    public string Lock { get; init; } = "";

    public static MapTextureTranslation None => new(); // TODO: Missing texture
}