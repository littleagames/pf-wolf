namespace PFWolf.Common.Assets;

public record Texture : Asset
{
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] PixelData { get; set; } = [];
}
