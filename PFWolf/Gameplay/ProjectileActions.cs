using PFWolf.Constants;

namespace PFWolf.Gameplay;

/// <param name="playActorSound">Plays one of an actor's sounds (its property naming the sound), from where it is</param>
internal sealed class ProjectileActions(MapManager map, GameSession session, LineOfSight lineOfSight, Action<Entities.Actors.Actor, string> playActorSound)
{
    /*
    =============================================================================

                         PROJECTILES AND JUMPS FOR ANY ACTOR

    Actions any actor can run, not just a Monster: firing a projectile any way at all
    (A_SpawnProjectile), and going to another state at random, with a player near, or with a
    player in sight (A_Jump, A_JumpIfPlayerWithin, A_JumpIfInSight). Made for traps, such as a
    wall that spits fireballs.

    An actor can stand inside a wall (mapdefs `in-wall: true`): it then looks and fires out of
    the face its map facing (`angles`) points out of, from just beyond that face.

    =============================================================================
    */

    internal void Register()
    {
        Entities.Actors.ActorActionRegistry.Register("A_SpawnProjectile", SpawnProjectileAction);
        Entities.Actors.ActorActionRegistry.Register("A_Jump", Jump);
        Entities.Actors.ActorActionRegistry.Register("A_JumpIfPlayerWithin", JumpIfPlayerWithin);
        Entities.Actors.ActorActionRegistry.Register("A_JumpIfActorWithin", JumpIfActorWithin);
        Entities.Actors.ActorActionRegistry.Register("A_JumpIfInSight", JumpIfInSight);
    }

    // Just beyond a wall's face for a projectile's box (ProjectileTryMove: ProjSize either way)
    // to be clear of it: half a tile from the centre to the face, ProjSize, and a little more
    const int WallFaceOut = 0x8000 + GameConstants.ProjSize + 0x100;

    /// <summary>
    /// A_SpawnProjectile("Fireball"[, angle[, "relative"|"absolute"|"aim"]]): fires a projectile
    /// (an actor whose think is A_Projectile), in degrees, positive turning left (counter-clockwise):
    /// relative (the default) to the way the actor faces (its map facing), absolute from east
    /// (90 north), or aim: at the nearest player, turned by the angle. The projectile's own
    /// `speed` (1/65536 tile a tic, default 0x2000, plus up to `speed.random` more) and `spread`
    /// (up to that many degrees either way, at random) apply, and its `attacksound` plays.
    /// </summary>
    void SpawnProjectileAction(Entities.Actors.Actor ob, string[] args)
    {
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.WriteLine("A_SpawnProjectile: no projectile given.");
            return;
        }

