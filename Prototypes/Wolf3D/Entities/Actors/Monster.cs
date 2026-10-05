using Wolf3D.Constants;
using Wolf3D.Enums;
using Wolf3D.Extensions;
using Wolf3D.Managers;
using static Wolf3D.Program;

namespace Wolf3D.Entities.Actors;

/// <summary>
/// An actor that moves, sees, fights and dies: Wolf3D's and Spear of Destiny's enemies, ported
/// from WL_STATE.C / WL_ACT2.C (Program.WL_STATE.cs / Program.WL_ACT2.cs). An actordefs class
/// is one when it has Monster as an ancestor (`parent: Monster`), which anything with health
/// or a think/action registered here needs. Its thinks and actions are registered below under
/// the names the actordefs use (`think: T_Chase`), and run on the actor itself; subclasses
/// (Blake Stone's, for one) override them.
/// </summary>
internal record Monster : Actor
{
    /// <summary>The thinks and actions only a Monster can run, by their actordefs names (and its subclasses').</summary>
    internal static void RegisterActions()
    {
        ActorActionRegistry.RegisterFor<Monster>("T_Stand", m => m.Stand());
        ActorActionRegistry.RegisterFor<Monster>("T_Path", m => m.Patrol());
        ActorActionRegistry.RegisterFor<Monster>("T_Chase", m => m.Chase());
        ActorActionRegistry.RegisterFor<Monster>("T_DogChase", m => m.DogChase());
        ActorActionRegistry.RegisterFor<Monster>("T_Bite", m => m.Bite());
        ActorActionRegistry.RegisterFor<Monster>("T_Ghosts", m => m.GhostChase());
        ActorActionRegistry.RegisterFor<Monster>("T_Schabb", m => m.DodgeAndRetreat("Attack"));
        ActorActionRegistry.RegisterFor<Monster>("T_SchabbThrow", m => m.ThrowNeedle());
        ActorActionRegistry.RegisterFor<Monster>("T_Gift", m => m.DodgeAndRetreat("Attack"));
        ActorActionRegistry.RegisterFor<Monster>("T_GiftThrow", m => m.ThrowRocket());
        ActorActionRegistry.RegisterFor<Monster>("T_Fat", m => m.DodgeAndRetreat("Attack"));
        ActorActionRegistry.RegisterFor<Monster>("T_Fake", m => m.FakeChase());
        ActorActionRegistry.RegisterFor<Monster>("T_FakeFire", m => m.ThrowFire());
        ActorActionRegistry.RegisterFor<Monster>("T_Shoot", m => m.Shoot());
        ActorActionRegistry.RegisterFor<Monster>("A_HitlerMorph", m => m.HitlerMorph());

        // Spear of Destiny's bosses
        ActorActionRegistry.RegisterFor<Monster>("T_Will", m => m.DodgeAndRetreat("Attack"));
        ActorActionRegistry.RegisterFor<Monster>("T_UShoot", m => m.UberShoot());
        ActorActionRegistry.RegisterFor<Monster>("A_FireProjectile", (m, args) => m.FireProjectile(args));
        ActorActionRegistry.RegisterFor<Monster>("A_StartAttack", m => m.StartAttack());
        ActorActionRegistry.RegisterFor<Monster>("A_Relaunch", m => m.Relaunch());
        ActorActionRegistry.RegisterFor<Monster>("A_Dormant", m => m.Dormant());
    }

    /// <summary>
    /// Warns (once, at startup) about each actordefs class that has health, or runs a think or
    /// action only a Monster can, without being a Monster: shooting it or its AI would do nothing.
    /// </summary>
    internal static void CheckClasses(Assets.ActorMetadata metadata)
    {
        foreach (var (name, data) in metadata.Actors)
        {
            Actor actor;
            try
            {
                actor = metadata.CreateActor(name, data);
            }
            catch (Exception)
            {
                continue;       // a broken parent chain is reported where it's spawned
            }

            var needs = actor.ResolvedStates.Values
                .SelectMany(f => new[] { f.Think, f.Action })
                .Select(call => (Call: call, Class: ActorActionRegistry.NeededClass(call)))
                .FirstOrDefault(n => n.Class != null && !n.Class.IsInstanceOfType(actor));
            if (needs.Class != null)
                Console.WriteLine($"Actor '{name}' runs {ActorActionRegistry.Parse(needs.Call!).Name} but isn't a {needs.Class.Name}: give it `parent: {needs.Class.Name}` (or a class descended from it).");
            else if (actor is not Monster && actor.Properties.Keys.Any(k => k == "health" || k.StartsWith("health.", StringComparison.Ordinal)))
                Console.WriteLine($"Actor '{name}' has health but isn't a Monster: give it `parent: Monster` (or a class descended from it).");
        }
    }

    internal void RecenterOnTile()
    {
        X = (int)((TileX << MapConstants.TILESHIFT) + MapConstants.TILEGLOBAL / 2);
        Y = (int)((TileY << MapConstants.TILESHIFT) + MapConstants.TILEGLOBAL / 2);
    }

    /*
    =============================================================================
    MOVEMENT / COLLISION (ported from Program.WL_STATE.cs)
    =============================================================================
    */

