using PFWolf.Assets;
using PFWolf.Configuration;
using PFWolf.Constants;

namespace PFWolf.Rendering;

/// <summary>What the particles need from the game</summary>
/// <param name="Detail">The player's Particles setting (Options → Video, r_particles)</param>
/// <param name="SkyOver">Whether a tile is open to the sky above (no ceiling for particles to meet)</param>
/// <param name="DoorOpen">Whether a door (its number) is open far enough for particles to go through</param>
/// <param name="Fraction">How far the frame is between the last two tics (FrameInterpolation), 1 when it's not</param>
internal sealed record ParticleHooks(
    Func<ParticleDetail> Detail,
    Func<int, int, bool> SkyOver,
    Func<int, bool> DoorOpen,
    Func<double> Fraction);

/*
=============================================================================

                                PARTICLES

Particles (particles.yaml types, thrown out by its emitters) are only for show, like rain
and snow: they have random numbers of their own (never the game's), aren't saved and aren't
sent to anyone, so demos and net games play the same with them or without. A loaded game
starts with none in the air.

They move on the game's tics (Tick, while the world isn't paused), each falling by its
gravity, slowed by its drag and meeting walls, shut doors, the floor and the ceiling as its
collide says, and are drawn between tics where the frame is (Project). Positions are in
texels: 64 a tile, x east, y south, z up from the floor.

How many there can be, and how many an emitter throws out, follows the Particles setting.

=============================================================================
*/

