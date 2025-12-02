namespace PFWolf.Common;

public record Position : IEquatable<Position>
{
    public Vector2 Origin { get; set; } = Vector2.Zero;
    public Vector2 Offset { get; set; } = Vector2.Zero;

    public AnchorPosition Alignment { get; set; }
    public ScaleType ScaleType { get; set; }

    public Position()
    {
        Origin = Vector2.Zero;
        Offset = Vector2.Zero;
        ScaleType = ScaleType.Relative;
        Alignment = AnchorPosition.TopLeft;
    }

    public Position(Position position)
    {
        Origin = new Vector2(position.Origin.X, position.Origin.Y);
        Offset = new Vector2(position.Offset.X, position.Offset.Y);
        ScaleType = position.ScaleType;
        Alignment = position.Alignment;
    }


    public Position(Vector2 position)
    {
        Origin = new Vector2(position.X, position.Y);
        Offset = Vector2.Zero;
        ScaleType = ScaleType.Relative;
        Alignment = AnchorPosition.TopLeft;
    }

    public Position(Vector2 position, AnchorPosition alignment, ScaleType scaleType)
    {
        Origin = new Vector2(position.X, position.Y);
        Offset = Vector2.Zero;
        ScaleType = scaleType;
        Alignment = alignment;
    }

    public void SetOrigin(Vector2 origin)
    {
        Origin = origin;
    }

    public void SetOffset(Vector2 offset)
    {
        Offset = offset;
    }

    public readonly static Position Zero = new Position();

    //public override bool Equals(Position other) =>
    //    other is not null &&
    //    X == other.X
    //    && Y == other.Y
    //    && Alignment == other.Alignment
    //    && ScaleType == other.ScaleType;

    //public override bool Equals(object? obj) =>
    //    obj is Position other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Origin.X, Origin.Y, Offset.X, Offset.Y, Alignment, ScaleType);

    public override string ToString() => $"Position {{ Origin {Origin}, Offset {Offset}) Alignment = {Alignment}, ScaleType = {ScaleType} }}";

    //public static bool operator ==(Position left, Position right) => left.Equals(right);
    //public static bool operator !=(Position left, Position right) => !left.Equals(right);
}

public record Transform
{
    // Backing fields
    private Position _position;
    private double _rotation;
    private Dimension _size;
    private BoundingBoxType _boundingBoxType;
    private AnchorPosition _boundingBoxAlignment;
    private bool _hasChanged;

    // Public properties with change tracking
    public Position Position
    {
        get => _position;
        set
        {
            if (!value.Equals(_position))
            {
                _position = value;
                _hasChanged = true;
            }
        }
    }

    public double Rotation
    {
        get => _rotation;
        set
        {
            if (value != _rotation)
            {
                _rotation = value;
                _hasChanged = true;
            }
        }
    }

    public Dimension Size
    {
        get => _size;
        set
        {
            if (!value.Equals(_size))
            {
                _size = value;
                _hasChanged = true;
            }
        }
    }

    public BoundingBoxType BoundingBox
    {
        get => _boundingBoxType;
        set
        {
            if (value != _boundingBoxType)
            {
                _boundingBoxType = value;
                _hasChanged = true;
            }
        }
    }

    // Reading HasChanged returns the current value and resets the flag to false.
    public bool HasChanged
    {
        get
        {
            bool value = _hasChanged;
            _hasChanged = false;
            return value;
        }
    }

    public AnchorPosition BoundingBoxAlignment
    {
        get => _boundingBoxAlignment;
        set
        {
            if (value != _boundingBoxAlignment)
            {
                _boundingBoxAlignment = value;
                _hasChanged = true;
            }
        }
    }

    // Constructors assign backing fields directly so HasChanged remains false.
    public Transform()
    {
        _position = new();
        _rotation = 0.0;
        _size = new Dimension(0, 0);
        _boundingBoxType = BoundingBoxType.NoBounds;
        _hasChanged = false;
    }

    public Transform(
        Position position,
        BoundingBoxType boundingBoxType = BoundingBoxType.NoBounds
        )
    {
        _position = position;
        _rotation = 0.0;
        _size = Dimension.Zero;
        _boundingBoxType = boundingBoxType;
        _hasChanged = false;
    }
    public Transform(
        Position position,
        BoundingBoxType boundingBoxType,
        Dimension size)
    {
        _position = new Position(position);
        _rotation = 0.0;
        _size = size;
        _boundingBoxType = boundingBoxType;
        _hasChanged = false;
    }

    public Transform(
        Vector2 position,
        AnchorPosition anchorPosition,
        ScaleType positionScaling,
        BoundingBoxType boundingBoxType = BoundingBoxType.NoBounds)
    {
        _position = new(position, anchorPosition, positionScaling);
        _rotation = 0.0;
        _size = Dimension.Zero;
        _boundingBoxType = boundingBoxType;
        _hasChanged = false;
    }

    public Transform(Transform transform)
    {
        _position = new Position(transform.Position);
        _rotation = transform.Rotation;
        _size = transform.Size;
        _boundingBoxType = transform.BoundingBox;
        _hasChanged = false;
    }
    
    public static Transform ScaleWidth(
        Position position,
        int height)
    {
        return new Transform(
            position,
            BoundingBoxType.ScaleWidthToScreen,
            size: new Dimension(0, height));
    }

    public void Update(Transform transform)
    {
        // Assign via properties so setters perform equality checks and set _hasChanged if needed.
        Position = transform.Position;
        Rotation = transform.Rotation;
        Size = transform.Size;
        BoundingBox = transform.BoundingBox;
    }

    public void Update(Position newPosition, Dimension size)
    {
        this.Position.SetOrigin(newPosition.Origin);
        this.Size = size;
        //this._hasChanged = true;
    }

    public void Update(Dimension size)
    {
        this.Size = size;
        //this._hasChanged = true;
    }

    public void Update(Position position)
    {
        // TODO: this.Position.Update(position);
        this.Position.Origin = position.Origin;
    }
}

public enum AnchorPosition
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

public enum ScaleType
{
    /// <summary>
    /// Takes the value as a base relative to screen size
    /// </summary>
    Relative,
    /// <summary>
    /// Takes the value as the exact pixel position
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