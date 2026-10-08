using System.Numerics;

namespace PFWolf.Editor.Rendering;

/// <summary>
/// Where the 3D view looks from. World units are tiles: X runs east along the map's x, Z south
/// along its y, and Y up in stories (the game's eye is at 0.5). Yaw is the game's angle: 0 east,
/// 90 north; pitch is up from level.
/// </summary>
public sealed class Camera3D
{
    private Vector3 _position = new(0.5f, EyeHeight, 0.5f);
    private float _yaw, _pitch;

    public const float EyeHeight = 0.5f;
    public const float FieldOfViewY = 60f;

    public Vector3 Position
    {
        get => _position;
        set
        {
            if (_position == value)
                return;
            _position = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Degrees anticlockwise from east, seen from above (0 east, 90 north), as the game's angles</summary>
    public float Yaw
    {
        get => _yaw;
        set
        {
            var yaw = ((value % 360) + 360) % 360;
            if (_yaw == yaw)
                return;
            _yaw = yaw;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Degrees up (positive) or down from level</summary>
    public float Pitch
    {
        get => _pitch;
        set
        {
            var pitch = Math.Clamp(value, -89f, 89f);
            if (_pitch == pitch)
                return;
            _pitch = pitch;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The camera moved or turned</summary>
    public event EventHandler? Changed;

    /// <summary>The way it looks, level, along the ground</summary>
    public Vector3 FlatForward => new(MathF.Cos(Radians(_yaw)), 0, -MathF.Sin(Radians(_yaw)));

    /// <summary>To its right, along the ground</summary>
    public Vector3 Right => new(MathF.Sin(Radians(_yaw)), 0, MathF.Cos(Radians(_yaw)));

    public Vector3 Forward
    {
        get
        {
            float pitch = Radians(_pitch);
            return FlatForward * MathF.Cos(pitch) + Vector3.UnitY * MathF.Sin(pitch);
        }
    }

    public Matrix4x4 View => Matrix4x4.CreateLookAt(_position, _position + Forward, Vector3.UnitY);

    public static Matrix4x4 Projection(float aspect)
        => Matrix4x4.CreatePerspectiveFieldOfView(Radians(FieldOfViewY), Math.Max(aspect, 0.01f), 0.02f, 400f);

    /// <summary>The ray from the eye through a point on the view, each coordinate -1 (left, bottom) to 1</summary>
    public (Vector3 Origin, Vector3 Direction) Ray(float viewX, float viewY, float aspect)
    {
        float tan = MathF.Tan(Radians(FieldOfViewY) / 2);
        var forward = Forward;
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var up = Vector3.Cross(right, forward);
        var direction = forward + right * (viewX * tan * aspect) + up * (viewY * tan);
        return (_position, Vector3.Normalize(direction));
    }

    /// <summary>Stands on a tile's middle at eye height, facing the game's angle</summary>
    public void PlaceOn(int x, int y, float? yaw = null)
    {
        _position = new Vector3(x + 0.5f, EyeHeight, y + 0.5f);
        if (yaw is { } angle)
            _yaw = ((angle % 360) + 360) % 360;
        _pitch = 0;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Moves by an amount along its own axes: forward along the ground, right, and up</summary>
    public void Move(float forward, float right, float up)
        => Position = _position + FlatForward * forward + Right * right + Vector3.UnitY * up;

    private static float Radians(float degrees) => degrees * MathF.PI / 180;
}
