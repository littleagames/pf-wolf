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

    /*
    ====================
    =
    = Sky
    =
    = A level can have a sky: a picture drawn above the horizon in place of the ceiling color,
    = behind any ceiling flats (it shows where they're missing or see-through). It's stretched
    = to fill the view above the horizon when looking straight ahead, and repeats around the
    = full circle every SkyCircle pixels, turning with the view but not moving as the player walks. Its bottom stays on the horizon as the view pitches; looking up past its top
    = shows its top row.
    =
    ====================
    */

    const int SkyCircle = 1024;         // sky pixels all the way round: a 256 wide picture shows 4 times

    internal string? LevelSky;   // the level's sky graphic (or wall texture), null for none
    Asset? skysource;            // the picture or texture skycolumns holds (an animation's frame)
    byte[] skycolumns = [];      // its pixels, column by column
    int skywidth, skyheight;

    /// <summary>
    /// Makes skycolumns hold the level's sky (an animated one's frame by the level's clock);
    /// false if it has none, or it can't be found.
    /// </summary>
    bool LoadSky()
    {
        if (string.IsNullOrEmpty(LevelSky))
            return false;

        Asset? source = assets.Exists<GraphicAsset>(LevelSky)
            ? assets.Find<GraphicAsset>(LevelSky, session.GameState.TimeCount)
            : worldTexture(LevelSky);
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
            byte* dest = (byte*)render.VbufPtr + render.ScreenOfs;
            for (int x = 0; x < render.ViewWidth; x++)
            {
                int angle = (render.MidAngle + render.PixelAngle[x]) % GameConstants.Ang360;
                if (angle < 0)
                    angle += GameConstants.Ang360;

                // angles grow to the left, and the picture runs left to right
                int u = (int)((long)(GameConstants.Ang360 - 1 - angle) * SkyCircle / GameConstants.Ang360 % skywidth);
                int column = u * skyheight;

                for (int y = 0; y < render.CenterY; y++)
                {
                    int row = (y - render.CenterY + render.BaseCenterY) * skyheight / render.BaseCenterY;
                    dest[y * pitch + x] = skycolumns[column + Math.Max(row, 0)];
                }
            }
        }
    }

    internal void VGAClearScreen()
    {
        var gameInfo = engine.GetGameInfo();
        gameInfo.Maps.TryGetValue(session.GameState.MapOn, out var mapInfo);

        // game-info's colors for the map, else the map's own (mapdefs map-info), else the default map's
        byte ceilingColor = mapInfo?.CeilingColor is { } ceiling ? video.ParseColorOr(ceiling, 0x1d)
            : map.MapCeilingColor ?? video.ParseColorOr(gameInfo.DefaultMap.CeilingColor, 0x1d);
        CeilingColor = ceilingColor;
        byte floorColor = mapInfo?.FloorColor is { } floor ? video.ParseColorOr(floor, 0x19)
            : map.MapFloorColor ?? video.ParseColorOr(gameInfo.DefaultMap.FloorColor, 0x19);

        var destIndex = render.Vbuf;
        int y;
        bool sky = LoadSky();
        unsafe
        {
            byte* dest = (byte*)render.VbufPtr;// + destIndex;
            if (sky)
            {
                DrawSky();
                y = render.CenterY;
                destIndex += render.CenterY * (int)video.bufferPitch;
            }
            else
            {
                // the ceiling is at the top of the walls
                for (y = 0; y < render.CenterY; y++, destIndex += (int)video.bufferPitch)
                {
                    byte color = lighting.LightRow[FlatRowShadeOffset(y, CeilingHalves) + ceilingColor];
                    for (var v = 0; v < render.ViewWidth; v++)
                        dest[destIndex + v] = color;
                }
            }

            for (; y < render.ViewHeight; y++, destIndex += (int)video.bufferPitch)
            {
                byte color = lighting.LightRow[FlatRowShadeOffset(y, FloorHalves) + floorColor];
                for (var v = 0; v < render.ViewWidth; v++)
                    dest[destIndex + v] = color;
            }
        }

        // In light zones or actors' light, even the plain colors change from place to place, so
        // they're cast too. A sky stays behind the ceiling flats, showing where there are none.
        bool floorflats = map.hasfloorflats || lighting.HasZones || actorLights.HasLights;
        bool ceilingflats = map.hasceilingflats || (lighting.HasZones || actorLights.HasLights) && !sky;
        if (floorflats || ceilingflats)
        {
            SetupFlatColumns();
            if (floorflats)
                DrawFlats(map.floorflat, floorColor, render.CenterY, render.ViewHeight - 1, FloorHalves, false);
            if (ceilingflats)
                DrawFlats(map.ceilingflat, ceilingColor, 0, render.CenterY - 1, CeilingHalves, sky);
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
    /// light, or over the sky (behind), keep what's there.
    /// </summary>
    internal void DrawFlats(TextureAsset?[] flats, byte color, int from, int to, double halves, bool behind)
    {
        int pitch = (int)video.bufferPitch;
        double numerator = render.HeightNumerator * 32.0 * halves;

        unsafe
        {
            byte* dest = (byte*)render.VbufPtr + render.ScreenOfs;
            for (int y = from; y <= to; y++)
            {
                // the row half a story (times halves) above or below the horizon at this distance
                double rowsfromcenter = Math.Abs(y + 0.5 - render.CenterY);
                long nx = (long)(numerator / rowsfromcenter);
                int shadeofs = lighting.ShadeOffset(nx);
                byte* row = dest + y * pitch;

                for (int x = 0; x < render.ViewWidth; x++)
                {
                    long px = render.ViewX + (nx * flatdirx[x] >> 16);
                    long py = render.ViewY + (nx * flatdiry[x] >> 16);
                    long tilex = px >> MapConstants.TileShift, tiley = py >> MapConstants.TileShift;
                    if ((ulong)tilex >= MapManager.MapSize || (ulong)tiley >= MapManager.MapSize)
                        continue;

                    long tile = (tiley << MapManager.MapShift) + tilex;
                    var texture = flats[tile];
                    byte pixel = color;
                    if (texture != null)
                    {
                        int u = (int)(px >> (MapConstants.TileShift - GameConstants.TextureShift)) & (GameConstants.TextureSize - 1);
                        int v = (int)(py >> (MapConstants.TileShift - GameConstants.TextureShift)) & (GameConstants.TextureSize - 1);
                        int height = texture.Height;
                        pixel = texture.RawData[u * texture.Width / GameConstants.TextureSize * height + v * height / GameConstants.TextureSize];
                    }

                    // No texture, or a see-through pixel (FlatMask, as sprites have): the color, or
                    // the sky, shows
                    if (texture == null || pixel == FlatMask)
                    {
                        if ((lighting.HasZones || actorLights.HasLights) && !behind)
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
    internal double CeilingHalves => EyeHalves(map.wallstories * GameConstants.TextureSize);

    int FlatRowShadeOffset(int y, double halves) =>
        lighting.Shading ? lighting.ShadeOffset((long)(render.HeightNumerator * 32.0 * halves / Math.Abs(y + 0.5 - render.CenterY))) : 0;
}
