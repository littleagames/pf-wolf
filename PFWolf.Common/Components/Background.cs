namespace PFWolf.Common.Components;

public record Background : RenderComponent
{
    private Background(byte color)
    {
        Color = color;
        Transform = new Transform(Position.Zero, BoundingBoxType.StretchToScreen);
    }

    public byte Color { get; private set; }

    public static Background Create(byte color)
    {
        return new Background(color);
    }
}
