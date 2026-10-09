namespace PFWolf.Assets;

public record MapAsset : Asset
{
    //public MapAsset(byte[] data)
    //{
    //    RawData = data;
    //}

    public ushort Height { get; set; }
    public ushort Width { get; set; }
    public string Name { get; set; } = "";

    /// <summary>The planes, each Width x Height tiles, row by row</summary>
    public ushort[][] MapData { get; set; } = [];

    // TODO: Move the decompression to a "To..." method here.
    // This will allow for future support for other map formats, like UWMF (Universal Wolf Map Format) or others.

    /// <summary>A copy whose planes can be changed without touching this one</summary>
    public MapAsset DeepCopy() => this with { MapData = MapData.Select(plane => (ushort[])plane.Clone()).ToArray() };
}
