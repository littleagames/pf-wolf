namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                                    PUSHABLE WALLS

    =============================================================================
    */

    static ushort pwallstate;
    static ushort pwallpos;                  // amount a pushable wall has been moved (0-63)
    static ushort pwallx, pwally;
    static byte pwalldir;
    static byte pwalltile;
    static int[][] dirs =[[0,-1],[1,0],[0,1],[-1,0]];
}
