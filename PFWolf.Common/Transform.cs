namespace PFWolf.Common;

public struct Transform
{
    public Vector2 Position;
    public double Rotation;
    public Dimension Size;
    public PositionalAlignment PositionalAlignment;
    public BoundingBoxType BoundingBoxType;

    public Transform()
    {
        Position = new Vector2(0, 0);
        Rotation = 0.0;
        Size = new Dimension(0, 0);
        PositionalAlignment = PositionalAlignment.TopLeft;
        BoundingBoxType = BoundingBoxType.NoBounds;
    }

    public static Transform Create(Vector2 position)
    {
        return new Transform();
    }
}

public enum PositionalAlignment
{
    TopLeft,
    TopCenter,
    TopRight,
    LeftCenter,
    Center,
    RightCenter,
    BottomLeft,
    BottomCenter,
    BottomRight
}

public enum BoundingBoxType
{
    NoBounds,
    ScaleToScreen,
    StretchToScreen,
    ScaleWidthToScreen,
    ScaleHeightToScreen
}