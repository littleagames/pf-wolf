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

    internal static bool CHECKDIAG(int x, int y)
    {
        Actor? temp = _mapManager.actorat[x, y];
        if (temp != null)
        { 
              if (temp is not objstruct)
                  return false;
            if (temp is objstruct check && check.flags.HasFlag(objflags.FL_SHOOTABLE))
                return false;
        }

        return true;
    }

    // SpawnNewObj/NewState/MoveObj/TryWalk/CHECKSIDE and CheckLine/CheckSight/SightPlayer/
    // FirstSighting/SelectDodgeDir/SelectChaseDir/SelectRunDir/KillActor/DamageActor (the
    // objstruct versions) were removed from here -- every actor, including the BJ-victory
    // cutscene, now runs on the new Entities.Actors.Actor type (see Program.EnemyAI.cs for
    // the ported equivalents, registered via ActorActionRegistry in Program.WL_AGENT.cs).
    // Only CHECKDIAG stays: it just inspects actorat[,], so both movement ports share it.
    internal const long MINSIGHT = 0x18000L;
}
