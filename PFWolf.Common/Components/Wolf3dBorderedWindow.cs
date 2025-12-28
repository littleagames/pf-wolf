namespace PFWolf.Common.Components;

public record Wolf3dBorderedWindow : RenderComponent
{
    public static Wolf3dBorderedWindow Create(Transform transform, byte backgroundColor, byte topLeftBorderColor, byte bottomRightBorderColor)
        => new Wolf3dBorderedWindow(transform, backgroundColor, topLeftBorderColor, topLeftBorderColor, bottomRightBorderColor, bottomRightBorderColor);
    private Wolf3dBorderedWindow(
        Transform transform,
        byte backgroundColor,
        byte topColor,
        byte leftColor,
        byte bottomColor, 
        byte rightColor)
    {
        Transform = transform;

        AddChildComponent(
            Rectangle.Create(
                backgroundColor,
                transform));

        // Borders
        AddChildComponent(
            Rectangle.Create(
                topColor,
                transform
                    .SetSize(transform.OriginalSize.Width, 1)
            )); // top
        AddChildComponent(
            Rectangle.Create(
                bottomColor,
                transform
                .SetPosition(
                    transform.Position.X,
                    transform.Position.Y + transform.OriginalSize.Height)
                .SetSize(
                    width: transform.OriginalSize.Width,
                    height: 1)
            )); // bottom
        AddChildComponent(
            Rectangle.Create(
                leftColor,
                transform.SetSize(
                    width: 1,
                    height: transform.OriginalSize.Height)
            )); // left
        AddChildComponent(
            Rectangle.Create(
                rightColor,
                transform.SetPosition(
                    transform.Position.X + transform.OriginalSize.Width,
                    transform.Position.Y)
                .SetSize(
                    width: 1,
                    height: transform.OriginalSize.Height)
            )); // right
    }
}
