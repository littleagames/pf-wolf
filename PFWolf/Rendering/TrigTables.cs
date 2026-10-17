using PFWolf.Constants;

namespace PFWolf.Rendering;

/// <summary>
/// The fixed-point sine, cosine and fine tangent tables the renderer and movement use, filled
/// once at startup (BuildTables). GameHost has same-named forwards.
/// </summary>
internal static class TrigTables
{
    internal static readonly int[] finetangent = new int[GameConstants.FineAngles / 4];
    internal static readonly int[] sintable = new int[GameConstants.Angles + GameConstants.Angles / 4];
    internal static readonly int[] costable = new int[GameConstants.Angles];   // sintable from Angles/4 on (filled with it in BuildTables)
    internal const float radtoint = GameConstants.FineAngles / 2 / GameConstants.Pi;

    internal static void BuildTables()
    {

        //
        // calculate fine tangents
        //

        int i;
        for (i = 0; i < GameConstants.FineAngles / 8; i++)
        {
            double tang = Math.Tan((i + 0.5d) / radtoint);
            finetangent[i] = (int)(tang * MapConstants.Global1);
            finetangent[GameConstants.FineAngles / 4 - 1 - i] = (int)((1 / tang) * MapConstants.Global1);
        }

        //
        // costable overlays sintable with a quarter phase shift
        // Angles is assumed to be divisable by four
        //

        float angle = 0;
        float anglestep = (float)(GameConstants.Pi / 2 / GameConstants.AngleQuad);
        for (i = 0; i < GameConstants.AngleQuad; i++)
        {
            int value= (int)(MapConstants.Global1 * Math.Sin(angle));
            sintable[i] = sintable[i + GameConstants.Angles] = sintable[GameConstants.Angles / 2 - i] = value;
            sintable[GameConstants.Angles - i] = sintable[GameConstants.Angles / 2 + i] = -value;
            angle += anglestep;
        }
        sintable[GameConstants.AngleQuad] = 65536;
        sintable[3 * GameConstants.AngleQuad] = -65536;
        Array.Copy(sintable, GameConstants.Angles / 4, costable, 0, costable.Length);

        //defined(USE_STARSKY) || defined(USE_RAIN) || defined(USE_SNOW)
        //Init3DPoints();
    }
}
