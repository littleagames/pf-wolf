namespace PFWolf.Common.Components;

public record Background : RenderComponent
{
    private Background(byte color)
    {
        Color = color;
        Transform = Transform.StretchToScreen();
    }

    public byte Color { get; private set; }

    public static Background Create(byte color)
    {
        return new Background(color);
    }
}
