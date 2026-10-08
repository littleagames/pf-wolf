namespace PFWolf.Assets.Sounds;

/// <summary>
/// A song from an OGG, MP3 or WAV file in a pk3's music/ folder, played in place of the IMF song
/// of its name. It's kept compressed and decoded as it streams.
/// </summary>
public record MusicFileAsset : Asset
{
    public MusicFileAsset(byte[] data)
    {
        RawData = data;
        // Opened once here so a file that can't be played is found at startup, not mid-game
        using var decoder = AudioFileDecoder.Open(data);
    }

    public AudioFileDecoder OpenDecoder() => AudioFileDecoder.Open(RawData);

    public override void Merge(Asset other)
    {
        // For now, do nothing
    }
}
