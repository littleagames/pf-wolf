using System.Collections;
using System.Text.RegularExpressions;
using PFWolf.Assets;
using PFWolf.Assets.Sounds;
using PFWolf.Loaders;

namespace PFWolf.Editor.Data;

/// <summary>The kinds of sound the sound browser lists</summary>
public enum SoundKind
{
    /// <summary>A sound-seq entry ("doors/open"): what actordefs and mapdefs name, played as its best device's sound</summary>
    Sound,
    /// <summary>A sampled sound: VSWAP's, or a pack's sounds/ WAV, OGG or MP3 in its place</summary>
    Digitized,
    AdLib,
    PcSpeaker,
    /// <summary>An IMF song, or a pack's music/ OGG, MP3 or WAV in its place</summary>
    Music,
}

/// <summary>Something that plays a sound or song; one that's a sound-seq entry can be gone to</summary>
public sealed record SoundUse(string Label, string? SoundName = null)
{
    public override string ToString() => Label;
}

/// <summary>One way a sound-seq entry can sound: a device and the asset it plays there</summary>
public sealed record SoundVariant(SoundKind Kind, string AssetName)
{
    public string Label => $"{SoundCatalog.KindName(Kind)}: {AssetName}";
    public override string ToString() => Label;
}

/// <summary>
/// One sound or song in the game's assets, where it came from and what plays it. A sound-seq
/// entry carries its <see cref="Profile"/>; the others are assets.
/// </summary>
public sealed record SoundEntry(SoundKind Kind, string Name, IReadOnlyList<string> Origins, IReadOnlyList<SoundUse> Uses)
{
    public SoundProfile? Profile { get; init; }

    /// <summary>A pack's sound or music file plays in place of the game's own of this name</summary>
    public bool IsFile { get; init; }

    /// <summary>The file or pack it comes from, for the list's tooltip</summary>
    public string Source { get; init; } = "";

    public override string ToString() => Name;
}

/// <summary>
/// Every sound-seq sound, digitized, AdLib and PC speaker sound and song the game's assets hold,
/// with what in actordefs, mapdefs, sound-seq and game-info plays each, and how to play them
/// </summary>
public static class SoundCatalog
{
    public static string KindName(SoundKind kind) => kind switch
    {
        SoundKind.Sound => "Sound",
        SoundKind.Digitized => "Digitized",
        SoundKind.AdLib => "AdLib",
        SoundKind.PcSpeaker => "PC speaker",
        _ => "Music",
    };

    /// <summary>
    /// The sound-seq entries as the engine sees them: the shared sounds/sound-seq.yaml with the
    /// running pack's own (gamepacks/PACK/sound-seq.yaml) over them, and which of the two each is from
    /// </summary>
    public static Dictionary<string, (SoundProfile Profile, string From)> Sequences(GameContent content)
    {
        var entries = new Dictionary<string, (SoundProfile, string)>(StringComparer.OrdinalIgnoreCase);
        if (content.Find<SoundSequenceAsset>("sound-seq") is { } shared)
        {
            foreach (var (name, profile) in shared.SoundInfo)
                entries[name] = (profile, "sounds/sound-seq.yaml");
        }
        var packName = $"{content.PackId}/sound-seq";
        if (content.Find<SoundSequenceAsset>(packName) is { } pack)
        {
            foreach (var (name, profile) in pack.SoundInfo)
                entries[name] = (profile, $"gamepacks/{content.PackId}/sound-seq.yaml");
        }
        return entries;
    }

