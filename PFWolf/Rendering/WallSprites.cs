using PFWolf.Constants;

namespace PFWolf.Rendering;

/// <summary>
/// A panel's ends: (X1, Y1) is the sprite's left edge seen from the front, (X2, Y2) its right
/// edge, so the sprite's columns run from end 1 to end 2.
/// </summary>
internal readonly record struct WallSpriteSpan(double X1, double Y1, double X2, double Y2)
{
    public double Length => Math.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));
}

/// <summary>
/// WALL SPRITES (DIRECTIONAL 3D SPRITES): a static with `flags: [WALLSPRITE]` in actordefs is
/// drawn as a flat panel standing in its tile, like Wolf4SDL's directional 3D sprites, instead
/// of a billboard -- fences, gates and grilles. Its map `angles` (actor.Dir) is the way the
/// front of the panel faces: 0/180 face east/west (a north-south panel), 90/270 face
/// north/south (an east-west panel), and the four diagonals stand the panel corner to corner.
/// The back shows the sprite mirrored.
///
/// The `wallsprite.offset` property moves the panel off the tile centre toward its front, in
/// texels (64 to a tile): 32 puts an axis-aligned panel on the tile edge. The panel is always
/// clipped to its own tile. Coordinates are 16.16 global fixed point.
///
/// The panel blocks along its line: the player and projectiles can stand or fly on either
/// side of it in its tile, and it blocks sight and hitscan unless it has the
/// WALLSPRITE.SHOOTTHROUGH flag (a grille or chain-link fence), and projectiles unless it has
/// WALLSPRITE.PROJECTILETHROUGH. Enemies keep out of its whole tile (WallSpriteBlocker in
/// actorat), as they do a diagonal wall's.
///
/// A WALLSPRITE.PASSABLE panel is only seen: the player, enemies and projectiles go through it
/// (an arch, cobwebs, a beam overhead); it still blocks sight and hitscan unless it's
/// SHOOTTHROUGH too. A panel that's an enemy (shootable: a window, a curtain) blocks as the
/// enemy does while it lives, and once it's destroyed (dead) it blocks nothing.
/// </summary>
internal static class WallSprites
{
    internal const string WallSpriteFlag = "WALLSPRITE";
    internal const string WallSpriteShootThroughFlag = "WALLSPRITE.SHOOTTHROUGH";
    internal const string WallSpriteProjectileThroughFlag = "WALLSPRITE.PROJECTILETHROUGH";
    internal const string WallSpritePassableFlag = "WALLSPRITE.PASSABLE";
    internal const string WallSpriteOffsetProperty = "wallsprite.offset";

    internal static bool IsWallSprite(Entities.Actors.Actor actor) =>
        actor.Flags.Contains(WallSpriteFlag, StringComparer.OrdinalIgnoreCase);

