using PFWolf.Constants;
using PFWolf.Enums;
using PFWolf.Extensions;
using PFWolf.Managers;
using static PFWolf.Gameplay.GameConstants;
using static PFWolf.Gameplay.LineOfSight;
using static PFWolf.Rendering.TrigTables;

namespace PFWolf.Entities.Actors;

/// <summary>
/// An actor that moves, sees, fights and dies: Wolf3D's and Spear of Destiny's enemies, ported
/// from WL_STATE.C / WL_ACT2.C. An actordefs class
/// is one when it has Monster as an ancestor (`parent: Monster`), which anything with health
/// or a think/action registered here needs. Its thinks and actions are registered below under
/// the names the actordefs use (`think: T_Chase`), and run on the actor itself; subclasses
/// (Blake Stone's, for one) override them.
/// </summary>
internal partial record Monster : Actor
{
    // What a monster uses of the game, through its world (Entities/Actors/ActorWorld.cs)
    private protected GameEngineManager GameEngineManager => World.Engine;
    private protected AssetManager AssetManager => World.Assets;
    private protected AudioManager AudioManager => World.Audio;
    private protected InventoryManager InventoryManager => World.Inventory;
    private protected HudMessageManager HudMessageManager => World.HudMessages;
    private protected MapManager MapManager => World.Map;
    private protected FactionManager FactionManager => World.Factions;
    private protected LevelAI LevelAI => World.LevelAI;

    private protected GameState GameState => World.Session.GameState;
    private protected uint Tics => World.Session.Tics;
    private protected int NextRandom() => World.Session.Random.RndT();
    private protected byte[] AreaByPlayer => World.Session.Areas.AreaByPlayer;
    private protected PlayerPawn Player => World.Session.Player;
    private protected PlayerState PlayerState => World.Session.PlayerState;
    private protected PlayerState LocalPlayer => World.Session.LocalPlayer;
    private protected GameMode GameMode => World.Session.GameMode;
    private protected bool DemoRecord => World.Session.DemoRecord;
    private protected bool DemoPlayback => World.Session.DemoPlayback;
    private protected bool LoadedGame => World.Session.LoadedGame;
    private protected bool MadeNoise { get => World.Session.MadeNoise; set => World.Session.MadeNoise = value; }
    private protected PlayState PlayState { get => World.Session.PlayState; set => World.Session.PlayState = value; }

    private protected bool CheckLine(Actor ob) => World.LineOfSight.CheckLine(ob);
    private protected bool CheckLine(Actor ob, Actor to) => World.LineOfSight.CheckLine(ob, to);
    private protected bool CheckLine(int fromX, int fromY, Actor to) => World.LineOfSight.CheckLine(fromX, fromY, to);
    private protected bool CheckDiag(int x, int y) => World.LineOfSight.CheckDiag(x, y);
    private protected DoorObject[] DoorObjects => World.Doors.DoorObjList;
    private protected int LastDoorObject => World.Doors.LastDoorObj;
    private protected void OpenDoor(int door) => World.Doors.OpenDoor(door);
    private protected void SpawnItemAt(string itemClass, int tilex, int tiley) => World.Map.SpawnThing(tilex, tiley, itemClass);
    private protected void PlayActorSound(Actor ob, string key) => World.ActorSounds.PlayActorSound(ob, key);
    private protected void PlaySoundAtActor(string s, Actor ob) => World.PositionalSounds.PlaySoundLocActor(s, ob);
    private protected string ActorTag(Actor actor) => World.HudMessageDrawing.ActorTag(actor);
    private protected string? BestWeapon(bool canFire = false) => World.PlayerWeapons.BestWeapon(canFire);
    private protected int WeaponSelectionOrder(string weapon) => World.PlayerWeapons.WeaponSelectionOrder(weapon);
    private protected bool PlayerWeaponIsSilent() => World.PlayerAttacks.PlayerWeaponIsSilent();
    private protected bool CouldTakeInventory(Inventory item) => World.PlayerStats.CouldTakeInventory(item);
    private protected bool TryApplyInventory(Inventory item, int amount) => World.PlayerStats.TryApplyInventory(item, amount);
    private protected void GivePoints(int points) => World.PlayerStats.GivePoints(points);
    private protected void TakeDamage(int points, Actor attacker) => World.PlayerStats.TakeDamage(points, attacker);
    private protected void TakeDamage(PlayerPawn victim, int points, Actor attacker) => World.Players.TakeDamage(victim, points, attacker);
    private protected bool InViewOf(PlayerPawn pawn, Actor actor) => World.PlayerView.InViewOf(pawn, actor);
    private protected bool PlayerTileInView(PlayerPawn pawn) => World.PlayerView.PlayerTileInView(pawn);
    private protected bool SeenByAPlayer(Actor actor) => World.PlayerView.SeenByAPlayer(actor);
    private protected void CarriedDeathWork(Actor ob, int tilex, int tiley) => World.SmartSwitches.CarriedDeathWork(ob, tilex, tiley);

