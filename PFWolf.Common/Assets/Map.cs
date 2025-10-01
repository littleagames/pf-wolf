namespace PFWolf.Common.Assets;

public record Map : Asset
{
    public Map()
    {
        Type = AssetType.Map;
    }

    public int Width { get; init; }
    public int Height { get; init; }
    public int NumPlanes { get; init; }
}
