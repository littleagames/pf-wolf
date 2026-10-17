namespace PFWolf.Assets.Sounds;

public struct HeadChunk
{
    public byte[] Riff;
    public uint FileLenMinus8;
    public byte[] Wave;
    public byte[] Fmt;
    public uint FormatLen;
    public ushort Val0x0001;
    public ushort Channels;
    public uint SampleRate;
    public uint BytesPerSec;
    public ushort BytesPerSample;
    public ushort BitsPerSample;
    public HeadChunk()
    {
        Riff = new byte[4];
        Wave = new byte[4];
        Fmt = new byte[4];
    }

    public static int SizeOf =>
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
            bw.Write(Riff);
            bw.Write(FileLenMinus8);
            bw.Write(Wave);
            bw.Write(Fmt);
            bw.Write(FormatLen);
            bw.Write(Val0x0001);
            bw.Write(Channels);
            bw.Write(SampleRate);
            bw.Write(BytesPerSec);
            bw.Write(BytesPerSample);
            bw.Write(BitsPerSample);
            return ms.ToArray();
        }
    }
}

public class WaveChunk
{
    public byte[] ChunkId;
    public uint ChunkLength;
    public WaveChunk()
    {
        ChunkId = new byte[4];
    }

    public static int SizeOf =>
        4 * sizeof(byte)
        + sizeof(uint);

    public byte[] AsBytes()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        {
            bw.Write(ChunkId);
            bw.Write(ChunkLength);
            return ms.ToArray();
        }
    }
}
public record Wolf3dDigitizedAudio : Asset
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

        byte[] wavebuffer = new byte[HeadChunk.SizeOf + WaveChunk.SizeOf + destsamples * 2];     // dest are 16-bit samples

        HeadChunk head = new HeadChunk
        {
            Riff = [(byte)'R', (byte)'I', (byte)'F', (byte)'F'],
            FileLenMinus8 = 0,
            Wave = [(byte)'W', (byte)'A', (byte)'V', (byte)'E'],
            Fmt = [(byte)'f', (byte)'m', (byte)'t', (byte)' '],
            FormatLen = 0x10,
            Val0x0001 = 0x0001,
            Channels = 1,
            SampleRate = (uint)targetSampleRate,
            BytesPerSec = (uint)(targetSampleRate * 2),
            BytesPerSample = 2,
            BitsPerSample = 16
        };

        WaveChunk dhead = new WaveChunk
        {
            ChunkId = [(byte)'d', (byte)'a', (byte)'t', (byte)'a'],
            ChunkLength = (uint)(destsamples * 2)
        };
        head.FileLenMinus8 = (uint)(HeadChunk.SizeOf + destsamples * 2);  // (sizeof(dhead)-8 = 0)

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
