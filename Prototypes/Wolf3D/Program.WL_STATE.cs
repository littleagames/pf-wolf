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
        newobj.x = (int)((tilex << TILESHIFT) + TILEGLOBAL / 2);
        newobj.y = (int)((tiley << TILESHIFT) + TILEGLOBAL / 2);
        newobj.dir = objdirtypes.nodir;

        actorat[tilex, tiley] = newobj;// (uint)((MAXACTORS - objfreelist) | 0xffff); // TODO: Might be the wrong value
        newobj.areanumber = (byte)(MAPSPOT((int)tilex, (int)tiley, 0) - AREATILE);

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
                Quit("MoveObj: bad dir!");
                break;
        }

        //
        // check to make sure it's not on top of player
        //
        if (ob.areanumber >= NUMAREAS || areabyplayer[ob.areanumber] != 0)
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
                if (!ob.hidden || !spotvis[player.tilex, player.tiley])
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
                    Quit("Walk: Bad dir");
                    break;
            }
        }

        if (doornumtile != -1)
        {
            OpenDoor(doornumtile);
            ob.distance = -doornumtile - 1;
            return true;
        }

        ob.areanumber = (byte)(MAPSPOT(ob.tilex, ob.tiley, 0) - AREATILE);
        ob.distance = (int)TILEGLOBAL;
        return true;
    }

    internal static bool CHECKDIAG(int x, int y)
    {
        Actor? temp = actorat[x, y];
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
        Actor? temp = actorat[x, y];
        if (temp != null)
        {
            if (temp is Wall)
                return 0;
            if (temp is Door door)
            {
                // DOORCHECK
                if ((demorecord || demoplayback))
                    doornumtile = (short)door.door;//(temp & 63);
                else
                {
                    doornumtile = (short)door.door;//(temp & ~BIT_DOOR);
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


    /*
    =============================================================================

                                    CHECKSIGHT

    =============================================================================
    */


    /*
    =====================
    =
    = CheckLine
    =
    = Returns true if a straight line between the player and ob is unobstructed
    =
    =====================
    */

    internal static bool CheckLine(objstruct ob)
    {
        int x1, y1, xt1, yt1, x2, y2, xt2, yt2;
        int x, y;
        int xdist, ydist, xstep, ystep;
        int partial, delta;
        int ltemp;
        int xfrac, yfrac, deltafrac;
        uint value, intercept;

        x1 = ob.x >> UNSIGNEDSHIFT;            // 1/256 tile precision
        y1 = ob.y >> UNSIGNEDSHIFT;
        xt1 = x1 >> 8;
        yt1 = y1 >> 8;

        x2 = plux;
        y2 = pluy;
        xt2 = player.tilex;
        yt2 = player.tiley;

        xdist = Math.Abs(xt2 - xt1);

        if (xdist > 0)
        {
            if (xt2 > xt1)
            {
                partial = 256 - (x1 & 0xff);
                xstep = 1;
            }
            else
            {
                partial = x1 & 0xff;
                xstep = -1;
            }

            deltafrac = Math.Abs(x2 - x1);
            delta = y2 - y1;
            ltemp = ((int)delta << 8) / deltafrac;
            if (ltemp > 0x7fffl)
                ystep = 0x7fff;
            else if (ltemp < -0x7fffl)
                ystep = -0x7fff;
            else
                ystep = ltemp;
            yfrac = y1 + (((int)ystep * partial) >> 8);

            x = xt1 + xstep;
            xt2 += xstep;
            do
            {
                y = yfrac >> 8;
                yfrac += ystep;

                value = (uint)tilemap[x, y];
                x += xstep;

                if (value == 0)
                    continue;

                if (value < BIT_DOOR || value > BIT_ALLTILES)
                    return false;

                //
                // see if the door is open enough
                //
                value &= ~(uint)BIT_DOOR;
                intercept = (uint)(yfrac - ystep / 2);

                if (intercept > doorobjlist[value].position)
                    return false;

            } while (x != xt2);
        }

        ydist = Math.Abs(yt2 - yt1);

        if (ydist > 0)
        {
            if (yt2 > yt1)
            {
                partial = 256 - (y1 & 0xff);
                ystep = 1;
            }
            else
            {
                partial = y1 & 0xff;
                ystep = -1;
            }

            deltafrac = Math.Abs(y2 - y1);
            delta = x2 - x1;
            ltemp = ((int)delta << 8) / deltafrac;
            if (ltemp > 0x7fffl)
                xstep = 0x7fff;
            else if (ltemp < -0x7fffl)
                xstep = -0x7fff;
            else
                xstep = ltemp;
            xfrac = x1 + (((int)xstep * partial) >> 8);

            y = yt1 + ystep;
            yt2 += ystep;
            do
            {
                x = xfrac >> 8;
                xfrac += xstep;

                value = (uint)tilemap[x, y];
                y += ystep;

                if (value == 0)
                    continue;

                if (value < BIT_DOOR || value > BIT_ALLTILES)
                    return false;

                //
                // see if the door is open enough
                //
                value &= ~(uint)BIT_DOOR;
                intercept = (uint)(xfrac - xstep / 2);

                if (intercept > doorobjlist[value].position)
                    return false;
            } while (y != yt2);
        }

        return true;
    }


    /*
    ================
    =
    = CheckSight
    =
    = Checks a straight line between player and current object
    =
    = If the sight is ok, check alertness and angle to see if they notice
    =
    = returns true if the player has been spoted
    =
    ================
    */

    internal const long MINSIGHT = 0x18000L;

    internal static bool CheckSight(objstruct ob)
    {
        int deltax, deltay;

        //
        // don't bother tracing a line if the area isn't connected to the player's
        //
        if (ob.areanumber < NUMAREAS && areabyplayer[ob.areanumber] == 0)
            return false;

        //
        // if the player is real close, sight is automatic
        //
        deltax = player.x - ob.x;
        deltay = player.y - ob.y;

        if (deltax > -MINSIGHT && deltax < MINSIGHT
            && deltay > -MINSIGHT && deltay < MINSIGHT)
            return true;

        //
        // see if they are looking in the right direction
        //
        switch (ob.dir)
        {
            case objdirtypes.north:
                if (deltay > 0)
                    return false;
                break;

            case objdirtypes.east:
                if (deltax < 0)
                    return false;
                break;

            case objdirtypes.south:
                if (deltay < 0)
                    return false;
                break;

            case objdirtypes.west:
                if (deltax > 0)
                    return false;
                break;

            // check diagonal moving guards fix

            case objdirtypes.northwest:
                if (!((demorecord || demoplayback)) && deltay > -deltax)
                    return false;
                break;

            case objdirtypes.northeast:
                if (!((demorecord || demoplayback)) && deltay > deltax)
                    return false;
                break;

            case objdirtypes.southwest:
                if (!((demorecord || demoplayback)) && deltax > deltay)
                    return false;
                break;

            case objdirtypes.southeast:
                if (!((demorecord || demoplayback)) && -deltax > deltay)
                    return false;
                break;
        }

        //
        // trace a line to check for blocking tiles (corners)
        //
        return CheckLine(ob);
    }



    internal static bool SightPlayer(objstruct ob)
    {
        if (ob.flags.HasFlag(objflags.FL_ATTACKMODE))
            Quit("An actor in ATTACKMODE called SightPlayer!");

        if (ob.temp2 != 0)
        {
            //
            // count down reaction time
            //
            ob.temp2 -= (short)tics;
            if (ob.temp2 > 0)
                return false;
            ob.temp2 = 0;                                  // time to react
        }
        else
        {
            if (ob.areanumber < NUMAREAS && areabyplayer[ob.areanumber] == 0)
                return false;

            if (ob.flags.HasFlag(objflags.FL_AMBUSH))
            {
                if (!CheckSight(ob))
                    return false;
                ob.flags &= ~objflags.FL_AMBUSH;
            }
            else
            {
                if (!madenoise && !CheckSight(ob))
                    return false;
            }


            switch (ob.obclass)
            {
                case classtypes.guardobj:
                    ob.temp2 = (short)(1 + US_RndT() / 4);
                    break;
                case classtypes.officerobj:
                    ob.temp2 = 2;
                    break;
                case classtypes.mutantobj:
                    ob.temp2 = (short)(1 + US_RndT() / 6);
                    break;
                case classtypes.ssobj:
                    ob.temp2 = (short)(1 + US_RndT() / 6);
                    break;
                case classtypes.dogobj:
                    ob.temp2 = (short)(1 + US_RndT() / 8);
                    break;

                case classtypes.bossobj:
                case classtypes.schabbobj:
                case classtypes.fakeobj:
                case classtypes.mechahitlerobj:
                case classtypes.realhitlerobj:
                case classtypes.gretelobj:
                case classtypes.giftobj:
                case classtypes.fatobj:
                case classtypes.spectreobj:
                case classtypes.angelobj:
                case classtypes.transobj:
                case classtypes.uberobj:
                case classtypes.willobj:
                case classtypes.deathobj:
                    ob.temp2 = 1;
                    break;
            }
            return false;
        }

        FirstSighting(ob);

        return true;
    }

    /*
    ===============
    =
    = FirstSighting
    =
    = Puts an actor into attack mode and possibly reverses the direction
    = if the player is behind it
    =
    ===============
    */

    internal static void FirstSighting(objstruct ob)
    {
        //
        // react to the player
        //
        switch (ob.obclass)
        {
            case classtypes.guardobj:
                PlaySoundLocActor((int)soundnames.HALTSND, ob);
                NewState(ob, s_grdchase1);
                ob.speed *= 3;                 // go faster when chasing player
                break;

            case classtypes.officerobj:
                PlaySoundLocActor((int)soundnames.SPIONSND, ob);
                NewState(ob, s_ofcchase1);
                ob.speed *= 5;                 // go faster when chasing player
                break;

            case classtypes.mutantobj:
                NewState(ob, s_mutchase1);
                ob.speed *= 3;                 // go faster when chasing player
                break;

            case classtypes.ssobj:
                PlaySoundLocActor((int)soundnames.SCHUTZADSND, ob);
                NewState(ob, s_sschase1);
                ob.speed *= 4;                 // go faster when chasing player
                break;

            case classtypes.dogobj:
                PlaySoundLocActor((int)soundnames.DOGBARKSND, ob);
                NewState(ob, s_dogchase1);
                ob.speed *= 2;                 // go faster when chasing player
                break;

            case classtypes.bossobj:
                SD_PlaySound((int)soundnames.GUTENTAGSND);
                NewState(ob, s_bosschase1);
                ob.speed = SPDPATROL * 3;        // go faster when chasing player
                break;

            case classtypes.gretelobj:
                SD_PlaySound((int)soundnames.KEINSND);
                NewState(ob, s_gretelchase1);
                ob.speed *= 3;                 // go faster when chasing player
                break;

            case classtypes.giftobj:
                SD_PlaySound((int)soundnames.EINESND);
                NewState(ob, s_giftchase1);
                ob.speed *= 3;                 // go faster when chasing player
                break;

            case classtypes.fatobj:
                SD_PlaySound((int)soundnames.ERLAUBENSND);
                NewState(ob, s_fatchase1);
                ob.speed *= 3;                 // go faster when chasing player
                break;

            case classtypes.schabbobj:
                SD_PlaySound((int)soundnames.SCHABBSHASND);
                NewState(ob, s_schabbchase1);
                ob.speed *= 3;                 // go faster when chasing player
                break;

            case classtypes.fakeobj:
                SD_PlaySound((int)soundnames.TOT_HUNDSND);
                NewState(ob, s_fakechase1);
                ob.speed *= 3;                 // go faster when chasing player
                break;

            case classtypes.mechahitlerobj:
                SD_PlaySound((int)soundnames.DIESND);
                NewState(ob, s_mechachase1);
                ob.speed *= 3;                 // go faster when chasing player
                break;

            case classtypes.realhitlerobj:
                SD_PlaySound((int)soundnames.DIESND);
                NewState(ob, s_hitlerchase1);
                ob.speed *= 5;                 // go faster when chasing player
                break;

            case classtypes.ghostobj:
                NewState(ob, s_blinkychase1);
                ob.speed *= 2;                 // go faster when chasing player
                break;
        }

        if (ob.distance < 0)
            ob.distance = 0;       // ignore the door opening command

        ob.flags |= (objflags.FL_ATTACKMODE | objflags.FL_FIRSTATTACK);
    }


    /*
    ==================================
    =
    = SelectDodgeDir
    =
    = Attempts to choose and initiate a movement for ob that sends it towards
    = the player while dodging
    =
    = If there is no possible move (ob is totally surrounded)
    =
    = ob->dir           =       nodir
    =
    = Otherwise
    =
    = ob->dir           = new direction to follow
    = ob->distance      = TILEGLOBAL or -doornumber
    = ob->tilex         = new destination
    = ob->tiley
    = ob->areanumber    = the floor tile number (0-(NUMAREAS-1)) of destination
    =
    ==================================
    */

    internal static void SelectDodgeDir(objstruct ob)
    {
        int deltax, deltay, i;
        uint absdx, absdy;
        objdirtypes[] dirtry = new objdirtypes[5];
        objdirtypes tdir;
        objdirtypes turnaround;

        if (ob.flags.HasFlag(objflags.FL_FIRSTATTACK))
        {
            //
            // turning around is only ok the very first time after noticing the
            // player
            //
            turnaround = objdirtypes.nodir;
            ob.flags &= ~objflags.FL_FIRSTATTACK;
        }
        else
            turnaround = opposite[(byte)ob.dir];

        deltax = player.tilex - ob.tilex;
        deltay = player.tiley - ob.tiley;

        //
        // arange 5 direction choices in order of preference
        // the four cardinal directions plus the diagonal straight towards
        // the player
        //

        if (deltax > 0)
        {
            dirtry[1] = objdirtypes.east;
            dirtry[3] = objdirtypes.west;
        }
        else
        {
            dirtry[1] = objdirtypes.west;
            dirtry[3] = objdirtypes.east;
        }

        if (deltay > 0)
        {
            dirtry[2] = objdirtypes.south;
            dirtry[4] = objdirtypes.north;
        }
        else
        {
            dirtry[2] = objdirtypes.north;
            dirtry[4] = objdirtypes.south;
        }

        //
        // randomize a bit for dodging
        //
        absdx = (uint)Math.Abs(deltax);
        absdy = (uint)Math.Abs(deltay);

        if (absdx > absdy)
        {
            tdir = dirtry[1];
            dirtry[1] = dirtry[2];
            dirtry[2] = tdir;
            tdir = dirtry[3];
            dirtry[3] = dirtry[4];
            dirtry[4] = tdir;
        }

        if (US_RndT() < 128)
        {
            tdir = dirtry[1];
            dirtry[1] = dirtry[2];
            dirtry[2] = tdir;
            tdir = dirtry[3];
            dirtry[3] = dirtry[4];
            dirtry[4] = tdir;
        }

        dirtry[0] = diagonal[(byte)dirtry[1], (byte)dirtry[2]];

        //
        // try the directions util one works
        //
        for (i = 0; i < 5; i++)
        {
            if (dirtry[i] == objdirtypes.nodir || dirtry[i] == turnaround)
                continue;

            ob.dir = dirtry[i];
            if (TryWalk(ob))
                return;
        }

        //
        // turn around only as a last resort
        //
        if (turnaround != objdirtypes.nodir)
        {
            ob.dir = turnaround;

            if (TryWalk(ob))
                return;
        }

        ob.dir = objdirtypes.nodir;
    }


    /*
    ============================
    =
    = SelectChaseDir
    =
    = As SelectDodgeDir, but doesn't try to dodge
    =
    ============================
    */

    internal static void SelectChaseDir(objstruct ob)
    {
        int deltax, deltay;
        objdirtypes[] d = new objdirtypes[3];
        objdirtypes tdir, olddir;
        objdirtypes turnaround;


        olddir = ob.dir;
        turnaround = opposite[(byte)olddir];

        deltax = player.tilex - ob.tilex;
        deltay = player.tiley - ob.tiley;

        d[1] = objdirtypes.nodir;
        d[2] = objdirtypes.nodir;

        if (deltax > 0)
            d[1] = objdirtypes.east;
        else if (deltax < 0)
            d[1] = objdirtypes.west;
        if (deltay > 0)
            d[2] = objdirtypes.south;
        else if (deltay < 0)
            d[2] = objdirtypes.north;

        if (Math.Abs(deltay) > Math.Abs(deltax))
        {
            tdir = d[1];
            d[1] = d[2];
            d[2] = tdir;
        }

        if (d[1] == turnaround)
            d[1] = objdirtypes.nodir;
        if (d[2] == turnaround)
            d[2] = objdirtypes.nodir;


        if (d[1] != objdirtypes.nodir)
        {
            ob.dir = d[1];
                if (TryWalk(ob))
                return;     /*either moved forward or attacked*/
        }

        if (d[2] != objdirtypes.nodir)
        {
            ob.dir = d[2];
            if (TryWalk(ob))
                return;
        }

        /* there is no direct path to the player, so pick another direction */

        if (olddir != objdirtypes.nodir)
        {
            ob.dir = olddir;
            if (TryWalk(ob))
                return;
        }

        if (US_RndT() > 128)      /*randomly determine direction of search*/
        {
            for (tdir = objdirtypes.north; tdir <= objdirtypes.west; tdir++)
            {
                if (tdir != turnaround)
                {
                    ob.dir = tdir;
                    if (TryWalk(ob))
                        return;
                }
            }
        }
        else
        {
            for (tdir = objdirtypes.west; tdir >= objdirtypes.north; tdir--)
            {
                if (tdir != turnaround)
                {
                    ob.dir = tdir;
                    if (TryWalk(ob))
                        return;
                }
            }
        }

        if (turnaround != objdirtypes.nodir)
        {
            ob.dir = turnaround;
            if (ob.dir != objdirtypes.nodir)
            {
                if (TryWalk(ob))
                    return;
            }
        }

        ob.dir = objdirtypes.nodir;                // can't move
    }


    /*
    ============================
    =
    = SelectRunDir
    =
    = Run Away from player
    =
    ============================
    */

    internal static void SelectRunDir(objstruct ob)
    {
        int deltax, deltay;
        objdirtypes[] d = new objdirtypes[3];
        objdirtypes tdir;


        deltax = player.tilex - ob.tilex;
        deltay = player.tiley - ob.tiley;

        if (deltax < 0)
            d[1] = objdirtypes.east;
        else
            d[1] = objdirtypes.west;
        if (deltay < 0)
            d[2] = objdirtypes.south;
        else
            d[2] = objdirtypes.north;

        if (Math.Abs(deltay) > Math.Abs(deltax))
        {
            tdir = d[1];
            d[1] = d[2];
            d[2] = tdir;
        }

        ob.dir = d[1];
        if (TryWalk(ob))
            return;     /*either moved forward or attacked*/

        ob.dir = d[2];
        if (TryWalk(ob))
            return;

        /* there is no direct path to the player, so pick another direction */

        if (US_RndT() > 128)      /*randomly determine direction of search*/
        {
            for (tdir = objdirtypes.north; tdir <= objdirtypes.west; tdir++)
            {
                ob.dir = tdir;
                if (TryWalk(ob))
                    return;
            }
        }
        else
        {
            for (tdir = objdirtypes.west; tdir >= objdirtypes.north; tdir--)
            {
                ob.dir = tdir;
                if (TryWalk(ob))
                    return;
            }
        }

        ob.dir = objdirtypes.nodir;                // can't move
    }

    internal static void KillActor(objstruct ob)
    {
        int tilex, tiley;

        tilex = ob.x >> TILESHIFT;         // drop item on center
        tiley = ob.y >> TILESHIFT;

        switch (ob.obclass)
        {
            case classtypes.guardobj:
                GivePoints(100);
                NewState(ob, s_grddie1);
                PlaceItemType((int)wl_stat_types.bo_clip2, tilex, tiley);
                break;

            case classtypes.officerobj:
                GivePoints(400);
                NewState(ob, s_ofcdie1);
                PlaceItemType((int)wl_stat_types.bo_clip2, tilex, tiley);
                break;

            case classtypes.mutantobj:
                GivePoints(700);
                NewState(ob, s_mutdie1);
                PlaceItemType((int)wl_stat_types.bo_clip2, tilex, tiley);
                break;

            case classtypes.ssobj:
                GivePoints(500);
                NewState(ob, s_ssdie1);
                if (gamestate.bestweapon < weapontypes.wp_machinegun)
                    PlaceItemType((int)wl_stat_types.bo_machinegun, tilex, tiley);
                else
                    PlaceItemType((int)wl_stat_types.bo_clip2, tilex, tiley);
                break;

            case classtypes.dogobj:
                GivePoints(200);
                NewState(ob, s_dogdie1);
                break;
            case classtypes.bossobj:
                GivePoints(5000);
                NewState(ob, s_bossdie1);
                PlaceItemType((int)wl_stat_types.bo_key1, tilex, tiley);
                break;

            case classtypes.gretelobj:
                GivePoints(5000);
                NewState(ob, s_greteldie1);
                PlaceItemType((int)wl_stat_types.bo_key1, tilex, tiley);
                break;

            case classtypes.giftobj:
                GivePoints(5000);
                gamestate.killx = player.x;
                gamestate.killy = player.y;
                NewState(ob, s_giftdie1);
                break;

            case classtypes.fatobj:
                GivePoints(5000);
                gamestate.killx = player.x;
                gamestate.killy = player.y;
                NewState(ob, s_fatdie1);
                break;

            case classtypes.schabbobj:
                GivePoints(5000);
                gamestate.killx = player.x;
                gamestate.killy = player.y;
                NewState(ob, s_schabbdie1);
                break;
            case classtypes.fakeobj:
                GivePoints(2000);
                NewState(ob, s_fakedie1);
                break;

            case classtypes.mechahitlerobj:
                GivePoints(5000);
                NewState(ob, s_mechadie1);
                break;
            case classtypes.realhitlerobj:
                GivePoints(5000);
                gamestate.killx = player.x;
                gamestate.killy = player.y;
                NewState(ob, s_hitlerdie1);
                break;
        }

        gamestate.killcount++;
        ob.flags &= ~objflags.FL_SHOOTABLE;
        actorat[ob.tilex, ob.tiley] = null;
        ob.flags |= objflags.FL_NONMARK;
    }

    internal static void DamageActor(objstruct ob, uint damage)
    {
        madenoise = true;

        //
        // do double damage if shooting a non attack mode actor
        //
        if ((ob.flags & objflags.FL_ATTACKMODE) == 0)
            damage <<= 1;

        ob.hitpoints -= (short)damage;

        if (ob.hitpoints <= 0)
            KillActor(ob);
        else
        {
            if (!ob.flags.HasFlag(objflags.FL_ATTACKMODE))
                FirstSighting(ob);             // put into combat mode

            switch ((classtypes)ob.obclass)                // dogs only have one hit point
            {
                case classtypes.guardobj:
                    if ((ob.hitpoints & 1) != 0)
                        NewState(ob, s_grdpain);
                    else
                        NewState(ob, s_grdpain1);
                    break;

                case classtypes.officerobj:
                    if ((ob.hitpoints & 1) != 0)
                        NewState(ob, s_ofcpain);
                    else
                        NewState(ob, s_ofcpain1);
                    break;

                case classtypes.mutantobj:
                    if ((ob.hitpoints & 1) != 0)
                        NewState(ob, s_mutpain);
                    else
                        NewState(ob, s_mutpain1);
                    break;

                case classtypes.ssobj:
                    if ((ob.hitpoints & 1) != 0)
                        NewState(ob, s_sspain);
                    else
                        NewState(ob, s_sspain1);

                    break;
            }
        }
    }

}
