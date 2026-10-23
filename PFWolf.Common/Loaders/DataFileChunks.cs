using System.Security.Cryptography;
using PFWolf.Assets;

namespace PFWolf.Loaders;

/// <summary>
/// The parts of a game's data files that raw-data-map names, each its own list: a chunk's place
/// in its part is its place in that raw-data-map list (graphics are split into fonts, pics, Tile8
/// and externs, AUDIOT's sounds into its PC speaker, AdLib and digitized thirds)
/// </summary>
public enum ChunkSection
{
    Walls,
    Sprites,
    DigitizedSounds,
    Fonts,
    Pics,
    Tile8,
    Externs,
    PcSounds,
    AdLibSounds,
    DigiSounds,
    Music,
    Maps,
}

/// <summary>A chunk with data: its place in its part, and a hash of what it holds</summary>
public sealed record DataChunk(int Index, string Hash);

/// <summary>
/// The data files of one release, or of a raw data mod (null for a file it doesn't have)
/// </summary>
public sealed record DataFileSet(
    string? Vswap = null,
    string? VgaDict = null, string? VgaHead = null, string? VgaGraph = null,
    string? AudioHed = null, string? AudioT = null,
    string? MapHead = null, string? GameMaps = null)
{
    public IEnumerable<string> Files =>
        new[] { Vswap, VgaDict, VgaHead, VgaGraph, AudioHed, AudioT, MapHead, GameMaps }.OfType<string>();
}

/// <summary>
/// Every chunk with data in a set of data files, by part, hashed so two files' chunks can be told
/// the same or not (a raw data mod's against the game's). Read with the data file loaders given no
/// names, so each chunk comes back under the made-up name that says where it is (WALL0012, PIC0003,
/// ALSND0040); empty chunks have none.
/// </summary>
public sealed class DataFileChunks
{
    private readonly Dictionary<ChunkSection, List<DataChunk>> _sections = [];

    /// <summary>A part's chunks with data, in order</summary>
    public IReadOnlyList<DataChunk> this[ChunkSection section] => _sections.GetValueOrDefault(section) ?? [];

    /// <summary>How many places a part has: one past its last chunk with data</summary>
    public int Count(ChunkSection section) => this[section] is { Count: > 0 } chunks ? chunks[^1].Index + 1 : 0;

    /// <summary>The parts the files have anything in</summary>
    public IEnumerable<ChunkSection> Sections => _sections.Where(section => section.Value.Count > 0).Select(section => section.Key);

    /// <summary>Whether any chunk anywhere has this hash</summary>
    public bool Contains(string hash) => _sections.Values.Any(chunks => chunks.Any(chunk => chunk.Hash == hash));

    /// <summary>
    /// Reads the files the set has. A file that can't be read throws (InvalidDataException,
    /// PfWolfFileLoaderException...): a raw data mod's file has to be read whole.
    /// </summary>
    /// <param name="fontCount">VGAGRAPH's number of font chunks (the release's file-pack font-count: 2, Blake Stone 5)</param>
    public static DataFileChunks Read(DataFileSet files, int fontCount)
    {
        var chunks = new DataFileChunks();
        if (files.Vswap != null)
        {
            var assets = new Wolf3dVswapFileLoader(files.Vswap).GetAssets([], [], []);
            chunks.Add(assets, "WALL", ChunkSection.Walls);
            chunks.Add(assets, "SPR", ChunkSection.Sprites);
            chunks.Add(assets, "DIGI", ChunkSection.DigitizedSounds);
        }

        if (files.VgaHead != null && files.VgaGraph != null && files.VgaDict != null)
        {
            var assets = new Wolf3dVgaFileLoader(files.VgaHead, files.VgaGraph, files.VgaDict, fontCount).GetAssets([]);
            chunks.Add(assets, "FONT", ChunkSection.Fonts);
            chunks.Add(assets, "PIC", ChunkSection.Pics);
            chunks.Add(assets, "EXTERN", ChunkSection.Externs);
            if (assets.GetValueOrDefault("tile8") is { } tile8)
                chunks.Section(ChunkSection.Tile8).Add(new DataChunk(0, HashOf(tile8)));
        }

        if (files.AudioHed != null && files.AudioT != null)
        {
            var assets = new Wolf3dAudioFileLoader(files.AudioT, files.AudioHed).GetAssets([], []);
            chunks.Add(assets, "PCSND", ChunkSection.PcSounds);
            chunks.Add(assets, "ALSND", ChunkSection.AdLibSounds);
            chunks.Add(assets, "DSSND", ChunkSection.DigiSounds);
            chunks.Add(assets, "MUSIC", ChunkSection.Music);
        }

        if (files.MapHead != null && files.GameMaps != null)
            chunks.Add(Wolf3dMapFileLoader.FromFiles(files.MapHead, files.GameMaps).GetAssets([]), "MAP", ChunkSection.Maps, firstNumber: 1);

        foreach (var section in chunks._sections.Values)
            section.Sort((a, b) => a.Index.CompareTo(b.Index));
        return chunks;
    }

