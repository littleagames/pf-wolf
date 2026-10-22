namespace PFWolf.Assets;

/// <summary>
/// A JAM Productions movie (Blake Stone's IANIM, EANIM, SANIM and GANIM files): a run of
/// chunks, each a two-letter code, a block number and its data. GR chunks are frames, drawn as
/// runs of pixels at offsets into the 320x200 screen; the others fade, pause and play sounds.
/// </summary>
public record JamMovieAsset : Asset
{
    public const int ScreenWidth = 320, ScreenHeight = 200;

    public readonly record struct Chunk(string Code, byte[] Data);

    public List<Chunk> Chunks { get; } = [];

    public JamMovieAsset(byte[] data)
    {
        RawData = data;
        using var reader = new BinaryReader(new MemoryStream(data));
        while (reader.BaseStream.Length - reader.BaseStream.Position >= 10)
        {
            ushort code = reader.ReadUInt16();
            reader.ReadInt32();     // block number
            int size = reader.ReadInt32();
            if (size < 0 || size > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException($"a chunk runs past the end of the movie ({size} bytes)");

            var name = $"{(char)(code & 0xFF)}{(char)(code >> 8)}";
            Chunks.Add(new Chunk(name, reader.ReadBytes(size)));
            if (name == "XX")
                break;
        }
    }

    /// <summary>
    /// How long each frame (GR chunk) stays up, in tics (70 a second), as the game plays it:
    /// <paramref name="frameTics"/> (movies.yaml frame-tics), and a pause (PA chunk) after a
    /// frame added to it. Without drawing any.
    /// </summary>
    public List<int> FrameTics(int frameTics)
    {
        var tics = new List<int>();
        foreach (var chunk in Chunks)
        {
            if (chunk.Code == "GR")
                tics.Add(Math.Max(frameTics, 1));
            else if (chunk.Code == "PA" && chunk.Data.Length >= 2 && tics.Count > 0)
                tics[^1] += BitConverter.ToUInt16(chunk.Data, 0);
            else if (chunk.Code == "XX")
                break;
        }
        return tics;
    }

    /// <summary>Every frame as the screen shows it, 320x200 palette indices row by row, first to last</summary>
    public List<byte[]> DrawFrames()
    {
        var frames = new List<byte[]>();
        var screen = new byte[ScreenWidth * ScreenHeight];
        foreach (var chunk in Chunks)
        {
            if (chunk.Code == "XX")
                break;
            if (chunk.Code != "GR")
                continue;
            DrawFrame(screen, chunk.Data, fill: frames.Count == 0);
            frames.Add((byte[])screen.Clone());
        }
        return frames;
    }

    /// <summary>
    /// Draws a frame (a GR chunk) into the screen: runs of pixels, each its byte offset into the
    /// screen and length, until one marked 0. The movie's first frame starts with the color to
    /// fill the screen with first (<paramref name="fill"/>).
    /// </summary>
    public static void DrawFrame(byte[] screen, byte[] data, bool fill)
    {
        int pos = 0;
        if (fill && data.Length > 0)
            Array.Fill(screen, data[pos++]);

        while (pos + 6 <= data.Length)
        {
            int opt = BitConverter.ToUInt16(data, pos);
            if (opt == 0)
                break;
            int offset = BitConverter.ToUInt16(data, pos + 2);
            int length = BitConverter.ToUInt16(data, pos + 4);
            pos += 6;

            int copied = Math.Min(length, Math.Min(data.Length - pos, screen.Length - offset));
            if (copied > 0)
                Array.Copy(data, pos, screen, offset, copied);
            pos += length;
        }
    }
}

/// <summary>
/// How a game pack plays its movies (gamepacks/{pack}/movies.yaml), by the names gamepack-info's
/// JamMovieFileLoader gives them
/// </summary>
public record MoviesAsset : Asset
{
    public Dictionary<string, MovieInfo> Movies { get; set; } = [];
}

public record MovieInfo
{
    /// <summary>Tics (70 a second) each frame stays up, at least</summary>
    public int FrameTics { get; set; } = 3;

    /// <summary>The palette it's shown in; the game palette when unset</summary>
    public string? Palette { get; set; }

    /// <summary>The sound each of the movie's sound numbers plays</summary>
    public Dictionary<int, string> Sounds { get; set; } = [];
}