    public static List<SoundEntry> Build(GameContent content)
    {
        var sequences = Sequences(content);
        var uses = Uses(content, sequences);
        var entries = new List<SoundEntry>();

        foreach (var (name, (profile, from)) in sequences.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (profile == null)
                continue;
            entries.Add(new SoundEntry(SoundKind.Sound, name, [$"In {from}"], uses.GetValueOrDefault(name, []))
            {
                Profile = profile,
                Source = from,
            });
        }

        var assets = content.Assets;
        foreach (var name in assets.AssetNames.Where(name => !name.Contains('/')).Order(StringComparer.OrdinalIgnoreCase))
        {
            var display = name.ToUpperInvariant();
            var used = uses.GetValueOrDefault(display, []);

            if (assets.Exists<Wolf3dDigitizedAudio>(name) || assets.Exists<SoundFileAsset>(name))
                entries.Add(Asset(SoundKind.Digitized, display, Origins(content, name, nameof(Wolf3dDigitizedAudio), nameof(SoundFileAsset)),
                    used, assets.Exists<SoundFileAsset>(name)));
            if (assets.Exists<AdLibSound>(name))
                entries.Add(Asset(SoundKind.AdLib, display, Origins(content, name, nameof(AdLibSound)), used, false));
            if (assets.Exists<PcSound>(name))
                entries.Add(Asset(SoundKind.PcSpeaker, display, Origins(content, name, nameof(PcSound)), used, false));
            if (assets.Exists<Wolf3dImfAudio>(name) || assets.Exists<MusicFileAsset>(name))
                entries.Add(Asset(SoundKind.Music, display, Origins(content, name, nameof(Wolf3dImfAudio), nameof(MusicFileAsset)),
                    used, assets.Exists<MusicFileAsset>(name)));
        }

        return entries;

        static SoundEntry Asset(SoundKind kind, string name, IReadOnlyList<AssetOrigin> origins, List<SoundUse> used, bool isFile)
            => new(kind, name, origins.Select(origin => $"{origin.Action} by {origin.Source}: {origin.Path}").ToList(), used)
            {
                IsFile = isFile,
                Source = origins.LastOrDefault(origin => origin.Action != AssetOrigin.LeftOut)?.Source ?? "",
            };
    }

    // The game's own asset first, then the file that plays in its place: that one wins
    private static List<AssetOrigin> Origins(GameContent content, string name, params string[] types)
    {
        var found = content.Assets.FindAssetOrigins(name).ToList();
        return types.SelectMany(type => found.FirstOrDefault(asset => asset.Type == type).Origins ?? []).ToList();
    }

    /// <summary>
    /// The devices a sound-seq entry can play on, best first (digitized, AdLib, PC speaker), after
    /// following its alias; a random entry's picks each have their own, so it has none
    /// </summary>
    public static List<SoundVariant> Variants(GameContent content, string soundName)
    {
        var sequences = Sequences(content);
        var profile = Resolve(sequences, soundName, pickRandom: null);
        if (profile == null)
            return content.Assets.Exists<SoundFileAsset>(soundName) || content.Assets.Exists<Wolf3dDigitizedAudio>(soundName)
                ? [new SoundVariant(SoundKind.Digitized, soundName.ToUpperInvariant())]
                : [];

        var variants = new List<SoundVariant>();
        if (HasAsset(content, SoundKind.Digitized, profile.Digitized))
            variants.Add(new SoundVariant(SoundKind.Digitized, profile.Digitized!.ToUpperInvariant()));
        if (HasAsset(content, SoundKind.AdLib, profile.AdLib))
            variants.Add(new SoundVariant(SoundKind.AdLib, profile.AdLib!.ToUpperInvariant()));
        if (HasAsset(content, SoundKind.PcSpeaker, profile.PC))
            variants.Add(new SoundVariant(SoundKind.PcSpeaker, profile.PC!.ToUpperInvariant()));
        return variants;
    }

    /// <summary>
    /// The profile a sound name plays, following aliases and (with <paramref name="pickRandom"/>)
    /// random picks as the engine does, up to 8 deep; null when there's none or it's still random
    /// </summary>
    public static SoundProfile? Resolve(Dictionary<string, (SoundProfile Profile, string From)> sequences, string name, Func<int, int>? pickRandom)
    {
        for (var depth = 0; depth < 8; depth++)
        {
            if (!sequences.TryGetValue(name, out var entry) || entry.Profile == null)
                return null;
            var profile = entry.Profile;
            if (profile.Random.Count > 0)
            {
                if (pickRandom == null)
                    return null;
                name = profile.Random[pickRandom(profile.Random.Count)];
                continue;
            }
            if (!string.IsNullOrWhiteSpace(profile.Alias))
            {
                name = profile.Alias;
                continue;
            }
            return profile;
        }
        return null;
    }

