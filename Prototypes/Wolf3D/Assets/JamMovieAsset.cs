namespace Wolf3D.Assets;

/// <summary>
/// A JAM Productions movie (Blake Stone's IANIM, EANIM, SANIM and GANIM files): a run of
/// chunks, each a two-letter code, a block number and its data. GR chunks are frames, drawn as
/// runs of pixels at offsets into the 320x200 screen; the others fade, pause and play sounds.
/// </summary>
internal record JamMovieAsset : Asset
{
    public const int ScreenWidth = 320, ScreenHeight = 200;

    internal readonly record struct Chunk(string Code, byte[] Data);

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

    public override void Merge(Asset other)
    {
    }
}

/// <summary>
/// How a game pack plays its movies (gamepacks/{pack}/movies.yaml), by the names gamepack-info's
/// JamMovieFileLoader gives them
/// </summary>
internal record MoviesAsset : Asset
{
    public Dictionary<string, MovieInfo> Movies { get; set; } = [];

    public override void Merge(Asset other)
    {
        if (other is MoviesAsset movies)
        {
            foreach (var (name, movie) in movies.Movies)
                Movies[name] = movie;
        }
    }
}

internal record MovieInfo
{
    /// <summary>Tics (70 a second) each frame stays up, at least</summary>
    public int FrameTics { get; set; } = 3;

    /// <summary>The palette it's shown in; the game palette when unset</summary>
    public string? Palette { get; set; }

    /// <summary>The sound each of the movie's sound numbers plays</summary>
    public Dictionary<int, string> Sounds { get; set; } = [];
}
