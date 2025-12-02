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

        // TODO: Child components should inherit scaling from parent
        // TODO: Child component transforms should transform relative to parent?

        AddChildComponent(Rectangle.Create(backgroundColor, new Transform(transform)));

        // Borders
        AddChildComponent(Rectangle.Create(topColor,
            new Transform(
                transform.Position,
                transform.BoundingBox,
                new Dimension(transform.Size.Width, 1))
            )); // top
        AddChildComponent(Rectangle.Create(bottomColor,
            new Transform(
                    new Position(
                        new Vector2(
                        transform.Position.Origin.X,
                        transform.Position.Origin.Y + transform.Size.Height),
                        transform.Position.Alignment,
                        transform.Position.ScaleType),
                    transform.BoundingBox, new Dimension(transform.Size.Width, 1))
            )); // bottom
        AddChildComponent(Rectangle.Create(leftColor,
            new Transform(
                transform.Position,
                transform.BoundingBox,
                new Dimension(1, transform.Size.Height))
            )); // left
        AddChildComponent(Rectangle.Create(rightColor,
            new Transform(
                    new Position(
                        new Vector2(
                        transform.Position.Origin.X + transform.Size.Width,
                        transform.Position.Origin.Y),
                        transform.Position.Alignment,
                        transform.Position.ScaleType),
                    transform.BoundingBox, new Dimension(1, transform.Size.Height))
            )); // right
    }
}
