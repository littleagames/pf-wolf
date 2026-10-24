using PFWolf.Constants;
using PFWolf.Extensions;
using PFWolf.Managers;
using static PFWolf.Gameplay.GameConstants;
using static PFWolf.Gameplay.LineOfSight;
using static PFWolf.Rendering.TrigTables;

namespace PFWolf.Entities.Actors;

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
        if (Math.Max(Math.Abs(Player.X - X), Math.Abs(Player.Y - Y)) < MapConstants.TileGlobal
            && PropertyInt("monster.touchdamage", 0) is > 0 and var zap)
        {
            PlayActorSound(this, "touchsound");
            TakeDamage(zap, this);
        }

        if (Dir == ObjDir.NoDir)
        {
            BounceStartDir();
            if (Dir == ObjDir.NoDir)
                return;
        }

        var move = (int)(Speed * Tics);
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
                var left = (ObjDir)(((int)Dir + 2) % 8);
                var right = Opposite[(byte)left];
                if (TryBounceDir(left) || TryBounceDir(right))
                    continue;
            }

            if (!TryBounceDir(Opposite[(byte)Dir]))
            {
                Dir = ObjDir.NoDir;
                return;
            }
        }
    }

    private static bool IsDiagonal(ObjDir dir) => dir is ObjDir.NorthEast or ObjDir.NorthWest or ObjDir.SouthEast or ObjDir.SouthWest;

    private bool TryBounceDir(ObjDir dir)
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
                "vertical" => (NextRandom() & 1) != 0 ? ObjDir.North : ObjDir.South,
                "horizontal" => (NextRandom() & 1) != 0 ? ObjDir.East : ObjDir.West,
                _ => (ObjDir)((NextRandom() % 4) * 2 + 1),
            };
            if (TryBounceDir(dir) || TryBounceDir(Opposite[(byte)dir]))
                return;
        }
        Dir = ObjDir.NoDir;
    }

    // A diagonal bouncer going straight (it was boxed in) takes the first diagonal it can, once there's room
    private bool BackToDiagonal()
    {
        if (IsDiagonal(Dir) || PropertyStrings("monster.bounce").FirstOrDefault()?.ToLowerInvariant() is "vertical" or "horizontal")
            return false;
        foreach (var dir in new[] { ObjDir.NorthEast, ObjDir.NorthWest, ObjDir.SouthWest, ObjDir.SouthEast })
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
            RuntimeFlags &= ~ObjFlags.Shootable;
        else
            RuntimeFlags |= ObjFlags.Shootable;
    }

    /// <summary>The liquid alien moving as a puddle (Chase): near the player, but not right by them, it rises (Rise)</summary>
    internal void LiquidMove()
    {
        int dx = Math.Abs(TileX - Target.TileX), dy = Math.Abs(TileY - Target.TileY);
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
        RuntimeFlags |= ObjFlags.Shootable;
        if (NextRandom() < 80 && Temp2 < 5)
        {
            Temp2++;
            SetState("Shoot");
            return;
        }

        if (Math.Abs(TileX - Target.TileX) > 1 || Math.Abs(TileY - Target.TileY) > 1)
        {
            if (!RuntimeFlags.Has(ObjFlags.Visible) || NextRandom() < 40 || Temp2 == 5)
            {
                RuntimeFlags &= ~ObjFlags.Shootable;
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

    private const int SeekTurnDelay = 30;

    /// <summary>
    /// A turret's think: facing the player (within its eighth of the circle, 15 tiles, in a clear
    /// line), it fires (Attack), more likely the nearer the player is; else a turning one turns
    /// round a step every half second (a STATIONARY one keeps facing its way)
    /// </summary>
    internal void Seek()
    {
        bool found = false;
        var target = Target;
        if ((target.TileX != TileX || target.TileY != TileY) && CheckView())
        {
            int dx = Math.Abs(TileX - target.TileX), dy = Math.Abs(TileY - target.TileY);
            if (dx < 15 && dy < 15)
            {
                int dist = Math.Max(dx, dy);
                int chance = dist == 0 || dist == 1 && Distance < 0x4000 ? 300 : NextRandom() / dist;
                if (NextRandom() < chance)
                {
                    SetState("Attack");
                    return;
                }
                found = true;
            }
        }

        if (HasFlag("STATIONARY") || found)
            return;

        Temp2 -= (short)Tics;
        if (Temp2 <= 0)
        {
            Temp2 = SeekTurnDelay;
            Dir = Dir >= ObjDir.SouthEast ? ObjDir.East : Dir + 1;
        }
    }

    /// <summary>Whether what it's after is in the actor's eighth of the circle (its facing, 22.5 degrees either side) in a clear line</summary>
    private bool CheckView()
    {
        if (AreaNumber < MapManager.Floors.NumAreas && AreaByPlayer[AreaNumber] == 0)
            return false;
        var target = Target;
        if (Dir == ObjDir.NoDir)
            return CheckLine(this, target);

        var angle = Math.Atan2(Y - target.Y, target.X - X) * 180 / Math.PI;
        double facing = (int)Dir * 45;
        double diff = Math.Abs((angle - facing + 540) % 360 - 180);
        return diff <= 22.5 && CheckLine(this, target);
    }

    /*
    =============================================================================

                        SECURITY LIGHTS, STEAM, OOZE

    =============================================================================
    */

    /// <summary>
    /// A security light's think: once there's been a noise where the player can hear it, it
    /// starts flashing (Alert). A noise made this tic or the last counts (bstone's `alerted`,
    /// which outlasts `madenoise` by a tic), so a light that thinks before the noise's maker
    /// still sees it.
    /// </summary>
    internal void SecurityLight()
    {
        if ((MadeNoise || World.Session.NoiseLastTic) && (AreaNumber >= MapManager.Floors.NumAreas || AreaByPlayer[AreaNumber] != 0))
            SetState("Alert");
    }

    /// <summary>
    /// A steam vent's think: while the player can see it, it counts down (Temp2, first four
    /// seconds) and lets off steam (Release), then waits up to 34 seconds for the next
    /// </summary>
    internal void SteamVent()
    {
        if (!RuntimeFlags.Has(ObjFlags.Visible))
            return;
        if (Temp3 == 0)
        {
            Temp3 = 1;
            Temp2 = 4 * 60;
        }
        Temp2 -= (short)Tics;
        if (Temp2 <= 0)
        {
            Temp2 = (short)(NextRandom() << 3);
            SetState("Release");
        }
    }

    /// <summary>A_HurtPlayerHere(damage[, chance]): the player standing on the actor's tile is hurt, chance times in 256 (ooze)</summary>
    internal void HurtPlayerHere(string[] args)
    {
        int damage = args.Length > 0 && int.TryParse(args[0], out var d) ? d : 1;
        int chance = args.Length > 1 && int.TryParse(args[1], out var c) ? c : 256;
        if (Player.TileX == TileX && Player.TileY == TileY && NextRandom() < chance)
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
        long dx = Math.Abs(Player.X - X), dy = Math.Abs(Player.Y - Y);
        if (dx > 0x18000 || dy > 0x18000)
            return;
        if (damage > 0 && dx <= 0x8000 && dy <= 0x8000)
            TakeDamage(damage, this);
        TicCount += (short)Tics;
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
        if (!RuntimeFlags.Has(ObjFlags.Shootable))
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
        int left = MapManager.GetActors().Count(a => !a.IsRemoved && a.Name == Name && a.RuntimeFlags.Has(ObjFlags.Shootable));
        HudMessageManager.Show(Managers.HudMessageKind.Other, args[0], args.ElementAtOrDefault(1),
            new Dictionary<char, string> { ['n'] = left.ToString() });
    }

    /// <summary>A_VictoryIfLast: the level is won once none of this actor's class still stands (the last projection generator)</summary>
    internal void VictoryIfLast()
    {
        if (!MapManager.GetActors().Any(a => !a.IsRemoved && a.Name == Name && a.RuntimeFlags.Has(ObjFlags.Shootable)))
            PlayState = PlayState.Victorious;
    }

    /// <summary>
    /// A_FireSpread("SpectorShot", chance, health, lowchance, 24, 16, 8): a projectile at the
    /// player and, chance times in 256 (lowchance below that health), one either side at each
    /// offset in Angles units too (bstone's morphed Goldfire)
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
        if (NextRandom() >= chance)
            return;
        for (int i = 4; i < args.Length; i++)
        {
            int offset = Arg(i, 0);
            FireProjectile([args[0], offset.ToString()]);
            FireProjectile([args[0], (-offset).ToString()]);
        }
    }
}