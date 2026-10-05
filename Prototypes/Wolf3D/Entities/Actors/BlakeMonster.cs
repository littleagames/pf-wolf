using Wolf3D.Constants;
using Wolf3D.Extensions;
using Wolf3D.Managers;
using static Wolf3D.Program;

namespace Wolf3D.Entities.Actors;

/// <summary>
/// Blake Stone's (and Planet Strike's) enemies: a <see cref="Monster"/> with their ways of
/// chasing, shooting, running away, being wounded, dying and being talked to, as bstone has
/// them. An actordefs class is one with `parent: BlakeMonster` (or BlakeEnemy, which has it).
/// Its machines (electro-spheres, turrets, vents, barriers...) are in BlakeMonster.Machines.cs.
/// </summary>
internal partial record BlakeMonster : Monster
{
    /// <summary>Blake Stone's actors keep thinking out of the player's areas (their own wake-up rules apply)</summary>
    internal override bool SleepsOutOfReach => false;

    /// <summary>The thinks and actions only a BlakeMonster can run, by their actordefs names.</summary>
    internal static void RegisterActions()
    {
        // Its chase and shot are Monster's T_Chase and T_Shoot, overridden; these older names
        // still work for actordefs written before
        ActorActionRegistry.RegisterFor<BlakeMonster>("T_BlakeChase", m => m.Chase());
        ActorActionRegistry.RegisterFor<BlakeMonster>("T_BlakeShoot", m => m.Shoot());
        ActorActionRegistry.RegisterFor<BlakeMonster>("T_BlowBack", m => m.BlowBack());
        ActorActionRegistry.RegisterFor<BlakeMonster>("T_Wounded", m => m.Wounded());
        ActorActionRegistry.RegisterFor<BlakeMonster>("T_WaitToWake", m => m.WaitToWake());
        ActorActionRegistry.RegisterFor<BlakeMonster>("A_SpawnEnemy", (m, args) => m.SpawnEnemy(args));
        ActorActionRegistry.RegisterFor<BlakeMonster>("A_Melee", (m, args) => m.Melee(args));
        RegisterMachineActions();
    }

    /*
    =============================================================================

                        WHERE IT GOES ITS OWN WAY (Monster's hooks)

    =============================================================================
    */

    // Shots it has left (`monster.ammo`; an informant: the room it last picked a hint in), the
    // tile it's heading for when it runs away (an informant: the hints it picked; one that came
    // out of a wall outlet: that outlet's tile, plus one), and the way a patroller turns when
    // blocked (a dir, plus 128 for clockwise; nodir when not turning). Kept in saved games.
    public short Ammo { get; internal set; }
    public byte SeekX { get; internal set; }
    public byte SeekY { get; internal set; }
    public byte TryDir { get; internal set; } = (byte)objdirtypes.nodir;

    // Out of the shots it started with: a `dropitem.needsammo` drop isn't left, a `dropitem.alt` one is
    protected override bool OutOfAmmo => Properties.ContainsKey("monster.ammo") && Ammo == 0;

    // The body slides back from what killed it (T_BlowBack)
    protected override void OnKilled(Actor? attacker) => StartBlowBack(attacker);

    internal override void Damage(uint damage, Actor? attacker = null)
    {
        // A sleeper on a timer (`monster.wakeprotected`, the gurney mutant) can't be shot awake
        if (PropertyBool("monster.wakeprotected") && Temp3 > 0)
            return;
        base.Damage(damage, attacker);
    }

    // Hurt, a SWAT guard goes down wounded; a `monster.painattack` actor already stunned since it
    // last chased doesn't flinch again
    protected override bool OnHurt(int oldHitpoints) =>
        WoundActor(oldHitpoints)
        || Properties.ContainsKey("monster.painattack") && RuntimeFlags.HasFlag(objflags.FL_LOCKEDSTATE);

