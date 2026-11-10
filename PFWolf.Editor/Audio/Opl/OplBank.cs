using PFWolf.Assets.Sounds;

namespace PFWolf.Editor.Audio.Opl;

/// <summary>
/// An instrument of a bank: the voice, how far its notes are moved (semitones), and the note a
/// drum always plays at, whatever key strikes it
/// </summary>
public sealed record OplBankInstrument(string Name, OplInstrument Instrument, int NoteOffset = 0, int? FixedNote = null);

/// <summary>
/// General MIDI's instruments as OPL2 voices, for importing MIDI files: the 128 programs, and
/// the drums by key (35-81). PFWolf's own <see cref="BuiltIn"/> gives each family of eight
/// programs one voice; a DMX GENMIDI bank (.op2, Doom's) loaded from disk gives all of them.
/// </summary>
public sealed class OplBank
{
    public const int FirstDrumKey = 35;
    public const int LastDrumKey = 81;

    public string Name { get; }
    private readonly OplBankInstrument[] _melodic;
    private readonly Dictionary<int, OplBankInstrument> _drums;

    private OplBank(string name, OplBankInstrument[] melodic, Dictionary<int, OplBankInstrument> drums)
    {
        Name = name;
        _melodic = melodic;
        _drums = drums;
    }

    public OplBankInstrument Melodic(int program) => _melodic[Math.Clamp(program, 0, 127)];

    /// <summary>The drum a key strikes on MIDI channel 10; null for a key the bank has none for</summary>
    public OplBankInstrument? Drum(int key) => _drums.GetValueOrDefault(key);

    public override string ToString() => Name;

    /// <summary>
    /// A DMX GENMIDI bank (.op2): "#OPL_II#", then 175 instruments of 36 bytes (flags, fine tune,
    /// fixed note, two voices of which the first is used), then their names, 32 bytes each.
    /// The first 128 are the programs, the rest the drums of keys 35-81.
    /// </summary>
    public static OplBank FromOp2(byte[] data, string name)
    {
        const int count = 175, size = 36, header = 8;
        if (data.Length < header + count * size || System.Text.Encoding.ASCII.GetString(data, 0, header) != "#OPL_II#")
            throw new InvalidDataException("not a DMX GENMIDI bank: it doesn't start with #OPL_II#");

        var hasNames = data.Length >= header + count * (size + 32);
        var instruments = new OplBankInstrument[count];
        for (var i = 0; i < count; i++)
        {
            var at = header + i * size;
            var flags = BitConverter.ToUInt16(data, at);
            var fixedNote = data[at + 3];
            var voice = at + 4;
            var instrument = new OplInstrument(Operator(voice), Operator(voice + 7), data[voice + 6]);
            var noteOffset = BitConverter.ToInt16(data, voice + 14);
            var label = hasNames ? System.Text.Encoding.Latin1.GetString(data, header + count * size + i * 32, 32).TrimEnd('\0', ' ') : $"#{i}";
            var cut = label.IndexOf('\0');
            instruments[i] = new OplBankInstrument(cut >= 0 ? label[..cut] : label, instrument,
                (flags & 1) != 0 ? 0 : noteOffset, (flags & 1) != 0 ? fixedNote : null);
        }

        var drums = new Dictionary<int, OplBankInstrument>();
        for (var key = FirstDrumKey; key <= LastDrumKey; key++)
            drums[key] = instruments[128 + key - FirstDrumKey];
        return new OplBank(name, instruments[..128], drums);

        // tremolo/vibrato/sustain/KSR/multiplier, attack/decay, sustain/release, waveform, key scale, level
        OplOperator Operator(int at) => new(data[at], (byte)(data[at + 4] | (data[at + 5] & 0x3F)), data[at + 1], data[at + 2], data[at + 3]);
    }

    /// <summary>
    /// PFWolf's own bank: one voice made here for each family of eight General MIDI programs, and
    /// a few drums. Rough by design (to be tuned by ear); load a GENMIDI bank for more.
    /// </summary>
    public static OplBank BuiltIn { get; } = MakeBuiltIn();

    // A voice from its registers: the modulator's 20, 40, 60, 80, E0, the carrier's, then C0
    private static OplInstrument Voice(byte m20, byte m40, byte m60, byte m80, byte mE0, byte c20, byte c40, byte c60, byte c80, byte cE0, byte c0)
        => new(new OplOperator(m20, m40, m60, m80, mE0), new OplOperator(c20, c40, c60, c80, cE0), c0);

