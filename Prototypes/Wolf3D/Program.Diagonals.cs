using Wolf3D.Constants;
using Wolf3D.Enums;
using Wolf3D.Managers;

namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                        DIAGONAL WALL COLLISION AND SIGHT

    A diagonal tile (MapManager.wallshape) is only solid on its named corner's side of the
    45 degree face. It keeps its actorat entry, so enemies never path into one; the player,
    projectiles and sight lines use these tests to get into and see through the open half.
    Coordinates are 16.16 global fixed point; u and v are tile-local (0..TILEGLOBAL).

    =============================================================================
    */

    // The shape the player last ran into in TryMove, for ClipMove to slide along
    static WallShape diagonalblock;

    /// <summary>
    /// How far tile-local (u, v) is into the solid side of the face: positive is solid, zero is
    /// on the face (scaled by root 2, which doesn't matter for the sign).
    /// </summary>
    private static double DiagonalDepth(WallShape shape, double u, double v) => shape switch
    {
        WallShape.SolidNE => u - v,
        WallShape.SolidSW => v - u,
        WallShape.SolidNW => MapConstants.TILEGLOBAL - u - v,
        _ => u + v - MapConstants.TILEGLOBAL,     // SolidSE
    };

    /// <summary>The tile edges a diagonal is solid along (its named corner's two), as automap faces.</summary>
    internal static SeenFlags DiagonalSolidEdges(WallShape shape) => shape switch
    {
        WallShape.SolidNW => SeenFlags.NorthFace | SeenFlags.WestFace,
        WallShape.SolidNE => SeenFlags.NorthFace | SeenFlags.EastFace,
        WallShape.SolidSW => SeenFlags.SouthFace | SeenFlags.WestFace,
        WallShape.SolidSE => SeenFlags.SouthFace | SeenFlags.EastFace,
        _ => SeenFlags.NorthFace | SeenFlags.SouthFace | SeenFlags.WestFace | SeenFlags.EastFace,
    };

    /// <summary>The corners (0 or 1, tile-local) the 45 degree face runs between.</summary>
    internal static (int U0, int V0, int U1, int V1) DiagonalFaceEnds(WallShape shape) =>
        shape is WallShape.SolidNE or WallShape.SolidSW
            ? (0, 0, 1, 1)      // NW corner to SE corner
            : (1, 0, 0, 1);     // NE corner to SW corner

    /// <summary>Whether tile-local (fu, fv), as fractions of a tile, is in the diagonal's solid half.</summary>
    internal static bool InDiagonalSolid(WallShape shape, float fu, float fv) =>
        DiagonalDepth(shape, fu * MapConstants.TILEGLOBAL, fv * MapConstants.TILEGLOBAL) > 0;

    /// <summary>
    /// Whether a box of half-width <paramref name="size"/> centred on (x, y) overlaps the solid
    /// half of the diagonal tile at (tilex, tiley).
    /// </summary>
    internal static bool BoxHitsDiagonal(WallShape shape, int tilex, int tiley, long x, long y, long size)
    {
        long ox = (long)tilex << MapConstants.TILESHIFT;
        long oy = (long)tiley << MapConstants.TILESHIFT;

        //
        // the part of the box inside the tile
        //
        long ul = Math.Max(x - size - ox, 0), uh = Math.Min(x + size - ox, MapConstants.TILEGLOBAL);
        long vl = Math.Max(y - size - oy, 0), vh = Math.Min(y + size - oy, MapConstants.TILEGLOBAL);
        if (ul > uh || vl > vh)
            return false;

        //
        // its corner that reaches furthest toward the solid corner
        //
        long u = shape is WallShape.SolidNE or WallShape.SolidSE ? uh : ul;
        long v = shape is WallShape.SolidSW or WallShape.SolidSE ? vh : vl;
        return DiagonalDepth(shape, u, v) > 0;
    }

    /// <summary>
    /// Whether the line from (x1, y1) to (x2, y2) passes through the solid half of the diagonal
    /// tile at (tilex, tiley). Grazing the face doesn't count.
    /// </summary>
    internal static bool LineHitsDiagonal(WallShape shape, int tilex, int tiley, long x1, long y1, long x2, long y2)
    {
        long ox = (long)tilex << MapConstants.TILESHIFT;
        long oy = (long)tiley << MapConstants.TILESHIFT;
        double dx = x2 - x1, dy = y2 - y1;

        //
        // clip the line to the tile (Liang-Barsky)
        //
        double t0 = 0, t1 = 1;
        bool Clip(double p, double q)
        {
            if (p == 0)
                return q >= 0;
            double r = q / p;
            if (p < 0)
            {
                if (r > t1) return false;
                if (r > t0) t0 = r;
            }
            else
            {
                if (r < t0) return false;
                if (r < t1) t1 = r;
            }
            return true;
        }

        if (!Clip(-dx, x1 - ox) || !Clip(dx, ox + MapConstants.TILEGLOBAL - x1)
            || !Clip(-dy, y1 - oy) || !Clip(dy, oy + MapConstants.TILEGLOBAL - y1))
            return false;

        //
        // depth is linear along the line, so the clipped ends are its deepest points (allowing
        // half a fixed-point unit of rounding, so a line along the face isn't blocked)
        //
        return DiagonalDepth(shape, x1 + t0 * dx - ox, y1 + t0 * dy - oy) > 0.5
            || DiagonalDepth(shape, x1 + t1 * dx - ox, y1 + t1 * dy - oy) > 0.5;
    }
}