    /// <summary>A WALLSPRITE.PASSABLE panel: nothing moving is stopped by it</summary>
    internal static bool IsPassable(Entities.Actors.Actor actor) =>
        actor.Flags.Contains(WallSpritePassableFlag, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A panel with health (a window, a curtain) that's been destroyed, so it's no longer
    /// shootable: it blocks nothing
    /// </summary>
    internal static bool IsBroken(Entities.Actors.Actor actor) =>
        actor is Entities.Actors.Monster && !actor.RuntimeFlags.HasFlag(ObjFlags.Shootable)
        && actor.Properties.Keys.Any(k => k == "health" || k.StartsWith("health.", StringComparison.Ordinal));

    /// <summary>Which way a facing's panel runs across the tile, for messages.</summary>
    internal static string WallSpriteAxis(ObjDir dir) => dir switch
    {
        ObjDir.East or ObjDir.West => "north-south",
        ObjDir.North or ObjDir.South => "east-west",
        ObjDir.NorthEast or ObjDir.SouthWest => "northwest-southeast",
        ObjDir.NorthWest or ObjDir.SouthEast => "northeast-southwest",
        _ => "none",
    };

    /// <summary>
    /// Where a wall sprite's panel stands, or null if the actor isn't one, has no facing, or its
    /// offset pushes the panel out of its tile.
    /// </summary>
    internal static WallSpriteSpan? GetWallSpriteSpan(Entities.Actors.Actor actor)
    {
        if (!IsWallSprite(actor) || actor.Dir == ObjDir.NoDir)
            return null;

        //
        // the front's normal (y grows south) and the direction the sprite's columns run, which
        // is the right hand of someone looking at the front
        //
        double angle = (int)actor.Dir * Math.PI / 4;
        double nx = Math.Round(Math.Cos(angle), 12), ny = -Math.Round(Math.Sin(angle), 12);
        double dx = ny, dy = -nx;

        double offset = actor.Properties.TryGetValue(WallSpriteOffsetProperty, out var value)
            ? Convert.ToDouble(value) * MapConstants.TileGlobal / 64
            : 0;

        double ox = (double)actor.TileX * MapConstants.TileGlobal;
        double oy = (double)actor.TileY * MapConstants.TileGlobal;
        double px = ox + MapConstants.TileGlobal / 2 + nx * offset;
        double py = oy + MapConstants.TileGlobal / 2 + ny * offset;

        //
        // clip the panel's line to the tile (Liang-Barsky, unbounded in both directions)
        //
        double t0 = double.NegativeInfinity, t1 = double.PositiveInfinity;
        bool Clip(double p, double q)
        {
            if (p == 0)
                return q >= 0;
            double r = q / p;
            if (p < 0)
                t0 = Math.Max(t0, r);
            else
                t1 = Math.Min(t1, r);
            return t0 < t1;
        }

        if (!Clip(-dx, px - ox) || !Clip(dx, ox + MapConstants.TileGlobal - px)
            || !Clip(-dy, py - oy) || !Clip(dy, oy + MapConstants.TileGlobal - py))
            return null;

        return new WallSpriteSpan(px + t0 * dx, py + t0 * dy, px + t1 * dx, py + t1 * dy);
    }

    /// <summary>
    /// Whether a box of half-width <paramref name="size"/> centred on (x, y) crosses the panel.
    /// Touching it along the box's edge doesn't count, so the player can slide along one.
    /// </summary>
    internal static bool BoxHitsWallSprite(WallSpriteSpan span, long x, long y, long size)
    {
        double dx = span.X2 - span.X1, dy = span.Y2 - span.Y1;

        //
        // clip the panel to the box (Liang-Barsky)
        //
        double t0 = 0, t1 = 1;
        bool Clip(double p, double q)
        {
            if (p == 0)
                return q > 0;
            double r = q / p;
            if (p < 0)
                t0 = Math.Max(t0, r);
            else
                t1 = Math.Min(t1, r);
            return t0 < t1;
        }

        return Clip(-dx, span.X1 - (x - size)) && Clip(dx, x + size - span.X1)
            && Clip(-dy, span.Y1 - (y - size)) && Clip(dy, y + size - span.Y1);
    }

    /// <summary>Whether the line from (x1, y1) to (x2, y2) crosses the panel.</summary>
    internal static bool LineHitsWallSprite(WallSpriteSpan span, long x1, long y1, long x2, long y2)
    {
        static double Cross(double ax, double ay, double bx, double by) => ax * by - ay * bx;

        double ex = span.X2 - span.X1, ey = span.Y2 - span.Y1;
        double d1 = Cross(ex, ey, x1 - span.X1, y1 - span.Y1);
        double d2 = Cross(ex, ey, x2 - span.X1, y2 - span.Y1);
        if (d1 > 0 && d2 > 0 || d1 < 0 && d2 < 0 || d1 == 0 && d2 == 0)
            return false;                               // both ends on one side, or along it

        double lx = x2 - x1, ly = y2 - y1;
        double d3 = Cross(lx, ly, span.X1 - x1, span.Y1 - y1);
        double d4 = Cross(lx, ly, span.X2 - x1, span.Y2 - y1);
        return !(d3 > 0 && d4 > 0 || d3 < 0 && d4 < 0);
    }
}

/// <summary>Wall sprite panels blocking movement, projectiles, sight and hitscan on the level (WallSprites)</summary>
internal sealed class WallSpriteBlocking(MapManager map)
{
    // Where ClipMove's move starts: a panel the player already overlaps there (one spawned on
    // top of them) doesn't block, so they can always step off it
    internal int WallSpriteFromX, WallSpriteFromY;

    /// <summary>
    /// The first panel standing in tiles xl..xh, yl..yh that a box of half-width
    /// <paramref name="size"/> centred on (x, y) crosses. For the player, a panel the box
    /// already crossed where ClipMove's move began is skipped; otherwise it's a projectile's box,
    /// which WALLSPRITE.PROJECTILETHROUGH panels let by.
    /// </summary>
    internal WallSpriteSpan? WallSpriteHitByBox(int xl, int yl, int xh, int yh, long x, long y, long size, bool player)
    {
        foreach (var actor in map.GetActors())
        {
            if (actor.TileX < xl || actor.TileX > xh || actor.TileY < yl || actor.TileY > yh)
                continue;
            if (!WallSprites.IsWallSprite(actor) || WallSprites.GetWallSpriteSpan(actor) is not { } span)
                continue;
            if (WallSprites.IsPassable(actor) || WallSprites.IsBroken(actor))
                continue;
            if (!player && actor.Flags.Contains(WallSprites.WallSpriteProjectileThroughFlag, StringComparer.OrdinalIgnoreCase))
                continue;
            if (!WallSprites.BoxHitsWallSprite(span, x, y, size))
                continue;
            if (player && WallSprites.BoxHitsWallSprite(span, WallSpriteFromX, WallSpriteFromY, size))
                continue;
            return span;
        }
        return null;
    }

    /// <summary>Whether a panel that isn't WALLSPRITE.SHOOTTHROUGH stands across the line (sight, hitscan).</summary>
    internal bool WallSpriteBlocksLine(long x1, long y1, long x2, long y2)
    {
        long xl = Math.Min(x1, x2) >> MapConstants.TileShift, xh = Math.Max(x1, x2) >> MapConstants.TileShift;
        long yl = Math.Min(y1, y2) >> MapConstants.TileShift, yh = Math.Max(y1, y2) >> MapConstants.TileShift;

        foreach (var actor in map.GetActors())
        {
            if (actor.TileX < xl || actor.TileX > xh || actor.TileY < yl || actor.TileY > yh)
                continue;
            if (!WallSprites.IsWallSprite(actor) || actor.Flags.Contains(WallSprites.WallSpriteShootThroughFlag, StringComparer.OrdinalIgnoreCase)
                || WallSprites.IsBroken(actor))
                continue;
            if (WallSprites.GetWallSpriteSpan(actor) is { } span && WallSprites.LineHitsWallSprite(span, x1, y1, x2, y2))
                return true;
        }
        return false;
    }
}
