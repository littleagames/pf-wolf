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

    /// <summary>
    /// A character's glyph: <see cref="Height"/> rows of its width, a byte a pixel, non-zero where
    /// it draws (empty for a character the font hasn't got)
    /// </summary>
    public byte[] Glyph(int ch)
    {
        if (ch is < 0 or > 255 || Width[ch] == 0 || Height <= 0)
            return [];
        int size = Width[ch] * Height, at = Location[ch];
        var pixels = new byte[size];
        if (at >= 0 && at < RawData.Length)
            RawData.AsSpan(at, Math.Min(size, RawData.Length - at)).CopyTo(pixels);
        return pixels;
    }

    /// <summary>
    /// How far into a font file the glyphs can start: the offsets are 16-bit, so a font's glyphs
    /// all start within the first 32767 bytes
    /// </summary>
    public const int MaxGlyphOffset = short.MaxValue;

    /// <summary>
    /// A Wolf3D font file (what <see cref="FromFile"/> reads, a mod's fonts/NAME.fnt): the height,
    /// then each of the 256 characters' glyph, in code order. Throws InvalidDataException when the
    /// glyphs don't fit the 16-bit offsets (by how many bytes).
    /// </summary>
    /// <param name="glyphs">Each character's width (0 for none) and its rows of a byte a pixel, non-zero where it draws</param>
    public static byte[] Write(int height, IReadOnlyList<(int Width, byte[] Pixels)> glyphs)
    {
        if (height is < 1 or > short.MaxValue)
            throw new InvalidDataException($"a font's height is 1 or more, not {height}");
        if (glyphs.Count != 256)
            throw new ArgumentException("a font has 256 characters", nameof(glyphs));

        var data = new List<byte>(new byte[HeaderSize]);
        BitConverter.TryWriteBytes(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(data)[..2], (short)height);
        for (int ch = 0; ch < 256; ch++)
        {
            var (width, pixels) = glyphs[ch];
            if (width is < 0 or > 255)
                throw new InvalidDataException($"character {ch} is {width} wide; a glyph is 0 to 255");
            int at = data.Count;
            if (width > 0 && at > MaxGlyphOffset)
                throw new InvalidDataException($"the glyphs are {at - MaxGlyphOffset} bytes too many for the font file's 16-bit offsets: make the font smaller or take characters out");
            BitConverter.TryWriteBytes(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(data).Slice(2 + ch * 2, 2), (short)Math.Min(at, MaxGlyphOffset));
            data[2 + 512 + ch] = (byte)width;
            for (int i = 0; i < width * height; i++)
                data.Add(i < pixels.Length ? pixels[i] : (byte)0);
        }
        return [.. data];
    }
}