    public static bool HasAsset(GameContent content, SoundKind kind, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        var assets = content.Assets;
        return kind switch
        {
            SoundKind.Digitized => assets.Exists<SoundFileAsset>(name) || assets.Exists<Wolf3dDigitizedAudio>(name),
            SoundKind.AdLib => assets.Exists<AdLibSound>(name),
            SoundKind.PcSpeaker => assets.Exists<PcSound>(name),
            SoundKind.Music => assets.Exists<MusicFileAsset>(name) || assets.Exists<Wolf3dImfAudio>(name),
            _ => false,
        };
    }

    /// <summary>
    /// A sound's samples as 16-bit mono and their rate, as the game plays them (a pack's file wins
    /// over the game's own digitized sound); null for music, or a sound that isn't there
    /// </summary>
    public static (short[] Samples, int SampleRate)? Samples(GameContent content, SoundKind kind, string name)
    {
        const int rate = 44100;
        switch (kind)
        {
            case SoundKind.Digitized when content.Find<SoundFileAsset>(name) is { } file:
                return (file.Samples, file.SampleRate);
            case SoundKind.Digitized when content.Find<Wolf3dDigitizedAudio>(name) is { } digitized:
                return (digitized.ToPcm16(rate), rate);
            case SoundKind.AdLib when content.Find<AdLibSound>(name) is { } adLib:
                return (Mono8To16(adLib.ToMono8()), rate);
            case SoundKind.PcSpeaker when content.Find<PcSound>(name) is { } pc:
                return (Mono8To16(pc.ToMono8()), rate);
            default:
                return null;
        }
    }

    /// <summary>Something to play: a sound's samples, or a song as it streams; null when it isn't there</summary>
    public static MusicSource? Open(GameContent content, SoundKind kind, string name)
    {
        if (kind == SoundKind.Music)
        {
            if (content.Find<MusicFileAsset>(name) is { } file)
                return new FileMusicSource(file);
            return content.Find<Wolf3dImfAudio>(name) is { } imf ? new ImfMusicSource(imf) : null;
        }
        return Samples(content, kind, name) is { } sound ? new SampleMusicSource(sound.Samples, sound.SampleRate) : null;
    }

    /// <summary>How long an IMF song is before it loops; null for a music file (it isn't decoded ahead)</summary>
    public static TimeSpan? MusicLength(GameContent content, string name)
        => content.Assets.Exists<MusicFileAsset>(name) || content.Find<Wolf3dImfAudio>(name) is not { } imf
            ? null
            : ImfMusicSource.Length(imf);

