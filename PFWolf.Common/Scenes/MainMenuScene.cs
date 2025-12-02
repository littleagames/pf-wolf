using PFWolf.Common.Components;

namespace PFWolf.Common.Scenes;

public class MainMenuScene : Scene
{
    private const int MenuX = 76;
    private const int MenuY = 55;
    private const int MenuWidth = 178;
    private const int MenuHeight = 13 * 9 + 6;
    private const int MenuIndent = 24;
    private const int LineSpacing = 13;

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

        AddComponent(Wolf3dBorderedWindow.Create(
            new Transform(
                new Position(
                    new Vector2(MenuX - 8, MenuY - 3),
                    AnchorPosition.TopLeft,
                    ScaleType.Relative),
                BoundingBoxType.Scale,
                new Dimension(MenuWidth, MenuHeight)),
            backgroundColor: 0x2d,
            topLeftBorderColor: 0x2b,
            bottomRightBorderColor: 0x23));

        AddComponent(Graphic.Create("c_mouselback", 
            new Transform(
                new Position(   // TODO: Position "bottom center" of screen
                    new Vector2(0, 184),
                    AnchorPosition.TopCenter,
                    ScaleType.Relative),
                BoundingBoxType.Scale)));

        AddComponent(Text.Create(
            "New Game",
            new Transform(new Position(new Vector2(MenuX, MenuY), AnchorPosition.TopLeft, ScaleType.Relative), BoundingBoxType.Scale),
            TextAlignment.Left,
            "largefont",
            0x13 // active
            
            ));
    }

    public override void Update()
    {
    }
    
    public override void Destroy()
    {
    }
}
