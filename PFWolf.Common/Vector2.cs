namespace PFWolf.Common;

public struct Vector2
{
    public Vector2(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; init; }
    public int Y { get; init; }

    public static Vector2 Zero => new(0, 0);
}
