using PFWolf.Constants;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>What projectiles need from the game</summary>
/// <param name="WallSpriteHit">The wall sprite panel a projectile's box at x, y (size either way) runs into among the tiles xl-xh, yl-yh, if any</param>
/// <param name="PlaySoundAt">A sound from where an actor is</param>
/// <param name="HurtPlayer">Hurts a particular player, as the acting player while it does</param>
internal sealed record ProjectileHooks(
    Func<int, int, int, int, int, int, long, WallSpriteSpan?> WallSpriteHit,
    Action<string, Entities.Actors.Actor> PlaySoundAt,
    Action<Entities.Actors.PlayerPawn, int, Entities.Actors.Actor> HurtPlayer);

/// <summary>
/// Enemies' projectiles (Rocket/Smoke/Needle/Fire, actordefs/wolf3d/projectiles.yaml), spawned
/// into the level's actors by MapManager.SpawnAtActor: their flight and what they hit, and the
/// effects they leave (A_SpawnThing, A_Remove). Also CheckPosition, which places the death cam.
/// </summary>
internal sealed class Projectiles(
    MapManager map,
    FactionManager factions,
    GameSession session,
    ProjectileHooks hooks)
{
    /*
    =================
    =
    = A_SpawnThing
    =
    =================
    */

    // A_SpawnThing("Smoke"): spawns the given actor at this one's position (a rocket's smoke trail)
    // A_SpawnThing("GreenOoze", 128): with a chance, only that many times in 256
    internal void A_SpawnThing(Entities.Actors.Actor ob, string[] args)
    {
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.WriteLine("A_SpawnThing: no actor given.");
            return;
        }

        if (args.Length > 1 && int.TryParse(args[1], out var chance) && session.random.RndT() >= chance)
            return;

        map.SpawnAtActor(args[0], ob);
    }

    // Terminal action for one-shot effects (Smoke, a rocket's Death): the legacy chain ended on a null
    // next state, which removed the object once its last frame's tics ran out.
    internal void A_Remove(Entities.Actors.Actor ob) => map.MarkForRemoval(ob);

    /*
    ===================
    =
    = ProjectileTryMove
    =
    = returns true if move ok
    ===================
    */

    internal bool ProjectileTryMove(Entities.Actors.Actor ob)
    {
        const int PROJSIZE = GameConstants.PROJSIZE;
        int xl, yl, xh, yh, x, y;
        Actor? check;

        xl = (ob.X - PROJSIZE) >> MapConstants.TILESHIFT;
        yl = (ob.Y - PROJSIZE) >> MapConstants.TILESHIFT;

        xh = (ob.X + PROJSIZE) >> MapConstants.TILESHIFT;
        yh = (ob.Y + PROJSIZE) >> MapConstants.TILESHIFT;

        //
        // check for solid walls
        //
        for (y = yl; y <= yh; y++)
            for (x = xl; x <= xh; x++)
            {
                check = map.actorat[x, y];
                if (check is null or ActorMark)     // enemies are hit by the projectile's own check
                    continue;

                // a wall sprite only blocks along its panel, unless it lets projectiles through
                if (check is WallSpriteBlocker)
                {
                    if (hooks.WallSpriteHit(x, y, x, y, ob.X, ob.Y, PROJSIZE) != null)
                        return false;
                    continue;
                }

                // a diagonal only blocks on its solid side of the face
                if (map.wallshape[x, y] is var shape and not WallShape.Square
                    && !Diagonals.BoxHitsDiagonal(shape, x, y, ob.X, ob.Y, PROJSIZE))
                    continue;

                return false;
            }

        return true;
    }

    /*
    =================
    =
    = A_Projectile
    =
    =================
    */

    // A_Projectile(min[, max]): flies the projectile along its Angle; hitting the player deals
    // min..max damage (just min without a max), and with the HURTMONSTERS flag so does hitting
    // an enemy. The roll scales one US_RndT() across the range, so a 32-wide range is exactly
    // the legacy (US_RndT()>>3) + base.
    internal void A_Projectile(Entities.Actors.Actor ob, string[] args)
    {
        const long PROJECTILESIZE = GameConstants.PROJECTILESIZE;
        long deltax, deltay;
        int damage = 0;
        int speed;

        speed = (int)(ob.Speed * session.tics);

        deltax = MathUtils.FixedMul(speed, TrigTables.costable[ob.Angle]);
        deltay = -MathUtils.FixedMul(speed, TrigTables.sintable[ob.Angle]);

        if (deltax > 0x10000L)
            deltax = 0x10000L;
        if (deltay > 0x10000L)
            deltay = 0x10000L;

        ob.X += (int)deltax;
        ob.Y += (int)deltay;

        // the first player (in player order) it's on, if any, unless they're on its side (it's
        // on its thrower's: Actor.Faction)
        var hit = map.Players.FirstOrDefault(pawn => Entities.Actors.Monster.IsTargetable(pawn)
            && Math.Abs(ob.X - pawn.X) < PROJECTILESIZE && Math.Abs(ob.Y - pawn.Y) < PROJECTILESIZE
            && !factions.IsAlly(ob, pawn));

        if (!ProjectileTryMove(ob))
        {
            // A projectile with a Death state (rockets) blows up against the wall with its
            // deathsound, like the legacy switch to s_boom1; the rest (needles, flames, sparks)
            // just vanish
            if (ob.ResolvedStates.ContainsKey("Death"))
            {
                if (ob.Properties.TryGetValue("deathsound", out var hitSound) && hitSound is string hitSoundName)
                    hooks.PlaySoundAt(hitSoundName, ob);
                ob.SetState("Death");
            }
            else
                map.MarkForRemoval(ob);
            return;
        }

        // A HURTMONSTERS projectile (a trap's fireball) hurts the enemies it flies into too, not
        // only the players: the nearest one within half a projectile's reach, never its shooter.
        // Any other hits an enemy of another side (factions.yaml) it flies into, as it would a
        // player; its own side's it passes by.
        var monster = hit != null ? null
            : ob.HasFlag("HURTMONSTERS")
                ? map.ShootableActorsNear(ob.X, ob.Y, PROJECTILESIZE / 2).FirstOrDefault(a => !ReferenceEquals(a, ob.Shooter))
                : map.ShootableActorsNear(ob.X, ob.Y, PROJECTILESIZE - 1)
                    .FirstOrDefault(a => !ReferenceEquals(a, ob.Shooter) && !factions.IsAlly(ob, a));

        if (hit != null || monster != null)
        {       // hit the player (or an enemy)
            var minDamage = args.Length > 0 && int.TryParse(args[0], out var min) ? min : 0;
            var maxDamage = args.Length > 1 && int.TryParse(args[1], out var max) ? max : minDamage;
            damage = maxDamage > minDamage
                ? minDamage + session.random.RndT() * (maxDamage - minDamage + 1) / 256
                : minDamage;

            if (hit != null)
                hooks.HurtPlayer(hit, damage, ob);
            else if (damage > 0)
                monster!.Damage((uint)damage, ob);

            // BURSTONPLAYER projectiles (Blake Stone's spit and shots) burst on the player (or
            // the enemy) as on a wall
            if (ob.HasFlag("BURSTONPLAYER") && ob.ResolvedStates.ContainsKey("Death"))
                ob.SetState("Death");
            else
                map.MarkForRemoval(ob);
            return;
        }

        ob.TileX = (byte)(ob.X >> MapConstants.TILESHIFT);
        ob.TileY = (byte)(ob.Y >> MapConstants.TILESHIFT);
    }

    /*
    ===============
    =
    = CheckPosition
    =
    ===============
    */
    internal bool CheckPosition(int obx, int oby)
    {
        int x, y, xl, yl, xh, yh;
        Actor? check;

        xl = (int)((obx - GameConstants.PLAYERSIZE) >> MapConstants.TILESHIFT);
        yl = (int)((oby - GameConstants.PLAYERSIZE) >> MapConstants.TILESHIFT);

        xh = (int)((obx + GameConstants.PLAYERSIZE) >> MapConstants.TILESHIFT);
        yh = (int)((oby + GameConstants.PLAYERSIZE) >> MapConstants.TILESHIFT);

        //
        // check for solid walls
        //
        for (y = yl; y <= yh; y++)
        {
            for (x = xl; x <= xh; x++)
            {
                check = map.actorat[x, y];
                if (check is not (null or ActorMark))     // only walls and solid things
                    return false;
            }
        }

        return true;
    }
}