    internal void MoveObj(int move)
    {
        int newx = X, newy = Y;

        switch (Dir)
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

        // A NOTSOLID actor (Blake Stone's electro-spheres) goes right through the player
        if ((AreaNumber >= _mapManager.Floors.NumAreas || areabyplayer[AreaNumber] != 0) && !HasFlag("NOTSOLID"))
        {
            var deltax = Math.Abs(newx - player.X);
            var deltay = Math.Abs(newy - player.Y);

            if (deltax <= MINACTORDIST && deltay <= MINACTORDIST)
            {
                if (!Hidden || !PlayerTileInView())
                {
                    // TOUCHDAMAGE actors (ghosts, Spectres) hurt the player on contact
                    if (Flags.Contains("TOUCHDAMAGE", StringComparer.OrdinalIgnoreCase))
                        TakeDamage((int)(tics * 2), this);

                    OnBumpedPlayer();
                    return;
                }
            }
        }

        X = newx;
        Y = newy;
        Distance -= move;
    }

    internal bool TryWalk()
    {
        int doornumtile = -1;
        var cutsCorners = Name is "Dog" or "FakeHitler";

        switch (Dir)
        {
            case objdirtypes.north:
                if (cutsCorners)
                {
                    if (!CHECKDIAG(TileX, TileY - 1)) return false;
                }
                else
                {
                    int r = CheckSide(TileX, TileY - 1, ref doornumtile);
                    if (r == 0) return false;
                    if (r == 1) return true;
                }
                TileY--;
                break;

            case objdirtypes.northeast:
                if (!CHECKDIAG(TileX + 1, TileY - 1)) return false;
                if (!CHECKDIAG(TileX + 1, TileY)) return false;
                if (!CHECKDIAG(TileX, TileY - 1)) return false;
                TileX++; TileY--;
                break;

            case objdirtypes.east:
                if (cutsCorners)
                {
                    if (!CHECKDIAG(TileX + 1, TileY)) return false;
                }
                else
                {
                    int r = CheckSide(TileX + 1, TileY, ref doornumtile);
                    if (r == 0) return false;
                    if (r == 1) return true;
                }
                TileX++;
                break;

            case objdirtypes.southeast:
                if (!CHECKDIAG(TileX + 1, TileY + 1)) return false;
                if (!CHECKDIAG(TileX + 1, TileY)) return false;
                if (!CHECKDIAG(TileX, TileY + 1)) return false;
                TileX++; TileY++;
                break;

            case objdirtypes.south:
                if (cutsCorners)
                {
                    if (!CHECKDIAG(TileX, TileY + 1)) return false;
                }
                else
                {
                    int r = CheckSide(TileX, TileY + 1, ref doornumtile);
                    if (r == 0) return false;
                    if (r == 1) return true;
                }
                TileY++;
                break;

            case objdirtypes.southwest:
                if (!CHECKDIAG(TileX - 1, TileY + 1)) return false;
                if (!CHECKDIAG(TileX - 1, TileY)) return false;
                if (!CHECKDIAG(TileX, TileY + 1)) return false;
                TileX--; TileY++;
                break;

            case objdirtypes.west:
                if (cutsCorners)
                {
                    if (!CHECKDIAG(TileX - 1, TileY)) return false;
                }
                else
                {
                    int r = CheckSide(TileX - 1, TileY, ref doornumtile);
                    if (r == 0) return false;
                    if (r == 1) return true;
                }
                TileX--;
                break;

            case objdirtypes.northwest:
                if (!CHECKDIAG(TileX - 1, TileY - 1)) return false;
                if (!CHECKDIAG(TileX - 1, TileY)) return false;
                if (!CHECKDIAG(TileX, TileY - 1)) return false;
                TileX--; TileY--;
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
            Distance = -doornumtile - 1;
            SyncPosition();
            return true;
        }

        // A door tile has no area of its own, so a PHASEDOORS actor passing through one
        // keeps the area it came from, and so does one crossing a floor code that isn't an area
        if (_mapManager.actorat[TileX, TileY] is not Door && _mapManager.VALIDAREA(_mapManager.MAPSPOT(TileX, TileY, 0)))
            AreaNumber = (byte)(_mapManager.MAPSPOT(TileX, TileY, 0) - _mapManager.Floors.AreaTile);
        Distance = (int)MapConstants.TILEGLOBAL;
        SyncPosition();
        return true;
    }

    // CHECKDIAG(int, int) lives in Program.WL_STATE.cs -- it isn't tied to the mover, so
    // there's no Entities.Actors.Actor-typed overload.

    internal int CheckSide(int x, int y, ref int doornumtile)
    {
        var temp = _mapManager.actorat[x, y];
        if (temp != null)
        {
            if (temp is Wall or BlockingActor)
                return 0;
            if (temp is Door door)
            {
                // PHASEDOORS actors (ghosts, Spectres) drift straight through a door
                // without opening it. Don't set doornumtile for them: TryWalk would give
                // them a "waiting on door" Distance, which T_Ghosts doesn't handle (its
                // move loop then never ends).
                if (Flags.Contains("PHASEDOORS", StringComparer.OrdinalIgnoreCase))
                    return 2;

                // NOLOCKEDDOORS actors (Blake Stone's) don't open a door that needs a key, and
                // NODOORS ones (its liquid alien and electro-spheres) none at all
                if (HasFlag("NODOORS") || HasFlag("NOLOCKEDDOORS") && doorobjlist[door.door].Lock.Length > 0)
                    return 0;

                doornumtile = door.door;
                if (!(demorecord || demoplayback))
                {
                    OpenDoor(doornumtile);
                    Distance = -doornumtile - 1;
                    return 1;
                }
            }
        }
        else if (_mapManager.IsShootableActorAt(x, y))
        {
            return 0;       // another living actor is standing there
        }

        return 2; // continue
    }

