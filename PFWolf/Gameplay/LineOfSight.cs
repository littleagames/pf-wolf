using PFWolf.Constants;

namespace PFWolf.Gameplay;

/// <summary>
/// Whether one thing can see another: a clear line through the tile map (walls, the solid
/// half of diagonals, the closed part of doors, and wall sprites' panels); and whether a
/// diagonal step is blocked. A shut see-through door (one with masked faces) lets sight
/// through, but only lets a shot through when it's doors.yaml shoot-through.
/// </summary>
/// <param name="wallSpriteBlocks">Whether a wall sprite's panel crosses the line between two points (global coordinates)</param>
internal sealed class LineOfSight(MapManager map, Doors doors, GameSession session, Func<long, long, long, long, bool> wallSpriteBlocks)
{
    // A diagonal step is blocked by a wall/door (ActorAt[,]) or by a living actor marked on the tile.
    internal bool CheckDiag(int x, int y) => ClearForDiagonal(map.ActorAt[x, y]);

    /// <summary>Whether a tile's entry (ActorAt[], or MapManager.ActorOn's for another story) lets a diagonal step by</summary>
    internal static bool ClearForDiagonal(Actor? spot) => spot switch
    {
        null => true,
        ActorMark mark => !Managers.MapManager.IsSolidActor(mark.Who),
        _ => false,
    };

    // The per-actor movement, sight and combat code (MoveObj/TryWalk/CheckSide, CheckSight/
    // SightPlayer, SelectChaseDir/SelectDodgeDir, Kill/Damage, ...) lives on Entities.Actors.Monster.
    // CheckDiag and CheckLine are here, since they aren't tied to a mover: CheckLine is whether
    // there's a clear line from any actor to the player (the player's own sight uses it too).
    // shot: the line's a shot's, which see-through doors stop unless they're shoot-through
    internal bool CheckLine(Entities.Actors.Actor ob, bool shot = false) => CheckLine(ob, session.Player, shot);

    /// <summary>How far open a door is (0 shut, 0xffff open); shut for no such door</summary>
    internal int DoorPosition(int door) => door >= 0 && door < doors.DoorObjList.Length ? (int)doors.DoorObjList[door].Position : 0;

    // Whether a line (a shot's, if shot) gets through door number door shut
    bool DoorLetsThrough(uint door, bool shot) => door < doors.DoorObjList.Length && doors.DoorObjList[door] is { } d && d.LetsThrough(shot);

    // The step a shot's line is followed in, and how far a shot goes at most
    internal const int ShotStep = 0x1000;
    internal const int ShotReach = 64 << MapConstants.TileShift;

    /// <summary>
    /// Follows a shot's line from (x, y) at the angle (degrees, east 0, north 90) a step at a
    /// time, until a wall, a door that isn't open, the map's edge or its reach (global units);
    /// <paramref name="stopAt"/> can stop it sooner, at a point (something in its way). Where it
    /// stopped (a step short of a wall) and why: <see cref="ShotEnd.NoPuffs"/> for a wall with
    /// walls.yaml `puffs: false`. It goes on through a shoot-through door.
    /// </summary>
    internal ShotTrace TraceShot(int x, int y, double angle, int reach = ShotReach, Func<int, int, bool>? stopAt = null)
    {
        double rad = angle * Math.PI / 180;
        int stepx = (int)Math.Round(Math.Cos(rad) * ShotStep), stepy = -(int)Math.Round(Math.Sin(rad) * ShotStep);
        int px = x, py = y;
        for (int travelled = 0; travelled < reach; travelled += ShotStep)
        {
            int nx = px + stepx, ny = py + stepy;
            int tx = nx >> MapConstants.TileShift, ty = ny >> MapConstants.TileShift;
            if (tx < 0 || ty < 0 || tx >= map.MapWidth || ty >= map.MapHeight)
                return new ShotTrace(ShotEnd.Edge, px, py);
            int tile = map.TileMap[tx, ty];
            if ((tile & GameConstants.BitDoor) != 0)
            {
                if (DoorPosition(tile & ~GameConstants.BitDoor) < 0x8000 && !DoorLetsThrough((uint)(tile & ~GameConstants.BitDoor), shot: true))
                    return new ShotTrace(ShotEnd.Wall, px, py, tx, ty);
            }
            else if (tile != 0)
            {
                bool puffs = !(map.GetMapData().Walls.TryGetValue(tile & ~GameConstants.BitWall, out var wall) && wall.Puffs == false);
                return new ShotTrace(puffs ? ShotEnd.Wall : ShotEnd.NoPuffs, px, py, tx, ty);
            }
            (px, py) = (nx, ny);
            if (stopAt?.Invoke(px, py) == true)
                return new ShotTrace(ShotEnd.Stopped, px, py);
        }
        return new ShotTrace(ShotEnd.Reach, px, py);
    }

