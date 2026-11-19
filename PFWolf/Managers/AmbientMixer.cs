namespace PFWolf.Managers;

/*
=============================================================================

AMBIENT MIXER

The ambient loop's two voices, so that one sound can fade out while the next fades in (a zone's
ambient-fade, Lua's game.ambient(sound, tics)). The voice the asked-for sound plays on is the
current one; the other is fading out, or silent. With no fade, a change cuts over at once, as
the ambient loop always did.

Asking for the sound that's playing changes nothing; asked for again while it fades out (it was
stopped, or another took its place), it fades back in from where it got to. Asked for after
everything was stopped (StopAll), it starts again.

Levels are 0 to 1, a share of the sound's own loudness (AudioManager: the sound volume setting
and its sound-seq volume). Only AudioManager plays anything: the hooks keep this to the
bookkeeping, so it can be tested without a sound device.

=============================================================================
*/
/// <param name="Start">Starts a voice looping a sound (from silence); false if there's nothing to play</param>
/// <param name="Stop">Stops a voice</param>
/// <param name="IsPlaying">Whether a voice is sounding (false once StopAll stopped it)</param>
/// <param name="SetLevel">A voice's level, 0 to 1</param>
internal sealed record AmbientMixerHooks(
    Func<int, string, bool> Start,
    Action<int> Stop,
    Func<int, bool> IsPlaying,
    Action<int, float> SetLevel);

internal sealed class AmbientMixer(AmbientMixerHooks hooks)
{
    /// <summary>The two voices</summary>
    internal const int Voices = 2;

    readonly string?[] names = new string?[Voices];     // what each voice plays (null: silent)
    readonly float[] levels = new float[Voices];
    readonly float[] steps = new float[Voices];         // a tic's change in level while fading

    int current;

    // A fade's step times its tics needn't land on 0 or 1 exactly in floats: this near is there
    const float Near = 0.0001f;

    /// <summary>The sound asked for last, null for none</summary>
    internal string? Requested { get; private set; }

    /// <summary>A voice's sound, null when it's silent</summary>
    internal string? Name(int voice) => names[voice];

    /// <summary>A voice's level, 0 to 1</summary>
    internal float Level(int voice) => levels[voice];

    /// <summary>The voice the asked-for sound plays on</summary>
    internal int Current => current;

    /// <summary>
    /// Loops a sound in place of the one there was, or none with null, the change fading over
    /// so many tics (0: at once)
    /// </summary>
    internal void Set(string? name, int fadeTics)
    {
        name = string.IsNullOrWhiteSpace(name) ? null : name;
        Requested = name;
        if (name == null)
        {
            FadeOut(current, fadeTics);
            return;
        }

        if (Same(names[current], name) && hooks.IsPlaying(current))
        {
            FadeIn(current, fadeTics);          // the one playing (or back in, if it was going)
            return;
        }

        int other = Voices - 1 - current;
        if (Same(names[other], name) && hooks.IsPlaying(other))
        {
            FadeOut(current, fadeTics);         // the one fading out comes back
            current = other;
            FadeIn(other, fadeTics);
            return;
        }

        FadeOut(current, fadeTics);
        hooks.Stop(other);
        current = other;
        names[other] = name;
        steps[other] = 0;
        if (!hooks.Start(other, name))
        {
            levels[other] = 0;
            return;                             // nothing to play: kept as asked, tried again when asked again
        }
        if (fadeTics > 0)
        {
            levels[other] = 0;
            steps[other] = 1.0f / fadeTics;
        }
        else
            levels[other] = 1;
        hooks.SetLevel(other, levels[other]);
    }

    /// <summary>The voices sounding, the asked-for one first: each sound, its level and which way it's fading</summary>
    internal IEnumerable<string> Describe()
    {
        foreach (int voice in new[] { current, Voices - 1 - current })
        {
            if (names[voice] is not { } name)
                continue;
            var fading = steps[voice] > 0 ? ", fading in" : steps[voice] < 0 ? ", fading out" : "";
            yield return $"{name} {levels[voice] * 100:0}%{fading}";
        }
    }

    /// <summary>Fades move on by so many tics</summary>
    internal void Tick(int tics)
    {
        if (tics <= 0)
            return;
        for (int voice = 0; voice < Voices; voice++)
        {
            if (steps[voice] == 0)
                continue;
            levels[voice] += steps[voice] * tics;
            if (levels[voice] >= 1 - Near)
            {
                levels[voice] = 1;
                steps[voice] = 0;
            }
            else if (levels[voice] <= Near)
            {
                Silence(voice);
                continue;
            }
            hooks.SetLevel(voice, levels[voice]);
        }
    }

    /// <summary>Everything was stopped (StopAll): no fades go on; what's asked for again starts afresh</summary>
    internal void Stopped()
    {
        for (int voice = 0; voice < Voices; voice++)
        {
            steps[voice] = 0;
            levels[voice] = 0;
            if (voice != current)
                names[voice] = null;
        }
    }

    void FadeIn(int voice, int fadeTics)
    {
        if (fadeTics <= 0 || levels[voice] >= 1)
        {
            steps[voice] = 0;
            levels[voice] = 1;
            hooks.SetLevel(voice, 1);
        }
        else
            steps[voice] = 1.0f / fadeTics;
    }

    void FadeOut(int voice, int fadeTics)
    {
        if (names[voice] == null)
            return;
        if (fadeTics <= 0 || !hooks.IsPlaying(voice))
            Silence(voice);
        else
            steps[voice] = -1.0f / fadeTics;
    }

    void Silence(int voice)
    {
        hooks.Stop(voice);
        names[voice] = null;
        levels[voice] = 0;
        steps[voice] = 0;
    }

    static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
