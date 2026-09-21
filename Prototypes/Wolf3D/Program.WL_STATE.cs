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

    // A diagonal step is blocked by a wall/door (actorat[,]) or by a living actor on the tile.
    internal static bool CHECKDIAG(int x, int y) =>
        _mapManager.actorat[x, y] == null && !_mapManager.IsShootableActorAt(x, y);

    // The per-actor movement, sight and combat code (MoveObj/TryWalk/CHECKSIDE, CheckLine/
    // CheckSight/SightPlayer, SelectChaseDir/SelectDodgeDir, KillActor/DamageActor, ...) lives
    // in Program.EnemyAI.cs, registered via ActorActionRegistry in Program.WL_AGENT.cs. Only
    // CHECKDIAG stays here, since it isn't tied to a particular actor.
    internal const long MINSIGHT = 0x18000L;
}
