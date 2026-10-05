using Wolf3D.Constants;
using Wolf3D.Extensions;

namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                                BLAKE STONE AI

    Blake Stone's enemies' ways, as bstone has them (3d_act2.cpp, 3d_state.cpp, 3d_agent.cpp),
    for any actor to use.

    Thinks and actions (actordefs `think:` / `action:`):
      T_BlakeChase   chase: closes in dodging, shoots only while it has ammo, and (with
                     `monster.shootmode`) takes turns at shooting and closing in
      T_BlakeShoot   a hitscan shot; SMART actors use up their ammo and alert the area
      T_BlowBack     on Death frames: the body slides back from the shot that killed it
      T_Wounded      on a held Wounded frame: lies there, then gets up (Recover) once the player
                     is more than a tile away

    Flags:
      FRIENDLY       starts friendly: patrolling, it only looks for the player after a noise
      INFORMANT      never goes after the player, isn't a kill, and gives hints when talked to
      TALKATIVE      can be talked to (use, facing it, close by)
      SMART          runs for supplies or a door while out of ammo, at half health or less, or
                     after turning mean when talked to (rent-a-cops, pro guards, scientists)
      PATROLTURNS    a patroller blocked turns aside rather than stopping
      RANDOMTURN     ... and sometimes turns at random
      NOLOCKEDDOORS  doesn't go through doors that need a key
      STEPBACK       walking into the player, turns back to the tile it came from
      STATIONARY     never moves
      CHASEDIR       closes in straight, without dodging

    Properties:
      monster.ammo          shots it starts with (out of them, a SMART actor runs)
      monster.shootmode     takes turns at shooting and closing in, as Blake Stone's aliens
      monster.painattack    hurt, the chance in 256 it shoots straight back
      monster.woundstages   [n, ...]: a list to pick its wound stages from; each 1/(n+1) of its
                            health lost puts it down wounded (a 0xFA value east of it on the
                            object plane picks instead)
      monster.healthgain / monster.ammogain (on an item): a SMART actor standing on it takes it
      talk.*                what it says when talked to (TryInterrogate)

    =============================================================================
    */

    // Why a SMART actor runs (CheckRunChase)
    const int RR_AMMO = 1, RR_HEALTH = 2, RR_INTERROGATED = 4;

    // How much faster a SMART actor runs than it chases
    const int RUNAWAY_SPEED = 1000;

    // How far a corpse slides back each tic (T_BlowBack)
    const int SLIDE_SPEED = 0x2000;

    // How far a body killed by anything but the player's own gun slides
    const int DEFAULT_KNOCKBACK = 0x5000;

    /// <summary>Whether the weapon in hand is a silent one (weapon.silent): its shots don't alert anyone</summary>
    static bool PlayerWeaponIsSilent() =>
        gamestate.weapon != null && _inventoryManager.GetProperty(gamestate.weapon, "weapon.silent") is { } silent
        && (silent is true || bool.TryParse(silent.ToString(), out var b) && b);

    /// <summary>
    /// Sets up what's Blake Stone's about a freshly spawned actor (MapManager.SpawnThing): its
    /// ammo, its friendliness, an informant's gifts, and a wounding actor's wound stages
    /// </summary>
    internal static void InitSpawnedActor(Entities.Actors.Actor ob, int tilex, int tiley)
    {
        // One number, or [min, max] for one at random
        var ammo = ob.PropertyInts("monster.ammo");
        ob.Ammo = (short)(ammo.Count == 0 ? 1 : ammo.Count == 1 ? ammo[0] : ammo[0] + US_RndT() % (ammo[1] - ammo[0] + 1));
        if (ob.HasFlag("FRIENDLY"))
            ob.RuntimeFlags |= objflags.FL_FRIENDLY;

        // The 0xFA value east of it on the object plane, if there is one
        int east = tilex + 1 < MapManager.MAPSIZE ? _mapManager.MAPSPOT(tilex + 1, tiley, 1) : 0;
        int? mapValue = (east & 0xff00) == 0xfa00 ? east & 0xff : null;

        // A sleeper's wake delay (T_WaitToWake), in tics: the map's value in seconds, else a
        // random [min, max] seconds; 0 wakes it only when shot, 255 never
        var wake = ob.PropertyInts("monster.wakedelay");
        if (wake.Count > 0)
        {
            int seconds = mapValue ?? (wake.Count == 1 ? wake[0] : wake[0] + US_RndT() % (wake[1] - wake[0] + 1));
            ob.Temp2 = ob.Temp3 = (short)(seconds * 60);
            if (seconds == 255)
                ob.RuntimeFlags &= ~objflags.FL_SHOOTABLE;
        }

        if (ob.HasFlag("INFORMANT"))
        {
            ob.RuntimeFlags |= objflags.FL_HASAMMO | objflags.FL_HASTOKENS;
            ob.SeekX = ob.SeekY = 0xff;     // no hint chosen yet
        }

        var stages = ob.PropertyInts("monster.woundstages");
        if (stages.Count > 0)
            ob.Temp1 = (short)(mapValue ?? stages[US_RndT() % stages.Count]);

        // `monster.floorhealth: [floor, times]`: that much tougher on that floor (game-info
        // floor-number; Goldfire on floor 9)
        if (ob.PropertyInts("monster.floorhealth") is [var floor, var times] && floor == _mapManager.CurrentFloorNumber)
            ob.Hitpoints = (short)Math.Min(ob.Hitpoints * times, short.MaxValue);
    }

    /*
    =============================================================================

                        SLEEPERS AND WHAT THEY BECOME

    A canister alien, a mutant asleep on a gurney, a pod egg: each stands in for the enemy it
    becomes (`monster.becomes`), which is what the level's kill total counts, so its own death
    isn't a kill and the enemy coming out of it isn't added to the total again.

    =============================================================================
    */

    /// <summary>
    /// A_SpawnEnemy("ScanAlien"[, "chase"]): an enemy appears on this actor's tile, as if placed
    /// by the map but not added to the kill total; with chase, it's already after the player
    /// </summary>
    internal static void A_SpawnEnemy(Entities.Actors.Actor ob, string[] args)
    {
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.WriteLine("A_SpawnEnemy: no actor given.");
            return;
        }

        var spawned = _mapManager.SpawnThing(ob.TileX, ob.TileY,
            new Assets.MapActorTranslation { Class = args[0], Angles = ob.Dir == objdirtypes.nodir ? -1 : (int)ob.Dir * 45 }, countKill: false);
        if (spawned == null)
        {
            Console.WriteLine($"A_SpawnEnemy: \"{args[0]}\" isn't an actor.");
            return;
        }

        spawned.X = ob.X;
        spawned.Y = ob.Y;
        spawned.AreaNumber = ob.AreaNumber;
        if (args.Skip(1).Any(a => a.Equals("chase", StringComparison.OrdinalIgnoreCase)) && spawned.ResolvedStates.ContainsKey("Chase"))
        {
            NewActorState(spawned, "Chase");
            spawned.Dir = objdirtypes.nodir;
        }
    }

    /// <summary>
    /// The think of a sleeper (Spawn): once the player can see it, it counts down its wake delay
    /// (Temp2) and wakes (its Wake state). One with no delay only wakes when shot.
    /// </summary>
    internal static void T_WaitToWake(Entities.Actors.Actor ob)
    {
        if (!ob.RuntimeFlags.HasFlag(objflags.FL_VISABLE) || ob.Temp3 <= 0 || ob.Temp3 == 255 * 60)
            return;
        if (ob.Temp2 > tics)
        {
            ob.Temp2 -= (short)tics;
            return;
        }
        ob.Temp2 = 0;
        ob.RuntimeFlags &= ~objflags.FL_SHOOTABLE;
        NewActorState(ob, "Wake");
    }

    /// <summary>
    /// A_Melee(chance, min, max): a blow at the player within reach (two tiles), landing chance
    /// times in 256 for min..max, with the actor's `meleesound`; it alerts the area
    /// </summary>
    internal static void A_Melee(Entities.Actors.Actor ob, string[] args)
    {
        int chance = args.Length > 0 && int.TryParse(args[0], out var c) ? c : 200;
        int min = args.Length > 1 && int.TryParse(args[1], out var a) ? a : 0;
        int max = args.Length > 2 && int.TryParse(args[2], out var b) ? b : min;

        PlayActorSound(ob, "meleesound");
        madenoise = true;

        if (Math.Abs(player.X - ob.X) - MapConstants.TILEGLOBAL <= MINACTORDIST
            && Math.Abs(player.Y - ob.Y) - MapConstants.TILEGLOBAL <= MINACTORDIST
            && US_RndT() < chance)
        {
            TakeDamage(max > min ? min + US_RndT() * (max - min + 1) / 256 : min, ob);
        }
    }

    /*
    =============================================================================

                                    CHASE

    =============================================================================
    */

    internal static void T_BlakeChase(Entities.Actors.Actor ob)
    {
        ob.RuntimeFlags &= ~objflags.FL_LOCKEDSTATE;

        if (gamestate.victoryflag || ob.HasFlag("STATIONARY"))
            return;

        if (ob.Ammo != 0)
        {
            if (CheckLine(ob))
            {
                ob.Hidden = false;
                int dist = Math.Max(Math.Max(Math.Abs(ob.TileX - player.TileX), Math.Abs(ob.TileY - player.TileY)), 1);
                bool nearAttack = dist == 1 && ob.Distance < 0x4000;

                // Shoot-mode actors keep at whichever they're doing until their count runs out
                bool shootMode = ob.PropertyBool("monster.shootmode");
                if (shootMode)
                {
                    if (ob.Ammo > tics)
                        ob.Ammo -= (short)tics;
                    else
                    {
                        ChangeShootMode(ob);
                        if (!ob.RuntimeFlags.HasFlag(objflags.FL_SHOOTMODE))
                            ob.Ammo >>= 1;      // closing in lasts half as long
                    }
                }

                int chance = nearAttack || shootMode
                    ? (ob.RuntimeFlags.HasFlag(objflags.FL_SHOOTMODE) ? 300 : 0)
                    : (int)((tics << 4) / dist);

                if (US_RndT() < chance && ob.Ammo != 0 && !ob.RuntimeFlags.HasFlag(objflags.FL_INTERROGATED))
                {
                    DoAttack(ob);
                    return;
                }
            }
            else
            {
                ob.Hidden = true;
                ChangeShootMode(ob);
            }
        }

        if (ob.Dir == objdirtypes.nodir)
        {
            SelectBlakeChaseDir(ob);
            if (ob.Dir == objdirtypes.nodir)
                return;     // boxed in
        }

        var move = (int)(ob.Speed * tics);
        while (move != 0)
        {
            if (ob.Distance < 0)
            {
                // waiting for a door to open
                OpenDoor(-ob.Distance - 1);
                if (doorobjlist[-ob.Distance - 1].action != dooractiontypes.dr_open)
                    return;
                ob.Distance = (int)MapConstants.TILEGLOBAL;
            }

            if (move < ob.Distance)
            {
                MoveObj(ob, move);
                break;
            }

            RecenterOnTile(ob);
            move -= ob.Distance;

            SelectBlakeChaseDir(ob);
            if (ob.Dir == objdirtypes.nodir)
                return;
        }
    }

    // Closing in dodges, unless it's a CHASEDIR actor (the floating bomb), which comes straight on
    static void SelectBlakeChaseDir(Entities.Actors.Actor ob)
    {
        if (ob.HasFlag("CHASEDIR"))
            SelectChaseDir(ob);
        else
            SelectDodgeDir(ob);
    }

    /// <summary>
    /// Switches between shooting and closing in: shooting lasts 1-2 shots' worth of its count,
    /// closing in 60-119 tics. (Blake Stone uses the same count as the actor's ammo.)
    /// </summary>
    static void ChangeShootMode(Entities.Actors.Actor ob)
    {
        if (ob.RuntimeFlags.HasFlag(objflags.FL_SHOOTMODE))
        {
            ob.RuntimeFlags &= ~objflags.FL_SHOOTMODE;
            ob.Ammo = (short)(60 + US_RndT() % 60);
        }
        else
        {
            ob.RuntimeFlags |= objflags.FL_SHOOTMODE;
            ob.Ammo = (short)(1 + US_RndT() % 2);
        }
    }

    /// <summary>
    /// Starts an attack: its Melee state when it has one and the player is within a tile, else
    /// Attack. Not at all past its `monster.attackrange` (tiles; the floating bomb's 1), nor
    /// closer than its `monster.attackmindist`, and only `monster.attackchance` times in 256
    /// (the liquid alien rising).
    /// </summary>
    static void DoAttack(Entities.Actors.Actor ob)
    {
        int dx = Math.Abs(ob.TileX - player.TileX), dy = Math.Abs(ob.TileY - player.TileY);
        int dist = Math.Max(Math.Max(dx, dy), 1);
        if (ob.PropertyInt("monster.attackrange", 0) is > 0 and var range && dist > range)
            return;
        if (ob.PropertyInt("monster.attackmindist", 0) is > 0 and var mindist && dist < mindist)
            return;
        if (ob.PropertyInt("monster.attackchance", 256) is var chance and < 256 && US_RndT() >= chance)
            return;
        NewActorState(ob, dist <= 1 && ob.ResolvedStates.ContainsKey("Melee") ? "Melee" : "Attack");
    }

    /*
    =============================================================================

                                    SHOOT

    =============================================================================
    */

    internal static void T_BlakeShoot(Entities.Actors.Actor ob)
    {
        bool smart = ob.HasFlag("SMART");
        if (smart && ob.Ammo == 0)
            return;

        if (ob.AreaNumber < _mapManager.Floors.NumAreas && areabyplayer[ob.AreaNumber] == 0)
            return;
        if (!CheckLine(ob))
            return;     // the player is behind a wall

        ShotAtPlayer(ob);
        PlayActorSound(ob, "attacksound");

        if (smart)
        {
            ob.Ammo--;
            CheckRunChase(ob);
        }

        madenoise = true;   // gunfire alerts the area
    }

    /*
    =============================================================================

                        RUNNING AWAY (SMART actors)

    =============================================================================
    */

    /// <summary>
    /// Why a SMART actor should run (RR_ flags), 0 for chasing; starts or stops it running
    /// </summary>
    static int CheckRunChase(Entities.Actors.Actor ob)
    {
        int reason = 0;
        if (ob.Ammo == 0)
            reason |= RR_AMMO;
        if (ob.Hitpoints <= _mapManager.GetScaledHealth(ob) >> 1)
            reason |= RR_HEALTH;
        if ((ob.RuntimeFlags & (objflags.FL_FRIENDLY | objflags.FL_INTERROGATED)) == objflags.FL_INTERROGATED)
            reason |= RR_INTERROGATED;

        if (reason != 0)
        {
            if (!ob.RuntimeFlags.HasFlag(objflags.FL_RUNAWAY))
            {
                ob.Temp3 = 0;
                ob.RuntimeFlags |= objflags.FL_RUNAWAY;
                ob.Speed += RUNAWAY_SPEED;
            }
        }
        else if (ob.RuntimeFlags.HasFlag(objflags.FL_RUNAWAY))
        {
            ob.RuntimeFlags &= ~objflags.FL_RUNAWAY;
            ob.Speed -= RUNAWAY_SPEED;
        }

        return reason;
    }

    /// <summary>
    /// Which way (in tiles) an actor closing in heads: at the player, or, for a SMART actor that
    /// should be running, at the pickup, door or far corner it's running for
    /// </summary>
    static void SeekDelta(Entities.Actors.Actor ob, out int deltax, out int deltay)
    {
        if (ob.HasFlag("SMART") && CheckRunChase(ob) is var whyRun and not 0)
        {
            if (ob.SeekX == 0)
                GetCornerSeek(ob);

            if (!LookForGoodies(ob, whyRun))
            {
                if (ob.TileX == ob.SeekX && ob.TileY == ob.SeekY)
                {
                    GetCornerSeek(ob);
                    ob.RuntimeFlags &= ~objflags.FL_INTERROGATED;
                }

                deltax = ob.SeekX - ob.TileX;
                deltay = ob.SeekY - ob.TileY;
                return;
            }

            // It took something: still short, it stays put this time
            if (CheckRunChase(ob) != 0)
            {
                deltax = deltay = 0;
                return;
            }
        }

        deltax = player.TileX - ob.TileX;
        deltay = player.TileY - ob.TileY;
    }

    // A running actor with nowhere better to go heads for one of these
    static readonly byte[] SeekPointX = [32, 63, 32, 1];
    static readonly byte[] SeekPointY = [1, 63, 32, 1];

    static void GetCornerSeek(Entities.Actors.Actor ob)
    {
        int point = US_RndT() & 3;
        ob.RuntimeFlags &= ~objflags.FL_RUNTOSTATIC;
        ob.SeekX = SeekPointX[point];
        ob.SeekY = SeekPointY[point];
    }

    /// <summary>
    /// A running actor looks for what it needs: true when it got some (taking an item it's
    /// standing on, or, out of the player's sight and area, simply getting some), false when it
    /// has picked an item or a door to head for (SeekX/SeekY) or has nothing new
    /// </summary>
    static bool LookForGoodies(Entities.Actors.Actor ob, int reason)
    {
        // A scientist that turned mean backs off to a door, then (half the time) attacks
        bool justFindDoor = false;
        if ((reason & RR_INTERROGATED) != 0)
        {
            justFindDoor = true;
            if (US_RndT() < 128)
                ob.RuntimeFlags &= ~objflags.FL_INTERROGATED;
        }

        int maxHealth = _mapManager.GetScaledHealth(ob);

        // Out of the player's area and sight, it cheats
        if (player.AreaNumber != ob.AreaNumber && !ob.RuntimeFlags.HasFlag(objflags.FL_VISABLE))
        {
            if (ob.Ammo == 0)
                ob.Ammo += 8;
            if (ob.Hitpoints <= maxHealth >> 1)
                ob.Hitpoints += 10;
            return true;
        }

        if (!justFindDoor)
        {
            foreach (var item in _mapManager.GetActors())
            {
                if (item.IsRemoved || item.AreaNumber != ob.AreaNumber)
                    continue;
                int ammoGain = item.PropertyInt("monster.ammogain", 0);
                int healthGain = item.PropertyInt("monster.healthgain", 0);
                if (ammoGain <= 0 && healthGain <= 0)
                    continue;

                // Standing on it: take it, if it's needed
                if (item.TileX == ob.TileX && item.TileY == ob.TileY)
                {
                    if (ammoGain > 0)
                    {
                        if (ob.Ammo != 0)
                            continue;
                        ob.Ammo += (short)ammoGain;
                    }
                    else
                    {
                        if (ob.Hitpoints > maxHealth >> 1)
                            continue;
                        ob.Hitpoints += (short)healthGain;
                    }

                    ob.SeekX = 0;
                    item.RunState("Pickup");        // food leaves its wrapper
                    _mapManager.MarkForRemoval(item);
                    return true;
                }

                // Else maybe head for it
                if (!ob.RuntimeFlags.HasFlag(objflags.FL_RUNTOSTATIC)
                    && ((reason & RR_AMMO) != 0 && ammoGain > 0 || (reason & RR_HEALTH) != 0 && healthGain > 0))
                {
                    ob.RuntimeFlags |= objflags.FL_RUNTOSTATIC;
                    ob.SeekX = item.TileX;
                    ob.SeekY = item.TileY;
                    return false;
                }
            }
        }

        // Running for a door out of the room, when the room joins the player's
        if (ob.AreaNumber < _mapManager.Floors.NumAreas && areabyplayer[ob.AreaNumber] != 0)
        {
            if (ob.RuntimeFlags.HasFlag(objflags.FL_RUNTOSTATIC))
                return false;   // already heading somewhere

            var doors = new List<int>();
            for (int door = 0; door < lastdoorobj && doors.Count < 8; door++)
                if (DoorLeadsOutOf(door, ob.AreaNumber))
                    doors.Add(door);

            if (doors.Count > 0)
            {
                // Not the one it used last, unless it's the only one
                int index = US_RndT() % doors.Count;
                if (doors[index] + 1 == ob.Temp3 && doors.Count > 1)
                    index = (index + 1) % doors.Count;

                int chosen = doors[index];
                ob.Temp3 = (short)(chosen + 1);
                ob.SeekX = (byte)doorobjlist[chosen].tilex;
                ob.SeekY = (byte)doorobjlist[chosen].tiley;
                ob.RuntimeFlags |= objflags.FL_RUNTOSTATIC;
            }
        }
        else if (ob.RuntimeFlags.HasFlag(objflags.FL_RUNTOSTATIC))
        {
            // What it was after is gone, or it's far off: off to a corner
            ob.SeekX = 0;
        }

        return false;
    }

    /// <summary>Whether a door joins an area to another and opens without a key (an actor's way out)</summary>
    static bool DoorLeadsOutOf(int door, int area)
    {
        var d = doorobjlist[door];
        if (d.Lock.Length > 0)
            return false;

        int x = d.tilex, y = d.tiley;
        var (a, b) = d.vertical ? (AreaAt(x - 1, y), AreaAt(x + 1, y)) : (AreaAt(x, y - 1), AreaAt(x, y + 1));
        return a == area || b == area;
    }

    // The area on a tile, or -1 for none
    static int AreaAt(int x, int y)
    {
        if (x < 0 || y < 0 || x >= MapManager.MAPSIZE || y >= MapManager.MAPSIZE)
            return -1;
        int spot = _mapManager.MAPSPOT(x, y, 0);
        return _mapManager.VALIDAREA(spot) ? spot - _mapManager.Floors.AreaTile : -1;
    }

    /*
    =============================================================================

                                    PATROL

    =============================================================================
    */

    /// <summary>
    /// A PATROLTURNS patroller reaching a tile's centre: a patrol point turns it, as ever, and
    /// when the way on is blocked (or, for a RANDOMTURN one, now and then anyway) it turns a
    /// step at a time, one way round, until it can go on. It stops (nodir) for this tic when
    /// that way's blocked too, and carries on turning next time.
    /// </summary>
    static void TurningPathDir(Entities.Actors.Actor ob)
    {
        var point = _mapManager.PatrolPointAt(ob.TileX, ob.TileY);
        if (point != null)
            ob.Dir = point.Dir;

        ob.Distance = (int)MapConstants.TILEGLOBAL;
        bool randomTurn = ob.HasFlag("RANDOMTURN") && US_RndT() > 180;
        bool cantWalk = !CanWalk(ob);

        // A patrol point wins over a random turn
        if (cantWalk || randomTurn && point == null)
        {
            if (ob.TryDir == (byte)objdirtypes.nodir)
                ob.TryDir |= (byte)(US_RndT() & 128);      // which way round
            else
                ob.Dir = (objdirtypes)(ob.TryDir & 127);    // on from the last way tried

            if ((ob.TryDir & 128) != 0)
                ob.Dir = ob.Dir == objdirtypes.east ? objdirtypes.southeast : ob.Dir - 1;
            else
                ob.Dir = ob.Dir >= objdirtypes.southeast ? objdirtypes.east : ob.Dir + 1;

            ob.TryDir = (byte)((ob.TryDir & 128) | (byte)ob.Dir);
            if (!CanWalk(ob))
                ob.Dir = objdirtypes.nodir;
        }

        if (ob.Dir != objdirtypes.nodir)
        {
            TryWalk(ob);
            ob.TryDir = (byte)objdirtypes.nodir;
        }
    }

    /// <summary>
    /// A STEPBACK actor that has walked into the player (MoveObj): it heads back to the tile it
    /// was coming from, the way it came, as far as it had already come
    /// </summary>
    static void StepBack(Entities.Actors.Actor ob)
    {
        var (dx, dy) = DirOffset(ob.Dir);
        if (dx == 0 && dy == 0)
            return;
        ob.TileX = (byte)(ob.TileX - dx);
        ob.TileY = (byte)(ob.TileY - dy);
        ob.Dir = opposite[(byte)ob.Dir];
        ob.Distance = (int)MapConstants.TILEGLOBAL - ob.Distance;
        ob.SyncPosition();
    }

    // The tile step for a direction, (0, 0) for none
    static (int Dx, int Dy) DirOffset(objdirtypes dir) => dir switch
    {
        objdirtypes.east => (1, 0),
        objdirtypes.northeast => (1, -1),
        objdirtypes.north => (0, -1),
        objdirtypes.northwest => (-1, -1),
        objdirtypes.west => (-1, 0),
        objdirtypes.southwest => (-1, 1),
        objdirtypes.south => (0, 1),
        objdirtypes.southeast => (1, 1),
        _ => (0, 0),
    };

    /// <summary>Whether TryWalk would get anywhere in the actor's direction, without moving it or opening anything</summary>
    static bool CanWalk(Entities.Actors.Actor ob)
    {
        var (dx, dy) = DirOffset(ob.Dir);
        if (dx == 0 && dy == 0)
            return false;

        int x = ob.TileX + dx, y = ob.TileY + dy;
        if (x < 0 || y < 0 || x >= MapManager.MAPSIZE || y >= MapManager.MAPSIZE)
            return false;
        if (dx != 0 && dy != 0)
            return CHECKDIAG(x, y) && CHECKDIAG(ob.TileX + dx, ob.TileY) && CHECKDIAG(ob.TileX, ob.TileY + dy);

        switch (_mapManager.actorat[x, y])
        {
            case Wall or BlockingActor:
                return false;
            case Door door:
                return ob.HasFlag("PHASEDOORS")
                    || !ob.HasFlag("NODOORS") && (!ob.HasFlag("NOLOCKEDDOORS") || doorobjlist[door.door].Lock.Length == 0);
            default:
                return !_mapManager.IsShootableActorAt(x, y);
        }
    }

    /*
    =============================================================================

                                WOUNDS AND DEATH

    =============================================================================
    */

    /// <summary>
    /// A `monster.woundstages` actor (a SWAT guard) losing another 1/(stages+1) of its health
    /// goes down: its Wounded state, no longer shootable or in the way, for 5 to 24 seconds.
    /// True when it did.
    /// </summary>
    static bool WoundActor(Entities.Actors.Actor ob, int oldHitpoints)
    {
        if (!ob.Properties.ContainsKey("monster.woundstages") || !ob.ResolvedStates.ContainsKey("Wounded"))
            return false;

        int boundary = _mapManager.GetScaledHealth(ob) / (ob.Temp1 + 1) + 1;
        if (oldHitpoints / boundary == ob.Hitpoints / boundary)
            return false;

        PlayActorSound(ob, "woundsound");
        NewActorState(ob, "Wounded");
        ob.RuntimeFlags &= ~objflags.FL_SHOOTABLE;
        ob.Temp2 = (short)(5 * 60 + US_RndT() % 20 * 60);
        return true;
    }

    /// <summary>The think on a wounded actor's last, held frame: when its time's up and the player isn't on top of it, it gets up (Recover)</summary>
    internal static void T_Wounded(Entities.Actors.Actor ob)
    {
        if (ob.Temp2 > tics)
        {
            ob.Temp2 -= (short)tics;
            return;
        }
        ob.Temp2 = 0;

        if (Math.Abs(player.X - ob.X) > MapConstants.TILEGLOBAL || Math.Abs(player.Y - ob.Y) > MapConstants.TILEGLOBAL)
        {
            ob.RuntimeFlags |= objflags.FL_SHOOTABLE;
            NewActorState(ob, "Recover");
        }
    }

    /// <summary>
    /// Sets a dying actor sliding back from its killer (T_BlowBack): as far as the weapon in
    /// hand's `weapon.knockback` when the player shot it, else the killer's own `knockback`
    /// (an explosion's), else 0x5000
    /// </summary>
    static void StartBlowBack(Entities.Actors.Actor ob, Entities.Actors.Actor? killer)
    {
        if (killer == null)
            return;

        int distance = ReferenceEquals(killer, player)
            ? (gamestate.weapon != null && int.TryParse(_inventoryManager.GetProperty(gamestate.weapon, "weapon.knockback")?.ToString(), out var k) ? k : 0)
            : killer.PropertyInt("knockback", DEFAULT_KNOCKBACK);
        if (distance <= 0)
            return;

        var angle = Math.Atan2(killer.Y - ob.Y, ob.X - killer.X);
        if (angle < 0)
            angle += Math.PI * 2;
        ob.Angle = (short)((int)(angle / (Math.PI * 2) * ANGLES) % ANGLES);
        ob.Temp3 = (short)Math.Min(distance, short.MaxValue);
        ob.RuntimeFlags |= objflags.FL_SLIDING;
    }

    /// <summary>The think on Death frames: the body slides back a step each tic until it's gone its distance or hits something</summary>
    internal static void T_BlowBack(Entities.Actors.Actor ob)
    {
        if (!ob.RuntimeFlags.HasFlag(objflags.FL_SLIDING))
            return;

        int step = Math.Min((int)ob.Temp3, SLIDE_SPEED);
        ob.Temp3 -= (short)step;
        if (ob.Temp3 <= 0)
            ob.RuntimeFlags &= ~objflags.FL_SLIDING;

        int dx = MathUtils.FixedMul(step, costable[ob.Angle]);
        int dy = -MathUtils.FixedMul(step, sintable[ob.Angle]);
        if (!ActorClipMove(ob, dx, dy))
            ob.RuntimeFlags &= ~objflags.FL_SLIDING;

        ob.TileX = (byte)(ob.X >> (int)MapConstants.TILESHIFT);
        ob.TileY = (byte)(ob.Y >> (int)MapConstants.TILESHIFT);
        ob.SyncPosition();
    }

    // Moves an actor unless that would put it into a wall, something solid or a door that isn't open
    static bool ActorClipMove(Entities.Actors.Actor ob, int dx, int dy)
    {
        int nx = ob.X + dx, ny = ob.Y + dy, size = (int)MINDIST;
        foreach (var (cx, cy) in new[] { (nx - size, ny - size), (nx + size, ny - size), (nx - size, ny + size), (nx + size, ny + size) })
        {
            int tx = cx >> (int)MapConstants.TILESHIFT, ty = cy >> (int)MapConstants.TILESHIFT;
            if (tx < 0 || ty < 0 || tx >= MapManager.MAPSIZE || ty >= MapManager.MAPSIZE)
                return false;
            switch (_mapManager.actorat[tx, ty])
            {
                case Wall or BlockingActor:
                    return false;
                case Door door when doorobjlist[door.door].action != dooractiontypes.dr_open:
                    return false;
            }
        }

        ob.X = nx;
        ob.Y = ny;
        return true;
    }

    // Shooting an informant warns the player, the first time and now and then after
    static bool warnedkilledinformant;

    static void WarnKilledInformant(Entities.Actors.Actor ob)
    {
        if (warnedkilledinformant && US_RndT() >= 25)
            return;
        warnedkilledinformant = true;
        if (ob.PropertyStrings("talk.killedmessage") is [var message, ..])
            _hudMessageManager.Show(Managers.HudMessageKind.Other, message, ob.PropertyStrings("talk.style").FirstOrDefault());
    }

    /*
    =============================================================================

                                TALKING (interrogation)

    Holding use near a TALKATIVE actor that's friendly and in view, facing it, talks to it:
    an INFORMANT gives a hint (one for the room it's in when the map has any there, else one
    of the map's general ones), and when talked to again its gifts; anyone else says something
    nice, or something mean and turns on the player.

    The hints are messages in a VGAGRAPH text (`talk.hints`, e.g. InformantHints), "^XX"
    between them; the map says which, with mapdefs map-info `hint:<text>` codes (the code's low
    byte the message's number, counting from 1, and the room it's in the one it's about). The
    nice and mean sayings (`talk.friendly`, `talk.hostile`) work the same way.

    =============================================================================
    */

    static int interrogatedelay;

    /// <summary>The use key held with nothing to use ahead: talk to whoever's there</summary>
    internal static void TryInterrogate()
    {
        if (interrogatedelay > 0)
        {
            interrogatedelay = Math.Max(interrogatedelay - (int)tics, 0);
            return;
        }

        const int MaxAngle = 45 / 2;
        Entities.Actors.Actor? chosen = null;
        int chosenDist = (int)MINACTORDIST;

        foreach (var ob in _mapManager.GetActors())
        {
            if (ob.IsRemoved || !ob.HasFlag("TALKATIVE")
                || (ob.RuntimeFlags & (objflags.FL_FRIENDLY | objflags.FL_VISABLE)) != (objflags.FL_FRIENDLY | objflags.FL_VISABLE)
                || Math.Abs(ob.TileX - player.TileX) > 2 || Math.Abs(ob.TileY - player.TileY) > 2)
                continue;

            int dist = Math.Min(Math.Abs(player.X - ob.X), Math.Abs(player.Y - ob.Y));
            if (dist >= chosenDist)
                continue;

            if (ob.RuntimeFlags.HasFlag(objflags.FL_ATTACKMODE))
            {
                ob.RuntimeFlags &= ~objflags.FL_FRIENDLY;
                continue;
            }

            var angle = Math.Atan2(player.Y - ob.Y, ob.X - player.X);
            if (angle < 0)
                angle += Math.PI * 2;
            int facing = Math.Abs(player.Angle - (int)(angle / (Math.PI * 2) * ANGLES));
            if (Math.Min(facing, ANGLES - facing) > MaxAngle)
                continue;

            chosen = ob;
            chosenDist = dist;
        }

        if (chosen != null)
            interrogatedelay = Interrogate(chosen) ? 20 : 120;      // an informant can be asked again sooner
    }

    /// <summary>Use let go: the next press talks straight away</summary>
    internal static void ResetInterrogateDelay() => interrogatedelay = 0;

    /// <summary>Talks to an actor (TryInterrogate); true for an informant</summary>
    static bool Interrogate(Entities.Actors.Actor ob)
    {
        bool informant = ob.HasFlag("INFORMANT");
        string? said = null;

        if (informant)
        {
            // Asked again, it hands over what it has
            if (ob.RuntimeFlags.HasFlag(objflags.FL_INTERROGATED))
            {
                var gifts = ob.PropertyStrings("talk.gifts");
                var giftMessages = ob.PropertyStrings("talk.giftmessages");
                foreach (var (flag, index) in new[] { (objflags.FL_HASAMMO, 0), (objflags.FL_HASTOKENS, 1) })
                {
                    if (!ob.RuntimeFlags.HasFlag(flag) || index >= gifts.Count
                        || _inventoryManager.CreateActor(gifts[index]) is not Entities.Actors.Inventory gift || !CouldTakeInventory(gift))
                        continue;
                    int amount = gift.Properties.TryGetValue("inventory.amount", out var a) ? Convert.ToInt32(a) : 1;
                    if (!TryApplyInventory(gift, amount))
                        continue;
                    ob.RuntimeFlags &= ~flag;
                    said = _hudMessageManager.Localize(giftMessages.ElementAtOrDefault(index) ?? "");
                    break;
                }
            }

            if (said == null)
            {
                said = InformantHint(ob);
                ob.RuntimeFlags |= objflags.FL_INTERROGATED;
            }
        }
        else
        {
            // Asked twice, or half the time anyway, it turns mean
            string list;
            if (ob.RuntimeFlags.HasFlag(objflags.FL_MUSTATTACK) || (US_RndT() & 1) != 0)
            {
                ob.RuntimeFlags &= ~objflags.FL_FRIENDLY;
                ob.RuntimeFlags |= objflags.FL_INTERROGATED;
                list = ob.PropertyStrings("talk.hostile").FirstOrDefault() ?? "";
            }
            else
            {
                ob.RuntimeFlags |= objflags.FL_MUSTATTACK;
                list = ob.PropertyStrings("talk.friendly").FirstOrDefault() ?? "";
            }
            said = RandomSaying(list);
        }

        if (!string.IsNullOrEmpty(said))
        {
            var header = ob.PropertyStrings("talk.header").FirstOrDefault();
            var text = string.IsNullOrEmpty(header) ? said : $"{_hudMessageManager.Localize(header)}\n\n{said}";
            _hudMessageManager.Show(Managers.HudMessageKind.Other, text, ob.PropertyStrings("talk.style").FirstOrDefault());
            if (ob.PropertyStrings("talk.sound").FirstOrDefault() is { Length: > 0 } sound)
                _audioManager.Play(sound);
        }

        return informant;
    }

    /// <summary>
    /// An informant's hint: one of the map's hints for the room it's in, if the map has any,
    /// else one of its general ones. It keeps to the one it picked (SeekX for a room's, SeekY
    /// for a general one; Ammo the room it picked it in).
    /// </summary>
    static string? InformantHint(Entities.Actors.Actor ob)
    {
        var listName = ob.PropertyStrings("talk.hints").FirstOrDefault();
        if (string.IsNullOrEmpty(listName))
            return null;

        var placed = _mapManager.Hints.GetValueOrDefault(listName) ?? [];
        var roomHints = placed.Where(h => h.Area == ob.AreaNumber).ToList();
        if (roomHints.Count > 0)
        {
            if (ob.Ammo != ob.AreaNumber)
                ob.SeekX = 0xff;
            ob.Ammo = ob.AreaNumber;
            if (ob.SeekX == 0xff || ob.SeekX >= roomHints.Count)
                ob.SeekX = (byte)(US_RndT() % roomHints.Count);
            return HintText(listName, roomHints[ob.SeekX].Message);
        }

        var general = placed.Where(h => h.Area == 0xff).ToList();
        if (general.Count == 0)
            return RandomSaying(listName);
        if (ob.SeekY == 0xff || ob.SeekY >= general.Count)
            ob.SeekY = (byte)(US_RndT() % general.Count);
        return HintText(listName, general[ob.SeekY].Message);
    }

    // One of the map's sayings from a text, or any from the text when the map places none
    static string? RandomSaying(string listName)
    {
        if (string.IsNullOrEmpty(listName))
            return null;
        var placed = _mapManager.Hints.GetValueOrDefault(listName);
        if (placed is { Count: > 0 })
            return HintText(listName, placed[US_RndT() % placed.Count].Message);
        int count = HintMessages(listName).Count;
        return count == 0 ? null : HintText(listName, 1 + US_RndT() % count);
    }

    static readonly Dictionary<string, List<string>> hinttexts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Message <paramref name="number"/> (from 1) of a hint text, or null</summary>
    static string? HintText(string listName, int number)
    {
        var messages = HintMessages(listName);
        return number >= 1 && number <= messages.Count ? messages[number - 1] : null;
    }

    /// <summary>
    /// A VGAGRAPH text's messages, split at its "^XX"s, with line breaks kept and its formatting
    /// codes (^FC color and the like) left out
    /// </summary>
    static List<string> HintMessages(string listName)
    {
        if (hinttexts.TryGetValue(listName, out var cached))
            return cached;

        var messages = new List<string>();
        if (_assetManager.Find<Assets.TextAsset>(listName) is { } asset)
        {
            var parts = asset.ToText().Split("^XX");
            foreach (var part in parts.Take(parts.Length - 1))     // after the last ^XX is only the end
            {
                var text = System.Text.RegularExpressions.Regex.Replace(part, @"\^[A-Z]{2}[0-9A-Fa-f]{0,2}", "");
                text = string.Join("\n", text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(l => l.Trim()))
                    .Trim('\n');
                messages.Add(text);
            }
        }
        else
            Console.WriteLine($"Hint text \"{listName}\" wasn't found");

        hinttexts[listName] = messages;
        return messages;
    }
}
