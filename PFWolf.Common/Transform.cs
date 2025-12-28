namespace PFWolf.Common;

public record Transform
{
    /// <summary>
    /// X/Y coordinates of the transform's top-left corner from the parent (screen)
    /// </summary>
    public Point Position { get; private set; } = Point.Zero;

    /// <summary>
    /// Location on the transform rectangle where the position offset begins (0,0)
    /// </summary>
    public AnchorPoint AnchorPoint { get; private set; } = AnchorPoint.TopLeft;

    /// <summary>
    /// The X/Y coordinates of the transform's anchor point
    /// </summary>
    public Point Offset { get; private set; } = Point.Zero;

    /// <summary>
    /// Degrees of rotation at the anchor point.
    /// </summary>
    public float Rotation { get; set; }

    /// <summary>
    /// Handles how the position scales with the parent
    /// </summary>
    public PositionType PositionType { get; private set; } = PositionType.Relative;

    /// <summary>
    /// Actual size of the transform
    /// </summary>
    public Dimension OriginalSize { get; private set; } = Dimension.Zero;

    private Dimension screenSize = new();

    /// <summary>
    /// The scale of the transform based on the bounding box type
    /// </summary>
    public Vector2 Scale { get; set; } = Vector2.One;

    /// <summary>
    /// The type of image scaling or stretching
    /// </summary>
    public BoundingBoxType BoundingBox { get; set; } = BoundingBoxType.NoBounds;

    /// <summary>
    /// Anchor setting where the Position X/Y is at 0,0 
    /// </summary>
    public AnchorPoint ScreenAnchorPoint { get; set; } = AnchorPoint.TopLeft;

    public Transform Copy()
        => Common.Transform.Create(
                new Point(this.Position.X, this.Position.Y),
                this.PositionType,
                new Dimension(this.OriginalSize.Width, this.OriginalSize.Height),
                this.AnchorPoint,
                this.BoundingBox,
                this.ScreenAnchorPoint
            );

    public Transform SetScreenSize(int screenWidth, int screenHeight)
    {
        screenSize = new(screenWidth, screenHeight);
        if (PositionType == PositionType.Relative) // TODO: If scale is already set, should this override it? No, I don't think it should
        {
            Scale = new Vector2(screenWidth / 320.0f, screenHeight / 200.0f);
        }

        return this;
    }

    /// <summary>
    /// Gets the position calculated
    /// </summary>
    public Point CalcuatedPosition => CalculatePosition();

    public Point CalculatedOffset => CalculateOffset();

    public Dimension CalculatedSize => CalculateSize();

    public Transform SetPosition(int x, int y)
    {
        return this with
        {
            Position = new Point(x, y)
        };
    }

    public Transform SetPosition(Point newPosition) => SetPosition(newPosition.X, newPosition.Y);

    public Transform SetOffset(int x, int y)
    {
        return this with
        {
            Offset = new Point(x, y)
        };
    }

    public Transform SetOffset(Point newPosition) => SetOffset(newPosition.X, newPosition.Y);

    public Transform SetSize(int width, int height)
    {
        OriginalSize = new Dimension(width, height);
        return this;
    }
    public Transform SetSize(Dimension newSize) => SetSize(newSize.Width, newSize.Height);

    public static Transform Create(
        Point position,
        PositionType positionType,
        Dimension size,
        AnchorPoint anchorPoint,
        BoundingBoxType boundingBox,
        AnchorPoint screenAnchorPoint
    )
    {
        return new Transform
        {
            Position = position,
            PositionType = positionType,
            OriginalSize = size,
            AnchorPoint = anchorPoint,
            BoundingBox = boundingBox,
            ScreenAnchorPoint = screenAnchorPoint
        };
    }

    public static Transform ScaleToWidth(
        int y,
        int height,
        PositionType positionType,
        VerticalAnchorPoint anchorPoint)
    {
        return new Transform
        {
            Position = new Point(0, y),
            PositionType = positionType,
            AnchorPoint = anchorPoint.ToAnchorPoint(),
            BoundingBox = BoundingBoxType.ScaleWidthToScreen,
            ScreenAnchorPoint = AnchorPoint.TopLeft,
            Rotation = 0.0f,
            Offset = Point.Zero,
            OriginalSize = new Dimension(0, height), // Width is unknown at this time
            Scale = new Vector2(1.0f, 1.0f)
        };
    }

    public static Transform ScaleToHeight(
        int x,
        int width,
        PositionType positionType,
        HorizontalAnchorPoint anchorPoint)
    {
        return new Transform
        {
            Position = new Point(x, 0),
            PositionType = positionType,
            AnchorPoint = anchorPoint.ToAnchorPoint(),
            BoundingBox = BoundingBoxType.ScaleHeightToScreen,
            ScreenAnchorPoint = AnchorPoint.TopLeft,
            Rotation = 0.0f,
            Offset = Point.Zero,
            OriginalSize = new Dimension(width, 0), // Width is unknown at this time
            Scale = new Vector2(1.0f, 1.0f)
        };
    }

