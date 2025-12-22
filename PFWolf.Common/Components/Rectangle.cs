namespace PFWolf.Common.Components;

public record Rectangle : RenderComponent
{
    public byte Color { get; set; }

    public static Rectangle Create(
        byte color,
        Transform transform)
        => new Rectangle(color, transform);

    private Rectangle(byte color, Transform transform)
    {
        Color = color;
        Transform = transform;
    }
}
