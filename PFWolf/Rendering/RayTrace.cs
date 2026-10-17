using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Rendering;

/// <summary>
/// The ray trace: for each screen column, a ray from the view point across the map, tile edge by
/// tile edge (vertical edges in vertentry, horizontal in horizentry), until it hits a wall, a
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
        int pwallposnorm=0, pwallposinv=0, pwallposi=0;           // holds modified pwall.Pos

        for (render.pixx = 0; render.pixx < render.viewwidth; render.pixx++)
        {
            angle = (short)(render.midangle + render.pixelangle[render.pixx]);

            if (angle < 0) // -90 - -1 degree arc
                angle += GameConstants.Ang360; // -90 is the same as 270
            if (angle >= GameConstants.Ang360) // 360-449 degree arc
                angle -= GameConstants.Ang360; // -449 is the same as 89

            //
            // setup xstep/ystep based on angle
            //
            if (angle < GameConstants.Ang90) // 0-89 degree arc
            {
                render.xtilestep = 1;
                render.ytilestep = -1;
                xstep = TrigTables.finetangent[GameConstants.Ang90 - 1 - angle];
                ystep = -TrigTables.finetangent[angle];
                xpartial = (uint)render.xpartialup;
                ypartial = (uint)render.ypartialdown;
            }
            else if (angle < GameConstants.Ang180) // 90-179 degree arc
            {
                render.xtilestep = -1;
                render.ytilestep = -1;
                xstep = -TrigTables.finetangent[angle - GameConstants.Ang90];
                ystep = -TrigTables.finetangent[GameConstants.Ang180 - 1 - angle];
                xpartial = (uint)render.xpartialdown;
                ypartial = (uint)render.ypartialdown;
            }
            else if (angle < GameConstants.Ang270) // 180-269 degree arc
            {
                render.xtilestep = -1;
                render.ytilestep = 1;
                xstep = -TrigTables.finetangent[GameConstants.Ang270 - 1 - angle];
                ystep = TrigTables.finetangent[angle - GameConstants.Ang180];
                xpartial = (uint)render.xpartialdown;
                ypartial = (uint)render.ypartialup;
            }
            else if (angle < GameConstants.Ang360) // 270-359 degree arc
            {
                render.xtilestep = 1;
                render.ytilestep = 1;
                xstep = TrigTables.finetangent[angle - GameConstants.Ang270];
                ystep = TrigTables.finetangent[GameConstants.Ang360 - 1 - angle];
                xpartial = (uint)render.xpartialup;
                ypartial = (uint)render.ypartialup;
            }

            //
            // initialise variables for intersection testing
            //
            render.yintercept = MathUtils.FixedMul(ystep, (int)xpartial) + render.viewy;
            render.yinttile = render.yintercept >> (int)MapConstants.TileShift;
            render.xtile = (short)(render.focaltx + render.xtilestep);

            render.xintercept = MathUtils.FixedMul(xstep, (int)ypartial) + render.viewx;
            render.xinttile = render.xintercept >> (int)MapConstants.TileShift;
            render.ytile = (short)(render.focalty + render.ytilestep);

            render.texdelta = 0;
            render.hittile = Lighting.TileIndex(render.focaltx, render.focalty);
            render.columnhit = render.pastwall = render.archopen = false;
            render.columntallest = 0;
            render.columntop = render.viewheight;
            bool focalhit = false;              // hit a wall in the view's own tile: no trace

            //
            // special treatment when player is in back tile of pushwall
            //
            render.hitpushwall = map.tilemap[render.focaltx, render.focalty] == GameConstants.BitWall;
            if (render.hitpushwall)
            {
                pushWalls.SelectPushWall(render.focaltx, render.focalty);
                render.hitstories = map.WallStories(pushWalls.pwall.X, pushWalls.pwall.Y);
                if ((pushWalls.pwall.Dir == ControlDir.East && render.xtilestep == 1) || (pushWalls.pwall.Dir == ControlDir.West && render.xtilestep == -1))
                {
                    yinttemp = render.yintercept - ((ystep * (64 - pushWalls.pwall.Pos)) >> 6);

                    //
                    //  trace hit vertical pushwall back?
                    //
                    if (yinttemp >> MapConstants.TileShift == render.focalty)
                    {
                        if (pushWalls.pwall.Dir == ControlDir.East)
                            render.xintercept = (render.focaltx << MapConstants.TileShift) + (pushWalls.pwall.Pos << 10);
                        else
                            render.xintercept = (int)(((render.focaltx << MapConstants.TileShift) - MapConstants.TileGlobal) + ((64 - pushWalls.pwall.Pos) << 10));

                        render.yintercept = yinttemp;
                        render.yinttile = render.yintercept >> MapConstants.TileShift;
                        render.tilehit = pushWalls.pwall.Tile;
                        hits.HitVertWall();
                        focalhit = true;
                    }
                }
                else if ((pushWalls.pwall.Dir == ControlDir.South && render.ytilestep == 1) || (pushWalls.pwall.Dir == ControlDir.North && render.ytilestep == -1))
                {
                    xinttemp = render.xintercept - ((xstep * (64 - pushWalls.pwall.Pos)) >> 6);

                    //
                    // trace hit horizontal pushwall back?
                    //
                    if (xinttemp >> MapConstants.TileShift == render.focaltx)
                    {
                        if (pushWalls.pwall.Dir == ControlDir.South)
                            render.yintercept = (render.focalty << MapConstants.TileShift) + (pushWalls.pwall.Pos << 10);
                        else
                            render.yintercept = (int)(((render.focalty << MapConstants.TileShift) - MapConstants.TileGlobal) + ((64 - pushWalls.pwall.Pos) << 10));

                        render.xintercept = xinttemp;
                        render.xinttile = render.xintercept >> MapConstants.TileShift;
                        render.tilehit = pushWalls.pwall.Tile;
                        hits.HitHorizWall();
                        focalhit = true;
                    }
                }
            }
            else if (map.wallshape[render.focaltx, render.focalty] is var focalshape and not WallShape.Square)
            {
                //
                // the view starts inside a diagonal tile (noclip), so the trace never enters it:
                // check its face from the view point
                //
                if (hits.TraceDiagonal(focalshape, render.focaltx, render.focalty,
                    render.viewx & (MapConstants.TileGlobal - 1), render.viewy & (MapConstants.TileGlobal - 1),
                    render.xtilestep * MapConstants.TileGlobal, ystep))
                {
                    render.tilehit = map.tilemap[render.focaltx, render.focalty];
                    render.hitstories = map.WallStories(render.focaltx, render.focalty);
                    hits.HitDiagWall(focalshape, render.focaltx, render.focalty);
                    MarkSeen(render.focaltx, render.focalty, SeenFlags.DiagonalFace);
                    focalhit = true;
                }
            }
            else if ((map.tilemap[render.focaltx, render.focalty] & GameConstants.BitDoor) != 0)
            {
                hits.NoteFocalLintel(xstep, ystep);
            }
            else if (posts.IsArch(render.focaltx, render.focalty))
            {
                posts.OpenFocalArch();
            }

            //
            // trace along this angle until we hit a wall, and on past it while a taller wall
            // could still show over the top: a wall that is hit and passed is traced through
            // from where the trace entered its tile, as if it were open floor
            //
            // CORE LOOP!
            //
            while (!focalhit)
            {
                //
                // check intersections with vertical walls
                //
                if ((render.xtile - render.xtilestep) == render.xinttile && (render.ytile - render.ytilestep) == render.yinttile)
                    render.yinttile = render.ytile;

                if ((render.ytilestep == -1 && render.yinttile <= render.ytile) || (render.ytilestep == 1 && render.yinttile >= render.ytile))
                {
                    posts.SaveTrace();
                    if (horizentry(xstep, ystep, xinttemp, ref pwallposnorm, ref pwallposinv, ref pwallposi))
                    {
                        if (!posts.TracePastWall(render.savedxinttile, render.savedytile))
                            break;
                        posts.RestoreTrace();
                        passhoriz(xstep);
                    }
                }

                //
                // check intersections with horizontal walls
                //
                if ((render.xtile - render.xtilestep) == render.xinttile && (render.ytile - render.ytilestep) == render.yinttile)
                    render.xinttile = render.xtile;

                if ((render.xtilestep == -1 && render.xinttile <= render.xtile) || (render.xtilestep == 1 && render.xinttile >= render.xtile))
                {
                    posts.SaveTrace();
                    if (vertentry(ystep, xstep, yinttemp, ref pwallposnorm, ref pwallposinv, ref pwallposi))
                    {
                        if (!posts.TracePastWall(render.savedxtile, render.savedyinttile))
                            break;
                        posts.RestoreTrace();
                        passvert(ystep);
                    }
                }
            }

            posts.DrawPosts();
        }
    }

    bool vertentry(int ystep, int xstep,int yinttemp, ref int pwallposnorm, ref int pwallposinv, ref int pwallposi)
    {
        // the pushwall cases below move yinttile, so keep the tile the trace entered for the automap
        int hitx = render.xtile, hity = render.yinttile;
        posts.CloseArch(posts.VertEdge, render.yintercept);
        posts.EnterTileLight(render.xtile - render.xtilestep, render.yinttile);
        render.tilehit = map.tilemap[render.xtile, render.yinttile];
        render.hitpushwall = render.tilehit == GameConstants.BitWall;
        if (render.hitpushwall)
            pushWalls.SelectPushWall(hitx, hity);     // which of the moving pushwalls this is
        render.hitstories =render.tilehit == GameConstants.BitWall ? map.WallStories(pushWalls.pwall.X, pushWalls.pwall.Y) : map.WallStories(hitx, hity);

        if (render.tilehit != 0)
        {
            if ((render.tilehit & GameConstants.BitDoor) != 0)
            {
                //
                // hit a vertical door, so find which coordinate the door would be
                // intersected at, and check to see if the door is open past that point
                //
                var door = doors.doorobjlist[render.tilehit & ~GameConstants.BitDoor];
                yinttemp = render.yintercept + (ystep >> 1);    // add halfstep to current intercept position
                int planex = (int)((render.xtile << (int)MapConstants.TileShift) + (MapConstants.TileGlobal / 2));

                if (door.action == DoorAction.Open)
                {
                    if (yinttemp >> MapConstants.TileShift == render.yinttile)
                        hits.NoteLintel(door, planex, yinttemp);
                    passvert(ystep); // door is open, continue tracing
                    return false;
                }

                //
                // midpoint is outside tile, so it hit the side of the wall before a door
                //
                if (yinttemp >> MapConstants.TileShift != render.yinttile)
                {
                    passvert(ystep);
                    return false;
                }

                if (door.action != DoorAction.Closed)
                {
                    //
                    // the trace hit the door plane at pixel position yintercept, see if the door is
                    // closed that much
                    //
                    if (door.OpenAt(yinttemp))
                    {
                        hits.NoteLintel(door, planex, yinttemp);
                        passvert(ystep);
                        return false;
                    }
                }

                render.yintercept = yinttemp;
                render.xintercept = planex;

                hits.HitVertDoor();
            }
            else if (render.tilehit == GameConstants.BitWall)
            {
                //
                // hit a sliding vertical wall
                //
                if (pushWalls.pwall.Dir == ControlDir.West || pushWalls.pwall.Dir == ControlDir.East)
                {
                    if (pushWalls.pwall.Dir == ControlDir.West)
                    {
                        pwallposnorm = 64 - pushWalls.pwall.Pos;
                        pwallposinv = pushWalls.pwall.Pos;
                    }
                    else
                    {
                        pwallposnorm = pushWalls.pwall.Pos;
                        pwallposinv = 64 - pushWalls.pwall.Pos;
                    }

                    if ((pushWalls.pwall.Dir == ControlDir.East && render.xtile == pushWalls.pwall.X && render.yinttile == pushWalls.pwall.Y)
                     || (pushWalls.pwall.Dir == ControlDir.West && !(render.xtile == pushWalls.pwall.X && render.yinttile == pushWalls.pwall.Y)))
                    {
                        yinttemp = render.yintercept + ((ystep * pwallposnorm) >> 6);

                        if (yinttemp >> MapConstants.TileShift != render.yinttile)
                        {
                            passvert(ystep);
                            return false;
                        }

                        render.yintercept = yinttemp;
                        render.xintercept = (int)(((render.xtile << MapConstants.TileShift) + MapConstants.TileGlobal) - (pwallposinv << 10));
                        render.yinttile = render.yintercept >> MapConstants.TileShift;
                        render.tilehit = pushWalls.pwall.Tile;

                        hits.HitVertWall();
                    }
                    else
                    {
                        yinttemp = render.yintercept + ((ystep * pwallposinv) >> 6);

                        if (yinttemp >> MapConstants.TileShift != render.yinttile)
                        {
                            passvert(ystep);
                            return false;
                        }

                        render.yintercept = yinttemp;
                        render.xintercept = (render.xtile << MapConstants.TileShift) -(pwallposinv << 10);
                        render.yinttile = render.yintercept >> MapConstants.TileShift;
                        render.tilehit = pushWalls.pwall.Tile;

                        hits.HitVertWall();
                    }
                }
                else
                {
                    if (pushWalls.pwall.Dir == ControlDir.North)
                        pwallposi = 64 - pushWalls.pwall.Pos;
                    else
                        pwallposi = pushWalls.pwall.Pos;

                    if ((pushWalls.pwall.Dir == ControlDir.South && (ushort)render.yintercept < (pwallposi << 10))
                     || (pushWalls.pwall.Dir == ControlDir.North && (ushort)render.yintercept > (pwallposi << 10)))
                    {
                        if (render.xtile == pushWalls.pwall.X && render.yinttile == pushWalls.pwall.Y)
                        {
                            if ((pushWalls.pwall.Dir == ControlDir.South && (int)((ushort)render.yintercept) + ystep < (pwallposi << 10))
                             || (pushWalls.pwall.Dir == ControlDir.North && (int)((ushort)render.yintercept) + ystep > (pwallposi << 10)))
                            {
                                //goto passvert;
                                passvert(ystep);
                                return false;
                            }

                            //
                            // set up a horizontal intercept position
                            //
                            if (pushWalls.pwall.Dir == ControlDir.South)
                                render.yintercept = (render.yinttile << MapConstants.TileShift) + (pwallposi << 10);
                            else
                                render.yintercept = (int)(((render.yinttile << MapConstants.TileShift) - MapConstants.TileGlobal) + (pwallposi << 10));

                            render.xintercept -= (xstep * (64 - pushWalls.pwall.Pos)) >> 6;
                            render.xinttile = render.xintercept >> MapConstants.TileShift;
                            render.tilehit = pushWalls.pwall.Tile;

                            hits.HitHorizWall();
                        }
                        else
                        {
                            render.texdelta = (ushort)(pwallposi << 10);
                            render.xintercept = render.xtile << MapConstants.TileShift;
                            render.tilehit = pushWalls.pwall.Tile;

                            hits.HitVertWall();
                        }
                    }
                    else
                    {
                        if (render.xtile == pushWalls.pwall.X && render.yinttile == pushWalls.pwall.Y)
                        {
                            render.texdelta = (ushort)(pwallposi << 10);
                            render.xintercept = render.xtile << MapConstants.TileShift;
                            render.tilehit = pushWalls.pwall.Tile;

                            hits.HitVertWall();
                        }
                        else
                        {
                            if ((pushWalls.pwall.Dir == ControlDir.South && ((ushort)render.yintercept) + ystep > (pwallposi << 10))
                             || (pushWalls.pwall.Dir == ControlDir.North && ((ushort)render.yintercept) + ystep < (pwallposi << 10)))
                            {
                                //goto passvert;
                                passvert(ystep);
                                return false;
                            }

                            //
                            // set up a horizontal intercept position
                            //
                            if (pushWalls.pwall.Dir == ControlDir.South)
                                render.yintercept = (render.yinttile << MapConstants.TileShift) - ((64 - pushWalls.pwall.Pos) << 10);
                            else
                                render.yintercept = (render.yinttile << MapConstants.TileShift) + ((64 - pushWalls.pwall.Pos) << 10);

                            render.xintercept -= (xstep * pushWalls.pwall.Pos) >> 6;
                            render.xinttile = render.xintercept >> MapConstants.TileShift;
                            render.tilehit = pushWalls.pwall.Tile;

                            hits.HitHorizWall();
                        }
                    }
                }
            }
            else
            {
                var shape = map.wallshape[render.xtile, render.yinttile];
                if (shape != WallShape.Square
                    && !hits.IsSolidEdge(shape, render.xtilestep == 1 ? ControlDir.West : ControlDir.East))
                {
                    //
                    // entered a diagonal by an open edge: hit its face, or cross the open half
                    //
                    long eu = render.xtilestep == 1 ? 0 : MapConstants.TileGlobal;
                    long ev = render.yintercept - (render.yinttile << MapConstants.TileShift);
                    if (!hits.TraceDiagonal(shape, render.xtile, render.yinttile, eu, ev, render.xtilestep * MapConstants.TileGlobal, ystep))
                    {
                        passvert(ystep);
                        return false;
                    }

                    hits.HitDiagWall(shape, render.xtile, render.yinttile);
                    MarkSeen(hitx, hity, SeenFlags.DiagonalFace);
                    return true;
                }
                else
                {
                    render.xintercept = (render.xtile << MapConstants.TileShift);

                    hits.HitVertWall();
                }
            }

            MarkSeen(hitx, hity, render.xtilestep == 1 ? SeenFlags.WestFace : SeenFlags.EastFace);
            return true;
        }

        //
        // mark the tile as visible and setup for next step
        //
        if (posts.IsArch(render.xtile, render.yinttile))
            posts.OpenArch(render.xtile, render.yinttile, posts.VertEdge, render.yintercept, true);
        passvert(ystep);
        return false;
    }

    internal void passvert(int ystep)
    {
        if (!render.pastwall)
            map.spotvis[render.xtile, render.yinttile] = true;
        render.xtile += render.xtilestep;
        render.yintercept += ystep;
        render.yinttile = render.yintercept >> (int)MapConstants.TileShift;
    }

    bool horizentry(int xstep,int ystep, int xinttemp, ref int pwallposnorm, ref int pwallposinv, ref int pwallposi)
    {
        // the pushwall cases below move xinttile, so keep the tile the trace entered for the automap
        int hitx = render.xinttile, hity = render.ytile;
        posts.CloseArch(render.xintercept, posts.HorizEdge);
        posts.EnterTileLight(render.xinttile, render.ytile - render.ytilestep);
        render.tilehit = map.tilemap[render.xinttile, render.ytile];
        render.hitpushwall = render.tilehit == GameConstants.BitWall;
        if (render.hitpushwall)
            pushWalls.SelectPushWall(hitx, hity);     // which of the moving pushwalls this is
        render.hitstories =render.tilehit == GameConstants.BitWall ? map.WallStories(pushWalls.pwall.X, pushWalls.pwall.Y) : map.WallStories(hitx, hity);

        if (render.tilehit != 0)
        {
            if ((render.tilehit & GameConstants.BitDoor) != 0)
            {
                //
                // hit a horizontal door, so find which coordinate the door would be
                // intersected at, and check to see if the door is open past that point
                //
                var door = doors.doorobjlist[render.tilehit & ~GameConstants.BitDoor];
                xinttemp = render.xintercept + (xstep >> 1);    // add half step to current intercept position
                int planey = (int)((render.ytile << (int)MapConstants.TileShift) + (MapConstants.TileGlobal / 2));

                if (door.action == (byte)DoorAction.Open)
                {
                    if ((xinttemp >> MapConstants.TileShift) == render.xinttile)
                        hits.NoteLintel(door, xinttemp, planey);
                    passhoriz(xstep); // door is open, continue tracing
                    return false;
                }

                //
                // midpoint is outside tile, so it hit the side of the wall before a door
                //
                if ((xinttemp >> MapConstants.TileShift) != render.xinttile)
                {
                    passhoriz(xstep);
                    return false;
                }

                if (door.action != DoorAction.Closed)
                {
                    //
                    // the trace hit the door plane at pixel position xintercept, see if the door is
                    // closed that much
                    //
                    if (door.OpenAt(xinttemp))
                    {
                        hits.NoteLintel(door, xinttemp, planey);
                        passhoriz(xstep);
                        return false;
                    }
                }

                render.xintercept = xinttemp;
                render.yintercept = planey;

                hits.HitHorizDoor();
            }
            else if (render.tilehit == GameConstants.BitWall)
            {
                //
                // hit a sliding horizontal wall
                //
                if (pushWalls.pwall.Dir == ControlDir.North || pushWalls.pwall.Dir == ControlDir.South)
                {
                    if (pushWalls.pwall.Dir == ControlDir.North)
                    {
                        pwallposnorm = 64 - pushWalls.pwall.Pos;
                        pwallposinv = pushWalls.pwall.Pos;
                    }
                    else
                    {
                        pwallposnorm = pushWalls.pwall.Pos;
                        pwallposinv = 64 - pushWalls.pwall.Pos;
                    }

                    if ((pushWalls.pwall.Dir == ControlDir.South && render.xinttile == pushWalls.pwall.X && render.ytile == pushWalls.pwall.Y)
                     || (pushWalls.pwall.Dir == ControlDir.North && !(render.xinttile == pushWalls.pwall.X && render.ytile == pushWalls.pwall.Y)))
                    {
                        xinttemp = render.xintercept + ((xstep * pwallposnorm) >> 6);

                        if (xinttemp >> MapConstants.TileShift != render.xinttile)
                        {
                            passhoriz(xstep);
                            return false;
                        }

                        render.xintercept = xinttemp;
                        render.yintercept = (int)(((render.ytile << MapConstants.TileShift) +MapConstants.TileGlobal) -(pwallposinv << 10));
                        render.xinttile = render.xintercept >> MapConstants.TileShift;
                        render.tilehit = pushWalls.pwall.Tile;

                        hits.HitHorizWall();
                    }
                    else
                    {
                        xinttemp = render.xintercept + ((xstep * pwallposinv) >> 6);

                        if (xinttemp >> MapConstants.TileShift != render.xinttile)
                        {
                            passhoriz(xstep);
                            return false;
                        }

                        render.xintercept = xinttemp;
                        render.yintercept = (render.ytile << MapConstants.TileShift) -(pwallposinv << 10);
                        render.xinttile = render.xintercept >> MapConstants.TileShift;
                        render.tilehit = pushWalls.pwall.Tile;

                        hits.HitHorizWall();
                    }
                }
                else
                {
                    if (pushWalls.pwall.Dir == ControlDir.West)
                        pwallposi = 64 - pushWalls.pwall.Pos;
                    else
                        pwallposi = pushWalls.pwall.Pos;

                    if ((pushWalls.pwall.Dir == ControlDir.East && (ushort)render.xintercept < (pwallposi << 10))
                     || (pushWalls.pwall.Dir == ControlDir.West && (ushort)render.xintercept > (pwallposi << 10)))
                    {
                        if (render.xinttile == pushWalls.pwall.X && render.ytile == pushWalls.pwall.Y)
                        {
                            if ((pushWalls.pwall.Dir == ControlDir.East && (render.xintercept) + xstep < (pwallposi << 10))
                             || (pushWalls.pwall.Dir == ControlDir.West && (render.xintercept) + xstep > (pwallposi << 10)))

                            {
                                passhoriz(xstep);
                                return false;
                            }

                            //
                            // set up a vertical intercept position
                            //
                            render.yintercept -= (ystep * (64 - pushWalls.pwall.Pos)) >> 6;
                            render.yinttile = render.yintercept >> MapConstants.TileShift;

                            if (pushWalls.pwall.Dir == ControlDir.East)
                                render.xintercept = (render.xinttile << MapConstants.TileShift) + (pwallposi << 10);
                            else
                                render.xintercept = (int)(((render.xinttile << MapConstants.TileShift) - MapConstants.TileGlobal) + (pwallposi << 10));

                            render.tilehit = pushWalls.pwall.Tile;

                            hits.HitVertWall();
                        }
                        else
                        {
                            render.texdelta = (ushort)(pwallposi << 10);
                            render.yintercept = render.ytile << MapConstants.TileShift;
                            render.tilehit = pushWalls.pwall.Tile;

                            hits.HitHorizWall();
                        }
                    }
                    else
                    {
                        if (render.xinttile == pushWalls.pwall.X && render.ytile == pushWalls.pwall.Y)
                        {
                            render.texdelta = (ushort)(pwallposi << 10);
                            render.yintercept = render.ytile << MapConstants.TileShift;
                            render.tilehit = pushWalls.pwall.Tile;

                            hits.HitHorizWall();
                        }
                        else
                        {
                            if ((pushWalls.pwall.Dir == ControlDir.East && (render.xintercept) + xstep > (pwallposi << 10))
                             || (pushWalls.pwall.Dir == ControlDir.West && (render.xintercept) + xstep < (pwallposi << 10)))

                            {
                                passhoriz(xstep);
                                return false;
                            }

                            //
                            // set up a vertical intercept position
                            //
                            render.yintercept -= (ystep * pushWalls.pwall.Pos) >> 6;
                            render.yinttile = render.yintercept >> MapConstants.TileShift;

                            if (pushWalls.pwall.Dir == ControlDir.East)
                                render.xintercept = (render.xinttile << MapConstants.TileShift) - ((64 - pushWalls.pwall.Pos) << 10);
                            else
                                render.xintercept = (render.xinttile << MapConstants.TileShift) + ((64 - pushWalls.pwall.Pos) << 10);

                            render.tilehit = pushWalls.pwall.Tile;

                            hits.HitVertWall();
                        }
                    }
                }
            }
            else
            {
                var shape = map.wallshape[render.xinttile, render.ytile];
                if (shape != WallShape.Square
                    && !hits.IsSolidEdge(shape, render.ytilestep == 1 ? ControlDir.North : ControlDir.South))
                {
                    //
                    // entered a diagonal by an open edge: hit its face, or cross the open half
                    //
                    long eu = render.xintercept - (render.xinttile << MapConstants.TileShift);
                    long ev = render.ytilestep == 1 ? 0 : MapConstants.TileGlobal;
                    if (!hits.TraceDiagonal(shape, render.xinttile, render.ytile, eu, ev, xstep, render.ytilestep * MapConstants.TileGlobal))
                    {
                        passhoriz(xstep);
                        return false;
                    }

                    hits.HitDiagWall(shape, render.xinttile, render.ytile);
                    MarkSeen(hitx, hity, SeenFlags.DiagonalFace);
                    return true;
                }
                else
                {
                    render.yintercept = render.ytile << MapConstants.TileShift;

                    hits.HitHorizWall();
                }
            }

            MarkSeen(hitx, hity, render.ytilestep == 1 ? SeenFlags.NorthFace : SeenFlags.SouthFace);
            return true;
        }

        //
        // mark the tile as visible and setup for next step
        //
        if (posts.IsArch(render.xinttile, render.ytile))
            posts.OpenArch(render.xinttile, render.ytile, render.xintercept, posts.HorizEdge, false);
        passhoriz(xstep);
        return false;
    }

    internal void passhoriz(int xstep)
    {
        if (!render.pastwall)
            map.spotvis[render.xinttile, render.ytile] = true;
        render.ytile += render.ytilestep;
        render.xintercept += xstep;
        render.xinttile = render.xintercept >> (int)MapConstants.TileShift;
    }

    /// <summary>Marks a wall face the rays hit as seen, for the automap, when the view is the player's</summary>
    internal void MarkSeen(int x, int y, SeenFlags face)
    {
        if (render.markseen)
            map.seen[x, y] |= face;
    }
}