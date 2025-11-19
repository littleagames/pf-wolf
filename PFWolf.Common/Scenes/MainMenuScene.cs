using PFWolf.Common.Components;

namespace PFWolf.Common.Scenes;

public class MainMenuScene : Scene
{
    public override void Start()
    {
        AddComponent(Background.Create(0x29));

        AddComponent(Stripe.Create(
            backColor: 0x00,
            stripeColor: 0x2c,
            Transform.ScaleWidth(
                new Position(
                    new Vector2(0, 10),
                    AnchorPosition.TopLeft,
                    ScaleType.Relative),
                height: 24)
            ));

        AddComponent(Graphic.Create("c_options",
            new Transform
            (
                new Position (
                    new Vector2(84, 0),
                    AnchorPosition.TopCenter,
                    ScaleType.Relative),
                BoundingBoxType.Scale)
            ));
    }

    public override void Update()
    {
    }
    
    public override void Destroy()
    {
    }
}
