using PFWolf.Assets;

namespace PFWolf.Rendering;

/*
=============================================================================

                            STAR AND CLOUD SKIES

A level can have a sky made rather than drawn (game-info star-sky, cloud-sky: End of Destiny's
sky levels, from Wolf4SDL's wl_atmos and wl_cloudsky). It's drawn above the horizon where a sky
picture would be, behind the ceiling flats.

STARS (wl_atmos DrawStarSky): stars at random places around and above the view, each in the
direction of its spot, nearer ones brighter (the next 15 palette indices after color, fainter
each), turning with the view but not moving as the player walks, on the background color; and
the moon, wl_atmos' 10 by 10 picture, in the direction moon-angle says.

CLOUDS (wl_cloudsky InitSky, DrawClouds): a 256 x 256 cloud picture made from the seed as
wl_cloudsky makes it (plasma by the C library's rand(), smoothed twice, its values put to the
colors), lying on a plane four stories above the eye, eight texels a tile, moving speed global
units a tic the way angle says, by the level's clock. It's drawn over the stars (or the ceiling
color), which show through where its color is none, and fades with distance as flats do.

Both come from the sky's own numbers (the seed, the view's own random for the stars), never the
game's: nothing is saved, and demos play the same with or without them.

=============================================================================
*/

internal sealed partial class SkyAndFlats
{
    StarSkyInfo? levelStars;
    CloudSkyInfo? levelClouds;

    // Each star: its spot (x and z around the view, y up), as wl_atmos' points
    (int X, int Y, int Z)[] stars = [];

    // The cloud picture's palette indices (FlatMask: see-through), and the seed and colors it was made with
    byte[] clouds = [];
    string cloudsMadeFor = "";

    /// <summary>Whether the level has a star or cloud sky</summary>
    internal bool HasGeneratedSky => levelStars != null || levelClouds != null;

    /// <summary>The level's star sky and cloud sky (null for none); the stars are scattered afresh</summary>
    internal void SetGeneratedSky(StarSkyInfo? starSky, CloudSkyInfo? cloudSky)
    {
        levelStars = starSky;
        levelClouds = cloudSky;
        stars = starSky == null ? [] : MakeStars(Math.Clamp(starSky.Stars, 0, 5000), new Random());
        if (cloudSky != null)
        {
            string key = cloudSky.Seed + ":" + string.Join(",", cloudSky.Colors);
            if (key != cloudsMadeFor)
            {
                clouds = MakeClouds(cloudSky.Seed, cloudSky.Colors.Select(ColorOrNone).ToArray());
                cloudsMadeFor = key;
            }
        }
    }

    byte ColorOrNone(string color) =>
        string.IsNullOrWhiteSpace(color) || color.Trim().Equals("none", StringComparison.OrdinalIgnoreCase)
            ? FlatMask
            : SkyColor(color, FlatMask);

    // A palette index (as wl_atmos and wl_cloudsky give them), or a #RRGGBB color
    byte SkyColor(string? color, byte fallback) =>
        byte.TryParse(color?.Trim(), out var index) ? index : video.ParseColorOr(color?.Trim(), fallback);

    /// <summary>wl_atmos' Init3DPoints: spots around the view (x, z from -16384 to 16383), up to 9215 high but not so high they'd show past the top of the view</summary>
    internal static (int X, int Y, int Z)[] MakeStars(int count, Random random)
    {
        var made = new (int X, int Y, int Z)[count];
        for (int i = 0; i < count; i++)
        {
            int x = 16384 - random.Next(32768), z = 16384 - random.Next(32768);
            double len = Math.Sqrt((double)x * x + (double)z * z);
            int y, tries = 50;
            do
            {
                y = 1024 + random.Next(8192);
                tries--;
            }
            while (tries > 0 && y * 256.0 / Math.Max(len, 1) >= 100);
            made[i] = (x, y, z);
        }
        return made;
    }

    // wl_atmos' moon, 10 by 10 palette indices (0: see-through)
    static readonly byte[] Moon =
    [
         0,  0, 27, 18, 15, 16, 19, 29,  0,  0,
         0, 22, 16, 15, 15, 16, 16, 18, 24,  0,
        27, 17, 15, 17, 16, 16, 17, 17, 18, 29,
        18, 15, 15, 15, 16, 16, 17, 17, 18, 20,
        16, 15, 15, 16, 16, 17, 17, 18, 19, 21,
        16, 15, 17, 20, 18, 17, 18, 18, 20, 22,
        19, 16, 18, 19, 17, 17, 18, 19, 22, 24,
        28, 19, 17, 17, 17, 18, 19, 21, 25, 31,
         0, 23, 18, 19, 18, 20, 22, 24, 28,  0,
         0,  0, 28, 21, 20, 22, 28, 30,  0,  0,
    ];

