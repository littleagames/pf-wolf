using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Rendering;

/// <summary>
/// Rain and snow, as Wolf4SDL's wl_atmos has them: a set of particles in a box around the view,
/// two Distance tiles across and as tall as the level's walls (and some in a smaller box close
/// by). Each falls at its own speed, and every time it starts again from the top it's somewhere
/// new, from a hash of its number and how many times it has fallen. So its place is worked out
/// from the level's clock and the view's own seed (falling, blown by the wind, wrapped around
/// the box), there's nothing to save or keep in step, and the game's own random numbers aren't
/// touched: demos and net games play the same with or without it.
///
/// A particle isn't drawn under a ceiling flat (any, even one that's all see-through) or an arch,
/// in a door's tile, or inside a wall below its top. Walls hide it per screen column (the posts
/// the trace queued, so it shows above a short wall in front of a taller one), and it's drawn in
/// among the sprites, farthest first.
/// </summary>
internal sealed class Weather(
    GameEngineManager engine,
    VideoManager video,
    MapManager map,
    Lighting lighting,
    RenderState render,
    GameSession session,
    Func<int, int> floorRow,
    Func<int, int, bool> isArch)
{
    internal WeatherInfo? LevelWeather;      // the level's rain or snow, null for none
    internal string? LevelWeatherName;

    const int MaxWeather = 10000;
    const int TexelGlobal = (int)MapConstants.TileGlobal / GameConstants.TextureSize;    // global units a texel
    const long NearWeather = MapConstants.TileGlobal / 4;                  // nothing's drawn nearer the view than this

    //
    // A share of the particles fall in a smaller box close around the view, so there's rain right
    // in front of you even with a wall a tile or two away: in the big box, most fall out of sight
    // behind the walls.
    //
    const double NearShare = 0.3;
    const int NearBoxTiles = 6;

    // Each particle: where it starts falling (a share of the height), how it sways, how much
    // faster or slower than the rest it falls, and the seed for where each fall of it lands
    float[] weatherz = [], weatherphase = [], weatherspeed = [];
    int weatherseed;

    /// <summary>The kinds of weather there are: the built-in ones and game-info's weather-types</summary>
    internal Dictionary<string, WeatherInfo> WeatherTypes() => WeatherInfo.TypesOf(engine.GetGameInfo());

    /// <summary>Starts the named weather (null, empty or none for none); a name there's no kind of is warned about</summary>
    internal void SetWeather(string? name)
    {
        LevelWeather = null;
        LevelWeatherName = null;
        if (string.IsNullOrWhiteSpace(name) || name.Equals(WeatherInfo.None, StringComparison.OrdinalIgnoreCase))
            return;
        if (!WeatherTypes().TryGetValue(name, out var info))
        {
            Console.WriteLine($"No weather named '{name}' in game-info weather-types; the level has none");
            return;
        }

        LevelWeather = info;
        LevelWeatherName = name;

        // The view's own random numbers, never the game's: demos and net games don't see them
        int count = Math.Clamp(info.Count, 0, MaxWeather);
        var random = new Random();
        varyRandom = random;
        StartAmount(info, random.NextDouble());
        weatherseed = random.Next();
        weatherz = new float[count];
        weatherphase = new float[count];
        weatherspeed = new float[count];
        for (int i = 0; i < count; i++)
        {
            weatherz[i] = random.NextSingle();
            weatherphase[i] = random.NextSingle() * MathF.PI * 2;
            weatherspeed[i] = 0.75f + random.NextSingle() * 0.5f;     // 75% to 125% of fall-speed
        }
    }

    //
    // VARIABLE WEATHER (WeatherInfo.Vary, End of Destiny's InitializeWeatherState and
    // PollWeatherChange): the share of the particles that fall, and which way it's going. Kept on
    // the level's clock as the frames come, not saved: a loaded game starts it afresh.
    //
    internal double Amount = 1;             // the share of count falling now
    internal int AmountChange;              // +1 thickening, -1 thinning, 0 steady
    int varyFrom = -1;                      // the level's clock when the amount was last brought up to date
    double varyCheckTics;                   // tics to the next chance to start changing
    Random varyRandom = new();

    /// <summary>The amount a level starts with, from a roll of 0 to 1: none, some (5 to 25 in 100) or all</summary>
    internal void StartAmount(WeatherInfo info, double roll)
    {
        AmountChange = 0;
        varyFrom = -1;
        varyCheckTics = info.VaryEvery * 70;
        Amount = !info.Vary ? 1 : roll < 0.4 ? 0 : roll < 0.6 ? 0.05 + varyRandom.NextDouble() * 0.2 : 1;
    }

    /// <summary>Brings the amount up to the level's clock: its chances to start changing, and the change itself</summary>
    internal void UpdateAmount(WeatherInfo info, int time)
    {
        if (!info.Vary)
            return;
        if (varyFrom < 0 || time < varyFrom)
        {
            varyFrom = time;
            return;
        }
        int tics = time - varyFrom;
        varyFrom = time;
        if (tics <= 0)
            return;

        varyCheckTics -= tics;
        while (varyCheckTics <= 0)
        {
            varyCheckTics += Math.Max(info.VaryEvery, 1) * 70;
            if (varyRandom.NextDouble() < info.VaryChance)
                AmountChange = Amount <= 0 ? 1 : Amount >= 1 ? -1 : varyRandom.Next(2) == 0 ? -1 : 1;
        }

        if (AmountChange != 0)
        {
            Amount = Math.Clamp(Amount + AmountChange * info.VaryRate * tics / 70.0, 0, 1);
            if (Amount is 0 or 1)
                AmountChange = 0;
        }
    }

    // Particle i's place, 0 to 1, in an even spread over the set (the golden ratio's steps)
    internal static double SpreadPlace(int i) => i * 0.6180339887498949 % 1.0;

    // How far across particle i's box is, in global units: the near ones' is smaller
    int WeatherBox(WeatherInfo info, int i) => i < weatherz.Length * NearShare
        ? Math.Min(NearBoxTiles, Math.Clamp(info.Distance, 1, 32) * 2) * (int)MapConstants.TileGlobal
        : Math.Clamp(info.Distance, 1, 32) * 2 * (int)MapConstants.TileGlobal;

    /// <summary>
    /// Where particle i lands on its fall'th fall, somewhere in a box across (0 to box - 1, each
    /// way): a new place each time, the same every frame of that fall.
    /// </summary>
    internal (int X, int Y) WeatherSpot(int i, long fall, int box)
    {
        ulong h = Mix((ulong)(uint)weatherseed << 32 ^ (ulong)(uint)i ^ (ulong)fall * 0x9E3779B97F4A7C15UL);
        return ((int)((h & 0xFFFFFFFF) % (ulong)box), (int)((h >> 32) % (ulong)box));
    }

    // SplitMix64's finaliser: every bit of the input stirs every bit of the output
    static ulong Mix(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    internal bool WeatherOn;        // this frame has weather: the posts are noted as they're drawn

    //
    // Each column's walls, as the trace queued them: how high each is (as WallHeight, so nearer
    // is higher) and the rows it covers
    //
    short[] occheight = [], occtop = [], occbottom = [];
    byte[] occcount = [];

    /// <summary>Gets the frame's weather ready, before the walls are traced</summary>
    internal void SetupWeather()
    {
        WeatherOn = LevelWeather != null && weatherz.Length > 0;
        if (!WeatherOn || occcount.Length == render.ViewWidth)
            return;
        occcount = new byte[render.ViewWidth];
        occheight = new short[render.ViewWidth * RenderState.MaxPosts];
        occtop = new short[render.ViewWidth * RenderState.MaxPosts];
        occbottom = new short[render.ViewWidth * RenderState.MaxPosts];
    }

    /// <summary>Notes what the column's queued posts cover, before DrawPosts draws them</summary>
    internal void NoteOccluders()
    {
        int at = render.PixX * RenderState.MaxPosts;
        for (int i = 0; i < render.PostCount; i++, at++)
        {
            // the rows DrawPost fills
            int half = render.PostHeights[i] >> 3;
            if (half <= 0) half = 100;
            int floor = floorRow(half);
            int top = floor - render.PostStories[i] * 2 * half;
            int bottom = floor - 1 - render.PostFirstStory[i] * 2 * half;

            // an arch's underside is above all that's past it too, up to where the trace leaves it
            if (render.PostUnderside[i] != 0)
            {
                int exithalf = render.PostUnderside[i] >> 3;
                bottom = Math.Max(bottom, floorRow(exithalf) - 2 * exithalf - 1);
            }

            occheight[at] = render.PostHeights[i];
            occtop[at] = (short)Math.Clamp(top, short.MinValue, short.MaxValue);
            occbottom[at] = (short)Math.Clamp(bottom, short.MinValue, short.MaxValue);
        }
        occcount[render.PixX] = (byte)render.PostCount;
    }

    // Whether a wall nearer than something height high (as WallHeight) covers row y of column x
    bool Occluded(int x, int y, int height)
    {
        int at = x * RenderState.MaxPosts;
        for (int end = at + occcount[x]; at < end; at++)
            if (occheight[at] > height && y >= occtop[at] && y <= occbottom[at])
                return true;
        return false;
    }

    /// <summary>
    /// Whether a particle at (gx, gy) (global units), z texels up, is somewhere it can't be: under
    /// a ceiling flat or an arch, in a door's tile, or inside a wall
    /// </summary>
    bool WeatherCovered(long gx, long gy, double z)
    {
        long tx = gx >> MapConstants.TileShift, ty = gy >> MapConstants.TileShift;
        if ((ulong)tx >= (ulong)map.MapWidth || (ulong)ty >= (ulong)map.MapHeight)
            return false;                           // off the map, it falls on the world outside

        int x = (int)tx, y = (int)ty;
        int tile = map.TileMap[x, y];
        if ((tile & GameConstants.BitDoor) != 0)
            return true;
        if (tile != 0)
            return z < map.WallStories(x, y) * GameConstants.TextureSize;
        if (map.CeilingFlat[map.TileIndex(x, y)] != null)
            return true;
        return isArch(x, y) && z < map.StoryMap[x, y] * GameConstants.TextureSize;
    }

    struct WeatherDrop
    {
        public short X0, Y0, X1, Y1;    // the head, and a streak's tail (a flake's the same)
        public byte Size;               // pixels across (a splash's: how far it has spread)
        public byte Color;
        public bool Splash;
    }

    const int SplashTics = 8;           // how long a splash lasts

    /// <summary>
    /// How far the wind has carried things by tic t, in global units. Gusts make it rise and fall
    /// around its strength, so this is its integral: steady drift less a wave.
    /// </summary>
    (long X, long Y) WindDrift(WeatherInfo info, double t)
    {
        double travel = t;
        if (info.Gust != 0 && info.GustTics > 0)
        {
            double w = 2 * Math.PI / info.GustTics;
            travel -= info.Gust * Math.Cos(w * t) / w;
        }
        return ((long)(info.WindX * TexelGlobal * travel), (long)(info.WindY * TexelGlobal * travel));
    }

    // The wind's strength at tic t, as a share of wind-x/wind-y
    internal static double WindStrength(WeatherInfo info, double t) =>
        info.Gust != 0 && info.GustTics > 0 ? 1 + info.Gust * Math.Sin(2 * Math.PI / info.GustTics * t) : 1;

    // Puts (gx, gy) back in the box around the view
    (long X, long Y) InWeatherBox(long gx, long gy, long originx, long originy, int box) =>
        (originx + PositiveMod(gx - originx, box), originy + PositiveMod(gy - originy, box));

    // Its color at (gx, gy), nx along the view: in the light there, and dimmed toward black with distance (to half)
    byte WeatherColor(byte basecolor, byte[] dim, long gx, long gy, long nx, long maxdist)
    {
        byte color = lighting.LightAt(map.TileIndexOrNone((int)(gx >> MapConstants.TileShift), (int)(gy >> MapConstants.TileShift)), gx, gy)
            [lighting.ShadeOffset(nx) + basecolor];
        int step = (int)(nx * (Lighting.FadeSteps / 2) / maxdist);
        return dim[Math.Min(step, Lighting.FadeSteps - 1) * 256 + color];
    }

    void AddWeatherDrop(int x0, int y0, int x1, int y1, int size, byte color, long nx, bool splash)
    {
        // Off the screen altogether
        if (Math.Max(x0, x1) + size < 0 || Math.Min(x0, x1) - size >= render.ViewWidth
            || Math.Max(y0, y1) + size < 0 || Math.Min(y0, y1) - size >= render.ViewHeight)
            return;

        weatherdrops[weathercount] = new WeatherDrop
        {
            X0 = (short)Math.Clamp(x0, short.MinValue, short.MaxValue), Y0 = (short)Math.Clamp(y0, short.MinValue, short.MaxValue),
            X1 = (short)Math.Clamp(x1, short.MinValue, short.MaxValue), Y1 = (short)Math.Clamp(y1, short.MinValue, short.MaxValue),
            Size = (byte)size, Color = color, Splash = splash,
        };
        weatherkeys[weathercount] = (int)Math.Min(render.HeightNumerator * 256L / nx, int.MaxValue);
        weathercount++;
    }

    WeatherDrop[] weatherdrops = [];
    int[] weatherkeys = [];      // each drop's height (as WallHeight), to sort them farthest first
    int weathercount, weathernext;

    /// <summary>Works out where this frame's particles are on screen, farthest first</summary>
    internal void ProjectWeather()
    {
        weathercount = weathernext = 0;
        if (!WeatherOn || LevelWeather is not { } info)
            return;

        int all = weatherz.Length;
        if (weatherdrops.Length < all * 2)
        {
            weatherdrops = new WeatherDrop[all * 2];      // each drop, and its splash
            weatherkeys = new int[all * 2];
        }

        bool snow = info.Kind.Equals("snow", StringComparison.OrdinalIgnoreCase);
        bool splashes = !snow && info.Splashes;
        int time = session.GameState.TimeCount;

        // as much of it as is falling now (variable weather): every particle has a place 0 to 1 in an
        // even spread across the set (near and far alike), and those placed below the amount fall
        UpdateAmount(info, time);
        long maxdist = (long)Math.Clamp(info.Distance, 1, 32) * MapConstants.TileGlobal;

        // Below the ceiling, if any tile has a ceiling flat it could show over; otherwise up past the tallest walls
        double top = map.HasCeilingFlats
            ? map.LevelWallStories * GameConstants.TextureSize
            : (map.MaxWallStories + 1) * GameConstants.TextureSize;
        double fall = Math.Max(info.FallSpeed, 0.01);
        var (windx, windy) = WindDrift(info, time);

        double gust = WindStrength(info, time);
        double nowx = info.WindX * gust, nowy = info.WindY * gust;

        byte basecolor = video.ParseColorOr(info.Color, 0x0f);
        var dim = video.GetLightRow(Lighting.LightLevels - 1, Lighting.LightLevels, Lighting.FadeSteps, 0, 0, 0);
        int maxstreak = Math.Max(render.ViewHeight / 4, 4);     // a streak right by the view is cut to this many rows

        for (int i = 0; i < all; i++)
        {
            if (Amount < 1 && SpreadPlace(i) >= Amount)
                continue;
            int box = WeatherBox(info, i);
            long originx = session.Camera.X - box / 2, originy = session.Camera.Y - box / 2;

            // How far it has fallen since it last started from the top, and how many times it has
            // fallen: each time from a new place
            double dropfall = fall * weatherspeed[i];
            double travelled = weatherz[i] * top + dropfall * time;
            long falls = (long)Math.Floor(travelled / top);
            double fallen = travelled - falls * top;
            if (splashes && fallen < dropfall * SplashTics)
                AddSplash(info, i, falls - 1, time - fallen / dropfall, fallen / dropfall, originx, originy, box, basecolor, dim, maxdist);

            double z = top - fallen;
            var (spotx, spoty) = WeatherSpot(i, falls, box);
            long gx = spotx + windx, gy = spoty + windy;
            if (snow && info.Drift != 0)
            {
                // each flake sways on its own
                gx += (long)(info.Drift * TexelGlobal * Math.Sin(time * 0.031 + weatherphase[i]));
                gy += (long)(info.Drift * TexelGlobal * Math.Cos(time * 0.023 + weatherphase[i] * 1.7));
            }
            (gx, gy) = InWeatherBox(gx, gy, originx, originy, box);

            if (WeatherCovered(gx, gy, z))
                continue;
            // Not right in the view's face, where a streak would cross half the screen
            if (!ProjectPoint(gx, gy, z, out int x0, out int y0, out long nx, out double half) || nx > maxdist || nx < NearWeather)
                continue;

            int x1 = x0, y1 = y0, size;
            if (snow)
                size = Math.Clamp((int)(info.Size * half / (GameConstants.TextureSize / 2) + 0.5), 1, 8);
            else
            {
                // It trails back along the way it's going (with the wind as it is now): faster
                // drops make longer streaks
                double speed = Math.Sqrt(nowx * nowx + nowy * nowy + dropfall * dropfall);
                double length = info.Length * weatherspeed[i];
                if (!ProjectPoint(gx - (long)(nowx / speed * length * TexelGlobal), gy - (long)(nowy / speed * length * TexelGlobal),
                        z + dropfall / speed * length, out x1, out y1, out _, out _))
                    continue;

                int rows = Math.Abs(y1 - y0);
                if (rows > maxstreak)
                {
                    x1 = x0 + (x1 - x0) * maxstreak / rows;
                    y1 = y0 + (y1 - y0) * maxstreak / rows;
                }
                size = Math.Clamp((int)(half / (GameConstants.TextureSize * 2)), 1, 3);
            }

            AddWeatherDrop(x0, y0, x1, y1, size, WeatherColor(basecolor, dim, gx, gy, nx, maxdist), nx, false);
        }

        Array.Sort(weatherkeys, weatherdrops, 0, weathercount);
    }

    /// <summary>
    /// Drop i's splash: it landed at tic landed, age tics ago, where the wind had carried it then.
    /// Only on open floor: not in a wall or door, nor under a roof (it never got there), nor off the map.
    /// </summary>
    void AddSplash(WeatherInfo info, int i, long fall, double landed, double age, long originx, long originy, int box,
        byte basecolor, byte[] dim, long maxdist)
    {
        var (windx, windy) = WindDrift(info, landed);
        var (spotx, spoty) = WeatherSpot(i, fall, box);
        var (gx, gy) = InWeatherBox(spotx + windx, spoty + windy, originx, originy, box);
        long tx = gx >> MapConstants.TileShift, ty = gy >> MapConstants.TileShift;
        if ((ulong)tx >= (ulong)map.MapWidth || (ulong)ty >= (ulong)map.MapHeight || WeatherCovered(gx, gy, 0))
            return;
        if (!ProjectPoint(gx, gy, 0, out int sx, out int sy, out long nx, out double half) || nx > maxdist || nx < NearWeather)
            return;

        // it spreads to about 3 texels across as it goes
        int spread = Math.Clamp((int)(3 * half / (GameConstants.TextureSize / 2) * (age + 1) / SplashTics + 0.5), 1, 12);
        AddWeatherDrop(sx, sy, sx, sy, spread, WeatherColor(basecolor, dim, gx, gy, nx, maxdist), nx, true);
    }

    internal static long PositiveMod(long value, long modulus)
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
    internal bool ProjectPoint(long gx, long gy, double z, out int sx, out int sy, out long nx, out double half)
    {
        long dx = gx - render.ViewX, dy = gy - render.ViewY;
        nx = (dx * render.ViewCos >> 16) - (dy * render.ViewSin >> 16);
        long ny = (dx * render.ViewSin >> 16) + (dy * render.ViewCos >> 16);
        if (nx < GameConstants.MinDist)
        {
            sx = sy = 0;
            half = 0;
            return false;
        }

        sx = (int)Math.Clamp(render.CenterX + ny * render.Scale / nx, -100000, 100000);
        half = render.HeightNumerator * 32.0 / nx;
        sy = (int)Math.Clamp(render.CenterY + half * (render.ViewEyeZ - z) / (GameConstants.TextureSize / 2), -100000, 100000);
        return true;
    }

    /// <summary>Draws the particles farther away than something height high (as WallHeight), farthest first</summary>
    internal void DrawWeatherBeyond(int height)
    {
        if (weathernext >= weathercount || weatherkeys[weathernext] >= height)
            return;

        int pitch = (int)video.BufferPitch;
        unsafe
        {
            byte* dest = (byte*)render.VbufPtr + render.ScreenOfs;
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
                int steps = Math.Min(Math.Max(Math.Abs(dx), Math.Abs(dy)), render.ViewHeight);
                for (int s = 0; s <= steps; s++)
                {
                    int px = drop.X0 + (steps == 0 ? 0 : dx * s / steps);
                    int py = drop.Y0 + (steps == 0 ? 0 : dy * s / steps);
                    if ((uint)py >= (uint)render.ViewHeight)
                        continue;
                    for (int x = Math.Max(px, 0); x < Math.Min(px + drop.Size, render.ViewWidth); x++)
                        if (!Occluded(x, py, key))
                            dest[py * pitch + x] = drop.Color;
                }
            }
        }
    }

    // A size by size square from (left, top), where no wall nearer than height covers it
    unsafe void DrawWeatherDot(byte* dest, int pitch, int left, int top, int size, byte color, int height)
    {
        for (int y = Math.Max(top, 0); y < Math.Min(top + size, render.ViewHeight); y++)
            for (int x = Math.Max(left, 0); x < Math.Min(left + size, render.ViewWidth); x++)
                if (!Occluded(x, y, height))
                    dest[y * pitch + x] = color;
    }
}
