using PFWolf.Constants;
using PFWolf.Enums;
using static PFWolf.Gameplay.GameConstants;

namespace PFWolf.Entities.Actors;

/*
=============================================================================

                                KNOCKBACK

A hit can shove what it hits away from what dealt it: so far for each hit of a player's weapon
(`weapon.knockback`, each of a shotgun's pellets), or of a missile, a blast or a monster
(`knockback`; a blast's weakening with its falloff, as its damage does), in 1/65536 tile. The
victim's `mass` (100 when left out) takes from it: at 200, half as far. A NOKNOCKBACK actor (a
tree, a lamp post), a wall sprite, a STATIONARY one or one of 0 mass never moves; nor does a
BlakeMonster, whose bodies slide back their own way (BlakeMonster.StartBlowBack).

The shove isn't all at once: it eases out over a few tics (PushTick), stopped by walls, doors,
solid things and the players.

A dying or dead one slides freely, onto whatever tile it ends up on. A living one walks tile to
tile, as the original's enemies did (MoveObj, ArriveOnTile), and that walk goes on as if it
hadn't been shoved: it's only displaced from it (DispX, DispY), no more than MaxDisplacement
either way, and the displacement eases off as it walks on (half its walking speed), so it drifts
back onto its way rather than snapping to the middle of the next tile. Standing still (on
guard, shooting), it stays where it was shoved.

=============================================================================
*/

internal partial record Monster
{
    /// <summary>The shove still to come, global units (eased out by PushTick)</summary>
    internal int PushX, PushY;

    /// <summary>How far a living one stands off the walk it's on, from being shoved (global units)</summary>
    internal int DispX, DispY;

    /// <summary>The knockback of the blow being dealt now, when it isn't its attacker's own (a blast's, weakened with distance); set around that one blow</summary>
    internal int? BlowKnockback;

    /// <summary>How far a living one can be shoved off its walk, either way (3/8 tile)</summary>
    internal const int MaxDisplacement = 0x6000;

    /// <summary>The most one hit (or a tic's hits) can have it still to be shoved, either way</summary>
    internal const int MaxPush = (int)MapConstants.TileGlobal * 2;

    /// <summary>The mass that takes a push as given</summary>
    internal const int DefaultMass = 100;

    // How far the walls must stay from its middle while it's shoved (the player's own box)
    private const int ShoveSize = (int)MinDist;

    // A tic's shove: a third of what's left, but not less than this (so it ends)
    private const int MinPushStep = 0x400;

    /// <summary>Whether a hit can shove it (see KNOCKBACK)</summary>
    internal virtual bool Knockable => !HasFlag("NOKNOCKBACK") && !HasFlag("WALLSPRITE") && !HasFlag("STATIONARY")
        && PropertyInt("mass", DefaultMass) > 0;

    /// <summary>Whether it's still being shoved</summary>
    internal bool Pushing => PushX != 0 || PushY != 0;

    /// <summary>How far a blow from <paramref name="attacker"/> shoves: a player's weapon's weapon.knockback, else the attacker's own knockback</summary>
    internal int KnockbackOf(Actor? attacker)
    {
        if (BlowKnockback is { } blow)
            return blow;
        if (attacker is PlayerPawn pawn)
            return pawn.State.Weapon is { Length: > 0 } weapon
                && int.TryParse(InventoryManager.GetProperty(weapon, "weapon.knockback")?.ToString(), out var k) ? k : 0;
        return attacker?.PropertyInt("knockback", 0) ?? 0;
    }

    /// <summary>
    /// A blow from <paramref name="attacker"/> shoves it, as far as KnockbackOf and its mass
    /// say: a projectile's own hit on along the way it flew (it may be past its victim's middle
    /// by then), anything else's -- a gunshot, a blow, a blast -- away from where that is (or,
    /// from right on top of it, from whoever fired it)
    /// </summary>
    internal void Knock(Actor? attacker)
    {
        if (attacker == null || !Knockable)
            return;
        long distance = KnockbackOf(attacker);
        if (distance <= 0)
            return;
        distance = Math.Min(distance * DefaultMass / PropertyInt("mass", DefaultMass), MaxPush);

        double dx, dy;
        if (BlowKnockback == null && attacker.HasFlag("PROJECTILE"))
        {
            double heading = attacker.Angle * Math.PI / 180;
            (dx, dy) = (Math.Cos(heading), -Math.Sin(heading));       // map y runs south
        }
        else
        {
            (dx, dy) = (X - attacker.X, Y - attacker.Y);
            if (dx == 0 && dy == 0 && attacker.Shooter is { } shooter)
                (dx, dy) = (X - shooter.X, Y - shooter.Y);
            if (dx == 0 && dy == 0)
                return;
        }

        double length = Math.Sqrt(dx * dx + dy * dy);
        PushX = (int)Math.Clamp(PushX + (long)Math.Round(dx * distance / length), -MaxPush, MaxPush);
        PushY = (int)Math.Clamp(PushY + (long)Math.Round(dy * distance / length), -MaxPush, MaxPush);
    }