    // ... and a `monster.painattack` actor now and then shoots straight back, and can't be
    // stunned again until it next chases
    protected override void OnPain()
    {
        if (!Properties.ContainsKey("monster.painattack"))
            return;
        if (US_RndT() < PropertyInt("monster.painattack", 0) && !HasFlag("STATIONARY"))
        {
            ChangeShootMode();
            DoAttack();
        }
        RuntimeFlags |= objflags.FL_LOCKEDSTATE;
    }

    // A STEPBACK actor walking into the player turns round and heads back to the tile it came
    // from, rather than standing stuck against them
    protected override void OnBumpedPlayer()
    {
        if (HasFlag("STEPBACK"))
            StepBack();
    }

    // A PATROLTURNS patroller turns when blocked rather than stopping
    internal override void SelectPathDir()
    {
        if (HasFlag("PATROLTURNS"))
            TurningPathDir();
        else
            base.SelectPathDir();
    }

    /*
    =============================================================================

                                BLAKE STONE AI

    Blake Stone's enemies' ways, as bstone has them (3d_act2.cpp, 3d_state.cpp, 3d_agent.cpp).
    The flags and properties below work on any BlakeMonster.

    Thinks and actions (actordefs `think:` / `action:`):
      T_Chase        chase: closes in dodging, shoots only while it has ammo, and (with
                     `monster.shootmode`) takes turns at shooting and closing in
                     (T_BlakeChase still works too)
      T_Shoot        a hitscan shot; SMART actors use up their ammo and alert the area
                     (T_BlakeShoot still works too)
      T_BlowBack     on Death frames: the body slides back from the shot that killed it
      T_Wounded      on a held Wounded frame: lies there, then gets up (Recover) once the player
                     is more than a tile away

    Flags:
      FRIENDLY       starts friendly: patrolling, it only looks for the player after a noise
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
      talk.*                what it says when talked to (LevelAI.TryTalk)

    An informant, which never goes after the player, isn't a kill and gives hints when talked
    to, is its own class (Informant).

    =============================================================================
    */

    // Why a SMART actor runs (CheckRunChase)
    private const int RR_AMMO = 1, RR_HEALTH = 2, RR_INTERROGATED = 4;

    // How much faster a SMART actor runs than it chases
    private const int RUNAWAY_SPEED = 1000;

    // How far a corpse slides back each tic (T_BlowBack)
    private const int SLIDE_SPEED = 0x2000;

    // How far a body killed by anything but the player's own gun slides
    private const int DEFAULT_KNOCKBACK = 0x5000;

    /// <summary>
    /// Sets up what's Blake Stone's about a freshly spawned actor (MapManager.SpawnThing): its
    /// ammo, its friendliness, a sleeper's wake delay, and a wounding actor's wound stages
    /// </summary>
    internal override void OnSpawned(int tilex, int tiley)
    {
        // One number, or [min, max] for one at random
        var ammo = PropertyInts("monster.ammo");
        Ammo = (short)(ammo.Count == 0 ? 1 : ammo.Count == 1 ? ammo[0] : ammo[0] + US_RndT() % (ammo[1] - ammo[0] + 1));
        if (HasFlag("FRIENDLY"))
            RuntimeFlags |= objflags.FL_FRIENDLY;

        // The 0xFA value east of it on the object plane, if there is one
        int east = tilex + 1 < MapManager.MAPSIZE ? _mapManager.MAPSPOT(tilex + 1, tiley, 1) : 0;
        int? mapValue = (east & 0xff00) == 0xfa00 ? east & 0xff : null;

        // A sleeper's wake delay (T_WaitToWake), in tics: the map's value in seconds, else a
        // random [min, max] seconds; 0 wakes it only when shot, 255 never
        var wake = PropertyInts("monster.wakedelay");
        if (wake.Count > 0)
        {
            int seconds = mapValue ?? (wake.Count == 1 ? wake[0] : wake[0] + US_RndT() % (wake[1] - wake[0] + 1));
            Temp2 = Temp3 = (short)(seconds * 60);
            if (seconds == 255)
                RuntimeFlags &= ~objflags.FL_SHOOTABLE;
        }

        var stages = PropertyInts("monster.woundstages");
        if (stages.Count > 0)
            Temp1 = (short)(mapValue ?? stages[US_RndT() % stages.Count]);

        // `monster.floorhealth: [floor, times]`: that much tougher on that floor (game-info
        // floor-number; Goldfire on floor 9)
        if (PropertyInts("monster.floorhealth") is [var floor, var times] && floor == _mapManager.CurrentFloorNumber)
            Hitpoints = (short)Math.Min(Hitpoints * times, short.MaxValue);
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
    internal void SpawnEnemy(string[] args)
    {
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.WriteLine("A_SpawnEnemy: no actor given.");
            return;
        }

        var spawned = _mapManager.SpawnThing(TileX, TileY,
            new Assets.MapActorTranslation { Class = args[0], Angles = Dir == objdirtypes.nodir ? -1 : (int)Dir * 45 }, countKill: false);
        if (spawned == null)
        {
            Console.WriteLine($"A_SpawnEnemy: \"{args[0]}\" isn't an actor.");
            return;
        }

        spawned.X = X;
        spawned.Y = Y;
        spawned.AreaNumber = AreaNumber;
        if (args.Skip(1).Any(a => a.Equals("chase", StringComparison.OrdinalIgnoreCase)) && spawned.ResolvedStates.ContainsKey("Chase"))
        {
            spawned.SetState("Chase");
            spawned.Dir = objdirtypes.nodir;
        }
    }