    /// <summary>The thinks and actions only a Monster can run, by their actordefs names (and its subclasses').</summary>
    internal static void RegisterActions()
    {
        ActorActionRegistry.RegisterFor<Monster>("T_Stand", m => m.Stand());
        ActorActionRegistry.RegisterFor<Monster>("T_Path", m => m.Patrol());
        // Running off to a marker (see RETREATING): Midnight Assault's Commander between phases
        ActorActionRegistry.RegisterFor<Monster>("A_RetreatTo", (m, args) => m.RetreatTo(args));
        ActorActionRegistry.RegisterFor<Monster>("T_Retreat", m => m.Retreat());
        ActorActionRegistry.RegisterFor<Monster>("T_Chase", m => m.Chase());
        ActorActionRegistry.RegisterFor<Monster>("T_DogChase", m => m.DogChase());
        ActorActionRegistry.RegisterFor<Monster>("T_Bite", m => m.Bite());
        ActorActionRegistry.RegisterFor<Monster>("T_Ghosts", m => m.GhostChase());
        // T_DodgeChase: attacks at random while it has a clear shot, otherwise dodges toward the
        // player, and backs off once within four tiles (vanilla's T_Schabb/T_Gift/T_Fat/T_Will)
        ActorActionRegistry.RegisterFor<Monster>("T_DodgeChase", m => m.DodgeChase());
        // T_Strafe: always dodges about the player, never closing in directly, and attacks at
        // random (less often than T_DodgeChase) while it has a clear shot (vanilla's T_Fake)
        ActorActionRegistry.RegisterFor<Monster>("T_Strafe", m => m.Strafe());
        // T_Shoot: a gun volley; `monster.pointblankdamage` adds that much when right beside the
        // player (the Ubermutant's 10, vanilla's T_UShoot)
        ActorActionRegistry.RegisterFor<Monster>("T_Shoot", m =>
        {
            m.Shoot();
            m.PointBlankHit();
        });
        // A_Morph("Class"[, "keep"]): becomes another enemy where it stands (a boss's next
        // phase); the new one is shootable at its own health and speed and already chasing. This
        // one leaves the level, or with "keep" stays as it is (a corpse, as Mecha Hitler's armor)
        ActorActionRegistry.RegisterFor<Monster>("A_Morph", (m, args) => m.Morph(args));

        // Factions (factions.yaml): A_SetFaction("allies"[, "follow"]) puts it on a side (with
        // follow, going about with the players from now on); A_Follow / A_Follow("off") starts
        // or stops it following them
        ActorActionRegistry.RegisterFor<Monster>("A_SetFaction", (m, args) =>
        {
            if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
                Console.WriteLine($"A_SetFaction ({m.Name}): no faction given.");
            else
                m.SetFaction(args[0].Trim(), args.Skip(1).Any(a => a.Trim().Equals("follow", StringComparison.OrdinalIgnoreCase)));
        });
        ActorActionRegistry.RegisterFor<Monster>("A_Follow", (m, args) =>
            m.Follows = !args.Any(a => a.Trim().Equals("off", StringComparison.OrdinalIgnoreCase)));

        // Reviving fallen allies (see REVIVING): T_ReviveChase / T_ReviveStand are T_Chase and
        // T_Stand that first look for a corpse of its side to bring back; A_Revive([count][,
        // radius]) brings them back (count a number or "min-max"); A_Resume, after a heal or a
        // raise, chases whoever it's after, or stands. A_Resume is any actor's, as every class
        // that dies gets a Raise state ending in it (ActorStateResolver.AddRaiseFromDeath).
        ActorActionRegistry.RegisterFor<Monster>("T_ReviveChase", m => m.ReviveChase());
        ActorActionRegistry.RegisterFor<Monster>("T_ReviveStand", m => m.ReviveStand());
        ActorActionRegistry.RegisterFor<Monster>("A_Revive", (m, args) => m.Revive(args));
        ActorActionRegistry.Register("A_Resume", actor => (actor as Monster)?.Resume());

        ActorActionRegistry.RegisterFor<Monster>("A_FireProjectile", (m, args) => m.FireProjectile(args));
        // A volley of several shots (the Angel's sparks): A_StartAttack starts the count, and
        // A_Relaunch(count, "TiredState"[, "BreakOffState"]) after each shot goes to TiredState
        // once count are fired, else breaks off to BreakOffState (default Chase) half the time
        ActorActionRegistry.RegisterFor<Monster>("A_StartAttack", m => m.StartAttack());
        ActorActionRegistry.RegisterFor<Monster>("A_Relaunch", (m, args) => m.Relaunch(args));
        ActorActionRegistry.RegisterFor<Monster>("A_Dormant", (m, args) => m.Dormant(args));
        // T_Scan("AttackState"): a turret's watch (Monster.Turret.cs)
        ActorActionRegistry.RegisterFor<Monster>("T_Scan", (m, args) => m.Scan(args));
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

            if (actor is Monster reviver)
                reviver.CheckReviveProperties();
        }
    }

    // Warns about a `monster.revive.count` or `monster.revive.radius` that makes no sense
    private void CheckReviveProperties()
    {
        var count = PropertyStrings("monster.revive.count");
        if (count.Count > 0 && (count.Count > 2 || ParseCountRange(string.Join("-", count)) == null))
            Console.WriteLine($"Actor '{Name}': monster.revive.count should be a number, [min, max] or \"min-max\" (0 or more), not {string.Join(", ", count)}; reviving 1.");
        if (Properties.TryGetValue("monster.revive.radius", out var radius) && !TryParseTiles(radius?.ToString(), out _))
            Console.WriteLine($"Actor '{Name}': monster.revive.radius should be a number of tiles, not {radius}; using {DefaultReviveRadius}.");
    }

    // The way back from each direction, and the diagonal between two (east/west, then
    // north/south), in ObjDir order (from WL_STATE.C)
    protected static readonly ObjDir[] Opposite = new ObjDir[9]
        {ObjDir.West,ObjDir.SouthWest,ObjDir.South,ObjDir.SouthEast,ObjDir.East,ObjDir.NorthEast,ObjDir.North,ObjDir.NorthWest,ObjDir.NoDir};

    protected static readonly ObjDir[,] Diagonal = new ObjDir[9, 9]
{
    /* east */  {ObjDir.NoDir,ObjDir.NoDir,ObjDir.NorthEast,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.SouthEast,ObjDir.NoDir,ObjDir.NoDir
},
                {ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir},
    /* north */ { ObjDir.NorthEast,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NorthWest,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir},
                { ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir},
    /* west */  { ObjDir.NoDir,ObjDir.NoDir,ObjDir.NorthWest,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.SouthWest,ObjDir.NoDir,ObjDir.NoDir},
                { ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir},
    /* south */ { ObjDir.SouthEast,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.SouthWest,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir},
                { ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir},
                { ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir,ObjDir.NoDir}
};

    internal void RecenterOnTile()
    {
        X = (int)((TileX << MapConstants.TileShift) + MapConstants.TileGlobal / 2);
        Y = (int)((TileY << MapConstants.TileShift) + MapConstants.TileGlobal / 2);
    }

    // Reached the middle of the tile it was walking to (EoD's CheckEnemyTile): what's there can
    // move it on (ActorWorld.TileArrived, an A_Warp pad), it takes what lies where it ends up
    // (Monster.Pickups.cs), and it picks its next way from there
    private void ArriveOnTile()
    {
        RecenterOnTile();
        World?.TileArrived?.Invoke(this);
        TakePickups();
    }

    // The way on after a tile, chasing: towards the player (or dodging), or while the level has its
    // enemies wander (MapManager.MonstersWander), straight on until something's in the way
    private void SelectChaseOrWanderDir(bool dodge)
    {
        if (MapManager.MonstersWander)
            SelectWanderDir();
        else if (dodge)
            SelectDodgeDir();
        else
            SelectChaseDir();
    }

    /// <summary>
    /// Wandering (End of Destiny's SelectLootDir, Greed's looters): on the way it was going; on
    /// its first move after noticing the player, back the way it came; where it can't go on,
    /// east, north, west or south, whichever is open first, but not straight back
    /// </summary>
    internal void SelectWanderDir()
    {
        if (RuntimeFlags.Has(ObjFlags.FirstAttack))
        {
            RuntimeFlags &= ~ObjFlags.FirstAttack;
            if (Dir != ObjDir.NoDir)
            {
                Dir = Opposite[(byte)Dir];
                if (TryWalk())
                    return;
            }
        }
        else if (Dir != ObjDir.NoDir && TryWalk())
            return;

        var back = Dir == ObjDir.NoDir ? ObjDir.NoDir : Opposite[(byte)Dir];
        foreach (var dir in new[] { ObjDir.East, ObjDir.North, ObjDir.West, ObjDir.South })
        {
            if (dir == back)
                continue;
            Dir = dir;
            if (TryWalk())
                return;
        }
        Dir = ObjDir.NoDir;
    }

    /*
    =============================================================================
    MOVEMENT / COLLISION (ported from WL_STATE.C)
    =============================================================================
    */

    internal void MoveObj(int move)
    {
        int newx = X, newy = Y;

        switch (Dir)
        {
            case ObjDir.North: newy -= move; break;
            case ObjDir.NorthEast: newx += move; newy -= move; break;
            case ObjDir.East: newx += move; break;
            case ObjDir.SouthEast: newx += move; newy += move; break;
            case ObjDir.South: newy += move; break;
            case ObjDir.SouthWest: newx -= move; newy += move; break;
            case ObjDir.West: newx -= move; break;
            case ObjDir.NorthWest: newx -= move; newy -= move; break;
            case ObjDir.NoDir: return;
            default: GameEngineManager.Quit("MoveObj: bad dir!"); break;
        }

        // A NOTSOLID actor (Blake Stone's electro-spheres) goes right through the player
        if ((AreaNumber >= MapManager.Floors.NumAreas || AreaByPlayer[AreaNumber] != 0) && !HasFlag("NOTSOLID"))
        {
            // Any player in the way stops it, not just the one it's after
            foreach (var pawn in MapManager.Players)
            {
                if (!IsTargetable(pawn))
                    continue;

                var deltax = Math.Abs(newx - pawn.X);
                var deltay = Math.Abs(newy - pawn.Y);

                if (deltax <= MinActorDist && deltay <= MinActorDist)
                {
                    // An ally walking after a player (or out of their way) is only stopped
                    // getting nearer them, not moving off past or away from them
                    if (RuntimeFlags.Has(ObjFlags.Following)
                        && Math.Max(deltax, deltay) >= Math.Max(Math.Abs(X - pawn.X), Math.Abs(Y - pawn.Y)))
                        continue;

                    if (!Hidden || !PlayerTileInView(pawn))
                    {
                        // TOUCHDAMAGE actors (ghosts, Spectres) hurt the player on contact
                        if (HasFlag("TOUCHDAMAGE"))
                            TakeDamage(pawn, (int)(Tics * 2), this);

                        OnBumpedPlayer();

                        // Walking after a player (or out of their way), it waits once it's up against one
                        if (RuntimeFlags.Has(ObjFlags.Following))
                            StopWalking();
                        return;
                    }
                }
            }
        }

        X = newx;
        Y = newy;
        Distance -= move;
    }

    // A monster blocker on the tile keeps it off: any (blocktile: monsters), or a BOSS (bosses)
    // (Midnight Assault's BLOCK_ENEMIES and BLOCK_BOSSES, CHECKSIDE/CHECKDIAG)
    bool Blocked(int x, int y) => MapManager.BlocksMonster(x, y, HasFlag("BOSS"));

    internal bool TryWalk()
    {
        int doornumtile = -1;
        int r;

        // Straight moves can open a door on the way (CheckSide; NODOORS actors such as
        // Wolf3D's dogs and fake Hitlers can't), diagonal ones need all three tiles clear
        switch (Dir)
        {
            case ObjDir.North:
                if (Blocked(TileX, TileY - 1)) return false;
                r = CheckSide(TileX, TileY - 1, ref doornumtile);
                if (r == 0) return false;
                if (r == 1) return true;
                TileY--;
                break;

            case ObjDir.NorthEast:
                if (Blocked(TileX + 1, TileY - 1) || Blocked(TileX + 1, TileY) || Blocked(TileX, TileY - 1)) return false;
                if (!CheckDiag(TileX + 1, TileY - 1)) return false;
                if (!CheckDiag(TileX + 1, TileY)) return false;
                if (!CheckDiag(TileX, TileY - 1)) return false;
                TileX++; TileY--;
                break;

            case ObjDir.East:
                if (Blocked(TileX + 1, TileY)) return false;
                r = CheckSide(TileX + 1, TileY, ref doornumtile);
                if (r == 0) return false;
                if (r == 1) return true;
                TileX++;
                break;

            case ObjDir.SouthEast:
                if (Blocked(TileX + 1, TileY + 1) || Blocked(TileX + 1, TileY) || Blocked(TileX, TileY + 1)) return false;
                if (!CheckDiag(TileX + 1, TileY + 1)) return false;
                if (!CheckDiag(TileX + 1, TileY)) return false;
                if (!CheckDiag(TileX, TileY + 1)) return false;
                TileX++; TileY++;
                break;

            case ObjDir.South:
                if (Blocked(TileX, TileY + 1)) return false;
                r = CheckSide(TileX, TileY + 1, ref doornumtile);
                if (r == 0) return false;
                if (r == 1) return true;
                TileY++;
                break;

            case ObjDir.SouthWest:
                if (Blocked(TileX - 1, TileY + 1) || Blocked(TileX - 1, TileY) || Blocked(TileX, TileY + 1)) return false;
                if (!CheckDiag(TileX - 1, TileY + 1)) return false;
                if (!CheckDiag(TileX - 1, TileY)) return false;
                if (!CheckDiag(TileX, TileY + 1)) return false;
                TileX--; TileY++;
                break;

            case ObjDir.West:
                if (Blocked(TileX - 1, TileY)) return false;
                r = CheckSide(TileX - 1, TileY, ref doornumtile);
                if (r == 0) return false;
                if (r == 1) return true;
                TileX--;
                break;

            case ObjDir.NorthWest:
                if (Blocked(TileX - 1, TileY - 1) || Blocked(TileX - 1, TileY) || Blocked(TileX, TileY - 1)) return false;
                if (!CheckDiag(TileX - 1, TileY - 1)) return false;
                if (!CheckDiag(TileX - 1, TileY)) return false;
                if (!CheckDiag(TileX, TileY - 1)) return false;
                TileX--; TileY--;
                break;

            case ObjDir.NoDir:
                return false;

            default:
                GameEngineManager.Quit("Walk: Bad dir");
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
        if (MapManager.ActorAt[TileX, TileY] is not Door && MapManager.ValidArea(MapManager.MapSpot(TileX, TileY, 0)))
            AreaNumber = (byte)(MapManager.MapSpot(TileX, TileY, 0) - MapManager.Floors.AreaTile);
        Distance = (int)MapConstants.TileGlobal;
        SyncPosition();
        return true;
    }

    // CheckDiag(int, int) lives in Gameplay/LineOfSight.cs -- it isn't tied to the mover, so
    // there's no Entities.Actors.Actor-typed overload.

    internal int CheckSide(int x, int y, ref int doornumtile)
    {
        var temp = MapManager.ActorAt[x, y];
        if (temp is ActorMark mark)
            return MapManager.IsSolidActor(mark.Who) ? 0 : 2;     // another enemy's there (alive, it's in the way)

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
                if (HasFlag("PHASEDOORS"))
                    return 2;

                // NOLOCKEDDOORS actors (Blake Stone's) don't open a door that needs a key, and
                // NODOORS ones (its liquid alien and electro-spheres) none at all
                if (HasFlag("NODOORS") || HasFlag("NOLOCKEDDOORS") && DoorObjects[door.DoorNumber].IsLocked)
                    return 0;

                doornumtile = door.DoorNumber;
                if (!(DemoRecord || DemoPlayback))
                {
                    OpenDoor(doornumtile);
                    Distance = -doornumtile - 1;
                    return 1;
                }
            }
        }

        return 2; // continue
    }

    /// <summary>
    /// Whether it can see someone it's hostile to (factions.yaml), who becomes its Target: the
    /// first player it sees (in player order), or with rivals about (MapManager.HasRivalries) the
    /// nearest player or enemy of another side it sees, players first of those as near
    /// </summary>
    internal bool CheckSight()
    {
        if (AreaNumber < MapManager.Floors.NumAreas && AreaByPlayer[AreaNumber] == 0)
            return false;

        if (!MapManager.HasRivalries)
        {
            foreach (var pawn in MapManager.Players)
            {
                if (IsTargetable(pawn) && IsHostileTo(pawn) && CanSee(pawn))
                {
                    Target = pawn;
                    return true;
                }
            }
            return false;
        }

        if (NearestFoe(CanSee) is not { } seen)
            return false;
        Target = seen;
        return true;
    }

    /// <summary>Whether its side goes after <paramref name="other"/>'s on sight</summary>
    internal bool IsHostileTo(Actor other) => FactionManager.IsHostile(this, other);

    /// <summary>Whether <paramref name="other"/> is on its side (or one it's allied to)</summary>
    internal bool IsAllyOf(Actor other) => FactionManager.IsAlly(this, other);

    // Everyone it could go after: the players, then (with rivals about) the other enemies
    private IEnumerable<Actor> FoeCandidates()
    {
        foreach (var pawn in MapManager.Players)
            yield return pawn;
        if (!MapManager.HasRivalries)
            yield break;
        foreach (var other in MapManager.Combatants)
        {
            if (!ReferenceEquals(other, this))
                yield return other;
        }
    }

    // The nearest (in tiles, either way) living one it's hostile to that passes the test; the
    // first in FoeCandidates' order of those as near
    private Actor? NearestFoe(Func<Actor, bool>? test = null)
    {
        Actor? nearest = null;
        int best = int.MaxValue;
        foreach (var candidate in FoeCandidates())
        {
            int dist = TileDistance(candidate);
            if (dist < best && IsTargetable(candidate) && IsHostileTo(candidate) && (test == null || test(candidate)))
                (nearest, best) = (candidate, dist);
        }
        return nearest;
    }

    private int TileDistance(Actor other) => Math.Max(Math.Abs(other.TileX - TileX), Math.Abs(other.TileY - TileY));

    // Whether an actor (a player, or another it could go after) is in front of it (or right
    // beside it) with nothing in the way
    internal bool CanSee(Actor other)
    {
        var deltax = other.X - X;
        var deltay = other.Y - Y;

        if (deltax > -MinSight && deltax < MinSight && deltay > -MinSight && deltay < MinSight)
            return true;

        switch (Dir)
        {
            case ObjDir.North:
                if (deltay > 0) return false;
                break;
            case ObjDir.East:
                if (deltax < 0) return false;
                break;
            case ObjDir.South:
                if (deltay < 0) return false;
                break;
            case ObjDir.West:
                if (deltax > 0) return false;
                break;
            case ObjDir.NorthWest:
                if (!(DemoRecord || DemoPlayback) && deltay > -deltax) return false;
                break;
            case ObjDir.NorthEast:
                if (!(DemoRecord || DemoPlayback) && deltay > deltax) return false;
                break;
            case ObjDir.SouthWest:
                if (!(DemoRecord || DemoPlayback) && deltax > deltay) return false;
                break;
            case ObjDir.SouthEast:
                if (!(DemoRecord || DemoPlayback) && -deltax > deltay) return false;
                break;
        }

        return CheckLine(this, other);
    }

    /*
    =============================================================================
    TARGET: the actor it goes after
    =============================================================================
    */

    private Actor? _target;

    /// <summary>
    /// The actor it goes after: the one it last saw (CheckSight) or was hurt by, while they're
    /// still on the level, alive and not on its side; else the nearest living one it's hostile
    /// to; else the first player it's hostile to, alive or not. With one player and no other
    /// sides, always them. With nobody at all to go after (an ally of the players with no
    /// enemies about), the first player: <see cref="HasFoe"/> says so first, and BeforeThink
    /// makes it give up.
    /// </summary>
    internal Actor Target
    {
        get
        {
            if (_target is { } t && IsStillTarget(t))
                return t;
            return _target = NearestFoe() ?? FirstHostilePlayer() ?? MapManager.Players.First();
        }
        set => _target = value;
    }

    /// <summary>The actor it's set on going after, as it stands (without picking one, as Target does), or null</summary>
    internal Actor? TargetIfAny => _target is { } t && IsOnLevel(t) ? t : null;

    /// <summary>Whether it has anyone to go after: Target would be someone it's hostile to (or who hurt it)</summary>
    internal bool HasFoe =>
        _target is { } t && IsStillTarget(t) || NearestFoe() != null || FirstHostilePlayer() != null;

    // Whether an actor it was after is still one to go after: on the level, alive, and not (or
    // no longer) on its side
    private bool IsStillTarget(Actor actor) => IsOnLevel(actor) && IsTargetable(actor) && !IsAllyOf(actor);

    // Whether an actor is still on the level: a player's pawn they still have (one that left, or
    // was replaced coming into a level, isn't), anything else not taken off it
    private static bool IsOnLevel(Actor actor) =>
        actor is PlayerPawn pawn ? ReferenceEquals(pawn.State.Pawn, pawn) : !actor.IsRemoved;

    // The first player (in player order) it's hostile to, living or not, or null
    private PlayerPawn? FirstHostilePlayer() => MapManager.Players.FirstOrDefault(IsHostileTo);

    /*
    With rivals about, an enemy after someone looks round now and then (RetargetTics) for
    someone nearer, or for anyone at all in sight when it has lost sight of who it's after.
    */
    private const int RetargetTics = 35;
    private int _retargetIn;

    /// <summary>
    /// Run (MapManager.DoActor) before each think: an enemy going after someone (AttackMode)
    /// with no one left to go after gives up and stands; with rivals about, it may turn on
    /// someone nearer. True when it changed state.
    /// </summary>
    internal bool BeforeThink(uint tics)
    {
        if (!RuntimeFlags.Has(ObjFlags.AttackMode))
            return false;

        if (!HasFoe)
        {
            GiveUp();
            return true;
        }

        if (MapManager.HasRivalries && (_retargetIn -= (int)tics) <= 0)
        {
            _retargetIn = RetargetTics;
            var current = Target;
            bool inSight = CheckLine(this, current);
            if (NearestFoe(foe => CheckLine(this, foe)) is { } seen && !ReferenceEquals(seen, current)
                && (!inSight || TileDistance(seen) < TileDistance(current)))
                Target = seen;
        }
        return false;
    }

    /// <summary>
    /// Stops going after anyone: back to its Spawn state at its walking speed, waiting to see
    /// someone again (as an ally of the players does once its enemies are dead)
    /// </summary>
    internal void GiveUp()
    {
        RuntimeFlags &= ~(ObjFlags.AttackMode | ObjFlags.FirstAttack | ObjFlags.Following);
        _target = null;
        Temp2 = 0;
        Speed = PropertyInt("speed", SpdPatrol);
        if (Distance < 0)
            Distance = 0;
        SetState("Spawn");
    }

    /*
    =============================================================================
    FOLLOWING: an ally of the players going about with them
    =============================================================================

    An actor that follows (Follows: its `monster.follow: true` once it has seen an allied
    player, or A_SetFaction's "follow") walks after the nearest living player on its side when
    they get more than FollowFar tiles away, in its Chase state at its chasing speed, and stops
    within FollowNear. Anyone it's hostile to that it sees on the way, it goes after instead.
    Walked into by a player, it steps a couple of tiles out of their way.
    */
    private const int FollowNear = 2, FollowFar = 3;

    // The tile it's walking to (SelectChaseDir heads there instead of at its target), while it
    // follows; and, stepping out of the way, where to (both just for a move, not saved)
    private (int X, int Y)? _walkGoal;
    private (int X, int Y)? _stepAside;

    /// <summary>Whether it goes about with the players (Follows)</summary>
    internal bool Follows
    {
        get => RuntimeFlags.Has(ObjFlags.Follows);
        set
        {
            if (value)
                RuntimeFlags |= ObjFlags.Follows;
            else
            {
                if (RuntimeFlags.Has(ObjFlags.Following))
                    StopWalking();
                RuntimeFlags &= ~ObjFlags.Follows;
            }
        }
    }

    // The nearest living player on its side, if any
    private PlayerPawn? Leader
    {
        get
        {
            PlayerPawn? nearest = null;
            int best = int.MaxValue;
            foreach (var pawn in MapManager.Players)
            {
                int dist = TileDistance(pawn);
                if (pawn.State.Health > 0 && dist < best && IsAllyOf(pawn))
                    (nearest, best) = (pawn, dist);
            }
            return nearest;
        }
    }

    /// <summary>
    /// Run (MapManager.DoActor) after BeforeThink and in place of its think, for an ally that
    /// follows: true when it saw to itself this tic (walking after a player, or out of their
    /// way), so its own think isn't run
    /// </summary>
    internal bool ThinkInstead(uint tics)
    {
        if (RuntimeFlags.Has(ObjFlags.AttackMode) || !RuntimeFlags.Has(ObjFlags.Shootable) || IsBusyReviving)
            return false;

        // `monster.follow`: it falls in with a player on its side once it sees them
        if (!Follows && PropertyBool("monster.follow") && Leader is { } met && CanSee(met))
            Follows = true;
        if (!Follows)
            return false;

        bool walking = RuntimeFlags.Has(ObjFlags.Following);
        if (walking && CheckSight())
        {
            // Someone to go after: that comes first
            StopWalking(stand: false);
            FirstSighting();
            return true;
        }

        if (_stepAside == null && (Leader is not { } leader || !walking && TileDistance(leader) <= FollowFar))
        {
            if (walking)
                StopWalking();
            return false;           // standing by: its own think looks out for enemies
        }

        if (!walking)
            StartWalking();
        WalkStep(tics);
        return true;
    }

    /// <summary>A player walked into it standing by: it steps a couple of tiles out of their way</summary>
    internal void ShovedBy(PlayerPawn pawn)
    {
        if (!Follows || RuntimeFlags.Has(ObjFlags.AttackMode) || _stepAside != null || !IsAllyOf(pawn))
            return;

        // To one side of the way they face (two tiles if there's room), else straight away from them
        bool facingAcross = Math.Abs(CosTable[pawn.Angle]) >= Math.Abs(SinTable[pawn.Angle]);
        (int X, int Y)[] ways = facingAcross ? [(0, 1), (0, -1)] : [(1, 0), (-1, 0)];
        ways = [.. ways, (Math.Sign(TileX - pawn.TileX), Math.Sign(TileY - pawn.TileY))];
        foreach (var (dx, dy) in ways)
        {
            if ((dx, dy) == (0, 0) || !OpenForStep(TileX + dx, TileY + dy, pawn))
                continue;
            _stepAside = OpenForStep(TileX + dx * 2, TileY + dy * 2, pawn) ? (TileX + dx * 2, TileY + dy * 2) : (TileX + dx, TileY + dy);
            return;
        }
    }

    // Open floor nothing stands on (nor the player shoving it)
    private bool OpenForStep(int x, int y, PlayerPawn shover) =>
        x > 0 && x < MapManager.MapWidth - 1 && y > 0 && y < MapManager.MapHeight - 1
        && MapManager.TileMap[x, y] == 0 && MapManager.ActorAt[x, y] is null
        && !(shover.TileX == x && shover.TileY == y);

    private void StartWalking()
    {
        RuntimeFlags |= ObjFlags.Following;
        Speed = ChaseSpeed();
        if (Distance < 0)
            Distance = 0;
        SetState("Chase");
    }

    private void StopWalking(bool stand = true)
    {
        RuntimeFlags &= ~ObjFlags.Following;
        _stepAside = null;
        Speed = PropertyInt("speed", SpdPatrol);
        if (stand)
            SetState("Spawn");
    }

    // Its speed going after someone (as FirstSighting sets it)
    private int ChaseSpeed()
    {
        int speed = PropertyInt("speed", SpdPatrol);
        if (Properties.TryGetValue("monster.chasespeed", out var chaseSpeed))
            return Convert.ToInt32(chaseSpeed);
        if (Properties.TryGetValue("monster.chasespeedmultiplier", out var mult))
            return speed * Convert.ToInt32(mult);
        return speed;
    }

    // Where it's walking: out of the way, else to its leader
    private (int X, int Y)? WalkGoal() =>
        _stepAside ?? (Leader is { } leader ? (leader.TileX, leader.TileY) : null);

    // Whether it's where it was walking to: out of the way, or near enough its leader
    private bool Arrived() =>
        _stepAside is { } aside ? TileX == aside.X && TileY == aside.Y
        : Leader is not { } leader || TileDistance(leader) <= FollowNear;

    // A tic's walk towards WalkGoal, as Chase moves (doors and all), stopping on a tile's middle
    // once it's there
    private void WalkStep(uint tics)
    {
        if (Dir == ObjDir.NoDir && !SelectWalkDir())
        {
            StopWalking();      // boxed in
            return;
        }

        var move = (int)(Speed * tics);
        while (move != 0)
        {
            if (Distance < 0)
            {
                OpenDoor(-Distance - 1);
                if (DoorObjects[-Distance - 1].Action != (byte)DoorAction.Open)
                    return;
                Distance = (int)MapConstants.TileGlobal;
                if (!(DemoRecord || DemoPlayback))
                    TryWalk();
            }

            if (move < Distance)
            {
                MoveObj(move);
                break;
            }

            ArriveOnTile();
            move -= Distance;
            Distance = 0;

            if (Arrived() || !SelectWalkDir())
            {
                StopWalking();
                return;
            }
        }
    }

    // Picks the way to WalkGoal (SelectChaseDir, towards it); false when there's none
    private bool SelectWalkDir()
    {
        _walkGoal = WalkGoal();
        if (_walkGoal == null)
            return false;
        try
        {
            SelectChaseDir();
        }
        finally
        {
            _walkGoal = null;
        }
        return Dir != ObjDir.NoDir;
    }

    /*
    =============================================================================
    REVIVING: bringing fallen allies back, as DOOM's Arch-Vile
    =============================================================================

    A reviver (think T_ReviveChase in place of T_Chase, T_ReviveStand in place of T_Stand)
    looks out for the corpses of its own side, or a side it counts its ally (factions.yaml),
    within `monster.revive.radius` tiles (default 2) that it can see. Finding one, it goes to
    its `monster.revive.state` (default Heal), whose A_Revive brings back
    `monster.revive.count` of them, nearest first (default 1; [min, max] or "min-max" for a
    random number in that range), playing its `revivesound`; with no such state, it revives
    them there and then. A revived actor has its full health back and plays its Raise state
    (its class's own, else its Death backwards: ActorStateResolver.AddRaiseFromDeath), whose
    A_Resume sends it after whoever its reviver was fighting (or anyone it sees), else back to
    standing. It's off the kill count again (killed once more, it counts once more), drops its
    items again, and is worth no points the second time.

    Not every corpse comes back: not a NOREVIVE class's (the bosses), one with a death cam, one
    still to become something else (`monster.becomes`), one sliding, or one in a doorway or
    with something alive (or a player) on its tile.
    */
    private const string DefaultReviveState = "Heal";
    private const double DefaultReviveRadius = 2;

    // Its `monster.revive.state`
    private string ReviveState =>
        Properties.TryGetValue("monster.revive.state", out var state) && state?.ToString() is { Length: > 0 } name ? name : DefaultReviveState;

    // Whether it's getting up, or bringing someone back: following the players waits
    private bool IsBusyReviving => CurrentState?.StateName is { } state
        && (state == ActorStateResolver.RaiseState || state.Equals(ReviveState, StringComparison.OrdinalIgnoreCase));

    internal void ReviveChase()
    {
        if (!TryStartReviving())
            Chase();
    }

    internal void ReviveStand()
    {
        if (!TryStartReviving())
            Stand();
    }

    // With a corpse to bring back in reach, it goes to its revive state (or, without one,
    // revives there and then): true when it did
    private bool TryStartReviving()
    {
        if (GameState.VictoryFlag || !Revivables(ReviveRadius()).Any())
            return false;

        if (ResolvedStates.TryGetValue(ReviveState, out var heal))
            SetState(heal);
        else
            Revive([]);
        return true;
    }

    /// <summary>
    /// A_Revive([count][, radius]): brings back up to count corpses of its side (a number, or
    /// "min-max" for a random number in that range) within radius tiles that it can see, nearest
    /// first; its `monster.revive.count` and `monster.revive.radius` for those not given
    /// </summary>
    internal void Revive(string[] args)
    {
        var (min, max) = ReviveCount();
        if (args.Length > 0 && args[0].Trim().Length > 0)
        {
            if (ParseCountRange(args[0]) is { } given)
                (min, max) = given;
            else
                Console.WriteLine($"A_Revive ({Name}): '{args[0].Trim()}' isn't a count (a number, or \"min-max\").");
        }

        double radius = ReviveRadius();
        if (args.Length > 1 && args[1].Trim().Length > 0)
        {
            if (TryParseTiles(args[1], out var givenRadius))
                radius = givenRadius;
            else
                Console.WriteLine($"A_Revive ({Name}): '{args[1].Trim()}' isn't a radius in tiles.");
        }

        int count = max > min ? min + NextRandom() % (max - min + 1) : min;
        int revived = 0;
        foreach (var corpse in Revivables(radius))
        {
            if (revived >= count)
                break;
            corpse.Rise(this);
            revived++;
        }

        if (revived > 0)
            PlayActorSound(this, "revivesound");
    }

    // The corpses it could bring back within radius tiles, nearest first (checked as it goes
    // through them, so one risen stops another on its tile coming up too)
    private IEnumerable<Monster> Revivables(double radius) =>
        MapManager.CorpsesNear(X, Y, (long)(radius * MapConstants.TileGlobal)).Where(corpse => corpse.CanBeRevivedBy(this));

    // Whether it's a corpse that the reviver can bring back where it lies
    private bool CanBeRevivedBy(Monster reviver) =>
        !ReferenceEquals(this, reviver) && !IsRemoved
        && !RuntimeFlags.Has(ObjFlags.Shootable) && !RuntimeFlags.Has(ObjFlags.Sliding)
        && CurrentState is { HoldsForever: true, StateName: var state } && IsDeathState(state)
        && !HasFlag("NOREVIVE") && !ResolvedStates.ContainsKey("DeathCam") && !Properties.ContainsKey("monster.becomes")
        && reviver.IsAllyOf(this) && HasRoomToRise() && reviver.CheckLine(reviver, this);

    // Death, XDeath and the damage types' Death.(type) (Monster.ChooseDeathState)
    private static bool IsDeathState(string state) =>
        state == "Death" || state == "XDeath" || state.StartsWith("Death.", StringComparison.Ordinal);

    // Whether its tile is clear to get up on: open floor (not a doorway), no one alive on it, and
    // no player standing over it
    private bool HasRoomToRise()
    {
        if (MapManager.TileMap[TileX, TileY] != 0 || MapManager.ActorAt[TileX, TileY] is not (null or ActorMark)
            || MapManager.IsShootableActorAt(TileX, TileY))
            return false;

        long centerX = (TileX << (int)MapConstants.TileShift) + MapConstants.TileGlobal / 2;
        long centerY = (TileY << (int)MapConstants.TileShift) + MapConstants.TileGlobal / 2;
        foreach (var pawn in MapManager.Players)
        {
            if (IsTargetable(pawn) && Math.Abs(pawn.X - centerX) <= MinActorDist && Math.Abs(pawn.Y - centerY) <= MinActorDist)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Brought back by <paramref name="reviver"/> (or by nothing in particular, null): its full
    /// health back, on its tile again, off the kill count, and getting up (its Raise state)
    /// </summary>
    internal void Rise(Monster? reviver)
    {
        Hitpoints = MapManager.GetScaledHealth(this);
        RuntimeFlags &= ~(ObjFlags.NonMark | ObjFlags.AttackMode | ObjFlags.FirstAttack | ObjFlags.Following | ObjFlags.Ambush);
        RuntimeFlags |= ObjFlags.Shootable | ObjFlags.Revived;
        RecenterOnTile();
        Dir = ObjDir.NoDir;
        Distance = 0;
        Temp2 = 0;
        _stepAside = null;
        Speed = PropertyInt("speed", SpdPatrol);

        // It takes up the fight its reviver is in
        _target = reviver is { } by && by.RuntimeFlags.Has(ObjFlags.AttackMode) && by.TargetIfAny is { } foe && IsHostileTo(foe) ? foe : null;

        MapManager.MarkActorTile(this);
        if (CountsAsKill && GameState.KillCount > 0)
            GameState.KillCount--;

        if (ResolvedStates.TryGetValue(ActorStateResolver.RaiseState, out var raise))
            SetState(raise);
        else
            Resume();
    }

    /// <summary>
    /// A_Resume, after a heal or a raise: on with the chase it's in; else after whoever it was
    /// brought back to fight, or anyone it sees; else back to standing
    /// </summary>
    internal void Resume()
    {
        if (RuntimeFlags.Has(ObjFlags.AttackMode))
        {
            GoTo("Chase");
            return;
        }

        if (_target is { } foe && IsStillTarget(foe) && IsHostileTo(foe) || CheckSight())
        {
            FirstSighting();
            return;
        }

        Speed = PropertyInt("speed", SpdPatrol);
        GoTo("Spawn");
    }

    // Goes to exactly the named state's first frame, from an action too (JumpTo)
    private void GoTo(string stateName)
    {
        if (ResolvedStates.TryGetValue(stateName, out var frame))
            JumpTo(frame);
    }

    // `monster.revive.count`: a number, [min, max] or "min-max"; 1 when it isn't set (or makes no sense)
    private (int Min, int Max) ReviveCount()
    {
        var values = PropertyStrings("monster.revive.count");
        var text = values.Count > 1 ? values[0] + "-" + values[1] : values.FirstOrDefault();
        return text != null && ParseCountRange(text) is { } range ? range : (1, 1);
    }

    // `monster.revive.radius`, in tiles
    private double ReviveRadius() =>
        Properties.TryGetValue("monster.revive.radius", out var value) && TryParseTiles(value?.ToString(), out var radius)
            ? radius : DefaultReviveRadius;

    /// <summary>A count: "2" (just that many) or "1-3" (from 1 to 3), not below 0; null when it isn't one</summary>
    internal static (int Min, int Max)? ParseCountRange(string text)
    {
        var parts = text.Split('-', 2, StringSplitOptions.TrimEntries);
        if (!int.TryParse(parts[0], out var min) || min < 0)
            return null;
        int max = min;
        if (parts.Length == 2 && (!int.TryParse(parts[1], out max) || max < min))
            return null;
        return (min, max);
    }

    private static bool TryParseTiles(string? text, out double tiles) =>
        double.TryParse(text?.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out tiles)
        && tiles >= 0;

    /// <summary>
    /// Puts it on another side (A_SetFaction, a script's actor.faction, the setfaction command):
    /// the level's kill total takes it in or out as it becomes the players' enemy or ally, and
    /// with <paramref name="follow"/> it goes about with them from now on. Whoever was after it
    /// lets it be once it's on their side, and it them.
    /// </summary>
    internal void SetFaction(string? faction, bool follow = false)
    {
        bool alive = RuntimeFlags.Has(ObjFlags.Shootable);
        bool counted = alive && CountsAsKill;
        Faction = faction;
        if (alive && counted != CountsAsKill)
        {
            GameState.KillTotal += (short)(CountsAsKill ? 1 : -1);
            GameState.PointsTotal += (CountsAsKill ? 1 : -1) * MapManager.KillWorth(this);
        }

        if (follow)
            Follows = true;
        else if (Follows && Leader == null && !MapManager.Players.Any(IsAllyOf))
            Follows = false;            // not on the players' side any more
    }

    /// <summary>
    /// Whether a player can be gone after: one alive. A lone player always can (dying ends the
    /// level there, and the rest of that frame plays on as it always has).
    /// </summary>
    internal bool IsTargetable(PlayerPawn pawn) => World.Session.IsTargetable(pawn);

    /// <summary>
    /// Whether an actor can be gone after: a player as above, anything else while it's alive
    /// (shootable) and on the level
    /// </summary>
    internal bool IsTargetable(Actor actor) => actor is PlayerPawn pawn
        ? IsTargetable(pawn)
        : !actor.IsRemoved && actor.RuntimeFlags.Has(ObjFlags.Shootable);

    /// <summary>Whether the player could see it (alone, whether it was drawn; with others, any of them: PlayerView.InViewOf)</summary>
    private bool SeenByThePlayers() =>
        GameMode == GameMode.Single ? RuntimeFlags.Has(ObjFlags.Visible) : SeenByAPlayer(this);

    /// <summary>Hurts the actor it's after: a player as their enemies' shots do, anything else as the player's shots</summary>
    protected void HurtTarget(int points)
    {
        switch (Target)
        {
            case PlayerPawn pawn:
                TakeDamage(pawn, points, this);
                break;
            case Monster monster when points > 0:
                monster.Damage((uint)points, this);
                break;
        }
    }

    /// <summary>
    /// Whether what it's after can see it coming (and so dodge its shots): alone, whether it was
    /// drawn; another player, from where they stand (PlayerView.InViewOf); an actor, whether it's
    /// looking its way with nothing between
    /// </summary>
    private bool TargetSeesMe() => Target switch
    {
        PlayerPawn pawn => GameMode == GameMode.Single ? RuntimeFlags.Has(ObjFlags.Visible) : InViewOf(pawn, this),
        Monster monster => monster.CanSee(this),
        _ => false,
    };

    /// <summary>
    /// Whether what it's after is on the run (harder to hit): a player running, an actor
    /// chasing someone down
    /// </summary>
    private bool TargetIsRunning() => Target switch
    {
        PlayerPawn pawn => pawn.State.ThrustSpeed >= RunSpeed,
        Monster monster => monster.RuntimeFlags.Has(ObjFlags.AttackMode) && monster.Dir != ObjDir.NoDir,
        _ => false,
    };

    // How long (tics) an actor takes to react once it has noticed the player: its
    // `monster.reactiondelay: [base, divisor]`, base plus a random 0-255 over divisor (no divisor,
    // or 0, for none). Without one, a fixed tic (as vanilla's bosses).
    private short GetReactionDelay()
    {
        var delay = PropertyInts("monster.reactiondelay");
        if (delay.Count == 0)
            return 1;

        return (short)(delay[0] + (delay.Count > 1 && delay[1] > 0 ? NextRandom() / delay[1] : 0));
    }

    /// <summary>
    /// Its suspicion of a disguised player (Gameplay/Stealth.cs): tics it still waits for their
    /// papers, -1 once it's satisfied, 0 when it hasn't any
    /// </summary>
    internal int Suspicion;

    internal virtual bool SightPlayer()
    {
        // A BLIND actor (Blake Stone's volatile material transports, which just go their way)
        // never goes after the player
        if (HasFlag("BLIND"))
            return false;

        if (RuntimeFlags.Has(ObjFlags.AttackMode))
            GameEngineManager.Quit("An actor in ATTACKMODE called SightPlayer!");

        if (Temp2 != 0)
        {
            Temp2 -= (short)Tics;
            if (Temp2 > 0)
                return false;
            Temp2 = 0;
        }
        else
        {
            if (AreaNumber < MapManager.Floors.NumAreas && AreaByPlayer[AreaNumber] == 0)
                return false;

            // A disguised player it suspects, or has been shown papers by, it lets be (Gameplay/Stealth.cs)
            if (World.Stealth?.Overlooks(this) == true)
                return false;

            if (RuntimeFlags.Has(ObjFlags.Ambush))
            {
                if (!CheckSight())
                    return false;
                RuntimeFlags &= ~ObjFlags.Ambush;
            }
            else
            {
                // A NOTICEWHENSEEN actor (Blake Stone's aliens) notices the player once the player can see it
                bool seen = HasFlag("NOTICEWHENSEEN") && SeenByThePlayers();
                if (!MadeNoise && !seen && !CheckSight())
                    return false;

                // A noise (or being seen) only rouses it when there's someone for it to go after
                if (!HasFoe)
                    return false;
            }

            Temp2 = GetReactionDelay();
            RuntimeFlags &= ~ObjFlags.Friendly;   // it has seen the player: no longer friendly
            return false;
        }

        FirstSighting();
        return true;
    }

    internal void FirstSighting()
    {
        // The player's been noticed: not unseen any more (on a stealth-only map, caught)
        if (World.Stealth?.Spotted(this) == true)
            return;

        PlayActorSound(this, "seesound");
        StartChasing();
    }

    /// <summary>
    /// After the player from now on, as one that's seen them (FirstSighting, without its sound):
    /// the Chase state, the chase speed, and attacking. A_SpawnEnemy's "alert" spawns them so.
    /// </summary>
    internal void StartChasing()
    {
        SetState("Chase");

        // Chasing is faster than patrolling: either a set speed (the Ubermutant's 3000) or a
        // multiple of the actor's own
        if (Properties.TryGetValue("monster.chasespeed", out var chaseSpeed))
            Speed = Convert.ToInt32(chaseSpeed);
        else if (Properties.TryGetValue("monster.chasespeedmultiplier", out var mult))
            Speed *= Convert.ToInt32(mult);

        if (Distance < 0)
            Distance = 0;

        RuntimeFlags |= ObjFlags.AttackMode | ObjFlags.FirstAttack;
    }

    internal void SelectDodgeDir()
    {
        int deltax, deltay, i;
        uint absdx, absdy;
        var dirtry = new ObjDir[5];
        ObjDir tdir;
        ObjDir turnaround;

        if (RuntimeFlags.Has(ObjFlags.FirstAttack))
        {
            turnaround = ObjDir.NoDir;
            RuntimeFlags &= ~ObjFlags.FirstAttack;
        }
        else
            turnaround = Opposite[(byte)Dir];

        SeekDelta(out deltax, out deltay);

        if (deltax > 0)
        {
            dirtry[1] = ObjDir.East;
            dirtry[3] = ObjDir.West;
        }
        else
        {
            dirtry[1] = ObjDir.West;
            dirtry[3] = ObjDir.East;
        }

        if (deltay > 0)
        {
            dirtry[2] = ObjDir.South;
            dirtry[4] = ObjDir.North;
        }
        else
        {
            dirtry[2] = ObjDir.North;
            dirtry[4] = ObjDir.South;
        }

        absdx = (uint)Math.Abs(deltax);
        absdy = (uint)Math.Abs(deltay);

        if (absdx > absdy)
        {
            tdir = dirtry[1]; dirtry[1] = dirtry[2]; dirtry[2] = tdir;
            tdir = dirtry[3]; dirtry[3] = dirtry[4]; dirtry[4] = tdir;
        }

        if (NextRandom() < 128)
        {
            tdir = dirtry[1]; dirtry[1] = dirtry[2]; dirtry[2] = tdir;
            tdir = dirtry[3]; dirtry[3] = dirtry[4]; dirtry[4] = tdir;
        }

        dirtry[0] = Diagonal[(byte)dirtry[1], (byte)dirtry[2]];

        for (i = 0; i < 5; i++)
        {
            if (dirtry[i] == ObjDir.NoDir || dirtry[i] == turnaround)
                continue;

            Dir = dirtry[i];
            if (TryWalk())
                return;
        }

        if (turnaround != ObjDir.NoDir)
        {
            Dir = turnaround;
            if (TryWalk())
                return;
        }

        Dir = ObjDir.NoDir;
    }

    internal void SelectChaseDir()
    {
        int deltax, deltay;
        var d = new ObjDir[3];
        ObjDir tdir, olddir;
        ObjDir turnaround;

        olddir = Dir;
        turnaround = Opposite[(byte)olddir];

        // Following (FollowStep): towards the tile it's walking to, not what it's after
        if (_walkGoal is { } goal)
            (deltax, deltay) = (goal.X - TileX, goal.Y - TileY);
        else
            SeekDelta(out deltax, out deltay);

        d[1] = ObjDir.NoDir;
        d[2] = ObjDir.NoDir;

        if (deltax > 0) d[1] = ObjDir.East;
        else if (deltax < 0) d[1] = ObjDir.West;
        if (deltay > 0) d[2] = ObjDir.South;
        else if (deltay < 0) d[2] = ObjDir.North;

        if (Math.Abs(deltay) > Math.Abs(deltax))
        {
            tdir = d[1]; d[1] = d[2]; d[2] = tdir;
        }

        if (d[1] == turnaround) d[1] = ObjDir.NoDir;
        if (d[2] == turnaround) d[2] = ObjDir.NoDir;

        if (d[1] != ObjDir.NoDir)
        {
            Dir = d[1];
            if (TryWalk()) return;
        }

        if (d[2] != ObjDir.NoDir)
        {
            Dir = d[2];
            if (TryWalk()) return;
        }

        if (olddir != ObjDir.NoDir)
        {
            Dir = olddir;
            if (TryWalk()) return;
        }

        if (NextRandom() > 128)
        {
            for (tdir = ObjDir.North; tdir <= ObjDir.West; tdir++)
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
            for (tdir = ObjDir.West; tdir >= ObjDir.North; tdir--)
            {
                if (tdir != turnaround)
                {
                    Dir = tdir;
                    if (TryWalk()) return;
                }
            }
        }

        if (turnaround != ObjDir.NoDir)
        {
            Dir = turnaround;
            if (Dir != ObjDir.NoDir)
            {
                if (TryWalk()) return;
            }
        }

        Dir = ObjDir.NoDir;
    }

    internal void SelectRunDir()
    {
        int deltax, deltay;
        var d = new ObjDir[3];
        ObjDir tdir;

        deltax = Target.TileX - TileX;
        deltay = Target.TileY - TileY;

        d[1] = deltax < 0 ? ObjDir.East : ObjDir.West;
        d[2] = deltay < 0 ? ObjDir.South : ObjDir.North;

        if (Math.Abs(deltay) > Math.Abs(deltax))
        {
            tdir = d[1]; d[1] = d[2]; d[2] = tdir;
        }

        Dir = d[1];
        if (TryWalk()) return;

        Dir = d[2];
        if (TryWalk()) return;

        if (NextRandom() > 128)
        {
            for (tdir = ObjDir.North; tdir <= ObjDir.West; tdir++)
            {
                Dir = tdir;
                if (TryWalk()) return;
            }
        }
        else
        {
            for (tdir = ObjDir.West; tdir >= ObjDir.North; tdir--)
            {
                Dir = tdir;
                if (TryWalk()) return;
            }
        }

        Dir = ObjDir.NoDir;
    }

    // Called as a patroller (T_Path, T_BJRun) reaches a tile's centre: a patrol point there
    // (actordefs PatrolPoint, placed on the legacy arrow tiles) turns it the way the point faces
    internal virtual void SelectPathDir()
    {
        if (MapManager.PatrolPointAt(TileX, TileY) is { } point)
            Dir = point.Dir;

        Distance = (int)MapConstants.TileGlobal;

        if (!TryWalk())
            Dir = ObjDir.NoDir;
    }

    /*
    =============================================================================
    COMBAT (ported from WL_STATE.C)
    =============================================================================
    */

    // The damage of the blow that's killing it (Damage sets it for Kill), 0 when it dies some
    // other way (a cheat, a smart switch)
    private int _killingBlow;

    /*
    =============================================================================
    DEATH STATES

    How it dies follows how it was killed (End of Destiny's gibbing and stealth deaths,
    Midnight Assault's gibbed, burned, sniped and silent ones; ZDoom's Death.Type and XDeath):

      Death.<Type>  the killing blow's damage type, when the class has that state: the
                    attacker's `damagetype` (a missile's, a blast's, a monster's), or for a
                    player's shot or blow their weapon's `weapon.damagetype` (a back-stab's,
                    `weapon.backstabtype`, Backstab when unset). Death.Fire,
                    Death.Melee, Death.Stealth... (any case)
      XDeath        an overkill: a killing blow of at least `death.gibdamage`, or health left
                    below -`death.gibhealth`; with `death.gibtypes: [...]`, only by those types
      Death         otherwise

    A state can pick again among them by chance or anything else (A_Jump, a 0-tic frame's
    next-state): Midnight Assault's sniped death is a Death.Sniper that's taken one time in four.
    =============================================================================
    */

    /// <summary>
    /// The damage type of the blow being dealt now, when the attacker alone doesn't say it (a
    /// back-stab: weapon.backstabtype); set around that one blow
    /// </summary>
    internal string? BlowType;

    /// <summary>The killing blow's damage type: see DEATH STATES; null for none</summary>
    internal string? KillingDamageType(Entities.Actors.Actor? attacker)
    {
        if (BlowType is { Length: > 0 } blow)
            return blow;
        if (attacker is PlayerPawn pawn)
            return pawn.State.Weapon is { Length: > 0 } weapon ? InventoryManager.GetStringProperty(weapon, "weapon.damagetype") : null;
        return attacker?.Properties.TryGetValue("damagetype", out var type) == true && type?.ToString() is { Length: > 0 } name ? name : null;
    }

    /// <summary>The state it dies into, killed by <paramref name="attacker"/> (see DEATH STATES)</summary>
    internal string DeathStateFor(Entities.Actors.Actor? attacker) =>
        ChooseDeathState(ResolvedStates.Keys, KillingDamageType(attacker), _killingBlow, Hitpoints,
            PropertyInt("death.gibdamage", 0), PropertyInt("death.gibhealth", -1), PropertyStrings("death.gibtypes"));

    /// <summary>
    /// The death state among <paramref name="states"/>: Death.(damage type), then XDeath for an
    /// overkill (a blow of at least gibDamage when that's over 0, or health left below -gibHealth
    /// when that's 0 or more; only by gibTypes when there are any), then Death
    /// </summary>
    internal static string ChooseDeathState(IEnumerable<string> states, string? damageType, int blow, int healthLeft,
        int gibDamage, int gibHealth, IReadOnlyCollection<string> gibTypes)
    {
        var names = states as ICollection<string> ?? states.ToList();
        if (damageType is { Length: > 0 } && names.FirstOrDefault(s => s.Equals("Death." + damageType, StringComparison.OrdinalIgnoreCase)) is { } typed)
            return typed;

        bool overkill = gibDamage > 0 && blow >= gibDamage || gibHealth >= 0 && healthLeft < -gibHealth;
        bool gibbingType = gibTypes.Count == 0 || damageType != null && gibTypes.Contains(damageType, StringComparer.OrdinalIgnoreCase);
        if (overkill && gibbingType && names.Contains("XDeath"))
            return "XDeath";

        return "Death";
    }

    internal virtual void Kill(Entities.Actors.Actor? attacker = null)
    {
        var tilex = X >> (int)MapConstants.TileShift;
        var tiley = Y >> (int)MapConstants.TileShift;

        // `monster.floordeath`: on that floor (game-info floor-number) it isn't killed but goes
        // to its FloorDeath state, worth nothing yet (Goldfire morphing on Planet Strike's last)
        if (PropertyInt("monster.floordeath", -1) is >= 0 and var deathFloor && deathFloor == MapManager.CurrentFloorNumber
            && ResolvedStates.ContainsKey("FloorDeath"))
        {
            SetState("FloorDeath");
            RuntimeFlags &= ~(ObjFlags.Shootable | ObjFlags.Friendly);
            return;
        }

        // As KillActor: it dies on the tile it's over, not the one it was walking to (except a
        // KEEPTILEONDEATH one: Mecha Hitler, whose Hitler would otherwise jump: MCS's fix), and
        // leaves ActorAt[] until its corpse lies on an empty tile
        if (!HasFlag("KEEPTILEONDEATH"))
        {
            TileX = (byte)tilex;
            TileY = (byte)tiley;
            SyncPosition();
        }
        MapManager.UnmarkActorTile(TileX, TileY);

        // Points for the player's kills (and a trap's), not for one enemy killing another, nor
        // for killing one again that a reviver brought back
        var killedBy = attacker?.Shooter ?? attacker;
        if (killedBy is not Monster && !RuntimeFlags.Has(ObjFlags.Revived))
            AwardKillPoints();
        if (killedBy is PlayerPawn killer)
            killer.State.Kills++;       // for the scoreboard, with others (a missile's: who fired it)
        World.Objectives?.ActorDied(this);     // an `objective` enemy's death (Gameplay/Objectives.cs)
        SetState(DeathStateFor(attacker));
        OnKilled(attacker);

        // One of the player's own side down: they hear of it (as an obituary)
        if (LocalPlayer.Pawn is { } local && IsAllyOf(local))
        {
            HudMessageManager.Show(HudMessageKind.Obituary, killedBy == null ? "$OB_ALLYDIED" : "$OB_ALLYKILLED", null,
                new Dictionary<char, string> { ['o'] = ActorTag(this), ['k'] = killedBy == null ? "" : ActorTag(killedBy) });
        }

        if (LeavesDrops && DeathDrop() is { } drop)
            SpawnItemAt(drop, tilex, tiley);
        CarriedDeathWork(this, tilex, tiley);

        // One with a death cam (a DeathCam state: Wolf3D's episode bosses) remembers the killer's spot for it
        if (ResolvedStates.ContainsKey("DeathCam"))
        {
            PlayerState.KillX = Player.X;
            PlayerState.KillY = Player.Y;
        }

        // A sleeper that becomes an enemy (`monster.becomes`) isn't the kill; that enemy is.
        // NOTCOUNTED actors (crates, Goldfire, who keeps coming back) aren't enemies to count.
        // Whoever killed it, as the kill ratio counts the players' enemies however they die
        if (CountsAsKill && !Properties.ContainsKey("monster.becomes"))
        {
            GameState.KillCount++;
            GameState.PointsCount += PropertyInt("points", 0);     // toward the points ratio (Blake Stone's)
            if (killedBy is PlayerPawn player)
            {
                bool unseen = World.Stealth?.Killed(player) == true;    // a knife or a scope's kill, unseen (Gameplay/Stealth.cs)
                World.KillChains?.Killed(this, player.State, tilex, tiley, unseen);
            }
        }
        RuntimeFlags &= ~(ObjFlags.Shootable | ObjFlags.Friendly);
        RuntimeFlags |= ObjFlags.NonMark;
    }

    /// <summary>
    /// Whether, until it has been on screen (Active), it does nothing while its area isn't
    /// connected to the player's, as Wolf3D's DoActor had it
    /// </summary>
    internal virtual bool SleepsOutOfReach => true;

    /// <summary>Its entry in ActorAt[] while it's marked there (MapManager.MarkActorTile)</summary>
    internal ActorMark Mark => _mark is { } mark && ReferenceEquals(mark.Who, this) ? mark : _mark = new ActorMark(this);   // not a copy's (`with`)
    private ActorMark? _mark;

    /// <summary>Whether it counts toward the level's kills (its total, and the count as each dies); NOTCOUNTED actors don't</summary>
    internal virtual bool IsKill => !HasFlag("NOTCOUNTED");

    /// <summary>
    /// Whether it's in the level's kill total and counted as it dies: a kill (IsKill) that isn't
    /// on the players' side (factions.yaml: it doesn't count them its allies)
    /// </summary>
    internal bool CountsAsKill => IsKill && !FactionManager.Table.IsAlly(Faction, PlayersFaction);

    // The local player's side (their pawn's, or their class's before they have one)
    private string PlayersFaction =>
        LocalPlayer.Pawn?.Faction ?? FactionManager.PlayerFaction(LocalPlayer.PlayerClass);

    /// <summary>Whether dying, it leaves its `dropweapon`/`dropitem`</summary>
    protected virtual bool LeavesDrops => true;

    /// <summary>Whether it has used up the shots it started with (a Monster has no count of them)</summary>
    protected virtual bool OutOfAmmo => false;

    /// <summary>Its `points` for killing it</summary>
    protected virtual void AwardKillPoints()
    {
        if (!Properties.TryGetValue("points", out var points))
            return;

        // POINTSONCE actors (Spectres, which come back) only pay out the first time;
        // Bonus is set at spawn and cleared here, as in the original
        if (!HasFlag("POINTSONCE"))
            GivePoints(Convert.ToInt32(points));
        else if (RuntimeFlags.Has(ObjFlags.Bonus))
        {
            GivePoints(Convert.ToInt32(points));
            RuntimeFlags &= ~ObjFlags.Bonus;
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
                if (!InventoryManager.Has(weaponName))
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
            if (floor != MapManager.CurrentFloorNumber || !LevelAI.TakeFloorDrop())
                return null;
        }

        bool outOfAmmo = OutOfAmmo;
        if (outOfAmmo && PropertyBool("dropitem.needsammo"))
            return null;

        var drop = Properties.TryGetValue("dropitem", out var dropitem) ? dropitem as string : null;
        if (Properties.TryGetValue("dropitem.alt", out var alt) && alt is string altName
            && (outOfAmmo || NextRandom() < PropertyInt("dropitem.altchance", 128)))
            drop = altName;
        return drop;
    }

    internal virtual void Damage(uint damage, Entities.Actors.Actor? attacker = null)
    {
        // `monster.minweapon` (the hanging turret's rapid assault weapon): only that weapon, or a
        // better one (by weapon.selectionorder), in Blake's hand hurts it -- when it's Blake (or
        // something he fired) that hurts it, not another actor
        var source = attacker?.Shooter ?? attacker;
        bool byPlayer = source is null or PlayerPawn;
        if (byPlayer && Properties.TryGetValue("monster.minweapon", out var minWeapon) && minWeapon is string minWeaponName
            && (PlayerState.Weapon == null || WeaponSelectionOrder(PlayerState.Weapon) > WeaponSelectionOrder(minWeaponName)))
            return;

        // It turns on whoever hurt it (or fired what did), unless they're on its side: a neutral
        // side's actor joins a fight this way
        if (source is Monster or PlayerPawn && !ReferenceEquals(source, this) && IsTargetable(source) && !IsAllyOf(source))
            _target = source;

        // A silent weapon (weapon.silent, Blake Stone's auto-charge pistol) doesn't alert anyone
        if (!(attacker is PlayerPawn && PlayerWeaponIsSilent()))
            MadeNoise = true;

        if (!RuntimeFlags.Has(ObjFlags.AttackMode))
            damage <<= 1;

        int oldHitpoints = Hitpoints;
        Hitpoints -= (short)damage;
        CloakHit = true;

        if (Hitpoints <= 0)
        {
            _killingBlow = (int)damage;     // for Kill's choice of death state
            try
            {
                Kill(attacker);
            }
            finally
            {
                _killingBlow = 0;
            }
            return;
        }

        if (OnHurt(oldHitpoints))
            return;

        if (!RuntimeFlags.Has(ObjFlags.AttackMode) && NoticesWhenHurt)
            FirstSighting();

        // `monster.damagestates: [full, 3/4, 1/2, 1/4]` (the floating bomb, which looks more
        // battered as it's hurt): it goes to the one for the health it has left instead of Pain.
        // A `monster.painonce` actor (the projection generator) only flinches at its first hit.
        int fullHealth = MapManager.GetScaledHealth(this);
        var damageStates = PropertyStrings("monster.damagestates");
        if (damageStates.Count > 0)
        {
            int stage = Hitpoints > 3 * fullHealth / 4 ? 0 : Hitpoints > fullHealth / 2 ? 1 : Hitpoints > fullHealth / 4 ? 2 : 3;
            SetState(damageStates[Math.Min(stage, damageStates.Count - 1)]);
        }
        // As DamageActor: "Pain" with odd hitpoints left, its "Pain1" variant (when it has one)
        // with even, so the flinch looks different hit to hit
        else if (ResolvedStates.ContainsKey("Pain") && (!PropertyBool("monster.painonce") || oldHitpoints >= fullHealth))
            SetState((Hitpoints & 1) == 0 && ResolvedStates.ContainsKey("Pain1") ? "Pain1" : "Pain");

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
        deltax = Target.TileX - TileX;
        deltay = Target.TileY - TileY;
    }

    /*
    =============================================================================
    PER-ENEMY THINK/ACTION FUNCTIONS (ported from WL_ACT2.C)
    =============================================================================
    */

    internal virtual void Stand() => SightPlayer();

    internal virtual void Patrol()
    {
        // A STATIONARY actor (Blake Stone's parked transports) stays put
        if (HasFlag("STATIONARY"))
            return;

        // A friendly patroller only looks for the player once there's been a noise
        if ((!RuntimeFlags.Has(ObjFlags.Friendly) || MadeNoise) && SightPlayer())
            return;

        if (Dir == ObjDir.NoDir)
        {
            SelectPathDir();
            if (Dir == ObjDir.NoDir)
                return;
        }

        var move = (int)(Speed * Tics);

        while (move != 0)
        {
            if (Distance < 0)
            {
                OpenDoor(-Distance - 1);
                if (DoorObjects[-Distance - 1].Action != (byte)DoorAction.Open)
                    return;
                Distance = (int)MapConstants.TileGlobal;
                if (!(DemoRecord || DemoPlayback))
                    TryWalk();
            }

            if (move < Distance)
            {
                MoveObj(move);
                break;
            }

            if (TileX > MapManager.MapWidth || TileY > MapManager.MapHeight)
                GameEngineManager.Quit($"T_Path hit a wall at {TileX},{TileY}, dir {Dir}");

            ArriveOnTile();
            move -= Distance;

            SelectPathDir();

            if (Dir == ObjDir.NoDir)
                return;
        }
    }

    internal virtual void Chase()
    {
        if (GameState.VictoryFlag)
            return;

        var dodge = false;

        if (CheckLine(this, Target))
        {
            Hidden = false;
            var dx = Math.Abs(TileX - Target.TileX);
            var dy = Math.Abs(TileY - Target.TileY);
            var dist = dx > dy ? dx : dy;
            int chance;

            if (DemoRecord || DemoPlayback)
            {
                chance = (dist == 0 || (dist == 1 && Distance < 0x4000)) ? 300 : (int)((Tics << 4) / dist);
            }
            else
            {
                chance = dist != 0 ? (int)((Tics << 4) / dist) : 300;

                if (dist == 1)
                {
                    var target = Math.Abs(X - Target.X);
                    if (target < 0x14000L)
                    {
                        target = Math.Abs(Y - Target.Y);
                        if (target < 0x14000L)
                            chance = 300;
                    }
                }
            }

            if (NextRandom() < chance)
            {
                SetState("Attack");
                return;
            }
            dodge = true;
        }
        else
            Hidden = true;

        if (Dir == ObjDir.NoDir)
        {
            SelectChaseOrWanderDir(dodge);
            if (Dir == ObjDir.NoDir)
                return;
        }

        var move = (int)(Speed * Tics);

        while (move != 0)
        {
            if (Distance < 0)
            {
                OpenDoor(-Distance - 1);
                if (DoorObjects[-Distance - 1].Action != (byte)DoorAction.Open)
                    return;
                Distance = (int)MapConstants.TileGlobal;
                if (!(DemoRecord || DemoPlayback))
                    TryWalk();
            }

            if (move < Distance)
            {
                MoveObj(move);
                break;
            }

            ArriveOnTile();
            move -= Distance;

            SelectChaseOrWanderDir(dodge);

            if (Dir == ObjDir.NoDir)
                return;
        }
    }

    /*
    =============================================================================

                                    RETREATING

    A_RetreatTo("Class"[, tag[, speed]]) sends a monster off to the nearest actor of that class
    (with that tag, if given: a marker the map places), at that speed if given; it can't be
    shot on the way. T_Retreat runs it there by the shortest open way (doors opened as it
    reaches them; another monster in the way it waits behind), and once it's within 3 tiles
    each way it's put on the spot and goes to its Retreated state (Midnight Assault's
    Commander, running off between phases to come back as the next: T_Retreat,
    SelectRetreatDir). With no way there at all, or no goal (a game loaded mid-run), it's put
    there at once.

    =============================================================================
    */

    // Its retreat's goal tile (A_RetreatTo); -1 for none
    internal int RetreatX = -1, RetreatY = -1;

    /// <summary>A_RetreatTo: see RETREATING</summary>
    internal void RetreatTo(string[] args)
    {
        var className = args.ElementAtOrDefault(0)?.Trim() ?? "";
        ushort? tag = ushort.TryParse(args.ElementAtOrDefault(1), out var t) && t > 0 ? t : null;
        var goal = MapManager.GetActors()
            .Where(a => !a.IsRemoved && a.Name.Equals(className, StringComparison.OrdinalIgnoreCase) && (tag == null || a.Tag == tag))
            .OrderBy(a => Math.Max(Math.Abs(a.TileX - TileX), Math.Abs(a.TileY - TileY)))
            .FirstOrDefault();
        if (goal == null)
        {
            Console.WriteLine($"A_RetreatTo ({Name}): there's no \"{className}\"{(tag != null ? $" with tag {tag}" : "")} to go to.");
            (RetreatX, RetreatY) = (-1, -1);
            return;
        }

        (RetreatX, RetreatY) = (goal.TileX, goal.TileY);
        if (int.TryParse(args.ElementAtOrDefault(2), out var speed) && speed > 0)
            Speed = speed;
        RuntimeFlags &= ~ObjFlags.Shootable;        // out of reach on its way
        Dir = ObjDir.NoDir;
    }

    bool NearRetreat => Math.Abs(RetreatX - TileX) <= 3 && Math.Abs(RetreatY - TileY) <= 3;

    /// <summary>T_Retreat: see RETREATING</summary>
    internal void Retreat()
    {
        if (GameState.VictoryFlag)
            return;
        if (RetreatX < 0 || NearRetreat)
        {
            ArriveFromRetreat();
            return;
        }

        if (Dir == ObjDir.NoDir)
        {
            if (!SelectRetreatDir())
                return;
            if (Dir == ObjDir.NoDir)
                return;
        }

        var move = (int)(Speed * Tics);
        while (move != 0)
        {
            if (Distance < 0)
            {
                OpenDoor(-Distance - 1);
                if (DoorObjects[-Distance - 1].Action != (byte)DoorAction.Open)
                    return;
                Distance = (int)MapConstants.TileGlobal;
                if (!(DemoRecord || DemoPlayback))
                    TryWalk();
            }

            if (move < Distance)
            {
                MoveObj(move);
                break;
            }

            ArriveOnTile();
            move -= Distance;
            if (NearRetreat)
            {
                ArriveFromRetreat();
                return;
            }
            if (!SelectRetreatDir() || Dir == ObjDir.NoDir)
                return;
        }
    }

    // On the spot (its retreat's goal, if it has one), then its Retreated state
    void ArriveFromRetreat()
    {
        if (RetreatX >= 0)
        {
            MapManager.UnmarkActorTile(TileX, TileY);
            SetPosition(RetreatX, RetreatY);        // its think marks it there again, as it lives
        }
        (RetreatX, RetreatY) = (-1, -1);
        Dir = ObjDir.NoDir;
        if (ResolvedStates.ContainsKey("Retreated"))
            SetState("Retreated");
    }

    /// <summary>
    /// The next step toward its retreat's goal: of the four ways, the one nearest by the open
    /// way there that it can walk (none: it waits a turn). False, having put it there, when
    /// there's no way there at all.
    /// </summary>
    bool SelectRetreatDir()
    {
        Dir = ObjDir.NoDir;
        var steps = RetreatSteps(RetreatX, RetreatY);
        int here = steps[TileX, TileY];
        if (here < 0)
        {
            Console.WriteLine($"T_Retreat ({Name}): no way from {TileX},{TileY} to {RetreatX},{RetreatY}; it's put there.");
            ArriveFromRetreat();
            return false;
        }

        (ObjDir Dir, int Dx, int Dy)[] ways = [(ObjDir.East, 1, 0), (ObjDir.North, 0, -1), (ObjDir.West, -1, 0), (ObjDir.South, 0, 1)];
        foreach (var way in ways
            .Where(w => InMap(TileX + w.Dx, TileY + w.Dy) && steps[TileX + w.Dx, TileY + w.Dy] is >= 0 and var n && n < here)
            .OrderBy(w => steps[TileX + w.Dx, TileY + w.Dy]))
        {
            Dir = way.Dir;
            if (TryWalk())
                return true;
        }
        Dir = ObjDir.NoDir;
        return true;
    }

    bool InMap(int x, int y) => x >= 0 && y >= 0 && x < MapManager.MapWidth && y < MapManager.MapHeight;

    /// <summary>
    /// Steps from each tile to (gx, gy) by the open way, four ways, through doors but not walls,
    /// solid things or a monster blocker it's kept off; -1 where there's no way
    /// </summary>
    int[,] RetreatSteps(int gx, int gy)
    {
        int w = MapManager.MapWidth, h = MapManager.MapHeight;
        var steps = new int[w, h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                steps[x, y] = -1;
        if (!InMap(gx, gy))
            return steps;

        bool Open(int x, int y)
        {
            if (!InMap(x, y) || Blocked(x, y))
                return false;
            var tile = MapManager.TileMap[x, y];
            if (tile != 0 && (tile & GameConstants.BitDoor) == 0)
                return false;
            return MapManager.ActorAt[x, y] is not BlockingActor;
        }

        var queue = new Queue<(int X, int Y)>();
        steps[gx, gy] = 0;
        queue.Enqueue((gx, gy));
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
            {
                if (!InMap(nx, ny) || steps[nx, ny] >= 0 || !((nx, ny) == (TileX, TileY) || Open(nx, ny)))
                    continue;
                steps[nx, ny] = steps[x, y] + 1;
                queue.Enqueue((nx, ny));
            }
        }
        return steps;
    }

    internal void DogChase()
    {
        if (Dir == ObjDir.NoDir)
        {
            SelectDodgeDir();
            if (Dir == ObjDir.NoDir)
                return;
        }

        var move = (int)(Speed * Tics);

        while (move != 0)
        {
            var dx = Target.X - X;
            if (dx < 0) dx = -dx;
            dx -= move;
            if (dx <= MinActorDist)
            {
                var dy = Target.Y - Y;
                if (dy < 0) dy = -dy;
                dy -= move;
                if (dy <= MinActorDist)
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

            ArriveOnTile();
            move -= Distance;

            SelectDodgeDir();
            if (Dir == ObjDir.NoDir)
                return;
        }
    }

    internal void Bite()
    {
        if (BiteLands(180))
            BiteTarget();
    }

    /// <summary>
    /// A bite's snap (its attacksound), and whether it lands: its target within a tile each way
    /// and a random 0-255 under <paramref name="chance"/>
    /// </summary>
    internal bool BiteLands(int chance)
    {
        if (Properties.TryGetValue("attacksound", out var sound) && sound is string soundName)
            PlaySoundAtActor(soundName, this);

        var dx = Target.X - X;
        if (dx < 0) dx = -dx;
        dx -= (int)MapConstants.TileGlobal;
        if (dx <= MinActorDist)
        {
            var dy = Target.Y - Y;
            if (dy < 0) dy = -dy;
            dy -= (int)MapConstants.TileGlobal;
            if (dy <= MinActorDist)
                return NextRandom() < chance;
        }
        return false;
    }

    /// <summary>A bite on its target: 0-15</summary>
    internal void BiteTarget() => HurtTarget(NextRandom() >> 4);

    internal void GhostChase()
    {
        if (Dir == ObjDir.NoDir)
        {
            SelectChaseDir();
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

            ArriveOnTile();
            move -= Distance;

            SelectChaseDir();
            if (Dir == ObjDir.NoDir)
                return;
        }
    }

    // T_DodgeChase (Schabbs, Gift, Fat, Wilhelm, the Death Knight, the Angel): keeps a shot lined
    // up when possible, otherwise dodges toward the player (or runs, once very close).
    internal void DodgeChase()
    {
        var dx = Math.Abs(TileX - Target.TileX);
        var dy = Math.Abs(TileY - Target.TileY);
        var dist = dx > dy ? dx : dy;
        var dodge = false;

        if (CheckLine(this, Target))
        {
            Hidden = false;
            if (NextRandom() < (Tics << 3))
            {
                SetState("Attack");
                return;
            }
            dodge = true;
        }
        else
            Hidden = true;

        if (Dir == ObjDir.NoDir)
        {
            SelectChaseOrWanderDir(dodge);
            if (Dir == ObjDir.NoDir)
                return;
        }

        var move = (int)(Speed * Tics);

        while (move != 0)
        {
            if (Distance < 0)
            {
                OpenDoor(-Distance - 1);
                if (DoorObjects[-Distance - 1].Action != (byte)DoorAction.Open)
                    return;
                Distance = (int)MapConstants.TileGlobal;
                TryWalk();
            }

            if (move < Distance)
            {
                MoveObj(move);
                break;
            }

            ArriveOnTile();
            move -= Distance;

            if (dist < 4) SelectRunDir();
            else if (dodge) SelectDodgeDir();
            else SelectChaseDir();

            if (Dir == ObjDir.NoDir)
                return;
        }
    }

    internal void Strafe()
    {
        if (CheckLine(this, Target))
        {
            Hidden = false;
            if (NextRandom() < (Tics << 1))
            {
                SetState("Attack");
                return;
            }
        }
        else
            Hidden = true;

        if (Dir == ObjDir.NoDir)
        {
            SelectDodgeDir();
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

            ArriveOnTile();
            move -= Distance;

            SelectDodgeDir();
            if (Dir == ObjDir.NoDir)
                return;
        }
    }

    internal virtual void Shoot()
    {
        if (AreaNumber < MapManager.Floors.NumAreas && AreaByPlayer[AreaNumber] == 0)
            return;

        if (CheckLine(this, Target))
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
        var dx = Math.Abs(TileX - Target.TileX);
        var dy = Math.Abs(TileY - Target.TileY);
        var dist = dx > dy ? dx : dy;

        if (Properties.ContainsKey("monster.sharpshooter"))
            dist = dist * 2 / 3;

        int hitchance;
        bool visible = TargetSeesMe();
        if (TargetIsRunning())
            hitchance = visible ? 160 - dist * 16 : 160 - dist * 8;
        else
            hitchance = visible ? 256 - dist * 16 : 256 - dist * 8;

        if (NextRandom() < hitchance)
        {
            int damage = dist < 2 ? NextRandom() >> 2 : dist < 4 ? NextRandom() >> 3 : NextRandom() >> 4;
            HurtTarget(damage);
        }
        else
            MissPuff();
    }

    /// <summary>
    /// A shot that missed: its `monster.puff` (an actordefs class) where it struck, the line from
    /// it past the player, turned 2 to 6 degrees either way, meeting a wall or a door that isn't
    /// open (Midnight Assault's enemy bullets, traced as the player's are); none on a wall with
    /// walls.yaml `puffs: false`. No puff, and no random numbers drawn, without one.
    /// </summary>
    void MissPuff()
    {
        if (!Properties.TryGetValue("monster.puff", out var cls) || cls?.ToString() is not { Length: > 0 } puff)
            return;
        double toTarget = Math.Atan2(Y - Target.Y, Target.X - X) * 180 / Math.PI;
        int turn = 2 + NextRandom() % 5;
        double angle = toTarget + (NextRandom() > 127 ? -turn : turn);
        var trace = World.LineOfSight.TraceShot(X, Y, angle);
        if (trace.End == ShotEnd.Wall)
            MapManager.SpawnAt(puff, trace.X, trace.Y, this);
    }

    // Spawns a projectile actor (Needle/Rocket/Fire, actordefs/wolf3d/projectiles.yaml) at the
    // thrower and aims it at the player. TicCount 1 makes its first frame expire on the very
    // next tic, so the state's Action (a rocket's first smoke puff) fires almost immediately.
    // angleOffset turns the shot away from the player, in Angles units (the Death Knight's
    // rockets go 4 either side). With no sound given, the projectile's own attacksound plays.
    private void ThrowProjectile(string className, int speed, string? sound = null, int angleOffset = 0)
    {
        var deltax = Target.X - X;
        var deltay = Y - Target.Y;
        var angle = (float)Math.Atan2((float)deltay, (float)deltax);
        if (angle < 0) angle = (float)(MathPi * 2 + angle);
        var iangle = (int)(angle / (MathPi * 2) * Angles);
        iangle = ((iangle + angleOffset) % Angles + Angles) % Angles;

        var newobj = MapManager.SpawnAtActor(className, this);
        if (newobj == null)
            return;

        newobj.TicCount = 1;
        newobj.Angle = (short)iangle;
        newobj.Speed = speed;
        newobj.Shooter = this;

        if (sound == null && newobj.Properties.TryGetValue("attacksound", out var attackSound))
            sound = attackSound as string;
        if (!string.IsNullOrEmpty(sound))
            PlaySoundAtActor(sound, newobj);
    }

    // T_Shoot's `monster.pointblankdamage` (the Ubermutant's 10): that much more damage when
    // right beside the player, whether or not the volley itself went off
    internal void PointBlankHit()
    {
        if (PropertyInt("monster.pointblankdamage", 0) is not (> 0 and var damage))
            return;

        var dx = Math.Abs(TileX - Target.TileX);
        var dy = Math.Abs(TileY - Target.TileY);
        if (Math.Max(dx, dy) <= 1)
            HurtTarget(damage);
    }

    /// <summary>
    /// A_FireProjectile("HRocket"[, angle offset[, "shoot"]]): launches a projectile at the player,
    /// turned by the offset in Angles units; with "shoot", also fires a gun volley (T_Shoot), as the
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
            speed = baseSpeed + (RuntimeActorProperty(args[0], "speed.random") is { } extra and > 0 ? NextRandom() * extra / 256 : 0);
        if (RuntimeActorProperty(args[0], "spread") is { } spread and > 0)
            angleOffset += NextRandom() % (spread * 2 + 1) - spread;

        ThrowProjectile(args[0], speed, angleOffset: angleOffset);
    }

    // A whole-number property of an actordefs class, or null
    int? RuntimeActorProperty(string className, string key) =>
        InventoryManager.GetProperty(className, key) is { } value && int.TryParse(value.ToString(), out var n) ? n : null;

    // A volley (see RegisterActions): A_StartAttack starts the count, A_Relaunch follows each
    // shot and either tires it out after the last, breaks off at random, or goes again
    internal void StartAttack() => Temp1 = 0;

    internal void Relaunch(string[] args)
    {
        if (args.Length < 2 || !int.TryParse(args[0].Trim(), out var count) || string.IsNullOrWhiteSpace(args[1]))
        {
            Console.WriteLine($"A_Relaunch ({Name}): give a shot count and a state, as A_Relaunch(3, \"Tired\").");
            return;
        }

        if (++Temp1 == count)
        {
            SetState(args[1].Trim());
            return;
        }

        if ((NextRandom() & 1) != 0)
            SetState(args.Length > 2 && !string.IsNullOrWhiteSpace(args[2]) ? args[2].Trim() : "Chase");
    }


    // A dead Spectre comes back once nothing is in the way: the player isn't right on top of
    // it, and the tiles it covers hold no wall, door, blocking object or live enemy. As Spear's,
    // it comes back with the health it died with (any hit kills it again); A_Dormant("heal")
    // gives it its full health back (End of Destiny's T_ResFlags)
    internal void Dormant(string[]? args = null)
    {
        foreach (var pawn in MapManager.Players)
        {
            var deltax = X - pawn.X;
            var deltay = Y - pawn.Y;
            if (deltax >= -MinActorDist && deltax <= MinActorDist && deltay >= -MinActorDist && deltay <= MinActorDist)
                return;
        }

        var xl = (int)((X - MinDist) >> (int)MapConstants.TileShift);
        var xh = (int)((X + MinDist) >> (int)MapConstants.TileShift);
        var yl = (int)((Y - MinDist) >> (int)MapConstants.TileShift);
        var yh = (int)((Y + MinDist) >> (int)MapConstants.TileShift);

        for (var y = yl; y <= yh; y++)
            for (var x = xl; x <= xh; x++)
            {
                if (MapManager.ActorAt[x, y] != null || MapManager.IsShootableActorAt(x, y))
                    return;
            }

        RuntimeFlags |= ObjFlags.Ambush | ObjFlags.Shootable;
        RuntimeFlags &= ~(ObjFlags.AttackMode | ObjFlags.NonMark);
        Dir = ObjDir.NoDir;
        if (args?.Any(a => a.Trim().Equals("heal", StringComparison.OrdinalIgnoreCase)) == true)
            Hitpoints = MapManager.GetScaledHealth(this);
        SetState("Spawn");
    }

    /// <summary>A_Morph: the enemy of class args[0] takes its place (see RegisterActions)</summary>
    internal void Morph(string[] args)
    {
        var className = args.Length > 0 ? args[0].Trim() : "";
        if (className.Length == 0)
        {
            Console.WriteLine($"A_Morph ({Name}): no class given.");
            return;
        }

        var newActor = MapManager.SpawnMorphedEnemy(className, this);
        if (newActor == null)
        {
            Console.WriteLine($"A_Morph ({Name}): there's no actordefs class \"{className}\".");
            return;
        }

        // As a map spawn of it would: its own speed, and a kill to count unless it's NOTCOUNTED
        newActor.Speed = newActor.PropertyInt("speed", SpdPatrol);
        if (!LoadedGame && newActor is Monster { CountsAsKill: true })
        {
            GameState.KillTotal++;
            GameState.PointsTotal += MapManager.KillWorth(newActor);
        }

        if (!args.Skip(1).Any(a => a.Trim().Equals("keep", StringComparison.OrdinalIgnoreCase)))
            MapManager.MarkForRemoval(this);
    }
}