        int angle = args.Length > 1 && int.TryParse(args[1], out var a) ? a : 0;
        SpawnProjectile(ob, args[0], angle, args.Length > 2 ? args[2].Trim() : "relative");
    }

    /// <summary>A_SpawnProjectile's work (scripts' actor:spawnprojectile too): the projectile, or null if none was fired</summary>
    internal Entities.Actors.Actor? SpawnProjectile(Entities.Actors.Actor ob, string className, int angle, string mode)
    {
        var (x, y) = LookoutPoint(ob);

        if (mode.Equals("aim", StringComparison.OrdinalIgnoreCase))
        {
            if (NearestPlayer(x, y) is not { } target)
                return null;
            angle += AngleTo(x, y, target.X, target.Y);
        }
        else if (!mode.Equals("absolute", StringComparison.OrdinalIgnoreCase))
        {
            if (!mode.Equals("relative", StringComparison.OrdinalIgnoreCase))
                Console.WriteLine($"A_SpawnProjectile: \"{mode}\" isn't relative, absolute or aim; taken as relative.");
            angle += FacingAngle(ob);
        }

        var missile = map.SpawnAtActor(className, ob);
        if (missile == null)
        {
            Console.WriteLine($"A_SpawnProjectile: \"{className}\" isn't an actor.");
            return null;
        }

        int spread = PropertyInt(missile, "spread", 0);
        if (spread > 0)
            angle += session.Random.RndT() % (spread * 2 + 1) - spread;
        int speed = PropertyInt(missile, "speed", 0x2000);
        int extra = PropertyInt(missile, "speed.random", 0);
        if (extra > 0)
            speed += session.Random.RndT() * extra / 256;

        missile.X = x;
        missile.Y = y;
        missile.TileX = (byte)(x >> MapConstants.TileShift);
        missile.TileY = (byte)(y >> MapConstants.TileShift);
        missile.Angle = (short)((angle % GameConstants.Angles + GameConstants.Angles) % GameConstants.Angles);
        missile.Speed = speed;
        missile.Shooter = ob;
        missile.TicCount = 1;       // its first frame ends on the next tic, as a thrown one's (Monster.ThrowProjectile)

        playActorSound(missile, "attacksound");
        return missile;
    }

    /// <summary>
    /// A_Jump(chance, "State"[, "State2"...]): chance times in 256, goes to one of the states
    /// (picked at random when there are several)
    /// </summary>
    void Jump(Entities.Actors.Actor ob, string[] args)
    {
        if (args.Length < 2 || !int.TryParse(args[0], out var chance) || session.Random.RndT() >= chance)
            return;
        int pick = args.Length > 2 ? 1 + session.Random.RndT() % (args.Length - 1) : 1;
        JumpToState(ob, args[pick], "A_Jump");
    }

    /// <summary>
    /// A_JumpIfPlayerWithin(tiles, "State"): goes to the state when a player is within that many
    /// tiles of it, each way (it may be a fraction: 1.5)
    /// </summary>
    void JumpIfPlayerWithin(Entities.Actors.Actor ob, string[] args)
    {
        if (args.Length < 2 || !TryParseTiles(args[0], out var reach))
            return;
        var (x, y) = LookoutPoint(ob);
        if (map.Players.Any(pawn => session.IsTargetable(pawn) && Reach(pawn, x, y) <= reach))
            JumpToState(ob, args[1], "A_JumpIfPlayerWithin");
    }

    /// <summary>
    /// A_JumpIfActorWithin(tiles, "State"): as A_JumpIfPlayerWithin, but anything shootable
    /// counts too, other than another of its own class (Midnight Assault's landmine, set off by
    /// whoever steps on it)
    /// </summary>
    void JumpIfActorWithin(Entities.Actors.Actor ob, string[] args)
    {
        if (args.Length < 2 || !TryParseTiles(args[0], out var reach))
            return;
        if (IsActorWithin(ob, reach))
            JumpToState(ob, args[1], "A_JumpIfActorWithin");
    }

    internal bool IsActorWithin(Entities.Actors.Actor ob, long reach)
    {
        var (x, y) = LookoutPoint(ob);
        return map.Players.Any(pawn => session.IsTargetable(pawn) && Reach(pawn, x, y) <= reach)
            || map.GetActors().Any(a => !a.IsRemoved && a is not Entities.Actors.PlayerPawn && !ReferenceEquals(a, ob)
                && !string.Equals(a.Name, ob.Name, StringComparison.OrdinalIgnoreCase)
                && a.RuntimeFlags.Has(ObjFlags.Shootable) && Reach(a, x, y) <= reach);
    }

    /// <summary>
    /// A_JumpIfInSight("State"[, tiles]): goes to the state when there's a clear line to a player
    /// (within that many tiles each way, if given). One in a wall only sees out of its face.
    /// </summary>
    void JumpIfInSight(Entities.Actors.Actor ob, string[] args)
    {
        if (args.Length == 0)
            return;
        long reach = args.Length > 1 && TryParseTiles(args[1], out var r) ? r : long.MaxValue;
        if (map.Players.Any(pawn => session.IsTargetable(pawn) && CanSee(ob, pawn, reach)))
            JumpToState(ob, args[0], "A_JumpIfInSight");
    }

    /// <summary>
    /// Whether there's a clear line from the actor to a player, within reach (global units,
    /// each way); one in a wall only sees in front of the face it looks out of
    /// </summary>
    internal bool CanSee(Entities.Actors.Actor ob, Entities.Actors.PlayerPawn pawn, long reach = long.MaxValue)
    {
        var (x, y) = LookoutPoint(ob);
        if (Reach(pawn, x, y) > reach)
            return false;
        if (IsInWall(ob))
        {
            int facing = FacingAngle(ob);
            int cos = TrigTables.SinTable[facing + GameConstants.Angles / 4], sin = TrigTables.SinTable[facing];
            if ((long)(pawn.X - x) * cos - (long)(pawn.Y - y) * sin <= 0)
                return false;
        }
        return lineOfSight.CheckLine(x, y, pawn);
    }

    /// <summary>Whether the actor stands inside a wall (a map thing with `in-wall: true`)</summary>
    internal bool IsInWall(Entities.Actors.Actor ob)
    {
        var tile = map.TileMap[ob.TileX, ob.TileY];
        return tile != 0 && (tile & GameConstants.BitDoor) == 0;
    }

    /// <summary>Which way it faces, in degrees: its map facing, or its angle when it has none</summary>
    internal static int FacingAngle(Entities.Actors.Actor ob) =>
        ob.Dir == ObjDir.NoDir ? ob.Angle : GameConstants.DirAngle[(byte)ob.Dir];

    /// <summary>
    /// Where it looks and fires from: where it is, or for one inside a wall, just beyond the face
    /// it faces out of (on both axes for a diagonal facing)
    /// </summary>
    internal (int X, int Y) LookoutPoint(Entities.Actors.Actor ob)
    {
        if (!IsInWall(ob))
            return (ob.X, ob.Y);

        int facing = FacingAngle(ob);
        int cos = TrigTables.SinTable[facing + GameConstants.Angles / 4], sin = TrigTables.SinTable[facing];
        const int Axis = 0x6000;    // about 22 degrees off an axis still counts as facing along it
        int centreX = (ob.TileX << MapConstants.TileShift) + 0x8000, centreY = (ob.TileY << MapConstants.TileShift) + 0x8000;
        int x = centreX + (cos > Axis ? WallFaceOut : cos < -Axis ? -WallFaceOut : 0);
        int y = centreY + (sin > Axis ? -WallFaceOut : sin < -Axis ? WallFaceOut : 0);  // y grows southward
        return (x, y);
    }

    // The nearest player who can be aimed at, each way, from a point
    internal Entities.Actors.PlayerPawn? NearestPlayer(int x, int y) =>
        map.Players.Where(session.IsTargetable).OrderBy(pawn => Reach(pawn, x, y)).FirstOrDefault();

    internal static long Reach(Entities.Actors.Actor pawn, int x, int y) => Math.Max(Math.Abs((long)pawn.X - x), Math.Abs((long)pawn.Y - y));

    // Degrees from one point to another, 0 east, 90 north
    internal static int AngleTo(int fromX, int fromY, int toX, int toY)
    {
        var angle = Math.Atan2(fromY - toY, toX - fromX);
        if (angle < 0)
            angle += Math.PI * 2;
        return (int)(angle / (Math.PI * 2) * GameConstants.Angles);
    }

    internal static bool TryParseTiles(string text, out long reach)
    {
        bool ok = double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tiles);
        reach = ok ? (long)(tiles * MapConstants.TileGlobal) : 0;
        return ok;
    }

    internal static int PropertyInt(Entities.Actors.Actor actor, string key, int fallback) =>
        actor.Properties.TryGetValue(key, out var value) && int.TryParse(value?.ToString(), out var n) ? n : fallback;

    readonly HashSet<(string Actor, string State)> missingJumpStates = [];

    void JumpToState(Entities.Actors.Actor ob, string state, string action)
    {
        if (ob.ResolvedStates.TryGetValue(state, out var frame))
            ob.JumpTo(frame);
        else if (missingJumpStates.Add((ob.Name, state)))
            Console.WriteLine($"{action}: actor '{ob.Name}' has no state \"{state}\".");
    }
}
