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
    = TileHit bit 7 is 0, because it's not a door tile
    = if bit 6 is 1 and the adjacent tile is a door tile, use door side pic
    =
    ====================
    */

    internal void HitVertWall()
    {
        string wallpic;
        int texture;

        texture = ((render.YIntercept - render.TexDelta) >> GameConstants.Fixed2TexShift) & GameConstants.TextureMask;

        if (render.XTileStep == -1)
        {
            texture = GameConstants.TextureMask - texture;
            render.XIntercept += (int)MapConstants.TileGlobal;
        }
        render.PostHeight = posts.CalcHeight();
        render.PostX = render.PixX;
        var mapDefs = map.GetMapData();
        var face = posts.SeenFace(vertical: true);
        bool jamb = false;
        MapTextureTranslation? mapTexture = MapTextureTranslation.None;
        if ((render.TileHit & GameConstants.BitWall) != 0)
        {
            //
            // check for adjacent doors
            //
            var doortile = map.TileMap[render.XTile - render.XTileStep, render.YIntTile];
            mapDefs?.Walls.TryGetValue((render.TileHit & ~GameConstants.BitWall), out mapTexture);
            if ((doortile & GameConstants.BitDoor) != 0)
            {
                // the door's jamb on the side the ray left it from
                var door = doors.DoorObjList[doortile & ~GameConstants.BitDoor];
                wallpic = Doors.DoorFace(door, render.XTileStep == 1 ? "east" : "west");
                SetUpperPost((mapTexture ?? MapTextureTranslation.None).WallFace(face),
                    FlipColumn(mapTexture, face, texture), render.XTile, render.YIntTile);   // the wall above the door frame
                jamb = true;
            }
            else
            {
                wallpic = (mapTexture ?? MapTextureTranslation.None).WallFace(face);
            }
        }
        else
        {
            mapDefs?.Walls.TryGetValue(render.TileHit, out mapTexture);
            wallpic = (mapTexture ?? MapTextureTranslation.None).WallFace(face);
        }

        var textureAsset = WorldTexture(wallpic, render.XTile, render.YIntTile);
        if (textureAsset == null)
        {
            render.UpperSource = null;
            return;
        }
        render.PostSource = textureAsset;
        render.PostOfs = jamb ? texture : FlipColumn(mapTexture, face, ScrollWall(render.XTile, render.YIntTile, texture));
        if (!jamb)                      // a door's jamb takes no patch
            SetPatchPost(render.XTile, render.YIntTile, face, texture);
        else
            SeeThroughJamb(mapTexture, face, texture, render.XTile, render.YIntTile);
        posts.ScalePost();
    }

    /// <summary>
    /// A door jamb with clear pixels (a masked texture from a pk3) is drawn over the face of the
    /// wall it's on, as a patch is, so the wall shows through it. Call with the jamb as PostSource.
    /// </summary>
    internal void SeeThroughJamb(MapTextureTranslation? mapTexture, string face, int texture, int tilex, int tiley)
    {
        if (!render.PostSource.Masked)
            return;
        var wall = WorldTexture((mapTexture ?? MapTextureTranslation.None).WallFace(face), tilex, tiley);
        if (wall == null)
            return;                     // nothing behind it: the jamb as it is

        render.PatchSource = render.PostSource;
        render.PatchOfs = texture;
        render.PatchStory = 1;
        render.PostSource = wall;
        render.PostOfs = FlipColumn(mapTexture, face, texture);
    }

    // A wall face's texture column, mirrored when the wall flips that face (mapdefs walls flip)
    internal int FlipColumn(MapTextureTranslation? mapTexture, string face, int texture) =>
        mapTexture != null && mapTexture.IsFlipped(face) ? GameConstants.TextureMask - texture : texture;

    /*
    ====================
    =
    = HitHorizWall
    =
    = TileHit bit 7 is 0, because it's not a door tile
    = if bit 6 is 1 and the adjacent tile is a door tile, use door side pic
    =
    ====================
    */

    internal void HitHorizWall()
    {
        string wallpic;
        int texture;

        texture = ((render.XIntercept - render.TexDelta) >> GameConstants.Fixed2TexShift) & GameConstants.TextureMask;

        if (render.YTileStep == -1)
            render.YIntercept += (int)MapConstants.TileGlobal;
        else
            texture = GameConstants.TextureMask - texture;

        render.PostHeight = posts.CalcHeight();
        render.PostX = render.PixX;

        var mapDefs = map.GetMapData();
        var face = posts.SeenFace(vertical: false);
        bool jamb = false;
        MapTextureTranslation? mapTexture = MapTextureTranslation.None;

        if ((render.TileHit & GameConstants.BitWall) != 0)
        {
            //
            // check for adjacent doors
            //
            var doortile = map.TileMap[render.XIntTile, render.YTile - render.YTileStep];
            mapDefs?.Walls.TryGetValue((render.TileHit & ~GameConstants.BitWall), out mapTexture);
            if ((doortile & GameConstants.BitDoor) != 0)
            {
                var door = doors.DoorObjList[doortile & ~GameConstants.BitDoor];
                wallpic = Doors.DoorFace(door, render.YTileStep == 1 ? "south" : "north");
                SetUpperPost((mapTexture ?? MapTextureTranslation.None).WallFace(face),
                    FlipColumn(mapTexture, face, texture), render.XIntTile, render.YTile);  // the wall above the door frame
                jamb = true;
            }
            else
            {
                wallpic = (mapTexture ?? MapTextureTranslation.None).WallFace(face);
            }
        }
        else
        {
            mapDefs?.Walls.TryGetValue(render.TileHit, out mapTexture);
            wallpic = (mapTexture ?? MapTextureTranslation.None).WallFace(face);
        }

        var textureAsset = WorldTexture(wallpic, render.XIntTile, render.YTile);
        if (textureAsset == null)
        {
            render.UpperSource = null;
            return;
        }

        render.PostSource = textureAsset;
        render.PostOfs = jamb ? texture : FlipColumn(mapTexture, face, ScrollWall(render.XIntTile, render.YTile, texture));
        if (!jamb)                      // a door's jamb takes no patch
            SetPatchPost(render.XIntTile, render.YTile, face, texture);
        else
            SeeThroughJamb(mapTexture, face, texture, render.XIntTile, render.YTile);
        posts.ScalePost();
    }

    // True when the door is seen through (WallPosts.SeesThrough): it's kept for drawing among the
    // sprites, and the trace goes on past it
    internal bool HitVertDoor()
    {
        string doorpage = "";
        int doornumtile;
        int texture;

        doornumtile = render.TileHit & ~GameConstants.BitDoor;
        texture = (doors.DoorObjList[doornumtile].TextureAlong(render.YIntercept) >> GameConstants.Fixed2TexShift) & GameConstants.TextureMask;

        render.PostHeight = posts.CalcHeight();
        render.PostX = render.PixX;

        // the face on the viewer's side: heading east, the ray meets the door's west face
        var door = doors.DoorObjList[doornumtile];
        doorpage = Doors.DoorFace(door, render.XTileStep == 1 ? "west" : "east");

        var doorTextureAsset = WorldTexture(doorpage, door.TileX, door.TileY);
        if (doorTextureAsset == null)
            return false;
        render.PostSource = doorTextureAsset;
        render.PostOfs = texture;
        return DoorPost(door, render.YIntercept, render.XTileStep == -1);
    }

    /// <inheritdoc cref="HitVertDoor"/>
    internal bool HitHorizDoor()
    {
        string doorpage = "";
        int doornumtile;
        int texture;

        doornumtile = render.TileHit & ~GameConstants.BitDoor;
        texture = (doors.DoorObjList[doornumtile].TextureAlong(render.XIntercept) >> GameConstants.Fixed2TexShift) & GameConstants.TextureMask;

        render.PostHeight = posts.CalcHeight();
        render.PostX = render.PixX;

        // heading south, the ray meets the door's north face
        var door = doors.DoorObjList[doornumtile];
        doorpage = Doors.DoorFace(door, render.YTileStep == 1 ? "north" : "south");
        var doorTextureAsset = WorldTexture(doorpage, door.TileX, door.TileY);
        if (doorTextureAsset == null)
            return false;
        render.PostSource = doorTextureAsset;
        render.PostOfs = texture;
        return DoorPost(door, render.XIntercept, render.YTileStep == 1);
    }

    // Queues the shut door's post, or keeps it for later when it's seen through (true)
    bool DoorPost(DoorObject door, int along, bool flip)
    {
        bool seethrough = posts.SeesThrough(render.PostSource);
        int drawn = UnderLintel(door, along, flip, seethrough);
        if (!seethrough)
        {
            posts.ScalePost(drawn);
            return false;
        }
        posts.QueueSeeThrough(drawn);
        return true;
    }

    /*
    ====================
    =
    = Tall walls
    =
    = Above the first story, a door's tile is capped like an arch (WallPosts.OpenDoorArch): a
    = lintel flush with the wall the door is set into, as tall as the door tile, with an underside
    = over the doorway. The trace opens it as it enters the tile, shut door or not; a shut door
    = ends it at the door plane and is drawn one story high under it.
    =
    ====================
    */

    /// <summary>
    /// A wall, door or sky texture as it shows now: an animation (animdefs) on the frame the
    /// level's clock is at, so every tile showing it is in step, and it stops while the game does
    /// </summary>
    internal TextureAsset? WorldTexture(string name) => assets.Find<TextureAsset>(name, session.GameState.TimeCount);

    /// <summary>
    /// A texture on a tile's wall or door: in place of any A_ChangeTexture swapped on the tile, and
    /// an animation that plays once from when the tile last changed (a looping one in step with
    /// every other tile)
    /// </summary>
    internal TextureAsset? WorldTexture(string name, int tilex, int tiley)
        => assets.Find<TextureAsset>(map.ChangedTexture(tilex, tiley, name), session.GameState.TimeCount,
            map.AnimStart[map.TileIndex(tilex, tiley)]);


    /// <summary>
    /// Sets the patch (mapdefs patches) on the face the trace sees of the wall tile it hit, for
    /// the post being drawn: none if the tile has none or it isn't on that face. A moving
    /// pushwall's patch is on its own tile, whatever tile the trace is in. ofs is the face's
    /// column before any flip, so a patch reads the right way round on a flipped face.
    /// </summary>
    internal void SetPatchPost(int tilex, int tiley, string face, int ofs)
    {
        render.PatchOfs = ofs;
        render.PatchSource = null;
        if (render.HitPushwall)
            (tilex, tiley) = (pushWalls.PWall.X, pushWalls.PWall.Y);

        int id = map.GetPatch(tilex, tiley);
        if (id == 0 || map.GetMapData()?.Patches.TryGetValue(id, out var patch) != true || !patch!.IsOn(face))
            return;
        render.PatchSource = WorldTexture(patch.TextureFor(face), tilex, tiley);
        render.PatchStory = patch.Story;
    }

    // Points the stories above the first at the named wall texture, for the post being drawn
    internal void SetUpperPost(string wallpic, int texture, int tilex, int tiley)
    {
        if (render.HitStories == 1)
            return;
        render.UpperSource = WorldTexture(wallpic, tilex, tiley);
        render.UpperOfs = texture;
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
    internal TextureAsset? LintelTexture(DoorObject door, out bool mirrored)
    {
        int dx = door.Vertical ? 0 : 1, dy = door.Vertical ? 1 : 0;
        var face = posts.SeenFace(door.Vertical);
        return WallTexture(door.TileX - dx, door.TileY - dy, face, out mirrored)
            ?? WallTexture(door.TileX + dx, door.TileY + dy, face, out mirrored);
    }

    // A wall tile's texture on face ("north", "south", "east" or "west"), and whether it flips it
    internal TextureAsset? WallTexture(int tilex, int tiley, string face, out bool mirrored)
    {
        mirrored = false;
        var tile = map.TileMap[tilex, tiley];
        if ((tile & GameConstants.BitDoor) != 0 || (tile & ~GameConstants.BitWall) == 0)
            return null;                // open floor, a door or a pushwall

        MapTextureTranslation? mapTexture = null;
        if (map.GetMapData()?.Walls.TryGetValue(tile & ~GameConstants.BitWall, out mapTexture) != true || mapTexture == null)
            return null;
        mirrored = mapTexture.IsFlipped(face);
        return WorldTexture(mapTexture.WallFace(face), tilex, tiley);
    }

    /// <summary>
    /// For the shut door the trace just met at its plane, along it at along (y for a vertical door,
    /// x for a horizontal one): how many of its stories to draw. Under its lintel (the arch the
    /// trace opened on entering the tile), the arch ends here and the door is one story; with no
    /// wall to cap it, the door texture carries on up (flip mirrors it as the wall faces seen
    /// from the same side). 0 draws them all. A see-through door leaves the arch open, as an open
    /// door does: the trace carries on under it.
    /// </summary>
    internal int UnderLintel(DoorObject door, int along, bool flip, bool seethrough = false)
    {
        if (render.ArchOpen)
        {
            if (!seethrough)
                posts.CloseArch(render.XIntercept, render.YIntercept);
            return 1;
        }
        if (render.HitStories > 1)
        {
            var lintel = LintelTexture(door, out bool mirrored);
            render.UpperSource = lintel ?? render.PostSource;
            render.UpperOfs = WallPosts.LintelColumn(along, lintel != null && mirrored ? !flip : flip);
        }
        return 0;
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
    internal bool IsSolidEdge(WallShape shape, ControlDir edge) => edge switch
    {
        ControlDir.West => shape is WallShape.SolidNW or WallShape.SolidSW,
        ControlDir.East => shape is WallShape.SolidNE or WallShape.SolidSE,
        ControlDir.North => shape is WallShape.SolidNW or WallShape.SolidNE,
        _ => shape is WallShape.SolidSW or WallShape.SolidSE,
    };

    /// <summary>
    /// Where a trace from tile-local (eu, ev), heading (du, dv), meets the diagonal face of the
    /// tile at (TileX, TileY). All in 16.16 fixed point. On a hit, sets XIntercept/YIntercept and
    /// DiagHitU; false means it misses the face within the tile (it crosses the open half).
    /// </summary>
    internal bool TraceDiagonal(WallShape shape, int tilex, int tiley, long eu, long ev, long du, long dv)
    {
        const long TILE = MapConstants.TileGlobal;
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

        render.XIntercept = (tilex << MapConstants.TileShift) + (int)hu;
        render.YIntercept = (tiley << MapConstants.TileShift) + (int)hv;
        render.DiagHitU = (int)hu;
        return true;
    }

    internal void HitDiagWall(WallShape shape, int tilex, int tiley)
    {
        //
        // seen from the open side, the face runs left to right along +u for SolidNW/SolidNE,
        // and along -u for SolidSW/SolidSE
        //
        int along = shape is WallShape.SolidSW or WallShape.SolidSE
            ? (int)MapConstants.TileGlobal - 1 - render.DiagHitU
            : render.DiagHitU;
        along = Math.Clamp(along, 0, (int)MapConstants.TileGlobal - 1);
        int texture = (along >> GameConstants.Fixed2TexShift) & GameConstants.TextureMask;

        render.PostHeight = posts.CalcHeight();
        render.PostX = render.PixX;

        var wallpic = map.GetDiagonal(tilex, tiley)?.Texture;
        if (string.IsNullOrEmpty(wallpic))
        {
            MapTextureTranslation? mapTexture = MapTextureTranslation.None;
            map.GetMapData()?.Walls.TryGetValue(render.TileHit & ~GameConstants.BitWall, out mapTexture);
            wallpic = (mapTexture ?? MapTextureTranslation.None).North;
        }

        var textureAsset = WorldTexture(wallpic, tilex, tiley);
        if (textureAsset == null)
            return;
        render.PostSource = textureAsset;
        render.PostOfs = ScrollWall(tilex, tiley, texture);
        posts.ScalePost();
    }

    /// <summary>
    /// A wall face's column (as the trace finds it, before any flip) moved along by its tile's
    /// scroller, which also moves the post's rows (Rendering/Scrollers.cs). A moving pushwall's
    /// scroller is its own tile's, as its patch is.
    /// </summary>
    internal int ScrollWall(int tilex, int tiley, int texture)
    {
        var scrollers = map.Scrollers;
        if (!scrollers.AnyWalls)
            return texture;
        if (render.HitPushwall)
            (tilex, tiley) = (pushWalls.PWall.X, pushWalls.PWall.Y);
        render.ScrollRow = scrollers.Row(tilex, tiley);
        return scrollers.Column(tilex, tiley, texture);
    }
}