    /// <summary>Whether it's dead (or dying): it slides freely, off any walk</summary>
    private bool PushedDead => Hitpoints <= 0 || RuntimeFlags.Has(ObjFlags.NonMark);

    /// <summary>Each tic it's being shoved (MapManager.DoActor, off its tile): the shove eases out, stopped by what's in the way</summary>
    internal void PushTick(uint tics)
    {
        bool dead = PushedDead;
        if (dead)
            DispX = DispY = 0;          // no walk to keep to

        for (uint i = 0; i < tics && Pushing; i++)
        {
            int wantX = PushStep(PushX), wantY = PushStep(PushY);
            PushX -= wantX;
            PushY -= wantY;

            int sx = wantX, sy = wantY;
            if (!dead)
            {
                sx = Math.Clamp(DispX + sx, -MaxDisplacement, MaxDisplacement) - DispX;
                sy = Math.Clamp(DispY + sy, -MaxDisplacement, MaxDisplacement) - DispY;
            }

            // Straight on, else along whatever's in the way; what's stopped is done
            bool movedX = false, movedY = false;
            if ((sx != 0 || sy != 0) && Shove(sx, sy))
                movedX = movedY = true;
            else
            {
                movedX = sx != 0 && Shove(sx, 0);
                movedY = sy != 0 && Shove(0, sy);
            }
            if (!movedX || sx != wantX)
                PushX = 0;
            if (!movedY || sy != wantY)
                PushY = 0;

            if (!dead)
            {
                DispX += movedX ? sx : 0;
                DispY += movedY ? sy : 0;
            }
        }

        // A body is on the tile it's slid onto, off the one it left (DoActor marks it there again)
        if (dead && (X >> (int)MapConstants.TileShift != TileX || Y >> (int)MapConstants.TileShift != TileY))
        {
            if (!MapManager.OnPlayersStory(this) || ReferenceEquals(MapManager.ActorMarkAt(TileX, TileY), this))
                MapManager.UnmarkActorTile(this);
            TileX = (byte)(X >> (int)MapConstants.TileShift);
            TileY = (byte)(Y >> (int)MapConstants.TileShift);
            SyncPosition();
        }
    }

    // A tic's part of the shove left
    private static int PushStep(int left) =>
        Math.Abs(left) <= MinPushStep ? left : Math.Abs(left / 3) >= MinPushStep ? left / 3 : Math.Sign(left) * MinPushStep;

    // Moves it so far if it's clear to
    private bool Shove(int dx, int dy)
    {
        int nx = X + dx, ny = Y + dy;
        if (!ClearToShove(nx, ny, players: !PushedDead, fromX: X, fromY: Y))
            return false;
        X = nx;
        Y = ny;
        return true;
    }

    /// <summary>
    /// Whether it can stand at (x, y) while shoved: walls, doors, solid things (and wall
    /// sprites' tiles) and the map's edge kept ShoveSize from its middle, other than on its own
    /// tile; with <paramref name="players"/>, not moving nearer a player it's up against
    /// </summary>
    private bool ClearToShove(int x, int y, bool players, int fromX, int fromY)
    {
        foreach (var (cx, cy) in new[] { (x - ShoveSize, y - ShoveSize), (x + ShoveSize, y - ShoveSize), (x - ShoveSize, y + ShoveSize), (x + ShoveSize, y + ShoveSize) })
        {
            int tx = cx >> (int)MapConstants.TileShift, ty = cy >> (int)MapConstants.TileShift;
            if (!MapManager.InMap(tx, ty))
                return false;
            if (tx == TileX && ty == TileY)
                continue;
            if (TileEntry(tx, ty) is Wall or BlockingActor or Door)
                return false;
        }

        if (players && MapManager.OnPlayersStory(this) && !HasFlag("NOTSOLID"))
        {
            long reach = Reach(PlayerSize, MinActorDist);
            foreach (var pawn in MapManager.Players)
            {
                if (!IsTargetable(pawn))
                    continue;
                long near = Math.Max(Math.Abs((long)x - pawn.X), Math.Abs((long)y - pawn.Y));
                if (near <= reach && near < Math.Max(Math.Abs((long)fromX - pawn.X), Math.Abs((long)fromY - pawn.Y)))
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Walking on (MoveObj, by <paramref name="move"/>), a shoved one eases back onto its walk;
    /// if where that leaves it is in a wall's way (its walk turned a corner), it's back on it at once
    /// </summary>
    private void EaseBack(int move)
    {
        int ease = Math.Max(move / 2, 1);
        int bx = Math.Clamp(DispX, -ease, ease), by = Math.Clamp(DispY, -ease, ease);
        X -= bx;
        Y -= by;
        DispX -= bx;
        DispY -= by;
        if ((DispX != 0 || DispY != 0) && !ClearToShove(X, Y, players: false, X, Y))
            DropDisplacement();
    }

    /// <summary>Back on its walk at once</summary>
    private void DropDisplacement()
    {
        X -= DispX;
        Y -= DispY;
        DispX = DispY = 0;
    }

    /// <summary>No more shove to come, and none it's still off its walk by (it's been put somewhere)</summary>
    internal void ClearPush() => PushX = PushY = DispX = DispY = 0;
}
