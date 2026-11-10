namespace PFWolf.Assets.Sounds;

/// <summary>
/// One OPL2 operator's registers: 0x20 (tremolo, vibrato, sustaining, key scale rate,
/// multiplier), 0x40 (key scale level, total level), 0x60 (attack, decay), 0x80 (sustain,
/// release) and 0xE0 (waveform)
/// </summary>
public readonly record struct OplOperator(byte Characteristic, byte Level, byte AttackDecay, byte SustainRelease, byte Waveform);

/// <summary>
/// A two-operator OPL2 voice: what a song's channel or an AdLib sound plays with. The modulator
/// and carrier's registers, and 0xC0 (feedback, connection).
/// </summary>
public readonly record struct OplInstrument(OplOperator Modulator, OplOperator Carrier, byte FeedbackConnection)
{
    /// <summary>The bytes an instrument takes in an AdLib sound: mChar, cChar, mScale, cScale, mAttack, cAttack, mSus, cSus, mWave, cWave, nConn</summary>
    public const int AdLibBytes = 11;

    /// <summary>An instrument as an AdLib sound keeps it (each field modulator then carrier, then nConn)</summary>
    public static OplInstrument FromAdLibBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < AdLibBytes)
            throw new ArgumentException($"An instrument is {AdLibBytes} bytes", nameof(bytes));
        return new OplInstrument(
            new OplOperator(bytes[0], bytes[2], bytes[4], bytes[6], bytes[8]),
            new OplOperator(bytes[1], bytes[3], bytes[5], bytes[7], bytes[9]),
            bytes[10]);
    }

    /// <summary>The instrument as an AdLib sound keeps it</summary>
    public void ToAdLibBytes(Span<byte> bytes)
    {
        if (bytes.Length < AdLibBytes)
            throw new ArgumentException($"An instrument is {AdLibBytes} bytes", nameof(bytes));
        bytes[0] = Modulator.Characteristic;
        bytes[1] = Carrier.Characteristic;
        bytes[2] = Modulator.Level;
        bytes[3] = Carrier.Level;
        bytes[4] = Modulator.AttackDecay;
        bytes[5] = Carrier.AttackDecay;
        bytes[6] = Modulator.SustainRelease;
        bytes[7] = Carrier.SustainRelease;
        bytes[8] = Modulator.Waveform;
        bytes[9] = Carrier.Waveform;
        bytes[10] = FeedbackConnection;
    }

    /// <summary>The instrument as it's read from a channel's registers</summary>
    public static OplInstrument FromRegisters(ReadOnlySpan<byte> registers, int channel)
    {
        var modulator = OplRegisters.ModulatorSlot(channel);
        var carrier = modulator + 3;
        return new OplInstrument(ReadOperator(registers, modulator), ReadOperator(registers, carrier), registers[0xC0 + channel]);

        static OplOperator ReadOperator(ReadOnlySpan<byte> registers, int slot)
            => new(registers[0x20 + slot], registers[0x40 + slot], registers[0x60 + slot], registers[0x80 + slot], registers[0xE0 + slot]);
    }

    /// <summary>The register writes that put the instrument on a channel (0-8), modulator, carrier, then 0xC0</summary>
    public IEnumerable<(byte Register, byte Value)> Registers(int channel)
    {
        var modulator = OplRegisters.ModulatorSlot(channel);
        foreach (var write in OperatorRegisters(Modulator, modulator))
            yield return write;
        foreach (var write in OperatorRegisters(Carrier, modulator + 3))
            yield return write;
        yield return ((byte)(0xC0 + channel), FeedbackConnection);
    }

    private static IEnumerable<(byte Register, byte Value)> OperatorRegisters(OplOperator op, int slot)
    {
        yield return ((byte)(0x20 + slot), op.Characteristic);
        yield return ((byte)(0x40 + slot), op.Level);
        yield return ((byte)(0x60 + slot), op.AttackDecay);
        yield return ((byte)(0x80 + slot), op.SustainRelease);
        yield return ((byte)(0xE0 + slot), op.Waveform);
    }
}

/// <summary>Where an OPL2's registers are: which channel and operator an operator register belongs to</summary>
public static class OplRegisters
{
    public const int Channels = 9;

    // The modulator's operator slot of each channel; its carrier's is 3 on
    private static readonly int[] ModulatorSlots = [0, 1, 2, 8, 9, 10, 16, 17, 18];

    /// <summary>The operator slot (the register's low bits) of a channel's modulator</summary>
    public static int ModulatorSlot(int channel) => ModulatorSlots[channel];

    /// <summary>
    /// The channel and operator an operator register (0x20-0x95, 0xE0-0xF5) belongs to; null for
    /// any other register, or a slot no channel has
    /// </summary>
    public static (int Channel, bool IsCarrier)? OperatorOf(byte register)
    {
        var group = register & 0xE0;
        if (group is not (0x20 or 0x40 or 0x60 or 0x80 or 0xE0))
            return null;
        var slot = register & 0x1F;
        for (var channel = 0; channel < Channels; channel++)
        {
            if (ModulatorSlots[channel] == slot)
                return (channel, false);
            if (ModulatorSlots[channel] + 3 == slot)
                return (channel, true);
        }
        return null;
    }

    /// <summary>The channel of an A0-A8 (F-number low), B0-B8 (key on, block, F-number high) or C0-C8 register; null for others</summary>
    public static int? ChannelOf(byte register, int group)
        => (register & 0xF0) == group && (register & 0x0F) < Channels ? register & 0x0F : null;

    /// <summary>The carrier's 0x40 register (key scale level, total level: the channel's volume)</summary>
    public static byte CarrierLevel(int channel) => (byte)(0x40 + ModulatorSlots[channel] + 3);

    /// <summary>The key-on bit of a B0-B8 register</summary>
    public const byte KeyOn = 0x20;
}

/// <summary>OPL pitches: an F-number (0-1023) and block (0-7), and the note they're nearest</summary>
public static class OplPitch
{
    /// <summary>The OPL2's clock / 72: a block 0 F-number of 1 is this / 2^20 Hz</summary>
    public const double ChipRate = 49716;

    public static double Frequency(int fNumber, int block) => fNumber * ChipRate / (1 << (20 - block));

    /// <summary>The MIDI note number (60 = middle C, 69 = A 440) of a frequency, with a fraction</summary>
    public static double MidiNote(double frequency) => frequency <= 0 ? 0 : 69 + 12 * Math.Log2(frequency / 440);

    /// <summary>
    /// The F-number and block of a frequency: the lowest block it fits in, for the finest F-number;
    /// null past what the chip plays
    /// </summary>
    public static (int FNumber, int Block)? FromFrequency(double frequency)
    {
        if (frequency <= 0)
            return null;
        for (var block = 0; block < 8; block++)
        {
            var fNumber = (int)Math.Round(frequency * (1 << (20 - block)) / ChipRate);
            if (fNumber < 1024)
                return (fNumber, block);
        }
        return null;
    }

    public static (int FNumber, int Block)? FromMidiNote(double note) => FromFrequency(440 * Math.Pow(2, (note - 69) / 12));
}
