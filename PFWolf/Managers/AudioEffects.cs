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
    int lowpass, sendFilter;
    bool available;

    // The reverb (ReverbMixer): two auxiliary slots, each with an effect of its own
    readonly int[] slots = new int[ReverbMixer.Slots];
    readonly int[] reverbs = new int[ReverbMixer.Slots];
    bool eaxReverb;     // the EAX reverb type (every preset number); else the standard reverb's

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

            sendFilter = ALC.EFX.GenFilter();
            ALC.EFX.Filter(sendFilter, FilterInteger.FilterType, (int)FilterType.Lowpass);

            available = true;
            int major = 0, minor = 0, sends = 0;
            ALC.EFX.GetInteger(device, EFXContextInteger.EFXMajorVersion, 1, ref major);
            ALC.EFX.GetInteger(device, EFXContextInteger.EFXMinorVersion, 1, ref minor);
            ALC.EFX.GetInteger(device, EFXContextInteger.MaxAuxiliarySends, 1, ref sends);
            string reverb = OpenReverb() is { } why ? $"; no reverb ({why})" : $"; reverb {(eaxReverb ? "EAX" : "standard")}";
            Description = $"EFX {major}.{minor}, {sends} auxiliary send{(sends == 1 ? "" : "s")} a source{reverb}";
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
        if (available)
        {
            for (int slot = 0; slot < slots.Length; slot++)
            {
                if (slots[slot] != 0)
                {
                    ALC.EFX.AuxiliaryEffectSlot(slots[slot], EffectSlotInteger.Effect, 0);
                    ALC.EFX.DeleteAuxiliaryEffectSlot(slots[slot]);
                }
                if (reverbs[slot] != 0)
                    ALC.EFX.DeleteEffect(reverbs[slot]);
                slots[slot] = reverbs[slot] = 0;
            }
            if (lowpass != 0)
                ALC.EFX.DeleteFilter(lowpass);
            if (sendFilter != 0)
                ALC.EFX.DeleteFilter(sendFilter);
        }
        lowpass = sendFilter = 0;
        available = false;
        ReverbAvailable = false;
    }

    /// <summary>Whether there's reverb: EFX, and its two slots made</summary>
    internal bool ReverbAvailable { get; private set; }

    // The two slots, each with a reverb effect (EAX's where there is, else the standard one);
    // null when they're made, else why not
    string? OpenReverb()
    {
        AL.GetError();
        for (int slot = 0; slot < slots.Length; slot++)
        {
            slots[slot] = ALC.EFX.GenAuxiliaryEffectSlot();
            reverbs[slot] = ALC.EFX.GenEffect();
            ALC.EFX.AuxiliaryEffectSlot(slots[slot], EffectSlotFloat.Gain, 0.0f);
        }
        var made = AL.GetError();
        if (made != ALError.NoError || slots.Contains(0))
            return $"its slots couldn't be made ({made})";

        ALC.EFX.Effect(reverbs[0], EffectInteger.EffectType, (int)EffectType.EaxReverb);
        eaxReverb = AL.GetError() == ALError.NoError;
        foreach (var effect in reverbs)
            ALC.EFX.Effect(effect, EffectInteger.EffectType, (int)(eaxReverb ? EffectType.EaxReverb : EffectType.Reverb));
        var typed = AL.GetError();
        if (typed != ALError.NoError)
            return $"no reverb effect ({typed})";
        ReverbAvailable = true;
        return null;
    }

    // Each preset's numbers by its name without hyphens (ReverbNames.Key): OpenTK's ReverbPresets
    static readonly Dictionary<string, ReverbProperties> presets = typeof(ReverbPresets)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(field => field.FieldType == typeof(ReverbProperties))
        .ToDictionary(field => field.Name.ToLowerInvariant(), field => (ReverbProperties)field.GetValue(null)!);

    /// <summary>A preset's numbers by its name (any case, hyphens or none), or null for none there is</summary>
    internal static ReverbProperties? Preset(string name) =>
        presets.TryGetValue(Assets.Sounds.ReverbNames.Key(name), out var preset) ? preset : null;

    /// <summary>The names OpenTK has presets for, without hyphens (for the tests)</summary>
    internal static IEnumerable<string> PresetKeys => presets.Keys;

    /// <summary>Loads a preset into a slot's effect and puts it on the slot; false with no reverb or no such preset</summary>
    internal bool LoadReverb(int slot, string name)
    {
        if (!ReverbAvailable || Preset(name) is not { } p)
            return false;
        int effect = reverbs[slot];
        if (eaxReverb)
        {
            ALC.EFX.Effect(effect, EffectFloat.EaxReverbDensity, p.Density);
            ALC.EFX.Effect(effect, EffectFloat.EaxReverbDiffusion, p.Diffusion);
            ALC.EFX.Effect(effect, EffectFloat.EaxReverbGain, p.Gain);
            ALC.EFX.Effect(effect, EffectFloat.EaxReverbGainHF, p.GainHF);
            ALC.EFX.Effect(effect, EffectFloat.EaxReverbGainLF, p.GainLF);
            ALC.EFX.Effect(effect, EffectFloat.EaxReverbDecayTime, p.DecayTime);
            ALC.EFX.Effect(effect, EffectFloat.EaxReverbDecayHFRatio, p.DecayHFRatio);
            ALC.EFX.Effect(effect, EffectFloat.EaxReverbDecayLFRatio, p.DecayLFRatio);
            ALC.EFX.Effect(effect, EffectFloat.EaxReverbReflectionsGain, p.ReflectionsGain);
            ALC.EFX.Effect(effect, EffectFloat.EaxReverbReflectionsDelay, p.ReflectionsDelay);
            var reflectionsPan = p.ReflectionsPan;
            ALC.EFX.Effect(effect, EffectVector3.EaxReverbReflectionsPan, ref reflectionsPan);
            ALC.EFX.Effect(effect, EffectFloat.EaxReverbLateReverbGain, p.LateReverbGain);
            ALC.EFX.Effect(effect, EffectFloat.EaxReverbLateReverbDelay, p.LateReverbDelay);
            var latePan = p.LateReverbPan;
            ALC.EFX.Effect(effect, EffectVector3.EaxReverbLateReverbPan, ref latePan);
            ALC.EFX.Effect(effect, EffectFloat.EaxReverbEchoTime, p.EchoTime);
            ALC.EFX.Effect(effect, EffectFloat.EaxReverbEchoDepth, p.EchoDepth);
            ALC.EFX.Effect(effect, EffectFloat.EaxReverbModulationTime, p.ModulationTime);
            ALC.EFX.Effect(effect, EffectFloat.EaxReverbModulationDepth, p.ModulationDepth);
            ALC.EFX.Effect(effect, EffectFloat.EaxReverbAirAbsorptionGainHF, p.AirAbsorptionGainHF);
            ALC.EFX.Effect(effect, EffectFloat.EaxReverbHFReference, p.HFReference);
            ALC.EFX.Effect(effect, EffectFloat.EaxReverbLFReference, p.LFReference);
            ALC.EFX.Effect(effect, EffectFloat.EaxReverbRoomRolloffFactor, p.RoomRolloffFactor);
            ALC.EFX.Effect(effect, EffectInteger.EaxReverbDecayHFLimit, p.DecayHFLimit);
        }
        else
        {
            // The standard reverb has no LF, echo, modulation or pans: the rest of the preset
            ALC.EFX.Effect(effect, EffectFloat.ReverbDensity, p.Density);
            ALC.EFX.Effect(effect, EffectFloat.ReverbDiffusion, p.Diffusion);
            ALC.EFX.Effect(effect, EffectFloat.ReverbGain, p.Gain);
            ALC.EFX.Effect(effect, EffectFloat.ReverbGainHF, p.GainHF);
            ALC.EFX.Effect(effect, EffectFloat.ReverbDecayTime, p.DecayTime);
            ALC.EFX.Effect(effect, EffectFloat.ReverbDecayHFRatio, p.DecayHFRatio);
            ALC.EFX.Effect(effect, EffectFloat.ReverbReflectionsGain, p.ReflectionsGain);
            ALC.EFX.Effect(effect, EffectFloat.ReverbReflectionsDelay, p.ReflectionsDelay);
            ALC.EFX.Effect(effect, EffectFloat.ReverbLateReverbGain, p.LateReverbGain);
            ALC.EFX.Effect(effect, EffectFloat.ReverbLateReverbDelay, p.LateReverbDelay);
            ALC.EFX.Effect(effect, EffectFloat.ReverbAirAbsorptionGainHF, p.AirAbsorptionGainHF);
            ALC.EFX.Effect(effect, EffectFloat.ReverbRoomRolloffFactor, p.RoomRolloffFactor);
            ALC.EFX.Effect(effect, EffectInteger.ReverbDecayHFLimit, p.DecayHFLimit);
        }
        // A slot copies its effect's numbers as it's given it: given it again, it has the new ones
        ALC.EFX.AuxiliaryEffectSlot(slots[slot], EffectSlotInteger.Effect, effect);
        return true;
    }

    /// <summary>A slot's gain (its fade and the master), 0 to 1</summary>
    internal void SetSlotGain(int slot, float gain)
    {
        if (ReverbAvailable)
            ALC.EFX.AuxiliaryEffectSlot(slots[slot], EffectSlotFloat.Gain, Math.Clamp(gain, 0, 1));
    }

    /// <summary>
    /// A source's send to a slot (-1: none), at <paramref name="level"/> of it (sound-seq reverb),
    /// its treble at <paramref name="gainHF"/> while it's muffled (null: as it is)
    /// </summary>
    internal void SetSend(int source, int slot, float level, float? gainHF)
    {
        if (!ReverbAvailable)
            return;
        if (slot < 0 || level <= 0)
        {
            ALC.EFX.Source(source, EFXSourceInteger3.AuxiliarySendFilter, 0, 0, 0);
            return;
        }
        int filter = 0;
        if (level < 0.999f || gainHF != null)
        {
            ALC.EFX.Filter(sendFilter, FilterFloat.LowpassGain, Math.Clamp(level, 0, 1));
            ALC.EFX.Filter(sendFilter, FilterFloat.LowpassGainHF, gainHF ?? 1.0f);
            filter = sendFilter;
        }
        ALC.EFX.Source(source, EFXSourceInteger3.AuxiliarySendFilter, slots[slot], 0, filter);
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
