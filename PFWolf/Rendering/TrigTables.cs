using PFWolf.Constants;

namespace PFWolf.Rendering;

/// <summary>
/// The fixed-point sine, cosine and fine tangent tables the renderer and movement use, filled
/// once at startup (BuildTables). GameHost has same-named forwards.
/// </summary>
internal static class TrigTables
{
    internal static readonly int[] finetangent = new int[GameConstants.FINEANGLES / 4];
    internal static readonly int[] sintable = new int[GameConstants.ANGLES + GameConstants.ANGLES / 4];
    internal static readonly int[] costable = new int[GameConstants.ANGLES];   // sintable from ANGLES/4 on (filled with it in BuildTables)
    internal const float radtoint = GameConstants.FINEANGLES / 2 / GameConstants.PI;

    internal static void BuildTables()
    {

        //
        // calculate fine tangents
        //

        int i;
        for (i = 0; i < GameConstants.FINEANGLES / 8; i++)
        {
            double tang = Math.Tan((i + 0.5d) / radtoint);
            finetangent[i] = (int)(tang * MapConstants.GLOBAL1);
            finetangent[GameConstants.FINEANGLES / 4 - 1 - i] = (int)((1 / tang) * MapConstants.GLOBAL1);
        }

        //
        // costable overlays sintable with a quarter phase shift
        // ANGLES is assumed to be divisable by four
        //

        float angle = 0;
        float anglestep = (float)(GameConstants.PI / 2 / GameConstants.ANGLEQUAD);
        for (i = 0; i < GameConstants.ANGLEQUAD; i++)
        {
            int value= (int)(MapConstants.GLOBAL1 * Math.Sin(angle));
            sintable[i] = sintable[i + GameConstants.ANGLES] = sintable[GameConstants.ANGLES / 2 - i] = value;
            sintable[GameConstants.ANGLES - i] = sintable[GameConstants.ANGLES / 2 + i] = -value;
            angle += anglestep;
        }
        sintable[GameConstants.ANGLEQUAD] = 65536;
        sintable[3 * GameConstants.ANGLEQUAD] = -65536;
        Array.Copy(sintable, GameConstants.ANGLES / 4, costable, 0, costable.Length);

        //defined(USE_STARSKY) || defined(USE_RAIN) || defined(USE_SNOW)
        //Init3DPoints();
    }
}
