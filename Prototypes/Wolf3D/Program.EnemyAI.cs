using Wolf3D.Constants;
using Wolf3D.Extensions;
using Wolf3D.Managers;

namespace Wolf3D;

// Enemy AI ported from Program.WL_STATE.cs / Program.WL_ACT2.cs to run on the new
// Entities.Actors.Actor type (actordefs/wolf3d/{guards,bosses,ghosts}.yaml). This file is
// deliberately careful to always spell out "Entities.Actors.Actor" in full: a bare "Actor"
// in a file whose namespace is "Wolf3D" resolves to the Wall/Door base class of the same
// name (also declared directly in "Wolf3D"), which wins over any using-directive or alias
// for a same-named type from another namespace. Registered into ActorActionRegistry (see
// WL_AGENT.cs) under the same Think/Action names those YAML files already use.
//
// Projectiles (Rocket/Needle/Fire/Smoke/Boom, actordefs/wolf3d/projectiles.yaml) are on the
// new actor type as well: the boss throw/fire actions below (T_SchabbThrow/T_GiftThrow/
// T_FakeFire) spawn them through MapManager.SpawnAtActor, seeded from the thrower's position,
// and Program.WL_ACT2.cs's T_Projectile/A_Smoke/A_Remove drive them.
internal partial class Program
{
    internal static void NewActorState(Entities.Actors.Actor ob, string stateName)
    {
        if (!ob.ResolvedStates.TryGetValue(stateName, out var state))
            return;
        ob.CurrentState = state;
        ob.TicCount = state.TicTime;
    }

    private static void RecenterOnTile(Entities.Actors.Actor ob)
    {
        ob.X = (int)((ob.TileX << MapConstants.TILESHIFT) + MapConstants.TILEGLOBAL / 2);
        ob.Y = (int)((ob.TileY << MapConstants.TILESHIFT) + MapConstants.TILEGLOBAL / 2);
    }

    /*
    =============================================================================
    MOVEMENT / COLLISION (ported from Program.WL_STATE.cs)
    =============================================================================
    */

    internal static void MoveObj(Entities.Actors.Actor ob, int move)
    {
        int newx = ob.X, newy = ob.Y;

        switch (ob.Dir)
        {
            case objdirtypes.north: newy -= move; break;
            case objdirtypes.northeast: newx += move; newy -= move; break;
            case objdirtypes.east: newx += move; break;
            case objdirtypes.southeast: newx += move; newy += move; break;
            case objdirtypes.south: newy += move; break;
            case objdirtypes.southwest: newx -= move; newy += move; break;
            case objdirtypes.west: newx -= move; break;
            case objdirtypes.northwest: newx -= move; newy -= move; break;
            case objdirtypes.nodir: return;
            default: _gameEngineManager.Quit("MoveObj: bad dir!"); break;
        }

        if (ob.AreaNumber >= MapDataConstants.NUMAREAS || areabyplayer[ob.AreaNumber] != 0)
        {
            var deltax = Math.Abs(newx - player.X);
            var deltay = Math.Abs(newy - player.Y);

            if (deltax <= MINACTORDIST && deltay <= MINACTORDIST)
            {
                if (!ob.Hidden || !_mapManager.spotvis[player.TileX, player.TileY])
                {
                    if (ob.Name is "Blinky" or "Clyde" or "Pinky" or "Inky")
                        TakeDamage((int)(tics * 2), ob);

                    return;
                }
            }
        }

        ob.X = newx;
        ob.Y = newy;
        ob.Distance -= move;
    }

