using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Managers;

namespace PFWolf;

internal partial class Program
{
    /*
    =============================================================================

                                    WEATHER

    Rain and snow, as Wolf4SDL's wl_atmos has them: a fixed set of particles in a box around the
    view, two Distance tiles across and as tall as the level's walls. Each one's place is worked
    out from the level's clock alone (falling, blown by the wind, wrapped around the box), so
    there's nothing to save or keep in step: the game's own random numbers aren't touched, and
    demos and net games play the same with or without it.

    A particle isn't drawn under a ceiling flat (any, even one that's all see-through) or an arch,
    in a door's tile, or inside a wall below its top. Walls hide it per screen column (the posts
    the trace queued, so it shows above a short wall in front of a taller one), and it's drawn in
    among the sprites, farthest first.

    =============================================================================
    */

    internal static WeatherInfo? levelweather;      // the level's rain or snow, null for none
    internal static string? levelweathername;

    const int MAXWEATHER = 10000;
    const int TEXELGLOBAL = (int)MapConstants.TILEGLOBAL / TEXTURESIZE;    // global units a texel
    const long NEARWEATHER = MapConstants.TILEGLOBAL / 2;                  // nothing's drawn nearer the view than this

    // Each particle's place in the box at time 0: x, y in global units, z as a share of the height
    static int[] weatherx = [], weathery = [];
    static float[] weatherz = [], weatherphase = [];

    /// <summary>The kinds of weather there are: the built-in ones and game-info's weather-types</summary>
    internal static Dictionary<string, WeatherInfo> WeatherTypes() => WeatherInfo.TypesOf(_gameEngineManager.GetGameInfo());

    /// <summary>Starts the named weather (null, empty or none for none); a name there's no kind of is warned about</summary>
    internal static void SetWeather(string? name)
    {
        levelweather = null;
        levelweathername = null;
        if (string.IsNullOrWhiteSpace(name) || name.Equals(WeatherInfo.None, StringComparison.OrdinalIgnoreCase))
            return;
        if (!WeatherTypes().TryGetValue(name, out var info))
        {
            Console.WriteLine($"No weather named '{name}' in game-info weather-types; the level has none");
            return;
        }

        levelweather = info;
        levelweathername = name;

        // Always the same particles for a kind, from its own seed
        int count = Math.Clamp(info.Count, 0, MAXWEATHER);
        int box = WeatherBox(info);
        var random = new Random(StringComparer.OrdinalIgnoreCase.GetHashCode(name) & 0x7fff ^ count);
        weatherx = new int[count];
        weathery = new int[count];
        weatherz = new float[count];
        weatherphase = new float[count];
        for (int i = 0; i < count; i++)
        {
            weatherx[i] = random.Next(box);
            weathery[i] = random.Next(box);
            weatherz[i] = random.NextSingle();
            weatherphase[i] = random.NextSingle() * MathF.PI * 2;
        }
    }

    // How far across the box is, in global units
    static int WeatherBox(WeatherInfo info) => Math.Clamp(info.Distance, 1, 32) * 2 * (int)MapConstants.TILEGLOBAL;

    static bool weatheron;          // this frame has weather: the posts are noted as they're drawn

    //
    // Each column's walls, as the trace queued them: how high each is (as wallheight, so nearer
    // is higher) and the rows it covers
    //
    static short[] occheight = [], occtop = [], occbottom = [];
    static byte[] occcount = [];

    /// <summary>Gets the frame's weather ready, before the walls are traced</summary>
    static void SetupWeather()
    {
        weatheron = levelweather != null && weatherx.Length > 0;
        if (!weatheron || occcount.Length == viewwidth)
            return;
        occcount = new byte[viewwidth];
        occheight = new short[viewwidth * MAXPOSTS];
        occtop = new short[viewwidth * MAXPOSTS];
        occbottom = new short[viewwidth * MAXPOSTS];
    }

    /// <summary>Notes what the column's queued posts cover, before DrawPosts draws them</summary>
    static void NoteOccluders()
    {
        int at = pixx * MAXPOSTS;
        for (int i = 0; i < postcount; i++, at++)
        {
            // the rows DrawPost fills
            int half = postheights[i] >> 3;
            if (half <= 0) half = 100;
            int floor = FloorRow(half);
            int top = floor - poststories[i] * 2 * half;
            int bottom = floor - 1 - postfirststory[i] * 2 * half;

            // an arch's underside is above all that's past it too, up to where the trace leaves it
            if (postunderside[i] != 0)
            {
                int exithalf = postunderside[i] >> 3;
                bottom = Math.Max(bottom, FloorRow(exithalf) - 2 * exithalf - 1);
            }

            occheight[at] = postheights[i];
            occtop[at] = (short)Math.Clamp(top, short.MinValue, short.MaxValue);
            occbottom[at] = (short)Math.Clamp(bottom, short.MinValue, short.MaxValue);
        }
        occcount[pixx] = (byte)postcount;
    }

