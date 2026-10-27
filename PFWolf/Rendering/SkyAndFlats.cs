using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Rendering;

/// <summary>
/// What's behind the walls: the ceiling and floor colors (game-info's, else the map's, else the
/// default map's), shaded row by row; a level's sky above the horizon and floor sky below it;
/// and the textured floor and ceiling flats drawn over them before the walls.
/// </summary>
/// <param name="worldTexture">A wall texture by name, an animated one's frame by the level's clock</param>
internal sealed partial class SkyAndFlats(
    GameEngineManager engine,
    AssetManager assets,
    VideoManager video,
    MapManager map,
    Lighting lighting,
    ActorLights actorLights,
    RenderState render,
    GameSession session,
    Func<string, TextureAsset?> worldTexture)
{
    internal double EyeHalves(int z) => Math.Abs(z - render.ViewEyeZ) / (double)(GameConstants.TextureSize / 2);

    internal static byte[] VgaCeiling =
    {
        0x1d,0x1d,0x1d,0x1d,0x1d,0x1d,0x1d,0x1d,0x1d,0xbf,
        0x4e,0x4e,0x4e,0x1d,0x8d,0x4e,0x1d,0x2d,0x1d,0x8d,
        0x1d,0x1d,0x1d,0x1d,0x1d,0x2d,0xdd,0x1d,0x1d,0x98,

        0x1d,0x9d,0x2d,0xdd,0xdd,0x9d,0x2d,0x4d,0x1d,0xdd,
        0x7d,0x1d,0x2d,0x2d,0xdd,0xd7,0x1d,0x1d,0x1d,0x2d,
        0x1d,0x1d,0x1d,0x1d,0xdd,0xdd,0x7d,0xdd,0xdd,0xdd
    };

    internal byte CeilingColor;           // this frame's ceiling color, for arch undersides too

    /// <summary>The Show Ceilings and Show Floors settings (r_ceilings, r_floors): off, the flats aren't drawn, only the plain colors</summary>
    internal Func<bool> ShowCeilings { get; init; } = () => true;
    internal Func<bool> ShowFloors { get; init; } = () => true;

    // A level's worth of no flats, for flats switched off
    TextureAsset?[] noFlats = [];
    TextureAsset?[] NoFlats(int length) => noFlats.Length == length ? noFlats : noFlats = new TextureAsset?[length];

    /*
    ====================
    =
    = Sky
    =
    = A level can have a sky: a picture drawn above the horizon in place of the ceiling color,
    = behind any ceiling flats (it shows where they're missing or see-through). It's stretched
    = to fill the view above the horizon when looking straight ahead, and repeats around the
    = full circle every SkyCircle pixels, turning with the view but not moving as the player walks. Its bottom stays on the horizon as the view pitches; looking up past its top
    = shows its top row's most common color (repeating the row itself would stretch a star on it
    = into a streak).
    =
    ====================
    */

    const int SkyCircle = 1024;         // sky pixels all the way round: a 256 wide picture shows 4 times

    internal string? LevelSky;   // the level's sky graphic (or wall texture), null for none

    /*
    A level can have a floor sky too (game-info floor-sky: Midnight Assault's floor parallax):
    a picture drawn below the horizon in place of the floor color, behind the floor flats, so
    it shows where a floor tile has none -- the valley below a bridge, the water off the shore.
    It runs down from the horizon (its top row there), stretched to the view's lower half
    looking straight ahead, and turns with the view as the sky does. Mirrored, it's drawn
    upside down from the horizon instead: a sky reflected in water.
    */
    internal string? LevelFloorSky;      // the level's floor sky graphic (or wall texture), null for none
    internal bool FloorSkyMirror;        // drawn upside down from the horizon

    readonly SkyPicture sky = new(), floorsky = new();

    /// <summary>A sky's picture, column by column (an animated one's frame by the level's clock)</summary>
    sealed class SkyPicture
    {
        Asset? source;                // the picture or texture Columns holds (an animation's frame)
        internal byte[] Columns = [];
        internal int Width, Height;
        internal byte TopColor, BottomColor;  // the most common color of the top and bottom rows: what fills past them

        /// <summary>Holds the named picture or wall texture's pixels; false if there's none, or it can't be found</summary>
        internal bool Load(string? name, AssetManager? assets, Func<string, TextureAsset?> worldTexture, int clock)
        {
            if (string.IsNullOrEmpty(name) || name.Equals("none", StringComparison.OrdinalIgnoreCase))
                return false;

            Asset? found = assets != null && assets.Exists<GraphicAsset>(name)
                ? assets.Find<GraphicAsset>(name, clock)
                : worldTexture(name);
            if (ReferenceEquals(found, source))
                return Columns.Length > 0;

            source = found;
            Columns = [];

            if (found is GraphicAsset { Width: > 0, Height: > 0 } pic)
            {
                // pictures are stored row by row
                Width = pic.Width;
                Height = pic.Height;
                Columns = new byte[Width * Height];
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                        Columns[x * Height + y] = pic.RawData[y * Width + x];
            }
            else if (found is TextureAsset texture && texture.RawData.Length >= texture.Width * texture.Height)
            {
                // wall textures already are column by column
                Width = texture.Width;
                Height = texture.Height;
                Columns = texture.RawData;
            }

            if (Columns.Length > 0)
            {
                TopColor = CommonColor(0);
                BottomColor = CommonColor(Height - 1);
            }
            return Columns.Length > 0;
        }

        // The color most of a row's pixels have (the lowest one on a tie)
        byte CommonColor(int row)
        {
            Span<int> counts = stackalloc int[256];
            for (int x = 0; x < Width; x++)
                counts[Columns[x * Height + row]]++;
            int best = 0;
            for (int color = 1; color < 256; color++)
                if (counts[color] > counts[best])
                    best = color;
            return (byte)best;
        }
    }

    // The column of a sky picture this view column shows: angles grow to the left, and the picture runs left to right
    int SkyColumn(int x, SkyPicture picture)
    {
        int angle = (render.MidAngle + render.PixelAngle[x]) % GameConstants.Ang360;
        if (angle < 0)
            angle += GameConstants.Ang360;
        int u = (int)((long)(GameConstants.Ang360 - 1 - angle) * SkyCircle / GameConstants.Ang360 % picture.Width);
        return u * picture.Height;
    }

    void DrawSky()
    {
        int pitch = (int)video.BufferPitch;

        unsafe
        {
            byte* dest = (byte*)render.VbufPtr + render.ScreenOfs;
            for (int x = 0; x < render.ViewWidth; x++)
            {
                int column = SkyColumn(x, sky);
                for (int y = 0; y < render.CenterY; y++)
                {
                    int row = (y - render.CenterY + render.BaseCenterY) * sky.Height / render.BaseCenterY;
                    dest[y * pitch + x] = row >= 0 ? sky.Columns[column + row] : sky.TopColor;
                }
            }
        }
    }

    /// <summary>Holds the level's floor sky; false if it has none, or it can't be found</summary>
    internal bool LoadFloorSky() => floorsky.Load(LevelFloorSky, assets, worldTexture, session.GameState.TimeCount);

    // The floor sky over the rows below the horizon: its top row at the horizon running down, or
    // mirrored, its bottom row at the horizon running up to its top at the bottom of the view.
    // Looking down past its far edge shows that edge row's most common color, as the sky does
    internal void DrawFloorSky()
    {
        int pitch = (int)video.BufferPitch;
        int last = floorsky.Height - 1;
        byte beyond = FloorSkyMirror ? floorsky.TopColor : floorsky.BottomColor;

        unsafe
        {
            byte* dest = (byte*)render.VbufPtr + render.ScreenOfs;
            for (int x = 0; x < render.ViewWidth; x++)
            {
                int column = SkyColumn(x, floorsky);
                for (int y = Math.Max((int)render.CenterY, 0); y < render.ViewHeight; y++)
                {
                    int below = (y - render.CenterY) * floorsky.Height / render.BaseCenterY;
                    int row = FloorSkyMirror ? last - below : below;
                    dest[y * pitch + x] = row >= 0 && row <= last ? floorsky.Columns[column + row] : beyond;
                }
            }
        }
    }

    internal void VGAClearScreen()
    {
        var gameInfo = engine.GetGameInfo();
        var mapInfo = CurrentMapInfo.For(session, gameInfo);

        // game-info's colors for the map, else the map's own (mapdefs map-info), else the default map's
        byte ceilingColor = mapInfo?.CeilingColor is { } ceiling ? video.ParseColorOr(ceiling, 0x1d)
            : map.MapCeilingColor ?? video.ParseColorOr(gameInfo.DefaultMap.CeilingColor, 0x1d);
        byte floorColor = mapInfo?.FloorColor is { } floor ? video.ParseColorOr(floor, 0x19)
            : map.MapFloorColor ?? video.ParseColorOr(gameInfo.DefaultMap.FloorColor, 0x19);

        // A_Warp's "swapcolors" has the second pair showing (each left out keeps the first)
        if (map.AltFlatColors)
        {
            if (mapInfo?.AltCeilingColor is { } altCeiling)
                ceilingColor = video.ParseColorOr(altCeiling, ceilingColor);
            if (mapInfo?.AltFloorColor is { } altFloor)
                floorColor = video.ParseColorOr(altFloor, floorColor);
        }
        CeilingColor = ceilingColor;

        var destIndex = render.Vbuf;
        int y;
        bool sky = this.sky.Load(LevelSky, assets, worldTexture, session.GameState.TimeCount);
        bool generated = !sky && HasGeneratedSky;     // a star or cloud sky (SkyAndFlats.Generated.cs)
        bool floorsky = LoadFloorSky();
        unsafe
        {
            byte* dest = (byte*)render.VbufPtr;// + destIndex;
            if (sky || generated)
            {
                if (generated)
                    DrawGeneratedSky(ceilingColor);
                else
                    DrawSky();
                y = render.CenterY;
                destIndex += render.CenterY * (int)video.BufferPitch;
            }
            else
            {
                // the ceiling is at the top of the walls
                for (y = 0; y < render.CenterY; y++, destIndex += (int)video.BufferPitch)
                {
                    byte color = lighting.LightRow[FlatRowShadeOffset(y, CeilingHalves) + ceilingColor];
                    new Span<byte>(dest + destIndex, render.ViewWidth).Fill(color);
                }
            }

            if (floorsky)
                DrawFloorSky();
            else
            {
                for (; y < render.ViewHeight; y++, destIndex += (int)video.BufferPitch)
                {
                    byte color = lighting.LightRow[FlatRowShadeOffset(y, FloorHalves) + floorColor];
                    new Span<byte>(dest + destIndex, render.ViewWidth).Fill(color);
                }
            }
        }

        frameFloorColor = floorColor;
        skyBehind = sky || generated;
        floorSkyBehind = floorsky;
    }

    // What VGAClearScreen drew this frame, for DrawLevelFlats: the floor color, and whether a sky
    // or floor sky is behind the flats
    byte frameFloorColor;
    bool skyBehind, floorSkyBehind;

    /// <summary>
    /// Casts the floor and ceiling flats over what VGAClearScreen drew, after the walls (WallRefresh),
    /// into only the rows they left: a wall's rows would just be drawn over.
    /// </summary>
    internal void DrawLevelFlats()
    {
        // In light zones or actors' light, even the plain colors change from place to place, so
        // they're cast too. A sky stays behind the ceiling flats, and a floor sky behind the floor
        // flats, showing where there are none.
        // With Show Floors or Show Ceilings off (Blake Stone's switches), that side's flats aren't drawn
        bool showFloors = ShowFloors(), showCeilings = ShowCeilings();
        bool floorflats = map.HasFloorFlats && showFloors || (lighting.HasZones || actorLights.HasLights) && !floorSkyBehind;
        bool ceilingflats = map.HasCeilingFlats && showCeilings || (lighting.HasZones || actorLights.HasLights) && !skyBehind;
        if (floorflats || ceilingflats)
        {
            SetupFlatColumns();
            if (floorflats)
                DrawFlats(showFloors ? map.FloorFlat : NoFlats(map.FloorFlat.Length), frameFloorColor, render.CenterY, render.ViewHeight - 1, FloorHalves, floorSkyBehind,
                    map.Scrollers.AnyFloors ? map.Scrollers.FloorX : null, map.Scrollers.FloorY);
            if (ceilingflats)
                DrawFlats(showCeilings ? map.CeilingFlat : NoFlats(map.CeilingFlat.Length), CeilingColor, 0, render.CenterY - 1, CeilingHalves, skyBehind,
                    map.Scrollers.AnyCeilings ? map.Scrollers.CeilingX : null, map.Scrollers.CeilingY);
        }
    }

    /*
    ====================
    =
    = Flats
    =
    = Textured floors and ceilings, from the flat plane as ECWolf has them. They're drawn over
    = the floor and ceiling colors after the walls, into the rows of each column the walls left
    = (RenderState's covers): every pixel in a row is the same distance away along the view, so
    = each is textured from where its view ray meets the floor (or ceiling), one texture per tile.
    =
    ====================
    */

    long[] flatdirx = [], flatdiry = [];

    // A flat's see-through palette index, as sprites use: the floor or ceiling color shows there
    internal const byte FlatMask = 0xff;

    // Each column's ray, per unit of distance along the view (as CalcHeight measures it), in 1/65536ths
    internal void SetupFlatColumns()
    {
        if (flatdirx.Length != render.ViewWidth)
        {
            flatdirx = new long[render.ViewWidth];
            flatdiry = new long[render.ViewWidth];
        }

        const double TORADIANS = 2 * Math.PI / GameConstants.FineAngles;
        for (int x = 0; x < render.ViewWidth; x++)
        {
            double angle = (render.MidAngle + render.PixelAngle[x]) * TORADIANS;
            double perview = 65536 / Math.Cos(render.PixelAngle[x] * TORADIANS);
            flatdirx[x] = (long)(Math.Cos(angle) * perview);
            flatdiry[x] = (long)(-Math.Sin(angle) * perview);
        }
    }

    /// <summary>
    /// Draws rows from to to (inclusive) of a flat whose surface is halves half stories from the
    /// eye (FloorHalves or CeilingHalves), each pixel in its tile's light. Tiles with no texture,
    /// and a texture's see-through (FlatMask) pixels, are color, or with no light zones or actor
    /// light, or over the sky (behind), keep what's there. scrollx and scrolly (null: none) are how
    /// far east and south each tile's texture has moved, in global units (Rendering/Scrollers.cs).
    /// </summary>
    internal void DrawFlats(TextureAsset?[] flats, byte color, int from, int to, double halves, bool behind,
        int[]? scrollx = null, int[]? scrolly = null)
    {
        if (from > to)
            return;
        int pitch = (int)video.BufferPitch;
        double numerator = render.HeightNumerator * 32.0 * halves;

        // this is every pixel of the floor or ceiling, so what the loop reads is kept in locals
        int width = render.ViewWidth;
        long viewx = render.ViewX, viewy = render.ViewY;
        ulong mapwidth = map.MapWidth, mapheight = map.MapHeight;
        bool shaded = lighting.Shading;
        bool fillbare = (lighting.HasZones || actorLights.HasLights) && !behind;    // tiles with no texture are cast too
        long[] dirxs = flatdirx, dirys = flatdiry;
        StartCovers(width, from);
        int[] blocktop = blockTop, blockbottom = blockBottom;

        unsafe
        {
            byte* dest = (byte*)render.VbufPtr + render.ScreenOfs;
            for (int y = from; y <= to; y++)
            {
                // the row half a story (times halves) above or below the horizon at this distance
                long nx = (long)(numerator / Math.Abs(y + 0.5 - render.CenterY));
                int shadeofs = lighting.ShadeOffset(nx);
                byte* row = dest + y * pitch;

                // what's kept from the tile, and the light cell, the last pixel was on
                long lasttile = -1;
                TextureAsset? lasttexture = null;
                byte[] data = [];
                bool square = false, inwall = false;
                long lightcx = -1, lightcy = -1, lighttile = -1;
                byte[] light = Lighting.NoShade;

                for (int x = 0; x < width; x++)
                {
                    // a row the walls painted here is theirs
                    if (y >= blocktop[x] && (y <= blockbottom[x] || NextCover(x, y)))
                        continue;

                    long px = viewx + (nx * dirxs[x] >> 16);
                    long py = viewy + (nx * dirys[x] >> 16);
                    long tilex = px >> MapConstants.TileShift, tiley = py >> MapConstants.TileShift;
                    if ((ulong)tilex >= mapwidth || (ulong)tiley >= mapheight)
                        continue;

                    long tile = tiley * (long)mapwidth + tilex;
                    if (tile != lasttile)
                    {
                        lasttile = tile;
                        lasttexture = flats[tile];
                        data = lasttexture?.RawData ?? [];
                        square = lasttexture is { Width: GameConstants.TextureSize, Height: GameConstants.TextureSize };
                        inwall = lasttexture == null && map.TileMap[tilex, tiley] != 0;
                    }
                    var texture = lasttexture;

                    // A wall's tile rarely has a flat, but the rows just past its face (where its post
                    // rounds short of the true edge) land on it: they take the tile the ray came from
                    if (inwall && FlatBeforeWall(dirxs[x], dirys[x], ref px, ref py, ref tilex, ref tiley))
                    {
                        tile = tiley * (long)mapwidth + tilex;
                        texture = flats[tile];
                    }

                    byte pixel = color;
                    if (texture != null)
                    {
                        // a scrolled tile shows the texel that's moved here from west and north of it
                        long tx = px, ty = py;
                        if (scrollx != null)
                        {
                            tx -= scrollx[tile];
                            ty -= scrolly![tile];
                        }
                        int u = (int)(tx >> (MapConstants.TileShift - GameConstants.TextureShift)) & (GameConstants.TextureSize - 1);
                        int v = (int)(ty >> (MapConstants.TileShift - GameConstants.TextureShift)) & (GameConstants.TextureSize - 1);
                        pixel = square && texture == lasttexture
                            ? data[(u << GameConstants.TextureShift) | v]
                            : FlatTexel(texture, u, v);
                    }

                    // No texture, or a see-through pixel (FlatMask, as sprites have): the color, or
                    // the sky, shows
                    bool bare = texture == null || pixel == FlatMask;
                    if (bare && !fillbare)
                        continue;
                    if (bare)
                        pixel = color;

                    // unshaded, the light is NoShade from 0: the pixel itself
                    if (shaded)
                    {
                        // the light's the same over a light cell (Lighting.LightAt)
                        long cx = px >> Lighting.CellGlobalShift, cy = py >> Lighting.CellGlobalShift;
                        if (cx != lightcx || cy != lightcy || tile != lighttile)
                        {
                            lightcx = cx;
                            lightcy = cy;
                            lighttile = tile;
                            light = lighting.LightAt((int)tile, px, py);
                        }
                        pixel = light[shadeofs + pixel];
                    }
                    row[x] = pixel;
                }
            }
        }
    }

    // A flat's texel u across and v down a tile (0-63), of any size of texture
    static byte FlatTexel(TextureAsset texture, int u, int v)
    {
        int height = texture.Height;
        return texture.RawData[u * texture.Width / GameConstants.TextureSize * height + v * height / GameConstants.TextureSize];
    }

    //
    // The covers (RenderState: the rows each column's walls painted) DrawFlats is going down past:
    // each column's, sorted by top, and the first that doesn't end above the row it's on (a top
    // of int.MaxValue when there are no more)
    //
    int[] blockTop = [], blockBottom = [], blockIndex = [];

    // Sorts each column's covers, and finds the first that doesn't end above row from
    void StartCovers(int width, int from)
    {
        if (blockTop.Length < width)
        {
            blockTop = new int[width];
            blockBottom = new int[width];
            blockIndex = new int[width];
        }
        bool traced = render.CoverCount.Length >= width;
        for (int x = 0; x < width; x++)
        {
            if (traced)
                SortCovers(x);
            blockIndex[x] = -1;
            blockTop[x] = blockBottom[x] = -1;      // ends above every row: NextCover finds the first
            NextCover(x, from);
        }
    }

    /// <summary>
    /// Moves column x on to its first cover that doesn't end above row y; true if that one has
    /// row y in it (it's painted), false if it starts lower or there's none left.
    /// </summary>
    bool NextCover(int x, int y)
    {
        int count = x < render.CoverCount.Length ? render.CoverCount[x] : 0;
        int first = x * RenderState.MaxCovers, i = blockIndex[x];
        while (++i < count && render.CoverBottom[first + i] < y) { }
        blockIndex[x] = i;
        if (i >= count)
        {
            blockTop[x] = blockBottom[x] = int.MaxValue;
            return false;
        }
        blockTop[x] = render.CoverTop[first + i];
        blockBottom[x] = render.CoverBottom[first + i];
        return blockTop[x] <= y;
    }

    // Sorts column x's covers by top
    void SortCovers(int x)
    {
        int[] tops = render.CoverTop, bottoms = render.CoverBottom;
        int first = x * RenderState.MaxCovers, last = first + render.CoverCount[x];
        for (int i = first + 1; i < last; i++)
        {
            int top = tops[i], bottom = bottoms[i];
            int j = i;
            for (; j > first && tops[j - 1] > top; j--)
            {
                tops[j] = tops[j - 1];
                bottoms[j] = bottoms[j - 1];
            }
            tops[j] = top;
            bottoms[j] = bottom;
        }
    }

    /// <summary>
    /// Moves a flat point (px, py) in a wall's tile back to the edge of the tile its ray (dirx, diry)
    /// entered through, and to that tile; false when the eye is in the wall's tile itself.
    /// </summary>
    bool FlatBeforeWall(long dirx, long diry, ref long px, ref long py, ref long tilex, ref long tiley)
    {
        long eyetilex = render.ViewX >> MapConstants.TileShift, eyetiley = render.ViewY >> MapConstants.TileShift;
        if (eyetilex == tilex && eyetiley == tiley)
            return false;

        // how far into the tile the point is from the side the ray came in on, along each axis;
        // the side crossed last is the one it entered by
        long tilesize = 1L << MapConstants.TileShift;
        long left = tilex << MapConstants.TileShift, top = tiley << MapConstants.TileShift;
        long intox = dirx > 0 ? px - left : left + tilesize - px;
        long intoy = diry > 0 ? py - top : top + tilesize - py;
        bool crossedx = eyetiley == tiley
            || eyetilex != tilex && intox * Math.Abs(diry) < intoy * Math.Abs(dirx);

        if (crossedx)
        {
            tilex += dirx > 0 ? -1 : 1;
            px = dirx > 0 ? left - 1 : left + tilesize;
        }
        else
        {
            tiley += diry > 0 ? -1 : 1;
            py = diry > 0 ? top - 1 : top + tilesize;
        }
        return (ulong)tilex < map.MapWidth && (ulong)tiley < map.MapHeight;
    }

    // ShadeOffset for screen row y of a floor or ceiling halves half stories from the eye (as DrawFlats)
    // How far the floor, and the ceiling at the top of the level's walls, are from the eye, in half stories
    internal double FloorHalves => EyeHalves(0);
    internal double CeilingHalves => EyeHalves(map.LevelWallStories * GameConstants.TextureSize);

    int FlatRowShadeOffset(int y, double halves) =>
        lighting.Shading ? lighting.ShadeOffset((long)(render.HeightNumerator * 32.0 * halves / Math.Abs(y + 0.5 - render.CenterY))) : 0;
}
