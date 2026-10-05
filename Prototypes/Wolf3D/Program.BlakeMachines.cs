using Wolf3D.Constants;

namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                        BLAKE STONE'S MACHINES AND SPECIALS

    The rest of Blake Stone's actors, as bstone has them (3d_act1.cpp, 3d_act2.cpp), for any
    actor to use: electro-spheres bouncing about, the liquid alien rising out of its puddle,
    hanging turrets turning to find the player, security lights, steam vents, ooze, the floating
    bomb blowing itself up, projection generators, enemies coming out of wall outlets (mapdefs
    walls `outlet:`) and Goldfire warping in at his spawn sites (WARPSITE markers).

    =============================================================================
    */

    // Set once a level's `dropitem.onfloor` item has been dropped (DeathDrop)
    static bool floordropgiven;

    /// <summary>Sets up a fresh or loaded level's outlets and warp sites (SetupGameLevel)</summary>
    internal static void InitLevelSpawners()
    {
        floordropgiven = false;
        InitOutlets();
        InitWarpSites();
    }

    /// <summary>Runs the level's outlets and warp sites, once a tic (PlayLoop)</summary>
    internal static void TickLevelSpawners()
    {
        TickOutlets();
        TickWarpSites();
    }

    /*
    =============================================================================

                                ELECTRO-SPHERES

    =============================================================================
    */

    /// <summary>
    /// The think of a bouncing actor (`monster.bounce`: horizontal, vertical or diagonal): it
    /// goes straight until blocked, then back the way it came (a diagonal one turns 90 degrees
    /// first if it can). Within a tile of the player it zaps them (`monster.touchdamage`, with
    /// its `touchsound`). It doesn't open doors.
    /// </summary>
    internal static void T_Bounce(Entities.Actors.Actor ob)
    {
        if (Math.Max(Math.Abs(player.X - ob.X), Math.Abs(player.Y - ob.Y)) < MapConstants.TILEGLOBAL
            && ob.PropertyInt("monster.touchdamage", 0) is > 0 and var zap)
        {
            PlayActorSound(ob, "touchsound");
            TakeDamage(zap, ob);
        }

        if (ob.Dir == objdirtypes.nodir)
        {
            BounceStartDir(ob);
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

            if (BackToDiagonal(ob))
                continue;
            if (CanWalk(ob))
            {
                TryWalk(ob);
                continue;
            }

            // Blocked: a diagonal one tries a right angle either way, then everything goes back
            if (IsDiagonal(ob.Dir))
            {
                var left = (objdirtypes)(((int)ob.Dir + 2) % 8);
                var right = opposite[(byte)left];
                if (TryBounceDir(ob, left) || TryBounceDir(ob, right))
                    continue;
            }

            if (!TryBounceDir(ob, opposite[(byte)ob.Dir]))
            {
                ob.Dir = objdirtypes.nodir;
                return;
            }
        }
    }

    static bool IsDiagonal(objdirtypes dir) => dir is objdirtypes.northeast or objdirtypes.northwest or objdirtypes.southeast or objdirtypes.southwest;

    static bool TryBounceDir(Entities.Actors.Actor ob, objdirtypes dir)
    {
        var old = ob.Dir;
        ob.Dir = dir;
        if (CanWalk(ob))
        {
            TryWalk(ob);
            return true;
        }
        ob.Dir = old;
        return false;
    }

    static readonly string[] BounceKinds = ["vertical", "horizontal", "diagonal"];

    // A random way for its kind of bounce, or the other way if that's blocked; boxed in, the
    // other kinds in turn (a diagonal one in a corridor goes up and down it)
    static void BounceStartDir(Entities.Actors.Actor ob)
    {
        var kind = ob.PropertyStrings("monster.bounce").FirstOrDefault()?.ToLowerInvariant() ?? "diagonal";
        int first = Math.Max(Array.IndexOf(BounceKinds, kind), 0);
        for (int i = 0; i < BounceKinds.Length; i++)
        {
            var dir = BounceKinds[(first + i) % BounceKinds.Length] switch
            {
                "vertical" => (US_RndT() & 1) != 0 ? objdirtypes.north : objdirtypes.south,
                "horizontal" => (US_RndT() & 1) != 0 ? objdirtypes.east : objdirtypes.west,
                _ => (objdirtypes)((US_RndT() % 4) * 2 + 1),
            };
            if (TryBounceDir(ob, dir) || TryBounceDir(ob, opposite[(byte)dir]))
                return;
        }
        ob.Dir = objdirtypes.nodir;
    }

    // A diagonal bouncer going straight (it was boxed in) takes the first diagonal it can, once there's room
    static bool BackToDiagonal(Entities.Actors.Actor ob)
    {
        if (IsDiagonal(ob.Dir) || ob.PropertyStrings("monster.bounce").FirstOrDefault()?.ToLowerInvariant() is "vertical" or "horizontal")
            return false;
        foreach (var dir in new[] { objdirtypes.northeast, objdirtypes.northwest, objdirtypes.southwest, objdirtypes.southeast })
            if (TryBounceDir(ob, dir))
                return true;
        return false;
    }

    /*
    =============================================================================

                                LIQUID ALIEN

    =============================================================================
    */

    /// <summary>
    /// A_SetShootable("on"/"off"): makes the actor shootable (and in the way), or not, as the
    /// liquid alien rising out of its puddle and sinking back
    /// </summary>
    internal static void A_SetShootable(Entities.Actors.Actor ob, string[] args)
    {
        if (args.FirstOrDefault()?.Equals("off", StringComparison.OrdinalIgnoreCase) == true)
            ob.RuntimeFlags &= ~objflags.FL_SHOOTABLE;
        else
            ob.RuntimeFlags |= objflags.FL_SHOOTABLE;
    }

    /// <summary>The liquid alien moving as a puddle (Chase): near the player, but not right by them, it rises (Rise)</summary>
    internal static void T_LiquidMove(Entities.Actors.Actor ob)
    {
        int dx = Math.Abs(ob.TileX - player.TileX), dy = Math.Abs(ob.TileY - player.TileY);
        if (Math.Max(dx, dy) < 6 && dx > 1 && dy > 1)
            NewActorState(ob, "Rise");
        else
            T_BlakeChase(ob);
    }

    /// <summary>
    /// The liquid alien standing up (Stand, its Temp2 the shots it has fired): it shoots again
    /// (Shoot) now and then, up to five times, else (with the player more than a tile off) sinks
    /// back (Fall) when the player can't see it, sometimes anyway, and after its fifth shot
    /// </summary>
    internal static void T_LiquidStand(Entities.Actors.Actor ob)
    {
        ob.RuntimeFlags |= objflags.FL_SHOOTABLE;
        if (US_RndT() < 80 && ob.Temp2 < 5)
        {
            ob.Temp2++;
            NewActorState(ob, "Shoot");
            return;
        }

        if (Math.Abs(ob.TileX - player.TileX) > 1 || Math.Abs(ob.TileY - player.TileY) > 1)
        {
            if (!ob.RuntimeFlags.HasFlag(objflags.FL_VISABLE) || US_RndT() < 40 || ob.Temp2 == 5)
            {
                ob.RuntimeFlags &= ~objflags.FL_SHOOTABLE;
                ob.Temp2 = 0;
                NewActorState(ob, "Fall");
            }
        }
        else
            ob.Temp2 = 0;
    }

    /*
    =============================================================================

                                HANGING TURRET

    =============================================================================
    */

    const int SEEK_TURN_DELAY = 30;

    /// <summary>
    /// A turret's think: facing the player (within its eighth of the circle, 15 tiles, in a clear
    /// line), it fires (Attack), more likely the nearer the player is; else a turning one turns
    /// round a step every half second (a STATIONARY one keeps facing its way)
    /// </summary>
    internal static void T_Seek(Entities.Actors.Actor ob)
    {
        bool found = false;
        if ((player.TileX != ob.TileX || player.TileY != ob.TileY) && CheckView(ob))
        {
            int dx = Math.Abs(ob.TileX - player.TileX), dy = Math.Abs(ob.TileY - player.TileY);
            if (dx < 15 && dy < 15)
            {
                int dist = Math.Max(dx, dy);
                int chance = dist == 0 || dist == 1 && ob.Distance < 0x4000 ? 300 : US_RndT() / dist;
                if (US_RndT() < chance)
                {
                    NewActorState(ob, "Attack");
                    return;
                }
                found = true;
            }
        }

        if (ob.HasFlag("STATIONARY") || found)
            return;

        ob.Temp2 -= (short)tics;
        if (ob.Temp2 <= 0)
        {
            ob.Temp2 = SEEK_TURN_DELAY;
            ob.Dir = ob.Dir >= objdirtypes.southeast ? objdirtypes.east : ob.Dir + 1;
        }
    }

    /// <summary>Whether the player is in the actor's eighth of the circle (its facing, 22.5 degrees either side) in a clear line</summary>
    static bool CheckView(Entities.Actors.Actor ob)
    {
        if (ob.AreaNumber < _mapManager.Floors.NumAreas && areabyplayer[ob.AreaNumber] == 0)
            return false;
        if (ob.Dir == objdirtypes.nodir)
            return CheckLine(ob);

        var angle = Math.Atan2(ob.Y - player.Y, player.X - ob.X) * 180 / Math.PI;
        double facing = (int)ob.Dir * 45;
        double diff = Math.Abs((angle - facing + 540) % 360 - 180);
        return diff <= 22.5 && CheckLine(ob);
    }

    /*
    =============================================================================

                        SECURITY LIGHTS, STEAM, OOZE

    =============================================================================
    */

    /// <summary>A security light's think: once there's been a noise where the player can hear it, it starts flashing (Alert)</summary>
    internal static void T_SecurityLight(Entities.Actors.Actor ob)
    {
        if (madenoise && (ob.AreaNumber >= _mapManager.Floors.NumAreas || areabyplayer[ob.AreaNumber] != 0))
            NewActorState(ob, "Alert");
    }

    /// <summary>
    /// A steam vent's think: while the player can see it, it counts down (Temp2, first four
    /// seconds) and lets off steam (Release), then waits up to 34 seconds for the next
    /// </summary>
    internal static void T_SteamVent(Entities.Actors.Actor ob)
    {
        if (!ob.RuntimeFlags.HasFlag(objflags.FL_VISABLE))
            return;
        if (ob.Temp3 == 0)
        {
            ob.Temp3 = 1;
            ob.Temp2 = 4 * 60;
        }
        ob.Temp2 -= (short)tics;
        if (ob.Temp2 <= 0)
        {
            ob.Temp2 = (short)(US_RndT() << 3);
            NewActorState(ob, "Release");
        }
    }

    /// <summary>A_HurtPlayerHere(damage[, chance]): the player standing on the actor's tile is hurt, chance times in 256 (ooze)</summary>
    internal static void A_HurtPlayerHere(Entities.Actors.Actor ob, string[] args)
    {
        int damage = args.Length > 0 && int.TryParse(args[0], out var d) ? d : 1;
        int chance = args.Length > 1 && int.TryParse(args[1], out var c) ? c : 256;
        if (player.TileX == ob.TileX && player.TileY == ob.TileY && US_RndT() < chance)
            TakeDamage(damage, ob);
    }

    /// <summary>
    /// A_HoldNearPlayer(damage): a think for a barrier rising (Planet Strike's v-posts and
    /// v-spikes, bstone's T_BarrierTransition closing): while the player is within 1.5 tiles
    /// its frame doesn't run down, so it can't shut on them, and within half a tile they're
    /// hurt for damage each tic.
    /// </summary>
    internal static void A_HoldNearPlayer(Entities.Actors.Actor ob, string[] args)
    {
        int damage = args.Length > 0 && int.TryParse(args[0], out var d) ? d : 0;
        long dx = Math.Abs(player.X - ob.X), dy = Math.Abs(player.Y - ob.Y);
        if (dx > 0x18000 || dy > 0x18000)
            return;
        if (damage > 0 && dx <= 0x8000 && dy <= 0x8000)
            TakeDamage(damage, ob);
        ob.TicCount += (short)tics;
    }

    /// <summary>
    /// A_JumpIfUntagged(state): an actor nothing is tagged to work (no tag, nor a map-info
    /// tag-link) goes to the state (Planet Strike's switchable barriers with no switch cycle by
    /// themselves, as bstone's do)
    /// </summary>
    internal static void A_JumpIfUntagged(Entities.Actors.Actor ob, string[] args)
    {
        if (ob.Tag == 0 && args.Length > 0 && ob.ResolvedStates.TryGetValue(args[0], out var frame))
            ob.JumpTo(frame);
    }

    /// <summary>A_SelfDestruct: the actor dies, with its points (the floating bomb, reaching the player, blows itself up)</summary>
    internal static void A_SelfDestruct(Entities.Actors.Actor ob)
    {
        if (!ob.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE))
            return;
        ob.Hitpoints = 0;
        KillActor(ob, null);

        // Called from a state's action: land on the first Death frame, not the one after it
        if (ob.ResolvedStates.TryGetValue("Death", out var death))
            ob.JumpTo(death);
    }

    /*
    =============================================================================

                            PROJECTION GENERATORS

    =============================================================================
    */

    /// <summary>
    /// A_CountRemaining("message"): shows the message with %n the number of this actor's class
    /// still standing (shootable), as each projection generator goes
    /// </summary>
    internal static void A_CountRemaining(Entities.Actors.Actor ob, string[] args)
    {
        if (args.Length == 0)
            return;
        int left = _mapManager.GetActors().Count(a => !a.IsRemoved && a.Name == ob.Name && a.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE));
        _hudMessageManager.Show(Managers.HudMessageKind.Other, args[0], args.ElementAtOrDefault(1),
            new Dictionary<char, string> { ['n'] = left.ToString() });
    }

    /// <summary>A_VictoryIfLast: the level is won once none of this actor's class still stands (the last projection generator)</summary>
    internal static void A_VictoryIfLast(Entities.Actors.Actor ob)
    {
        if (!_mapManager.GetActors().Any(a => !a.IsRemoved && a.Name == ob.Name && a.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE)))
            playstate = playstatetypes.ex_victorious;
    }

    /*
    =============================================================================

                                WALL OUTLETS

    A wall with an `outlet:` (mapdefs walls) lets its actor out onto open floor beside it, in
    a room that joins the player's: every so many seconds (a 0xFA value on its object-plane tile,
    else its delay), as long as fewer than its max for the skill are about. One comes out where
    the player can see it, or now and then anyway, never within a tile of the player.

    =============================================================================
    */

    sealed class Outlet
    {
        public int X, Y, Delay;
        public required Assets.MapOutletTranslation Spec;
    }

    static List<Outlet> outlets = [];

    static void InitOutlets()
    {
        outlets = [];
        var walls = _mapManager.GetMapData().Walls;
        for (int y = 0; y < _mapManager.mapheight; y++)
            for (int x = 0; x < _mapManager.mapwidth; x++)
                if (walls.TryGetValue(_mapManager.MAPSPOT(x, y, 0), out var wall) && wall.Outlet is { Class.Length: > 0 } spec)
                {
                    var outlet = new Outlet { X = x, Y = y, Spec = spec };
                    outlet.Delay = OutletDelay(outlet);
                    outlets.Add(outlet);
                }
    }

    static int OutletDelay(Outlet outlet)
    {
        int spot = _mapManager.MAPSPOT(outlet.X, outlet.Y, 1);
        if ((spot & 0xff00) == 0xfa00)
            return 60 * (spot & 0xff);
        int min = outlet.Spec.Delay.ElementAtOrDefault(0), max = Math.Max(outlet.Spec.Delay.ElementAtOrDefault(1), min);
        return 60 * min + US_RndT() * 60 * (max - min) / 256;
    }

    // An actor that came out of an outlet keeps its tile (SeekX/SeekY, plus one), so each
    // outlet knows how many of its own are about
    static int OutletCount(Outlet outlet) =>
        _mapManager.GetActors().Count(a => !a.IsRemoved && a.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE)
            && a.SeekX == outlet.X + 1 && a.SeekY == outlet.Y + 1 && a.Name == outlet.Spec.Class);

    static void TickOutlets()
    {
        foreach (var outlet in outlets)
        {
            var max = outlet.Spec.Max;
            if (max.Count == 0 || OutletCount(outlet) >= max[Math.Min(gamestate.difficulty, max.Count - 1)])
                continue;

            if (outlet.Delay > tics)
            {
                outlet.Delay -= (int)tics;
                continue;
            }
            outlet.Delay = 1;       // try again next tic, unless one comes out

            // Beside it, in a room joined to the player's
            (int X, int Y)? spot = null;
            foreach (var (dx, dy) in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
            {
                int area = AreaAt(outlet.X + dx, outlet.Y + dy);
                if (area >= 0 && area < _mapManager.Floors.NumAreas && areabyplayer[area] != 0)
                {
                    spot = (outlet.X + dx, outlet.Y + dy);
                    break;
                }
            }
            if (spot is not { } tile || _mapManager.actorat[tile.X, tile.Y] != null || _mapManager.IsShootableActorAt(tile.X, tile.Y)
                || Math.Abs(player.TileX - tile.X) < 2 && Math.Abs(player.TileY - tile.Y) < 2)
                continue;

            // Where the player can see it, or now and then anyway
            if (!PlayerCanSee(tile.X, tile.Y) && US_RndT() < 200)
                continue;

            var spawned = _mapManager.SpawnThing(tile.X, tile.Y, new Assets.MapActorTranslation { Class = outlet.Spec.Class, Angles = -1 }, countKill: false);
            if (spawned != null)
            {
                spawned.SeekX = (byte)(outlet.X + 1);
                spawned.SeekY = (byte)(outlet.Y + 1);
                if (outlet.Spec.Sound.Length > 0)
                    PlaySoundLocActor(outlet.Spec.Sound, spawned);
            }
            outlet.Delay = OutletDelay(outlet);
            break;
        }
    }

    /// <summary>Whether the player has a clear line to a tile's centre</summary>
    static bool PlayerCanSee(int tilex, int tiley)
    {
        var probe = new Entities.Actors.Actor { Name = "" };
        probe.SetPosition(tilex, tiley);
        return CheckLine(probe);
    }

    /*
    =============================================================================

                            WARP SITES (Goldfire)

    A WARPSITE marker's `warpsite.class` (Goldfire) warps in at one of the level's sites: one
    the player can see (not where he last did, when there are others), after a wait (first
    `warpsite.firstwait`, then `warpsite.wait`, both [min, max] seconds; one second on its
    `warpsite.quickfloor`), once nothing's on it and the player is more than a tile off. Only one
    is about at a time; when it dies (A_WarpSiteGone) the next wait starts.

    =============================================================================
    */

    static readonly List<Entities.Actors.Actor> warpsites = [];
    static int warpwait, warplast = -1, warpchosen = -1;
    static bool warpfirst = true;
    static string? warpclass;

    static void InitWarpSites()
    {
        warpsites.Clear();
        warpsites.AddRange(_mapManager.GetActors().Where(a => a.HasFlag("WARPSITE")));
        warpclass = warpsites.Select(s => s.PropertyStrings("warpsite.class").FirstOrDefault()).FirstOrDefault(c => !string.IsNullOrEmpty(c));
        warpfirst = true;
        warplast = warpchosen = -1;
        warpwait = warpsites.Count > 0 ? WarpWait(warpsites[0], first: true) : 0;
    }

    static int WarpWait(Entities.Actors.Actor site, bool first)
    {
        if (site.PropertyInt("warpsite.quickfloor", -1) == _mapManager.CurrentFloorNumber)
            return 60;
        var range = site.PropertyInts(first ? "warpsite.firstwait" : "warpsite.wait");
        int min = range.ElementAtOrDefault(0), max = Math.Max(range.ElementAtOrDefault(1), min);
        return 60 * min + US_RndT() * 60 * (max - min) / 256;
    }

    static void TickWarpSites()
    {
        if (warpsites.Count == 0 || warpclass == null)
            return;
        if (_mapManager.GetActors().Any(a => !a.IsRemoved && a.Name == warpclass))
            return;     // it's about (or still warping out)

        if (warpwait > tics)
        {
            warpwait -= (int)tics;
            return;
        }

        if (warpchosen < 0)
        {
            // Pick a site the player can see
            warpwait = 0;
            for (int i = 0; i < warpsites.Count; i++)
            {
                if (warpsites.Count > 1 && i == warplast)
                    continue;
                if (!PlayerCanSee(warpsites[i].TileX, warpsites[i].TileY))
                    continue;
                warpchosen = warplast = i;
                warpwait = WarpWait(warpsites[i], warpfirst);
                break;
            }
            return;
        }

        var site = warpsites[warpchosen];
        if (_mapManager.IsShootableActorAt(site.TileX, site.TileY)
            || Math.Abs(player.TileX - site.TileX) <= 1 || Math.Abs(player.TileY - site.TileY) <= 1)
            return;

        _mapManager.SpawnThing(site.TileX, site.TileY, new Assets.MapActorTranslation { Class = warpclass }, countKill: false);
        warpfirst = false;
        warpchosen = -1;
    }

    /// <summary>A_WarpSiteGone: the warp site's actor has gone (warped out); the wait for the next starts</summary>
    internal static void A_WarpSiteGone(Entities.Actors.Actor ob)
    {
        if (warpsites.Count > 0)
            warpwait = WarpWait(warpsites[0], first: false);
        warpchosen = -1;
    }

    /// <summary>A_WarpSitesOff: no more comes; the level's warp sites go (Goldfire, morphed)</summary>
    internal static void A_WarpSitesOff(Entities.Actors.Actor ob)
    {
        foreach (var site in warpsites)
            _mapManager.MarkForRemoval(site);
        warpsites.Clear();
        warpchosen = -1;
    }

    /// <summary>
    /// A_FireSpread("SpectorShot", chance, health, lowchance, 24, 16, 8): a projectile at the
    /// player and, chance times in 256 (lowchance below that health), one either side at each
    /// offset in ANGLES units too (bstone's morphed Goldfire)
    /// </summary>
    internal static void A_FireSpread(Entities.Actors.Actor ob, string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("A_FireSpread: no projectile given.");
            return;
        }

        A_FireProjectile(ob, [args[0]]);

        int Arg(int i, int fallback) => args.Length > i && int.TryParse(args[i], out var n) ? n : fallback;
        int chance = ob.Hitpoints < Arg(2, 0) ? Arg(3, 0) : Arg(1, 0);
        if (US_RndT() >= chance)
            return;
        for (int i = 4; i < args.Length; i++)
        {
            int offset = Arg(i, 0);
            A_FireProjectile(ob, [args[0], offset.ToString()]);
            A_FireProjectile(ob, [args[0], (-offset).ToString()]);
        }
    }
}
