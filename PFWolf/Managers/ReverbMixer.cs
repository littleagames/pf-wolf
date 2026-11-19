namespace PFWolf.Managers;

/*
=============================================================================

REVERB MIXER

The reverb the player hears (a zone's, a map's, a script's game.reverb: Gameplay/ZoneReverb.cs)
on two EFX auxiliary slots, so that a change crossfades: the new preset is loaded into the idle
slot, every sound's send moves to it, and its gain comes up as the old slot's goes down. The old
slot's tail rings on while it fades, so a shot fired as the player leaves a cave still echoes
behind them. With no fade a change cuts over at once.

Asking for the reverb that's playing changes nothing; asked for again while it fades out (none
was asked for, or another), it fades back in. Master scales both slots: 0 while the game's paused
(a menu's click is dry) or the Reverb setting's off. After StopAll (a level's end) nothing rings,
and the reverb asked for next starts afresh.

Only AudioManager touches OpenAL: the hooks keep this to the bookkeeping, so it can be tested
without a sound device.

=============================================================================
*/
/// <param name="Load">Loads a preset into a slot; false when it can't (no EFX, an unknown name)</param>
/// <param name="SetGain">A slot's gain, 0 to 1</param>
/// <param name="Route">Every sound's send to a slot from now on, or none (-1: dry)</param>
internal sealed record ReverbMixerHooks(
    Func<int, string, bool> Load,
    Action<int, float> SetGain,
    Action<int> Route);

internal sealed class ReverbMixer(ReverbMixerHooks hooks)
{
    /// <summary>The two slots</summary>
    internal const int Slots = 2;

    readonly string?[] names = new string?[Slots];      // the preset each slot holds (null: silent)
    readonly float[] levels = new float[Slots];
    readonly float[] steps = new float[Slots];          // a tic's change in level while fading

    int current;
    float master = 1;
    const float Near = 0.0001f;

    /// <summary>The reverb asked for last, null for none (dry)</summary>
    internal string? Requested { get; private set; }

    /// <summary>The slot sounds are sent to now, -1 for none (dry)</summary>
    internal int Routed { get; private set; } = -1;

    /// <summary>A slot's preset, null when it's silent</summary>
    internal string? Name(int slot) => names[slot];

    /// <summary>A slot's level, 0 to 1, before Master</summary>
    internal float Level(int slot) => levels[slot];

    /// <summary>Both slots' loudness, 0 to 1: 0 while paused or with the Reverb setting off</summary>
    internal float Master
    {
        get => master;
        set
        {
            value = Math.Clamp(value, 0, 1);
            if (value == master)
                return;
            master = value;
            for (int slot = 0; slot < Slots; slot++)
                hooks.SetGain(slot, levels[slot] * master);
        }
    }

    /// <summary>
    /// A preset in place of the reverb there was, or dry with null (or "none"), crossfading
    /// over so many tics (0: at once)
    /// </summary>
    internal void Set(string? name, int fadeTics)
    {
        name = string.IsNullOrWhiteSpace(name) || Assets.Sounds.ReverbNames.IsNone(name) ? null : name.Trim();
        Requested = name;
        fadeTics = Math.Max(fadeTics, 0);
        if (name == null)
        {
            FadeOut(current, fadeTics);
            RouteTo(-1);
            return;
        }

        if (Same(names[current], name))
        {
            FadeIn(current, fadeTics);          // the one playing (or back in, if it was going)
            RouteTo(current);
            return;
        }

        int other = Slots - 1 - current;
        if (Same(names[other], name))
        {
            FadeOut(current, fadeTics);         // the one fading out comes back
            current = other;
            FadeIn(other, fadeTics);
            RouteTo(other);
            return;
        }

        FadeOut(current, fadeTics);
        if (names[other] != null)
            Silence(other);                     // a third so soon: the oldest tail is cut
        current = other;
        if (!hooks.Load(other, name))
        {
            RouteTo(-1);                        // nothing to hear: kept as asked, tried again when asked again
            return;
        }
        names[other] = name;
        if (fadeTics > 0)
        {
            levels[other] = 0;
            steps[other] = 1.0f / fadeTics;
        }
        else
        {
            levels[other] = 1;
            steps[other] = 0;
        }
        hooks.SetGain(other, levels[other] * master);
        RouteTo(other);
    }

    /// <summary>Fades move on by so many tics</summary>
    internal void Tick(int tics)
    {
        if (tics <= 0)
            return;
        for (int slot = 0; slot < Slots; slot++)
        {
            if (steps[slot] == 0)
                continue;
            levels[slot] += steps[slot] * tics;
            if (levels[slot] >= 1 - Near)
            {
                levels[slot] = 1;
                steps[slot] = 0;
            }
            else if (levels[slot] <= Near)
            {
                Silence(slot);
                continue;
            }
            hooks.SetGain(slot, levels[slot] * master);
        }
    }

    /// <summary>Everything was stopped (StopAll, a level's end): no reverb rings; what's asked for next starts afresh</summary>
    internal void Stopped()
    {
        for (int slot = 0; slot < Slots; slot++)
        {
            if (names[slot] != null || levels[slot] != 0)
                Silence(slot);
        }
        RouteTo(-1);
    }

    /// <summary>The slots sounding, the asked-for one first: each preset, its level and which way it's fading</summary>
    internal IEnumerable<string> Describe()
    {
        foreach (int slot in new[] { current, Slots - 1 - current })
        {
            if (names[slot] is not { } name)
                continue;
            var fading = steps[slot] > 0 ? ", fading in" : steps[slot] < 0 ? ", fading out" : "";
            yield return $"{name} {levels[slot] * 100:0}%{fading}";
        }
    }

    void RouteTo(int slot)
    {
        if (slot == Routed)
            return;
        Routed = slot;
        hooks.Route(slot);
    }

    void FadeIn(int slot, int fadeTics)
    {
        if (fadeTics <= 0 || levels[slot] >= 1)
        {
            steps[slot] = 0;
            if (levels[slot] != 1)
            {
                levels[slot] = 1;
                hooks.SetGain(slot, master);
            }
        }
        else
            steps[slot] = 1.0f / fadeTics;
    }

    void FadeOut(int slot, int fadeTics)
    {
        if (names[slot] == null)
            return;
        if (fadeTics <= 0)
            Silence(slot);
        else
            steps[slot] = -1.0f / fadeTics;
    }

    void Silence(int slot)
    {
        names[slot] = null;
        levels[slot] = 0;
        steps[slot] = 0;
        hooks.SetGain(slot, 0);
    }

    static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
