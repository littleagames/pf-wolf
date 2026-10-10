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

    private const int HeaderSize = sizeof(short) + 256 * sizeof(short) + 256;

    /// <summary>The start of a WDC font file (.wsf)</summary>
    private static ReadOnlySpan<byte> WsfMagic => "WSF1.0"u8;

    /// <summary>
    /// A font from a file in Wolf3D's font format (a 16-bit height, 256 16-bit glyph offsets
    /// and 256 byte widths, then each glyph's rows of one byte per pixel, non-zero where it draws),
    /// or in WDC's (.wsf: see <see cref="FromWsf"/>). Throws if the file is too short for its
    /// glyphs, which would otherwise be read past its end.
    /// </summary>
    public static FontAsset FromFile(byte[] data)
    {
        if (data.AsSpan().StartsWith(WsfMagic))
            data = FromWsf(data);

        const int headerSize = HeaderSize;
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

    /// <summary>
    /// A WDC font file (.wsf) in Wolf3D's font format. A .wsf is "WSF1.0", an 8-bit height and
    /// 256 byte widths, then the glyphs one after another in character order, each in the same
    /// rows of one byte per pixel as Wolf3D's; it has no offsets, so they're worked out here.
    /// </summary>
    public static byte[] FromWsf(byte[] wsf)
    {
        int widthsAt = WsfMagic.Length + 1, glyphsAt = widthsAt + 256;
        if (wsf.Length < glyphsAt)
            throw new InvalidDataException($"a .wsf font needs at least {glyphsAt} bytes, this has {wsf.Length}");

        int height = wsf[WsfMagic.Length];
        var glyphs = wsf.AsSpan(glyphsAt);
        var data = new byte[HeaderSize + glyphs.Length];
        BitConverter.TryWriteBytes(data.AsSpan(0, 2), (short)height);
        int offset = HeaderSize;
        for (int ch = 0; ch < 256; ch++)
        {
            int width = wsf[widthsAt + ch];
            if (width > 0 && offset > short.MaxValue)
                throw new InvalidDataException("the .wsf font is too big for Wolf3D's 16-bit glyph offsets");
            BitConverter.TryWriteBytes(data.AsSpan(2 + ch * 2, 2), (short)Math.Min(offset, short.MaxValue));
            data[2 + 512 + ch] = (byte)width;
            offset += width * height;
        }

        glyphs.CopyTo(data.AsSpan(HeaderSize));
        return data;
    }

    public short Height { get; init; }
    public short[] Location { get; init; } = new short[256];
    public byte[] Width { get; init; } = new byte[256];
}