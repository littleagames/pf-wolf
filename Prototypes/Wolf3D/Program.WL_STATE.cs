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
internal static objstruct SpawnNewObj(uint tilex, uint tiley, statestruct state)
    {
        objstruct newobj = GetNewActor();
        newobj.state = state;
        if (state.tictime != 0)
            newobj.ticcount = (short)(US_RndT() % state.tictime + 1);
        else
            newobj.ticcount = 0;

        newobj.tilex = (byte)tilex;
        newobj.tiley = (byte)tiley;
        newobj.x = (int)((tilex << MapConstants.TILESHIFT) + MapConstants.TILEGLOBAL / 2);
        newobj.y = (int)((tiley << MapConstants.TILESHIFT) + MapConstants.TILEGLOBAL / 2);
        newobj.dir = objdirtypes.nodir;

        _mapManager.actorat[tilex, tiley] = newobj;// (uint)((MAXACTORS - objfreelist) | 0xffff); // TODO: Might be the wrong value
        newobj.areanumber = (byte)(_mapManager.MAPSPOT((int)tilex, (int)tiley, 0) - MapDataConstants.AREATILE);

        return newobj;

    }


    /*
    ===================
    =
    = NewState
    =
    = Changes ob to a new state, setting ticcount to the max for that state
    =
    ===================
    */

    internal static void NewState(objstruct ob, statestruct state)
    {
        ob.state = state;
        ob.ticcount = state.tictime;
    }

    internal static void MoveObj(objstruct ob, int move)
    {
        int deltax, deltay;
        int newx, newy;

        newx = ob.x;
        newy = ob.y;

        switch (ob.dir)
        {
            case objdirtypes.north:
                newy -= move;
                break;
            case objdirtypes.northeast:
                newx += move;
                newy -= move;
                break;
            case objdirtypes.east:
                newx += move;
                break;
            case objdirtypes.southeast:
                newx += move;
                newy += move;
                break;
            case objdirtypes.south:
                newy += move;
                break;
            case objdirtypes.southwest:
                newx -= move;
                newy += move;
                break;
            case objdirtypes.west:
                newx -= move;
                break;
            case objdirtypes.northwest:
                newx -= move;
                newy -= move;
                break;

            case objdirtypes.nodir:
                return;

            default:
                _gameEngineManager.Quit("MoveObj: bad dir!");
                break;
        }

        //
        // check to make sure it's not on top of player
        //
        if (ob.areanumber >= MapDataConstants.NUMAREAS || areabyplayer[ob.areanumber] != 0)
        {
            deltax = Math.Abs(newx - player.x);
            deltay = Math.Abs(newy - player.y);

            if (deltax <= MINACTORDIST && deltay <= MINACTORDIST)
            {
                //
                // TODO: this trick allows guards to get closer to the player
                // until they meet CheckLine, but sometimes it lets them get
                // inside the player and prevent him from moving... Maybe allow
                // the player to move *away* from guards that are on top of him,
                // but not into? That should allow him to back out of a situation
                // where he gets stuck, but not exploit it by moving further into
                // the guard and effectively no-clipping through them...
                //
                if (!ob.hidden || !_mapManager.spotvis[player.tilex, player.tiley])
                {
                    if (ob.obclass == classtypes.ghostobj || ob.obclass == classtypes.spectreobj)
                        TakeDamage((int)(tics * 2), ob);

                    return;
                }
            }
        }

        ob.x = newx;
        ob.y = newy;
        ob.distance -= move;
    }


    internal static bool TryWalk(objstruct ob)
    {
        int doornumtile = -1;

        if (ob.obclass == classtypes.inertobj)
        {
            switch ((objdirtypes)ob.dir)
            {
                case objdirtypes.north:
                    ob.tiley--;
                    break;

                case objdirtypes.northeast:
                    ob.tilex++;
                    ob.tiley--;
                    break;

                case objdirtypes.east:
                    ob.tilex++;
                    break;

                case objdirtypes.southeast:
                    ob.tilex++;
                    ob.tiley++;
                    break;

                case objdirtypes.south:
                    ob.tiley++;
                    break;

                case objdirtypes.southwest:
                    ob.tilex--;
                    ob.tiley++;
                    break;

                case objdirtypes.west:
                    ob.tilex--;
                    break;

                case objdirtypes.northwest:
                    ob.tilex--;
                    ob.tiley--;
                    break;
            }
        }
        else
        {
            switch (ob.dir)
            {
                case objdirtypes.north:
                    if (ob.obclass == classtypes.dogobj || ob.obclass == classtypes.fakeobj)
                    {
                        if (!CHECKDIAG(ob.tilex, ob.tiley - 1))
                            return false;
                    }
                    else
                    {
                        int r = CHECKSIDE(ob, ob.tilex, ob.tiley - 1, ref doornumtile);
                        if (r == 0) return false;
                        if (r == 1) return true;
                    }
                    ob.tiley--;
                    break;

                case objdirtypes.northeast:
                    if (!CHECKDIAG(ob.tilex + 1, ob.tiley - 1)) return false;
                    if (!CHECKDIAG(ob.tilex + 1, ob.tiley)) return false;
                    if (!CHECKDIAG(ob.tilex, ob.tiley - 1)) return false;
                    ob.tilex++;
                    ob.tiley--;
                    break;

                case objdirtypes.east:
                    if (ob.obclass == classtypes.dogobj || ob.obclass == classtypes.fakeobj)
                    {
                        if (!CHECKDIAG(ob.tilex + 1, ob.tiley)) return false;
                    }
                    else
                    {
                        int r = CHECKSIDE(ob, ob.tilex + 1, ob.tiley, ref doornumtile);
                        if (r == 0) return false;
                        if (r == 1) return true;
                    }
                    ob.tilex++;
                    break;

                case objdirtypes.southeast:
                    if (!CHECKDIAG(ob.tilex + 1, ob.tiley + 1)) return false;
                    if (!CHECKDIAG(ob.tilex + 1, ob.tiley)) return false;
                    if (!CHECKDIAG(ob.tilex, ob.tiley + 1)) return false;
                    ob.tilex++;
                    ob.tiley++;
                    break;

                case objdirtypes.south:
                    if (ob.obclass == classtypes.dogobj || ob.obclass == classtypes.fakeobj)
                    {
                        if (!CHECKDIAG(ob.tilex, ob.tiley + 1)) return false;
                    }
                    else
                    {
                        int r = CHECKSIDE(ob, ob.tilex, ob.tiley + 1, ref doornumtile);
                        if (r == 0) return false;
                        if (r == 1) return true;
                    }
                    ob.tiley++;
                    break;

                case objdirtypes.southwest:
                    if (!CHECKDIAG(ob.tilex - 1, ob.tiley + 1)) return false;
                    if (!CHECKDIAG(ob.tilex - 1, ob.tiley)) return false;
                    if (!CHECKDIAG(ob.tilex, ob.tiley + 1)) return false;
                    ob.tilex--;
                    ob.tiley++;
                    break;

                case objdirtypes.west:
                    if (ob.obclass == classtypes.dogobj || ob.obclass == classtypes.fakeobj)
                    {
                        if (!CHECKDIAG(ob.tilex - 1, ob.tiley)) return false;
                    }
                    else
                    {
                        int r = CHECKSIDE(ob, ob.tilex - 1, ob.tiley, ref doornumtile);
                        if (r == 0) return false;
                        if (r == 1) return true;
                    }
                    ob.tilex--;
                    break;

                case objdirtypes.northwest:
                    if (!CHECKDIAG(ob.tilex - 1, ob.tiley - 1)) return false;
                    if (!CHECKDIAG(ob.tilex - 1, ob.tiley)) return false;
                    if (!CHECKDIAG(ob.tilex, ob.tiley - 1)) return false;
                    ob.tilex--;
                    ob.tiley--;
                    break;

                case objdirtypes.nodir:
                    return false;

                default:
                    _gameEngineManager.Quit("Walk: Bad dir");
                    break;
            }
        }

        if (doornumtile != -1)
        {
            OpenDoor(doornumtile);
            ob.distance = -doornumtile - 1;
            return true;
        }

        ob.areanumber = (byte)(_mapManager.MAPSPOT(ob.tilex, ob.tiley, 0) - MapDataConstants.AREATILE);
        ob.distance = (int)MapConstants.TILEGLOBAL;
        return true;
    }

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

    internal static int CHECKSIDE(objstruct ob, int x, int y, ref int doornumtile)
    {
        Actor? temp = _mapManager.actorat[x, y];
        if (temp != null)
        {
            if (temp is Wall)
                return 0;
            if (temp is Door door)
            {
                // DOORCHECK
                if ((demorecord || demoplayback))
                    doornumtile = door.door;//(temp & 63);
                else
                {
                    doornumtile = door.door;//(temp & ~BIT_DOOR);
                    if (ob.obclass != classtypes.ghostobj
                        && ob.obclass != classtypes.spectreobj)
                    {
                        OpenDoor(doornumtile);
                        ob.distance = -doornumtile - 1;
                        return 1;
                    }
                }
            }

            if (temp is objstruct check && check.flags.HasFlag(objflags.FL_SHOOTABLE))
                return 0;
        }

        return 2; // continue;
    }


    // CheckLine/CheckSight/SightPlayer/FirstSighting/SelectDodgeDir/SelectChaseDir/
    // SelectRunDir/KillActor/DamageActor (objstruct versions) were removed here -- all
    // their callers were enemy AI, which now runs on the new Entities.Actors.Actor type
    // (see Program.EnemyAI.cs for the ported equivalents, registered via
    // ActorActionRegistry in Program.WL_AGENT.cs). Nothing left in objlist2 (player,
    // projectiles, the BJ-victory actor) is ever FL_SHOOTABLE, so KillActor/DamageActor
    // had no remaining caller once enemies were gone -- Program.WL_AGENT.cs's
    // GunAttack/KnifeAttack now only need the Entities.Actors.Actor overloads.
    // MoveObj/TryWalk/CHECKDIAG/CHECKSIDE above stay here, still used by BJ Victory.
    internal const long MINSIGHT = 0x18000L;
}
