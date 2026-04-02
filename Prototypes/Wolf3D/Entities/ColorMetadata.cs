namespace Wolf3D.Entities;

internal class ColorMetadata
{
    /// <summary>
    /// Mapping a color name to a single 256 color palette byte index value
    /// </summary>
    public Dictionary<string, byte> Colors256 { get; set; } = [];

    public Dictionary<string, Color> Colors { get; set; } = [];
}
