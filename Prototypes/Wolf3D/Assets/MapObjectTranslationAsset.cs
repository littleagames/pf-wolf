namespace Wolf3D.Assets;

internal record MapObjectTranslationAsset : Asset
{
    public Dictionary<int, MapActorTranslation> Things { get; internal set; } = new();
    public Dictionary<int, MapTextureTranslation> Walls { get; internal set; } = new();
    public Dictionary<int, MapTextureTranslation> Doors { get; internal set; } = new();

    // TODO:
    // Player
    // Enemies

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
        }
    }
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

    public static MapTextureTranslation None => new(); // TODO: Missing texture
}