    /// <summary>
    /// The think of a sleeper (Spawn): once the player can see it, it counts down its wake delay
    /// (Temp2) and wakes (its Wake state). One with no delay only wakes when shot.
    /// </summary>
    internal void WaitToWake()
    {
        if (!RuntimeFlags.HasFlag(objflags.FL_VISABLE) || Temp3 <= 0 || Temp3 == 255 * 60)
            return;
        if (Temp2 > tics)
        {
            Temp2 -= (short)tics;
            return;
        }
        Temp2 = 0;
        RuntimeFlags &= ~objflags.FL_SHOOTABLE;
        SetState("Wake");
    }

    /// <summary>
    /// A_Melee(chance, min, max): a blow at the player within reach (two tiles), landing chance
    /// times in 256 for min..max, with the actor's `meleesound`; it alerts the area
    /// </summary>
    internal void Melee(string[] args)
    {
        int chance = args.Length > 0 && int.TryParse(args[0], out var c) ? c : 200;
        int min = args.Length > 1 && int.TryParse(args[1], out var a) ? a : 0;
        int max = args.Length > 2 && int.TryParse(args[2], out var b) ? b : min;

        PlayActorSound(this, "meleesound");
        madenoise = true;

        if (Math.Abs(player.X - X) - MapConstants.TILEGLOBAL <= MINACTORDIST
            && Math.Abs(player.Y - Y) - MapConstants.TILEGLOBAL <= MINACTORDIST
            && US_RndT() < chance)
        {
            TakeDamage(max > min ? min + US_RndT() * (max - min + 1) / 256 : min, this);
        }
    }

    /*
    =============================================================================

                                    CHASE

    =============================================================================
    */

