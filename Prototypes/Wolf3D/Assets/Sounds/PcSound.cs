namespace Wolf3D.Assets.Sounds;

internal record PcSound : Asset
{
    public PcSound(byte[] data)
    {
        RawData = data;
        Common = new SoundCommon(data);
        Data = new byte[Common.Length];
        Buffer.BlockCopy(data, 6, Data, 0, (int)Common.Length);
    }

    public SoundCommon Common { get; set; } = null!;
    public byte[] Data { get; set; } = null!;
}

internal record SoundCommon
{
    public uint Length { get; init; }
    public ushort Priority { get; init; }

    public SoundCommon()
    {
    }

    public SoundCommon(byte[] data)
    {
        Length = BitConverter.ToUInt32(data, 0);
        Priority = BitConverter.ToUInt16(data, 4);
    }
}