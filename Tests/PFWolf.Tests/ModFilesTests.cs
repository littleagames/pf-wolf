using System.IO.Compression;
using System.Text;
using PFWolf.Constants;
using PFWolf.Editor.Editing;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>The editor writing into mods: folders, and pk3s rewritten in place with a .bak; and New mod's pk3s</summary>
public class ModFilesTests
{
    private string _folder = "";

    [SetUp]
    public void MakeFolder()
    {
        _folder = Path.Combine(Path.GetTempPath(), "pfwolf-modfiles-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void RemoveFolder()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    private string MakePk3(params (string Entry, string Text)[] files)
    {
        var path = Path.Combine(_folder, "mod.pk3");
        ModFiles.CreateArchive(path, files.Select(file => (file.Entry, Encoding.UTF8.GetBytes(file.Text))));
        return path;
    }

    private static Dictionary<string, string> Entries(string pk3)
    {
        using var archive = ZipFile.OpenRead(pk3);
        return archive.Entries.ToDictionary(entry => entry.FullName, entry =>
        {
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        });
    }

    [Test]
    public void Writing_Into_A_Pk3_Replaces_And_Adds_Entries_And_Keeps_The_Rest()
    {
        // Arrange: stored as "TEXTS\Old.txt", as some Windows tools write them
        var pk3 = MakePk3(("modinfo.yaml", "name: Mod"), ("TEXTS\\Old.txt", "old"));

        // Act
        var where = ModFiles.Write(pk3, ("texts/old.txt", Encoding.UTF8.GetBytes("new")), ("maps/MAP01.wad", [1, 2, 3]));

        // Assert
        var entries = Entries(pk3);
        Assert.That(entries.Keys, Is.EquivalentTo(new[] { "modinfo.yaml", "texts/old.txt", "maps/MAP01.wad" }));
        Assert.That(entries["texts/old.txt"], Is.EqualTo("new"));
        Assert.That(where, Is.EqualTo($"{pk3}: texts/old.txt"));
        Assert.That(File.Exists(pk3 + ".tmp"), Is.False, "no temporary file is left");
    }

    [Test]
    public void Writing_Into_A_Pk3_Keeps_The_Last_Version_As_Bak()
    {
        // Arrange
        var pk3 = MakePk3(("game-info.yaml", "first"));

        // Act
        ModFiles.Write(pk3, ("game-info.yaml", Encoding.UTF8.GetBytes("second")));
        ModFiles.Write(pk3, ("game-info.yaml", Encoding.UTF8.GetBytes("third")));

        // Assert: the .bak is the version before the last save
        Assert.That(Entries(pk3)["game-info.yaml"], Is.EqualTo("third"));
        Assert.That(Entries(pk3 + ModFiles.BackupExtension)["game-info.yaml"], Is.EqualTo("second"));
    }

    [Test]
    public void ReadText_Reads_A_Folder_Or_A_Pk3_And_Gives_Null_For_A_Missing_File()
    {
        // Arrange
        var pk3 = MakePk3(("game-info.yaml", "in the pk3"));
        var folder = Directory.CreateDirectory(Path.Combine(_folder, "folder-mod")).FullName;
        File.WriteAllText(Path.Combine(folder, "game-info.yaml"), "in the folder");

        // Act / Assert
        Assert.That(ModFiles.ReadText(pk3, "GAME-INFO.yaml"), Is.EqualTo("in the pk3"));
        Assert.That(ModFiles.ReadText(folder, "game-info.yaml"), Is.EqualTo("in the folder"));
        Assert.That(ModFiles.ReadText(pk3, "nothing.yaml"), Is.Null);
        Assert.That(ModFiles.ReadText(folder, "nothing.yaml"), Is.Null);
    }

    [Test]
    public void A_Level_Saved_Into_A_Pk3_Goes_In_With_Its_Game_Info()
    {
        // Arrange
        var pk3 = MakePk3(("game-info.yaml", "# kept\nmaps:\n  MAP01:\n    name: \"First\"\n"));
        var document = new MapDocument("MAP02", MapFiles.NewMap("MAP02", 1, 107), isNew: true);
        EditorMaps.Set(document, 0, 3, 4, 5);

        // Act
        var where = MapFiles.Save(document, pk3);

        // Assert
        var source = new Pk3AssetSource(pk3);
        var level = EcWolfMapLoader.Load(source.Open("maps/MAP02.wad").ToArray());
        var gameInfo = YamlDataEntryLoader.Deserialize<PFWolf.Assets.GameInfoAsset>(ModFiles.ReadText(pk3, "game-info.yaml")!);
        Assert.That(where, Is.EqualTo($"{pk3}: maps/MAP02.wad"));
        Assert.That(level.MapData[0][4 * MapConstants.MAPSIZE + 3], Is.EqualTo(5));
        Assert.That(gameInfo.Maps.Keys, Is.EquivalentTo(new[] { "MAP01", "MAP02" }));
        Assert.That(ModFiles.ReadText(pk3, "game-info.yaml"), Does.StartWith("# kept"));
        Assert.That(document.IsDirty, Is.False);
        Assert.That(document.SaveFolder, Is.EqualTo(pk3));
    }

    [TestCase("My Game!", "my-game")]
    [TestCase("  Deep  Blue 2 ", "deep-blue-2")]
    public void IdFrom_Makes_A_Game_Id_From_A_Name(string name, string id)
        => Assert.That(NewMod.IdFrom(name), Is.EqualTo(id));

    [TestCase("my-game", true)]
    [TestCase("wolf3d", false)]
    [TestCase("blake-aog", false)]
    [TestCase("standalone", false)]
    [TestCase("My Game", false)]
    [TestCase("", false)]
    public void CheckGameId_Takes_New_Lower_Case_Ids(string id, bool fine)
        => Assert.That(NewMod.CheckGameId(id) == null, Is.EqualTo(fine));

    [Test]
    public void A_New_Stand_Alone_Game_Is_A_Bare_Skeleton()
    {
        // Arrange
        var request = new NewModRequest(Path.Combine(_folder, "mods", "my-game.pk3"), "My \"Game\"", "my-game", BasePack: null);

        // Act
        NewMod.Create(request);

        // Assert
        var source = new Pk3AssetSource(request.Path);
        Assert.That(source.EntryPaths, Is.EquivalentTo(new[] { "modinfo.yaml", "gamepack-info.yaml", "game-info.yaml", "maps/MAP01.wad" }));
        var games = ModSource.ReadGames(source, []);
        Assert.That(games!.GamePacks["my-game"].BasePack, Is.EqualTo("standalone"));
        Assert.That(games.GamePacks["my-game"].Title, Is.EqualTo("My \"Game\""));
        var gameInfo = YamlDataEntryLoader.Deserialize<PFWolf.Assets.GameInfoAsset>(ModFiles.ReadText(request.Path, "game-info.yaml")!);
        Assert.That(gameInfo.Episodes["EP01"].StartMap, Is.EqualTo("MAP01"));
        Assert.That(EcWolfMapLoader.Load(source.Open("maps/MAP01.wad").ToArray()).Width, Is.EqualTo(MapConstants.MAPSIZE));
    }

    [Test]
    public void A_New_Mod_Of_A_Game_Is_Its_Modinfo_Naming_The_Game()
    {
        // Arrange
        var request = new NewModRequest(Path.Combine(_folder, "spear-mod.pk3"), "Spear Mod", "spear-mod", BasePack: "spear");

        // Act
        NewMod.Create(request);
        var mod = ModSource.TryOpen(request.Path, [])!;

        // Assert
        Assert.That(mod.Source.EntryPaths, Is.EqualTo(new[] { "modinfo.yaml" }));
        Assert.That(mod.Info.Name, Is.EqualTo("Spear Mod"));
        Assert.That(mod.HasGames, Is.False);
        Assert.That(mod.IsForGamePack("spear", ["wolf3d"]), Is.True);
        Assert.That(mod.IsForGamePack("wolf3d", []), Is.False);
    }

    [Test]
    public void CheckPath_Wants_A_New_Pk3_With_A_Full_Path()
    {
        // Arrange
        var taken = MakePk3(("modinfo.yaml", ""));

        // Act / Assert
        Assert.That(NewMod.CheckPath(Path.Combine(_folder, "new.pk3")), Is.Null);
        Assert.That(NewMod.CheckPath(taken), Is.Not.Null);
        Assert.That(NewMod.CheckPath("relative.pk3"), Is.Not.Null);
        Assert.That(NewMod.CheckPath(Path.Combine(_folder, "new.txt")), Is.Not.Null);
    }
}