    internal bool CheckSight()
    {
        if (AreaNumber < _mapManager.Floors.NumAreas && areabyplayer[AreaNumber] == 0)
            return false;

        var deltax = player.X - X;
        var deltay = player.Y - Y;

        if (deltax > -MINSIGHT && deltax < MINSIGHT && deltay > -MINSIGHT && deltay < MINSIGHT)
            return true;

        switch (Dir)
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

        return CheckLine(this);
    }

    // How long (tics) an actor takes to react once it has noticed the player: its
    // `monster.reactiondelay: [base, divisor]`, base plus a random 0-255 over divisor (no divisor,
    // or 0, for none). Wolf3D's grunts keep their vanilla delays without one.
    private short GetReactionDelay()
    {
        var delay = PropertyInts("monster.reactiondelay");
        if (delay.Count > 0)
            return (short)(delay[0] + (delay.Count > 1 && delay[1] > 0 ? US_RndT() / delay[1] : 0));

        return Name switch
        {
            "Guard" => (short)(1 + US_RndT() / 4),
            "Officer" => 2,
            "Mutant" or "SS" => (short)(1 + US_RndT() / 6),
            "Dog" => (short)(1 + US_RndT() / 8),
            _ => 1, // bosses
        };
    }

    internal virtual bool SightPlayer()
    {
        // A BLIND actor (Blake Stone's volatile material transports, which just go their way)
        // never goes after the player
        if (HasFlag("BLIND"))
            return false;

        if (RuntimeFlags.HasFlag(objflags.FL_ATTACKMODE))
            _gameEngineManager.Quit("An actor in ATTACKMODE called SightPlayer!");

        if (Temp2 != 0)
        {
            Temp2 -= (short)tics;
            if (Temp2 > 0)
                return false;
            Temp2 = 0;
        }
        else
        {
            if (AreaNumber < _mapManager.Floors.NumAreas && areabyplayer[AreaNumber] == 0)
                return false;

            if (RuntimeFlags.HasFlag(objflags.FL_AMBUSH))
            {
                if (!CheckSight())
                    return false;
                RuntimeFlags &= ~objflags.FL_AMBUSH;
            }
            else
            {
                // A NOTICEWHENSEEN actor (Blake Stone's aliens) notices the player once the player can see it
                bool seen = HasFlag("NOTICEWHENSEEN") && RuntimeFlags.HasFlag(objflags.FL_VISABLE);
                if (!madenoise && !seen && !CheckSight())
                    return false;
            }

            Temp2 = GetReactionDelay();
            RuntimeFlags &= ~objflags.FL_FRIENDLY;   // it has seen the player: no longer friendly
            return false;
        }

        FirstSighting();
        return true;
    }

    internal void FirstSighting()
    {
        PlayActorSound(this, "seesound");

        SetState("Chase");

        // Chasing is faster than patrolling: either a set speed (the Ubermutant's 3000) or a
        // multiple of the actor's own
        if (Properties.TryGetValue("monster.chasespeed", out var chaseSpeed))
            Speed = Convert.ToInt32(chaseSpeed);
        else if (Properties.TryGetValue("monster.chasespeedmultiplier", out var mult))
            Speed *= Convert.ToInt32(mult);

        if (Distance < 0)
            Distance = 0;

        RuntimeFlags |= objflags.FL_ATTACKMODE | objflags.FL_FIRSTATTACK;
    }

    internal void SelectDodgeDir()
    {
        int deltax, deltay, i;
        uint absdx, absdy;
        var dirtry = new objdirtypes[5];
        objdirtypes tdir;
        objdirtypes turnaround;

        if (RuntimeFlags.HasFlag(objflags.FL_FIRSTATTACK))
        {
            turnaround = objdirtypes.nodir;
            RuntimeFlags &= ~objflags.FL_FIRSTATTACK;
        }
        else
            turnaround = opposite[(byte)Dir];

        SeekDelta(out deltax, out deltay);

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

            Dir = dirtry[i];
            if (TryWalk())
                return;
        }

        if (turnaround != objdirtypes.nodir)
        {
            Dir = turnaround;
            if (TryWalk())
                return;
        }

