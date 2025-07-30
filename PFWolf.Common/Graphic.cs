namespace PFWolf.Common;

public record Graphic
{
    public byte[] Data { get; set; } = [];
    public Vector2 Dimensions { get; set; }

    [Obsolete("Use Dimensions.X")]
    public int Width => Dimensions.X;

    [Obsolete("Use Dimensions.Y")]
    public int Height => Dimensions.Y;
}
