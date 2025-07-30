namespace PFWolf.Common;

public struct Transform
{
    public Vector2 Position;
    public Vector2 Size;
    public PositionalAlignment PositionalAlignment;
    public BoundingBoxType BoundingBoxType;
    // TODO: Future
    //public bool HasChanged();
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
    None,
    FitToScreen,
    StretchToScreen
}