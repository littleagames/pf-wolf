namespace Wolf3D.Assets.Sounds;

internal struct headchunk
{
    public byte[] RIFF;
    public uint filelenminus8;
    public byte[] WAVE;
    public byte[] fmt_;
    public uint formatlen;
    public ushort val0x0001;
    public ushort channels;
    public uint samplerate;
    public uint bytespersec;
    public ushort bytespersample;
    public ushort bitspersample;
    public headchunk()
    {
        RIFF = new byte[4];
        WAVE = new byte[4];
        fmt_ = new byte[4];
    }

    public static int size_of =>
        4 * sizeof(byte)
        + sizeof(uint)
        + 4 * sizeof(byte)
        + 4 * sizeof(byte)
        + sizeof(uint)
        + sizeof(ushort) * 2
        + sizeof(uint) * 2
        + sizeof(ushort) * 2;
    public byte[] AsBytes()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        {
            bw.Write(RIFF);
            bw.Write(filelenminus8);
            bw.Write(WAVE);
            bw.Write(fmt_);
            bw.Write(formatlen);
            bw.Write(val0x0001);
            bw.Write(channels);
            bw.Write(samplerate);
            bw.Write(bytespersec);
            bw.Write(bytespersample);
            bw.Write(bitspersample);
            return ms.ToArray();
        }
    }
}

internal class wavechunk
{
    public byte[] chunkid;
    public uint chunklength;
    public wavechunk()
    {
        chunkid = new byte[4];
    }

    public static int size_of =>
        4 * sizeof(byte)
        + sizeof(uint);

    public byte[] AsBytes()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        {
            bw.Write(chunkid);
            bw.Write(chunklength);
            return ms.ToArray();
        }
    }
}
internal record Wolf3dDigitizedAudio : Asset
{

    public int OriginalSampleRate { get; set; } = 7042;

    public short[] ToPcm16(int targetSampleRate)
    {
        if (targetSampleRate < OriginalSampleRate)
            throw new PfWolfAudioException("Target sample rate must be greater than or equal to the original sample rate.");

        int destsamples = (int)((float)Size * (float)targetSampleRate / (float)OriginalSampleRate);

        short[] newsamples = new short[destsamples];
        for (int i = 0; i < destsamples; i++)
        {
            newsamples[i] = GetSample((float)Size * (float)i / (float)destsamples,
                RawData, Size);
        }

        return newsamples;
    }

    public byte[] ToRawWav(int targetSampleRate)
    {
        short[] newsamples = ToPcm16(targetSampleRate);
        int destsamples = newsamples.Length;

        byte[] wavebuffer = new byte[headchunk.size_of + wavechunk.size_of + destsamples * 2];     // dest are 16-bit samples

        headchunk head = new headchunk
        {
            RIFF = [(byte)'R', (byte)'I', (byte)'F', (byte)'F'],
            filelenminus8 = 0,
            WAVE = [(byte)'W', (byte)'A', (byte)'V', (byte)'E'],
            fmt_ = [(byte)'f', (byte)'m', (byte)'t', (byte)' '],
            formatlen = 0x10,
            val0x0001 = 0x0001,
            channels = 1,
            samplerate = (uint)targetSampleRate,
            bytespersec = (uint)(targetSampleRate * 2),
            bytespersample = 2,
            bitspersample = 16
        };

        wavechunk dhead = new wavechunk
        {
            chunkid = [(byte)'d', (byte)'a', (byte)'t', (byte)'a'],
            chunklength = (uint)(destsamples * 2)
        };
        head.filelenminus8 = (uint)(headchunk.size_of + destsamples * 2);  // (sizeof(dhead)-8 = 0)

        var headData = head.AsBytes();
        Buffer.BlockCopy(headData, 0, wavebuffer, 0, headData.Length);
        var dheadData = dhead.AsBytes();
        Buffer.BlockCopy(dheadData, 0, wavebuffer, headData.Length, dheadData.Length);

        Buffer.BlockCopy(
            src: newsamples,
            srcOffset: 0,
            dst: wavebuffer,
            dstOffset: headData.Length + dheadData.Length,
            count: destsamples * 2);

        return wavebuffer;
    }

    public override void Merge(Asset other)
    {
        // For now, do nothing
    }

    private static short GetSample(float csample, byte[] samples, int size)
    {
        int cursample = (int)csample;
        float sf = csample - (float)cursample;

        // At the edges of the buffer there is no neighboring sample to interpolate
        // against. Holding the edge sample's own value (rather than defaulting to 0,
        // i.e. digital silence) keeps the curve flat instead of snapping toward
        // silence, which otherwise produces an audible pop at the start/end of playback.
        float s1 = (float)(samples[cursample] - 128);
        float s0 = cursample - 1 >= 0 ? (float)(samples[cursample - 1] - 128) : s1;
        float s2 = cursample + 1 < size ? (float)(samples[cursample + 1] - 128) : s1;

        float val = s0 * sf * (sf - 1) / 2 - s1 * (sf * sf - 1) + s2 * (sf + 1) * sf / 2;
        int intval = (int)(val * 256);
        if (intval < -32768) intval = -32768;
        else if (intval > 32767) intval = 32767;
        return (short)intval;
    }
}
