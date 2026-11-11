using PFWolf.Assets;
using PFWolf.Assets.Sounds;
using PFWolf.Editor.Audio;

namespace PFWolf.Editor.Data;

/// <summary>
/// The sounds a class makes as the actor window's preview plays it: an action's own (A_PlaySound),
/// or the property the engine plays for it (A_DeathScream its deathsound, T_Shoot its
/// attacksound...), and its seesound as it goes from standing or patrolling to chasing. Played as
/// the game picks them: the sound-seq entry's best device that's there.
/// </summary>
public sealed class ActorSoundPreview : IDisposable
{
    // The actions the engine plays a class's sound property for
    private static readonly Dictionary<string, string> PropertySounds = new(StringComparer.Ordinal)
    {
        ["A_DeathScream"] = "deathsound",
        ["A_ActiveSound"] = "activesound",
        ["A_Melee"] = "meleesound",
        ["T_Shoot"] = "attacksound",
        ["A_Shoot"] = "attacksound",
        ["T_Bite"] = "attacksound",
        ["A_SpawnProjectile"] = "attacksound",
        ["A_FireProjectile"] = "attacksound",
        ["A_FireMissile"] = "attacksound",
    };

    // The actions given a sound as their first argument
    private static readonly HashSet<string> SoundArgument = new(StringComparer.Ordinal) { "A_PlaySound", "A_Taunt" };

    /// <summary>The sound an action makes on a class (as the class has it, its parents' merged in), or null for none</summary>
    public static string? SoundOf(string action, ActorData merged)
    {
        var name = ActorCatalog.CallName(action);
        if (SoundArgument.Contains(name))
            return ActorCatalog.CallArguments(action).FirstOrDefault(arg => arg.Length > 0);
        return PropertySounds.TryGetValue(name, out var property) ? First(merged, property) : null;
    }

    /// <summary>Its seesound, as it goes from standing or patrolling (Spawn, Path) into chasing (Chase, See)</summary>
    public static string? SoundOfGoing(string from, string to, ActorData merged)
        => from is "Spawn" or "Path" && to is "Chase" or "See" ? First(merged, "seesound") : null;

    // A sound property's sound: one, or the first of a list (the engine picks one of them)
    private static string? First(ActorData merged, string property) => merged.Properties.GetValueOrDefault(property) switch
    {
        string one when one.Length > 0 => one,
        IEnumerable<object> list => list.Select(item => item?.ToString()).FirstOrDefault(item => !string.IsNullOrEmpty(item)),
        _ => null,
    };

    private readonly SoundPlayer _player = new();
    private readonly Random _random = new();
    private Dictionary<string, (SoundProfile Profile, string From)> _sequences = [];
    private GameContent? _content;

    public void Load(GameContent? content)
    {
        _content = content;
        _sequences = content == null ? [] : SoundCatalog.Sequences(content);
    }

    /// <summary>
    /// What a sound name plays (its device and asset), as the game would pick: the best device the
    /// game has it on; null when there's nothing to play
    /// </summary>
    public (SoundKind Kind, string Asset)? Pick(string name)
    {
        if (_content == null || SoundCatalog.Resolve(_sequences, name, count => _random.Next(count)) is not { } profile)
            return _content != null && SoundCatalog.HasAsset(_content, SoundKind.Digitized, name) ? (SoundKind.Digitized, name) : null;
        return new[] { (SoundKind.Digitized, profile.Digitized), (SoundKind.AdLib, profile.AdLib), (SoundKind.PcSpeaker, profile.PC) }
            .Where(option => SoundCatalog.HasAsset(_content, option.Item1, option.Item2))
            .Select(option => ((SoundKind, string)?)(option.Item1, option.Item2!))
            .FirstOrDefault();
    }

    /// <summary>Plays a sound by its name, cutting off the one before</summary>
    public void Play(string name)
    {
        if (!SoundPlayer.IsAvailable || _content == null || Pick(name) is not { } picked)
            return;
        try
        {
            if (SoundCatalog.Samples(_content, picked.Kind, picked.Asset) is { } sound)
                _player.Play(new SampleMusicSource(sound.Samples, sound.SampleRate, 0), loop: false);
        }
        catch (Exception e) when (e is InvalidDataException or PFWolf.Exceptions.PfWolfAudioException)
        {
            // a sound that can't be read is left out of the preview
        }
    }

    public void Stop() => _player.Stop();

    public void Dispose() => _player.Dispose();
}
