using System.IO.Compression;
using PFWolf.Assets;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>
/// The raw data mod generator's naming (docs/dev/raw-data-mods-plan.md phase 2): made-up parts
/// first, then the stock WL6 files against themselves and a VSWAP with walls put in before the
/// doors, as WDC adds them.
/// </summary>
public class RawDataModGeneratorTests
{
    private string _workFolder = "";
    private string _startFolder = "";

    [SetUp]
    public void SetUp()
    {
        _startFolder = Directory.GetCurrentDirectory();
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-rawgen-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workFolder);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.SetCurrentDirectory(_startFolder);
        try
        {
            Directory.Delete(_workFolder, recursive: true);
        }
        catch (IOException)
        {
            // Only a temp folder
        }
    }

    private static DataChunk Chunk(int index, string hash) => new(index, hash);

    [Test]
    public void Unchanged_Chunks_Keep_Their_Names_And_Changed_Ones_Their_Places_Names()
    {
        // Arrange
        string[] names = ["A", "B", "C"];
        DataChunk[] stock = [Chunk(0, "a"), Chunk(1, "b"), Chunk(2, "c")];
        DataChunk[] mod = [Chunk(0, "a"), Chunk(1, "x"), Chunk(2, "c")];

        // Act
        var named = RawDataModGenerator.MatchSection(ChunkSection.Walls, names, stock, mod, 3);

        // Assert
        Assert.That(named.Select(chunk => chunk.Name), Is.EqualTo(new[] { "A", "B", "C" }));
        Assert.That(named.Select(chunk => chunk.Match), Is.EqualTo(new[] { ChunkMatch.Same, ChunkMatch.Changed, ChunkMatch.Same }));
    }

    [Test]
    public void A_Chunk_Put_In_Moves_The_Ones_After_It_Which_Keep_Their_Names()
    {
        // Arrange: a pic put in at place 1
        string[] names = ["A", "B", "C"];
        DataChunk[] stock = [Chunk(0, "a"), Chunk(1, "b"), Chunk(2, "c")];
        DataChunk[] mod = [Chunk(0, "a"), Chunk(1, "new"), Chunk(2, "b"), Chunk(3, "c")];

        // Act
        var named = RawDataModGenerator.MatchSection(ChunkSection.Pics, names, stock, mod, 4);

        // Assert
        Assert.That(named.Select(chunk => chunk.Name), Is.EqualTo(new[] { "A", "PIC0001", "B", "C" }));
        Assert.That(named.Select(chunk => chunk.Match), Is.EqualTo(new[] { ChunkMatch.Same, ChunkMatch.New, ChunkMatch.Moved, ChunkMatch.Moved }));
    }

    [Test]
    public void The_Door_Pages_Stay_Last_When_Walls_Are_Added_Even_Changed()
    {
        // Arrange: walls W0 W1 then doors D0 D1; the mod adds a wall and repaints D1
        string[] names = ["W0", "W1", "D0", "D1"];
        DataChunk[] stock = [Chunk(0, "w0"), Chunk(1, "w1"), Chunk(2, "d0"), Chunk(3, "d1")];
        DataChunk[] mod = [Chunk(0, "w0"), Chunk(1, "w1"), Chunk(2, "added"), Chunk(3, "d0"), Chunk(4, "d1 repainted")];

        // Act
        var named = RawDataModGenerator.MatchSection(ChunkSection.Walls, names, stock, mod, 5, tailNames: 2);

        // Assert
        Assert.That(named.Select(chunk => chunk.Name), Is.EqualTo(new[] { "W0", "W1", "WALL0002", "D0", "D1" }));
        Assert.That(named[2].Match, Is.EqualTo(ChunkMatch.New));
        Assert.That(named[4].Match, Is.EqualTo(ChunkMatch.Changed));
    }

    [Test]
    public void Without_The_Base_Files_Names_Go_By_Place_But_Doors_Still_Stay_Last()
    {
        // Arrange
        string[] names = ["W0", "W1", "D0", "D1"];
        DataChunk[] mod = [Chunk(0, "?"), Chunk(1, "?"), Chunk(2, "?"), Chunk(3, "?"), Chunk(4, "?")];

        // Act
        var named = RawDataModGenerator.MatchSection(ChunkSection.Walls, names, baseChunks: null, mod, 5, tailNames: 2);

        // Assert
        Assert.That(named.Select(chunk => chunk.Name), Is.EqualTo(new[] { "W0", "W1", "WALL0002", "D0", "D1" }));
        Assert.That(named.Where(chunk => chunk.Name != "WALL0002").Select(chunk => chunk.Match), Has.All.EqualTo(ChunkMatch.ByPosition));
    }

    [Test]
    public void Empty_Places_Keep_Names_And_Levels_Are_Numbered_From_One()
    {
        // Arrange: level slots 0 and 2 used, 3 past the names
        string[] names = ["MAP01", "MAP02"];
        DataChunk[] mod = [Chunk(0, "m"), Chunk(3, "n")];

        // Act
        var named = RawDataModGenerator.MatchSection(ChunkSection.Maps, names, null, mod, 4);

        // Assert
        Assert.That(named.Select(chunk => chunk.Name), Is.EqualTo(new[] { "MAP01", "MAP02", "MAP0003", "MAP0004" }));
        Assert.That(named.Select(chunk => chunk.Match), Is.EqualTo(new[] { ChunkMatch.ByPosition, ChunkMatch.Empty, ChunkMatch.Empty, ChunkMatch.New }));
    }

    [Test]
    public void Raw_Data_Map_Lists_Split_Into_Parts_Around_Tile8_And_By_Sound_Kind()
    {
        // Arrange
        var map = new RawDataMapAsset
        {
            Graphics = ["STRUCTPIC", "SmallFont", "LargeFont", "P0", "P1", "Tile8", "E0"],
            Audio = ["PCA", "PCB", "ALA", "ALB", "DSA"],
        };

        // Act
        var parts = RawDataModGenerator.SplitNames(map, fontCount: 2).ToDictionary(part => part.Section, part => part.Names);

        // Assert
        Assert.That(parts[ChunkSection.Fonts], Is.EqualTo(new[] { "SmallFont", "LargeFont" }));
        Assert.That(parts[ChunkSection.Pics], Is.EqualTo(new[] { "P0", "P1" }));
        Assert.That(parts[ChunkSection.Tile8], Is.EqualTo(new[] { "Tile8" }));
        Assert.That(parts[ChunkSection.Externs], Is.EqualTo(new[] { "E0" }));
        Assert.That(parts[ChunkSection.AdLibSounds], Is.EqualTo(new[] { "ALA", "ALB" }));
        Assert.That(parts[ChunkSection.DigiSounds], Is.EqualTo(new[] { "DSA" }));
    }

    [Test]
    public void The_Door_Pages_Are_The_Last_Wall_Names_Doors_Use()
    {
        // Arrange
        var defs = new MapObjectTranslationAsset();
        defs.Doors[90] = new MapTextureTranslation { North = "SLOT", East = "DOOR" };

        // Act / Assert
        Assert.That(RawDataModGenerator.DoorPageCount(["W0", "DOOR", "W1", "SLOT", "DOOR"], defs), Is.EqualTo(2));
        Assert.That(RawDataModGenerator.DoorPageCount(["W0"], defs), Is.Zero);
    }

    [Test]
    [Category("GameData")]
    public void The_Stock_Files_Match_Themselves_With_Nothing_To_Name()
    {
        // Arrange
        var data = GameFolderWithWl6();

        // Act
        var match = RawDataModGenerator.Match(data, "wolf3d");

        // Assert
        Assert.That(match.BaseFilesFound, Is.True);
        Assert.That(match.Sections.Where(section => section.Differs).Select(section => section.Section), Is.Empty);
        Assert.That(match.Sections.SelectMany(section => section.Chunks).Select(chunk => chunk.Match),
            Has.None.EqualTo(ChunkMatch.Changed).And.None.EqualTo(ChunkMatch.New).And.None.EqualTo(ChunkMatch.Moved));
        Assert.That(match[ChunkSection.Walls]!.Chunks[^1].Name, Is.EqualTo("DOOR3_2").Or.EqualTo(match[ChunkSection.Walls]!.BaseNames[^1]));
    }

    [Test]
    [Category("GameData")]
    public void Walls_Added_Before_The_Doors_Get_Placeholders_And_The_Doors_Keep_Their_Names()
    {
        // Arrange: two new wall pages before the 8 door pages, the first wall repainted
        var data = GameFolderWithWl6();
        var mod = Directory.CreateDirectory(Path.Combine(_workFolder, "mod")).FullName;
        var pages = TestVswap.Read(Path.Combine(data, "VSWAP.WL6"));
        var newWall = Enumerable.Repeat((byte)7, 4096).ToArray();
        pages.Pages[0] = pages.Pages[0].Select(b => (byte)(b ^ 0xFF)).ToArray();
        pages.InsertWalls(pages.SpriteStart - 8, newWall, newWall.Select(b => (byte)8).ToArray());
        pages.Write(Path.Combine(mod, "VSWAP.WL6"));

        // Act
        var match = RawDataModGenerator.Match(mod, "wolf3d");
        var walls = match[ChunkSection.Walls]!;

        // Assert
        Assert.That(match.Sections.Select(section => section.Section), Is.EqualTo(new[] { ChunkSection.Walls, ChunkSection.Sprites, ChunkSection.DigitizedSounds }),
            "only VSWAP is the mod's");
        Assert.That(walls.Chunks, Has.Count.EqualTo(walls.BaseNames.Count + 2));
        Assert.That(walls.Chunks[0].Match, Is.EqualTo(ChunkMatch.Changed));
        Assert.That(walls.Chunks[0].Name, Is.EqualTo(walls.BaseNames[0]));
        var added = walls.BaseNames.Count - 8;
        Assert.That(walls.Chunks.Skip(added).Take(2).Select(chunk => chunk.Name), Is.EqualTo(new[] { $"WALL{added:0000}", $"WALL{added + 1:0000}" }));
        Assert.That(walls.Chunks.TakeLast(8).Select(chunk => chunk.Name), Is.EqualTo(walls.BaseNames.TakeLast(8)));
        Assert.That(walls.Chunks.TakeLast(8).Select(chunk => chunk.Match), Has.All.EqualTo(ChunkMatch.Moved));
        Assert.That(walls.Differs, Is.True);
        Assert.That(match[ChunkSection.Sprites]!.Differs, Is.False);
    }

    [Test]
    [Category("GameData")]
    public void The_Base_Is_Detected_From_The_Files()
    {
        // Arrange: the WL6 files renamed to another extension
        var data = GameFolderWithWl6();
        var mod = Directory.CreateDirectory(Path.Combine(_workFolder, "mod")).FullName;
        foreach (var file in Wl6Files)
            File.Copy(Path.Combine(data, file), Path.Combine(mod, Path.ChangeExtension(file, ".MOD")));

        // Act
        var candidates = PFWolf.Managers.RawDataModGenerator.DetectBase(mod);

        // Assert
        Assert.That(candidates, Is.Not.Empty);
        Assert.That(candidates[0].ReleaseId, Is.EqualTo("wolf3d"));
        Assert.That(candidates[0].Share, Is.EqualTo(1.0));
        Assert.That(candidates[0].ModFiles.Vswap, Is.EqualTo(Path.Combine(mod, "VSWAP.MOD")));
    }

    private static readonly string[] Wl6Files =
        ["AUDIOHED.WL6", "AUDIOT.WL6", "MAPHEAD.WL6", "GAMEMAPS.WL6", "VGADICT.WL6", "VGAHEAD.WL6", "VGAGRAPH.WL6", "VSWAP.WL6"];

    /// <summary>The work folder as a game folder: pfwolf.pk3 and copies of the WL6 files; the working folder is set to it</summary>
    private string GameFolderWithWl6()
    {
        foreach (var file in Wl6Files)
            File.Copy(Wl6(file), Path.Combine(_workFolder, file));
        ZipFile.CreateFromDirectory(TestPaths.Pk3SourceFolder(), Path.Combine(_workFolder, AssetManager.BasePk3FileName));
        Directory.SetCurrentDirectory(_workFolder);
        return _workFolder;
    }

    private static string Wl6(string file)
    {
        var candidates = new[] { Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA") }
            .Concat(new[] { "Debug", "Release" }.Select(c => Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64", c, "net10.0")))
            .Where(folder => !string.IsNullOrWhiteSpace(folder));
        var path = candidates.Select(folder => Path.Combine(folder!, file)).FirstOrDefault(File.Exists);
        if (path == null)
            Assert.Ignore($"{file} isn't in the built game folder; set PFWOLF_GAMEDATA to a folder that has it");
        return path!;
    }
}

/// <summary>
/// A VSWAP's pages, read and written back with walls put in, as WDC does: the header's counts,
/// offsets and lengths, then the pages one after another. Sound info page numbers count from the
/// first sound page, so they stay right.
/// </summary>
internal sealed class TestVswap
{
    public List<byte[]> Pages { get; } = [];
    public int SpriteStart { get; private set; }
    public int SoundStart { get; private set; }

    public static TestVswap Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        int count = BitConverter.ToUInt16(bytes, 0);
        var vswap = new TestVswap { SpriteStart = BitConverter.ToUInt16(bytes, 2), SoundStart = BitConverter.ToUInt16(bytes, 4) };
        // A page runs to the next one (as the game reads it), or its length field before a sparse one
        int Offset(int page) => page < count ? BitConverter.ToInt32(bytes, 6 + page * 4) : bytes.Length;
        for (int i = 0; i < count; i++)
        {
            var offset = Offset(i);
            var length = Offset(i + 1) == 0 ? BitConverter.ToUInt16(bytes, 6 + count * 4 + i * 2) : Offset(i + 1) - offset;
            vswap.Pages.Add(offset == 0 ? [] : bytes.AsSpan(offset, length).ToArray());
        }
        return vswap;
    }

    public void InsertWalls(int at, params byte[][] walls)
    {
        Pages.InsertRange(at, walls);
        SpriteStart += walls.Length;
        SoundStart += walls.Length;
    }

    public void Write(string path)
    {
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write((ushort)Pages.Count);
        writer.Write((ushort)SpriteStart);
        writer.Write((ushort)SoundStart);
        var offset = 6 + Pages.Count * 6;
        foreach (var page in Pages)
        {
            writer.Write(page.Length == 0 ? 0 : offset);
            offset += page.Length;
        }
        foreach (var page in Pages)
            writer.Write((ushort)page.Length);
        foreach (var page in Pages)
            writer.Write(page);
    }
}