    // Whether a wall nearer than something height high (as wallheight) covers row y of column x
    static bool Occluded(int x, int y, int height)
    {
        int at = x * MAXPOSTS;
        for (int end = at + occcount[x]; at < end; at++)
            if (occheight[at] > height && y >= occtop[at] && y <= occbottom[at])
                return true;
        return false;
    }

    /// <summary>
    /// Whether a particle at (gx, gy) (global units), z texels up, is somewhere it can't be: under
    /// a ceiling flat or an arch, in a door's tile, or inside a wall
    /// </summary>
    static bool WeatherCovered(long gx, long gy, double z)
    {
        long tx = gx >> MapConstants.TILESHIFT, ty = gy >> MapConstants.TILESHIFT;
        if ((ulong)tx >= (ulong)_mapManager.mapwidth || (ulong)ty >= (ulong)_mapManager.mapheight)
            return false;                           // off the map, it falls on the world outside

        int x = (int)tx, y = (int)ty;
        byte tile = _mapManager.tilemap[x, y];
        if ((tile & BIT_DOOR) != 0)
            return true;
        if (tile != 0)
            return z < _mapManager.WallStories(x, y) * TEXTURESIZE;
        if (_mapManager.ceilingflat[(y << MapManager.MAPSHIFT) + x] != null)
            return true;
        return IsArch(x, y) && z < _mapManager.storymap[x, y] * TEXTURESIZE;
    }

    struct WeatherDrop
    {
        public short X0, Y0, X1, Y1;    // the head, and a streak's tail (a flake's the same)
        public byte Size;               // pixels across (a splash's: how far it has spread)
        public byte Color;
        public bool Splash;
    }

    const int SPLASHTICS = 8;           // how long a splash lasts

    /// <summary>
    /// How far the wind has carried things by tic t, in global units. Gusts make it rise and fall
    /// around its strength, so this is its integral: steady drift less a wave.
    /// </summary>
    static (long X, long Y) WindDrift(WeatherInfo info, double t)
    {
        double travel = t;
        if (info.Gust != 0 && info.GustTics > 0)
        {
            double w = 2 * Math.PI / info.GustTics;
            travel -= info.Gust * Math.Cos(w * t) / w;
        }
        return ((long)(info.WindX * TEXELGLOBAL * travel), (long)(info.WindY * TEXELGLOBAL * travel));
    }

    // The wind's strength at tic t, as a share of wind-x/wind-y
    static double WindStrength(WeatherInfo info, double t) =>
        info.Gust != 0 && info.GustTics > 0 ? 1 + info.Gust * Math.Sin(2 * Math.PI / info.GustTics * t) : 1;

    // Puts (gx, gy) back in the box around the view
    static (long X, long Y) InWeatherBox(long gx, long gy, long originx, long originy, int box) =>
        (originx + PositiveMod(gx - originx, box), originy + PositiveMod(gy - originy, box));

    // Its color at (gx, gy), nx along the view: in the light there, and dimmed toward black with distance (to half)
    static byte WeatherColor(byte basecolor, byte[] dim, long gx, long gy, long nx, long maxdist)
    {
        byte color = LightAt(TileIndex((int)(gx >> MapConstants.TILESHIFT), (int)(gy >> MapConstants.TILESHIFT)), gx, gy)
            [ShadeOffset(nx) + basecolor];
        int step = (int)(nx * (FADESTEPS / 2) / maxdist);
        return dim[Math.Min(step, FADESTEPS - 1) * 256 + color];
    }

    static void AddWeatherDrop(int x0, int y0, int x1, int y1, int size, byte color, long nx, bool splash)
    {
        // Off the screen altogether
        if (Math.Max(x0, x1) + size < 0 || Math.Min(x0, x1) - size >= viewwidth
            || Math.Max(y0, y1) + size < 0 || Math.Min(y0, y1) - size >= viewheight)
            return;

        weatherdrops[weathercount] = new WeatherDrop
        {
            X0 = (short)Math.Clamp(x0, short.MinValue, short.MaxValue), Y0 = (short)Math.Clamp(y0, short.MinValue, short.MaxValue),
            X1 = (short)Math.Clamp(x1, short.MinValue, short.MaxValue), Y1 = (short)Math.Clamp(y1, short.MinValue, short.MaxValue),
            Size = (byte)size, Color = color, Splash = splash,
        };
        weatherkeys[weathercount] = (int)Math.Min(heightnumerator * 256L / nx, int.MaxValue);
        weathercount++;
    }