internal sealed class Particles(
    AssetManager assets,
    VideoManager video,
    MapManager map,
    Lighting lighting,
    RenderState render,
    PointDrawing points,
    ParticleHooks hooks)
{
    const float TexelsPerTile = GameConstants.TextureSize;
    const int TexelGlobal = (int)MapConstants.TileGlobal / GameConstants.TextureSize;   // global units a texel
    const long NearParticle = MapConstants.TileGlobal / 8;     // nothing's drawn nearer the view than this
    const long FarParticle = 32 * MapConstants.TileGlobal;     // nor farther
    const int RampSteps = 16;                                  // colors a particle goes through over its life
    const float DoorThreshold = 0xC000;

    internal enum CollideMode : byte { Die, Bounce, Stop, None }

    /// <summary>A particles.yaml type, read once for drawing and moving</summary>
    internal sealed class ResolvedType
    {
        public required string Name;
        public PointDrawing.Shape Shape;
        public float Size, SizeEnd;
        public byte[] Ramp = [];            // RampSteps palette indices, first to last of its life
        public ParticleRange Life;
        public float Gravity, Keep;          // Keep: the share of speed kept each tic (1 - drag)
        public bool Bright;
        public CollideMode Collide;
        public float Bounce, Trail;
    }

    struct Particle
    {
        public float X, Y, Z;               // where it is (texels)
        public float VX, VY, VZ;            // how fast it goes (texels a tic)
        public float PX, PY, PZ;            // where it was before the last batch of tics, to draw between
        public float Age, Life;             // tics
        public bool Stopped;                // collide: stop, and it met something
        public ResolvedType Type;
    }

    Particle[] pool = new Particle[MaxFor(ParticleDetail.High)];
    int count;
    readonly Random random = new();      // the view's own, never the game's

    /// <summary>How many particles there are now</summary>
    internal int Count => count;

    /// <summary>Where particle i is (texels), and how fast it's going up</summary>
    internal (float X, float Y, float Z, float VZ) Position(int i) => (pool[i].X, pool[i].Y, pool[i].Z, pool[i].VZ);

    /// <summary>Whether there's anything to draw this frame</summary>
    internal bool Active => count > 0 && hooks.Detail() != ParticleDetail.Off;

    /// <summary>The most particles there can be at a detail setting</summary>
    internal static int MaxFor(ParticleDetail detail) => detail switch
    {
        ParticleDetail.Off => 0,
        ParticleDetail.Low => 1024,
        ParticleDetail.Medium => 2048,
        _ => 4096,
    };

    /// <summary>The share of an emitter's count (or rate) thrown out at a detail setting</summary>
    internal static double ShareFor(ParticleDetail detail) => detail switch
    {
        ParticleDetail.Off => 0,
        ParticleDetail.Low => 0.25,
        ParticleDetail.Medium => 0.5,
        _ => 1,
    };

    /// <summary>How many of a burst of count are thrown out at a detail setting: at least one, for a burst of any size</summary>
    internal static int ScaledCount(int count, ParticleDetail detail) =>
        count <= 0 || detail == ParticleDetail.Off ? 0 : Math.Max(1, (int)Math.Round(count * ShareFor(detail)));

    // ---- Definitions ---------------------------------------------------------------------------

    ParticlesAsset? definitions;
    bool definitionsRead;
    readonly Dictionary<string, ResolvedType?> resolved = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> warned = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The running game's particles.yaml (with its base packs' and mods' laid over it), or none</summary>
    internal ParticlesAsset Definitions
    {
        get
        {
            if (!definitionsRead)
            {
                definitions = assets.FindInGamePackIfAny<ParticlesAsset>("particles");
                definitionsRead = true;
            }
            return definitions ?? Empty;
        }
    }

    static readonly ParticlesAsset Empty = new();

    /// <summary>Takes particles.yaml as given, in place of the game's (tests, and the editor later)</summary>
    internal void UseDefinitions(ParticlesAsset asset)
    {
        definitions = asset;
        definitionsRead = true;
        resolved.Clear();
    }

    /// <summary>Clears every particle, and reads the types afresh (their colors are the palette's): a new level, life or loaded game</summary>
    internal void Reset()
    {
        count = 0;
        resolved.Clear();
    }

    /// <summary>A particle type by name, read for drawing; null (warned about once) when there's none or it can't be read</summary>
    internal ResolvedType? TypeNamed(string name)
    {
        if (resolved.TryGetValue(name, out var type))
            return type;
        type = Definitions.Types.TryGetValue(name, out var info) ? Resolve(name, info, Warn) : null;
        resolved[name] = type;
        return type;
    }

    void Warn(string message)
    {
        if (warned.Add(message))
            Console.WriteLine(message);
    }

    /// <summary>
    /// A type as it's drawn and moved; what's wrong in it is told to warn, and put right. Without
    /// colors (before the palette's there), its colors are only checked, and it has none.
    /// </summary>
    internal ResolvedType Resolve(string name, ParticleTypeInfo info, Action<string> warn, bool colors = true)
    {
        var shape = info.Shape?.Trim().ToLowerInvariant() switch
        {
            "dot" or null or "" => PointDrawing.Shape.Dot,
            "square" => PointDrawing.Shape.Square,
            "streak" => PointDrawing.Shape.Streak,
            var other => Warned(PointDrawing.Shape.Dot, $"particles.yaml type {name}: shape {other} isn't dot, square or streak; drawing a dot"),
        };
        var collide = info.Collide?.Trim().ToLowerInvariant() switch
        {
            "die" or null or "" => CollideMode.Die,
            "bounce" => CollideMode.Bounce,
            "stop" => CollideMode.Stop,
            "none" => CollideMode.None,
            var other => Warned(CollideMode.Die, $"particles.yaml type {name}: collide {other} isn't die, bounce, stop or none; it dies"),
        };
        if (!ParticleRange.TryParse(info.Life, out var life) || life.Max <= 0)
        {
            warn($"particles.yaml type {name}: life {info.Life} isn't a number of tics or a range (20-35); it lasts 35");
            life = new ParticleRange(35, 35);
        }

        var named = info.Colors is { Count: > 0 } ? info.Colors : [info.Color ?? ""];
        var rgbs = named.Select(color => ColorRgb(name, color, warn, colors)).ToList();

        return new ResolvedType
        {
            Name = name,
            Shape = shape,
            Size = (float)Math.Max(info.Size, 0),
            SizeEnd = (float)Math.Max(info.SizeEnd ?? info.Size, 0),
            Ramp = colors ? Ramp(rgbs) : [],
            Life = new ParticleRange(Math.Max(life.Min, 1), Math.Max(life.Max, 1)),
            Gravity = (float)info.Gravity,
            Keep = (float)(1 - Math.Clamp(info.Drag, 0, 1)),
            Bright = info.Bright,
            Collide = collide,
            Bounce = (float)Math.Clamp(info.Bounce, 0, 1),
            Trail = (float)Math.Max(info.Trail, 0),
        };

        T Warned<T>(T value, string message)
        {
            warn(message);
            return value;
        }
    }

    // A color as red, green and blue: #RRGGBB or a palette index (looked up in the palette when lookup)
    (byte R, byte G, byte B) ColorRgb(string type, string? color, Action<string> warn, bool lookup)
    {
        var text = color?.Trim() ?? "";
        if (int.TryParse(text, out int index) && index is >= 0 and <= 255)
            return lookup && video != null ? video.PaletteRgb(index) : ((byte)0, (byte)0, (byte)0);
        if (text is { Length: 7 } && text[0] == '#' && int.TryParse(text.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out int rgb))
            return ((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        warn($"particles.yaml type {type}: color {color} isn't #RRGGBB or a palette index (0-255); it's white");
        return (255, 255, 255);
    }

    // The colors' palette indices over a life: evenly from first to last, each step the nearest
    // palette color to the mix of the two it's between
    byte[] Ramp(List<(byte R, byte G, byte B)> rgbs)
    {
        var ramp = new byte[RampSteps];
        for (int step = 0; step < RampSteps; step++)
        {
            double at = rgbs.Count == 1 ? 0 : step / (double)(RampSteps - 1) * (rgbs.Count - 1);
            int from = Math.Min((int)at, rgbs.Count - 1), to = Math.Min(from + 1, rgbs.Count - 1);
            double t = at - from;
            static byte Mix(byte a, byte b, double t) => (byte)Math.Round(a + (b - a) * t);
            // (no palette in a test: white's index in Wolf3D's)
            ramp[step] = video?.NearestPaletteIndex(Mix(rgbs[from].R, rgbs[to].R, t), Mix(rgbs[from].G, rgbs[to].G, t),
                Mix(rgbs[from].B, rgbs[to].B, t)) ?? 15;
        }
        return ramp;
    }

    /// <summary>
    /// What's wrong in particles.yaml, for saying once at startup (with the signon warnings): the
    /// same won't be said again as the types are first used
    /// </summary>
    internal List<string> CheckDefinitions()
    {
        var problems = new List<string>();
        var defs = Definitions;
        foreach (var (name, info) in defs.Types)
            Resolve(name, info, problems.Add, colors: false);
        foreach (var (name, emitter) in defs.Emitters)
        {
            if (string.IsNullOrWhiteSpace(emitter.Type))
                problems.Add($"particles.yaml emitter {name} has no type");
            else if (!defs.Types.ContainsKey(emitter.Type))
                problems.Add($"particles.yaml emitter {name}: type {emitter.Type} isn't in particles.yaml types");
            if (!ParticleRange.TryParse(emitter.Speed, out _))
                problems.Add($"particles.yaml emitter {name}: speed {emitter.Speed} isn't a number or a range (1-3)");
        }
        warned.UnionWith(problems);
        return problems;
    }

    // ---- Spawning ------------------------------------------------------------------------------

    /// <summary>
    /// A burst from an emitter (a particles.yaml emitters name) or of a type (types name, thrown
    /// out every way at its emitter defaults) at (x, y) texels, its z added to z, the emitter
    /// facing angle degrees (0 east, 90 north). howMany in place of its count; scaled by the
    /// Particles setting. How many were thrown out, or -1 when there's no such emitter or type.
    /// </summary>
    internal int Burst(string name, double x, double y, double z, double angle, int? howMany = null)
    {
        var defs = Definitions;
        EmitterInfo emitter;
        if (defs.Emitters.TryGetValue(name, out var named))
            emitter = named;
        else if (defs.Types.ContainsKey(name))
            emitter = new EmitterInfo { Type = name };
        else
            return -1;

        if (TypeNamed(emitter.Type) is not { } type)
            return -1;

        var detail = hooks.Detail();
        int wanted = ScaledCount(howMany ?? emitter.Count, detail);
        int max = MaxFor(detail);
        if (pool.Length < max)
            Array.Resize(ref pool, max);

        var speed = ParticleRange.Parse(emitter.Speed, 1);
        double heading = (angle + emitter.Angle) * Math.PI / 180, pitch = emitter.Pitch * Math.PI / 180;
        var (dx, dy, dz) = (Math.Cos(pitch) * Math.Cos(heading), -Math.Cos(pitch) * Math.Sin(heading), Math.Sin(pitch));
        double cosSpread = Math.Cos(Math.Clamp(emitter.Spread, 0, 180) * Math.PI / 180);

        int made = 0;
        for (; made < wanted && count < max; made++)
        {
            // somewhere in a disc of radius round the emitter
            double r = emitter.Radius * Math.Sqrt(random.NextDouble()), around = random.NextDouble() * Math.PI * 2;
            float px = (float)(x + r * Math.Cos(around)), py = (float)(y + r * Math.Sin(around)), pz = (float)(z + emitter.Z);

            var (vx, vy, vz) = InCone(dx, dy, dz, cosSpread);
            double s = speed.At(random.NextDouble());
            pool[count++] = new Particle
            {
                X = px, Y = py, Z = pz, PX = px, PY = py, PZ = pz,
                VX = (float)(vx * s), VY = (float)(vy * s), VZ = (float)(vz * s),
                Life = (float)type.Life.At(random.NextDouble()),
                Type = type,
            };
        }
        return made;
    }

    // A way within the cone round (dx, dy, dz) (a unit vector) whose half-angle's cosine is cosSpread, evenly over it
    (double X, double Y, double Z) InCone(double dx, double dy, double dz, double cosSpread)
    {
        double cosTheta = 1 - random.NextDouble() * (1 - cosSpread);
        double sinTheta = Math.Sqrt(Math.Max(0, 1 - cosTheta * cosTheta));
        double phi = random.NextDouble() * Math.PI * 2;

        // two ways square to it, and to each other
        var (ax, ay, az) = Math.Abs(dz) < 0.9 ? (0.0, 0.0, 1.0) : (1.0, 0.0, 0.0);
        double e1x = ay * dz - az * dy, e1y = az * dx - ax * dz, e1z = ax * dy - ay * dx;
        double len = Math.Sqrt(e1x * e1x + e1y * e1y + e1z * e1z);
        e1x /= len; e1y /= len; e1z /= len;
        double e2x = dy * e1z - dz * e1y, e2y = dz * e1x - dx * e1z, e2z = dx * e1y - dy * e1x;

        double c = Math.Cos(phi) * sinTheta, sn = Math.Sin(phi) * sinTheta;
        return (dx * cosTheta + e1x * c + e2x * sn, dy * cosTheta + e1y * c + e2y * sn, dz * cosTheta + e1z * c + e2z * sn);
    }

    // ---- Moving --------------------------------------------------------------------------------

    /// <summary>Moves every particle on by the tics the game ran, and lets go of those whose time is up</summary>
    internal void Tick(uint tics)
    {
        if (hooks.Detail() == ParticleDetail.Off)
        {
            count = 0;
            return;
        }

        for (int i = count - 1; i >= 0; i--)
        {
            ref var p = ref pool[i];
            p.PX = p.X;
            p.PY = p.Y;
            p.PZ = p.Z;

            bool alive = true;
            for (uint t = 0; t < tics && alive; t++)
                alive = Step(ref p);
            if (!alive)
                pool[i] = pool[--count];        // the last in its place: order doesn't matter
        }
    }

    // One tic of a particle's life; false once it's over
    bool Step(ref Particle p)
    {
        if (++p.Age >= p.Life)
            return false;
        if (p.Stopped)
            return true;

        var type = p.Type;
        p.VZ -= type.Gravity;
        p.VX *= type.Keep;
        p.VY *= type.Keep;
        p.VZ *= type.Keep;

        float nx = p.X + p.VX, ny = p.Y + p.VY, nz = p.Z + p.VZ;
        if (type.Collide == CollideMode.None)
        {
            (p.X, p.Y, p.Z) = (nx, ny, nz);
            return true;
        }

        // Walls and shut doors
        if (Blocked(nx, ny, nz))
        {
            switch (type.Collide)
            {
                case CollideMode.Die:
                    return false;
                case CollideMode.Stop:
                    p.Stopped = true;
                    return true;
            }

            // Off whichever way it hit: the side it crossed into a wall on (a corner: both)
            bool hitx = Blocked(nx, p.Y, nz), hity = Blocked(p.X, ny, nz);
            if (!hitx && !hity)
                hitx = hity = true;
            if (hitx)
            {
                p.VX = -p.VX * type.Bounce;
                nx = p.X;
            }
            if (hity)
            {
                p.VY = -p.VY * type.Bounce;
                ny = p.Y;
            }
        }

        // The floor
        if (nz < 0)
        {
            switch (type.Collide)
            {
                case CollideMode.Die:
                    return false;
                case CollideMode.Stop:
                    (p.X, p.Y, p.Z) = (nx, ny, 0);
                    p.Stopped = true;
                    return true;
            }

            nz = -nz * type.Bounce;
            p.VZ = -p.VZ * type.Bounce;
            if (Math.Abs(p.VZ) <= Math.Max(Math.Abs(type.Gravity) * 2, 0.05f))
            {
                // too slow to bounce again: it lies on the floor, sliding to a stop
                nz = 0;
                p.VZ = 0;
                p.VX *= 0.8f;
                p.VY *= 0.8f;
            }
        }

        // The ceiling, where there's no sky
        float ceiling = CeilingAt(nx, ny);
        if (nz > ceiling)
        {
            switch (type.Collide)
            {
                case CollideMode.Die:
                    return false;
                case CollideMode.Stop:
                    (p.X, p.Y, p.Z) = (nx, ny, ceiling);
                    p.Stopped = true;
                    return true;
            }

            nz = Math.Max(ceiling - (nz - ceiling) * type.Bounce, 0);
            p.VZ = -p.VZ * type.Bounce;
        }

        (p.X, p.Y, p.Z) = (nx, ny, nz);
        return true;
    }

    /// <summary>Whether a point (texels) is somewhere a particle can't be: off the map, in a shut door's tile, or in a wall below its top</summary>
    internal bool Blocked(float x, float y, float z)
    {
        int tx = (int)MathF.Floor(x / TexelsPerTile), ty = (int)MathF.Floor(y / TexelsPerTile);
        if (!map.InMap(tx, ty))
            return true;
        int tile = map.TileMap[tx, ty];
        if (tile == 0)
            return false;
        if ((tile & GameConstants.BitDoor) != 0)
            return !hooks.DoorOpen(tile & ~GameConstants.BitDoor);
        return z < map.WallStories(tx, ty) * TexelsPerTile;
    }

    /// <summary>How high the ceiling is over a point (texels): the level's walls' top, or none where the sky shows</summary>
    internal float CeilingAt(float x, float y)
    {
        int tx = (int)MathF.Floor(x / TexelsPerTile), ty = (int)MathF.Floor(y / TexelsPerTile);
        if (!map.InMap(tx, ty) || hooks.SkyOver(tx, ty))
            return float.MaxValue;
        return map.LevelWallStories * TexelsPerTile;
    }

    /// <summary>Whether a door, as far open as it is (0 shut to 0xFFFF open), lets particles through</summary>
    internal static bool DoorLetsThrough(int position) => position >= DoorThreshold;

    // ---- Drawing -------------------------------------------------------------------------------

    /// <summary>Works out where this frame's particles are on screen, into the frame's points (PointDrawing.cs)</summary>
    internal void Project()
    {
        if (!Active || !points.On)
            return;

        float f = (float)Math.Clamp(hooks.Fraction(), 0, 1);
        int maxsize = Math.Max(render.ViewHeight / 8, 4);
        for (int i = 0; i < count; i++)
        {
            ref var p = ref pool[i];
            var type = p.Type;
            float x = p.PX + (p.X - p.PX) * f, y = p.PY + (p.Y - p.PY) * f, z = p.PZ + (p.Z - p.PZ) * f;
            long gx = (long)(x * TexelGlobal), gy = (long)(y * TexelGlobal);
            if (!points.ProjectPoint(gx, gy, z, out int sx, out int sy, out long nx, out double half)
                || nx < NearParticle || nx > FarParticle)
                continue;

            float life = Math.Clamp(p.Age / p.Life, 0, 1);
            double texels = type.Size + (type.SizeEnd - type.Size) * life;
            int size = Math.Clamp((int)(texels * half / (GameConstants.TextureSize / 2) + 0.5), 1, maxsize);

            byte color = type.Ramp[Math.Min((int)(life * RampSteps), RampSteps - 1)];
            if (!type.Bright)
                color = lighting.LightAt(map.TileIndexOrNone((int)(gx >> MapConstants.TileShift), (int)(gy >> MapConstants.TileShift)), gx, gy)
                    [lighting.ShadeOffset(nx) + color];

            int tx = sx, ty = sy;
            var shape = type.Shape;
            if (shape == PointDrawing.Shape.Streak)
            {
                // back along the way it's going, trail tics of it
                float tailx = x - p.VX * type.Trail, taily = y - p.VY * type.Trail, tailz = z - p.VZ * type.Trail;
                if (p.Stopped || !points.ProjectPoint((long)(tailx * TexelGlobal), (long)(taily * TexelGlobal), tailz, out tx, out ty, out _, out _))
                    (tx, ty, shape) = (sx, sy, PointDrawing.Shape.Square);
                size = Math.Min(size, 4);
            }

            points.Add(sx, sy, tx, ty, size, color, nx, shape);
        }
    }
}
