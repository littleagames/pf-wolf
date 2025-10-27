using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PFWolf.Common.Components;

public record Text : RenderComponent
{
    public string String { get; init; }
    public Transform Transform { get; init; }
    public string Font { get; init; }
    public byte ForeColor { get; private set; }
    public byte BackColor { get; private set; }

    protected Text(string text, Transform transform, string font, byte foreColor)
    {
        String = text;
        Transform = transform;
        Font = font;
        ForeColor = foreColor;
    }

    public void SetColor(byte color)
    {
        ForeColor = color;
    }

    public static Text Create(string text, Transform transform, string font, byte foreColor)
        => new(text, transform, font, foreColor);
}
