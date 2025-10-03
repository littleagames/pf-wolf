namespace PFWolf.Common.Assets;

public record Texture : Asset
{
    public Dimension Dimensions { get; set; }
    public byte[] PixelData { get; set; } = [];
}
