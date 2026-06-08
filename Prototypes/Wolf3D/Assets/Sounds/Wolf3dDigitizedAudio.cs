using static Wolf3D.Managers.AudioManager;

namespace Wolf3D.Assets.Sounds;

public record Wolf3dDigitizedAudio : Asset
{

    public int OriginalSampleRate { get; set; } = 7042;

    public byte[] ToRawWav(int targetSampleRate)
    {
        if (targetSampleRate < OriginalSampleRate)
            throw new PfWolfAudioException("Target sample rate must be greater than or equal to the original sample rate.");
        int i;

        int destsamples = (int)((float)Size * (float)targetSampleRate
            / (float)OriginalSampleRate);

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

        // alignment is correct, as wavebuffer comes from malloc
        // and sizeof(headchunk) % 4 == 0 and sizeof(wavechunk) % 4 == 0
        short[] newsamples = new short[(wavebuffer.Length + headchunk.size_of
            + wavechunk.size_of) / sizeof(short)];
        float cursample = 0.0F;
        float samplestep = (float)ORIGSAMPLERATE / (float)targetSampleRate;
        for (i = 0; i < destsamples; i++, cursample += samplestep)
        {
            newsamples[i] = GetSample((float)Size * (float)i / (float)destsamples,
                RawData, Size);
        }

        Buffer.BlockCopy(
            src: newsamples,
            srcOffset: 0,
            dst: wavebuffer,
            dstOffset: headData.Length + dheadData.Length,
            count: wavebuffer.Length - (headData.Length + dheadData.Length));

        return wavebuffer;
    }

    private short GetSample(float csample, byte[] samples, int size)
    {
        float s0 = 0, s1 = 0, s2 = 0;
        int cursample = (int)csample;
        float sf = csample - (float)cursample;

        if (cursample - 1 >= 0) s0 = (float)(samples[cursample - 1] - 128);
        s1 = (float)(samples[cursample] - 128);
        if (cursample + 1 < size) s2 = (float)(samples[cursample + 1] - 128);

        float val = s0 * sf * (sf - 1) / 2 - s1 * (sf * sf - 1) + s2 * (sf + 1) * sf / 2;
        int intval = (int)(val * 256);
        if (intval < -32768) intval = -32768;
        else if (intval > 32767) intval = 32767;
        return (short)intval;
    }
}
