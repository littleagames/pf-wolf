namespace PFWolf.Assets;

public record GraphicAsset : Asset
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
    /// where it doesn't; null when every pixel shows. Pictures drawn on the screen (menus, the
    /// level-end screens, help text, the status bar) and fonts leave those pixels out.
    /// </summary>
    public byte[]? OpacityMask { get; init; }
}
