using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Rendering;

/// <summary>
/// The ray trace: for each screen column, a ray from the view point across the map, tile edge by
/// tile edge (vertical edges in VertEntry, horizontal in HorizEntry), until it hits a wall, a
/// door, a diagonal or a moving pushwall (WallHits), going on past short walls for taller ones
/// behind them; then the column's posts are drawn (WallPosts). Tiles it passes are seen.
/// </summary>
internal sealed class RayTrace(
    MapManager map,
    Doors doors,
    PushWalls pushWalls,
    WallPosts posts,
    WallHits hits,
    RenderState render)
{
    internal void WallRefresh()
    {
        short angle;
        int xstep = 0, ystep = 0;
        int xinttemp = 0, yinttemp = 0;                            // holds temporary intercept position
        uint xpartial = 0, ypartial = 0;
        int pwallposnorm=0, pwallposinv=0, pwallposi=0;           // holds modified PWall.Pos
        render.SizeCovers(render.ViewWidth);
        render.AnySeeThrough = false;
        posts.StartFrame();

        for (render.PixX = 0; render.PixX < render.ViewWidth; render.PixX++)
        {
            angle = (short)(render.MidAngle + render.PixelAngle[render.PixX]);

            if (angle < 0) // -90 - -1 degree arc
                angle += GameConstants.Ang360; // -90 is the same as 270
            if (angle >= GameConstants.Ang360) // 360-449 degree arc
                angle -= GameConstants.Ang360; // -449 is the same as 89

            //
            // setup xstep/ystep based on angle
            //
            if (angle < GameConstants.Ang90) // 0-89 degree arc
            {
                render.XTileStep = 1;
                render.YTileStep = -1;
                xstep = TrigTables.FineTangent[GameConstants.Ang90 - 1 - angle];
                ystep = -TrigTables.FineTangent[angle];
                xpartial = (uint)render.XPartialUp;
                ypartial = (uint)render.YPartialDown;
            }
            else if (angle < GameConstants.Ang180) // 90-179 degree arc
            {
                render.XTileStep = -1;
                render.YTileStep = -1;
                xstep = -TrigTables.FineTangent[angle - GameConstants.Ang90];
                ystep = -TrigTables.FineTangent[GameConstants.Ang180 - 1 - angle];
                xpartial = (uint)render.XPartialDown;
                ypartial = (uint)render.YPartialDown;
            }
            else if (angle < GameConstants.Ang270) // 180-269 degree arc
            {
                render.XTileStep = -1;
                render.YTileStep = 1;
                xstep = -TrigTables.FineTangent[GameConstants.Ang270 - 1 - angle];
                ystep = TrigTables.FineTangent[angle - GameConstants.Ang180];
                xpartial = (uint)render.XPartialDown;
                ypartial = (uint)render.YPartialUp;
            }
            else if (angle < GameConstants.Ang360) // 270-359 degree arc
            {
                render.XTileStep = 1;
                render.YTileStep = 1;
                xstep = TrigTables.FineTangent[angle - GameConstants.Ang270];
                ystep = TrigTables.FineTangent[GameConstants.Ang360 - 1 - angle];
                xpartial = (uint)render.XPartialUp;
                ypartial = (uint)render.YPartialUp;
            }

            //
            // initialise variables for intersection testing
            //
            render.YIntercept = MathUtils.FixedMul(ystep, (int)xpartial) + render.ViewY;
            render.YIntTile = render.YIntercept >> (int)MapConstants.TileShift;
            render.XTile = (short)(render.FocalTx + render.XTileStep);

            render.XIntercept = MathUtils.FixedMul(xstep, (int)ypartial) + render.ViewX;
            render.XIntTile = render.XIntercept >> (int)MapConstants.TileShift;
            render.YTile = (short)(render.FocalTy + render.YTileStep);

            render.TexDelta = 0;
            render.HitTile = map.TileIndexOrNone(render.FocalTx, render.FocalTy);
            render.ColumnHit = render.PastWall = render.ArchOpen = false;
            // nothing in front of the sprites until a wall's drawn here: a trace that ends on a
            // wall with no texture (the solid edge round an open map) draws none, and mustn't
            // leave the depth an earlier frame had
            render.WallHeight[render.PixX] = 0;
            render.SeeThroughCount[render.PixX] = 0;
            posts.StartColumn();
            posts.OpenTile(render.FocalTx, render.FocalTy, 0, 0, false, focal: true);
            bool focalhit = false;              // hit a wall in the view's own tile: no trace

            //
            // special treatment when player is in back tile of pushwall
            //
            render.HitPushwall = map.TileMap[render.FocalTx, render.FocalTy] == GameConstants.BitWall;
            if (render.HitPushwall)
            {
                pushWalls.SelectPushWall(render.FocalTx, render.FocalTy);
                posts.SetHitSpan(pushWalls.PWall.Span(map.LevelWallStories));
                if ((pushWalls.PWall.Dir == ControlDir.East && render.XTileStep == 1) || (pushWalls.PWall.Dir == ControlDir.West && render.XTileStep == -1))
                {
                    yinttemp = render.YIntercept - ((ystep * (64 - pushWalls.PWall.Pos)) >> 6);

                    //
                    //  trace hit vertical pushwall back?
                    //
                    if (yinttemp >> MapConstants.TileShift == render.FocalTy)
                    {
                        if (pushWalls.PWall.Dir == ControlDir.East)
                            render.XIntercept = (render.FocalTx << MapConstants.TileShift) + (pushWalls.PWall.Pos << 10);
                        else
                            render.XIntercept = (int)(((render.FocalTx << MapConstants.TileShift) - MapConstants.TileGlobal) + ((64 - pushWalls.PWall.Pos) << 10));

                        render.YIntercept = yinttemp;
                        render.YIntTile = render.YIntercept >> MapConstants.TileShift;
                        render.TileHit = pushWalls.PWall.Tile;
                        hits.HitVertWall();
                        focalhit = true;
                    }
                }
                else if ((pushWalls.PWall.Dir == ControlDir.South && render.YTileStep == 1) || (pushWalls.PWall.Dir == ControlDir.North && render.YTileStep == -1))
                {
                    xinttemp = render.XIntercept - ((xstep * (64 - pushWalls.PWall.Pos)) >> 6);

                    //
                    // trace hit horizontal pushwall back?
                    //
                    if (xinttemp >> MapConstants.TileShift == render.FocalTx)
                    {
                        if (pushWalls.PWall.Dir == ControlDir.South)
                            render.YIntercept = (render.FocalTy << MapConstants.TileShift) + (pushWalls.PWall.Pos << 10);
                        else
                            render.YIntercept = (int)(((render.FocalTy << MapConstants.TileShift) - MapConstants.TileGlobal) + ((64 - pushWalls.PWall.Pos) << 10));

                        render.XIntercept = xinttemp;
                        render.XIntTile = render.XIntercept >> MapConstants.TileShift;
                        render.TileHit = pushWalls.PWall.Tile;
                        hits.HitHorizWall();
                        focalhit = true;
                    }
                }
            }
            else if (map.WallShapes[render.FocalTx, render.FocalTy] is var focalshape and not WallShape.Square)
            {
                //
                // the view starts inside a diagonal tile (NoClip), so the trace never enters it:
                // check its face from the view point
                //
                if (hits.TraceDiagonal(focalshape, render.FocalTx, render.FocalTy,
                    render.ViewX & (MapConstants.TileGlobal - 1), render.ViewY & (MapConstants.TileGlobal - 1),
                    render.XTileStep * MapConstants.TileGlobal, ystep))
                {
                    render.TileHit = map.TileMap[render.FocalTx, render.FocalTy];
                    posts.SetHitBlock(render.FocalTx, render.FocalTy);
                    hits.HitDiagWall(focalshape, render.FocalTx, render.FocalTy);
                    MarkSeen(render.FocalTx, render.FocalTy, SeenFlags.DiagonalFace);
                    focalhit = true;
                }
            }
            else if ((map.TileMap[render.FocalTx, render.FocalTy] & GameConstants.BitDoor) != 0)
            {
                posts.OpenFocalDoorArch(doors.DoorObjList[map.TileMap[render.FocalTx, render.FocalTy] & ~GameConstants.BitDoor].Vertical);
            }

            //
            // trace along this angle until we hit a wall, and on past it while rows are left
            // that something farther could show in (over it, or under one that doesn't reach the
            // ground): a wall that is hit and passed is traced through from where the trace
            // entered its tile, as if it were open floor. Open tiles' floors, blocks and the
            // ceiling close rows too.
            //
            // CORE LOOP!
            //
            while (!focalhit && render.ColumnOpen)
            {
                //
                // check intersections with vertical walls
                //
                if ((render.XTile - render.XTileStep) == render.XIntTile && (render.YTile - render.YTileStep) == render.YIntTile)
                    render.YIntTile = render.YTile;

                if ((render.YTileStep == -1 && render.YIntTile <= render.YTile) || (render.YTileStep == 1 && render.YIntTile >= render.YTile))
                {
                    posts.SaveTrace();
                    if (HorizEntry(xstep, ystep, xinttemp, ref pwallposnorm, ref pwallposinv, ref pwallposi))
                    {
                        if (!posts.TracePastWall(render.SavedXIntTile, render.SavedYTile))
                            break;
                        posts.RestoreTrace();
                        PassHoriz(xstep);
                    }
                }

                //
                // check intersections with horizontal walls
                //
                if ((render.XTile - render.XTileStep) == render.XIntTile && (render.YTile - render.YTileStep) == render.YIntTile)
                    render.XIntTile = render.XTile;

                if ((render.XTileStep == -1 && render.XIntTile <= render.XTile) || (render.XTileStep == 1 && render.XIntTile >= render.XTile))
                {
                    posts.SaveTrace();
                    if (VertEntry(ystep, xstep, yinttemp, ref pwallposnorm, ref pwallposinv, ref pwallposi))
                    {
                        if (!posts.TracePastWall(render.SavedXTile, render.SavedYIntTile))
                            break;
                        posts.RestoreTrace();
                        PassVert(ystep);
                    }
                }
            }

            posts.FinishColumn();
            posts.DrawPosts();
        }
    }

    bool VertEntry(int ystep, int xstep,int yinttemp, ref int pwallposnorm, ref int pwallposinv, ref int pwallposi)
    {
        // the pushwall cases below move YIntTile, so keep the tile the trace entered for the automap
        int hitx = render.XTile, hity = render.YIntTile;
        posts.CloseArch(posts.VertEdge, render.YIntercept);
        posts.CloseTile(posts.VertEdge, render.YIntercept);
        posts.EnterTileLight(render.XTile - render.XTileStep, render.YIntTile);
        posts.OpenTile(hitx, hity, posts.VertEdge, render.YIntercept, true);
        render.TileHit = map.TileMap[render.XTile, render.YIntTile];
        render.HitPushwall = render.TileHit == GameConstants.BitWall;
        if (render.HitPushwall)
        {
            pushWalls.SelectPushWall(hitx, hity);     // which of the moving MovingWalls this is
            posts.SetHitSpan(pushWalls.PWall.Span(map.LevelWallStories));
        }
        else
            posts.SetHitBlock(hitx, hity, door: (render.TileHit & GameConstants.BitDoor) != 0);

        if (render.TileHit != 0)
        {
            if ((render.TileHit & GameConstants.BitDoor) != 0)
            {
                //
                // hit a vertical door, so find which coordinate the door would be
                // intersected at, and check to see if the door is open past that point
                //
                var door = doors.DoorObjList[render.TileHit & ~GameConstants.BitDoor];
                posts.OpenDoorArch(render.XTile, render.YIntTile, door.Vertical, posts.VertEdge, render.YIntercept, true);
                yinttemp = render.YIntercept + (ystep >> 1);    // add halfstep to current intercept position
                int planex = (int)((render.XTile << (int)MapConstants.TileShift) + (MapConstants.TileGlobal / 2));

                if (door.Action == DoorAction.Open)
                {
                    PassVert(ystep); // door is open, continue tracing
                    return false;
                }

                //
                // midpoint is outside tile, so it hit the side of the wall before a door
                //
                if (yinttemp >> MapConstants.TileShift != render.YIntTile)
                {
                    PassVert(ystep);
                    return false;
                }

                if (door.Action != DoorAction.Closed)
                {
                    //
                    // the trace hit the door plane at pixel position YIntercept, see if the door is
                    // closed that much
                    //
                    if (door.OpenAt(yinttemp))
                    {
                        PassVert(ystep);
                        return false;
                    }
                }

                int edgex = render.XIntercept, edgey = render.YIntercept;
                render.YIntercept = yinttemp;
                render.XIntercept = planex;

                if (hits.HitVertDoor())
                {
                    // seen through: on past it from where the trace entered, as an open door
                    render.XIntercept = edgex;
                    render.YIntercept = edgey;
                    MarkSeen(hitx, hity, render.XTileStep == 1 ? SeenFlags.WestFace : SeenFlags.EastFace);
                    PassVert(ystep);
                    return false;
                }
            }
            else if (render.TileHit == GameConstants.BitWall)
            {
                //
                // hit a sliding vertical wall
                //
                if (pushWalls.PWall.Dir == ControlDir.West || pushWalls.PWall.Dir == ControlDir.East)
                {
                    if (pushWalls.PWall.Dir == ControlDir.West)
                    {
                        pwallposnorm = 64 - pushWalls.PWall.Pos;
                        pwallposinv = pushWalls.PWall.Pos;
                    }
                    else
                    {
                        pwallposnorm = pushWalls.PWall.Pos;
                        pwallposinv = 64 - pushWalls.PWall.Pos;
                    }

                    if ((pushWalls.PWall.Dir == ControlDir.East && render.XTile == pushWalls.PWall.X && render.YIntTile == pushWalls.PWall.Y)
                     || (pushWalls.PWall.Dir == ControlDir.West && !(render.XTile == pushWalls.PWall.X && render.YIntTile == pushWalls.PWall.Y)))
                    {
                        yinttemp = render.YIntercept + ((ystep * pwallposnorm) >> 6);

                        if (yinttemp >> MapConstants.TileShift != render.YIntTile)
                        {
                            PassVert(ystep);
                            return false;
                        }

                        render.YIntercept = yinttemp;
                        render.XIntercept = (int)(((render.XTile << MapConstants.TileShift) + MapConstants.TileGlobal) - (pwallposinv << 10));
                        render.YIntTile = render.YIntercept >> MapConstants.TileShift;
                        render.TileHit = pushWalls.PWall.Tile;

                        hits.HitVertWall();
                    }
                    else
                    {
                        yinttemp = render.YIntercept + ((ystep * pwallposinv) >> 6);

                        if (yinttemp >> MapConstants.TileShift != render.YIntTile)
                        {
                            PassVert(ystep);
                            return false;
                        }

                        render.YIntercept = yinttemp;
                        render.XIntercept = (render.XTile << MapConstants.TileShift) -(pwallposinv << 10);
                        render.YIntTile = render.YIntercept >> MapConstants.TileShift;
                        render.TileHit = pushWalls.PWall.Tile;

                        hits.HitVertWall();
                    }
                }
                else
                {
                    if (pushWalls.PWall.Dir == ControlDir.North)
                        pwallposi = 64 - pushWalls.PWall.Pos;
                    else
                        pwallposi = pushWalls.PWall.Pos;

                    if ((pushWalls.PWall.Dir == ControlDir.South && (ushort)render.YIntercept < (pwallposi << 10))
                     || (pushWalls.PWall.Dir == ControlDir.North && (ushort)render.YIntercept > (pwallposi << 10)))
                    {
                        if (render.XTile == pushWalls.PWall.X && render.YIntTile == pushWalls.PWall.Y)
                        {
                            if ((pushWalls.PWall.Dir == ControlDir.South && (int)((ushort)render.YIntercept) + ystep < (pwallposi << 10))
                             || (pushWalls.PWall.Dir == ControlDir.North && (int)((ushort)render.YIntercept) + ystep > (pwallposi << 10)))
                            {
                                //goto PassVert;
                                PassVert(ystep);
                                return false;
                            }

                            //
                            // set up a horizontal intercept position
                            //
                            if (pushWalls.PWall.Dir == ControlDir.South)
                                render.YIntercept = (render.YIntTile << MapConstants.TileShift) + (pwallposi << 10);
                            else
                                render.YIntercept = (int)(((render.YIntTile << MapConstants.TileShift) - MapConstants.TileGlobal) + (pwallposi << 10));

                            render.XIntercept -= (xstep * (64 - pushWalls.PWall.Pos)) >> 6;
                            render.XIntTile = render.XIntercept >> MapConstants.TileShift;
                            render.TileHit = pushWalls.PWall.Tile;

                            hits.HitHorizWall();
                        }
                        else
                        {
                            render.TexDelta = (ushort)(pwallposi << 10);
                            render.XIntercept = render.XTile << MapConstants.TileShift;
                            render.TileHit = pushWalls.PWall.Tile;

                            hits.HitVertWall();
                        }
                    }
                    else
                    {
                        if (render.XTile == pushWalls.PWall.X && render.YIntTile == pushWalls.PWall.Y)
                        {
                            render.TexDelta = (ushort)(pwallposi << 10);
                            render.XIntercept = render.XTile << MapConstants.TileShift;
                            render.TileHit = pushWalls.PWall.Tile;

                            hits.HitVertWall();
                        }
                        else
                        {
                            if ((pushWalls.PWall.Dir == ControlDir.South && ((ushort)render.YIntercept) + ystep > (pwallposi << 10))
                             || (pushWalls.PWall.Dir == ControlDir.North && ((ushort)render.YIntercept) + ystep < (pwallposi << 10)))
                            {
                                //goto PassVert;
                                PassVert(ystep);
                                return false;
                            }

                            //
                            // set up a horizontal intercept position
                            //
                            if (pushWalls.PWall.Dir == ControlDir.South)
                                render.YIntercept = (render.YIntTile << MapConstants.TileShift) - ((64 - pushWalls.PWall.Pos) << 10);
                            else
                                render.YIntercept = (render.YIntTile << MapConstants.TileShift) + ((64 - pushWalls.PWall.Pos) << 10);

                            render.XIntercept -= (xstep * pushWalls.PWall.Pos) >> 6;
                            render.XIntTile = render.XIntercept >> MapConstants.TileShift;
                            render.TileHit = pushWalls.PWall.Tile;

                            hits.HitHorizWall();
                        }
                    }
                }
            }
            else
            {
                var shape = map.WallShapes[render.XTile, render.YIntTile];
                if (shape != WallShape.Square
                    && !hits.IsSolidEdge(shape, render.XTileStep == 1 ? ControlDir.West : ControlDir.East))
                {
                    //
                    // entered a diagonal by an open edge: hit its face, or cross the open half
                    //
                    long eu = render.XTileStep == 1 ? 0 : MapConstants.TileGlobal;
                    long ev = render.YIntercept - (render.YIntTile << MapConstants.TileShift);
                    if (!hits.TraceDiagonal(shape, render.XTile, render.YIntTile, eu, ev, render.XTileStep * MapConstants.TileGlobal, ystep))
                    {
                        PassVert(ystep);
                        return false;
                    }

                    hits.HitDiagWall(shape, render.XTile, render.YIntTile);
                    MarkSeen(hitx, hity, SeenFlags.DiagonalFace);
                    return true;
                }
                else
                {
                    render.XIntercept = (render.XTile << MapConstants.TileShift);

                    hits.HitVertWall();
                }
            }

            MarkSeen(hitx, hity, render.XTileStep == 1 ? SeenFlags.WestFace : SeenFlags.EastFace);
            return true;
        }

        //
        // mark the tile as visible and setup for next step
        //
        if (map.WallShapes[render.XTile, render.YIntTile] is var openshape and not WallShape.Square
            && !hits.IsSolidEdge(openshape, render.XTileStep == 1 ? ControlDir.West : ControlDir.East))
            WalkableDiagonal(openshape, render.XTile, render.YIntTile, render.XTileStep == 1 ? 0 : MapConstants.TileGlobal,
                render.YIntercept - (render.YIntTile << MapConstants.TileShift), render.XTileStep * MapConstants.TileGlobal, ystep);
        PassVert(ystep);
        return false;
    }

    internal void PassVert(int ystep)
    {
        if (!render.PastWall)
            map.Spotvis[render.XTile, render.YIntTile] = true;
        render.XTile += render.XTileStep;
        render.YIntercept += ystep;
        render.YIntTile = render.YIntercept >> (int)MapConstants.TileShift;
    }

    bool HorizEntry(int xstep,int ystep, int xinttemp, ref int pwallposnorm, ref int pwallposinv, ref int pwallposi)
    {
        // the pushwall cases below move XIntTile, so keep the tile the trace entered for the automap
        int hitx = render.XIntTile, hity = render.YTile;
        posts.CloseArch(render.XIntercept, posts.HorizEdge);
        posts.CloseTile(render.XIntercept, posts.HorizEdge);
        posts.EnterTileLight(render.XIntTile, render.YTile - render.YTileStep);
        posts.OpenTile(hitx, hity, render.XIntercept, posts.HorizEdge, false);
        render.TileHit = map.TileMap[render.XIntTile, render.YTile];
        render.HitPushwall = render.TileHit == GameConstants.BitWall;
        if (render.HitPushwall)
        {
            pushWalls.SelectPushWall(hitx, hity);     // which of the moving MovingWalls this is
            posts.SetHitSpan(pushWalls.PWall.Span(map.LevelWallStories));
        }
        else
            posts.SetHitBlock(hitx, hity, door: (render.TileHit & GameConstants.BitDoor) != 0);

        if (render.TileHit != 0)
        {
            if ((render.TileHit & GameConstants.BitDoor) != 0)
            {
                //
                // hit a horizontal door, so find which coordinate the door would be
                // intersected at, and check to see if the door is open past that point
                //
                var door = doors.DoorObjList[render.TileHit & ~GameConstants.BitDoor];
                posts.OpenDoorArch(render.XIntTile, render.YTile, door.Vertical, render.XIntercept, posts.HorizEdge, false);
                xinttemp = render.XIntercept + (xstep >> 1);    // add half step to current intercept position
                int planey = (int)((render.YTile << (int)MapConstants.TileShift) + (MapConstants.TileGlobal / 2));

                if (door.Action == (byte)DoorAction.Open)
                {
                    PassHoriz(xstep); // door is open, continue tracing
                    return false;
                }

                //
                // midpoint is outside tile, so it hit the side of the wall before a door
                //
                if ((xinttemp >> MapConstants.TileShift) != render.XIntTile)
                {
                    PassHoriz(xstep);
                    return false;
                }

                if (door.Action != DoorAction.Closed)
                {
                    //
                    // the trace hit the door plane at pixel position XIntercept, see if the door is
                    // closed that much
                    //
                    if (door.OpenAt(xinttemp))
                    {
                        PassHoriz(xstep);
                        return false;
                    }
                }

                int edgex = render.XIntercept, edgey = render.YIntercept;
                render.XIntercept = xinttemp;
                render.YIntercept = planey;

                if (hits.HitHorizDoor())
                {
                    render.XIntercept = edgex;
                    render.YIntercept = edgey;
                    MarkSeen(hitx, hity, render.YTileStep == 1 ? SeenFlags.NorthFace : SeenFlags.SouthFace);
                    PassHoriz(xstep);
                    return false;
                }
            }
            else if (render.TileHit == GameConstants.BitWall)
            {
                //
                // hit a sliding horizontal wall
                //
                if (pushWalls.PWall.Dir == ControlDir.North || pushWalls.PWall.Dir == ControlDir.South)
                {
                    if (pushWalls.PWall.Dir == ControlDir.North)
                    {
                        pwallposnorm = 64 - pushWalls.PWall.Pos;
                        pwallposinv = pushWalls.PWall.Pos;
                    }
                    else
                    {
                        pwallposnorm = pushWalls.PWall.Pos;
                        pwallposinv = 64 - pushWalls.PWall.Pos;
                    }

                    if ((pushWalls.PWall.Dir == ControlDir.South && render.XIntTile == pushWalls.PWall.X && render.YTile == pushWalls.PWall.Y)
                     || (pushWalls.PWall.Dir == ControlDir.North && !(render.XIntTile == pushWalls.PWall.X && render.YTile == pushWalls.PWall.Y)))
                    {
                        xinttemp = render.XIntercept + ((xstep * pwallposnorm) >> 6);

                        if (xinttemp >> MapConstants.TileShift != render.XIntTile)
                        {
                            PassHoriz(xstep);
                            return false;
                        }

                        render.XIntercept = xinttemp;
                        render.YIntercept = (int)(((render.YTile << MapConstants.TileShift) +MapConstants.TileGlobal) -(pwallposinv << 10));
                        render.XIntTile = render.XIntercept >> MapConstants.TileShift;
                        render.TileHit = pushWalls.PWall.Tile;

                        hits.HitHorizWall();
                    }
                    else
                    {
                        xinttemp = render.XIntercept + ((xstep * pwallposinv) >> 6);

                        if (xinttemp >> MapConstants.TileShift != render.XIntTile)
                        {
                            PassHoriz(xstep);
                            return false;
                        }

                        render.XIntercept = xinttemp;
                        render.YIntercept = (render.YTile << MapConstants.TileShift) -(pwallposinv << 10);
                        render.XIntTile = render.XIntercept >> MapConstants.TileShift;
                        render.TileHit = pushWalls.PWall.Tile;

                        hits.HitHorizWall();
                    }
                }
                else
                {
                    if (pushWalls.PWall.Dir == ControlDir.West)
                        pwallposi = 64 - pushWalls.PWall.Pos;
                    else
                        pwallposi = pushWalls.PWall.Pos;

                    if ((pushWalls.PWall.Dir == ControlDir.East && (ushort)render.XIntercept < (pwallposi << 10))
                     || (pushWalls.PWall.Dir == ControlDir.West && (ushort)render.XIntercept > (pwallposi << 10)))
                    {
                        if (render.XIntTile == pushWalls.PWall.X && render.YTile == pushWalls.PWall.Y)
                        {
                            // (ushort): the intercept's place along the tile, not the whole map
                            // coordinate, or a ray heading away from the face is set up on it
                            if ((pushWalls.PWall.Dir == ControlDir.East && (int)((ushort)render.XIntercept) + xstep < (pwallposi << 10))
                             || (pushWalls.PWall.Dir == ControlDir.West && (int)((ushort)render.XIntercept) + xstep > (pwallposi << 10)))

                            {
                                PassHoriz(xstep);
                                return false;
                            }

                            //
                            // set up a vertical intercept position
                            //
                            render.YIntercept -= (ystep * (64 - pushWalls.PWall.Pos)) >> 6;
                            render.YIntTile = render.YIntercept >> MapConstants.TileShift;

                            if (pushWalls.PWall.Dir == ControlDir.East)
                                render.XIntercept = (render.XIntTile << MapConstants.TileShift) + (pwallposi << 10);
                            else
                                render.XIntercept = (int)(((render.XIntTile << MapConstants.TileShift) - MapConstants.TileGlobal) + (pwallposi << 10));

                            render.TileHit = pushWalls.PWall.Tile;

                            hits.HitVertWall();
                        }
                        else
                        {
                            render.TexDelta = (ushort)(pwallposi << 10);
                            render.YIntercept = render.YTile << MapConstants.TileShift;
                            render.TileHit = pushWalls.PWall.Tile;

                            hits.HitHorizWall();
                        }
                    }
                    else
                    {
                        if (render.XIntTile == pushWalls.PWall.X && render.YTile == pushWalls.PWall.Y)
                        {
                            render.TexDelta = (ushort)(pwallposi << 10);
                            render.YIntercept = render.YTile << MapConstants.TileShift;
                            render.TileHit = pushWalls.PWall.Tile;

                            hits.HitHorizWall();
                        }
                        else
                        {
                            if ((pushWalls.PWall.Dir == ControlDir.East && (int)((ushort)render.XIntercept) + xstep > (pwallposi << 10))
                             || (pushWalls.PWall.Dir == ControlDir.West && (int)((ushort)render.XIntercept) + xstep < (pwallposi << 10)))

                            {
                                PassHoriz(xstep);
                                return false;
                            }

                            //
                            // set up a vertical intercept position
                            //
                            render.YIntercept -= (ystep * pushWalls.PWall.Pos) >> 6;
                            render.YIntTile = render.YIntercept >> MapConstants.TileShift;

                            if (pushWalls.PWall.Dir == ControlDir.East)
                                render.XIntercept = (render.XIntTile << MapConstants.TileShift) - ((64 - pushWalls.PWall.Pos) << 10);
                            else
                                render.XIntercept = (render.XIntTile << MapConstants.TileShift) + ((64 - pushWalls.PWall.Pos) << 10);

                            render.TileHit = pushWalls.PWall.Tile;

                            hits.HitVertWall();
                        }
                    }
                }
            }
            else
            {
                var shape = map.WallShapes[render.XIntTile, render.YTile];
                if (shape != WallShape.Square
                    && !hits.IsSolidEdge(shape, render.YTileStep == 1 ? ControlDir.North : ControlDir.South))
                {
                    //
                    // entered a diagonal by an open edge: hit its face, or cross the open half
                    //
                    long eu = render.XIntercept - (render.XIntTile << MapConstants.TileShift);
                    long ev = render.YTileStep == 1 ? 0 : MapConstants.TileGlobal;
                    if (!hits.TraceDiagonal(shape, render.XIntTile, render.YTile, eu, ev, xstep, render.YTileStep * MapConstants.TileGlobal))
                    {
                        PassHoriz(xstep);
                        return false;
                    }

                    hits.HitDiagWall(shape, render.XIntTile, render.YTile);
                    MarkSeen(hitx, hity, SeenFlags.DiagonalFace);
                    return true;
                }
                else
                {
                    render.YIntercept = render.YTile << MapConstants.TileShift;

                    hits.HitHorizWall();
                }
            }

            MarkSeen(hitx, hity, render.YTileStep == 1 ? SeenFlags.NorthFace : SeenFlags.SouthFace);
            return true;
        }

        //
        // mark the tile as visible and setup for next step
        //
        if (map.WallShapes[render.XIntTile, render.YTile] is var openshape and not WallShape.Square
            && !hits.IsSolidEdge(openshape, render.YTileStep == 1 ? ControlDir.North : ControlDir.South))
            WalkableDiagonal(openshape, render.XIntTile, render.YTile, render.XIntercept - (render.XIntTile << MapConstants.TileShift),
                render.YTileStep == 1 ? 0 : MapConstants.TileGlobal, xstep, render.YTileStep * MapConstants.TileGlobal);
        PassHoriz(xstep);
        return false;
    }

    /// <summary>
    /// A diagonal tile that's walked on or under at the player's story (its block doesn't take the
    /// story in), entered by an open edge: its slanted face is drawn at its block's stories if the
    /// trace meets it, and the trace goes on through the tile as through any open one
    /// </summary>
    void WalkableDiagonal(WallShape shape, int tilex, int tiley, long eu, long ev, long du, long dv)
    {
        if (map.TileMap[tilex, tiley] != 0 || !map.IsWallCode(tilex, tiley))
            return;
        int xintercept = render.XIntercept, yintercept = render.YIntercept;
        if (hits.TraceDiagonal(shape, tilex, tiley, eu, ev, du, dv))
        {
            var tilehit = render.TileHit;
            render.TileHit = (ushort)map.MapSpot(tilex, tiley, 0);
            hits.HitDiagWall(shape, tilex, tiley, solid: false);
            render.TileHit = tilehit;
        }
        (render.XIntercept, render.YIntercept) = (xintercept, yintercept);
    }

    internal void PassHoriz(int xstep)
    {
        if (!render.PastWall)
            map.Spotvis[render.XIntTile, render.YTile] = true;
        render.YTile += render.YTileStep;
        render.XIntercept += xstep;
        render.XIntTile = render.XIntercept >> (int)MapConstants.TileShift;
    }

    /// <summary>Marks a wall face the rays hit as seen, for the automap, when the view is the player's</summary>
    internal void MarkSeen(int x, int y, SeenFlags face)
    {
        if (render.MarkSeen)
            map.Seen[x, y] |= face;
    }
}