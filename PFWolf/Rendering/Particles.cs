using System.Runtime.CompilerServices;
using PFWolf.Assets;
using PFWolf.Configuration;
using PFWolf.Constants;

namespace PFWolf.Rendering;

/// <summary>What the particles need from the game</summary>
/// <param name="Detail">The player's Particles setting (Options → Video, r_particles)</param>
/// <param name="SkyOver">Whether a tile is open to the sky above (no ceiling for particles to meet)</param>
/// <param name="DoorOpen">Whether a door (its number) is open far enough for particles to go through</param>
/// <param name="Fraction">How far the frame is between the last two tics (FrameInterpolation), 1 when it's not</param>
/// <param name="ViewPoint">Where the view is (global units), for how far a running emitter is from it; none: every one's near</param>
internal sealed record ParticleHooks(
    Func<ParticleDetail> Detail,
    Func<int, int, bool> SkyOver,
    Func<int, bool> DoorOpen,
    Func<double> Fraction,
    Func<(int X, int Y)>? ViewPoint = null);

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
    const double TicsPerSecond = 70;
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
        definitionsOwn = false;
        resolved.Clear();
        changedTypes.Clear();
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

        return Emit(emitter, type, x, y, z, angle, ScaledCount(howMany ?? emitter.Count, hooks.Detail()));
    }

    // wanted of an emitter's particles (already scaled by the setting) at (x, y), z added to its own, facing angle
    int Emit(EmitterInfo emitter, ResolvedType type, double x, double y, double z, double angle, int wanted)
    {
        int max = MaxFor(hooks.Detail());
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

    // ---- Actors' emitters ----------------------------------------------------------------------

    // Each class's `particles` property and whether any of its states have `particles:`, read once
    readonly Dictionary<string, (List<string> Emitters, bool States)> classEmitters = new(StringComparer.OrdinalIgnoreCase);

    (List<string> Emitters, bool States) ClassEmitters(Entities.Actors.Actor actor)
    {
        if (classEmitters.TryGetValue(actor.Name, out var known))
            return known;
        var names = actor.Properties.TryGetValue("particles", out var value)
            ? Loaders.StateDataNodeDeserializer.EmitterNames(value)
            : [];
        bool states = actor.ResolvedStates.Values.Any(first =>
        {
            var seen = new HashSet<Entities.Actors.ActorStateFrame>(ReferenceEqualityComparer.Instance);
            for (var frame = first; frame != null && seen.Add(frame); frame = frame.Next)
                if (frame.Particles != null)
                    return true;
            return false;
        });
        classEmitters[actor.Name] = known = (names, states);
        return known;
    }

    /// <summary>An actor's emitters as it goes, its class's read the first time it's asked for</summary>
    internal Entities.Actors.ActorParticleState StateOf(Entities.Actors.Actor actor)
    {
        var state = actor.ParticleState ??= new Entities.Actors.ActorParticleState();
        if (!state.Resolved)
        {
            state.ClassEmitters = ClassEmitters(actor).Emitters;
            state.Resolved = true;
        }
        return state;
    }

    static readonly List<string> NoEmitters = [];

    /// <summary>
    /// The emitters an actor runs now: none after A_StopEmitter(); else its state's
    /// `particles:` if the frame has one, else what A_StartEmitter / A_StopEmitter left, else its
    /// class's `particles`
    /// </summary>
    internal List<string> CurrentEmitters(Entities.Actors.Actor actor)
    {
        var state = StateOf(actor);
        if (state.Override is { Off: true })
            return NoEmitters;
        return actor.CurrentState?.Particles ?? state.Override?.Names ?? state.ClassEmitters;
    }

    // Whether an actor could be running an emitter, without making it a state to find out
    bool MayEmit(Entities.Actors.Actor actor) =>
        actor.ParticleState?.Override != null || ClassEmitters(actor) is { } known && (known.Emitters.Count > 0 || known.States);

    /// <summary>Which way an actor faces, in degrees (0 east, 90 north): its direction, else its angle (a projectile's, the player's)</summary>
    internal static double Facing(Entities.Actors.Actor actor) =>
        actor.Dir != ObjDir.NoDir ? GameConstants.DirAngle[(byte)actor.Dir] : actor.Angle;

    // Where an actor is, in texels
    static (double X, double Y) TexelsOf(Entities.Actors.Actor actor) =>
        (actor.X / (double)TexelGlobal, actor.Y / (double)TexelGlobal);

    /// <summary>Each actor's running emitters throw out what they owe for the tics gone by: rate a second, scaled by the setting</summary>
    void EmitFromActors(uint tics)
    {
        var defs = Definitions;
        if (defs.Emitters.Count == 0)
            return;

        double share = ShareFor(hooks.Detail());
        var view = hooks.ViewPoint?.Invoke();
        foreach (var actor in map.GetActors())
        {
            if (actor == null || !MayEmit(actor))
                continue;
            var names = CurrentEmitters(actor);
            if (names.Count == 0)
                continue;

            var state = StateOf(actor);
            var (x, y) = TexelsOf(actor);
            foreach (var name in names)
            {
                if (!defs.Emitters.TryGetValue(name, out var emitter))
                {
                    Warn($"{actor.Name}: particles.yaml has no emitter {name}");
                    continue;
                }
                if (TypeNamed(emitter.Type) is not { } type)
                    continue;

                var owed = state.Owed ??= new(StringComparer.OrdinalIgnoreCase);
                if (view is (int vx, int vy) && Math.Max(Math.Abs(actor.X - vx), Math.Abs(actor.Y - vy)) > emitter.Range * MapConstants.TileGlobal)
                {
                    owed.Remove(name);      // out of range: nothing piles up to come out at once
                    continue;
                }

                double due = owed.GetValueOrDefault(name) + Math.Max(emitter.Rate, 0) * share * tics / TicsPerSecond;
                int whole = (int)due;
                owed[name] = due - whole;
                if (whole > 0)
                    Emit(emitter, type, x, y, actor.Z, Facing(actor), whole);
            }
        }
    }

    /*
    =============================================================================

                                    ACTIONS

    For actor states (on the actor itself) and switches (on the actors with the switch's tag):

      A_SpawnParticles("name"[, count][, z])  a burst from the emitter (or of the particle
                                              type) where the actor is: count in place of its
                                              own, z texels higher
      A_StartEmitter(["name"])                the emitter runs on the actor, beside what it runs
                                              already; with no name, its class's emitters back
      A_StopEmitter(["name"])                 that emitter stops; with no name, all of them,
                                              states' particles: too, until A_StartEmitter

    =============================================================================
    */

    /// <summary>The actions, for actor states and switches. Saved: what A_StartEmitter / A_StopEmitter leave</summary>
    internal static void RegisterActions(Func<Particles> particles, Func<MapManager> map)
    {
        Entities.Actors.ActorActionRegistry.Register("A_SpawnParticles", (actor, args) => particles().SpawnAction(actor, args));
        Entities.Actors.ActorActionRegistry.Register("A_StartEmitter", (actor, args) => particles().StartEmitter(actor, args.FirstOrDefault()));
        Entities.Actors.ActorActionRegistry.Register("A_StopEmitter", (actor, args) => particles().StopEmitter(actor, args.FirstOrDefault()));
        Entities.MapTriggerRegistry.Register("A_SpawnParticles", (trigger, args) =>
            ForTagged(map(), trigger, actor => particles().SpawnAction(actor, args)));
        Entities.MapTriggerRegistry.Register("A_StartEmitter", (trigger, args) =>
            ForTagged(map(), trigger, actor => particles().StartEmitter(actor, args.FirstOrDefault())));
        Entities.MapTriggerRegistry.Register("A_StopEmitter", (trigger, args) =>
            ForTagged(map(), trigger, actor => particles().StopEmitter(actor, args.FirstOrDefault())));
    }

    static bool ForTagged(MapManager map, Entities.TriggerActivation trigger, Func<Entities.Actors.Actor, bool> act)
    {
        bool any = false;
        foreach (var actor in map.TaggedActors(trigger.Tag).ToList())
            any |= act(actor);
        return any;
    }

    /// <summary>A_SpawnParticles("name"[, count][, z]): a burst where the actor is, facing its way</summary>
    internal bool SpawnAction(Entities.Actors.Actor actor, string[] args)
    {
        var name = args.FirstOrDefault()?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            Warn($"{actor.Name}: A_SpawnParticles needs an emitter or particle type's name");
            return false;
        }

        int? count = null;
        if (args.ElementAtOrDefault(1) is { Length: > 0 } countArg)
        {
            if (!int.TryParse(countArg, out int n) || n < 0)
            {
                Warn($"{actor.Name}: A_SpawnParticles count '{countArg}' isn't a number of particles");
                return false;
            }
            count = n;
        }
        double z = 0;
        if (args.ElementAtOrDefault(2) is { Length: > 0 } zArg
            && !double.TryParse(zArg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z))
        {
            Warn($"{actor.Name}: A_SpawnParticles z '{zArg}' isn't a height in texels");
            return false;
        }

        var (x, y) = TexelsOf(actor);
        if (Burst(name, x, y, actor.Z + z, Facing(actor), count) < 0)
        {
            Warn($"{actor.Name}: A_SpawnParticles: particles.yaml has no emitter or particle type {name}");
            return false;
        }
        return true;
    }

    /// <summary>A_StartEmitter(["name"]): the emitter runs on the actor beside the rest; no name, its class's back</summary>
    internal bool StartEmitter(Entities.Actors.Actor actor, string? name)
    {
        var state = StateOf(actor);
        if (string.IsNullOrWhiteSpace(name))
        {
            state.Override = null;
            return true;
        }

        name = name.Trim();
        if (!Definitions.Emitters.ContainsKey(name))
            Warn($"{actor.Name}: A_StartEmitter: particles.yaml has no emitter {name}");
        var names = new List<string>(state.Override is { Off: false } set ? set.Names : state.ClassEmitters);
        if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
            names.Add(name);
        state.Override = new Entities.Actors.ParticleOverride(false, names);
        return true;
    }

    /// <summary>A_StopEmitter(["name"]): that emitter stops; no name, all of them, states' too</summary>
    internal bool StopEmitter(Entities.Actors.Actor actor, string? name)
    {
        var state = StateOf(actor);
        if (string.IsNullOrWhiteSpace(name))
        {
            state.Override = new Entities.Actors.ParticleOverride(true, []);
            return true;
        }

        name = name.Trim();
        var names = new List<string>(state.Override is { Off: false } set ? set.Names : state.ClassEmitters);
        names.RemoveAll(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
        state.Override = new Entities.Actors.ParticleOverride(false, names);
        return true;
    }

    /// <summary>The console's `emitters`: each actor running emitters, and which</summary>
    internal IEnumerable<string> DescribeEmitters()
    {
        foreach (var actor in map.GetActors())
        {
            if (actor == null || !MayEmit(actor))
                continue;
            var names = CurrentEmitters(actor);
            var state = StateOf(actor);
            var how = state.Override switch
            {
                { Off: true } => " (A_StopEmitter)",
                not null => " (A_StartEmitter / A_StopEmitter)",
                _ => actor.CurrentState?.Particles != null ? " (its state's)" : "",
            };
            yield return $"{actor.Name} at {actor.TileX},{actor.TileY}: {(names.Count == 0 ? "none" : string.Join(", ", names))}{how}";
        }
    }

    // ---- Scripts' emitters and definitions -----------------------------------------------------

    /// <summary>
    /// An emitter a script made (game.emitter): an emitter's settings with the script's changes,
    /// standing somewhere or on an actor. It belongs to the level it was made on (a new level, a
    /// death or a loaded game ends it: game.onlevel makes it again), and isn't saved.
    /// </summary>
    internal sealed class ScriptEmitter
    {
        public required EmitterInfo Info;
        public required ResolvedType Type;
        public double X, Y, Z;                      // texels, standing on its own
        public Entities.Actors.Actor? Actor;        // on an actor instead: its spot, Z higher
        public bool Running = true;
        public bool Gone;                           // removed, its actor gone, or its level over
        public int Level;                           // the map's LevelSerial it was made on
        public double Owed;
    }

    readonly List<ScriptEmitter> scriptEmitters = [];

    /// <summary>How many emitters scripts have running on the level</summary>
    internal int ScriptEmitterCount => scriptEmitters.Count;

    /// <summary>
    /// An emitter (or a particle type, thrown out every way) with a script's changes: its
    /// emitter fields and its type's, by name as particles.yaml writes them (size-end, or
    /// size_end, or sizeend). Null, with why told to error, when there's no such name or a
    /// change can't be made.
    /// </summary>
    internal (EmitterInfo Emitter, ResolvedType Type)? WithChanges(string name, IReadOnlyList<(string Key, object? Value)> changes, Action<string> error)
    {
        var defs = Definitions;
        EmitterInfo emitter;
        if (defs.Emitters.TryGetValue(name, out var named))
            emitter = named with { };
        else if (defs.Types.ContainsKey(name))
            emitter = new EmitterInfo { Type = name };
        else
        {
            error($"particles.yaml has no emitter or particle type {name}");
            return null;
        }

        var typeChanges = new List<(string Key, object? Value)>();
        foreach (var (key, value) in changes)
        {
            if (FieldNamed(typeof(EmitterInfo), key) != null)
            {
                if (!SetField(emitter, key, value, error))
                    return null;
            }
            else if (FieldNamed(typeof(ParticleTypeInfo), key) != null)
                typeChanges.Add((key, value));
            else
            {
                error($"an emitter or particle type has no {key}");
                return null;
            }
        }

        if (!defs.Types.TryGetValue(emitter.Type, out var typeInfo))
        {
            error($"particles.yaml has no particle type {emitter.Type}");
            return null;
        }
        if (typeChanges.Count == 0)
            return TypeNamed(emitter.Type) is { } type ? (emitter, type) : null;

        // Changed for this one: read afresh, kept for the next time the same changes are asked for
        var key2 = emitter.Type + "|" + string.Join("|", typeChanges.Select(c => $"{Normalize(c.Key)}={Describe(c.Value)}"));
        if (!changedTypes.TryGetValue(key2, out var changed))
        {
            var info = typeInfo with { };
            foreach (var (key, value) in typeChanges)
                if (!SetField(info, key, value, error))
                    return null;
            changed = Resolve(emitter.Type, info, Warn);
            if (changedTypes.Count > 256)
                changedTypes.Clear();
            changedTypes[key2] = changed;
        }
        return (emitter, changed);
    }

    readonly Dictionary<string, ResolvedType> changedTypes = new(StringComparer.OrdinalIgnoreCase);

    static string Describe(object? value) => value switch
    {
        null => "",
        List<string> list => "[" + string.Join(",", list) + "]",
        IFormattable number => number.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    static string Normalize(string key) => key.Replace("-", "").Replace("_", "").ToLowerInvariant();

    static System.Reflection.PropertyInfo? FieldNamed(Type type, string key)
    {
        var wanted = Normalize(key);
        return type.GetProperties().FirstOrDefault(p => p.CanWrite && p.Name.ToLowerInvariant() == wanted);
    }

    // Sets an emitter's or type's field from a script's value: a number, a string, a boolean or a list of strings
    static bool SetField(object target, string key, object? value, Action<string> error)
    {
        var property = FieldNamed(target.GetType(), key)!;
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        try
        {
            object? converted = value switch
            {
                null when Nullable.GetUnderlyingType(property.PropertyType) != null => null,
                _ when type == typeof(string) => value switch
                {
                    IFormattable number => number.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                    string text => text,
                    _ => throw new FormatException("a string or a number"),
                },
                _ when type == typeof(double) => value is double d ? d : value is string s
                    ? double.Parse(s, System.Globalization.CultureInfo.InvariantCulture) : throw new FormatException("a number"),
                _ when type == typeof(int) => value is double n && n == Math.Floor(n) ? (int)n : throw new FormatException("a whole number"),
                _ when type == typeof(bool) => value is bool b ? b : throw new FormatException("true or false"),
                _ when type == typeof(List<string>) => value is List<string> list ? new List<string>(list)
                    : value is string one ? [one] : throw new FormatException("a list of strings"),
                _ => throw new FormatException("something it can't take"),
            };
            property.SetValue(target, converted);
            return true;
        }
        catch (FormatException e)
        {
            error($"{key} should be {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// A script's burst (game.particles, actor:particles): an emitter (or type) with its changes,
    /// at (x, y) texels, z added to its own, facing angle. How many were thrown out; -1 (and why,
    /// to error) when it can't be.
    /// </summary>
    internal int ScriptBurst(string name, IReadOnlyList<(string Key, object? Value)> changes, double x, double y, double z, double angle, Action<string> error)
    {
        if (WithChanges(name, changes, error) is not (EmitterInfo emitter, ResolvedType type))
            return -1;
        return Emit(emitter, type, x, y, z, angle, ScaledCount(emitter.Count, hooks.Detail()));
    }

    /// <summary>A script's running emitter (game.emitter), at a spot or on an actor; null (and why, to error) when it can't be</summary>
    internal ScriptEmitter? StartScriptEmitter(string name, IReadOnlyList<(string Key, object? Value)> changes, Action<string> error,
        Entities.Actors.Actor? actor = null, double x = 0, double y = 0, double z = 0)
    {
        if (WithChanges(name, changes, error) is not (EmitterInfo emitter, ResolvedType type))
            return null;
        var made = new ScriptEmitter { Info = emitter, Type = type, Actor = actor, X = x, Y = y, Z = z, Level = map.LevelSerial };
        scriptEmitters.Add(made);
        return made;
    }

    /// <summary>A script's changes to its running emitter (emitter:set): false (and why, to error) when one can't be made</summary>
    internal bool ChangeScriptEmitter(ScriptEmitter emitter, IReadOnlyList<(string Key, object? Value)> changes, Action<string> error)
    {
        // Laid over its settings as they are: read back through WithChanges as a one-off emitter of its own
        var name = $"\u0001script{RuntimeHelpers.GetHashCode(emitter)}";
        var defs = WritableDefinitions();
        defs.Emitters[name] = emitter.Info;
        try
        {
            var changed = WithChanges(name, changes, error);
            if (changed is not (EmitterInfo info, ResolvedType type))
                return false;
            emitter.Info = info;
            emitter.Type = type;
            return true;
        }
        finally
        {
            defs.Emitters.Remove(name);
        }
    }

    /// <summary>Takes a script's emitter off the level</summary>
    internal void RemoveScriptEmitter(ScriptEmitter emitter)
    {
        emitter.Gone = true;
        scriptEmitters.Remove(emitter);
    }

    // particles.yaml's definitions, copied the first time a script adds to them (the asset's own stay as loaded)
    ParticlesAsset WritableDefinitions()
    {
        if (!definitionsOwn)
        {
            var loaded = Definitions;
            definitions = new ParticlesAsset
            {
                Types = new(loaded.Types, StringComparer.OrdinalIgnoreCase),
                Emitters = new(loaded.Emitters, StringComparer.OrdinalIgnoreCase),
            };
            definitionsRead = definitionsOwn = true;
        }
        return definitions!;
    }

    bool definitionsOwn;

    /// <summary>
    /// game.defineparticle: a particle type of a script's, over particles.yaml's of its name:
    /// fields as particles.yaml's, from a base type's (base) or the defaults. False (and why, to
    /// error) when it can't be made.
    /// </summary>
    internal bool DefineType(string name, IReadOnlyList<(string Key, object? Value)> fields, Action<string> error)
    {
        var defs = WritableDefinitions();
        ParticleTypeInfo info = new();
        foreach (var (key, value) in fields)
        {
            if (Normalize(key) != "base")
                continue;
            if (value is not string baseName || !defs.Types.TryGetValue(baseName, out var source))
            {
                error($"base {Describe(value)} isn't a particle type");
                return false;
            }
            info = source with { Colors = [.. source.Colors] };
        }
        foreach (var (key, value) in fields)
        {
            if (Normalize(key) == "base")
                continue;
            if (FieldNamed(typeof(ParticleTypeInfo), key) == null)
            {
                error($"a particle type has no {key}");
                return false;
            }
            if (!SetField(info, key, value, error))
                return false;
        }
        defs.Types[name] = info;
        resolved.Remove(name);
        changedTypes.Clear();
        return true;
    }

    /// <summary>game.defineemitter: an emitter of a script's, as DefineType does a type</summary>
    internal bool DefineEmitter(string name, IReadOnlyList<(string Key, object? Value)> fields, Action<string> error)
    {
        var defs = WritableDefinitions();
        EmitterInfo info = new();
        foreach (var (key, value) in fields)
        {
            if (Normalize(key) != "base")
                continue;
            if (value is not string baseName || !defs.Emitters.TryGetValue(baseName, out var source))
            {
                error($"base {Describe(value)} isn't an emitter");
                return false;
            }
            info = source with { };
        }
        foreach (var (key, value) in fields)
        {
            if (Normalize(key) == "base")
                continue;
            if (FieldNamed(typeof(EmitterInfo), key) == null)
            {
                error($"an emitter has no {key}");
                return false;
            }
            if (!SetField(info, key, value, error))
                return false;
        }
        if (!defs.Types.ContainsKey(info.Type))
        {
            error($"its type {info.Type} isn't a particle type");
            return false;
        }
        defs.Emitters[name] = info;
        return true;
    }

    // Scripts' running emitters throw out what they owe; those of an earlier level, or whose actor's gone, are let go
    void EmitFromScripts(uint tics)
    {
        if (scriptEmitters.Count == 0)
            return;
        double share = ShareFor(hooks.Detail());
        var view = hooks.ViewPoint?.Invoke();
        for (int i = scriptEmitters.Count - 1; i >= 0; i--)
        {
            var e = scriptEmitters[i];
            if (e.Level != map.LevelSerial || e.Actor is { IsRemoved: true })
            {
                e.Gone = true;
                scriptEmitters.RemoveAt(i);
                continue;
            }
            if (!e.Running)
                continue;

            var (x, y, z) = e.Actor is { } actor
                ? (actor.X / (double)TexelGlobal, actor.Y / (double)TexelGlobal, actor.Z + e.Z)
                : (e.X, e.Y, e.Z);
            if (view is (int vx, int vy) && Math.Max(Math.Abs(x * TexelGlobal - vx), Math.Abs(y * TexelGlobal - vy)) > e.Info.Range * MapConstants.TileGlobal)
            {
                e.Owed = 0;
                continue;
            }

            double due = e.Owed + Math.Max(e.Info.Rate, 0) * share * tics / TicsPerSecond;
            int whole = (int)due;
            e.Owed = due - whole;
            if (whole > 0)
                Emit(e.Info, e.Type, x, y, z, e.Actor != null ? Facing(e.Actor) : 0, whole);
        }
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

        EmitFromActors(tics);
        EmitFromScripts(tics);

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