    /// <summary>Whether there's a clear line from an actor to another (a player, or any actor)</summary>
    internal bool CheckLine(Entities.Actors.Actor ob, Entities.Actors.Actor to, bool shot = false) =>
        map.OnPlayersStory(ob) && map.OnPlayersStory(to) ? CheckLine(ob.X, ob.Y, to, shot)
        : CheckLine3D(ob.X, ob.Y, map.MiddleHeight(ob), to.X, to.Y, map.MiddleHeight(to), shot);

    /// <summary>
    /// Whether there's a clear line from a point (global coordinates) to an actor; the point's own
    /// tile isn't checked. A player is aimed at where their last move left them (PlUX/PlUY and
    /// their tile, as vanilla), anything else at where it is. <paramref name="shot"/>: it's a
    /// shot's line, which a see-through door stops unless it's shoot-through. From a point on
    /// another story than the player's (<paramref name="fromStory"/>; 0, the player's), or to an
    /// actor on one, the line's followed in 3D (<see cref="CheckLine3D"/>).
    /// </summary>
    internal bool CheckLine(int fromX, int fromY, Entities.Actors.Actor to, bool shot = false, int fromStory = 0)
    {
        if (fromStory != 0 && fromStory != map.Story || !map.OnPlayersStory(to))
        {
            double fromZ = ((fromStory == 0 ? map.Story : fromStory) - 1) * GameConstants.TextureSize + GameConstants.TextureSize / 2;
            return CheckLine3D(fromX, fromY, fromZ, to.X, to.Y, map.MiddleHeight(to), shot);
        }
        return CheckLineFlat(fromX, fromY, to, shot);
    }

    /*
    A line between two stories (or along one that isn't the player's) is followed tile by tile in
    3D, from one point's height (texels up from the ground) to the other's. Where it crosses a
    tile it's blocked if, at the heights it crosses that tile at, the tile has a block (MapManager's
    spans; a wall's default block from story 1), or it's a door's tile anywhere off the player's
    story (its frame and lintel: doors work there only) or with the door shut on it (unless the line
    gets through, as CheckLine's), a moving pushwall's, or on the map's edge. On the two ends' own
    tiles only a block between the end and where the line leaves (or meets) the tile stops it, not
    one the end is inside (a thing in a wall still sees out): a guard under a bridge doesn't see the
    player standing on it. Diagonal walls count as whole blocks, and wall sprites' panels don't stop it.
    */

    /// <summary>Whether a line in 3D (global coordinates, heights in texels up from the ground) is clear</summary>
    internal bool CheckLine3D(long fromX, long fromY, double fromZ, long toX, long toY, double toZ, bool shot = false)
    {
        double x0 = (double)fromX / MapConstants.TileGlobal, y0 = (double)fromY / MapConstants.TileGlobal;
        double x1 = (double)toX / MapConstants.TileGlobal, y1 = (double)toY / MapConstants.TileGlobal;
        int tx = (int)Math.Floor(x0), ty = (int)Math.Floor(y0);
        int ex = (int)Math.Floor(x1), ey = (int)Math.Floor(y1);
        double dx = x1 - x0, dy = y1 - y0;
        int sx = Math.Sign(dx), sy = Math.Sign(dy);
        double nextX = sx > 0 ? (tx + 1 - x0) / dx : sx < 0 ? (x0 - tx) / -dx : double.PositiveInfinity;
        double nextY = sy > 0 ? (ty + 1 - y0) / dy : sy < 0 ? (y0 - ty) / -dy : double.PositiveInfinity;
        double stepX = sx != 0 ? 1 / Math.Abs(dx) : double.PositiveInfinity;
        double stepY = sy != 0 ? 1 / Math.Abs(dy) : double.PositiveInfinity;

        double Z(double t) => fromZ + (toZ - fromZ) * t;

        // The start's own tile, up (or down) to where the line leaves it: only a block it isn't inside
        double first = Math.Min(Math.Min(nextX, nextY), 1);
        if ((tx, ty) == (ex, ey))
            return !EndBlocks(tx, ty, fromZ, toZ, fromZ, toZ);
        if (EndBlocks(tx, ty, fromZ, Z(first), fromZ, double.NaN))
            return false;

        for (int guard = 0; guard < 4 * MapConstants.MaxMapSize; guard++)
        {
            double enter;
            if (nextX < nextY)
            {
                enter = nextX;
                nextX += stepX;
                tx += sx;
            }
            else
            {
                enter = nextY;
                nextY += stepY;
                ty += sy;
            }
            if (enter >= 1)
                break;
            if ((tx, ty) == (ex, ey))
                return !EndBlocks(tx, ty, Z(enter), toZ, double.NaN, toZ);     // the end's own tile, to it
            double leave = Math.Min(Math.Min(nextX, nextY), 1);
            double za = Z(enter), zb = Z(leave);
            if (BlocksLine3D(tx, ty, Math.Min(za, zb), Math.Max(za, zb), shot))
                return false;
        }
        return true;
    }

