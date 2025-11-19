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

        AddChildComponent(Rectangle.Create(backColor,
            Transform.ScaleWidth(
                new Position(
                    new Vector2(0, 10),
                    AnchorPosition.TopLeft,
                    ScaleType.Relative),
                height: 24)
            ));
        AddChildComponent(Rectangle.Create(stripeColor,
            Transform.ScaleWidth(
                new Position(
                    new Vector2(0, 32),
                    AnchorPosition.TopLeft,
                    ScaleType.Relative),
                height: 1)));
    }
}
