using PFWolf.Assets;
using PFWolf.Configuration;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>
/// The player's data folder (docs/dev/mac-linux-plan.md, phase 3): data files and mods looked for
/// there after the game folder, for a game installed where it can't be written to
/// </summary>
public class UserDataFolderTests
{
    private string _workFolder = "";
    private string _gameFolder = "";
    private string _userFolder = "";
    private string _startFolder = "";

    [SetUp]
    public void SetUp()
    {
        _startFolder = Directory.GetCurrentDirectory();
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-userdata-" + Guid.NewGuid().ToString("N"));
        _gameFolder = Directory.CreateDirectory(Path.Combine(_workFolder, "game")).FullName;
        _userFolder = Directory.CreateDirectory(Path.Combine(_workFolder, "user")).FullName;
        UserDataFolder.Override = _userFolder;
        Directory.SetCurrentDirectory(_gameFolder);
    }

    [TearDown]
    public void TearDown()
    {
        UserDataFolder.Override = null;
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

    private static GamePackInfoAsset Wolf3d() => new(new() { ["wolf3d"] = new GamePack { FilePack = new FilePack() } });

    [Test]
    public void A_Data_File_Only_In_The_Players_Folder_Is_Found_There()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_userFolder, "VSWAP.WL6"), "x");

        // Act
        var found = Wolf3d().ResolveDataFile("wolf3d", "vswap.wl6");

        // Assert
        Assert.That(File.Exists(found), Is.True, found);
        Assert.That(Path.GetDirectoryName(Path.GetFullPath(found)), Is.EqualTo(_userFolder));
    }

    [Test]
    public void The_Game_Folders_Data_File_Comes_First()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_gameFolder, "VSWAP.WL6"), "game");
        File.WriteAllText(Path.Combine(_userFolder, "VSWAP.WL6"), "user");

        // Act
        var found = Wolf3d().ResolveDataFile("wolf3d", "VSWAP.WL6");

        // Assert
        Assert.That(File.ReadAllText(found), Is.EqualTo("game"));
        Assert.That(Wolf3d().FindMissingDataFiles("wolf3d"), Is.Empty);
    }

    [Test]
    public void A_Data_File_In_Neither_Keeps_Its_Name()
    {
        Assert.That(Wolf3d().ResolveDataFile("wolf3d", "AUDIOT.WL6"), Is.EqualTo("AUDIOT.WL6"));
    }

    [Test]
    public void Mods_In_The_Players_Mods_Folder_Are_Listed_Found_And_Named()
    {
        // Arrange
        var mods = Directory.CreateDirectory(Path.Combine(_userFolder, "mods")).FullName;
        var name = "zz-user-mod-" + Guid.NewGuid().ToString("N")[..8];
        var pk3 = Path.Combine(mods, name + ".pk3");
        File.WriteAllBytes(pk3, []);

        // Act / Assert
        Assert.That(ModSource.ModsFolders, Has.Member(mods));
        Assert.That(ModSource.FindInModsFolder(), Has.Member(pk3));
        Assert.That(ModSource.ResolvePath(name), Is.EqualTo(pk3), "--file name finds name.pk3");
        Assert.That(ModsConfig.ConfigName(pk3), Is.EqualTo(name + ".pk3"), "mods.cfg keeps the name");
    }

    [Test]
    public void New_Mods_Go_To_The_Players_Folder_When_The_Games_Cant_Take_Them()
    {
        // Arrange: a game folder that isn't there can't be written to
        var missingGame = Path.Combine(_workFolder, "nowhere", "game");

        // Act / Assert
        Assert.That(ModSource.WritableModsFolderOf(missingGame), Is.EqualTo(Path.Combine(_userFolder, "mods")));
        Assert.That(Directory.Exists(Path.Combine(_userFolder, "mods")), Is.True, "made for it");
        Assert.That(ModSource.WritableModsFolderOf(_gameFolder), Is.EqualTo(Path.Combine(_gameFolder, "mods")));
        Assert.That(Directory.Exists(Path.Combine(_gameFolder, "mods")), Is.False, "checked without making it");
    }
}
