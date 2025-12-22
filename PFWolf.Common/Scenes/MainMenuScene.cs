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
            Transform.ScaleToWidth(
                y: 10,
                height: 24,
                PositionType.Relative,
                VerticalAnchorPoint.Top)
            ));

        AddComponent(Graphic.Create("c_options",
            Transform.Centered(
                x: 84,
                PositionType.Relative,
                VerticalAnchorPoint.Top,
                BoundingBoxType.Scale)
            ));

        //AddComponent(Wolf3dBorderedWindow.Create(
        //    new Transform(
        //        new Position(
        //            new Point(MenuX - 8, MenuY - 3),
        //            AnchorPoint.TopLeft,
        //            PositionType.Relative),
        //        BoundingBoxType.Scale,
        //        new Dimension(MenuWidth, MenuHeight)),
        //    backgroundColor: 0x2d,
        //    topLeftBorderColor: 0x2b,
        //    bottomRightBorderColor: 0x23));

        //AddComponent(Graphic.Create("c_mouselback", 
        //    new Transform(
        //        new Position(   // TODO: Position "bottom center" of screen
        //            new Point(0, 184),
        //            AnchorPoint.TopCenter,
        //            PositionType.Relative),
        //        BoundingBoxType.Scale)));

        //AddComponent(Text.Create(
        //    "New Game",
        //    new Transform(new Position(new Point(MenuX, MenuY), AnchorPoint.TopLeft, PositionType.Relative), BoundingBoxType.Scale),
        //    TextAlignment.Left,
        //    "largefont",
        //    0x13 // active
            
        //    ));
    }

    public override void Update()
    {
    }
    
    public override void Destroy()
    {
    }
}
