namespace Wolf3D.Assets;

internal record RawDataMapAsset : Asset
{
    public List<string> Walls { get; set; } = [];
    public List<string> Sprites { get; set; } = [];
    public List<string> DigitizedAudio { get; set; } = [];
    public List<string> Audio { get; set; } = [];
    public List<string> Music { get; set; } = [];
    public List<string> Graphics { get; set; } = [];
    public List<string> Maps { get; set; } = [];
    public override void Merge(Asset other)
    {
        if (other is not RawDataMapAsset otherMap)
            throw new ArgumentException($"Cannot merge asset of type {other.GetType().Name} into {nameof(RawDataMapAsset)}");
        Merge(otherMap);
    }

    public void Merge(RawDataMapAsset other)
    {
        if (other.Walls.Count > 0)
            Walls = other.Walls;
        if (other.Sprites.Count > 0)
            Sprites = other.Sprites;
        if (other.DigitizedAudio.Count > 0)
            DigitizedAudio = other.DigitizedAudio;
        if (other.Audio.Count > 0)
            Audio = other.Audio;
        if (other.Music.Count > 0)
            Music = other.Music;
        if (other.Graphics.Count > 0)
            Graphics = other.Graphics;
        if (other.Maps.Count > 0)
            Maps = other.Maps;
    }
}
