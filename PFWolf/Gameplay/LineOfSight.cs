namespace PFWolf.Gameplay;

/// <summary>
/// Whether one thing can see another: a clear line through the tile map (walls, the solid
/// half of diagonals, the closed part of doors, and wall sprites' panels); and whether a
/// diagonal step is blocked
/// </summary>
/// <param name="wallSpriteBlocks">Whether a wall sprite's panel crosses the line between two points (global coordinates)</param>
internal sealed class LineOfSight(MapManager map, Doors doors, GameSession session, Func<long, long, long, long, bool> wallSpriteBlocks)
{
    // A diagonal step is blocked by a wall/door (ActorAt[,]) or by a living actor marked on the tile.
    internal bool CheckDiag(int x, int y) => map.ActorAt[x, y] switch
    {
        null => true,
        ActorMark mark => !Managers.MapManager.IsSolidActor(mark.Who),
        _ => false,
    };

    // The per-actor movement, sight and combat code (MoveObj/TryWalk/CheckSide, CheckSight/
    // SightPlayer, SelectChaseDir/SelectDodgeDir, Kill/Damage, ...) lives on Entities.Actors.Monster.
    // CheckDiag and CheckLine are here, since they aren't tied to a mover: CheckLine is whether
    // there's a clear line from any actor to the player (the player's own sight uses it too).
    internal bool CheckLine(Entities.Actors.Actor ob) => CheckLine(ob, session.Player);

    /// <summary>Whether there's a clear line from an actor to another (a player, or any actor)</summary>
    internal bool CheckLine(Entities.Actors.Actor ob, Entities.Actors.Actor to) => CheckLine(ob.X, ob.Y, to);

    /// <summary>
    /// Whether there's a clear line from a point (global coordinates) to an actor; the point's own
    /// tile isn't checked. A player is aimed at where their last move left them (PlUX/PlUY and
    /// their tile, as vanilla), anything else at where it is.
    /// </summary>
    internal bool CheckLine(int fromX, int fromY, Entities.Actors.Actor to)
    {
        int x1, y1, xt1, yt1, x2, y2, xt2, yt2;
        int x, y;
        int xdist, ydist, xstep, ystep;
        int partial, delta;
        int ltemp;
        int xfrac, yfrac, deltafrac;
        uint value, intercept;

        // a wall sprite's panel across the line (Rendering/WallSprites.cs), unless it's shoot-through
        if (wallSpriteBlocks(fromX, fromY, to.X, to.Y))
            return false;

        x1 = fromX >> GameConstants.UnsignedShift;
        y1 = fromY >> GameConstants.UnsignedShift;
        xt1 = x1 >> 8;
        yt1 = y1 >> 8;

        if (to is Entities.Actors.PlayerPawn pawn)
        {
            x2 = pawn.State.PlUX;
            y2 = pawn.State.PlUY;
            xt2 = pawn.TileX;
            yt2 = pawn.TileY;
        }
        else
        {
            // An actor's TileX/TileY jump to the tile it's stepping onto as the step begins: the
            // line goes to the tile it's actually over
            x2 = (ushort)(to.X >> GameConstants.UnsignedShift);
            y2 = (ushort)(to.Y >> GameConstants.UnsignedShift);
            xt2 = x2 >> 8;
            yt2 = y2 >> 8;
        }

        xdist = Math.Abs(xt2 - xt1);

        if (xdist > 0)
        {
            if (xt2 > xt1)
            {
                partial = 256 - (x1 & 0xff);
                xstep = 1;
            }
            else
            {
                partial = x1 & 0xff;
                xstep = -1;
            }

            deltafrac = Math.Abs(x2 - x1);
            delta = y2 - y1;
            ltemp = ((int)delta << 8) / deltafrac;
            if (ltemp > 0x7fffl)
                ystep = 0x7fff;
            else if (ltemp < -0x7fffl)
                ystep = -0x7fff;
            else
                ystep = ltemp;
            yfrac = y1 + (((int)ystep * partial) >> 8);

            x = xt1 + xstep;
            xt2 += xstep;
            do
            {
                y = yfrac >> 8;
                yfrac += ystep;

                value = (uint)map.TileMap[x, y];
                var shape = map.WallShapes[x, y];
                if (shape != WallShape.Square && Diagonals.LineHitsDiagonal(shape, x, y, fromX, fromY, to.X, to.Y))
                    return false;
                x += xstep;

                if (value == 0 || shape != WallShape.Square)
                    continue;

                if (value < GameConstants.BitDoor || value > GameConstants.BitAllTiles)
                    return false;

                value &= ~(uint)GameConstants.BitDoor;
                intercept = (uint)(yfrac - ystep / 2);

                if (intercept > doors.DoorObjList[value].Position)
                    return false;

            } while (x != xt2);
        }

        ydist = Math.Abs(yt2 - yt1);

        if (ydist > 0)
        {
            if (yt2 > yt1)
            {
                partial = 256 - (y1 & 0xff);
                ystep = 1;
            }
            else
            {
                partial = y1 & 0xff;
                ystep = -1;
            }

            deltafrac = Math.Abs(y2 - y1);
            delta = x2 - x1;
            ltemp = ((int)delta << 8) / deltafrac;
            if (ltemp > 0x7fffl)
                xstep = 0x7fff;
            else if (ltemp < -0x7fffl)
                xstep = -0x7fff;
            else
                xstep = ltemp;
            xfrac = x1 + (((int)xstep * partial) >> 8);

            y = yt1 + ystep;
            yt2 += ystep;
            do
            {
                x = xfrac >> 8;
                xfrac += xstep;

                value = (uint)map.TileMap[x, y];
                var shape = map.WallShapes[x, y];
                if (shape != WallShape.Square && Diagonals.LineHitsDiagonal(shape, x, y, fromX, fromY, to.X, to.Y))
                    return false;
                y += ystep;

                if (value == 0 || shape != WallShape.Square)
                    continue;

                if (value < GameConstants.BitDoor || value > GameConstants.BitAllTiles)
                    return false;

                value &= ~(uint)GameConstants.BitDoor;
                intercept = (uint)(xfrac - xstep / 2);

                if (intercept > doors.DoorObjList[value].Position)
                    return false;
            } while (y != yt2);
        }

        return true;
    }

    internal const long MinSight = 0x18000L;
}
