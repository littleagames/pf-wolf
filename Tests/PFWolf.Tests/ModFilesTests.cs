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
    public void Writing_Into_A_Pk3_Keeps_Each_File_In_Its_Place()
    {
        // Arrange: the game loads a pk3's files in order, and a later one of an asset wins
        var pk3 = MakePk3(("actordefs/a.yaml", "a"), ("actordefs/b.yaml", "b"), ("actordefs/c.yaml", "c"));

        // Act
        ModFiles.Write(pk3, ("actordefs/b.yaml", Encoding.UTF8.GetBytes("b2")), ("actordefs/d.yaml", Encoding.UTF8.GetBytes("d")));

        // Assert
        using var archive = ZipFile.OpenRead(pk3);
        Assert.That(archive.Entries.Select(entry => entry.FullName),
            Is.EqualTo(new[] { "actordefs/a.yaml", "actordefs/b.yaml", "actordefs/c.yaml", "actordefs/d.yaml" }));
        Assert.That(new Pk3AssetSource(pk3).EntryPaths, Is.EqualTo(new[] { "actordefs/a.yaml", "actordefs/b.yaml", "actordefs/c.yaml", "actordefs/d.yaml" }));
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
    public void PfWolf_Pk3_Is_Never_Written()
    {
        // Arrange
        var pk3 = Path.Combine(_folder, "PFWOLF.pk3");
        ModFiles.CreateArchive(pk3, [("game-info.yaml", Encoding.UTF8.GetBytes("first"))]);

        // Act, Assert
        Assert.Throws<UnauthorizedAccessException>(() => ModFiles.Write(pk3, ("game-info.yaml", Encoding.UTF8.GetBytes("second"))));
        Assert.Throws<UnauthorizedAccessException>(() => ModFiles.Delete(pk3, "game-info.yaml"));
        Assert.That(Entries(pk3)["game-info.yaml"], Is.EqualTo("first"));
        Assert.That(File.Exists(pk3 + ModFiles.BackupExtension), Is.False);
    }

    [Test]
    public void The_Folder_PfWolf_Pk3_Is_Built_From_Is_Never_Written()
    {
        // Arrange: named pfwolf-pk3, or (renamed) with gamepacks/gamepack-info.yaml in it
        var named = Path.Combine(_folder, ModFiles.BaseSourceFolderName);
        var renamed = Path.Combine(_folder, "base-copy");
        Directory.CreateDirectory(Path.Combine(named, "actordefs"));
        Directory.CreateDirectory(Path.Combine(renamed, "gamepacks"));
        File.WriteAllText(Path.Combine(renamed, "gamepacks", "gamepack-info.yaml"), "wolf3d:\n");

        // Act, Assert: a folder inside it too
        Assert.Throws<UnauthorizedAccessException>(() => ModFiles.Write(named, ("game-info.yaml", [1])));
        Assert.Throws<UnauthorizedAccessException>(() => ModFiles.Write(Path.Combine(named, "actordefs"), ("x.yaml", [1])));
        Assert.Throws<UnauthorizedAccessException>(() => ModFiles.Write(renamed, ("game-info.yaml", [1])));
        Assert.That(File.Exists(Path.Combine(named, "game-info.yaml")), Is.False);
        Assert.That(ModFiles.WhyReadOnly(Path.Combine(_folder, "my-mod")), Is.Null);
        Assert.That(NewMod.CheckPath(Path.Combine(_folder, "mods", "pfwolf.pk3")), Is.Not.Null);
    }

    [Test]
    public void Change_Renames_And_Deletes_In_A_Pk3_As_One_Rewrite()
    {
        // Arrange
        var pk3 = MakePk3(("textures/A.png", "a"), ("SPRITES/B.png", "b"), ("modinfo.yaml", "name: Mod"));

        // Act: A renamed to C (written new, the old taken out), and B deleted (named in another case)
        ModFiles.Change(pk3, [("textures/C.png", Encoding.UTF8.GetBytes("a"))], ["textures/A.png"]);
        ModFiles.Delete(pk3, "sprites/b.PNG");

        // Assert
        var entries = Entries(pk3);
        Assert.That(entries.Keys, Is.EquivalentTo(new[] { "modinfo.yaml", "textures/C.png" }));
        Assert.That(Entries(pk3 + ModFiles.BackupExtension).Keys, Does.Contain("SPRITES/B.png"), "the .bak is the version before the delete");
    }

    [Test]
    public void Change_Writes_A_File_It_Is_Also_Told_To_Delete()
    {
        // Arrange
        var pk3 = MakePk3(("graphics/X.png", "old"));

        // Act
        ModFiles.Change(pk3, [("graphics/X.png", Encoding.UTF8.GetBytes("new"))], ["graphics/x.png"]);

        // Assert
        Assert.That(Entries(pk3)["graphics/X.png"], Is.EqualTo("new"));
    }

    [Test]
    public void Change_In_A_Folder_Writes_And_Deletes_Files()
    {
        // Arrange
        var mod = Path.Combine(_folder, "folder-mod");
        Directory.CreateDirectory(Path.Combine(mod, "sprites"));
        File.WriteAllText(Path.Combine(mod, "sprites", "OLD.png"), "old");

        // Act
        ModFiles.Change(mod, [("sprites/NEW.png", Encoding.UTF8.GetBytes("new"))], ["sprites/OLD.png"]);

        // Assert
        Assert.That(File.Exists(Path.Combine(mod, "sprites", "OLD.png")), Is.False);
        Assert.That(ModFiles.ReadBytes(mod, "sprites/NEW.png"), Is.EqualTo(Encoding.UTF8.GetBytes("new")));
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
        var read = EcWolfMapLoader.Load(source.Open("maps/MAP02.wad").ToArray());
        Assert.That(read.Format, Is.EqualTo(PFWolf.Assets.MapFormat.Uwmf), "a new level saves as UWMF");
        var level = PFWolf.Loaders.Uwmf.UwmfTranslator.ToMapAsset(read.Uwmf!, new PFWolf.Assets.MapObjectTranslationAsset());
        var gameInfo = YamlDataEntryLoader.Deserialize<PFWolf.Assets.GameInfoAsset>(ModFiles.ReadText(pk3, "game-info.yaml")!);
        Assert.That(where, Is.EqualTo($"{pk3}: maps/MAP02.wad"));
        Assert.That(level.MapData[0][4 * MapConstants.ClassicMapSize + 3], Is.EqualTo(5));
        Assert.That(gameInfo.Maps.Keys, Is.EquivalentTo(new[] { "MAP01", "MAP02" }));
        Assert.That(ModFiles.ReadText(pk3, "game-info.yaml"), Does.StartWith("# kept"));
        Assert.That(document.IsDirty, Is.False);
        Assert.That(document.SaveFolder, Is.EqualTo(pk3));
    }

    [TestCase("My Game!", "my-game")]
    [TestCase("  Deep  Blue 2 ", "deep-blue-2")]
    [TestCase("Deep   Blue Sea", "deep-blue-sea")]
    [TestCase("Bob's Game", "bobs-game")]
    [TestCase("Already-a - name", "already-a-name")]
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
    public void A_New_Stand_Alone_Game_Starts_From_The_Example_Game_Under_Its_Own_Name()
    {
        // Arrange
        var request = new NewModRequest(Path.Combine(_folder, "mods", "my-game.pk3"), "My \"Game\"", "my-game", BasePack: null);
        var palette = File.ReadAllBytes(Path.Combine(TestPaths.Pk3SourceFolder(), "palettes", "wolfpal.pal"));
        var example = Path.Combine(TestPaths.RepoRoot(), "examples", "mods", "standalone-demo");
        var exampleFiles = Directory.GetFiles(example, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(example, file).Replace('\\', '/'))
            .Where(file => !file.EndsWith(".zip") && file is not ("gamepack-info.yaml" or "modinfo.yaml" or "game-info.yaml"));

        // Act
        NewMod.Create(request, palette);

        // Assert
        var source = new Pk3AssetSource(request.Path);
        Assert.That(source.EntryPaths, Is.EquivalentTo(exampleFiles.Concat(["modinfo.yaml", "gamepack-info.yaml", "game-info.yaml", "palettes/wolfpal.pal"])));
        Assert.That(source.Open("mapdefs/map.yaml").ToArray(), Is.EqualTo(File.ReadAllBytes(Path.Combine(example, "mapdefs", "map.yaml"))));
        Assert.That(source.Open("palettes/wolfpal.pal").ToArray(), Is.EqualTo(palette));
        var games = ModSource.ReadGames(source, []);
        Assert.That(games!.GamePacks["my-game"].BasePack, Is.EqualTo("standalone"));
        Assert.That(games.GamePacks["my-game"].GamePalette, Is.EqualTo("wolfpal"));
        Assert.That(games.GamePacks["my-game"].Title, Is.EqualTo("My \"Game\""));
        var gameInfo = YamlDataEntryLoader.Deserialize<PFWolf.Assets.GameInfoAsset>(ModFiles.ReadText(request.Path, "game-info.yaml")!);
        Assert.That(gameInfo.Episodes["EP01"].StartMap, Is.EqualTo("MAP01"));
        Assert.That(EcWolfMapLoader.Load(source.Open("maps/MAP01.wad").ToArray()).Width, Is.EqualTo(MapConstants.ClassicMapSize));
    }

    [Test]
    public void The_Default_Palette_Comes_From_The_Game_Folders_Pfwolf_Pk3()
    {
        // Arrange
        ZipFile.CreateFromDirectory(TestPaths.Pk3SourceFolder(), Path.Combine(_folder, "pfwolf.pk3"));

        // Act
        var palette = NewMod.ReadDefaultPalette(_folder);
        var none = NewMod.ReadDefaultPalette(Path.Combine(_folder, "nowhere"));

        // Assert
        Assert.That(palette, Is.EqualTo(File.ReadAllBytes(Path.Combine(TestPaths.Pk3SourceFolder(), "palettes", "wolfpal.pal"))));
        Assert.That(none, Is.Null);
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
        Assert.That(mod.Info.Id, Is.EqualTo("spear-mod"));
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
