namespace PFWolf.Assets;

public record FontAsset : Asset
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

    /// <summary>
    /// A font from a file in Wolf3D's font format (a 16-bit height, 256 16-bit glyph offsets
    /// and 256 byte widths, then each glyph's rows of one byte per pixel, non-zero where it draws).
    /// Throws if the file is too short for its glyphs, which would otherwise be read past its end.
    /// </summary>
    public static FontAsset FromFile(byte[] data)
    {
        const int headerSize = sizeof(short) + 256 * sizeof(short) + 256;
        if (data.Length < headerSize)
            throw new InvalidDataException($"a font needs at least {headerSize} bytes, this has {data.Length}");

        var font = new FontAsset(data);
        if (font.Height <= 0)
            throw new InvalidDataException($"the font's height is {font.Height}");

        for (int ch = 0; ch < 256; ch++)
        {
            if (font.Width[ch] > 0 && (font.Location[ch] < headerSize || font.Location[ch] + font.Width[ch] * font.Height > data.Length))
                throw new InvalidDataException($"character {ch}'s glyph is outside the file");
        }

        return font;
    }

    public short Height { get; init; }
    public short[] Location { get; init; } = new short[256];
    public byte[] Width { get; init; } = new byte[256];

    public override void Merge(Asset other)
    {
        // For now, do nothing
    }
}