    internal override void Chase()
    {
        RuntimeFlags &= ~objflags.FL_LOCKEDSTATE;

        if (gamestate.victoryflag || HasFlag("STATIONARY"))
            return;

        if (Ammo != 0)
        {
            if (CheckLine(this))
            {
                Hidden = false;
                int dist = Math.Max(Math.Max(Math.Abs(TileX - player.TileX), Math.Abs(TileY - player.TileY)), 1);
                bool nearAttack = dist == 1 && Distance < 0x4000;

                // Shoot-mode actors keep at whichever they're doing until their count runs out
                bool shootMode = PropertyBool("monster.shootmode");
                if (shootMode)
                {
                    if (Ammo > tics)
                        Ammo -= (short)tics;
                    else
                    {
                        ChangeShootMode();
                        if (!RuntimeFlags.HasFlag(objflags.FL_SHOOTMODE))
                            Ammo >>= 1;      // closing in lasts half as long
                    }
                }

                int chance = nearAttack || shootMode
                    ? (RuntimeFlags.HasFlag(objflags.FL_SHOOTMODE) ? 300 : 0)
                    : (int)((tics << 4) / dist);

                if (US_RndT() < chance && Ammo != 0 && !RuntimeFlags.HasFlag(objflags.FL_INTERROGATED))
                {
                    DoAttack();
                    return;
                }
            }
            else
            {
                Hidden = true;
                ChangeShootMode();
            }
        }

        if (Dir == objdirtypes.nodir)
        {
            SelectBlakeChaseDir();
            if (Dir == objdirtypes.nodir)
                return;     // boxed in
        }

        var move = (int)(Speed * tics);
        while (move != 0)
        {
            if (Distance < 0)
            {
                // waiting for a door to open
                OpenDoor(-Distance - 1);
                if (doorobjlist[-Distance - 1].action != dooractiontypes.dr_open)
                    return;
                Distance = (int)MapConstants.TILEGLOBAL;
            }

            if (move < Distance)
            {
                MoveObj(move);
                break;
            }

            RecenterOnTile();
            move -= Distance;

            SelectBlakeChaseDir();
            if (Dir == objdirtypes.nodir)
                return;
        }
    }

    // Closing in dodges, unless it's a CHASEDIR actor (the floating bomb), which comes straight on
    private void SelectBlakeChaseDir()
    {
        if (HasFlag("CHASEDIR"))
            SelectChaseDir();
        else
            SelectDodgeDir();
    }

    /// <summary>
    /// Switches between shooting and closing in: shooting lasts 1-2 shots' worth of its count,
    /// closing in 60-119 tics. (Blake Stone uses the same count as the actor's ammo.)
    /// </summary>
    private void ChangeShootMode()
    {
        if (RuntimeFlags.HasFlag(objflags.FL_SHOOTMODE))
        {
            RuntimeFlags &= ~objflags.FL_SHOOTMODE;
            Ammo = (short)(60 + US_RndT() % 60);
        }
        else
        {
            RuntimeFlags |= objflags.FL_SHOOTMODE;
            Ammo = (short)(1 + US_RndT() % 2);
        }
    }

    /// <summary>
    /// Starts an attack: its Melee state when it has one and the player is within a tile, else
    /// Attack. Not at all past its `monster.attackrange` (tiles; the floating bomb's 1), nor
    /// closer than its `monster.attackmindist`, and only `monster.attackchance` times in 256
    /// (the liquid alien rising).
    /// </summary>
    private void DoAttack()
    {
        int dx = Math.Abs(TileX - player.TileX), dy = Math.Abs(TileY - player.TileY);
        int dist = Math.Max(Math.Max(dx, dy), 1);
        if (PropertyInt("monster.attackrange", 0) is > 0 and var range && dist > range)
            return;
        if (PropertyInt("monster.attackmindist", 0) is > 0 and var mindist && dist < mindist)
            return;
        if (PropertyInt("monster.attackchance", 256) is var chance and < 256 && US_RndT() >= chance)
            return;
        SetState(dist <= 1 && ResolvedStates.ContainsKey("Melee") ? "Melee" : "Attack");
    }

    /*
    =============================================================================

                                    SHOOT

    =============================================================================
    */

