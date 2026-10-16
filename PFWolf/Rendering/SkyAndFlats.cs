using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Rendering;

/// <summary>
/// What's behind the walls: the ceiling and floor colors (game-info's, else the map's, else the
/// default map's), shaded row by row; a level's sky above the horizon; and the textured floor
/// and ceiling flats drawn over them before the walls.
/// </summary>
/// <param name="worldTexture">A wall texture by name, an animated one's frame by the level's clock</param>
internal sealed class SkyAndFlats(
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
    internal double EyeHalves(int z) => Math.Abs(z - render.vieweyez) / (double)(GameConstants.TEXTURESIZE / 2);

    internal static byte[] vgaCeiling =
    {
        0x1d,0x1d,0x1d,0x1d,0x1d,0x1d,0x1d,0x1d,0x1d,0xbf,
        0x4e,0x4e,0x4e,0x1d,0x8d,0x4e,0x1d,0x2d,0x1d,0x8d,
        0x1d,0x1d,0x1d,0x1d,0x1d,0x2d,0xdd,0x1d,0x1d,0x98,

        0x1d,0x9d,0x2d,0xdd,0xdd,0x9d,0x2d,0x4d,0x1d,0xdd,
        0x7d,0x1d,0x2d,0x2d,0xdd,0xd7,0x1d,0x1d,0x1d,0x2d,
        0x1d,0x1d,0x1d,0x1d,0xdd,0xdd,0x7d,0xdd,0xdd,0xdd
    };

    internal byte ceilingcolor;           // this frame's ceiling color, for arch undersides too

    /*
    ====================
    =
    = Sky
    =
    = A level can have a sky: a picture drawn above the horizon in place of the ceiling color,
    = behind any ceiling flats (it shows where they're missing or see-through). It's stretched
    = to fill the view above the horizon when looking straight ahead, and repeats around the
    = full circle every SKYCIRCLE pixels, turning with the view but not moving as the player walks. Its bottom stays on the horizon as the view pitches; looking up past its top
    = shows its top row.
    =
    ====================
    */

    const int SKYCIRCLE = 1024;         // sky pixels all the way round: a 256 wide picture shows 4 times

    internal string? levelsky;   // the level's sky graphic (or wall texture), null for none
    Asset? skysource;            // the picture or texture skycolumns holds (an animation's frame)
    byte[] skycolumns = [];      // its pixels, column by column
    int skywidth, skyheight;

    /// <summary>
    /// Makes skycolumns hold the level's sky (an animated one's frame by the level's clock);
    /// false if it has none, or it can't be found.
    /// </summary>
    bool LoadSky()
    {
        if (string.IsNullOrEmpty(levelsky))
            return false;

        Asset? source = assets.Exists<GraphicAsset>(levelsky)
            ? assets.Find<GraphicAsset>(levelsky, session.gamestate.TimeCount)
            : worldTexture(levelsky);
        if (ReferenceEquals(source, skysource))
            return skycolumns.Length > 0;

        skysource = source;
        skycolumns = [];

        if (source is GraphicAsset { Width: > 0, Height: > 0 } pic)
        {
            // pictures are stored row by row
            skywidth = pic.Width;
            skyheight = pic.Height;
            skycolumns = new byte[skywidth * skyheight];
            for (int x = 0; x < skywidth; x++)
                for (int y = 0; y < skyheight; y++)
                    skycolumns[x * skyheight + y] = pic.RawData[y * skywidth + x];
        }
        else if (source is TextureAsset texture && texture.RawData.Length >= texture.Width * texture.Height)
        {
            // wall textures already are column by column
            skywidth = texture.Width;
            skyheight = texture.Height;
            skycolumns = texture.RawData;
        }

        return skycolumns.Length > 0;
    }

    void DrawSky()
    {
        int pitch = (int)video.bufferPitch;

        unsafe
        {
            byte* dest = (byte*)render.vbufPtr + render.screenofs;
            for (int x = 0; x < render.viewwidth; x++)
            {
                int angle = (render.midangle + render.pixelangle[x]) % GameConstants.ANG360;
                if (angle < 0)
                    angle += GameConstants.ANG360;

                // angles grow to the left, and the picture runs left to right
                int u = (int)((long)(GameConstants.ANG360 - 1 - angle) * SKYCIRCLE / GameConstants.ANG360 % skywidth);
                int column = u * skyheight;

                for (int y = 0; y < render.centery; y++)
                {
                    int row = (y - render.centery + render.basecentery) * skyheight / render.basecentery;
                    dest[y * pitch + x] = skycolumns[column + Math.Max(row, 0)];
                }
            }
        }
    }

    internal void VGAClearScreen()
    {
        var gameInfo = engine.GetGameInfo();
        gameInfo.Maps.TryGetValue(session.gamestate.mapon, out var mapInfo);

        // game-info's colors for the map, else the map's own (mapdefs map-info), else the default map's
        byte ceilingColor = mapInfo?.CeilingColor is { } ceiling ? video.ParseColorOr(ceiling, 0x1d)
            : map.MapCeilingColor ?? video.ParseColorOr(gameInfo.DefaultMap.CeilingColor, 0x1d);
        ceilingcolor = ceilingColor;
        byte floorColor = mapInfo?.FloorColor is { } floor ? video.ParseColorOr(floor, 0x19)
            : map.MapFloorColor ?? video.ParseColorOr(gameInfo.DefaultMap.FloorColor, 0x19);

        var destIndex = render.vbuf;
        int y;
        bool sky = LoadSky();
        unsafe
        {
            byte* dest = (byte*)render.vbufPtr;// + destIndex;
            if (sky)
            {
                DrawSky();
                y = render.centery;
                destIndex += render.centery * (int)video.bufferPitch;
            }
            else
            {
                // the ceiling is at the top of the walls
                for (y = 0; y < render.centery; y++, destIndex += (int)video.bufferPitch)
                {
                    byte color = lighting.lightrow[FlatRowShadeOffset(y, CeilingHalves) + ceilingColor];
                    for (var v = 0; v < render.viewwidth; v++)
                        dest[destIndex + v] = color;
                }
            }

            for (; y < render.viewheight; y++, destIndex += (int)video.bufferPitch)
            {
                byte color = lighting.lightrow[FlatRowShadeOffset(y, FloorHalves) + floorColor];
                for (var v = 0; v < render.viewwidth; v++)
                    dest[destIndex + v] = color;
            }
        }

        // In light zones or actors' light, even the plain colors change from place to place, so
        // they're cast too. A sky stays behind the ceiling flats, showing where there are none.
        bool floorflats = map.hasfloorflats || lighting.haszones || actorLights.haslights;
        bool ceilingflats = map.hasceilingflats || (lighting.haszones || actorLights.haslights) && !sky;
        if (floorflats || ceilingflats)
        {
            SetupFlatColumns();
            if (floorflats)
                DrawFlats(map.floorflat, floorColor, render.centery, render.viewheight - 1, FloorHalves, false);
            if (ceilingflats)
                DrawFlats(map.ceilingflat, ceilingColor, 0, render.centery - 1, CeilingHalves, sky);
        }
    }

    /*
    ====================
    =
    = Flats
    =
    = Textured floors and ceilings, from the flat plane as ECWolf has them. They're drawn over
    = the floor and ceiling colors before the walls, row by row: every pixel in a row is the
    = same distance away along the view, so each is textured from where its view ray meets the
    = floor (or ceiling), one texture per tile.
    =
    ====================
    */

    long[] flatdirx = [], flatdiry = [];

    // A flat's see-through palette index, as sprites use: the floor or ceiling color shows there
    internal const byte FLATMASK = 0xff;

    // Each column's ray, per unit of distance along the view (as CalcHeight measures it), in 1/65536ths
    internal void SetupFlatColumns()
    {
        if (flatdirx.Length != render.viewwidth)
        {
            flatdirx = new long[render.viewwidth];
            flatdiry = new long[render.viewwidth];
        }

        const double TORADIANS = 2 * Math.PI / GameConstants.FINEANGLES;
        for (int x = 0; x < render.viewwidth; x++)
        {
            double angle = (render.midangle + render.pixelangle[x]) * TORADIANS;
            double perview = 65536 / Math.Cos(render.pixelangle[x] * TORADIANS);
            flatdirx[x] = (long)(Math.Cos(angle) * perview);
            flatdiry[x] = (long)(-Math.Sin(angle) * perview);
        }
    }

    /// <summary>
    /// Draws rows from to to (inclusive) of a flat whose surface is halves half stories from the
    /// eye (FloorHalves or CeilingHalves), each pixel in its tile's light. Tiles with no texture,
    /// and a texture's see-through (FLATMASK) pixels, are color, or with no light zones or actor
    /// light, or over the sky (behind), keep what's there.
    /// </summary>
    internal void DrawFlats(TextureAsset?[] flats, byte color, int from, int to, double halves, bool behind)
    {
        int pitch = (int)video.bufferPitch;
        double numerator = render.heightnumerator * 32.0 * halves;

        unsafe
        {
            byte* dest = (byte*)render.vbufPtr + render.screenofs;
            for (int y = from; y <= to; y++)
            {
                // the row half a story (times halves) above or below the horizon at this distance
                double rowsfromcenter = Math.Abs(y + 0.5 - render.centery);
                long nx = (long)(numerator / rowsfromcenter);
                int shadeofs = lighting.ShadeOffset(nx);
                byte* row = dest + y * pitch;

                for (int x = 0; x < render.viewwidth; x++)
                {
                    long px = render.viewx + (nx * flatdirx[x] >> 16);
                    long py = render.viewy + (nx * flatdiry[x] >> 16);
                    long tilex = px >> MapConstants.TILESHIFT, tiley = py >> MapConstants.TILESHIFT;
                    if ((ulong)tilex >= MapManager.MAPSIZE || (ulong)tiley >= MapManager.MAPSIZE)
                        continue;

                    long tile = (tiley << MapManager.MAPSHIFT) + tilex;
                    var texture = flats[tile];
                    byte pixel = color;
                    if (texture != null)
                    {
                        int u = (int)(px >> (MapConstants.TILESHIFT - GameConstants.TEXTURESHIFT)) & (GameConstants.TEXTURESIZE - 1);
                        int v = (int)(py >> (MapConstants.TILESHIFT - GameConstants.TEXTURESHIFT)) & (GameConstants.TEXTURESIZE - 1);
                        int height = texture.Height;
                        pixel = texture.RawData[u * texture.Width / GameConstants.TEXTURESIZE * height + v * height / GameConstants.TEXTURESIZE];
                    }

                    // No texture, or a see-through pixel (FLATMASK, as sprites have): the color, or
                    // the sky, shows
                    if (texture == null || pixel == FLATMASK)
                    {
                        if ((lighting.haszones || actorLights.haslights) && !behind)
                            row[x] = lighting.LightAt((int)tile, px, py)[shadeofs + color];
                        continue;
                    }

                    row[x] = lighting.LightAt((int)tile, px, py)[shadeofs + pixel];
                }
            }
        }
    }

    // ShadeOffset for screen row y of a floor or ceiling halves half stories from the eye (as DrawFlats)
    // How far the floor, and the ceiling at the top of the level's walls, are from the eye, in half stories
    internal double FloorHalves => EyeHalves(0);
    internal double CeilingHalves => EyeHalves(map.wallstories * GameConstants.TEXTURESIZE);

    int FlatRowShadeOffset(int y, double halves) =>
        lighting.shading ? lighting.ShadeOffset((long)(render.heightnumerator * 32.0 * halves / Math.Abs(y + 0.5 - render.centery))) : 0;
}