    static WeatherDrop[] weatherdrops = [];
    static int[] weatherkeys = [];      // each drop's height (as wallheight), to sort them farthest first
    static int weathercount, weathernext;

    /// <summary>Works out where this frame's particles are on screen, farthest first</summary>
    static void ProjectWeather()
    {
        weathercount = weathernext = 0;
        if (!weatheron || levelweather is not { } info)
            return;

        int count = weatherx.Length;
        if (weatherdrops.Length < count * 2)
        {
            weatherdrops = new WeatherDrop[count * 2];      // each drop, and its splash
            weatherkeys = new int[count * 2];
        }

        bool snow = info.Kind.Equals("snow", StringComparison.OrdinalIgnoreCase);
        bool splashes = !snow && info.Splashes;
        int time = gamestate.TimeCount;
        int box = WeatherBox(info);
        long maxdist = (long)Math.Clamp(info.Distance, 1, 32) * MapConstants.TILEGLOBAL;

        // Below the ceiling, if any tile has a ceiling flat it could show over; otherwise up past the tallest walls
        double top = _mapManager.hasceilingflats
            ? wallstories * TEXTURESIZE
            : (_mapManager.MaxWallStories + 1) * TEXTURESIZE;
        double fall = Math.Max(info.FallSpeed, 0.01);
        var (windx, windy) = WindDrift(info, time);

        // A rain streak trails back along the way it's going (with the wind as it is now), length texels
        double gust = WindStrength(info, time);
        double nowx = info.WindX * gust, nowy = info.WindY * gust;
        double speed = Math.Sqrt(nowx * nowx + nowy * nowy + fall * fall);
        double tailx = -nowx / speed * info.Length * TEXELGLOBAL;
        double taily = -nowy / speed * info.Length * TEXELGLOBAL;
        double tailz = fall / speed * info.Length;

        byte basecolor = _videoManager.ParseColorOr(info.Color, 0x0f);
        var dim = _videoManager.GetLightRow(LIGHTLEVELS - 1, LIGHTLEVELS, FADESTEPS, 0, 0, 0);
        long originx = camera.X - box / 2, originy = camera.Y - box / 2;

        for (int i = 0; i < count; i++)
        {
            // how far it has fallen since it last started from the top
            double fallen = PositiveMod(weatherz[i] * top + fall * time, top);
            if (splashes && fallen < fall * SPLASHTICS)
                AddSplash(info, i, time - fallen / fall, fallen / fall, originx, originy, box, basecolor, dim, maxdist);

            double z = top - fallen;
            long gx = weatherx[i] + windx, gy = weathery[i] + windy;
            if (snow && info.Drift != 0)
            {
                // each flake sways on its own
                gx += (long)(info.Drift * TEXELGLOBAL * Math.Sin(time * 0.031 + weatherphase[i]));
                gy += (long)(info.Drift * TEXELGLOBAL * Math.Cos(time * 0.023 + weatherphase[i] * 1.7));
            }
            (gx, gy) = InWeatherBox(gx, gy, originx, originy, box);

            if (WeatherCovered(gx, gy, z))
                continue;
            // Not right in the view's face, where a streak would cross half the screen
            if (!ProjectPoint(gx, gy, z, out int x0, out int y0, out long nx, out double half) || nx > maxdist || nx < NEARWEATHER)
                continue;

            int x1 = x0, y1 = y0, size;
            if (snow)
                size = Math.Clamp((int)(info.Size * half / (TEXTURESIZE / 2) + 0.5), 1, 8);
            else
            {
                if (!ProjectPoint(gx + (long)tailx, gy + (long)taily, z + tailz, out x1, out y1, out _, out _))
                    continue;
                size = Math.Clamp((int)(half / (TEXTURESIZE * 2)), 1, 3);
            }

            AddWeatherDrop(x0, y0, x1, y1, size, WeatherColor(basecolor, dim, gx, gy, nx, maxdist), nx, false);
        }

        Array.Sort(weatherkeys, weatherdrops, 0, weathercount);
    }

    /// <summary>
    /// Drop i's splash: it landed at tic landed, age tics ago, where the wind had carried it then.
    /// Only on open floor: not in a wall or door, nor under a roof (it never got there), nor off the map.
    /// </summary>
    static void AddSplash(WeatherInfo info, int i, double landed, double age, long originx, long originy, int box,
        byte basecolor, byte[] dim, long maxdist)
    {
        var (windx, windy) = WindDrift(info, landed);
        var (gx, gy) = InWeatherBox(weatherx[i] + windx, weathery[i] + windy, originx, originy, box);
        long tx = gx >> MapConstants.TILESHIFT, ty = gy >> MapConstants.TILESHIFT;
        if ((ulong)tx >= (ulong)_mapManager.mapwidth || (ulong)ty >= (ulong)_mapManager.mapheight || WeatherCovered(gx, gy, 0))
            return;
        if (!ProjectPoint(gx, gy, 0, out int sx, out int sy, out long nx, out double half) || nx > maxdist || nx < NEARWEATHER)
            return;

        // it spreads to about 3 texels across as it goes
        int spread = Math.Clamp((int)(3 * half / (TEXTURESIZE / 2) * (age + 1) / SPLASHTICS + 0.5), 1, 12);
        AddWeatherDrop(sx, sy, sx, sy, spread, WeatherColor(basecolor, dim, gx, gy, nx, maxdist), nx, true);
    }