    public static Transform StretchToScreen()
        => new Transform
        {
            Position = Point.Zero,
            PositionType = PositionType.Absolute,
            AnchorPoint = AnchorPoint.TopLeft,
            BoundingBox = BoundingBoxType.StretchToScreen,
            ScreenAnchorPoint = AnchorPoint.TopLeft,
            Rotation = 0.0f,
            Offset = Point.Zero,
            OriginalSize = Dimension.Zero, // Unknown size at this time
            Scale = new Vector2(1.0f, 1.0f)
        };

    public static Transform Centered(int x, PositionType positionType, VerticalAnchorPoint anchorPoint, BoundingBoxType boundingBox)
        => new Transform
        {
            Position = new Point(x, 0),
            PositionType = positionType,
            AnchorPoint = AnchorPoint.TopCenter,
            BoundingBox = boundingBox,
            ScreenAnchorPoint = AnchorPoint.TopCenter,
            Rotation = 0.0f,
            Offset = Point.Zero,
            OriginalSize = Dimension.Zero, // Unknown size at this time
            Scale = new Vector2(1.0f, 1.0f)
        };

    private Point CalculatePosition()
    {
        // Get the position, scale it, then adjust for anchor point, include the offset as well
        var normalizedPosition = Position * Scale;

        // TODO: This is calculated offset (will need offset + this, as you could have an offset, but if the anchor is topcenter, you can offset 0,0 to 10, 10 from the center)
        // Will need this calculated value and the calculated position
        switch (ScreenAnchorPoint)
        {
            case AnchorPoint.TopCenter:
                return new Point(screenSize.Width / 2, 0) + normalizedPosition;
            case AnchorPoint.TopRight:
                return new Point(screenSize.Width, 0) + normalizedPosition;
            case AnchorPoint.MiddleLeft:
                return new Point(0, screenSize.Height / 2) + normalizedPosition;
            case AnchorPoint.Center:
                return new Point(
                    screenSize.Width / 2,
                    screenSize.Height / 2
                ) + normalizedPosition;
            case AnchorPoint.MiddleRight:
                return new Point(
                    screenSize.Width,
                    screenSize.Height / 2
                ) + normalizedPosition;
            case AnchorPoint.BottomLeft:
                return new Point(0,
                    screenSize.Height
                ) + normalizedPosition;
            case AnchorPoint.BottomCenter:
                return new Point(
                    screenSize.Width / 2,
                    screenSize.Height
                ) +normalizedPosition;
            case AnchorPoint.BottomRight:
                return new Point(
                    screenSize.Width,
                    screenSize.Height
                ) + normalizedPosition;
        }

        return normalizedPosition;
    }

    public Point CalculateOffset()
    {
        var normalizedOffset = this.Offset * this.Scale;
        switch (AnchorPoint)
        {
            case AnchorPoint.TopCenter:
                return new Point(CalculatedSize.Width / 2, 0) + normalizedOffset;
            case AnchorPoint.TopRight:
                return new Point(CalculatedSize.Width, 0) + normalizedOffset;
            case AnchorPoint.MiddleLeft:
                return new Point(0, CalculatedSize.Height / 2) + normalizedOffset;
            case AnchorPoint.Center:
                return new Point(
                    CalculatedSize.Width / 2,
                    CalculatedSize.Height / 2
                ) + normalizedOffset;
            case AnchorPoint.MiddleRight:
                return new Point(
                    CalculatedSize.Width,
                    CalculatedSize.Height / 2
                ) + normalizedOffset;
            case AnchorPoint.BottomLeft:
                return new Point(0,
                    CalculatedSize.Height
                ) + normalizedOffset;
            case AnchorPoint.BottomCenter:
                return new Point(
                    CalculatedSize.Width / 2,
                    CalculatedSize.Height
                ) + normalizedOffset;
            case AnchorPoint.BottomRight:
                return new Point(
                    CalculatedSize.Width,
                    CalculatedSize.Height
                ) + normalizedOffset;
        }

        return normalizedOffset;
    }

    private Dimension CalculateSize()
    {
        switch (BoundingBox)
        {
            case BoundingBoxType.Scale:
                return new Dimension(
                    (int)(OriginalSize.Width * Scale.Min),
                    (int)(OriginalSize.Height * Scale.Min));
            case BoundingBoxType.Stretch:
                return new Dimension(
                    (int)(OriginalSize.Width * Scale.X),
                    (int)(OriginalSize.Height * Scale.Y));
        }

        return OriginalSize;
    }
}

public enum VerticalAnchorPoint
{
    Top,
    Middle,
    Bottom
}

public enum HorizontalAnchorPoint
{
    Left,
    Center,
    Right
}

public enum AnchorPoint
{
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    Center,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

public enum PositionType
{
    /// <summary>
    /// Sets the position origin from a scaled value of the base bounding box (as 320x200)
    /// </summary>
    Relative,
    /// <summary>
    /// Sets the position origin to exact pixel values of the bounding box
    /// </summary>
    Absolute
}

public enum BoundingBoxType
{
    /// <summary>
    /// Retains size, and uses pixel coordinates for location of asset
    /// </summary>
    NoBounds,
    /// <summary>
    /// Scales with the resolution but maintains its aspect ratio
    /// </summary>
    Scale,
    /// <summary>
    /// Scales with both vertical and horizontal and does not maintain aspect ratio
    /// </summary>
    Stretch,
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