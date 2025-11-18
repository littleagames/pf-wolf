using PFWolf.Common.Components;

namespace PFWolf.Common.Scenes;

public class MainMenuScene : Scene
{
    public override void Start()
    {
        AddComponent(Background.Create(0x29));

        // Stripe
        AddComponent(Rectangle.Create(0x00,
            Transform.ScaleWidth(
                new Position(
                    new Vector2(0, 10),
                    AnchorPosition.TopLeft,
                    ScaleType.Relative),
                height: 24)
            ));
        AddComponent(Rectangle.Create(0x2c,
            Transform.ScaleWidth(
                new Position(
                    new Vector2(0, 32),
                    AnchorPosition.TopLeft,
                    ScaleType.Relative),
                height: 1)));
        // End Stripe

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
