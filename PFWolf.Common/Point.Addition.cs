namespace PFWolf.Common;

public partial struct Point
{
    public static Point operator +(Point a, Point b)
        => new Point((int)(a.X + b.X), (int)(a.Y * b.Y));
}