    internal static bool TryWalk(Entities.Actors.Actor ob)
    {
        int doornumtile = -1;
        var cutsCorners = ob.Name is "Dog" or "FakeHitler";

        switch (ob.Dir)
        {
            case objdirtypes.north:
                if (cutsCorners)
                {
                    if (!CHECKDIAG(ob.TileX, ob.TileY - 1)) return false;
                }
                else
                {
                    int r = CHECKSIDE(ob, ob.TileX, ob.TileY - 1, ref doornumtile);
                    if (r == 0) return false;
                    if (r == 1) return true;
                }
                ob.TileY--;
                break;

            case objdirtypes.northeast:
                if (!CHECKDIAG(ob.TileX + 1, ob.TileY - 1)) return false;
                if (!CHECKDIAG(ob.TileX + 1, ob.TileY)) return false;
                if (!CHECKDIAG(ob.TileX, ob.TileY - 1)) return false;
                ob.TileX++; ob.TileY--;
                break;

            case objdirtypes.east:
                if (cutsCorners)
                {
                    if (!CHECKDIAG(ob.TileX + 1, ob.TileY)) return false;
                }
                else
                {
                    int r = CHECKSIDE(ob, ob.TileX + 1, ob.TileY, ref doornumtile);
                    if (r == 0) return false;
                    if (r == 1) return true;
                }
                ob.TileX++;
                break;

            case objdirtypes.southeast:
                if (!CHECKDIAG(ob.TileX + 1, ob.TileY + 1)) return false;
                if (!CHECKDIAG(ob.TileX + 1, ob.TileY)) return false;
                if (!CHECKDIAG(ob.TileX, ob.TileY + 1)) return false;
                ob.TileX++; ob.TileY++;
                break;

            case objdirtypes.south:
                if (cutsCorners)
                {
                    if (!CHECKDIAG(ob.TileX, ob.TileY + 1)) return false;
                }
                else
                {
                    int r = CHECKSIDE(ob, ob.TileX, ob.TileY + 1, ref doornumtile);
                    if (r == 0) return false;
                    if (r == 1) return true;
                }
                ob.TileY++;
                break;

            case objdirtypes.southwest:
                if (!CHECKDIAG(ob.TileX - 1, ob.TileY + 1)) return false;
                if (!CHECKDIAG(ob.TileX - 1, ob.TileY)) return false;
                if (!CHECKDIAG(ob.TileX, ob.TileY + 1)) return false;
                ob.TileX--; ob.TileY++;
                break;

            case objdirtypes.west:
                if (cutsCorners)
                {
                    if (!CHECKDIAG(ob.TileX - 1, ob.TileY)) return false;
                }
                else
                {
                    int r = CHECKSIDE(ob, ob.TileX - 1, ob.TileY, ref doornumtile);
                    if (r == 0) return false;
                    if (r == 1) return true;
                }
                ob.TileX--;
                break;

            case objdirtypes.northwest:
                if (!CHECKDIAG(ob.TileX - 1, ob.TileY - 1)) return false;
                if (!CHECKDIAG(ob.TileX - 1, ob.TileY)) return false;
                if (!CHECKDIAG(ob.TileX, ob.TileY - 1)) return false;
                ob.TileX--; ob.TileY--;
                break;

            case objdirtypes.nodir:
                return false;

            default:
                _gameEngineManager.Quit("Walk: Bad dir");
                break;
        }

        if (doornumtile != -1)
        {
            OpenDoor(doornumtile);
            ob.Distance = -doornumtile - 1;
            ob.SyncPosition();
            return true;
        }

        ob.AreaNumber = (byte)(_mapManager.MAPSPOT(ob.TileX, ob.TileY, 0) - MapDataConstants.AREATILE);
        ob.Distance = (int)MapConstants.TILEGLOBAL;
        ob.SyncPosition();
        return true;
    }

    // CHECKDIAG(int, int) lives in Program.WL_STATE.cs -- it isn't tied to the mover, so
    // there's no Entities.Actors.Actor-typed overload.

    internal static int CHECKSIDE(Entities.Actors.Actor ob, int x, int y, ref int doornumtile)
    {
        var temp = _mapManager.actorat[x, y];
        if (temp != null)
        {
            if (temp is Wall)
                return 0;
            if (temp is Door door)
            {
                if (demorecord || demoplayback)
                    doornumtile = door.door;
                else
                {
                    doornumtile = door.door;
                    // Ghosts phase through closed doors rather than opening them
                    // (Program.WL_STATE.cs's original ghostobj/spectreobj exception).
                    if (ob.Name is not ("Blinky" or "Clyde" or "Pinky" or "Inky"))
                    {
                        OpenDoor(doornumtile);
                        ob.Distance = -doornumtile - 1;
                        return 1;
                    }
                }
            }
        }
        else if (_mapManager.IsShootableActorAt(x, y))
        {
            return 0;       // another living actor is standing there
        }

        return 2; // continue
    }

