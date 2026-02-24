namespace Wolf3D;

internal partial class Program
{
    internal const int FRACBITS = 16;

    internal static int FixedMul(int a, int b)
    {
        return (int) (((Int64)a * b + 0x8000) >> FRACBITS);
    }

    internal static int FixedDiv(int a, int b)
    {
        Int64 c = ((Int64)a << FRACBITS) / (Int64)b;

        return (int) c;
    }

    private static void Error(string errorStr)
    {
        SDL2.SDL.SDL_ShowSimpleMessageBox(SDL2.SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_ERROR, "Wolf4SDL", errorStr, IntPtr.Zero);
    }
}
