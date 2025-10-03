namespace PFWolf.Common;

public struct Dimension
{
    public Dimension(int width, int height)
    {
        Width = width;
        Height = height;
    }
    public int Width { get; init; }
    public int Height { get; init; }
    public static Dimension Zero => new(0, 0);
}