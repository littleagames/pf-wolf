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

    [Test]
    public void The_Raw_Data_Map_Gives_Only_The_Lists_That_Differ_And_Alias_Follows_Moved_Pics()
    {
        // Arrange: a pic put in before P1, which the help texts draw as chunk 4
        NamedChunk Named(int index, string name, ChunkMatch match, int? from = null) => new(index, name, match, "abcdef0123456789ff", from ?? index);
        var match = new RawDataMatch("wolf3d", "wolf3d", new GamePack(), new FilePack(), new DataFileSet(), BaseFilesFound: true,
        [
            new SectionMatch(ChunkSection.Sprites, ["S0"], [Named(0, "S0", ChunkMatch.Same)]),
            new SectionMatch(ChunkSection.Fonts, ["F0"], [Named(0, "F0", ChunkMatch.Same)]),
            new SectionMatch(ChunkSection.Pics, ["P0", "P1"],
                [Named(0, "P0", ChunkMatch.Same), new NamedChunk(1, "PIC0001", ChunkMatch.New, "abcdef0123456789ff", null), Named(2, "P1", ChunkMatch.Moved, 1)]),
            new SectionMatch(ChunkSection.Tile8, ["Tile8"], [Named(0, "Tile8", ChunkMatch.Same)]),
            new SectionMatch(ChunkSection.Externs, ["E0"], [Named(0, "E0", ChunkMatch.Moved)]),
        ], "STRUCTPIC", FontCount: 1)
        {
            GamePalette = "wolfpal",
            BaseAlias = new AliasAsset { ArtExtern = new() { [3] = "P1", [9] = "GONE" } },
        };

        // Act
        var files = RawDataModWriter.Write(match, "my-mod", "My Mod");
        var rawDataMap = files.Texts["raw-data-map.yaml"];
        var parsed = YamlDataEntryLoader.Deserialize<RawDataMapAsset>(rawDataMap);
        var alias = YamlDataEntryLoader.Deserialize<AliasAsset>(files.Texts["alias.yaml"]);

        // Assert
        Assert.That(parsed.Sprites, Is.Empty, "unchanged: the base game's");
        Assert.That(parsed.Graphics, Is.EqualTo(new[] { "STRUCTPIC", "F0", "P0", "PIC0001", "P1", "Tile8", "E0" }));
        Assert.That(rawDataMap, Does.Contain("PIC0001  # new: name this [abcdef0123456789]"));
        Assert.That(alias.ArtExtern, Is.EqualTo(new Dictionary<int, string> { [4] = "P1" }));
        Assert.That(files.Texts["alias.yaml"], Does.Contain("# 9: GONE"));
        Assert.That(files.Texts["gamepack-info.yaml"], Does.Contain("my-mod:").And.Contain("base-pack: wolf3d").And.Contain("game-palette: wolfpal"));
        Assert.That(files.Report, Does.Contain("PIC0001"));
    }

    [Test]
    public void Run_Again_A_Mod_Keeps_The_Names_Given_Since_And_Says_What_Went()
    {
        // Arrange: last time PIC0001 (hash abcd...) was renamed BOSSPIC, and a chunk now gone was GONEPIC
        var match = new RawDataMatch("wolf3d", "wolf3d", new GamePack(), new FilePack(), new DataFileSet(), BaseFilesFound: true,
        [
            new SectionMatch(ChunkSection.Pics, ["P0"],
                [new NamedChunk(0, "P0", ChunkMatch.Same, "1111", 0), new NamedChunk(1, "PIC0001", ChunkMatch.New, "abcdef0123456789ff", null)]),
        ], "STRUCTPIC", FontCount: 0) { GamePalette = "wolfpal" };
        const string previous = """
            graphics:
                - STRUCTPIC
                - P0
                - BOSSPIC  # new [abcdef0123456789]
                - "GONE PIC"  # new [9999999999999999]
            """;

        // Act
        var files = RawDataModWriter.Write(match, "my-mod", "My Mod", previous);

        // Assert
        Assert.That(RawDataModWriter.KeptNames(previous), Is.EqualTo(new Dictionary<string, string>
        {
            ["abcdef0123456789"] = "BOSSPIC",
            ["9999999999999999"] = "GONE PIC",
        }));
        Assert.That(files.Texts["raw-data-map.yaml"], Does.Contain("- BOSSPIC  # new [abcdef0123456789]"));
        Assert.That(files.Report, Does.Not.Contain("To name").And.Contain("No longer in the data files (named last time): GONE PIC"));
    }

    [Test]
    public void A_Chunk_Is_Named_On_Its_Line_And_In_The_Walls_Made_For_It()
    {
        // Arrange
        const string map = "walls:\n    - GSTONEA1\n    - WALL0098  # new: name this [0123456789abcdef]\n    - \"WALL0099\"  # new: name this [fedcba9876543210]\n";
        const string walls = "walls:\n    '50':\n        North: WALL0098\n        East: WALL0099  # dark\n        West: WALL00980\n";

        // Act
        var renamed = RawDataModWriter.RenameChunk(map, "wall0098", "BUNKER1");
        var renamedWalls = RawDataModWriter.RenameValues(walls, "WALL0098", "BUNKER1");

        // Assert
        Assert.That(RawDataModWriter.NamesToGive(map), Is.EqualTo(new[] { "WALL0098", "WALL0099" }));
        Assert.That(renamed, Does.Contain("    - BUNKER1  # new [0123456789abcdef]\n"));
        Assert.That(RawDataModWriter.NamesToGive(renamed!), Is.EqualTo(new[] { "WALL0099" }));
        Assert.That(RawDataModWriter.KeptNames(renamed!)["0123456789abcdef"], Is.EqualTo("BUNKER1"), "a run after keeps it");
        Assert.That(RawDataModWriter.RenameChunk(map, "NOPE", "X"), Is.Null);
        Assert.That(renamedWalls, Does.Contain("North: BUNKER1\n").And.Contain("East: WALL0099  # dark").And.Contain("West: WALL00980"));
    }

    [Test]
    [Category("GameData")]
    public void The_Editor_Lists_The_Chunks_To_Name_And_Names_One()
    {
        // Arrange: a made mod with one new wall id
        var data = GameFolderWithWl6();
        var source = Directory.CreateDirectory(Path.Combine(_workFolder, "source")).FullName;
        var pages = TestVswap.Read(Path.Combine(data, "VSWAP.WL6"));
        pages.InsertWalls(pages.SpriteStart - 8, Enumerable.Repeat((byte)7, 4096).ToArray(), Enumerable.Repeat((byte)8, 4096).ToArray());
        pages.Write(Path.Combine(source, "VSWAP.WL6"));
        var mod = Path.Combine(_workFolder, "made-mod");
        var match = RawDataModGenerator.Match(source, "wolf3d");
        RawDataModWriter.Save(RawDataModWriter.Write(match, "made-mod", "Made Mod"), mod, overwrite: false);
        var added = match[ChunkSection.Walls]!.BaseNames.Count - 8;
        var placeholder = $"WALL{added:0000}";

        // Act
        var content = PFWolf.Editor.Data.GameContent.Load(data, "", [mod]);
        var toName = PFWolf.Editor.Data.RawDataMapFile.NamesToGive(content);
        PFWolf.Editor.Data.RawDataMapFile.Rename(toName[placeholder], PFWolf.Editor.Data.ArtKind.Texture, placeholder, "BUNKER1").Apply();
        var reloaded = PFWolf.Editor.Data.GameContent.Load(data, "", [mod]);

        // Assert
        Assert.That(toName.Keys, Is.EquivalentTo(new[] { placeholder, $"WALL{added + 1:0000}" }));
        Assert.That(reloaded.Find<TextureAsset>("BUNKER1")!.RawData[0], Is.EqualTo(7));
        Assert.That(reloaded.Find<TextureAsset>(placeholder), Is.Null);
        Assert.That(reloaded.MapDefs.Walls[added / 2 + 1].North, Is.EqualTo("BUNKER1"));
        Assert.That(PFWolf.Editor.Data.RawDataMapFile.NamesToGive(reloaded).Keys, Is.EquivalentTo(new[] { $"WALL{added + 1:0000}" }));
    }

    [Test]
    public void The_Mods_Own_Palette_Goes_In_As_The_Game_Palette()
    {
        // Arrange
        var match = new RawDataMatch("wolf3d", "wolf3d", new GamePack(), new FilePack(), new DataFileSet(), BaseFilesFound: true,
            [new SectionMatch(ChunkSection.Sprites, ["S0"], [new NamedChunk(0, "S0", ChunkMatch.Same, "1", 0)])], "STRUCTPIC", FontCount: 2)
        {
            GamePalette = "wolfpal",
        };
        var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, 0, (byte)(255 - i))).ToArray();
        var mod = Path.Combine(_workFolder, "mod");

        // Act
        var files = RawDataModWriter.Write(match, "my-mod", "My Mod", palette: new RawDataModPalette("TC.PAL", colors, "JASC-PAL"));
        RawDataModWriter.Save(files, mod, overwrite: false);
        var without = RawDataModWriter.Write(match, "my-mod", "My Mod");

        // Assert
        Assert.That(File.ReadAllBytes(Path.Combine(mod, "palettes", "wolfpal.pal")), Is.EqualTo(PaletteFile.ToRaw(colors)));
        Assert.That(files.Report, Does.Contain("Palette: palettes/wolfpal.pal").And.Contain("from TC.PAL (JASC-PAL)"));
        Assert.That(files.Report, Does.Not.Contain("the palette (a palette file"));
        Assert.That(without.Binaries, Is.Empty);
        Assert.That(without.Report, Does.Contain("the palette (a palette file"));
    }

    [Test]
    public void The_One_Palette_File_Beside_The_Data_Files_Is_Found()
    {
        // Arrange
        var one = Directory.CreateDirectory(Path.Combine(_workFolder, "one")).FullName;
        File.WriteAllText(Path.Combine(one, "GAMEPAL.PAL"), "JASC-PAL\r\n0100\r\n256\r\n1 2 3\r\n");
        File.WriteAllText(Path.Combine(one, "README.PAL"), "not a palette");
        var two = Directory.CreateDirectory(Path.Combine(_workFolder, "two")).FullName;
        File.WriteAllBytes(Path.Combine(two, "A.PAL"), new byte[768]);
        File.WriteAllBytes(Path.Combine(two, "B.ACT"), new byte[772]);

        // Act / Assert
        Assert.That(RawDataModGenerator.FindPaletteFile(one), Is.EqualTo(Path.Combine(one, "GAMEPAL.PAL")));
        Assert.That(RawDataModGenerator.FindPaletteFile(two), Is.Null, "which of the two isn't for it to say");
        Assert.That(RawDataModPalette.FromFile(Path.Combine(one, "GAMEPAL.PAL")).Colors[0], Is.EqualTo(new PaletteColor(1, 2, 3)));
    }

    [Test]
    [Category("GameData")]
    public void A_Made_Mod_With_Its_Own_Palette_Is_Drawn_In_It()
    {
        // Arrange: the stock VSWAP and a palette with every color the same blue
        var data = GameFolderWithWl6();
        var source = Directory.CreateDirectory(Path.Combine(_workFolder, "source")).FullName;
        File.Copy(Path.Combine(data, "VSWAP.WL6"), Path.Combine(source, "VSWAP.WL6"));
        File.WriteAllBytes(Path.Combine(source, "GAMEPAL.PAL"), Enumerable.Repeat(new byte[] { 0, 0, 63 }, 256).SelectMany(rgb => rgb).ToArray());
        var mod = Path.Combine(_workFolder, "blue-mod");

        // Act
        var match = RawDataModGenerator.Match(source, "wolf3d");
        var palette = RawDataModPalette.FromFile(RawDataModGenerator.FindPaletteFile(source)!);
        RawDataModWriter.Save(RawDataModWriter.Write(match, "blue-mod", "Blue Mod", palette: palette), mod, overwrite: false);
        var content = PFWolf.Editor.Data.GameContent.Load(data, "", [mod]);

        // Assert
        Assert.That(palette.How, Does.Contain("6-bit"));
        Assert.That(content.Palette[5], Is.EqualTo(new PaletteColor(0, 0, 255)).Using<PaletteColor>((a, b) => a.Red == b.Red && a.Green == b.Green && a.Blue == b.Blue));
    }

    [Test]
    public void A_Data_File_That_Cannot_Be_Read_Is_Named_With_The_Files_Read_With_It()
    {
        // Arrange: a VGAHEAD whose size isn't a whole number of 3-byte offsets
        string File(string name, int size)
        {
            var path = Path.Combine(_workFolder, name);
            System.IO.File.WriteAllBytes(path, new byte[size]);
            return path;
        }
        var files = new DataFileSet(VgaDict: File("FOXTROT6.WL6", 1024), VgaHead: File("FOXTROT4.WL6", 4), VgaGraph: File("FOXTROT5.WL6", 16));

        // Act
        var error = Assert.Throws<PFWolf.Exceptions.DataFileReadException>(() => DataFileChunks.Read(files, fontCount: 2));

        // Assert
        Assert.That(error!.Message, Does.StartWith("FOXTROT5.WL6 (VGAGRAPH, read with FOXTROT4.WL6 and FOXTROT6.WL6) can't be read: "));
    }

    [TestCase("TED5v1.0")]
    [TestCase("TED6v1.0")]
    [TestCase("CED0.7:Mapfile was edited with ChaosEdit (www.chaos-software.de.vu)")]
    public void Levels_Are_Told_By_The_Banner_Of_The_Editor_That_Saved_Them(string banner)
    {
        // Arrange
        var file = Path.Combine(_workFolder, "FOXTROT7.WL6");
        File.WriteAllBytes(file, [.. System.Text.Encoding.ASCII.GetBytes(banner), .. new byte[256]]);

        // Act
        var guesses = DataFileSniffer.Guess([file]);

        // Assert
        Assert.That(guesses, Is.EquivalentTo(new Dictionary<DataFileKind, string> { [DataFileKind.GameMaps] = file }));
    }

    [Test]
    public void A_Wdc_Map_Export_Is_Not_Taken_For_The_Levels()
    {
        // Arrange
        var file = Path.Combine(_workFolder, "Map - 0.map");
        File.WriteAllBytes(file, [.. System.Text.Encoding.ASCII.GetBytes("WDC3.1"), .. new byte[256]]);

        // Act
        var guesses = DataFileSniffer.Guess([file]);

        // Assert
        Assert.That(guesses, Does.Not.ContainKey(DataFileKind.GameMaps));
    }

    [Test]
    [Category("GameData")]
    public void Data_Files_Named_Anything_Are_Told_Apart_By_What_Is_In_Them()
    {
        // Arrange: the WL6 files under names that say nothing, and a text file
        var data = GameFolderWithWl6();
        var mod = Directory.CreateDirectory(Path.Combine(_workFolder, "foxtrot")).FullName;
        var renamed = new Dictionary<string, DataFileKind>
        {
            ["FOXTROT1.WL6"] = DataFileKind.Vswap, ["FOXTROT2.WL6"] = DataFileKind.VgaDict, ["FOXTROT3.WL6"] = DataFileKind.VgaHead,
            ["FOXTROT4.WL6"] = DataFileKind.VgaGraph, ["FOXTROT5.WL6"] = DataFileKind.AudioHed, ["FOXTROT6.WL6"] = DataFileKind.AudioT,
            ["FOXTROT7.WL6"] = DataFileKind.MapHead, ["FOXTROT8.WL6"] = DataFileKind.GameMaps,
        };
        foreach (var (name, kind) in renamed)
            File.Copy(Path.Combine(data, DataFileSniffer.StemOf(kind) + ".WL6"), Path.Combine(mod, name));
        File.WriteAllText(Path.Combine(mod, "README.TXT"), "Operation Foxtrot");

        // Act
        var guesses = DataFileSniffer.Guess(Directory.GetFiles(mod));
        var candidates = RawDataModGenerator.DetectBase(mod);
        var match = RawDataModGenerator.Match(mod, "wolf3d", candidates[0].ModFiles);
        var made = Path.Combine(_workFolder, "foxtrot-mod");
        RawDataModWriter.Save(RawDataModWriter.Write(match, "foxtrot", "Foxtrot"), made, overwrite: false);
        var content = PFWolf.Editor.Data.GameContent.Load(data, "", [made]);

        // Assert
        Assert.That(guesses.ToDictionary(guess => Path.GetFileName(guess.Value), guess => guess.Key), Is.EquivalentTo(renamed));
        Assert.That(candidates[0].ReleaseId, Is.EqualTo("wolf3d"));
        Assert.That(candidates[0].Sniffed, Is.EqualTo(8));
        Assert.That(candidates[0].Share, Is.EqualTo(1.0));
        Assert.That(File.ReadAllText(Path.Combine(made, "gamepack-info.yaml")), Does.Contain("data: { file: FOXTROT1.WL6"));
        Assert.That(content.PackId, Is.EqualTo("foxtrot"));
        Assert.That(content.HasMap("MAP01"), Is.True);
        Assert.That(content.Find<TextureAsset>("GSTONEA1"), Is.Not.Null);
    }

    [Test]
    [Category("GameData")]
    public void The_Modder_Says_Which_File_Is_Which_Over_The_Guess()
    {
        // Arrange: a VSWAP under another name, and an explicit set naming it
        var data = GameFolderWithWl6();
        var mod = Directory.CreateDirectory(Path.Combine(_workFolder, "mod")).FullName;
        File.Copy(Path.Combine(data, "VSWAP.WL6"), Path.Combine(mod, "PAGES.DAT"));
        var files = new DataFileSet().With(DataFileKind.Vswap, Path.Combine(mod, "PAGES.DAT"));

        // Act
        var candidates = RawDataModGenerator.DetectBase(mod, files);

        // Assert
        Assert.That(candidates[0].ModFiles, Is.EqualTo(files));
        Assert.That(candidates[0].Sniffed, Is.Zero, "said, not guessed");
        Assert.That(RawDataModGenerator.FolderFiles(mod), Is.EqualTo(new[] { (Path.Combine(mod, "PAGES.DAT"), new FileInfo(Path.Combine(data, "VSWAP.WL6")).Length) }));
    }

    [Test]
    [Category("GameData")]
    public void The_Command_Line_Takes_Which_File_Is_Which()
    {
        // Arrange
        var data = GameFolderWithWl6();
        var source = Directory.CreateDirectory(Path.Combine(_workFolder, "Odd TC")).FullName;
        File.Copy(Path.Combine(data, "VSWAP.WL6"), Path.Combine(source, "PAGES.DAT"));
        var exe = Path.Combine(TestContext.CurrentContext.TestDirectory, "PFWolf.exe");
        var start = new System.Diagnostics.ProcessStartInfo(exe)
        {
            WorkingDirectory = _workFolder, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (var arg in new[] { "--make-mod", source, "--data-file", "VSWAP=PAGES.DAT", "--out", Path.Combine(_workFolder, "out") })
            start.ArgumentList.Add(arg);
        start.Environment["SDL_VIDEODRIVER"] = "dummy";

        // Act
        using var process = System.Diagnostics.Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        Assert.That(process.WaitForExit(TimeSpan.FromMinutes(2)), Is.True);
        var output = stdout.Result + process.StandardError.ReadToEnd();

        // Assert
        Assert.That(process.ExitCode, Is.Zero, output);
        Assert.That(output, Does.Contain("VSWAP     PAGES.DAT"));
        Assert.That(File.ReadAllText(Path.Combine(_workFolder, "out", "gamepack-info.yaml")), Does.Contain("file: PAGES.DAT"));
    }

    [Test]
    public void A_Game_Id_Is_Made_From_The_Title()
    {
        Assert.That(RawDataModWriter.GameIdFrom("My Mod: Part 2!"), Is.EqualTo("my-mod-part-2"));
        Assert.That(RawDataModWriter.GameIdFrom("???"), Is.EqualTo("raw-data-mod"));
    }

    [Test]
    [Category("GameData")]
    public void A_Made_Mod_Loads_With_Its_New_Walls_Named_And_Given_Wall_Ids()
    {
        // Arrange: two new wall pages (one wall id's pair) before the doors
        var data = GameFolderWithWl6();
        var source = Directory.CreateDirectory(Path.Combine(_workFolder, "source")).FullName;
        var pages = TestVswap.Read(Path.Combine(data, "VSWAP.WL6"));
        pages.InsertWalls(pages.SpriteStart - 8, Enumerable.Repeat((byte)7, 4096).ToArray(), Enumerable.Repeat((byte)8, 4096).ToArray());
        pages.Write(Path.Combine(source, "VSWAP.WL6"));
        var mod = Path.Combine(_workFolder, "made-mod");

        // Act
        var match = RawDataModGenerator.Match(source, "wolf3d");
        var files = RawDataModWriter.Write(match, "made-mod", "Made Mod");
        var clashes = RawDataModWriter.Save(files, mod, overwrite: false);
        var content = PFWolf.Editor.Data.GameContent.Load(data, "", [mod]);
        var again = RawDataModWriter.Save(files, mod, overwrite: false);

        // Assert
        var added = match[ChunkSection.Walls]!.BaseNames.Count - 8;
        Assert.That(clashes, Is.Empty);
        Assert.That(again, Is.Not.Empty, "the second time, it's there already");
        Assert.That(File.Exists(Path.Combine(mod, "VSWAP.WL6")), Is.True, "the data file copied in");
        Assert.That(content.PackId, Is.EqualTo("made-mod"));
        Assert.That(content.Find<TextureAsset>($"WALL{added:0000}")!.RawData[0], Is.EqualTo(7));
        Assert.That(content.Find<TextureAsset>("DOOR1_1"), Is.Not.Null);
        Assert.That(content.MapDefs.Walls[added / 2 + 1].North, Is.EqualTo($"WALL{added:0000}"));
        Assert.That(content.MapDefs.Walls[added / 2 + 1].East, Is.EqualTo($"WALL{added + 1:0000}"));
        Assert.That(content.MapDefs.Doors, Is.Not.Empty, "wolf3d's doors still there");
        Assert.That(content.HasMap("MAP01"), Is.True);
    }

    [Test]
    [Category("GameData")]
    public void The_Game_Makes_A_Mod_From_The_Command_Line()
    {
        // Arrange
        var data = GameFolderWithWl6();
        var source = Directory.CreateDirectory(Path.Combine(_workFolder, "My TC")).FullName;
        File.Copy(Path.Combine(data, "VSWAP.WL6"), Path.Combine(source, "VSWAP.WL6"));
        var exe = Path.Combine(TestContext.CurrentContext.TestDirectory, "PFWolf.exe");
        Assert.That(File.Exists(exe), Is.True, $"{exe} wasn't built beside the tests");
        var start = new System.Diagnostics.ProcessStartInfo(exe)
        {
            WorkingDirectory = _workFolder,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in new[] { "--make-mod", source, "--out", Path.Combine(_workFolder, "out") })
            start.ArgumentList.Add(arg);
        start.Environment["SDL_VIDEODRIVER"] = "dummy";
        start.Environment["SDL_AUDIODRIVER"] = "dummy";

        // Act
        using var process = System.Diagnostics.Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.That(process.WaitForExit(TimeSpan.FromMinutes(2)), Is.True, "didn't quit");
        var output = stdout.Result + stderr.Result;

        // Assert
        Assert.That(process.ExitCode, Is.Zero, output);
        Assert.That(output, Does.Contain("Building on wolf3d").And.Contain("my-tc"));
        Assert.That(File.ReadAllText(Path.Combine(_workFolder, "out", "gamepack-info.yaml")), Does.Contain("my-tc:"));
        Assert.That(File.Exists(Path.Combine(_workFolder, "out", "raw-data-map.yaml")), Is.False, "nothing differs from wolf3d's names");
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
