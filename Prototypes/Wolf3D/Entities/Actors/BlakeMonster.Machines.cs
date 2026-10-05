using Wolf3D.Constants;
using Wolf3D.Extensions;
using Wolf3D.Managers;
using static Wolf3D.Program;

namespace Wolf3D.Entities.Actors;

// Blake Stone's machines and specials, as bstone has them (3d_act1.cpp, 3d_act2.cpp):
// electro-spheres bouncing about, the liquid alien rising out of its puddle, hanging turrets
// turning to find the player, security lights, steam vents, ooze, the floating bomb blowing
// itself up, projection generators, Planet Strike's barriers.
internal partial record BlakeMonster
{
    private static void RegisterMachineActions()
    {
        ActorActionRegistry.RegisterFor<BlakeMonster>("T_Bounce", m => m.Bounce());
        ActorActionRegistry.RegisterFor<BlakeMonster>("A_SetShootable", (m, args) => m.SetShootable(args));
        ActorActionRegistry.RegisterFor<BlakeMonster>("T_LiquidMove", m => m.LiquidMove());
        ActorActionRegistry.RegisterFor<BlakeMonster>("T_LiquidStand", m => m.LiquidStand());
        ActorActionRegistry.RegisterFor<BlakeMonster>("T_Seek", m => m.Seek());
        ActorActionRegistry.RegisterFor<BlakeMonster>("T_SecurityLight", m => m.SecurityLight());
        ActorActionRegistry.RegisterFor<BlakeMonster>("T_SteamVent", m => m.SteamVent());
        ActorActionRegistry.RegisterFor<BlakeMonster>("A_HurtPlayerHere", (m, args) => m.HurtPlayerHere(args));
        ActorActionRegistry.RegisterFor<BlakeMonster>("A_HoldNearPlayer", (m, args) => m.HoldNearPlayer(args));
        ActorActionRegistry.RegisterFor<BlakeMonster>("A_JumpIfUntagged", (m, args) => m.JumpIfUntagged(args));
        ActorActionRegistry.RegisterFor<BlakeMonster>("A_SelfDestruct", m => m.SelfDestruct());
        ActorActionRegistry.RegisterFor<BlakeMonster>("A_CountRemaining", (m, args) => m.CountRemaining(args));
        ActorActionRegistry.RegisterFor<BlakeMonster>("A_VictoryIfLast", m => m.VictoryIfLast());
        ActorActionRegistry.RegisterFor<BlakeMonster>("A_FireSpread", (m, args) => m.FireSpread(args));
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
    internal void Bounce()
    {
        if (Math.Max(Math.Abs(player.X - X), Math.Abs(player.Y - Y)) < MapConstants.TILEGLOBAL
            && PropertyInt("monster.touchdamage", 0) is > 0 and var zap)
        {
            PlayActorSound(this, "touchsound");
            TakeDamage(zap, this);
        }

        if (Dir == objdirtypes.nodir)
        {
            BounceStartDir();
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

            if (BackToDiagonal())
                continue;
            if (CanWalk())
            {
                TryWalk();
                continue;
            }

            // Blocked: a diagonal one tries a right angle either way, then everything goes back
            if (IsDiagonal(Dir))
            {
                var left = (objdirtypes)(((int)Dir + 2) % 8);
                var right = opposite[(byte)left];
                if (TryBounceDir(left) || TryBounceDir(right))
                    continue;
            }

            if (!TryBounceDir(opposite[(byte)Dir]))
            {
                Dir = objdirtypes.nodir;
                return;
            }
        }
    }

    private static bool IsDiagonal(objdirtypes dir) => dir is objdirtypes.northeast or objdirtypes.northwest or objdirtypes.southeast or objdirtypes.southwest;

    private bool TryBounceDir(objdirtypes dir)
    {
        var old = Dir;
        Dir = dir;
        if (CanWalk())
        {
            TryWalk();
            return true;
        }
        Dir = old;
        return false;
    }

    private static readonly string[] BounceKinds = ["vertical", "horizontal", "diagonal"];

    // A random way for its kind of bounce, or the other way if that's blocked; boxed in, the
    // other kinds in turn (a diagonal one in a corridor goes up and down it)
    private void BounceStartDir()
    {
        var kind = PropertyStrings("monster.bounce").FirstOrDefault()?.ToLowerInvariant() ?? "diagonal";
        int first = Math.Max(Array.IndexOf(BounceKinds, kind), 0);
        for (int i = 0; i < BounceKinds.Length; i++)
        {
            var dir = BounceKinds[(first + i) % BounceKinds.Length] switch
            {
                "vertical" => (US_RndT() & 1) != 0 ? objdirtypes.north : objdirtypes.south,
                "horizontal" => (US_RndT() & 1) != 0 ? objdirtypes.east : objdirtypes.west,
                _ => (objdirtypes)((US_RndT() % 4) * 2 + 1),
            };
            if (TryBounceDir(dir) || TryBounceDir(opposite[(byte)dir]))
                return;
        }
        Dir = objdirtypes.nodir;
    }

    // A diagonal bouncer going straight (it was boxed in) takes the first diagonal it can, once there's room
    private bool BackToDiagonal()
    {
        if (IsDiagonal(Dir) || PropertyStrings("monster.bounce").FirstOrDefault()?.ToLowerInvariant() is "vertical" or "horizontal")
            return false;
        foreach (var dir in new[] { objdirtypes.northeast, objdirtypes.northwest, objdirtypes.southwest, objdirtypes.southeast })
            if (TryBounceDir(dir))
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
    internal void SetShootable(string[] args)
    {
        if (args.FirstOrDefault()?.Equals("off", StringComparison.OrdinalIgnoreCase) == true)
            RuntimeFlags &= ~objflags.FL_SHOOTABLE;
        else
            RuntimeFlags |= objflags.FL_SHOOTABLE;
    }

    /// <summary>The liquid alien moving as a puddle (Chase): near the player, but not right by them, it rises (Rise)</summary>
    internal void LiquidMove()
    {
        int dx = Math.Abs(TileX - player.TileX), dy = Math.Abs(TileY - player.TileY);
        if (Math.Max(dx, dy) < 6 && dx > 1 && dy > 1)
            SetState("Rise");
        else
            Chase();
    }

    /// <summary>
    /// The liquid alien standing up (Stand, its Temp2 the shots it has fired): it shoots again
    /// (Shoot) now and then, up to five times, else (with the player more than a tile off) sinks
    /// back (Fall) when the player can't see it, sometimes anyway, and after its fifth shot
    /// </summary>
    internal void LiquidStand()
    {
        RuntimeFlags |= objflags.FL_SHOOTABLE;
        if (US_RndT() < 80 && Temp2 < 5)
        {
            Temp2++;
            SetState("Shoot");
            return;
        }

        if (Math.Abs(TileX - player.TileX) > 1 || Math.Abs(TileY - player.TileY) > 1)
        {
            if (!RuntimeFlags.HasFlag(objflags.FL_VISABLE) || US_RndT() < 40 || Temp2 == 5)
            {
                RuntimeFlags &= ~objflags.FL_SHOOTABLE;
                Temp2 = 0;
                SetState("Fall");
            }
        }
        else
            Temp2 = 0;
    }

    /*
    =============================================================================

                                HANGING TURRET

    =============================================================================
    */

    private const int SEEK_TURN_DELAY = 30;

    /// <summary>
    /// A turret's think: facing the player (within its eighth of the circle, 15 tiles, in a clear
    /// line), it fires (Attack), more likely the nearer the player is; else a turning one turns
    /// round a step every half second (a STATIONARY one keeps facing its way)
    /// </summary>
    internal void Seek()
    {
        bool found = false;
        if ((player.TileX != TileX || player.TileY != TileY) && CheckView())
        {
            int dx = Math.Abs(TileX - player.TileX), dy = Math.Abs(TileY - player.TileY);
            if (dx < 15 && dy < 15)
            {
                int dist = Math.Max(dx, dy);
                int chance = dist == 0 || dist == 1 && Distance < 0x4000 ? 300 : US_RndT() / dist;
                if (US_RndT() < chance)
                {
                    SetState("Attack");
                    return;
                }
                found = true;
            }
        }

        if (HasFlag("STATIONARY") || found)
            return;

        Temp2 -= (short)tics;
        if (Temp2 <= 0)
        {
            Temp2 = SEEK_TURN_DELAY;
            Dir = Dir >= objdirtypes.southeast ? objdirtypes.east : Dir + 1;
        }
    }

    /// <summary>Whether the player is in the actor's eighth of the circle (its facing, 22.5 degrees either side) in a clear line</summary>
    private bool CheckView()
    {
        if (AreaNumber < _mapManager.Floors.NumAreas && areabyplayer[AreaNumber] == 0)
            return false;
        if (Dir == objdirtypes.nodir)
            return CheckLine(this);

        var angle = Math.Atan2(Y - player.Y, player.X - X) * 180 / Math.PI;
        double facing = (int)Dir * 45;
        double diff = Math.Abs((angle - facing + 540) % 360 - 180);
        return diff <= 22.5 && CheckLine(this);
    }

    /*
    =============================================================================

                        SECURITY LIGHTS, STEAM, OOZE

    =============================================================================
    */

    /// <summary>A security light's think: once there's been a noise where the player can hear it, it starts flashing (Alert)</summary>
    internal void SecurityLight()
    {
        if (madenoise && (AreaNumber >= _mapManager.Floors.NumAreas || areabyplayer[AreaNumber] != 0))
            SetState("Alert");
    }

    /// <summary>
    /// A steam vent's think: while the player can see it, it counts down (Temp2, first four
    /// seconds) and lets off steam (Release), then waits up to 34 seconds for the next
    /// </summary>
    internal void SteamVent()
    {
        if (!RuntimeFlags.HasFlag(objflags.FL_VISABLE))
            return;
        if (Temp3 == 0)
        {
            Temp3 = 1;
            Temp2 = 4 * 60;
        }
        Temp2 -= (short)tics;
        if (Temp2 <= 0)
        {
            Temp2 = (short)(US_RndT() << 3);
            SetState("Release");
        }
    }

    /// <summary>A_HurtPlayerHere(damage[, chance]): the player standing on the actor's tile is hurt, chance times in 256 (ooze)</summary>
    internal void HurtPlayerHere(string[] args)
    {
        int damage = args.Length > 0 && int.TryParse(args[0], out var d) ? d : 1;
        int chance = args.Length > 1 && int.TryParse(args[1], out var c) ? c : 256;
        if (player.TileX == TileX && player.TileY == TileY && US_RndT() < chance)
            TakeDamage(damage, this);
    }

    /// <summary>
    /// A_HoldNearPlayer(damage): a think for a barrier rising (Planet Strike's v-posts and
    /// v-spikes, bstone's T_BarrierTransition closing): while the player is within 1.5 tiles
    /// its frame doesn't run down, so it can't shut on them, and within half a tile they're
    /// hurt for damage each tic.
    /// </summary>
    internal void HoldNearPlayer(string[] args)
    {
        int damage = args.Length > 0 && int.TryParse(args[0], out var d) ? d : 0;
        long dx = Math.Abs(player.X - X), dy = Math.Abs(player.Y - Y);
        if (dx > 0x18000 || dy > 0x18000)
            return;
        if (damage > 0 && dx <= 0x8000 && dy <= 0x8000)
            TakeDamage(damage, this);
        TicCount += (short)tics;
    }

    /// <summary>
    /// A_JumpIfUntagged(state): an actor nothing is tagged to work (no tag, nor a map-info
    /// tag-link) goes to the state (Planet Strike's switchable barriers with no switch cycle by
    /// themselves, as bstone's do)
    /// </summary>
    internal void JumpIfUntagged(string[] args)
    {
        if (Tag == 0 && args.Length > 0 && ResolvedStates.TryGetValue(args[0], out var frame))
            JumpTo(frame);
    }

    /// <summary>A_SelfDestruct: the actor dies, with its points (the floating bomb, reaching the player, blows itself up)</summary>
    internal void SelfDestruct()
    {
        if (!RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE))
            return;
        Hitpoints = 0;
        Kill(null);

        // Called from a state's action: land on the first Death frame, not the one after it
        if (ResolvedStates.TryGetValue("Death", out var death))
            JumpTo(death);
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
    internal void CountRemaining(string[] args)
    {
        if (args.Length == 0)
            return;
        int left = _mapManager.GetActors().Count(a => !a.IsRemoved && a.Name == Name && a.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE));
        _hudMessageManager.Show(Managers.HudMessageKind.Other, args[0], args.ElementAtOrDefault(1),
            new Dictionary<char, string> { ['n'] = left.ToString() });
    }

    /// <summary>A_VictoryIfLast: the level is won once none of this actor's class still stands (the last projection generator)</summary>
    internal void VictoryIfLast()
    {
        if (!_mapManager.GetActors().Any(a => !a.IsRemoved && a.Name == Name && a.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE)))
            playstate = playstatetypes.ex_victorious;
    }

    /// <summary>
    /// A_FireSpread("SpectorShot", chance, health, lowchance, 24, 16, 8): a projectile at the
    /// player and, chance times in 256 (lowchance below that health), one either side at each
    /// offset in ANGLES units too (bstone's morphed Goldfire)
    /// </summary>
    internal void FireSpread(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("A_FireSpread: no projectile given.");
            return;
        }

        FireProjectile([args[0]]);

        int Arg(int i, int fallback) => args.Length > i && int.TryParse(args[i], out var n) ? n : fallback;
        int chance = Hitpoints < Arg(2, 0) ? Arg(3, 0) : Arg(1, 0);
        if (US_RndT() >= chance)
            return;
        for (int i = 4; i < args.Length; i++)
        {
            int offset = Arg(i, 0);
            FireProjectile([args[0], offset.ToString()]);
            FireProjectile([args[0], (-offset).ToString()]);
        }
    }
}