using Wolf3D.Constants;
using Wolf3D.Managers;

namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                                GLOBAL VARIABLES

    =============================================================================
    */


    static readonly objdirtypes[] opposite = new objdirtypes[9]
        {objdirtypes.west,objdirtypes.southwest,objdirtypes.south,objdirtypes.southeast,objdirtypes.east,objdirtypes.northeast,objdirtypes.north,objdirtypes.northwest,objdirtypes.nodir};

    static readonly objdirtypes[,] diagonal = new objdirtypes[9, 9]
{
    /* east */  {objdirtypes.nodir,objdirtypes.nodir,objdirtypes.northeast,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.southeast,objdirtypes.nodir,objdirtypes.nodir
},
                {objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir},
    /* north */ { objdirtypes.northeast,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.northwest,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir},
                { objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir},
    /* west */  { objdirtypes.nodir,objdirtypes.nodir,objdirtypes.northwest,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.southwest,objdirtypes.nodir,objdirtypes.nodir},
                { objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir},
    /* south */ { objdirtypes.southeast,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.southwest,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir},
                { objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir},
                { objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir,objdirtypes.nodir}
};

    // actorat[,] only holds walls and doors, so any occupant blocks. (Actors live in
    // MapManager._actors and aren't tracked there, so -- unlike the original -- other
    // actors don't block a diagonal step.)
    internal static bool CHECKDIAG(int x, int y) => _mapManager.actorat[x, y] == null;

    // The per-actor movement, sight and combat code (MoveObj/TryWalk/CHECKSIDE, CheckLine/
    // CheckSight/SightPlayer, SelectChaseDir/SelectDodgeDir, KillActor/DamageActor, ...) lives
    // in Program.EnemyAI.cs, registered via ActorActionRegistry in Program.WL_AGENT.cs. Only
    // CHECKDIAG stays here, since it just inspects actorat[,].
    internal const long MINSIGHT = 0x18000L;
}
