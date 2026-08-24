namespace Wolf3D.Assets;

internal record GraphicAsset : Asset
{
    public GraphicAsset(byte[] data, short width, short height)
    {
        RawData = data;
        Width = width;
        Height = height;
    }

    public short Width { get; init; }
    public short Height { get; init; }

    public override void Merge(Asset other)
    {
        // For now, do nothing
    }
}
