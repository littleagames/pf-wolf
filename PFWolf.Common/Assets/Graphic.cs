namespace PFWolf.Common.Assets;

public record Graphic : Asset
{
    public Graphic()
    {
    }

    public byte[] Data { get; set; } = [];
    public Dimension Size { get; set; } = Dimension.Zero;
    public Vector2 Offset { get; set; } = Vector2.Zero;
}
