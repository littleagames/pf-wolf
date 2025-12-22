namespace PFWolf.Common.Components;

public record Stripe : RenderComponent
{
    public byte BackColor { get; set; }
    public byte StripeColor { get; set; }

    public static Stripe Create(byte backColor, byte stripeColor, Transform transform)
        => new Stripe(backColor, stripeColor, transform);

    private Stripe(byte backColor, byte stripeColor, Transform transform)
    {
        BackColor = backColor;
        StripeColor = stripeColor;
        Transform = transform;

        AddChildComponent(Rectangle.Create(backColor, transform));

        AddChildComponent(Rectangle.Create(stripeColor,
            transform.SetPosition(
                x: transform.Position.X,
                y: transform.Position.Y + transform.Size.Height - 1)
            .SetSize(
                width: transform.Size.Width,
                height: 1)));
    }
}
