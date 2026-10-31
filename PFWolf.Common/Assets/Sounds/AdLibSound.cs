using NukedOPL3Sharp;

namespace PFWolf.Assets.Sounds;

public record AdLibSound : Asset
{
    public AdLibSound(byte[] data)
    {
        RawData = data;
    }

    /// <summary>
    /// A pk3's sounds/NAME.adl: an AdLib sound as AUDIOT keeps it (a 32-bit length, a 16-bit
    /// priority, the 16-byte instrument, the octave block, then one note value a tick, 140 a second)
    /// </summary>
    public static AdLibSound FromFile(byte[] data)
    {
        if (data.Length < 23 || BitConverter.ToUInt32(data, 0) > (uint)(data.Length - 23))
            throw new InvalidDataException("not an AdLib sound: its length runs past the end of the file");
        return new AdLibSound(data);
    }

    private const int AdLibSampleRate = 44100;
    private const int SoundTicksPerSecond = 140;
    // The OPL's output for one channel peaks near ±4100, so this brings it up to about half scale
    private const int Gain = 4;

    /// <summary>The sound as 16-bit mono samples at 44100 Hz</summary>
    public short[] ToPcm16()
    {
        using var dataReader = new BinaryReader(new MemoryStream(RawData));
        var length = dataReader.ReadUInt32();
        dataReader.ReadUInt16(); // Priority.
        var instrument = dataReader.ReadBytes(16);
        var block = dataReader.ReadByte();
        if (length > RawData.Length - 23 || length > int.MaxValue)
            throw new InvalidDataException($"AdLib sound  has an invalid length.");
        return RenderAdLib(instrument, block, dataReader.ReadBytes((int)length));
    }

    private static short[] RenderAdLib(byte[] instrument, byte block, byte[] tones)
    {
        const int modifier = 0;
        const int carrier = 3;
        var chip = new Opl3Chip();
        chip.Reset(AdLibSampleRate);
        chip.WriteRegister(0x01, 0x20); // Enable the OPL2 waveform-select registers, as Wolf3D does at startup.
        WriteOperator(chip, modifier, instrument, 0);
        WriteOperator(chip, carrier, instrument, 1);
        chip.WriteRegister(0xc0, 0);

        var framesPerTick = AdLibSampleRate / SoundTicksPerSecond;
        var releaseFrames = AdLibSampleRate / 5;
        var samples = new short[(tones.Length * framesPerTick) + releaseFrames];
        var destination = 0;
        var stereo = new short[framesPerTick * 2];
        foreach (var tone in tones)
        {
            if (tone == 0)
                chip.WriteRegister(0xb0, 0);
            else
            {
                chip.WriteRegister(0xa0, tone);
                chip.WriteRegister(0xb0, (byte)(((block & 7) << 2) | 0x20));
            }
            chip.GenerateStream(stereo);
            ConvertToMono(stereo, samples.AsSpan(destination, framesPerTick));
            destination += framesPerTick;
        }
        chip.WriteRegister(0xb0, 0);
        stereo = new short[releaseFrames * 2];
        chip.GenerateStream(stereo);
        ConvertToMono(stereo, samples.AsSpan(destination));
        return samples;
    }

    private static void WriteOperator(Opl3Chip chip, int registerOffset, byte[] instrument, int fieldOffset)
    {
        chip.WriteRegister((ushort)(0x20 + registerOffset), instrument[fieldOffset]);
        chip.WriteRegister((ushort)(0x40 + registerOffset), instrument[fieldOffset + 2]);
        chip.WriteRegister((ushort)(0x60 + registerOffset), instrument[fieldOffset + 4]);
        chip.WriteRegister((ushort)(0x80 + registerOffset), instrument[fieldOffset + 6]);
        chip.WriteRegister((ushort)(0xe0 + registerOffset), instrument[fieldOffset + 8]);
    }

    // Gained while still 16-bit: cut to 8 bits first, a quiet sound (the menu's gun moves peak
    // near ±1200) was left with a dozen levels and sounded harsh
    private static void ConvertToMono(ReadOnlySpan<short> stereo, Span<short> mono)
    {
        for (var frame = 0; frame < mono.Length; frame++)
        {
            var mixed = (stereo[frame * 2] + stereo[(frame * 2) + 1]) * Gain / 2;
            mono[frame] = (short)Math.Clamp(mixed, short.MinValue, short.MaxValue);
        }
    }
}