using System;

namespace PFWolf.Common;

public struct Dimension : IEquatable<Dimension>
{
    public Dimension(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public int Width { get; init; }
    public int Height { get; init; }
    public static Dimension Zero => new(0, 0);

    public readonly bool Equals(Dimension other) =>
        Width == other.Width && Height == other.Height;

    public readonly override bool Equals(object? obj) =>
        obj is Dimension other && Equals(other);

    public readonly override int GetHashCode() =>
        HashCode.Combine(Width, Height);

    public readonly override string ToString() => $"Dimension {{ Width = {Width}, Height = {Height} }}";

    public static bool operator ==(Dimension left, Dimension right) => left.Equals(right);
    public static bool operator !=(Dimension left, Dimension right) => !left.Equals(right);
}