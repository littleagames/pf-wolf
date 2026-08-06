using NukedOPL3Sharp;

namespace Wolf3D.Assets.Sounds;

internal record AdLibSound : Asset
{
    public AdLibSound(byte[] data)
    {
        RawData = data;
    }

    private const int AdLibSampleRate = 44100;
    private const int SoundTicksPerSecond = 140;

    public byte[] ToMono8()
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

    private static byte[] RenderAdLib(byte[] instrument, byte block, byte[] tones)
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
        var samples = new byte[(tones.Length * framesPerTick) + releaseFrames];
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
            ConvertToMono8(stereo, samples.AsSpan(destination, framesPerTick));
            destination += framesPerTick;
        }
        chip.WriteRegister(0xb0, 0);
        stereo = new short[releaseFrames * 2];
        chip.GenerateStream(stereo);
        ConvertToMono8(stereo, samples.AsSpan(destination));
        ApplyGain(samples, 4.0);
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

    private static void ConvertToMono8(ReadOnlySpan<short> stereo, Span<byte> mono)
    {
        for (var frame = 0; frame < mono.Length; frame++)
        {
            var mixed = (stereo[frame * 2] + stereo[(frame * 2) + 1]) / 2;
            mono[frame] = (byte)Math.Clamp(128 + (mixed >> 8), byte.MinValue, byte.MaxValue);
        }
    }

    private static void ApplyGain(Span<byte> samples, double gain)
    {
        for (var index = 0; index < samples.Length; index++)
        {
            var amplified = 128 + ((samples[index] - 128) * gain);
            samples[index] = (byte)Math.Clamp(amplified, byte.MinValue, byte.MaxValue);
        }
    }
}