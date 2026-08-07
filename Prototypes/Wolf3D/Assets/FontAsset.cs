namespace Wolf3D.Assets;

internal record FontAsset : Asset
{
    public FontAsset(byte[] data)
    {
        RawData = data;


        int dataIndex = 0;
        Height = BitConverter.ToInt16(data, dataIndex);
        dataIndex += sizeof(short);
        for (int i = 0; i < Location.Length; i++)
        {
            Location[i] = BitConverter.ToInt16(data, dataIndex);
            dataIndex += sizeof(short);
        }

        for (int j = 0; j < Width.Length; j++)
        {
            Width[j] = data[dataIndex];
            dataIndex += sizeof(byte);
        }
    }

    public short Height { get; init; }
    public short[] Location { get; init; } = new short[256];
    public byte[] Width { get; init; } = new byte[256];
}