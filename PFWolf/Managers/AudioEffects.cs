using OpenTK.Audio.OpenAL;

namespace PFWolf.Managers;

/*
=============================================================================

AUDIO EFFECTS

OpenAL's EFX extension (OpenTK's ALC.EFX): filters on a source's sound, and (ambience plan phase
4b) reverb on auxiliary sends. The bundled OpenAL Soft has it on every OS; a library that hasn't
(Apple's framework, should OpenAL Soft fail to load) leaves everything as it was, every call here
doing nothing, and the soundinfo command says why.

Opened with the device, on AudioManager's opening thread: its context is current for the whole
process, and EFX's functions are found through it (alGetProcAddress).

A muffled sound (Gameplay/SoundMuffling.cs: a shut door between) loses its treble as well as its
loudness: a low-pass on its direct path, its high frequencies down to LowpassFor(what's left of
it). OpenAL copies a filter's values as it's put on a source, so one filter, set afresh each
time, serves every source.

=============================================================================
*/
internal sealed class AudioEffects
{
    int lowpass;
    bool available;

    /// <summary>Whether EFX is there to use</summary>
    internal bool Available => available;

    /// <summary>Why it isn't, when it isn't (null until Open, or when it is)</summary>
    internal string? Failure { get; private set; }

    /// <summary>What EFX the device has, for soundinfo: its version and sends per source, or why there's none</summary>
    internal string Description { get; private set; } = "EFX: not looked for (sound is off)";

    /// <summary>Finds out whether the device has EFX and makes what's used from it; with the context current</summary>
    internal void Open(ALDevice device)
    {
        try
        {
            if (!ALC.EFX.IsExtensionPresent(device))
            {
                Fail("the OpenAL library has no EFX (ALC_EXT_EFX)");
                return;
            }
            AL.GetError();      // nothing left over to be taken for ours
            lowpass = ALC.EFX.GenFilter();
            ALC.EFX.Filter(lowpass, FilterInteger.FilterType, (int)FilterType.Lowpass);
            var error = AL.GetError();
            if (lowpass == 0 || error != ALError.NoError)
            {
                if (lowpass != 0)
                    ALC.EFX.DeleteFilter(lowpass);
                lowpass = 0;
                Fail($"its low-pass filter couldn't be made ({error})");
                return;
            }

            available = true;
            int major = 0, minor = 0, sends = 0;
            ALC.EFX.GetInteger(device, EFXContextInteger.EFXMajorVersion, 1, ref major);
            ALC.EFX.GetInteger(device, EFXContextInteger.EFXMinorVersion, 1, ref minor);
            ALC.EFX.GetInteger(device, EFXContextInteger.MaxAuxiliarySends, 1, ref sends);
            Description = $"EFX {major}.{minor}, {sends} auxiliary send{(sends == 1 ? "" : "s")} a source";
        }
        catch (Exception e) when (e is EntryPointNotFoundException or DllNotFoundException or InvalidOperationException)
        {
            lowpass = 0;
            Fail($"its functions couldn't be found ({e.Message})");
        }
    }

    void Fail(string why)
    {
        available = false;
        Failure = why;
        Description = $"EFX: none, so no muffled treble or reverb ({why})";
    }

    /// <summary>
    /// A source's direct path: dulled for what's left of it through shut doors (LowpassFor), or
    /// left as it is (no filter) for null
    /// </summary>
    internal void SetDirectFilter(int source, float? gainHF)
    {
        if (!available)
            return;
        if (gainHF is float hf)
        {
            ALC.EFX.Filter(lowpass, FilterFloat.LowpassGain, 1.0f);
            ALC.EFX.Filter(lowpass, FilterFloat.LowpassGainHF, hf);
            ALC.EFX.Source(source, EFXSourceInteger.DirectFilter, lowpass);
        }
        else
            ALC.EFX.Source(source, EFXSourceInteger.DirectFilter, 0);
    }

    /// <summary>Deletes what Open made, before the context goes</summary>
    internal void Shutdown()
    {
        if (available && lowpass != 0)
            ALC.EFX.DeleteFilter(lowpass);
        lowpass = 0;
        available = false;
    }

    /// <summary>The level a muffled sound's high frequencies are left at</summary>
    internal const float DullingPower = 1.5f;

    /// <summary>
    /// A sound with <paramref name="muffle"/> left of it through shut doors: its high frequencies'
    /// level on the low-pass (muffle^1.5, so a door dulls it more than it quietens it), or null for
    /// no filter (not muffled)
    /// </summary>
    internal static float? LowpassFor(float muffle) =>
        muffle >= 0.999f ? null : MathF.Pow(Math.Clamp(muffle, 0, 1), DullingPower);
}
