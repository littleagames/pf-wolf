using PFWolf.Assets.Sounds;
using PFWolf.Editor.Editing;

namespace PFWolf.Editor.Data;

/// <summary>
/// Songs and sounds put in mods as files, as the engine loads them: music/NAME.imf (or .wlf,
/// .ogg, .mp3, .wav) is the song NAME; sounds/NAME.wav (or .ogg, .mp3) the digitized sound,
/// sounds/NAME.adl the AdLib one and sounds/NAME.pcs the PC speaker one
/// </summary>
public static class SoundFiles
{
    public static readonly string[] SongExtensions = [".imf", ".wlf", ".ogg", ".mp3", ".wav"];
    public static readonly string[] SoundExtensions = [".wav", ".ogg", ".mp3", ".adl", ".pcs"];
    private static readonly string[] SampledExtensions = [".wav", ".ogg", ".mp3"];

    public static string Folder(SoundKind kind) => kind == SoundKind.Music ? "music" : "sounds";

    /// <summary>
    /// What a file is as a song (<paramref name="asSong"/>) or a sound, checked by reading it as
    /// the engine will; throws InvalidDataException (with why) when it can't be one
    /// </summary>
    public static SoundKind Check(bool asSong, string fileName, byte[] data)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!(asSong ? SongExtensions : SoundExtensions).Contains(extension))
            throw new InvalidDataException($"a {(asSong ? "song" : "sound")} is one of {string.Join(", ", asSong ? SongExtensions : SoundExtensions)}");

        switch (extension)
        {
            case ".imf" or ".wlf":
                Wolf3dImfAudio.FromFile(data);
                return SoundKind.Music;
            case ".adl":
                AdLibSound.FromFile(data).ToPcm16();
                return SoundKind.AdLib;
            case ".pcs":
                PcSound.FromFile(data).ToMono8();
                return SoundKind.PcSpeaker;
        }

        if (asSong)
        {
            using var decoder = new MusicFileAsset(data).OpenDecoder();
            return SoundKind.Music;
        }
        _ = new SoundFileAsset(data);
        return SoundKind.Digitized;
    }

    // The extensions of files that would play as the same song or sound of a name
    private static string[] SameKind(SoundKind kind) => kind switch
    {
        SoundKind.Music => SongExtensions,
        SoundKind.AdLib => [".adl"],
        SoundKind.PcSpeaker => [".pcs"],
        _ => SampledExtensions,
    };

    /// <summary>
    /// Where a song or sound of a kind and name goes in a mod, and the mod's files it takes the
    /// place of (the same name with another extension, which would play over it or under it)
    /// </summary>
    public static (string EntryPath, IReadOnlyList<string> Deletes) Target(string modPath, SoundKind kind, string name, string extension)
    {
        var folder = Folder(kind);
        var entryPath = $"{folder}/{name.ToUpperInvariant()}{extension.ToLowerInvariant()}";
        var deletes = new List<string>();
        foreach (var other in SameKind(kind))
        {
            if (other.Equals(extension, StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var candidate in new[] { $"{folder}/{name.ToUpperInvariant()}{other}", $"{folder}/{name.ToLowerInvariant()}{other}" }.Distinct())
            {
                if (ModFiles.ReadBytes(modPath, candidate) != null && !deletes.Any(path => ModFiles.SamePath(path, candidate)))
                    deletes.Add(candidate);
            }
        }
        return (entryPath, deletes);
    }

    /// <summary>
    /// A song or sound's file as the engine reads it from a pk3 (.imf, .adl or .pcs), and its
    /// extension: null for a sound-seq entry, a digitized sound, or a song a music file plays over
    /// </summary>
    public static (byte[] Data, string Extension)? Exportable(GameContent content, SoundKind kind, string name)
    {
        switch (kind)
        {
            case SoundKind.Music when !content.Assets.Exists<MusicFileAsset>(name) && content.Find<Wolf3dImfAudio>(name) is { } song:
                return (song.RawData, ".imf");
            case SoundKind.AdLib when content.Find<AdLibSound>(name) is { } adLib:
                // An AUDIOT chunk may run on past the sound
                return (adLib.RawData[..(23 + adLib.Tones.Length)], ".adl");
            case SoundKind.PcSpeaker when content.Find<PcSound>(name) is { } pc:
                return (pc.RawData[..(6 + pc.Values.Length)], ".pcs");
            default:
                return null;
        }
    }
}
