using System.Numerics;

namespace PFWolf.Common;

public partial struct Point
{
    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; init; }
    public int Y { get; init; }

    public static Point Zero => new(0, 0);

    public readonly override string ToString() => $"({X}, {Y})";
}