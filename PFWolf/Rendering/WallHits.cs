using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Rendering;

/// <summary>
/// What the ray trace does when it hits something: a wall (vertical or horizontal), a door, a
/// diagonal wall or a moving pushwall. Each works out the texture and its column there, its
/// stories, any patch or lintel over it and the face's light, and queues the post (WallPosts).
/// </summary>
internal sealed class WallHits(
    AssetManager assets,
    MapManager map,
    Doors doors,
    PushWalls pushWalls,
    WallPosts posts,
    RenderState render,
    GameSession session)
{
    /*
    ====================
    =
    = HitVertWall
    =
    = tilehit bit 7 is 0, because it's not a door tile
    = if bit 6 is 1 and the adjacent tile is a door tile, use door side pic
    =
    ====================
    */

    internal void HitVertWall()
    {
        string wallpic;
        int texture;

        texture = ((render.yintercept - render.texdelta) >> Program.FIXED2TEXSHIFT) & Program.TEXTUREMASK;

        if (render.xtilestep == -1)
        {
            texture = Program.TEXTUREMASK - texture;
            render.xintercept += (int)MapConstants.TILEGLOBAL;
        }
        render.postheight = posts.CalcHeight();
        render.postx = render.pixx;
        var mapDefs = map.GetMapData();
        var face = posts.SeenFace(vertical: true);
        bool jamb = false;
        MapTextureTranslation? mapTexture = MapTextureTranslation.None;
        if ((render.tilehit & Program.BIT_WALL) != 0)
        {
            //
            // check for adjacent doors
            //
            var doortile = map.tilemap[render.xtile - render.xtilestep, render.yinttile];
            mapDefs?.Walls.TryGetValue((render.tilehit & ~Program.BIT_WALL), out mapTexture);
            if ((doortile & Program.BIT_DOOR) != 0)
            {
                // the door's jamb on the side the ray left it from
                var door = doors.doorobjlist[doortile & ~Program.BIT_DOOR];
                wallpic = Doors.DoorFace(door, render.xtilestep == 1 ? "east" : "west");
                SetUpperPost((mapTexture ?? MapTextureTranslation.None).WallFace(face),
                    FlipColumn(mapTexture, face, texture), render.xtile, render.yinttile);   // the wall above the door frame
                jamb = true;
            }
            else
            {
                wallpic = (mapTexture ?? MapTextureTranslation.None).WallFace(face);
            }
        }
        else
        {
            mapDefs?.Walls.TryGetValue(render.tilehit, out mapTexture);
            wallpic = (mapTexture ?? MapTextureTranslation.None).WallFace(face);
        }

        var textureAsset = WorldTexture(wallpic, render.xtile, render.yinttile);
        if (textureAsset == null)
        {
            render.uppersource = null;
            return;
        }
        render.postsource = textureAsset;
        render.postofs = jamb ? texture : FlipColumn(mapTexture, face, texture);
        if (!jamb)                      // a door's jamb takes no patch
            SetPatchPost(render.xtile, render.yinttile, face, texture);
        else
            SeeThroughJamb(mapTexture, face, texture, render.xtile, render.yinttile);
        posts.ScalePost();
    }

    /// <summary>
    /// A door jamb with clear pixels (a masked texture from a pk3) is drawn over the face of the
    /// wall it's on, as a patch is, so the wall shows through it. Call with the jamb as postsource.
    /// </summary>
    internal void SeeThroughJamb(MapTextureTranslation? mapTexture, string face, int texture, int tilex, int tiley)
    {
        if (!render.postsource.Masked)
            return;
        var wall = WorldTexture((mapTexture ?? MapTextureTranslation.None).WallFace(face), tilex, tiley);
        if (wall == null)
            return;                     // nothing behind it: the jamb as it is

        render.patchsource = render.postsource;
        render.patchofs = texture;
        render.patchstory = 1;
        render.postsource = wall;
        render.postofs = FlipColumn(mapTexture, face, texture);
    }

    // A wall face's texture column, mirrored when the wall flips that face (mapdefs walls flip)
    internal int FlipColumn(MapTextureTranslation? mapTexture, string face, int texture) =>
        mapTexture != null && mapTexture.IsFlipped(face) ? Program.TEXTUREMASK - texture : texture;

    /*
    ====================
    =
    = HitHorizWall
    =
    = tilehit bit 7 is 0, because it's not a door tile
    = if bit 6 is 1 and the adjacent tile is a door tile, use door side pic
    =
    ====================
    */

    internal void HitHorizWall()
    {
        string wallpic;
        int texture;

        texture = ((render.xintercept - render.texdelta) >> Program.FIXED2TEXSHIFT) & Program.TEXTUREMASK;

        if (render.ytilestep == -1)
            render.yintercept += (int)MapConstants.TILEGLOBAL;
        else
            texture = Program.TEXTUREMASK - texture;

        render.postheight = posts.CalcHeight();
        render.postx = render.pixx;

        var mapDefs = map.GetMapData();
        var face = posts.SeenFace(vertical: false);
        bool jamb = false;
        MapTextureTranslation? mapTexture = MapTextureTranslation.None;

        if ((render.tilehit & Program.BIT_WALL) != 0)
        {
            //
            // check for adjacent doors
            //
            var doortile = map.tilemap[render.xinttile, render.ytile - render.ytilestep];
            mapDefs?.Walls.TryGetValue((render.tilehit & ~Program.BIT_WALL), out mapTexture);
            if ((doortile & Program.BIT_DOOR) != 0)
            {
                var door = doors.doorobjlist[doortile & ~Program.BIT_DOOR];
                wallpic = Doors.DoorFace(door, render.ytilestep == 1 ? "south" : "north");
                SetUpperPost((mapTexture ?? MapTextureTranslation.None).WallFace(face),
                    FlipColumn(mapTexture, face, texture), render.xinttile, render.ytile);  // the wall above the door frame
                jamb = true;
            }
            else
            {
                wallpic = (mapTexture ?? MapTextureTranslation.None).WallFace(face);
            }
        }
        else
        {
            mapDefs?.Walls.TryGetValue(render.tilehit, out mapTexture);
            wallpic = (mapTexture ?? MapTextureTranslation.None).WallFace(face);
        }

        var textureAsset = WorldTexture(wallpic, render.xinttile, render.ytile);
        if (textureAsset == null)
        {
            render.uppersource = null;
            return;
        }

        render.postsource = textureAsset;
        render.postofs = jamb ? texture : FlipColumn(mapTexture, face, texture);
        if (!jamb)                      // a door's jamb takes no patch
            SetPatchPost(render.xinttile, render.ytile, face, texture);
        else
            SeeThroughJamb(mapTexture, face, texture, render.xinttile, render.ytile);
        posts.ScalePost();
    }

    internal void HitVertDoor()
    {
        string doorpage = "";
        int doornumtile;
        int texture;

        doornumtile = render.tilehit & ~Program.BIT_DOOR;
        texture = (doors.doorobjlist[doornumtile].TextureAlong(render.yintercept) >> Program.FIXED2TEXSHIFT) & Program.TEXTUREMASK;

        render.postheight = posts.CalcHeight();
        render.postx = render.pixx;

        // the face on the viewer's side: heading east, the ray meets the door's west face
        var door = doors.doorobjlist[doornumtile];
        doorpage = Doors.DoorFace(door, render.xtilestep == 1 ? "west" : "east");

        var doorTextureAsset = WorldTexture(doorpage, door.tilex, door.tiley);
        if (doorTextureAsset == null)
            return;
        render.postsource = doorTextureAsset;
        render.postofs = texture;
        SetLintelPost(door, render.yintercept, render.xtilestep == -1);

        posts.ScalePost();
    }

    internal void HitHorizDoor()
    {
        string doorpage = "";
        int doornumtile;
        int texture;

        doornumtile = render.tilehit & ~Program.BIT_DOOR;
        texture = (doors.doorobjlist[doornumtile].TextureAlong(render.xintercept) >> Program.FIXED2TEXSHIFT) & Program.TEXTUREMASK;

        render.postheight = posts.CalcHeight();
        render.postx = render.pixx;

        // heading south, the ray meets the door's north face
        var door = doors.doorobjlist[doornumtile];
        doorpage = Doors.DoorFace(door, render.ytilestep == 1 ? "north" : "south");
        var doorTextureAsset = WorldTexture(doorpage, door.tilex, door.tiley);
        if (doorTextureAsset == null)
            return;
        render.postsource = doorTextureAsset;
        render.postofs = texture;
        SetLintelPost(door, render.xintercept, render.ytilestep == 1);

        posts.ScalePost();
    }

    /*
    ====================
    =
    = Tall walls
    =
    = Above the first story, a door is capped by a lintel on the door plane, as tall as the
    = door tile and textured like the wall the door is set into. It is there whether the door
    = is shut or not: the trace queues an open door's lintel as it passes through.
    =
    ====================
    */

    /// <summary>
    /// A wall, door or sky texture as it shows now: an animation (animdefs) on the frame the
    /// level's clock is at, so every tile showing it is in step, and it stops while the game does
    /// </summary>
    internal TextureAsset? WorldTexture(string name) => assets.Find<TextureAsset>(name, session.gamestate.TimeCount);

    /// <summary>
    /// A texture on a tile's wall or door: in place of any A_ChangeTexture swapped on the tile, and
    /// an animation that plays once from when the tile last changed (a looping one in step with
    /// every other tile)
    /// </summary>
    internal TextureAsset? WorldTexture(string name, int tilex, int tiley)
        => assets.Find<TextureAsset>(map.ChangedTexture(tilex, tiley, name), session.gamestate.TimeCount,
            map.animstart[(tiley << MapManager.MAPSHIFT) + tilex]);


    /// <summary>
    /// Sets the patch (mapdefs patches) on the face the trace sees of the wall tile it hit, for
    /// the post being drawn: none if the tile has none or it isn't on that face. A moving
    /// pushwall's patch is on its own tile, whatever tile the trace is in. ofs is the face's
    /// column before any flip, so a patch reads the right way round on a flipped face.
    /// </summary>
    internal void SetPatchPost(int tilex, int tiley, string face, int ofs)
    {
        render.patchofs = ofs;
        render.patchsource = null;
        if (render.hitpushwall)
            (tilex, tiley) = (pushWalls.pwall.X, pushWalls.pwall.Y);

        int id = map.GetPatch(tilex, tiley);
        if (id == 0 || map.GetMapData()?.Patches.TryGetValue(id, out var patch) != true || !patch!.IsOn(face))
            return;
        render.patchsource = WorldTexture(patch.Texture, tilex, tiley);
        render.patchstory = patch.Story;
    }

    // Points the stories above the first at the named wall texture, for the post being drawn
    internal void SetUpperPost(string wallpic, int texture, int tilex, int tiley)
    {
        if (render.hitstories == 1)
            return;
        render.uppersource = WorldTexture(wallpic, tilex, tiley);
        render.upperofs = texture;
    }

    /// <summary>
    /// The face ("north", "south", "east" or "west") the trace sees on what it hits: vertical for
    /// an edge along x = constant. Heading east, it meets a wall's west face, and so on.
    /// </summary>

    /// <summary>
    /// The texture of the wall a door is set into, from the wall tile on either side of it in
    /// line with the door, on the face the trace sees; null if neither side is a plain wall.
    /// mirrored: that wall flips the face (mapdefs walls flip).
    /// </summary>
    internal TextureAsset? LintelTexture(doorobj_t door, out bool mirrored)
    {
        int dx = door.vertical ? 0 : 1, dy = door.vertical ? 1 : 0;
        var face = posts.SeenFace(door.vertical);
        return WallTexture(door.tilex - dx, door.tiley - dy, face, out mirrored)
            ?? WallTexture(door.tilex + dx, door.tiley + dy, face, out mirrored);
    }

    // A wall tile's texture on face ("north", "south", "east" or "west"), and whether it flips it
    internal TextureAsset? WallTexture(int tilex, int tiley, string face, out bool mirrored)
    {
        mirrored = false;
        var tile = map.tilemap[tilex, tiley];
        if ((tile & Program.BIT_DOOR) != 0 || (tile & ~Program.BIT_WALL) == 0)
            return null;                // open floor, a door or a pushwall

        MapTextureTranslation? mapTexture = null;
        if (map.GetMapData()?.Walls.TryGetValue(tile & ~Program.BIT_WALL, out mapTexture) != true || mapTexture == null)
            return null;
        mirrored = mapTexture.IsFlipped(face);
        return WorldTexture(mapTexture.WallFace(face), tilex, tiley);
    }

    // The lintel column where the trace meets the door plane at along (y for a vertical door, x
    // for a horizontal one); flip mirrors it like the wall faces seen from the same side

    // Caps the door post being drawn with its lintel (the door texture again if it has no wall)
    internal void SetLintelPost(doorobj_t door, int along, bool flip)
    {
        if (render.hitstories == 1)
            return;
        var lintel = LintelTexture(door, out bool mirrored);
        render.uppersource = lintel ?? render.postsource;
        render.upperofs = WallPosts.LintelColumn(along, lintel != null && mirrored ? !flip : flip);
    }

    /// <summary>
    /// Notes the lintel of the door the trace is passing through, meeting its plane at
    /// (planex, planey), in the column's queue. Nothing to do for a one story door.
    /// </summary>
    internal void NoteLintel(doorobj_t door, int planex, int planey)
    {
        int stories = map.WallStories(door.tilex, door.tiley);
        if (stories == 1)
            return;

        var texture = LintelTexture(door, out bool mirrored);
        if (texture == null)
            return;                     // set into nothing: leave the view above it open

        short height = posts.HeightAt(planex, planey);
        int ofs = door.vertical
            ? WallPosts.LintelColumn(planey, (render.xtilestep == -1) != mirrored)
            : WallPosts.LintelColumn(planex, (render.ytilestep == 1) != mirrored);
        posts.QueuePost(height, 1, stories, texture, ofs, texture, ofs, light: posts.WallLight(planex, planey));
    }

    /// <summary>
    /// When the view starts inside a door tile, the trace never enters it, so check whether this
    /// column meets the door plane ahead of the view point.
    /// </summary>
    internal void NoteFocalLintel(int xstep, int ystep)
    {
        var door = doors.doorobjlist[map.tilemap[render.focaltx, render.focalty] & ~Program.BIT_DOOR];
        const int HALFTILE = (int)MapConstants.TILEGLOBAL / 2;

        if (door.vertical)
        {
            int planex = (render.focaltx << MapConstants.TILESHIFT) + HALFTILE;
            int dist = (planex - render.viewx) * render.xtilestep;
            if (dist <= 0)
                return;                 // the plane is behind the view
            int planey = render.viewy + MathUtils.FixedMul(ystep, dist);
            if (planey >> MapConstants.TILESHIFT == render.focalty)
                NoteLintel(door, planex, planey);
        }
        else
        {
            int planey = (render.focalty << MapConstants.TILESHIFT) + HALFTILE;
            int dist = (planey - render.viewy) * render.ytilestep;
            if (dist <= 0)
                return;
            int planex = render.viewx + MathUtils.FixedMul(xstep, dist);
            if (planex >> MapConstants.TILESHIFT == render.focaltx)
                NoteLintel(door, planex, planey);
        }
    }

    /*
    ====================
    =
    = Diagonal walls
    =
    = A diagonal tile (MapManager.wallshape) is solid on the two edges at its named corner, with
    = a 45 degree face between the other two corners. A trace that enters through a solid edge
    = hits it like a square wall. One that enters through an open edge either meets the face or
    = crosses the open half and carries on to the next tile.
    =
    ====================
    */


    // Whether the tile edge a trace enters by is one of the diagonal's two solid edges
    internal bool IsSolidEdge(WallShape shape, controldirs edge) => edge switch
    {
        controldirs.di_west => shape is WallShape.SolidNW or WallShape.SolidSW,
        controldirs.di_east => shape is WallShape.SolidNE or WallShape.SolidSE,
        controldirs.di_north => shape is WallShape.SolidNW or WallShape.SolidNE,
        _ => shape is WallShape.SolidSW or WallShape.SolidSE,
    };

    /// <summary>
    /// Where a trace from tile-local (eu, ev), heading (du, dv), meets the diagonal face of the
    /// tile at (tilex, tiley). All in 16.16 fixed point. On a hit, sets xintercept/yintercept and
    /// diaghitu; false means it misses the face within the tile (it crosses the open half).
    /// </summary>
    internal bool TraceDiagonal(WallShape shape, int tilex, int tiley, long eu, long ev, long du, long dv)
    {
        const long TILE = MapConstants.TILEGLOBAL;
        long num, den;

        if (shape is WallShape.SolidNE or WallShape.SolidSW)
        {
            num = ev - eu;              // face runs NW to SE: u == v
            den = du - dv;
        }
        else
        {
            num = TILE - eu - ev;       // face runs NE to SW: u + v == TILE
            den = du + dv;
        }

        if (den == 0)
            return false;               // parallel to the face
        if (num != 0 && (num < 0) != (den < 0))
            return false;               // the face is behind the trace

        long hu = eu + num * du / den;
        long hv = ev + num * dv / den;
        if (hu < 0 || hu > TILE || hv < 0 || hv > TILE)
            return false;

        render.xintercept = (tilex << MapConstants.TILESHIFT) + (int)hu;
        render.yintercept = (tiley << MapConstants.TILESHIFT) + (int)hv;
        render.diaghitu = (int)hu;
        return true;
    }

    internal void HitDiagWall(WallShape shape, int tilex, int tiley)
    {
        //
        // seen from the open side, the face runs left to right along +u for SolidNW/SolidNE,
        // and along -u for SolidSW/SolidSE
        //
        int along = shape is WallShape.SolidSW or WallShape.SolidSE
            ? (int)MapConstants.TILEGLOBAL - 1 - render.diaghitu
            : render.diaghitu;
        along = Math.Clamp(along, 0, (int)MapConstants.TILEGLOBAL - 1);
        int texture = (along >> Program.FIXED2TEXSHIFT) & Program.TEXTUREMASK;

        render.postheight = posts.CalcHeight();
        render.postx = render.pixx;

        var wallpic = map.GetDiagonal(tilex, tiley)?.Texture;
        if (string.IsNullOrEmpty(wallpic))
        {
            MapTextureTranslation? mapTexture = MapTextureTranslation.None;
            map.GetMapData()?.Walls.TryGetValue(render.tilehit & ~Program.BIT_WALL, out mapTexture);
            wallpic = (mapTexture ?? MapTextureTranslation.None).North;
        }

        var textureAsset = WorldTexture(wallpic, tilex, tiley);
        if (textureAsset == null)
            return;
        render.postsource = textureAsset;
        render.postofs = texture;
        posts.ScalePost();
    }
}