    static long PositiveMod(long value, long modulus)
    {
        long r = value % modulus;
        return r < 0 ? r + modulus : r;
    }

    static double PositiveMod(double value, double modulus)
    {
        double r = value % modulus;
        return r < 0 ? r + modulus : r;
    }

    /// <summary>
    /// Where a point (global units, z texels up) is on screen, as sprites are placed: nx how far
    /// along the view it is, half how many pixels half a story is there. False when it's too close
    /// or behind the view.
    /// </summary>
    static bool ProjectPoint(long gx, long gy, double z, out int sx, out int sy, out long nx, out double half)
    {
        long dx = gx - viewx, dy = gy - viewy;
        nx = (dx * viewcos >> 16) - (dy * viewsin >> 16);
        long ny = (dx * viewsin >> 16) + (dy * viewcos >> 16);
        if (nx < MINDIST)
        {
            sx = sy = 0;
            half = 0;
            return false;
        }

        sx = (int)Math.Clamp(centerx + ny * scale / nx, -100000, 100000);
        half = heightnumerator * 32.0 / nx;
        sy = (int)Math.Clamp(centery + half * (vieweyez - z) / (TEXTURESIZE / 2), -100000, 100000);
        return true;
    }

    /// <summary>Draws the particles farther away than something height high (as wallheight), farthest first</summary>
    static void DrawWeatherBeyond(int height)
    {
        if (weathernext >= weathercount || weatherkeys[weathernext] >= height)
            return;

        int pitch = (int)_videoManager.bufferPitch;
        unsafe
        {
            byte* dest = (byte*)vbufPtr + screenofs;
            for (; weathernext < weathercount && weatherkeys[weathernext] < height; weathernext++)
            {
                ref var drop = ref weatherdrops[weathernext];
                int key = weatherkeys[weathernext];
                if (drop.Splash)
                {
                    // droplets thrown out to each side and up from where it landed
                    int r = drop.Size, dot = Math.Max(r / 4, 1);
                    DrawWeatherDot(dest, pitch, drop.X0 - r, drop.Y0 - (r + 1) / 2, dot, drop.Color, key);
                    DrawWeatherDot(dest, pitch, drop.X0 + r - dot + 1, drop.Y0 - (r + 1) / 2, dot, drop.Color, key);
                    if (r > 2)
                        DrawWeatherDot(dest, pitch, drop.X0, drop.Y0 - r, dot, drop.Color, key);
                    continue;
                }

                if (drop.X0 == drop.X1 && drop.Y0 == drop.Y1)
                {
                    // a flake: a square around its middle
                    DrawWeatherDot(dest, pitch, drop.X0 - (drop.Size >> 1), drop.Y0 - (drop.Size >> 1), drop.Size, drop.Color, key);
                    continue;
                }

                // a streak, head to tail, size pixels wide
                int dx = drop.X1 - drop.X0, dy = drop.Y1 - drop.Y0;
                int steps = Math.Min(Math.Max(Math.Abs(dx), Math.Abs(dy)), viewheight);
                for (int s = 0; s <= steps; s++)
                {
                    int px = drop.X0 + (steps == 0 ? 0 : dx * s / steps);
                    int py = drop.Y0 + (steps == 0 ? 0 : dy * s / steps);
                    if ((uint)py >= (uint)viewheight)
                        continue;
                    for (int x = Math.Max(px, 0); x < Math.Min(px + drop.Size, viewwidth); x++)
                        if (!Occluded(x, py, key))
                            dest[py * pitch + x] = drop.Color;
                }
            }
        }
    }

    // A size by size square from (left, top), where no wall nearer than height covers it
    static unsafe void DrawWeatherDot(byte* dest, int pitch, int left, int top, int size, byte color, int height)
    {
        for (int y = Math.Max(top, 0); y < Math.Min(top + size, viewheight); y++)
            for (int x = Math.Max(left, 0); x < Math.Min(left + size, viewwidth); x++)
                if (!Occluded(x, y, height))
                    dest[y * pitch + x] = color;
    }
}
