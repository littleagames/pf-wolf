using Wolf3D.Constants;
using Wolf3D.Enums;
using Wolf3D.Managers;

namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                                GLOBAL VARIABLES

    =============================================================================
    */



    // A diagonal step is blocked by a wall/door (actorat[,]) or by a living actor marked on the tile.
    internal static bool CHECKDIAG(int x, int y) => _mapManager.actorat[x, y] switch
    {
        null => true,
        ActorMark mark => !Managers.MapManager.IsSolidActor(mark.Who),
        _ => false,
    };

    // The per-actor movement, sight and combat code (MoveObj/TryWalk/CheckSide, CheckSight/
    // SightPlayer, SelectChaseDir/SelectDodgeDir, Kill/Damage, ...) lives on Entities.Actors.Monster.
    // CHECKDIAG and CheckLine stay here, since they aren't tied to a mover: CheckLine is whether
    // there's a clear line from any actor to the player (the player's own sight uses it too).
    internal static bool CheckLine(Entities.Actors.Actor ob) => CheckLine(ob, player);

    /// <summary>Whether there's a clear line from an actor to a player</summary>
    internal static bool CheckLine(Entities.Actors.Actor ob, Entities.Actors.PlayerPawn to)
    {
        int x1, y1, xt1, yt1, x2, y2, xt2, yt2;
        int x, y;
        int xdist, ydist, xstep, ystep;
        int partial, delta;
        int ltemp;
        int xfrac, yfrac, deltafrac;
        uint value, intercept;

        // a wall sprite's panel across the line (Program.WallSprites.cs), unless it's shoot-through
        if (WallSpriteBlocksLine(ob.X, ob.Y, to.X, to.Y))
            return false;

        x1 = ob.X >> UNSIGNEDSHIFT;
        y1 = ob.Y >> UNSIGNEDSHIFT;
        xt1 = x1 >> 8;
        yt1 = y1 >> 8;

        x2 = to.State.PlUX;
        y2 = to.State.PlUY;
        xt2 = to.TileX;
        yt2 = to.TileY;

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

                value = (uint)_mapManager.tilemap[x, y];
                var shape = _mapManager.wallshape[x, y];
                if (shape != WallShape.Square && LineHitsDiagonal(shape, x, y, ob.X, ob.Y, to.X, to.Y))
                    return false;
                x += xstep;

                if (value == 0 || shape != WallShape.Square)
                    continue;

                if (value < BIT_DOOR || value > BIT_ALLTILES)
                    return false;

                value &= ~(uint)BIT_DOOR;
                intercept = (uint)(yfrac - ystep / 2);

                if (intercept > doorobjlist[value].position)
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

                value = (uint)_mapManager.tilemap[x, y];
                var shape = _mapManager.wallshape[x, y];
                if (shape != WallShape.Square && LineHitsDiagonal(shape, x, y, ob.X, ob.Y, to.X, to.Y))
                    return false;
                y += ystep;

                if (value == 0 || shape != WallShape.Square)
                    continue;

                if (value < BIT_DOOR || value > BIT_ALLTILES)
                    return false;

                value &= ~(uint)BIT_DOOR;
                intercept = (uint)(xfrac - xstep / 2);

                if (intercept > doorobjlist[value].position)
                    return false;
            } while (y != yt2);
        }

        return true;
    }

    internal const long MINSIGHT = 0x18000L;
}
