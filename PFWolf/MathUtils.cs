namespace PFWolf;

internal static class MathUtils
{
    internal const int FRACBITS = 16;

    internal static int FixedMul(int a, int b)
    {
        return (int) (((Int64)a * b + 0x8000) >> FRACBITS);
    }

    /// <summary>
    /// Wolf3D v1.4's FixedByFrac, for demos: sign and magnitude multiplied apart, the fraction
    /// capped just under 1 and the result truncated toward zero, where FixedMul rounds
    /// </summary>
    internal static int FixedByFracOrig(int a, int b)
    {
        bool negative = false;
        if (b == 65536) b = 65535;
        else if (b == -65536) { b = 65535; negative = true; }
        else if (b < 0) { b = -b; negative = true; }

        if (a < 0)
        {
            a = -a;
            negative = !negative;
        }

        var result = (int)(((Int64)a * b) >> FRACBITS);
        return negative ? -result : result;
    }

    internal static int FixedDiv(int a, int b)
    {
        Int64 c = ((Int64)a << FRACBITS) / (Int64)b;

        return (int) c;
    }
}