    private static OplBank MakeBuiltIn()
    {
        (string Name, OplInstrument Voice, int Offset)[] families =
        [
            ("Piano", Voice(0x01, 0x1A, 0xF2, 0x53, 0, 0x01, 0x00, 0xF2, 0x74, 0, 0x08), 0),
            ("Chromatic percussion", Voice(0x07, 0x1F, 0xF4, 0x45, 0, 0x01, 0x00, 0xF3, 0x45, 0, 0x04), 0),
            ("Organ", Voice(0x22, 0x14, 0xF0, 0x07, 0, 0x21, 0x00, 0xF0, 0x07, 0, 0x01), 0),
            ("Guitar", Voice(0x01, 0x1C, 0xF3, 0x46, 0, 0x01, 0x00, 0xF4, 0x46, 1, 0x0A), 0),
            ("Bass", Voice(0x01, 0x18, 0xF5, 0x48, 0, 0x01, 0x00, 0xF6, 0x58, 0, 0x0C), 0),
            ("Strings", Voice(0x61, 0x1C, 0x71, 0x13, 0, 0x61, 0x00, 0x72, 0x13, 0, 0x0E), 0),
            ("Ensemble", Voice(0x62, 0x20, 0x61, 0x14, 0, 0x61, 0x00, 0x62, 0x14, 0, 0x06), 0),
            ("Brass", Voice(0x21, 0x16, 0x91, 0x16, 0, 0x21, 0x00, 0x81, 0x16, 0, 0x0E), 0),
            ("Reed", Voice(0x22, 0x1E, 0xA1, 0x16, 0, 0x21, 0x00, 0xA1, 0x16, 1, 0x0C), 0),
            ("Pipe", Voice(0x61, 0x2A, 0x86, 0x17, 0, 0x61, 0x00, 0x84, 0x17, 0, 0x00), 0),
            ("Synth lead", Voice(0x22, 0x10, 0xF1, 0x05, 0, 0x21, 0x00, 0xF1, 0x05, 0, 0x0E), 0),
            ("Synth pad", Voice(0x62, 0x24, 0x52, 0x12, 0, 0x61, 0x00, 0x51, 0x12, 0, 0x06), 0),
            ("Synth effects", Voice(0x63, 0x22, 0x41, 0x23, 0, 0x61, 0x00, 0x42, 0x23, 0, 0x08), 0),
            ("Ethnic", Voice(0x03, 0x18, 0xF5, 0x47, 0, 0x01, 0x00, 0xF5, 0x47, 0, 0x0A), 0),
            ("Percussive", Voice(0x05, 0x1E, 0xF8, 0x68, 0, 0x01, 0x00, 0xF7, 0x68, 0, 0x04), 0),
            ("Sound effects", Voice(0x0F, 0x00, 0xF2, 0x44, 0, 0x01, 0x00, 0xF3, 0x44, 0, 0x0E), 0),
        ];
        var melodic = new OplBankInstrument[128];
        for (var program = 0; program < 128; program++)
        {
            var (name, voice, offset) = families[program / 8];
            melodic[program] = new OplBankInstrument(name, voice, offset);
        }

        var kick = new OplBankInstrument("Kick", Voice(0x00, 0x0B, 0xF8, 0xA8, 0, 0x00, 0x00, 0xF6, 0x87, 0, 0x08), FixedNote: 26);
        var snare = new OplBankInstrument("Snare", Voice(0x0C, 0x00, 0xF8, 0xB6, 0, 0x00, 0x00, 0xF7, 0x96, 0, 0x0E), FixedNote: 60);
        var hatClosed = new OplBankInstrument("Hi-hat", Voice(0x0F, 0x00, 0xFA, 0xFA, 0, 0x0E, 0x00, 0xFA, 0xFA, 0, 0x0E), FixedNote: 84);
        var hatOpen = new OplBankInstrument("Open hi-hat", Voice(0x0F, 0x00, 0xF7, 0xF5, 0, 0x0E, 0x00, 0xF7, 0xF5, 0, 0x0E), FixedNote: 84);
        var cymbal = new OplBankInstrument("Cymbal", Voice(0x0E, 0x00, 0xF4, 0xF3, 0, 0x0F, 0x00, 0xF3, 0xF3, 0, 0x0E), FixedNote: 80);
        var tom = new OplBankInstrument("Tom", Voice(0x01, 0x08, 0xF7, 0x86, 0, 0x00, 0x00, 0xF6, 0x86, 0, 0x04));
        var block = new OplBankInstrument("Block", Voice(0x05, 0x10, 0xFA, 0xF8, 0, 0x01, 0x00, 0xFA, 0xF8, 0, 0x04), FixedNote: 72);

        var drums = new Dictionary<int, OplBankInstrument>();
        for (var key = FirstDrumKey; key <= LastDrumKey; key++)
        {
            drums[key] = key switch
            {
                35 or 36 => kick,
                38 or 40 or 37 or 39 => snare,
                42 or 44 => hatClosed,
                46 => hatOpen,
                49 or 51 or 52 or 53 or 55 or 57 or 59 => cymbal,
                41 or 43 or 45 or 47 or 48 or 50 => tom with { FixedNote = key + 4 },
                _ => block,
            };
        }
        return new OplBank("PFWolf's own (rough, one voice a family)", melodic, drums);
    }
}
