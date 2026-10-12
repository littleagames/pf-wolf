namespace PFWolf.Assets;

/// <summary>
/// An animation, whatever kind of asset its frames are (see <see cref="AnimatedAsset{T}"/>)
/// </summary>
public interface IAnimatedAsset
{
    /// <summary>The kind of asset its frames are (TextureAsset, GraphicAsset)</summary>
    Type FrameType { get; }

    /// <summary>Tics from the first frame to the end of the last</summary>
    int TotalTics { get; }

    /// <summary>False when it plays once and holds its last frame (`stop` in animdefs)</summary>
    bool Loops { get; }

    /// <summary>The frame showing <paramref name="elapsedTics"/> after it started</summary>
    Asset FrameAssetAt(int elapsedTics);
}

/// <summary>One frame of an animation: the asset shown, for this many tics</summary>
public record AnimationFrame<T>(string Name, T Asset, int Tics) where T : Asset;

/// <summary>
/// An animdefs animation: a name that stands for a list of <typeparamref name="T"/> frames, each
/// shown for some tics, over and over (or once, holding the last, when it ends in `stop`). It's
/// kept under the same name and kind as its frames, so a wall, flat, sky or picture that names it
/// shows whichever frame is up (AssetManager.Find).
/// </summary>
public record AnimatedAsset<T> : Asset, IAnimatedAsset where T : Asset
{
    private readonly int[] _ends;

    public AnimatedAsset(IReadOnlyList<AnimationFrame<T>> frames, bool loops)
    {
        if (frames.Count == 0)
            throw new ArgumentException("An animation needs a frame", nameof(frames));

        Frames = frames;
        Loops = loops;
        _ends = new int[frames.Count];
        int end = 0;
        for (int i = 0; i < frames.Count; i++)
            _ends[i] = end += Math.Max(1, frames[i].Tics);
        TotalTics = end;
    }

    public IReadOnlyList<AnimationFrame<T>> Frames { get; }

    public bool Loops { get; }

    public int TotalTics { get; }

    public Type FrameType => typeof(T);

    /// <summary>The index of the frame showing <paramref name="elapsedTics"/> after it started</summary>
    public int FrameIndexAt(int elapsedTics)
    {
        if (elapsedTics <= 0)
            return 0;
        if (elapsedTics >= TotalTics)
        {
            if (!Loops)
                return Frames.Count - 1;
            elapsedTics %= TotalTics;
        }

        // Few frames, so a walk beats a binary search
        int i = 0;
        while (_ends[i] <= elapsedTics)
            i++;
        return i;
    }

    public T FrameAt(int elapsedTics) => Frames[FrameIndexAt(elapsedTics)].Asset;

    public Asset FrameAssetAt(int elapsedTics) => FrameAt(elapsedTics);
}

/// <summary>
/// The animations in animdefs/ (shared) or animdefs/{pack}/ (the pack's own), by name, as written:
/// <code>
/// SLIME:
///   tics: 8                        # each frame's tics, unless it gives its own (70 a second)
///   frames:
///     - SLIME1
///     - { name: SLIME2, tics: 16 }
///     - stop                       # optional, last: play once and hold the last frame
/// </code>
/// AssetManager turns each into an <see cref="AnimatedAsset{T}"/> once every asset is loaded.
/// </summary>
public record AnimationDefinitionsAsset : Asset
{
    public AnimationDefinitionsAsset()
    {
    }

    public AnimationDefinitionsAsset(Dictionary<string, AnimationDefinition>? animations)
    {
        foreach (var (name, animation) in animations ?? [])
            Animations[name] = animation ?? new AnimationDefinition();
    }

    public Dictionary<string, AnimationDefinition> Animations { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>One animation in animdefs</summary>
public class AnimationDefinition
{
    public const int DefaultTics = 8;
    public const string Stop = "stop";

    /// <summary>Tics each frame shows for, unless it gives its own</summary>
    public int Tics { get; set; } = DefaultTics;

    /// <summary>
    /// Frame names, or mappings with a name and tics; `stop` last makes it play once
    /// </summary>
    public List<object> Frames { get; set; } = [];

    /// <summary>
    /// The frames as names and tics, and whether it loops; what's wrong with any entry goes to
    /// <paramref name="warn"/> and the entry is left out
    /// </summary>
    public (List<(string Name, int Tics)> Frames, bool Loops) Read(Action<string> warn)
    {
        var frames = new List<(string, int)>();
        bool loops = true;
        for (int i = 0; i < Frames.Count; i++)
        {
            var entry = Frames[i];
            if (entry is string name)
            {
                if (name.Equals(Stop, StringComparison.OrdinalIgnoreCase))
                {
                    if (i != Frames.Count - 1)
                        warn("`stop` has to be the last frame; the frames after it are left out");
                    loops = false;
                    break;
                }
                if (!string.IsNullOrWhiteSpace(name))
                    frames.Add((name.Trim(), Math.Max(1, Tics)));
                continue;
            }

            if (entry is IDictionary<object, object> map)
            {
                string? frameName = null;
                int tics = Tics;
                foreach (var (key, value) in map)
                {
                    switch (key?.ToString()?.ToLowerInvariant())
                    {
                        case "name": frameName = value?.ToString(); break;
                        case "tics" when int.TryParse(value?.ToString(), out var t): tics = t; break;
                        case "tics": warn($"frame {i + 1}: tics '{value}' isn't a number"); break;
                        default: warn($"frame {i + 1}: '{key}' isn't a frame setting (name, tics)"); break;
                    }
                }
                if (string.IsNullOrWhiteSpace(frameName))
                    warn($"frame {i + 1} has no name, so it's left out");
                else
                    frames.Add((frameName.Trim(), Math.Max(1, tics)));
                continue;
            }

            warn($"frame {i + 1} isn't a name or {{ name, tics }}, so it's left out");
        }

        return (frames, loops);
    }
}
