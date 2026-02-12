namespace Wolf3D;

internal static class StringHelpers
{
    internal static char[] ToFixedArray(this string input, int size)
    {
        char[] fixedArray = new char[size];
        int charsToCopy = Math.Min(input.Length, size);
        Array.Copy(input.ToCharArray(), 0, fixedArray, 0, charsToCopy);
        return fixedArray;
    }
}