    /// <summary>A 16-bit mono PCM WAV file of these samples</summary>
    public static byte[] ToWav(short[] samples, int sampleRate)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + samples.Length * 2);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write((short)1); // mono
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(samples.Length * 2);
        foreach (var sample in samples)
            writer.Write(sample);
        writer.Flush();
        return stream.ToArray();
    }

    private static short[] Mono8To16(byte[] samples)
    {
        var wide = new short[samples.Length];
        for (var index = 0; index < samples.Length; index++)
            wide[index] = (short)((samples[index] - 128) << 8);
        return wide;
    }

    // A quoted name in an actordefs action: A_PlaySound("bs/podhatch", "positional")
    private static readonly Regex Quoted = new("\"([^\"]+)\"|'([^']+)'", RegexOptions.Compiled);

    /// <summary>
    /// Sounds and songs by name: actordefs class properties (seesound, inventory.pickupsound…) and
    /// state actions, mapdefs doors, switches and outlets, sound-seq entries naming another, and
    /// game-info's music
    /// </summary>
    public static Dictionary<string, List<SoundUse>> Uses(GameContent content, Dictionary<string, (SoundProfile Profile, string From)> sequences)
    {
        var uses = new Dictionary<string, List<SoundUse>>(StringComparer.OrdinalIgnoreCase);
        void Add(string? name, string label, string? soundName = null)
        {
            var use = new SoundUse(label, soundName);
            if (string.IsNullOrWhiteSpace(name))
                return;
            if (!uses.TryGetValue(name, out var list))
                uses[name] = list = [];
            if (!list.Contains(use))
                list.Add(use);
        }

        // sound-seq: what each entry plays, and the entries other entries pass on to
        foreach (var (name, (profile, _)) in sequences.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (profile == null)
                continue;
            Add(profile.Digitized, $"Sound {name} (digitized)", name);
            Add(profile.AdLib, $"Sound {name} (AdLib)", name);
            Add(profile.PC, $"Sound {name} (PC speaker)", name);
            Add(profile.Alias, $"Sound {name} (as its alias)", name);
            foreach (var pick in profile.Random)
                Add(pick, $"Sound {name} (one of its random picks)", name);
        }

        // actordefs: properties naming sounds, and sounds played by state actions
        foreach (var (className, actor) in content.Actors.OrderBy(actor => actor.Key, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var (key, value) in actor.Properties)
            {
                if (!key.Contains("sound", StringComparison.OrdinalIgnoreCase))
                    continue;
                foreach (var name in Strings(value))
                    Add(name, $"{className} {key}");
            }

            foreach (var (stateName, frames) in actor.States)
            {
                foreach (var frame in frames.OfType<ActorStatesData>())
                {
                    foreach (var text in new[] { frame.Action, frame.Think })
                    {
                        if (string.IsNullOrWhiteSpace(text))
                            continue;
                        foreach (Match match in Quoted.Matches(text))
                        {
                            var name = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                            if (sequences.ContainsKey(name) || HasAsset(content, SoundKind.Digitized, name))
                                Add(name, $"{className} {stateName} state");
                        }
                    }
                }
            }
        }

        // mapdefs
        var defs = content.MapDefs;
        foreach (var (id, door) in defs.Doors.OrderBy(door => door.Key))
        {
            Add(door.OpenSound, $"Door {id} opening");
            Add(door.CloseSound, $"Door {id} closing");
            Add(door.LockedSound, $"Door {id} while locked");
            Add(door.WrongSideSound, $"Door {id} from the wrong side");
        }
        foreach (var (id, wall) in defs.Walls.OrderBy(wall => wall.Key))
        {
            Add(wall.Switch?.Sound, $"Switch wall {id}");
            Add(wall.Switch?.LockedSound, $"Switch wall {id} while locked");
            Add(wall.Outlet?.Sound, $"Outlet wall {id}");
        }

        // game-info's music
        if (content.GameInfo is { } gameInfo)
        {
            foreach (var (mapName, info) in gameInfo.Maps)
                Add(info.Music, $"{mapName.ToUpperInvariant()}'s music");
            Add(gameInfo.IntroMusic, "The title, demo loop and menus");
            Add(gameInfo.HighScoresMusic, "The high scores");
            Add(gameInfo.MenuMusic, "The menus");
            foreach (var screen in gameInfo.Intro.Concat(gameInfo.TitleLoop))
                Add(screen.Music, "A title screen");
            foreach (var (number, cluster) in gameInfo.Clusters)
                Add(cluster.VictoryMusic, $"Cluster {number}'s victory");
        }

        return uses;
    }

    // A property's names: one string, or a list of them (a random pick: ["bs/explode1", "bs/explode2"])
    private static IEnumerable<string> Strings(object? value)
    {
        switch (value)
        {
            case string text:
                yield return text;
                break;
            case IEnumerable list:
                foreach (var item in list)
                {
                    if (item is string text)
                        yield return text;
                }
                break;
        }
    }
}
