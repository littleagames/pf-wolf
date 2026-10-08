namespace PFWolf.Assets.Sounds;

/// <summary>
/// A sound effect from a WAV, OGG or MP3 file in a pk3's sounds/ folder, decoded to 16-bit mono.
/// It plays as the digitized sound of its name, in place of a VSWAP/AUDIOT one of that name.
/// </summary>
public record SoundFileAsset : Asset
{
    public short[] Samples { get; }
    public int SampleRate { get; }

    public SoundFileAsset(byte[] data)
    {
        RawData = data;
        (Samples, SampleRate) = AudioFileDecoder.DecodeMono16(data);
    }

    public override void Merge(Asset other)
    {
        // For now, do nothing
    }
}