    internal override void Shoot()
    {
        bool smart = HasFlag("SMART");
        if (smart && Ammo == 0)
            return;

        if (AreaNumber < _mapManager.Floors.NumAreas && areabyplayer[AreaNumber] == 0)
            return;
        if (!CheckLine(this))
            return;     // the player is behind a wall

        ShotAtPlayer();
        PlayActorSound(this, "attacksound");

        if (smart)
        {
            Ammo--;
            CheckRunChase();
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
    private int CheckRunChase()
    {
        int reason = 0;
        if (Ammo == 0)
            reason |= RR_AMMO;
        if (Hitpoints <= _mapManager.GetScaledHealth(this) >> 1)
            reason |= RR_HEALTH;
        if ((RuntimeFlags & (objflags.FL_FRIENDLY | objflags.FL_INTERROGATED)) == objflags.FL_INTERROGATED)
            reason |= RR_INTERROGATED;

        if (reason != 0)
        {
            if (!RuntimeFlags.HasFlag(objflags.FL_RUNAWAY))
            {
                Temp3 = 0;
                RuntimeFlags |= objflags.FL_RUNAWAY;
                Speed += RUNAWAY_SPEED;
            }
        }
        else if (RuntimeFlags.HasFlag(objflags.FL_RUNAWAY))
        {
            RuntimeFlags &= ~objflags.FL_RUNAWAY;
            Speed -= RUNAWAY_SPEED;
        }

        return reason;
    }

    /// <summary>
    /// Which way (in tiles) an actor closing in heads: at the player, or, for a SMART actor that
    /// should be running, at the pickup, door or far corner it's running for
    /// </summary>
    protected override void SeekDelta(out int deltax, out int deltay)
    {
        if (HasFlag("SMART") && CheckRunChase() is var whyRun and not 0)
        {
            if (SeekX == 0)
                GetCornerSeek();

            if (!LookForGoodies(whyRun))
            {
                if (TileX == SeekX && TileY == SeekY)
                {
                    GetCornerSeek();
                    RuntimeFlags &= ~objflags.FL_INTERROGATED;
                }

                deltax = SeekX - TileX;
                deltay = SeekY - TileY;
                return;
            }

            // It took something: still short, it stays put this time
            if (CheckRunChase() != 0)
            {
                deltax = deltay = 0;
                return;
            }
        }

        deltax = player.TileX - TileX;
        deltay = player.TileY - TileY;
    }

    // A running actor with nowhere better to go heads for one of these
    private static readonly byte[] SeekPointX = [32, 63, 32, 1];
    private static readonly byte[] SeekPointY = [1, 63, 32, 1];

    private void GetCornerSeek()
    {
        int point = US_RndT() & 3;
        RuntimeFlags &= ~objflags.FL_RUNTOSTATIC;
        SeekX = SeekPointX[point];
        SeekY = SeekPointY[point];
    }

    /// <summary>
    /// A running actor looks for what it needs: true when it got some (taking an item it's
    /// standing on, or, out of the player's sight and area, simply getting some), false when it
    /// has picked an item or a door to head for (SeekX/SeekY) or has nothing new
    /// </summary>
    private bool LookForGoodies(int reason)
    {
        // A scientist that turned mean backs off to a door, then (half the time) attacks
        bool justFindDoor = false;
        if ((reason & RR_INTERROGATED) != 0)
        {
            justFindDoor = true;
            if (US_RndT() < 128)
                RuntimeFlags &= ~objflags.FL_INTERROGATED;
        }

        int maxHealth = _mapManager.GetScaledHealth(this);

        // Out of the player's area and sight, it cheats
        if (player.AreaNumber != AreaNumber && !RuntimeFlags.HasFlag(objflags.FL_VISABLE))
        {
            if (Ammo == 0)
                Ammo += 8;
            if (Hitpoints <= maxHealth >> 1)
                Hitpoints += 10;
            return true;
        }

        if (!justFindDoor)
        {
            foreach (var item in _mapManager.GetActors())
            {
                if (item.IsRemoved || item.AreaNumber != AreaNumber)
                    continue;
                int ammoGain = item.PropertyInt("monster.ammogain", 0);
                int healthGain = item.PropertyInt("monster.healthgain", 0);
                if (ammoGain <= 0 && healthGain <= 0)
                    continue;

                // Standing on it: take it, if it's needed
                if (item.TileX == TileX && item.TileY == TileY)
                {
                    if (ammoGain > 0)
                    {
                        if (Ammo != 0)
                            continue;
                        Ammo += (short)ammoGain;
                    }
                    else
                    {
                        if (Hitpoints > maxHealth >> 1)
                            continue;
                        Hitpoints += (short)healthGain;
                    }

                    SeekX = 0;
                    item.RunState("Pickup");        // food leaves its wrapper
                    _mapManager.MarkForRemoval(item);
                    return true;
                }

                // Else maybe head for it
                if (!RuntimeFlags.HasFlag(objflags.FL_RUNTOSTATIC)
                    && ((reason & RR_AMMO) != 0 && ammoGain > 0 || (reason & RR_HEALTH) != 0 && healthGain > 0))
                {
                    RuntimeFlags |= objflags.FL_RUNTOSTATIC;
                    SeekX = item.TileX;
                    SeekY = item.TileY;
                    return false;
                }
            }
        }

        // Running for a door out of the room, when the room joins the player's
        if (AreaNumber < _mapManager.Floors.NumAreas && areabyplayer[AreaNumber] != 0)
        {
            if (RuntimeFlags.HasFlag(objflags.FL_RUNTOSTATIC))
                return false;   // already heading somewhere

            var doors = new List<int>();
            for (int door = 0; door < lastdoorobj && doors.Count < 8; door++)
                if (DoorLeadsOutOf(door, AreaNumber))
                    doors.Add(door);

            if (doors.Count > 0)
            {
                // Not the one it used last, unless it's the only one
                int index = US_RndT() % doors.Count;
                if (doors[index] + 1 == Temp3 && doors.Count > 1)
                    index = (index + 1) % doors.Count;

                int chosen = doors[index];
                Temp3 = (short)(chosen + 1);
                SeekX = (byte)doorobjlist[chosen].tilex;
                SeekY = (byte)doorobjlist[chosen].tiley;
                RuntimeFlags |= objflags.FL_RUNTOSTATIC;
            }
        }
        else if (RuntimeFlags.HasFlag(objflags.FL_RUNTOSTATIC))
        {
            // What it was after is gone, or it's far off: off to a corner
            SeekX = 0;
        }

        return false;
    }

    /// <summary>Whether a door joins an area to another and opens without a key (an actor's way out)</summary>
    private static bool DoorLeadsOutOf(int door, int area)
    {
        var d = doorobjlist[door];
        if (d.Lock.Length > 0)
            return false;

        int x = d.tilex, y = d.tiley;
        var (a, b) = d.vertical ? (_mapManager.AreaAt(x - 1, y), _mapManager.AreaAt(x + 1, y)) : (_mapManager.AreaAt(x, y - 1), _mapManager.AreaAt(x, y + 1));
        return a == area || b == area;
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
    private void TurningPathDir()
    {
        var point = _mapManager.PatrolPointAt(TileX, TileY);
        if (point != null)
            Dir = point.Dir;

        Distance = (int)MapConstants.TILEGLOBAL;
        bool randomTurn = HasFlag("RANDOMTURN") && US_RndT() > 180;
        bool cantWalk = !CanWalk();

        // A patrol point wins over a random turn
        if (cantWalk || randomTurn && point == null)
        {
            if (TryDir == (byte)objdirtypes.nodir)
                TryDir |= (byte)(US_RndT() & 128);      // which way round
            else
                Dir = (objdirtypes)(TryDir & 127);    // on from the last way tried

            if ((TryDir & 128) != 0)
                Dir = Dir == objdirtypes.east ? objdirtypes.southeast : Dir - 1;
            else
                Dir = Dir >= objdirtypes.southeast ? objdirtypes.east : Dir + 1;

            TryDir = (byte)((TryDir & 128) | (byte)Dir);
            if (!CanWalk())
                Dir = objdirtypes.nodir;
        }

        if (Dir != objdirtypes.nodir)
        {
            TryWalk();
            TryDir = (byte)objdirtypes.nodir;
        }
    }

    /// <summary>
    /// A STEPBACK actor that has walked into the player (MoveObj): it heads back to the tile it
    /// was coming from, the way it came, as far as it had already come
    /// </summary>
    private void StepBack()
    {
        var (dx, dy) = DirOffset(Dir);
        if (dx == 0 && dy == 0)
            return;
        TileX = (byte)(TileX - dx);
        TileY = (byte)(TileY - dy);
        Dir = opposite[(byte)Dir];
        Distance = (int)MapConstants.TILEGLOBAL - Distance;
        SyncPosition();
    }

    // The tile step for a direction, (0, 0) for none
    private static (int Dx, int Dy) DirOffset(objdirtypes dir) => dir switch
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
    private bool CanWalk()
    {
        var (dx, dy) = DirOffset(Dir);
        if (dx == 0 && dy == 0)
            return false;

        int x = TileX + dx, y = TileY + dy;
        if (x < 0 || y < 0 || x >= MapManager.MAPSIZE || y >= MapManager.MAPSIZE)
            return false;
        if (dx != 0 && dy != 0)
            return CHECKDIAG(x, y) && CHECKDIAG(TileX + dx, TileY) && CHECKDIAG(TileX, TileY + dy);

        switch (_mapManager.actorat[x, y])
        {
            case Wall or BlockingActor:
                return false;
            case Door door:
                return HasFlag("PHASEDOORS")
                    || !HasFlag("NODOORS") && (!HasFlag("NOLOCKEDDOORS") || doorobjlist[door.door].Lock.Length == 0);
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
    private bool WoundActor(int oldHitpoints)
    {
        if (!Properties.ContainsKey("monster.woundstages") || !ResolvedStates.ContainsKey("Wounded"))
            return false;

        int boundary = _mapManager.GetScaledHealth(this) / (Temp1 + 1) + 1;
        if (oldHitpoints / boundary == Hitpoints / boundary)
            return false;

        PlayActorSound(this, "woundsound");
        SetState("Wounded");
        RuntimeFlags &= ~objflags.FL_SHOOTABLE;
        Temp2 = (short)(5 * 60 + US_RndT() % 20 * 60);
        return true;
    }

    /// <summary>The think on a wounded actor's last, held frame: when its time's up and the player isn't on top of it, it gets up (Recover)</summary>
    internal void Wounded()
    {
        if (Temp2 > tics)
        {
            Temp2 -= (short)tics;
            return;
        }
        Temp2 = 0;

        if (Math.Abs(player.X - X) > MapConstants.TILEGLOBAL || Math.Abs(player.Y - Y) > MapConstants.TILEGLOBAL)
        {
            RuntimeFlags |= objflags.FL_SHOOTABLE;
            SetState("Recover");
        }
    }

    /// <summary>
    /// Sets a dying actor sliding back from its killer (T_BlowBack): as far as the weapon in
    /// hand's `weapon.knockback` when the player shot it, else the killer's own `knockback`
    /// (an explosion's), else 0x5000
    /// </summary>
    private void StartBlowBack(Entities.Actors.Actor? killer)
    {
        if (killer == null)
            return;

        int distance = ReferenceEquals(killer, player)
            ? (playerstate.weapon != null && int.TryParse(_inventoryManager.GetProperty(playerstate.weapon, "weapon.knockback")?.ToString(), out var k) ? k : 0)
            : killer.PropertyInt("knockback", DEFAULT_KNOCKBACK);
        if (distance <= 0)
            return;

        var angle = Math.Atan2(killer.Y - Y, X - killer.X);
        if (angle < 0)
            angle += Math.PI * 2;
        Angle = (short)((int)(angle / (Math.PI * 2) * ANGLES) % ANGLES);
        Temp3 = (short)Math.Min(distance, short.MaxValue);
        RuntimeFlags |= objflags.FL_SLIDING;
    }

    /// <summary>The think on Death frames: the body slides back a step each tic until it's gone its distance or hits something</summary>
    internal void BlowBack()
    {
        if (!RuntimeFlags.HasFlag(objflags.FL_SLIDING))
            return;

        int step = Math.Min((int)Temp3, SLIDE_SPEED);
        Temp3 -= (short)step;
        if (Temp3 <= 0)
            RuntimeFlags &= ~objflags.FL_SLIDING;

        int dx = MathUtils.FixedMul(step, costable[Angle]);
        int dy = -MathUtils.FixedMul(step, sintable[Angle]);
        if (!ClipMove(dx, dy))
            RuntimeFlags &= ~objflags.FL_SLIDING;

        TileX = (byte)(X >> (int)MapConstants.TILESHIFT);
        TileY = (byte)(Y >> (int)MapConstants.TILESHIFT);
        SyncPosition();
    }

    // Moves an actor unless that would put it into a wall, something solid or a door that isn't open
    private bool ClipMove(int dx, int dy)
    {
        int nx = X + dx, ny = Y + dy, size = (int)MINDIST;
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

        X = nx;
        Y = ny;
        return true;
    }

    /*
    =============================================================================

                                TALKING (interrogation)

    Holding use near a TALKATIVE actor that's friendly and in view, facing it, talks to it:
    it says something nice, or something mean and turns on the player. (An Informant gives a
    hint instead, and when talked to again its gifts.)

    The hints are messages in a VGAGRAPH text (`talk.hints`, e.g. InformantHints), "^XX"
    between them; the map says which, with mapdefs map-info `hint:<text>` codes (the code's low
    byte the message's number, counting from 1, and the room it's in the one it's about). The
    nice and mean sayings (`talk.friendly`, `talk.hostile`) work the same way.

    =============================================================================
    */


    /// <summary>Talks to it (LevelAI.TryTalk); true when it's an informant, which can be asked again sooner</summary>
    internal virtual bool TalkTo()
    {
        // Asked twice, or half the time anyway, it turns mean
        string list;
        if (RuntimeFlags.HasFlag(objflags.FL_MUSTATTACK) || (US_RndT() & 1) != 0)
        {
            RuntimeFlags &= ~objflags.FL_FRIENDLY;
            RuntimeFlags |= objflags.FL_INTERROGATED;
            list = PropertyStrings("talk.hostile").FirstOrDefault() ?? "";
        }
        else
        {
            RuntimeFlags |= objflags.FL_MUSTATTACK;
            list = PropertyStrings("talk.friendly").FirstOrDefault() ?? "";
        }
        Say(RandomSaying(list));
        return false;
    }

    /// <summary>Shows what it says (under its `talk.header`, in its `talk.style`), with its `talk.sound`</summary>
    protected void Say(string? said)
    {
        if (string.IsNullOrEmpty(said))
            return;

        var header = PropertyStrings("talk.header").FirstOrDefault();
        var text = string.IsNullOrEmpty(header) ? said : $"{_hudMessageManager.Localize(header)}\n\n{said}";
        _hudMessageManager.Show(Managers.HudMessageKind.Other, text, PropertyStrings("talk.style").FirstOrDefault());
        if (PropertyStrings("talk.sound").FirstOrDefault() is { Length: > 0 } sound)
            _audioManager.Play(sound);
    }

    // One of the map's sayings from a text, or any from the text when the map places none
    protected static string? RandomSaying(string listName)
    {
        if (string.IsNullOrEmpty(listName))
            return null;
        var placed = _mapManager.Hints.GetValueOrDefault(listName);
        if (placed is { Count: > 0 })
            return HintText(listName, placed[US_RndT() % placed.Count].Message);
        int count = HintMessages(listName).Count;
        return count == 0 ? null : HintText(listName, 1 + US_RndT() % count);
    }

    private static readonly Dictionary<string, List<string>> hinttexts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Message <paramref name="number"/> (from 1) of a hint text, or null</summary>
    protected static string? HintText(string listName, int number)
    {
        var messages = HintMessages(listName);
        return number >= 1 && number <= messages.Count ? messages[number - 1] : null;
    }

    /// <summary>
    /// A VGAGRAPH text's messages, split at its "^XX"s, with line breaks kept and its formatting
    /// codes (^FC color and the like) left out
    /// </summary>
    private static List<string> HintMessages(string listName)
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