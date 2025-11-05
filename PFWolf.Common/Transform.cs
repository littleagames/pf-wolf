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
    /// <summary>
    /// Retains size, and uses pixel coordinates for location of asset
    /// </summary>
    NoBounds,
    /// <summary>
    /// Scales with the resolution change of both width and height
    /// </summary>
    Scale,
    /// <summary>
    /// Scales with the screen size maxing at whatever border it touches first
    /// </summary>
    ScaleToScreen,
    /// <summary>
    /// Stretches to fit the ScreenWidth and ScreenHeight
    /// </summary>
    StretchToScreen,
    /// <summary>
    /// Scales by the width of the screen only
    /// </summary>
    ScaleWidthToScreen,
    /// <summary>
    /// Scales to the height of the screen only
    /// </summary>
    ScaleHeightToScreen
}