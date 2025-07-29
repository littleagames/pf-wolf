namespace PFWolf.Common;

public record Graphic
{
    public byte[] Data { get; set; } = [];
    public int Width { get; set; }
    public int Height { get; set; }
}
