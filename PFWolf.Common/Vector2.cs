using System.Numerics;

namespace PFWolf.Common;

public struct Vector2
{
    public Vector2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float X { get; init; }
    public float Y { get; init; }

    public static Vector2 Zero => new(0.0f, 0.0f);
    public static Vector2 One => new(1.0f, 1.0f);

    public float Min => MathF.Min(X, Y);

    public readonly override string ToString() => $"({X}, {Y})";
}