namespace Wolf3D.Assets.Sounds;

internal record PcSound : Asset
{
    private const int SampleRate = 44100;
    private const int SoundTicksPerSecond = 140;
    private const float SquareWaveAmplitude = 4000.0f; // Amplitude for square wave generation
    private const float PitFrequency = 1193180.0f; // PC PIT counts down at this frequency
    private const float PitScaleFactor = 60.0f; // Reload value multiplier (see SDL_DoFX)

    public PcSound(byte[] data)
    {
        RawData = data;
    }

    public byte[] ToMono8()
    {
        using var dataReader = new BinaryReader(new MemoryStream(RawData));
        var length = dataReader.ReadUInt32();
        dataReader.ReadUInt16(); // Priority.

        if (length > RawData.Length - 6 || length > int.MaxValue)
            throw new InvalidDataException("PC sound has an invalid length.");

        var pcData = dataReader.ReadBytes((int)length);

        // PC speaker sounds are sampled at 140 Hz
        // Each byte represents a PIT reload value
        // We need to expand this to 44100 Hz and generate square waves
        int framesPerTick = SampleRate / SoundTicksPerSecond;
        int outputSamples = checked(pcData.Length * framesPerTick);

        var stereoSamples = new short[outputSamples * 2]; // Stereo 16-bit
        int phaseOffset = 0;

        for (int tick = 0; tick < pcData.Length; tick++)
        {
            byte pitValue = pcData[tick];

            // Calculate frequency from PIT reload value
            // frequency = PIT_FREQ / (pitValue * scale_factor)
            float frequency = pitValue != 0 ? PitFrequency / (pitValue * PitScaleFactor) : 0;

            // Generate square wave for this tick
            for (int frame = 0; frame < framesPerTick; frame++)
            {
                int sampleIndex = (tick * framesPerTick + frame) * 2;

                if (frequency == 0)
                {
                    // Silence
                    stereoSamples[sampleIndex] = 0;
                    stereoSamples[sampleIndex + 1] = 0;
                }
                else
                {
                    // Generate square wave
                    // frac = (phaseOffset * frequency * 2) / sampleRate
                    // If frac % 2 == 0, we're at peak; otherwise at trough
                    int frac = (int)((phaseOffset * frequency * 2) / SampleRate);
                    short sample = ((frac % 2) == 0) ? (short)SquareWaveAmplitude : (short)-SquareWaveAmplitude;

                    stereoSamples[sampleIndex] = sample;
                    stereoSamples[sampleIndex + 1] = sample;

                    phaseOffset++;
                }
            }
        }

        return ConvertToMono8(stereoSamples);
    }

    private static byte[] ConvertToMono8(ReadOnlySpan<short> stereo)
    {
        var mono = new byte[stereo.Length / 2];

        for (int frame = 0; frame < mono.Length; frame++)
        {
            // Mix stereo channels and convert to 8-bit
            var mixed = (stereo[frame * 2] + stereo[(frame * 2) + 1]) / 2;
            mono[frame] = (byte)Math.Clamp(128 + (mixed >> 8), byte.MinValue, byte.MaxValue);
        }

        return mono;
    }
}