        Dir = objdirtypes.nodir;
    }

    internal void SelectChaseDir()
    {
        int deltax, deltay;
        var d = new objdirtypes[3];
        objdirtypes tdir, olddir;
        objdirtypes turnaround;

        olddir = Dir;
        turnaround = opposite[(byte)olddir];

        SeekDelta(out deltax, out deltay);

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
            Dir = d[1];
            if (TryWalk()) return;
        }

        if (d[2] != objdirtypes.nodir)
        {
            Dir = d[2];
            if (TryWalk()) return;
        }

        if (olddir != objdirtypes.nodir)
        {
            Dir = olddir;
            if (TryWalk()) return;
        }

        if (US_RndT() > 128)
        {
            for (tdir = objdirtypes.north; tdir <= objdirtypes.west; tdir++)
            {
                if (tdir != turnaround)
                {
                    Dir = tdir;
                    if (TryWalk()) return;
                }
            }
        }
        else
        {
            for (tdir = objdirtypes.west; tdir >= objdirtypes.north; tdir--)
            {
                if (tdir != turnaround)
                {
                    Dir = tdir;
                    if (TryWalk()) return;
                }
            }
        }

        if (turnaround != objdirtypes.nodir)
        {
            Dir = turnaround;
            if (Dir != objdirtypes.nodir)
            {
                if (TryWalk()) return;
            }
        }

        Dir = objdirtypes.nodir;
    }

    internal void SelectRunDir()
    {
        int deltax, deltay;
        var d = new objdirtypes[3];
        objdirtypes tdir;

        deltax = player.TileX - TileX;
        deltay = player.TileY - TileY;

        d[1] = deltax < 0 ? objdirtypes.east : objdirtypes.west;
        d[2] = deltay < 0 ? objdirtypes.south : objdirtypes.north;

        if (Math.Abs(deltay) > Math.Abs(deltax))
        {
            tdir = d[1]; d[1] = d[2]; d[2] = tdir;
        }

        Dir = d[1];
        if (TryWalk()) return;

        Dir = d[2];
        if (TryWalk()) return;

        if (US_RndT() > 128)
        {
            for (tdir = objdirtypes.north; tdir <= objdirtypes.west; tdir++)
            {
                Dir = tdir;
                if (TryWalk()) return;
            }
        }
        else
        {
            for (tdir = objdirtypes.west; tdir >= objdirtypes.north; tdir--)
            {
                Dir = tdir;
                if (TryWalk()) return;
            }
        }

        Dir = objdirtypes.nodir;
    }

    // Called as a patroller (T_Path, T_BJRun) reaches a tile's centre: a patrol point there
    // (actordefs PatrolPoint, placed on the legacy arrow tiles) turns it the way the point faces
    internal virtual void SelectPathDir()
    {
        if (_mapManager.PatrolPointAt(TileX, TileY) is { } point)
            Dir = point.Dir;

        Distance = (int)MapConstants.TILEGLOBAL;

        if (!TryWalk())
            Dir = objdirtypes.nodir;
    }

    /*
    =============================================================================
    COMBAT (ported from Program.WL_STATE.cs)
    =============================================================================
    */

    internal virtual void Kill(Entities.Actors.Actor? attacker = null)
    {
        var tilex = X >> (int)MapConstants.TILESHIFT;
        var tiley = Y >> (int)MapConstants.TILESHIFT;

        // `monster.floordeath`: on that floor (game-info floor-number) it isn't killed but goes
        // to its FloorDeath state, worth nothing yet (Goldfire morphing on Planet Strike's last)
        if (PropertyInt("monster.floordeath", -1) is >= 0 and var deathFloor && deathFloor == _mapManager.CurrentFloorNumber
            && ResolvedStates.ContainsKey("FloorDeath"))
        {
            SetState("FloorDeath");
            RuntimeFlags &= ~(objflags.FL_SHOOTABLE | objflags.FL_FRIENDLY);
            return;
        }

        AwardKillPoints();
        SetState("Death");
        OnKilled(attacker);

        if (LeavesDrops && DeathDrop() is { } drop)
            PlaceItemType(drop, tilex, tiley);
        CarriedDeathWork(this, tilex, tiley);

        if (Name is "Schabbs" or "Gift" or "Fat" or "RealHitler")
        {
            gamestate.killx = player.X;
            gamestate.killy = player.Y;
        }

        // A sleeper that becomes an enemy (`monster.becomes`) isn't the kill; that enemy is.
        // NOTCOUNTED actors (crates, Goldfire, who keeps coming back) aren't enemies to count.
        if (IsKill && !Properties.ContainsKey("monster.becomes"))
            gamestate.killcount++;
        RuntimeFlags &= ~(objflags.FL_SHOOTABLE | objflags.FL_FRIENDLY);
        RuntimeFlags |= objflags.FL_NONMARK;
    }

    /// <summary>Whether it counts toward the level's kills (its total, and the count as each dies); NOTCOUNTED actors don't</summary>
    internal virtual bool IsKill => !HasFlag("NOTCOUNTED");

    /// <summary>Whether dying, it leaves its `dropweapon`/`dropitem`</summary>
    protected virtual bool LeavesDrops => true;

    /// <summary>Its `points` for killing it</summary>
    protected virtual void AwardKillPoints()
    {
        if (!Properties.TryGetValue("points", out var points))
            return;

        // POINTSONCE actors (Spectres, which come back) only pay out the first time;
        // FL_BONUS is set at spawn and cleared here, as in the original
        if (!Flags.Contains("POINTSONCE", StringComparer.OrdinalIgnoreCase))
            GivePoints(Convert.ToInt32(points));
        else if (RuntimeFlags.HasFlag(objflags.FL_BONUS))
        {
            GivePoints(Convert.ToInt32(points));
            RuntimeFlags &= ~objflags.FL_BONUS;
        }
    }

    /// <summary>Just after it has gone to its Death state, killed by <paramref name="attacker"/> (if known)</summary>
    protected virtual void OnKilled(Actor? attacker) { }

    /// <summary>
    /// What a dying actor leaves: its `dropweapon` while the player hasn't got it (by default,
    /// while they hold nothing as good, by weapon.selectionorder, as the SS's machine gun; with
    /// `dropweapon.ifmissing`, while they haven't got that weapon, as Blake Stone's guards), else
    /// its `dropitem`. With `dropitem.alt`, the alt instead `dropitem.altchance` times in 256, and
    /// always once it's out of ammo (`monster.ammo`). `dropitem.needsammo`: nothing at all once
    /// it's out of ammo.
    /// </summary>
    private string? DeathDrop()
    {
        if (Properties.TryGetValue("dropweapon", out var dropweapon) && dropweapon is string weaponName)
        {
            if (PropertyBool("dropweapon.ifmissing"))
            {
                if (!_inventoryManager.Has(weaponName))
                    return weaponName;
            }
            else
            {
                var best = BestWeapon();
                if (best == null || WeaponSelectionOrder(best) > WeaponSelectionOrder(weaponName))
                    return weaponName;
            }
        }

        // `dropitem.onfloor`: only on that floor (game-info floor-number), and only once a level
        // (Goldfire's gold card, on floor 9)
        if (PropertyInt("dropitem.onfloor", -1) is >= 0 and var floor)
        {
            if (floor != _mapManager.CurrentFloorNumber || !_mapManager.AI.TakeFloorDrop())
                return null;
        }

        bool outOfAmmo = Properties.ContainsKey("monster.ammo") && Ammo == 0;
        if (outOfAmmo && PropertyBool("dropitem.needsammo"))
            return null;

        var drop = Properties.TryGetValue("dropitem", out var dropitem) ? dropitem as string : null;
        if (Properties.TryGetValue("dropitem.alt", out var alt) && alt is string altName
            && (outOfAmmo || US_RndT() < PropertyInt("dropitem.altchance", 128)))
            drop = altName;
        return drop;
    }

    internal virtual void Damage(uint damage, Entities.Actors.Actor? attacker = null)
    {
        // `monster.minweapon` (the hanging turret's rapid assault weapon): only that weapon, or a
        // better one (by weapon.selectionorder), in Blake's hand hurts it
        if (Properties.TryGetValue("monster.minweapon", out var minWeapon) && minWeapon is string minWeaponName
            && (gamestate.weapon == null || WeaponSelectionOrder(gamestate.weapon) > WeaponSelectionOrder(minWeaponName)))
            return;

        // A silent weapon (weapon.silent, Blake Stone's auto-charge pistol) doesn't alert anyone
        if (!(ReferenceEquals(attacker, player) && PlayerWeaponIsSilent()))
            madenoise = true;

        if (!RuntimeFlags.HasFlag(objflags.FL_ATTACKMODE))
            damage <<= 1;

        int oldHitpoints = Hitpoints;
        Hitpoints -= (short)damage;
        CloakHit = true;

        if (Hitpoints <= 0)
        {
            Kill(attacker);
            return;
        }

        if (OnHurt(oldHitpoints))
            return;

        if (!RuntimeFlags.HasFlag(objflags.FL_ATTACKMODE) && NoticesWhenHurt)
            FirstSighting();

        // `monster.damagestates: [full, 3/4, 1/2, 1/4]` (the floating bomb, which looks more
        // battered as it's hurt): it goes to the one for the health it has left instead of Pain.
        // A `monster.painonce` actor (the projection generator) only flinches at its first hit.
        int fullHealth = _mapManager.GetScaledHealth(this);
        var damageStates = PropertyStrings("monster.damagestates");
        if (damageStates.Count > 0)
        {
            int stage = Hitpoints > 3 * fullHealth / 4 ? 0 : Hitpoints > fullHealth / 2 ? 1 : Hitpoints > fullHealth / 4 ? 2 : 3;
            SetState(damageStates[Math.Min(stage, damageStates.Count - 1)]);
        }
        // Legacy DamageActor alternated between two single-frame Pain variants by hitpoints
        // parity; the new actordefs' "Pain" group instead plays both frames in sequence
        // before falling through to Chase -- close enough visually, simpler to drive.
        else if (ResolvedStates.ContainsKey("Pain") && (!PropertyBool("monster.painonce") || oldHitpoints >= fullHealth))
            SetState("Pain");

        OnPain();
    }

    /// <summary>Hurt but still alive, before anything else: true when that's taken care of (no noticing the player, no pain)</summary>
    protected virtual bool OnHurt(int oldHitpoints) => false;

    /// <summary>Whether being hurt makes it go after the player</summary>
    protected virtual bool NoticesWhenHurt => true;

    /// <summary>Hurt, after it has gone to its pain state</summary>
    protected virtual void OnPain() { }

    /// <summary>Just spawned on the map at its tile (MapManager.SpawnThing), with its health and flags set</summary>
    internal virtual void OnSpawned(int tilex, int tiley) { }

    /// <summary>Its move was stopped by the player standing in the way</summary>
    protected virtual void OnBumpedPlayer() { }

    /// <summary>Which way (in tiles) it heads closing in (SelectDodgeDir, SelectChaseDir): at the player</summary>
    protected virtual void SeekDelta(out int deltax, out int deltay)
    {
        deltax = player.TileX - TileX;
        deltay = player.TileY - TileY;
    }

    /*
    =============================================================================
    PER-ENEMY THINK/ACTION FUNCTIONS (ported from Program.WL_ACT2.cs)
    =============================================================================
    */

    internal virtual void Stand() => SightPlayer();

    internal virtual void Patrol()
    {
        // A STATIONARY actor (Blake Stone's parked transports) stays put
        if (HasFlag("STATIONARY"))
            return;

        // A friendly patroller only looks for the player once there's been a noise
        if ((!RuntimeFlags.HasFlag(objflags.FL_FRIENDLY) || madenoise) && SightPlayer())
            return;

        if (Dir == objdirtypes.nodir)
        {
            SelectPathDir();
            if (Dir == objdirtypes.nodir)
                return;
        }

        var move = (int)(Speed * tics);

        while (move != 0)
        {
            if (Distance < 0)
            {
                OpenDoor(-Distance - 1);
                if (doorobjlist[-Distance - 1].action != (byte)dooractiontypes.dr_open)
                    return;
                Distance = (int)MapConstants.TILEGLOBAL;
                if (!(demorecord || demoplayback))
                    TryWalk();
            }

            if (move < Distance)
            {
                MoveObj(move);
                break;
            }

            if (TileX > MapManager.MAPSIZE || TileY > MapManager.MAPSIZE)
                _gameEngineManager.Quit($"T_Path hit a wall at {TileX},{TileY}, dir {Dir}");

            RecenterOnTile();
            move -= Distance;

            SelectPathDir();

            if (Dir == objdirtypes.nodir)
                return;
        }
    }

    internal virtual void Chase()
    {
        if (gamestate.victoryflag)
            return;

        var dodge = false;

        if (CheckLine(this))
        {
            Hidden = false;
            var dx = Math.Abs(TileX - player.TileX);
            var dy = Math.Abs(TileY - player.TileY);
            var dist = dx > dy ? dx : dy;
            int chance;

            if (demorecord || demoplayback)
            {
                chance = (dist == 0 || (dist == 1 && Distance < 0x4000)) ? 300 : (int)((tics << 4) / dist);
            }
            else
            {
                chance = dist != 0 ? (int)((tics << 4) / dist) : 300;

                if (dist == 1)
                {
                    var target = Math.Abs(X - player.X);
                    if (target < 0x14000L)
                    {
                        target = Math.Abs(Y - player.Y);
                        if (target < 0x14000L)
                            chance = 300;
                    }
                }
            }

            if (US_RndT() < chance)
            {
                SetState("Attack");
                return;
            }
            dodge = true;
        }
        else
            Hidden = true;

        if (Dir == objdirtypes.nodir)
        {
            if (dodge) SelectDodgeDir(); else SelectChaseDir();
            if (Dir == objdirtypes.nodir)
                return;
        }

        var move = (int)(Speed * tics);

        while (move != 0)
        {
            if (Distance < 0)
            {
                OpenDoor(-Distance - 1);
                if (doorobjlist[-Distance - 1].action != (byte)dooractiontypes.dr_open)
                    return;
                Distance = (int)MapConstants.TILEGLOBAL;
                if (!(demorecord || demoplayback))
                    TryWalk();
            }

            if (move < Distance)
            {
                MoveObj(move);
                break;
            }

            RecenterOnTile();
            move -= Distance;

            if (dodge) SelectDodgeDir(); else SelectChaseDir();

            if (Dir == objdirtypes.nodir)
                return;
        }
    }

    internal void DogChase()
    {
        if (Dir == objdirtypes.nodir)
        {
            SelectDodgeDir();
            if (Dir == objdirtypes.nodir)
                return;
        }

        var move = (int)(Speed * tics);

        while (move != 0)
        {
            var dx = player.X - X;
            if (dx < 0) dx = -dx;
            dx -= move;
            if (dx <= MINACTORDIST)
            {
                var dy = player.Y - Y;
                if (dy < 0) dy = -dy;
                dy -= move;
                if (dy <= MINACTORDIST)
                {
                    SetState("Attack");
                    return;
                }
            }

            if (move < Distance)
            {
                MoveObj(move);
                break;
            }

            RecenterOnTile();
            move -= Distance;

            SelectDodgeDir();
            if (Dir == objdirtypes.nodir)
                return;
        }
    }

    internal void Bite()
    {
        if (Properties.TryGetValue("attacksound", out var sound) && sound is string soundName)
            PlaySoundLocActor(soundName, this);

        var dx = player.X - X;
        if (dx < 0) dx = -dx;
        dx -= (int)MapConstants.TILEGLOBAL;
        if (dx <= MINACTORDIST)
        {
            var dy = player.Y - Y;
            if (dy < 0) dy = -dy;
            dy -= (int)MapConstants.TILEGLOBAL;
            if (dy <= MINACTORDIST)
            {
                if (US_RndT() < 180)
                    TakeDamage(US_RndT() >> 4, this);
            }
        }
    }

    internal void GhostChase()
    {
        if (Dir == objdirtypes.nodir)
        {
            SelectChaseDir();
            if (Dir == objdirtypes.nodir)
                return;
        }

        var move = (int)(Speed * tics);

        while (move != 0)
        {
            if (move < Distance)
            {
                MoveObj(move);
                break;
            }

            RecenterOnTile();
            move -= Distance;

            SelectChaseDir();
            if (Dir == objdirtypes.nodir)
                return;
        }
    }

    // Shared dodge/retreat AI for Schabbs/Gift/Fat: keeps a shot lined up when possible,
    // otherwise dodges toward the player (or runs, once very close).
    private void DodgeAndRetreat(string attackState)
    {
        var dx = Math.Abs(TileX - player.TileX);
        var dy = Math.Abs(TileY - player.TileY);
        var dist = dx > dy ? dx : dy;
        var dodge = false;

        if (CheckLine(this))
        {
            Hidden = false;
            if (US_RndT() < (tics << 3))
            {
                SetState(attackState);
                return;
            }
            dodge = true;
        }
        else
            Hidden = true;

        if (Dir == objdirtypes.nodir)
        {
            if (dodge) SelectDodgeDir(); else SelectChaseDir();
            if (Dir == objdirtypes.nodir)
                return;
        }

        var move = (int)(Speed * tics);

        while (move != 0)
        {
            if (Distance < 0)
            {
                OpenDoor(-Distance - 1);
                if (doorobjlist[-Distance - 1].action != (byte)dooractiontypes.dr_open)
                    return;
                Distance = (int)MapConstants.TILEGLOBAL;
                TryWalk();
            }

            if (move < Distance)
            {
                MoveObj(move);
                break;
            }

            RecenterOnTile();
            move -= Distance;

            if (dist < 4) SelectRunDir();
            else if (dodge) SelectDodgeDir();
            else SelectChaseDir();

            if (Dir == objdirtypes.nodir)
                return;
        }
    }

    internal void FakeChase()
    {
        if (CheckLine(this))
        {
            Hidden = false;
            if (US_RndT() < (tics << 1))
            {
                SetState("Attack");
                return;
            }
        }
        else
            Hidden = true;

        if (Dir == objdirtypes.nodir)
        {
            SelectDodgeDir();
            if (Dir == objdirtypes.nodir)
                return;
        }

        var move = (int)(Speed * tics);

        while (move != 0)
        {
            if (move < Distance)
            {
                MoveObj(move);
                break;
            }

            RecenterOnTile();
            move -= Distance;

            SelectDodgeDir();
            if (Dir == objdirtypes.nodir)
                return;
        }
    }

    internal virtual void Shoot()
    {
        if (AreaNumber < _mapManager.Floors.NumAreas && areabyplayer[AreaNumber] == 0)
            return;

        if (CheckLine(this))
            ShotAtPlayer();

        // The shooter's own attacksound (vanilla's per-class switch: SSFIRE, BOSSFIRE, ... and
        // NAZIFIRE for the rest, Spear's bosses included)
        PlayActorSound(this, "attacksound");
    }

    /// <summary>
    /// A hitscan shot at the player, who's in sight: more likely to hit the nearer it is, less
    /// when the player is running, and less again when the shooter is in view (the player can
    /// dodge). `monster.sharpshooter` actors count the distance as two thirds of it.
    /// </summary>
    internal void ShotAtPlayer()
    {
        var dx = Math.Abs(TileX - player.TileX);
        var dy = Math.Abs(TileY - player.TileY);
        var dist = dx > dy ? dx : dy;

        if (Properties.ContainsKey("monster.sharpshooter"))
            dist = dist * 2 / 3;

        int hitchance;
        if (thrustspeed >= RUNSPEED)
            hitchance = RuntimeFlags.HasFlag(objflags.FL_VISABLE) ? 160 - dist * 16 : 160 - dist * 8;
        else
            hitchance = RuntimeFlags.HasFlag(objflags.FL_VISABLE) ? 256 - dist * 16 : 256 - dist * 8;

        if (US_RndT() < hitchance)
        {
            int damage = dist < 2 ? US_RndT() >> 2 : dist < 4 ? US_RndT() >> 3 : US_RndT() >> 4;
            TakeDamage(damage, this);
        }
    }

    // Spawns a projectile actor (Needle/Rocket/Fire, actordefs/wolf3d/projectiles.yaml) at the
    // thrower and aims it at the player. TicCount 1 makes its first frame expire on the very
    // next tic, so the state's Action (a rocket's first smoke puff) fires almost immediately.
    // angleOffset turns the shot away from the player, in ANGLES units (the Death Knight's
    // rockets go 4 either side). With no sound given, the projectile's own attacksound plays.
    private void ThrowProjectile(string className, int speed, string? sound = null, int angleOffset = 0)
    {
        var deltax = player.X - X;
        var deltay = Y - player.Y;
        var angle = (float)Math.Atan2((float)deltay, (float)deltax);
        if (angle < 0) angle = (float)(M_PI * 2 + angle);
        var iangle = (int)(angle / (M_PI * 2) * ANGLES);
        iangle = ((iangle + angleOffset) % ANGLES + ANGLES) % ANGLES;

        var newobj = _mapManager.SpawnAtActor(className, this);
        if (newobj == null)
            return;

        newobj.TicCount = 1;
        newobj.Angle = (short)iangle;
        newobj.Speed = speed;
        newobj.Shooter = this;

        if (sound == null && newobj.Properties.TryGetValue("attacksound", out var attackSound))
            sound = attackSound as string;
        if (!string.IsNullOrEmpty(sound))
            PlaySoundLocActor(sound, newobj);
    }

    // Each plays the thrown projectile's attacksound (projectiles.yaml)
    internal void ThrowNeedle() =>
        ThrowProjectile("Needle", 0x2000);

    internal void ThrowRocket() =>
        ThrowProjectile("Rocket", 0x2000);

    internal void ThrowFire() =>
        ThrowProjectile("Fire", 0x1200);

    /*
    =============================================================================
    SPEAR OF DESTINY BOSSES (ported from wl_act2.cpp's SPEAR section)
    =============================================================================
    */

    // Wilhelm, the Death Knight and the Angel chase like Schabbs: attack when there's a clear
    // shot, otherwise dodge toward the player, and back off once within four tiles

    // The Ubermutant's volley: a normal shot, plus 10 more damage when right beside the player
    internal void UberShoot()
    {
        Shoot();

        var dx = Math.Abs(TileX - player.TileX);
        var dy = Math.Abs(TileY - player.TileY);
        if (Math.Max(dx, dy) <= 1)
            TakeDamage(10, this);
    }

    /// <summary>
    /// A_FireProjectile("HRocket"[, angle offset[, "shoot"]]): launches a projectile at the player,
    /// turned by the offset in ANGLES units; with "shoot", also fires a gun volley (T_Shoot), as the
    /// Death Knight does with each rocket.
    /// </summary>
    internal void FireProjectile(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("A_FireProjectile: no projectile given.");
            return;
        }

        var angleOffset = args.Length > 1 && int.TryParse(args[1], out var offset) ? offset : 0;
        if (args.Skip(2).Any(a => a.Equals("shoot", StringComparison.OrdinalIgnoreCase)))
            Shoot();

        // The projectile's own `speed` (plus up to `speed.random` more) and `spread` (aimed up to
        // that many degrees either side of the player, at random), else 0x2000 dead on
        int speed = 0x2000;
        if (RuntimeActorProperty(args[0], "speed") is { } baseSpeed)
            speed = baseSpeed + (RuntimeActorProperty(args[0], "speed.random") is { } extra and > 0 ? US_RndT() * extra / 256 : 0);
        if (RuntimeActorProperty(args[0], "spread") is { } spread and > 0)
            angleOffset += US_RndT() % (spread * 2 + 1) - spread;

        ThrowProjectile(args[0], speed, angleOffset: angleOffset);
    }

    // A whole-number property of an actordefs class, or null
    static int? RuntimeActorProperty(string className, string key) =>
        _inventoryManager.GetProperty(className, key) is { } value && int.TryParse(value.ToString(), out var n) ? n : null;

    // The Angel's spark volley: A_StartAttack starts the count, A_Relaunch follows each spark and
    // either tires the Angel out after the third, breaks off at random, or goes again
    internal void StartAttack() => Temp1 = 0;

    internal void Relaunch()
    {
        if (++Temp1 == 3)
        {
            SetState("Tired");
            return;
        }

        if ((US_RndT() & 1) != 0)
            SetState("Chase");
    }


    // A dead Spectre comes back once nothing is in the way: the player isn't right on top of
    // it, and the tiles it covers hold no wall, door, blocking object or live enemy
    internal void Dormant()
    {
        var deltax = X - player.X;
        var deltay = Y - player.Y;
        if (deltax >= -MINACTORDIST && deltax <= MINACTORDIST && deltay >= -MINACTORDIST && deltay <= MINACTORDIST)
            return;

        var xl = (int)((X - MINDIST) >> (int)MapConstants.TILESHIFT);
        var xh = (int)((X + MINDIST) >> (int)MapConstants.TILESHIFT);
        var yl = (int)((Y - MINDIST) >> (int)MapConstants.TILESHIFT);
        var yh = (int)((Y + MINDIST) >> (int)MapConstants.TILESHIFT);

        for (var y = yl; y <= yh; y++)
            for (var x = xl; x <= xh; x++)
            {
                if (_mapManager.actorat[x, y] != null || _mapManager.IsShootableActorAt(x, y))
                    return;
            }

        RuntimeFlags |= objflags.FL_AMBUSH | objflags.FL_SHOOTABLE;
        RuntimeFlags &= ~(objflags.FL_ATTACKMODE | objflags.FL_NONMARK);
        Dir = objdirtypes.nodir;
        SetState("Spawn");
    }

    internal void HitlerMorph()
    {
        var newActor = _mapManager.SpawnMorphedEnemy("RealHitler", this);
        if (newActor == null)
            return;

        newActor.Speed = SPDPATROL * 5;

        if (!loadedgame)
            gamestate.killtotal++;
    }
}