    internal static bool CheckLine(Entities.Actors.Actor ob)
    {
        int x1, y1, xt1, yt1, x2, y2, xt2, yt2;
        int x, y;
        int xdist, ydist, xstep, ystep;
        int partial, delta;
        int ltemp;
        int xfrac, yfrac, deltafrac;
        uint value, intercept;

        x1 = ob.X >> UNSIGNEDSHIFT;
        y1 = ob.Y >> UNSIGNEDSHIFT;
        xt1 = x1 >> 8;
        yt1 = y1 >> 8;

        x2 = plux;
        y2 = pluy;
        xt2 = player.TileX;
        yt2 = player.TileY;

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

                value = (uint)_mapManager.tilemap[x, y];
                x += xstep;

                if (value == 0)
                    continue;

                if (value < BIT_DOOR || value > BIT_ALLTILES)
                    return false;

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

                value = (uint)_mapManager.tilemap[x, y];
                y += ystep;

                if (value == 0)
                    continue;

                if (value < BIT_DOOR || value > BIT_ALLTILES)
                    return false;

                value &= ~(uint)BIT_DOOR;
                intercept = (uint)(xfrac - xstep / 2);

                if (intercept > doorobjlist[value].position)
                    return false;
            } while (y != yt2);
        }

        return true;
    }

    internal static bool CheckSight(Entities.Actors.Actor ob)
    {
        if (ob.AreaNumber < MapDataConstants.NUMAREAS && areabyplayer[ob.AreaNumber] == 0)
            return false;

        var deltax = player.X - ob.X;
        var deltay = player.Y - ob.Y;

        if (deltax > -MINSIGHT && deltax < MINSIGHT && deltay > -MINSIGHT && deltay < MINSIGHT)
            return true;

        switch (ob.Dir)
        {
            case objdirtypes.north:
                if (deltay > 0) return false;
                break;
            case objdirtypes.east:
                if (deltax < 0) return false;
                break;
            case objdirtypes.south:
                if (deltay < 0) return false;
                break;
            case objdirtypes.west:
                if (deltax > 0) return false;
                break;
            case objdirtypes.northwest:
                if (!(demorecord || demoplayback) && deltay > -deltax) return false;
                break;
            case objdirtypes.northeast:
                if (!(demorecord || demoplayback) && deltay > deltax) return false;
                break;
            case objdirtypes.southwest:
                if (!(demorecord || demoplayback) && deltax > deltay) return false;
                break;
            case objdirtypes.southeast:
                if (!(demorecord || demoplayback) && -deltax > deltay) return false;
                break;
        }

        return CheckLine(ob);
    }

    private static short GetReactionDelay(Entities.Actors.Actor ob) => ob.Name switch
    {
        "Guard" => (short)(1 + US_RndT() / 4),
        "Officer" => 2,
        "Mutant" or "SS" => (short)(1 + US_RndT() / 6),
        "Dog" => (short)(1 + US_RndT() / 8),
        _ => 1, // bosses
    };

    internal static bool SightPlayer(Entities.Actors.Actor ob)
    {
        if (ob.RuntimeFlags.HasFlag(objflags.FL_ATTACKMODE))
            _gameEngineManager.Quit("An actor in ATTACKMODE called SightPlayer!");

        if (ob.Temp2 != 0)
        {
            ob.Temp2 -= (short)tics;
            if (ob.Temp2 > 0)
                return false;
            ob.Temp2 = 0;
        }
        else
        {
            if (ob.AreaNumber < MapDataConstants.NUMAREAS && areabyplayer[ob.AreaNumber] == 0)
                return false;

            if (ob.RuntimeFlags.HasFlag(objflags.FL_AMBUSH))
            {
                if (!CheckSight(ob))
                    return false;
                ob.RuntimeFlags &= ~objflags.FL_AMBUSH;
            }
            else
            {
                if (!madenoise && !CheckSight(ob))
                    return false;
            }

            ob.Temp2 = GetReactionDelay(ob);
            return false;
        }

        FirstSighting(ob);
        return true;
    }

    internal static void FirstSighting(Entities.Actors.Actor ob)
    {
        if (ob.Properties.TryGetValue("seesound", out var seesound) && seesound is string seesoundName)
            PlaySoundLocActor(seesoundName, ob);

        NewActorState(ob, "Chase");

        if (ob.Properties.TryGetValue("monster.chasespeedmultiplier", out var mult))
            ob.Speed *= Convert.ToInt32(mult);

        if (ob.Distance < 0)
            ob.Distance = 0;

        ob.RuntimeFlags |= objflags.FL_ATTACKMODE | objflags.FL_FIRSTATTACK;
    }

    internal static void SelectDodgeDir(Entities.Actors.Actor ob)
    {
        int deltax, deltay, i;
        uint absdx, absdy;
        var dirtry = new objdirtypes[5];
        objdirtypes tdir;
        objdirtypes turnaround;

        if (ob.RuntimeFlags.HasFlag(objflags.FL_FIRSTATTACK))
        {
            turnaround = objdirtypes.nodir;
            ob.RuntimeFlags &= ~objflags.FL_FIRSTATTACK;
        }
        else
            turnaround = opposite[(byte)ob.Dir];

        deltax = player.TileX - ob.TileX;
        deltay = player.TileY - ob.TileY;

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

        absdx = (uint)Math.Abs(deltax);
        absdy = (uint)Math.Abs(deltay);

        if (absdx > absdy)
        {
            tdir = dirtry[1]; dirtry[1] = dirtry[2]; dirtry[2] = tdir;
            tdir = dirtry[3]; dirtry[3] = dirtry[4]; dirtry[4] = tdir;
        }

        if (US_RndT() < 128)
        {
            tdir = dirtry[1]; dirtry[1] = dirtry[2]; dirtry[2] = tdir;
            tdir = dirtry[3]; dirtry[3] = dirtry[4]; dirtry[4] = tdir;
        }

        dirtry[0] = diagonal[(byte)dirtry[1], (byte)dirtry[2]];

        for (i = 0; i < 5; i++)
        {
            if (dirtry[i] == objdirtypes.nodir || dirtry[i] == turnaround)
                continue;

            ob.Dir = dirtry[i];
            if (TryWalk(ob))
                return;
        }

        if (turnaround != objdirtypes.nodir)
        {
            ob.Dir = turnaround;
            if (TryWalk(ob))
                return;
        }

        ob.Dir = objdirtypes.nodir;
    }

    internal static void SelectChaseDir(Entities.Actors.Actor ob)
    {
        int deltax, deltay;
        var d = new objdirtypes[3];
        objdirtypes tdir, olddir;
        objdirtypes turnaround;

        olddir = ob.Dir;
        turnaround = opposite[(byte)olddir];

        deltax = player.TileX - ob.TileX;
        deltay = player.TileY - ob.TileY;

        d[1] = objdirtypes.nodir;
        d[2] = objdirtypes.nodir;

        if (deltax > 0) d[1] = objdirtypes.east;
        else if (deltax < 0) d[1] = objdirtypes.west;
        if (deltay > 0) d[2] = objdirtypes.south;
        else if (deltay < 0) d[2] = objdirtypes.north;

        if (Math.Abs(deltay) > Math.Abs(deltax))
        {
            tdir = d[1]; d[1] = d[2]; d[2] = tdir;
        }

        if (d[1] == turnaround) d[1] = objdirtypes.nodir;
        if (d[2] == turnaround) d[2] = objdirtypes.nodir;

        if (d[1] != objdirtypes.nodir)
        {
            ob.Dir = d[1];
            if (TryWalk(ob)) return;
        }

        if (d[2] != objdirtypes.nodir)
        {
            ob.Dir = d[2];
            if (TryWalk(ob)) return;
        }

        if (olddir != objdirtypes.nodir)
        {
            ob.Dir = olddir;
            if (TryWalk(ob)) return;
        }

        if (US_RndT() > 128)
        {
            for (tdir = objdirtypes.north; tdir <= objdirtypes.west; tdir++)
            {
                if (tdir != turnaround)
                {
                    ob.Dir = tdir;
                    if (TryWalk(ob)) return;
                }
            }
        }
        else
        {
            for (tdir = objdirtypes.west; tdir >= objdirtypes.north; tdir--)
            {
                if (tdir != turnaround)
                {
                    ob.Dir = tdir;
                    if (TryWalk(ob)) return;
                }
            }
        }

        if (turnaround != objdirtypes.nodir)
        {
            ob.Dir = turnaround;
            if (ob.Dir != objdirtypes.nodir)
            {
                if (TryWalk(ob)) return;
            }
        }

        ob.Dir = objdirtypes.nodir;
    }

    internal static void SelectRunDir(Entities.Actors.Actor ob)
    {
        int deltax, deltay;
        var d = new objdirtypes[3];
        objdirtypes tdir;

        deltax = player.TileX - ob.TileX;
        deltay = player.TileY - ob.TileY;

        d[1] = deltax < 0 ? objdirtypes.east : objdirtypes.west;
        d[2] = deltay < 0 ? objdirtypes.south : objdirtypes.north;

        if (Math.Abs(deltay) > Math.Abs(deltax))
        {
            tdir = d[1]; d[1] = d[2]; d[2] = tdir;
        }

        ob.Dir = d[1];
        if (TryWalk(ob)) return;

        ob.Dir = d[2];
        if (TryWalk(ob)) return;

        if (US_RndT() > 128)
        {
            for (tdir = objdirtypes.north; tdir <= objdirtypes.west; tdir++)
            {
                ob.Dir = tdir;
                if (TryWalk(ob)) return;
            }
        }
        else
        {
            for (tdir = objdirtypes.west; tdir >= objdirtypes.north; tdir--)
            {
                ob.Dir = tdir;
                if (TryWalk(ob)) return;
            }
        }

        ob.Dir = objdirtypes.nodir;
    }

    internal static void SelectPathDir(Entities.Actors.Actor ob)
    {
        var spot = (uint)(_mapManager.MAPSPOT(ob.TileX, ob.TileY, 1) - MapDataConstants.ICONARROWS);

        if (spot < 8)
            ob.Dir = (objdirtypes)spot;

        ob.Distance = (int)MapConstants.TILEGLOBAL;

        if (!TryWalk(ob))
            ob.Dir = objdirtypes.nodir;
    }

    /*
    =============================================================================
    COMBAT (ported from Program.WL_STATE.cs)
    =============================================================================
    */

    internal static void KillActor(Entities.Actors.Actor ob)
    {
        var tilex = ob.X >> (int)MapConstants.TILESHIFT;
        var tiley = ob.Y >> (int)MapConstants.TILESHIFT;

        if (ob.Properties.TryGetValue("points", out var points))
            GivePoints(Convert.ToInt32(points));

        NewActorState(ob, "Death");

        if (ob.Name == "SS")
        {
            // KillActor's one enemy-specific drop rule that isn't a flat `dropitem`:
            // upgrades a Clip drop to a MachineGun if the player doesn't have one yet.
            PlaceItemType(GetBestWeapon() < weapontypes.wp_machinegun ? "MachineGun" : "Clip", tilex, tiley);
        }
        else if (ob.Properties.TryGetValue("dropitem", out var dropitem) && dropitem is string dropitemName)
        {
            PlaceItemType(dropitemName, tilex, tiley);
        }

        if (ob.Name is "Schabbs" or "Gift" or "Fat" or "RealHitler")
        {
            gamestate.killx = player.X;
            gamestate.killy = player.Y;
        }

        gamestate.killcount++;
        ob.RuntimeFlags &= ~objflags.FL_SHOOTABLE;
        ob.RuntimeFlags |= objflags.FL_NONMARK;
    }

    internal static void DamageActor(Entities.Actors.Actor ob, uint damage)
    {
        madenoise = true;

        if (!ob.RuntimeFlags.HasFlag(objflags.FL_ATTACKMODE))
            damage <<= 1;

        ob.Hitpoints -= (short)damage;

        if (ob.Hitpoints <= 0)
        {
            KillActor(ob);
            return;
        }

        if (!ob.RuntimeFlags.HasFlag(objflags.FL_ATTACKMODE))
            FirstSighting(ob);

        // Legacy DamageActor alternated between two single-frame Pain variants by hitpoints
        // parity; the new actordefs' "Pain" group instead plays both frames in sequence
        // before falling through to Chase -- close enough visually, simpler to drive.
        if (ob.ResolvedStates.ContainsKey("Pain"))
            NewActorState(ob, "Pain");
    }

    /*
    =============================================================================
    PER-ENEMY THINK/ACTION FUNCTIONS (ported from Program.WL_ACT2.cs)
    =============================================================================
    */

    internal static void T_Stand(Entities.Actors.Actor ob) => SightPlayer(ob);

    internal static void T_Path(Entities.Actors.Actor ob)
    {
        if (SightPlayer(ob))
            return;

        if (ob.Dir == objdirtypes.nodir)
        {
            SelectPathDir(ob);
            if (ob.Dir == objdirtypes.nodir)
                return;
        }

        var move = (int)(ob.Speed * tics);

        while (move != 0)
        {
            if (ob.Distance < 0)
            {
                OpenDoor(-ob.Distance - 1);
                if (doorobjlist[-ob.Distance - 1].action != (byte)dooractiontypes.dr_open)
                    return;
                ob.Distance = (int)MapConstants.TILEGLOBAL;
                if (!(demorecord || demoplayback))
                    TryWalk(ob);
            }

            if (move < ob.Distance)
            {
                MoveObj(ob, move);
                break;
            }

            if (ob.TileX > MapManager.MAPSIZE || ob.TileY > MapManager.MAPSIZE)
                _gameEngineManager.Quit($"T_Path hit a wall at {ob.TileX},{ob.TileY}, dir {ob.Dir}");

            RecenterOnTile(ob);
            move -= ob.Distance;

            SelectPathDir(ob);

            if (ob.Dir == objdirtypes.nodir)
                return;
        }
    }

    internal static void T_Chase(Entities.Actors.Actor ob)
    {
        if (gamestate.victoryflag)
            return;

        var dodge = false;

        if (CheckLine(ob))
        {
            ob.Hidden = false;
            var dx = Math.Abs(ob.TileX - player.TileX);
            var dy = Math.Abs(ob.TileY - player.TileY);
            var dist = dx > dy ? dx : dy;
            int chance;

            if (demorecord || demoplayback)
            {
                chance = (dist == 0 || (dist == 1 && ob.Distance < 0x4000)) ? 300 : (int)((tics << 4) / dist);
            }
            else
            {
                chance = dist != 0 ? (int)((tics << 4) / dist) : 300;

                if (dist == 1)
                {
                    var target = Math.Abs(ob.X - player.X);
                    if (target < 0x14000L)
                    {
                        target = Math.Abs(ob.Y - player.Y);
                        if (target < 0x14000L)
                            chance = 300;
                    }
                }
            }

            if (US_RndT() < chance)
            {
                NewActorState(ob, "Attack");
                return;
            }
            dodge = true;
        }
        else
            ob.Hidden = true;

        if (ob.Dir == objdirtypes.nodir)
        {
            if (dodge) SelectDodgeDir(ob); else SelectChaseDir(ob);
            if (ob.Dir == objdirtypes.nodir)
                return;
        }

        var move = (int)(ob.Speed * tics);

        while (move != 0)
        {
            if (ob.Distance < 0)
            {
                OpenDoor(-ob.Distance - 1);
                if (doorobjlist[-ob.Distance - 1].action != (byte)dooractiontypes.dr_open)
                    return;
                ob.Distance = (int)MapConstants.TILEGLOBAL;
                if (!(demorecord || demoplayback))
                    TryWalk(ob);
            }

            if (move < ob.Distance)
            {
                MoveObj(ob, move);
                break;
            }

            RecenterOnTile(ob);
            move -= ob.Distance;

            if (dodge) SelectDodgeDir(ob); else SelectChaseDir(ob);

            if (ob.Dir == objdirtypes.nodir)
                return;
        }
    }

    internal static void T_DogChase(Entities.Actors.Actor ob)
    {
        if (ob.Dir == objdirtypes.nodir)
        {
            SelectDodgeDir(ob);
            if (ob.Dir == objdirtypes.nodir)
                return;
        }

        var move = (int)(ob.Speed * tics);

        while (move != 0)
        {
            var dx = player.X - ob.X;
            if (dx < 0) dx = -dx;
            dx -= move;
            if (dx <= MINACTORDIST)
            {
                var dy = player.Y - ob.Y;
                if (dy < 0) dy = -dy;
                dy -= move;
                if (dy <= MINACTORDIST)
                {
                    NewActorState(ob, "Attack");
                    return;
                }
            }

            if (move < ob.Distance)
            {
                MoveObj(ob, move);
                break;
            }

            RecenterOnTile(ob);
            move -= ob.Distance;

            SelectDodgeDir(ob);
            if (ob.Dir == objdirtypes.nodir)
                return;
        }
    }

    internal static void T_Bite(Entities.Actors.Actor ob)
    {
        if (ob.Properties.TryGetValue("attacksound", out var sound) && sound is string soundName)
            PlaySoundLocActor(soundName, ob);

        var dx = player.X - ob.X;
        if (dx < 0) dx = -dx;
        dx -= (int)MapConstants.TILEGLOBAL;
        if (dx <= MINACTORDIST)
        {
            var dy = player.Y - ob.Y;
            if (dy < 0) dy = -dy;
            dy -= (int)MapConstants.TILEGLOBAL;
            if (dy <= MINACTORDIST)
            {
                if (US_RndT() < 180)
                    TakeDamage(US_RndT() >> 4, ob);
            }
        }
    }

    internal static void T_Ghosts(Entities.Actors.Actor ob)
    {
        if (ob.Dir == objdirtypes.nodir)
        {
            SelectChaseDir(ob);
            if (ob.Dir == objdirtypes.nodir)
                return;
        }

        var move = (int)(ob.Speed * tics);

        while (move != 0)
        {
            if (move < ob.Distance)
            {
                MoveObj(ob, move);
                break;
            }

            RecenterOnTile(ob);
            move -= ob.Distance;

            SelectChaseDir(ob);
            if (ob.Dir == objdirtypes.nodir)
                return;
        }
    }

    // Shared dodge/retreat AI for Schabbs/Gift/Fat: keeps a shot lined up when possible,
    // otherwise dodges toward the player (or runs, once very close).
    private static void DodgeAndRetreat(Entities.Actors.Actor ob, string attackState)
    {
        var dx = Math.Abs(ob.TileX - player.TileX);
        var dy = Math.Abs(ob.TileY - player.TileY);
        var dist = dx > dy ? dx : dy;
        var dodge = false;

        if (CheckLine(ob))
        {
            ob.Hidden = false;
            if (US_RndT() < (tics << 3))
            {
                NewActorState(ob, attackState);
                return;
            }
            dodge = true;
        }
        else
            ob.Hidden = true;

        if (ob.Dir == objdirtypes.nodir)
        {
            if (dodge) SelectDodgeDir(ob); else SelectChaseDir(ob);
            if (ob.Dir == objdirtypes.nodir)
                return;
        }

        var move = (int)(ob.Speed * tics);

        while (move != 0)
        {
            if (ob.Distance < 0)
            {
                OpenDoor(-ob.Distance - 1);
                if (doorobjlist[-ob.Distance - 1].action != (byte)dooractiontypes.dr_open)
                    return;
                ob.Distance = (int)MapConstants.TILEGLOBAL;
                TryWalk(ob);
            }

            if (move < ob.Distance)
            {
                MoveObj(ob, move);
                break;
            }

            RecenterOnTile(ob);
            move -= ob.Distance;

            if (dist < 4) SelectRunDir(ob);
            else if (dodge) SelectDodgeDir(ob);
            else SelectChaseDir(ob);

            if (ob.Dir == objdirtypes.nodir)
                return;
        }
    }

    internal static void T_Schabb(Entities.Actors.Actor ob) => DodgeAndRetreat(ob, "Attack");
    internal static void T_Gift(Entities.Actors.Actor ob) => DodgeAndRetreat(ob, "Attack");
    internal static void T_Fat(Entities.Actors.Actor ob) => DodgeAndRetreat(ob, "Attack");

    internal static void T_Fake(Entities.Actors.Actor ob)
    {
        if (CheckLine(ob))
        {
            ob.Hidden = false;
            if (US_RndT() < (tics << 1))
            {
                NewActorState(ob, "Attack");
                return;
            }
        }
        else
            ob.Hidden = true;

        if (ob.Dir == objdirtypes.nodir)
        {
            SelectDodgeDir(ob);
            if (ob.Dir == objdirtypes.nodir)
                return;
        }

        var move = (int)(ob.Speed * tics);

        while (move != 0)
        {
            if (move < ob.Distance)
            {
                MoveObj(ob, move);
                break;
            }

            RecenterOnTile(ob);
            move -= ob.Distance;

            SelectDodgeDir(ob);
            if (ob.Dir == objdirtypes.nodir)
                return;
        }
    }

    internal static void T_Shoot(Entities.Actors.Actor ob)
    {
        var hitchance = 128;

        if (ob.AreaNumber < MapDataConstants.NUMAREAS && areabyplayer[ob.AreaNumber] == 0)
            return;

        if (CheckLine(ob))
        {
            var dx = Math.Abs(ob.TileX - player.TileX);
            var dy = Math.Abs(ob.TileY - player.TileY);
            var dist = dx > dy ? dx : dy;

            if (ob.Properties.ContainsKey("monster.sharpshooter"))
                dist = dist * 2 / 3;

            if (thrustspeed >= RUNSPEED)
                hitchance = ob.RuntimeFlags.HasFlag(objflags.FL_VISABLE) ? 160 - dist * 16 : 160 - dist * 8;
            else
                hitchance = ob.RuntimeFlags.HasFlag(objflags.FL_VISABLE) ? 256 - dist * 16 : 256 - dist * 8;

            if (US_RndT() < hitchance)
            {
                int damage = dist < 2 ? US_RndT() >> 2 : dist < 4 ? US_RndT() >> 3 : US_RndT() >> 4;
                TakeDamage(damage, ob);
            }
        }

        // Mirrors the original per-obclass sound switch in T_Shoot (Program.WL_ACT2.cs) --
        // NOT the same as each actor's own "attacksound" property, since a few enemies
        // (Gift/Fat/Schabbs/FakeHitler) use a different sound for their thrown/breathed
        // attack than for this generic gunshot roll.
        var sound = ob.Name switch
        {
            "SS" => "wolfss/attack",
            "Gift" or "Fat" => "missile/fire",
            "Hans" => "hans/attack",
            "MechaHitler" or "RealHitler" => "hitler/attack",
            "Schabbs" => "schabbs/throw",
            "FakeHitler" => "flame/fire",
            _ => "guard/attack", // Guard/Officer/Mutant/Gretel: generic soldier gunfire
        };
        PlaySoundLocActor(sound, ob);
    }

    // Spawns a projectile actor (Needle/Rocket/Fire, actordefs/wolf3d/projectiles.yaml) at the
    // thrower and aims it at the player. TicCount 1 makes its first frame expire on the very
    // next tic, so the state's Action (a rocket's first A_Smoke) fires almost immediately.
    private static void ThrowProjectile(Entities.Actors.Actor ob, string className, int speed, string sound)
    {
        var deltax = player.X - ob.X;
        var deltay = ob.Y - player.Y;
        var angle = (float)Math.Atan2((float)deltay, (float)deltax);
        if (angle < 0) angle = (float)(M_PI * 2 + angle);
        var iangle = (int)(angle / (M_PI * 2) * ANGLES);

        var newobj = _mapManager.SpawnAtActor(className, ob);
        if (newobj == null)
            return;

        newobj.TicCount = 1;
        newobj.Angle = (short)iangle;
        newobj.Speed = speed;

        PlaySoundLocActor(sound, newobj);
    }

    internal static void T_SchabbThrow(Entities.Actors.Actor ob) =>
        ThrowProjectile(ob, "Needle", 0x2000, "schabbs/throw");

    internal static void T_GiftThrow(Entities.Actors.Actor ob) =>
        ThrowProjectile(ob, "Rocket", 0x2000, "missile/fire");

    internal static void T_FakeFire(Entities.Actors.Actor ob) =>
        ThrowProjectile(ob, "Fire", 0x1200, "flame/fire");

    internal static void A_DeathScream(Entities.Actors.Actor ob)
    {
        if (ob.Properties.TryGetValue("deathsound", out var sound) && sound is string soundName)
            PlaySoundLocActor(soundName, ob);
    }

    internal static void A_MechaSound(Entities.Actors.Actor ob)
    {
        if (ob.AreaNumber >= MapDataConstants.NUMAREAS || areabyplayer[ob.AreaNumber] != 0)
            PlaySoundLocActor("hitler/active", ob);
    }

    internal static void A_Slurpie(Entities.Actors.Actor ob) => _audioManager.Play("misc/slurpie");

    internal static void A_HitlerMorph(Entities.Actors.Actor ob)
    {
        var newActor = _mapManager.SpawnMorphedEnemy("RealHitler", ob);
        if (newActor == null)
            return;

        newActor.Speed = SPDPATROL * 5;

        if (!loadedgame)
            gamestate.killtotal++;
    }

    internal static void A_StartDeathCam(Entities.Actors.Actor ob)
    {
        var language = _assetManager.GetText("en-us");
        int dx, dy;
        float fangle;
        int xmove, ymove;
        int dist;

        _videoManager.FinishPaletteShifts();

        GameEngineManager.WaitVBL(100);

        if (gamestate.victoryflag)
        {
            playstate = playstatetypes.ex_victorious;
            return;
        }

        gamestate.victoryflag = true;
        uint fadeheight = (uint)(viewsize != 21 ? _videoManager.screenHeight - _videoManager.scaleFactor * STATUSLINES : _videoManager.screenHeight);
        _videoManager.BarScaledCoord(0, 0, _videoManager.screenWidth, (int)fadeheight, bordercol);
        _videoManager.FizzleFade(0, 0, (uint)_videoManager.screenWidth, fadeheight, 70, false);

        if (bordercol != "VIEWCOLOR")
        {
            fontnumber = "LargeFont";
            SETFONTCOLOR("White", bordercol);
            PrintX = 68; PrintY = 45;
            US_Print("$STR_SEEAGAIN".ToLanguageText(language));
        }
        else
        {
            Write(0, 7, "$STR_SEEAGAIN".ToLanguageText(language));
        }

        _videoManager.Update();

        _inputManager.UserInput(300);

        NewActorState(player, Entities.Actors.PlayerPawn.DeathCamState);

        player.X = gamestate.killx;
        player.Y = gamestate.killy;

        dx = ob.X - player.X;
        dy = player.Y - ob.Y;

        fangle = (float)Math.Atan2((float)dy, (float)dx);
        if (fangle < 0)
            fangle = (float)(M_PI * 2 + fangle);

        player.Angle = (short)(fangle / (M_PI * 2) * ANGLES);

        dist = 0x14000;
        do
        {
            xmove = MathUtils.FixedMul(dist, costable[player.Angle]);
            ymove = -MathUtils.FixedMul(dist, sintable[player.Angle]);

            player.X = ob.X - xmove;
            player.Y = ob.Y - ymove;
            dist += 0x1000;

        } while (!CheckPosition(player));
        plux = (ushort)(player.X >> UNSIGNEDSHIFT);
        pluy = (ushort)(player.Y >> UNSIGNEDSHIFT);
        player.TileX = (byte)(player.X >> MapConstants.TILESHIFT);
        player.TileY = (byte)(player.Y >> MapConstants.TILESHIFT);

        DrawPlayBorder();

        fizzlein = true;

        if (ob.ResolvedStates.ContainsKey("DeathCam"))
            NewActorState(ob, "DeathCam");
    }
}
