namespace PFWolf.Common.Assets;

public record Graphic : Asset
{
    public Graphic()
    {
        Type = AssetType.Graphic;
    }
    public byte[] Data { get; set; } = [];
    public Vector2 Dimensions { get; set; }
}
