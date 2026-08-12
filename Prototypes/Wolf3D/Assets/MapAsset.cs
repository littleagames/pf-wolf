namespace Wolf3D.Assets;

internal record MapAsset : Asset
{
    //public MapAsset(byte[] data)
    //{
    //    RawData = data;
    //}

    public ushort Height { get; set; }
    public ushort Width { get; set; }
    public string Name { get; internal set; }
    public ushort[][] MapData { get; internal set; }

    // TODO: Move the decompression to a "To..." method here.
    // This will allow for future support for other map formats, like UWMF (Universal Wolf Map Format) or others.
}
