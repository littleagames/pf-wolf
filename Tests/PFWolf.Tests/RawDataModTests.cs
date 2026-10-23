using System.IO.Compression;
using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>
/// Raw data mods (docs/dev/raw-data-mods-plan.md): a folder mod whose gamepack-info.yaml adds a
/// game with its own file-pack plays the data files in its folder first, and the game folder's
/// for any it doesn't have. A file changed since the file-pack's md5 was written is warned about.
/// </summary>
public class RawDataModTests
{
    private string _workFolder = "";
    private string _startFolder = "";

    [SetUp]
    public void SetUp()
    {
        _startFolder = Directory.GetCurrentDirectory();
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-rawdata-" + Guid.NewGuid().ToString("N"));
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

    [Test]
    public void A_Mod_Game_Looks_For_Its_Data_Files_In_Its_Folder_First()
    {
        // Arrange: the mod folder has VSWAP, the game folder has the rest
        var mod = Directory.CreateDirectory(Path.Combine(_workFolder, "mod")).FullName;
        File.WriteAllText(Path.Combine(mod, "VSWAP.WL6"), "x");
        var info = new GamePackInfoAsset(new()
        {
            ["wolf3d"] = new GamePack { FilePack = new FilePack() },
            ["my-mod"] = new GamePack { BasePack = "wolf3d", FilePack = new FilePack(), ModFolder = mod },
            ["pk3-mod"] = new GamePack { BasePack = "wolf3d", FilePack = new FilePack() },
            ["overlay"] = new GamePack { BasePack = "my-mod" },
        });

        // Act / Assert
        Assert.That(info.ResolveDataFile("my-mod", "VSWAP.WL6"), Is.EqualTo(Path.Combine(mod, "VSWAP.WL6")));
        Assert.That(info.ResolveDataFile("my-mod", "AUDIOT.WL6"), Is.EqualTo("AUDIOT.WL6"), "not in the mod: the game folder's");
        Assert.That(info.ResolveDataFile("overlay", "VSWAP.WL6"), Is.EqualTo(Path.Combine(mod, "VSWAP.WL6")), "the file-pack's own mod");
        Assert.That(info.ResolveDataFile("pk3-mod", "VSWAP.WL6"), Is.EqualTo("VSWAP.WL6"));
        Assert.That(info.ResolveDataFile("wolf3d", "VSWAP.WL6"), Is.EqualTo("VSWAP.WL6"));
        Assert.That(info.GetDataFolder("wolf3d"), Is.Null);
    }

    [Test]
    public void A_Folder_Mods_Games_Know_Its_Folder_But_A_Pk3s_Do_Not()
    {
        // Arrange
        var folder = Directory.CreateDirectory(Path.Combine(_workFolder, "folder-mod")).FullName;
        File.WriteAllText(Path.Combine(folder, "gamepack-info.yaml"), "my-mod:\n  title: My Mod\n  base-pack: wolf3d\n");
        var pk3 = Path.Combine(_workFolder, "pk3-mod.pk3");
        ZipFile.CreateFromDirectory(folder, pk3);

        // Act
        var fromFolder = ModSource.ReadGames(new DirectoryAssetSource(folder), []);
        var fromPk3 = ModSource.ReadGames(new Pk3AssetSource(pk3), []);

        // Assert
        Assert.That(fromFolder!.GamePacks["my-mod"].ModFolder, Is.EqualTo(folder));
        Assert.That(fromPk3!.GamePacks["my-mod"].ModFolder, Is.Null);
    }

    [Test]
    [Category("GameData")]
    public void A_Raw_Data_Mod_Plays_Its_Own_Files_Over_The_Game_Folders()
    {
        // Arrange: the game folder has pfwolf.pk3 and the WL6 files; the mod a changed VSWAP and a
        // file-pack naming the WL6 files with the stock md5s
        ZipFile.CreateFromDirectory(TestPaths.Pk3SourceFolder(), Path.Combine(_workFolder, AssetManager.BasePk3FileName));
        foreach (var file in Wl6Files)
            File.Copy(Wl6(file), Path.Combine(_workFolder, file));

        var mod = Directory.CreateDirectory(Path.Combine(_workFolder, "raw-demo")).FullName;
        var vswap = File.ReadAllBytes(Path.Combine(_workFolder, "VSWAP.WL6"));
        var firstWallPage = BitConverter.ToInt32(vswap, 6);
        vswap[firstWallPage] ^= 0xFF;
        File.WriteAllBytes(Path.Combine(mod, "VSWAP.WL6"), vswap);
        File.WriteAllText(Path.Combine(mod, "gamepack-info.yaml"), GamePackInfo(_workFolder));

        var warnings = new List<string>();
        var sink = WarningLog.Sink;
        WarningLog.Sink = warnings.Add;
        GameContent content;
        try
        {
            // Act
            content = GameContent.Load(_workFolder, "", [mod]);
        }
        finally
        {
            WarningLog.Sink = sink;
        }

        // Assert
        var stockWall = new Wolf3dVswapFileLoader(Path.Combine(_workFolder, "VSWAP.WL6")).GetAssets([], [], [])["wall0000"];
        Assert.That(content.PackId, Is.EqualTo("raw-demo"));
        Assert.That(content.Find<TextureAsset>("GSTONEA1")!.RawData[0], Is.EqualTo((byte)(((TextureAsset)stockWall).RawData[0] ^ 0xFF)),
            "the mod's VSWAP");
        Assert.That(content.HasMap("MAP01"), Is.True, "the game folder's GAMEMAPS");
        Assert.That(content.Find<PFWolf.Assets.Sounds.AdLibSound>("ALHITWAL"), Is.Not.Null, "the game folder's AUDIOT");
        Assert.That(warnings, Has.One.Contains("VSWAP.WL6").And.Contains("has changed since"));
    }

    [Test]
    [Category("GameData")]
    public void A_Raw_Data_Mod_Missing_A_File_Everywhere_Says_Where_It_Looked()
    {
        // Arrange: no WL6 files at all
        ZipFile.CreateFromDirectory(TestPaths.Pk3SourceFolder(), Path.Combine(_workFolder, AssetManager.BasePk3FileName));
        Wl6("VSWAP.WL6");      // (ignored without the game data, like the test above)
        var mod = Directory.CreateDirectory(Path.Combine(_workFolder, "raw-demo")).FullName;
        File.WriteAllText(Path.Combine(mod, "gamepack-info.yaml"), GamePackInfo(folderWithMd5s: null));

        // Act / Assert
        var error = Assert.Throws<PFWolf.Exceptions.DataFilesException>(() => GameContent.Load(_workFolder, "", [mod]));
        Assert.That(error!.Message, Does.Contain(mod).And.Contain("game folder").And.Contain("VSWAP.WL6"));
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

    private static readonly string[] Wl6Files =
        ["AUDIOHED.WL6", "AUDIOT.WL6", "MAPHEAD.WL6", "GAMEMAPS.WL6", "VGADICT.WL6", "VGAHEAD.WL6", "VGAGRAPH.WL6", "VSWAP.WL6"];

    /// <summary>A game on wolf3d with its own file-pack of the WL6 names, with the md5s of the files in a folder</summary>
    private static string GamePackInfo(string? folderWithMd5s)
    {
        string Ref(string file)
            => folderWithMd5s == null
                ? $"{{ file: {file} }}"
                : $"{{ file: {file}, md5: {GamePackInfoAsset.Md5Of(Path.Combine(folderWithMd5s, file))} }}";

        return $$"""
            raw-demo:
              title: Raw Demo
              base-pack: wolf3d
              file-pack:
                file-loaders:
                  Wolf3DAudioFileLoader: { header: {{Ref("AUDIOHED.WL6")}}, data: {{Ref("AUDIOT.WL6")}} }
                  Wolf3DMapFileLoader: { header: {{Ref("MAPHEAD.WL6")}}, data: {{Ref("GAMEMAPS.WL6")}} }
                  Wolf3DVgaFileLoader: { dict: {{Ref("VGADICT.WL6")}}, header: {{Ref("VGAHEAD.WL6")}}, data: {{Ref("VGAGRAPH.WL6")}} }
                  Wolf3DVswapFileLoader: { data: {{Ref("VSWAP.WL6")}} }
            """;
    }
}