    /// <summary>
    /// wl_cloudsky's InitSky: the cloud picture for a seed, its values put to colors (FlatMask for
    /// none), row by row as wl_cloudsky keeps it (skyc[i * 256 + j])
    /// </summary>
    internal static byte[] MakeClouds(uint seed, byte[] colors)
    {
        var sky = new byte[65536];
        var rand = new CRand(seed);
        sky[0] = (byte)(rand.Next() % 256);
        SplitS(sky, rand, 256, 0, 0, 256, 256);

        // smooth the clouds a bit, twice
        for (int k = 0; k < 2; k++)
        {
            for (int i = 0; i < 256; i++)
            {
                for (int j = 0; j < 256; j++)
                {
                    int val = -sky[j * 256 + i];
                    for (int m = 0; m < 3; m++)
                        for (int n = 0; n < 3; n++)
                            val += sky[((j + n - 1) & 0xff) * 256 + ((i + m - 1) & 0xff)];
                    sky[j * 256 + i] = (byte)(val >> 3);
                }
            }
        }

        if (colors.Length == 0)
            colors = [FlatMask];
        for (int i = 0; i < sky.Length; i++)
            sky[i] = colors[sky[i] * colors.Length / 256];
        return sky;
    }

    // wl_cloudsky's SplitS: the plasma's midpoint displacement, as C's byte arithmetic has it
    static void SplitS(byte[] skyc, CRand rand, int size, int x1, int y1, int x2, int y2)
    {
        if (size == 1)
            return;
        int half = size / 2;

        void Edge(int at, int a, int b)
        {
            if (skyc[at] != 0)
                return;
            skyc[at] = (byte)((byte)((skyc[a] + skyc[b]) / 2) + rand.Next() % (size * 2) - size);
            if (skyc[at] == 0)
                skyc[at] = 1;
        }

        Edge((x1 + half) * 256 + y1, x1 * 256 + y1, (x2 & 0xff) * 256 + y1);
        Edge((x1 + half) * 256 + (y2 & 0xff), x1 * 256 + (y2 & 0xff), (x2 & 0xff) * 256 + (y2 & 0xff));
        Edge(x1 * 256 + y1 + half, x1 * 256 + y1, x1 * 256 + (y2 & 0xff));
        Edge((x2 & 0xff) * 256 + y1 + half, (x2 & 0xff) * 256 + y1, (x2 & 0xff) * 256 + (y2 & 0xff));

        skyc[(x1 + half) * 256 + y1 + half] = (byte)((byte)((skyc[x1 * 256 + y1] + skyc[(x2 & 0xff) * 256 + y1]
            + skyc[x1 * 256 + (y2 & 0xff)] + skyc[(x2 & 0xff) * 256 + (y2 & 0xff)]) / 4) + rand.Next() % (size * 2) - size);

        SplitS(skyc, rand, half, x1, y1 + half, x1 + half, y2);
        SplitS(skyc, rand, half, x1 + half, y1, x2, y1 + half);
        SplitS(skyc, rand, half, x1 + half, y1 + half, x2, y2);
        SplitS(skyc, rand, half, x1, y1, x1 + half, y1 + half);
    }

    /// <summary>The C library's rand() as Windows' (and Wolf4SDL's builds) have it, from srand(seed)</summary>
    internal sealed class CRand(uint seed)
    {
        uint state = seed;

        internal int Next()
        {
            state = state * 214013 + 2531011;
            return (int)((state >> 16) & 0x7fff);
        }
    }

    /// <summary>The star sky and the clouds over the rows above the horizon, the background (or the ceiling color) behind</summary>
    void DrawGeneratedSky(byte ceilingColor)
    {
        int pitch = (int)video.BufferPitch;
        int rows = Math.Clamp((int)render.CenterY, 0, render.ViewHeight);
        byte background = levelStars != null ? SkyColor(levelStars.Background, 0) : ceilingColor;

        unsafe
        {
            byte* dest = (byte*)render.VbufPtr + render.ScreenOfs;
            for (int y = 0; y < rows; y++)
            {
                byte color = levelStars != null ? background : lighting.LightRow[FlatRowShadeOffset(y, CeilingHalves) + background];
                for (int x = 0; x < render.ViewWidth; x++)
                    dest[y * pitch + x] = color;
            }
            if (levelStars != null)
                DrawStars(dest, pitch, rows, levelStars);
            if (levelClouds != null && clouds.Length == 65536)
                DrawClouds(dest, pitch, rows, levelClouds);
        }
    }

