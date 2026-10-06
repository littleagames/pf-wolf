namespace PFWolf.Assets;

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

    /// <summary>
    /// For a picture with see-through pixels (a PNG with alpha), 1 where a pixel shows and 0
    /// where it doesn't; null when every pixel shows. Only fonts use it so far.
    /// </summary>
    public byte[]? OpacityMask { get; init; }

    public override void Merge(Asset other)
    {
        // For now, do nothing
    }
}
