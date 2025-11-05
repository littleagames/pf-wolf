namespace PFWolf.Common.Components;

public record Text : RenderComponent
{
    public string String { get; init; }
    public Transform Transform { get; init; }
    public TextAlignment Alignment { get; init; } = TextAlignment.Left;
    public string Font { get; init; }
    public byte ForeColor { get; private set; }
    public byte BackColor { get; private set; }

    protected Text(
        string text,
        Transform transform,
        TextAlignment textAlignment,
        string font,
        byte foreColor)
    {
        String = text;
        Transform = transform;
        Alignment = textAlignment;
        Font = font;
        ForeColor = foreColor;
    }

    public void SetColor(byte color)
    {
        ForeColor = color;
    }

    public static Text Create(string text, Transform transform, TextAlignment alignment, string font, byte foreColor)
        => new(text, transform, alignment, font, foreColor);
}
