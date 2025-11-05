namespace PFWolf.Common.Components;

public record Background : RenderComponent
{
    private Background(byte color)
    {
        Color = color;
    }

    public byte Color { get; private set; }

    public static Background Create(byte color)
    {
        return new Background(color);
    }
}