    // Where a direction (dx east, dy south, as the map's) is across the view: false behind it
    bool ProjectDirection(double dx, double dy, out double forward, out double across)
    {
        double cos = render.ViewCos / 65536.0, sin = render.ViewSin / 65536.0;
        forward = dx * cos - dy * sin;
        across = dx * sin + dy * cos;
        return forward > 0;
    }

    unsafe void DrawStars(byte* dest, int pitch, int rows, StarSkyInfo info)
    {
        // wl_atmos draws for a 200-high view, its spots up to 100 rows above the horizon
        double vscale = render.BaseCenterY / 100.0;
        int bright = StarSize(render.ViewWidth);
        for (int i = 0; i < stars.Length; i++)
        {
            var (sx, sy, sz) = stars[i];
            if (!ProjectDirection(sx, sz, out double forward, out double across))
                continue;
            int shade = (int)(forward / 1024);         // wl_atmos: z >> 18, nearer brighter
            if (shade > 15)
                continue;
            int x = (int)(render.CenterX + across * render.Scale / forward);
            int y = (int)(render.CenterY - sy * 256.0 / forward * vscale);

            // a square as big as the view says, the fainter half of them a pixel smaller
            int starsize = shade < 8 ? bright : Math.Max(bright - 1, 1);
            int starleft = x - starsize / 2, startop = y - starsize / 2;
            byte color = (byte)(info.Color + shade);
            for (int py = Math.Max(startop, 0); py < Math.Min(startop + starsize, rows); py++)
                for (int px = Math.Max(starleft, 0); px < Math.Min(starleft + starsize, render.ViewWidth); px++)
                    dest[py * pitch + px] = color;
        }

        if (!info.Moon)
            return;
        double a = info.MoonAngle * Math.PI / 180;
        if (!ProjectDirection(Math.Cos(a) * 16384, -Math.Sin(a) * 16384, out double mf, out double ma) || mf < 1)
            return;
        // As big against the view at any resolution: wl_atmos' 10 pixels of a 304-wide view
        int moonsize = MoonPixels(render.ViewWidth);
        int left = (int)(render.CenterX + ma * render.Scale / mf) - moonsize / 2;
        int top = (int)(render.CenterY - render.BaseCenterY * 7 / 8.0 * 16384 / mf) - moonsize / 2;
        for (int j = 0; j < moonsize; j++)
        {
            int y = top + j;
            if ((uint)y >= (uint)rows)
                continue;
            for (int k = 0; k < moonsize; k++)
            {
                int x = left + k;
                byte pixel = Moon[j * 10 / moonsize * 10 + k * 10 / moonsize];
                if (pixel != 0 && (uint)x < (uint)render.ViewWidth)
                    dest[y * pitch + x] = pixel;
            }
        }
    }

    /// <summary>
    /// How many pixels across a bright star is, so the stars look the same at any resolution:
    /// wl_atmos' single pixel up to a 640-wide view, 2 up to 1280, 3 beyond
    /// </summary>
    internal static int StarSize(int viewWidth) => Math.Clamp((viewWidth + 639) / 640, 1, 3);

    /// <summary>How many pixels across the moon is: wl_atmos' 10 of a 304-wide view, kept at that share of any view</summary>
    internal static int MoonPixels(int viewWidth) => Math.Max((int)Math.Round(viewWidth * 10 / 304.0), 10);

    // The clouds' plane: four stories (eight half stories) above the eye
    const double CloudHalves = 8;

    unsafe void DrawClouds(byte* dest, int pitch, int rows, CloudSkyInfo info)
    {
        SetupFlatColumns();

        // where they've moved to by now, in global units
        double travel = (double)session.GameState.TimeCount * info.Speed;
        double angle = info.Angle * Math.PI / 180;
        long movex = (long)(travel * Math.Sin(angle)), movey = (long)(-travel * Math.Cos(angle));

        double numerator = render.HeightNumerator * 32.0 * CloudHalves;
        for (int y = 0; y < rows; y++)
        {
            double above = render.CenterY - (y + 0.5);
            if (above <= 0)
                continue;
            long nx = (long)(numerator / above);
            int shadeofs = lighting.Shading ? lighting.ShadeOffset(nx) : 0;
            byte* row = dest + y * pitch;
            for (int x = 0; x < render.ViewWidth; x++)
            {
                long px = render.ViewX + (nx * flatdirx[x] >> 16) + movex;
                long py = render.ViewY + (nx * flatdiry[x] >> 16) + movey;
                int u = (int)(px >> 13) & 255, v = (int)(py >> 13) & 255;
                byte pixel = clouds[(255 - u) * 256 + 255 - v];
                if (pixel != FlatMask)
                    row[x] = lighting.LightRow[shadeofs + pixel];
            }
        }
    }
}
