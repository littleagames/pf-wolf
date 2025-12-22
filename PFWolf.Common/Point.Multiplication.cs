using System.Numerics;

namespace PFWolf.Common;

public partial struct Point
{
    /// <summary>
    /// Multiply Point by Vector2: scales each component and returns a new Point.
    /// </summary>
    /// <param name="point"></param>
    /// <param name="vector"></param>
    /// <returns></returns>
    public static Point operator *(Point point, Vector2 vector)
        => new Point((int)(point.X * vector.X), (int)(point.Y * vector.Y));

    // Allow the Vector2-first order as well.
    public static Point operator *(Vector2 v, Point p) => p * v;
}