    // Whether a block on an end's own tile stops the line between two heights: one that the end
    // (at fromZ or toZ, NaN for neither) is inside doesn't (a thing in a wall still sees out)
    bool EndBlocks(int x, int y, double za, double zb, double fromZ, double toZ)
    {
        int i = map.TileIndex(x, y);
        if (!map.InMap(x, y) || (uint)i >= (uint)map.SpanStop.Length)
            return false;
        double low = Math.Min(za, zb), high = Math.Max(za, zb);
        bool Stops(int start, int stop) => Crosses(start, stop, low, high) && !Holds(start, stop, fromZ) && !Holds(start, stop, toZ);
        return Stops(map.SpanStart[i], map.SpanStop[i]) || Stops(map.UpperSpanStart[i], map.UpperSpanStop[i]);
    }

    // Whether a block takes in a height (NaN: never)
    static bool Holds(int start, int stop, double z) =>
        stop != 0 && z >= (start - 1) * GameConstants.TextureSize && z < stop * GameConstants.TextureSize;

    /// <summary>Whether a tile stops a line crossing it between two heights (texels up from the ground)</summary>
    internal bool BlocksLine3D(int x, int y, double low, double high, bool shot)
    {
        if (!map.InMap(x, y) || map.OnEdge(x, y))
            return true;
        int tile = map.TileMap[x, y];
        if (tile == GameConstants.BitWall)
            return true;            // a moving pushwall
        if ((tile & GameConstants.BitDoor) != 0)
        {
            double floor = (map.Story - 1) * GameConstants.TextureSize, top = floor + GameConstants.TextureSize;
            if (low < floor || high > top)
                return true;        // its frame or lintel
            int door = tile & ~GameConstants.BitDoor;
            return DoorPosition(door) < 0x8000 && !DoorLetsThrough((uint)door, shot);
        }
        int i = map.TileIndex(x, y);
        if ((uint)i >= (uint)map.SpanStop.Length)
            return false;
        return Crosses(map.SpanStart[i], map.SpanStop[i], low, high)
            || Crosses(map.UpperSpanStart[i], map.UpperSpanStop[i], low, high);
    }

    // Whether a block from one story to another (a stop of 0: none) takes in any of low..high
    static bool Crosses(int start, int stop, double low, double high) =>
        stop != 0 && high > (start - 1) * GameConstants.TextureSize && low < stop * GameConstants.TextureSize;

    // The 2D line (vanilla's CheckLine) across the player's story's TileMap
    bool CheckLineFlat(int fromX, int fromY, Entities.Actors.Actor to, bool shot)
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
                if (shape != WallShape.Square && value != 0 && Diagonals.LineHitsDiagonal(shape, x, y, fromX, fromY, to.X, to.Y))
                    return false;
                x += xstep;

                if (value == 0 || shape != WallShape.Square)
                    continue;

                if (value < GameConstants.BitDoor || value > GameConstants.BitAllTiles)
                    return false;

                value &= ~(uint)GameConstants.BitDoor;
                intercept = (uint)(yfrac - ystep / 2);

                if (intercept > doors.DoorObjList[value].Position && !DoorLetsThrough(value, shot))
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
                if (shape != WallShape.Square && value != 0 && Diagonals.LineHitsDiagonal(shape, x, y, fromX, fromY, to.X, to.Y))
                    return false;
                y += ystep;

                if (value == 0 || shape != WallShape.Square)
                    continue;

                if (value < GameConstants.BitDoor || value > GameConstants.BitAllTiles)
                    return false;

                value &= ~(uint)GameConstants.BitDoor;
                intercept = (uint)(xfrac - xstep / 2);

                if (intercept > doors.DoorObjList[value].Position && !DoorLetsThrough(value, shot))
                    return false;
            } while (y != yt2);
        }

        return true;
    }

    internal const long MinSight = 0x18000L;
}

/// <summary>Why a traced shot stopped (LineOfSight.TraceShot)</summary>
internal enum ShotEnd { Wall, NoPuffs, Edge, Reach, Stopped }

/// <summary>Where a traced shot stopped, and why; for a wall or a door, its tile (else -1, -1)</summary>
internal readonly record struct ShotTrace(ShotEnd End, int X, int Y, int TileX = -1, int TileY = -1);