    /// <summary>A hash of what an asset holds: its bytes, a picture's size, or a level's planes</summary>
    public static string HashOf(Asset asset)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        hash.AppendData(asset.RawData);
        switch (asset)
        {
            case GraphicAsset graphic:
                hash.AppendData(BitConverter.GetBytes(graphic.Width));
                hash.AppendData(BitConverter.GetBytes(graphic.Height));
                break;
            case SpriteAsset sprite:
                hash.AppendData(BitConverter.GetBytes(sprite.Width));
                hash.AppendData(BitConverter.GetBytes(sprite.Height));
                break;
            case MapAsset map:
                hash.AppendData(BitConverter.GetBytes(map.Width));
                hash.AppendData(BitConverter.GetBytes(map.Height));
                foreach (var plane in map.MapData)
                {
                    var bytes = new byte[plane.Length * sizeof(ushort)];
                    Buffer.BlockCopy(plane, 0, bytes, 0, bytes.Length);
                    hash.AppendData(bytes);
                }
                break;
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private List<DataChunk> Section(ChunkSection section)
    {
        if (!_sections.TryGetValue(section, out var chunks))
            _sections[section] = chunks = [];
        return chunks;
    }

    /// <summary>
    /// The made-up name the data file loaders give a chunk raw-data-map has no name for (and the
    /// one a generated raw-data-map gives a new chunk, so the two agree)
    /// </summary>
    public static string PlaceholderName(ChunkSection section, int index) => section switch
    {
        ChunkSection.Walls => DataMapNames.Generated("WALL", index),
        ChunkSection.Sprites => DataMapNames.Generated("SPR", index),
        ChunkSection.DigitizedSounds => DataMapNames.Generated("DIGI", index),
        ChunkSection.Fonts => DataMapNames.Generated("FONT", index),
        ChunkSection.Pics => DataMapNames.Generated("PIC", index),
        ChunkSection.Tile8 => "Tile8",
        ChunkSection.Externs => DataMapNames.Generated("EXTERN", index),
        ChunkSection.PcSounds => DataMapNames.Generated("PCSND", index),
        ChunkSection.AdLibSounds => DataMapNames.Generated("ALSND", index),
        ChunkSection.DigiSounds => DataMapNames.Generated("DSSND", index),
        ChunkSection.Music => DataMapNames.Generated("MUSIC", index),
        ChunkSection.Maps => DataMapNames.Generated("MAP", index + 1),
        _ => throw new ArgumentOutOfRangeException(nameof(section)),
    };

    // The assets under PREFIX#### (the loaders' made-up names), by that number less the first's
    private void Add(Dictionary<string, Asset> assets, string prefix, ChunkSection section, int firstNumber = 0)
    {
        foreach (var (name, asset) in assets)
        {
            if (name.Length == prefix.Length + 4 && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(name.AsSpan(prefix.Length), out var number))
                Section(section).Add(new DataChunk(number - firstNumber, HashOf(asset)));
        }
    }
}
