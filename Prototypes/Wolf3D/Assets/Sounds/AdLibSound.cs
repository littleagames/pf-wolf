namespace Wolf3D.Assets.Sounds;

internal record AdLibSound : Asset
{
    public SoundCommon Common => new SoundCommon(RawData);
}