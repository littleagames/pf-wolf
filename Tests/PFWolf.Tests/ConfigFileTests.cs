using PFWolf.Configuration;
using PFWolf.Loaders;

namespace PFWolf.Tests;

public class ConfigFileTests
{
    private string _folder = "";

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "pfwolf-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    private string PathOf(string name) => Path.Combine(_folder, name);

    [Test]
    public void ModsConfig_Missing_File_Is_No_Mods()
    {
        Assert.That(ModsConfig.Read(PathOf("missing.cfg")), Is.Empty);
    }

    [Test]
    public void ModsConfig_Write_Then_Read_Round_Trips_In_Order()
    {
        // Arrange
        var path = PathOf(ModsConfig.FileName);
        string[] mods = ["switch-demo", @"C:\elsewhere\big mod.pk3", "lights.pk3"];

        // Act
        var written = ModsConfig.Write(path, mods);
        var read = ModsConfig.Read(path);

        // Assert
        Assert.That(written, Is.True);
        Assert.That(read, Is.EqualTo(mods));
    }

    [Test]
    public void ModsConfig_Read_Skips_Blanks_And_Comments_And_Trims()
    {
        // Arrange
        var path = PathOf(ModsConfig.FileName);
        File.WriteAllLines(path, ["# comment", "", "  first.pk3  ", "   ", "#second.pk3", "third"]);

        // Act
        var read = ModsConfig.Read(path);

        // Assert
        Assert.That(read, Is.EqualTo(new[] { "first.pk3", "third" }));
    }

    [Test]
    public void ModsConfig_Write_Fails_Quietly_When_The_Folder_Is_Missing()
    {
        Assert.That(ModsConfig.Write(Path.Combine(_folder, "nope", "mods.cfg"), ["a"]), Is.False);
    }

    [Test]
    public void ModsConfig_ConfigName_Is_The_Name_In_The_Mods_Folder_Else_The_Full_Path()
    {
        // Arrange
        var inModsFolder = Path.Combine(ModSource.ModsFolder, "mine.pk3");
        var elsewhere = PathOf("other.pk3");

        // Act / Assert
        Assert.That(ModsConfig.ConfigName(inModsFolder), Is.EqualTo("mine.pk3"));
        Assert.That(ModsConfig.ConfigName(Path.Combine(ModSource.ModsFolder, "folder-mod") + Path.DirectorySeparatorChar), Is.EqualTo("folder-mod"));
        Assert.That(ModsConfig.ConfigName(elsewhere), Is.EqualTo(elsewhere));
    }

    [Test]
    public void MultiplayerConfig_Missing_File_Has_Defaults()
    {
        // Act
        var config = MultiplayerConfig.Read(PathOf("missing.cfg"));

        // Assert
        Assert.That(config.Name, Is.EqualTo(Environment.UserName));
        Assert.That(config.LastAddress, Is.Empty);
        Assert.That(config.OpenPort, Is.True);
    }

    [Test]
    public void MultiplayerConfig_Write_Then_Read_Round_Trips()
    {
        // Arrange
        var path = PathOf(MultiplayerConfig.FileName);
        var config = new MultiplayerConfig { Name = "Some Player", LastAddress = "192.168.1.5:10700", OpenPort = false };

        // Act
        config.Write(path);
        var read = MultiplayerConfig.Read(path);

        // Assert
        Assert.That((read.Name, read.LastAddress, read.OpenPort), Is.EqualTo(("Some Player", "192.168.1.5:10700", false)));
    }

    [Test]
    public void MultiplayerConfig_Keeps_Unknown_Lines()
    {
        // Arrange
        var path = PathOf(MultiplayerConfig.FileName);
        File.WriteAllLines(path, ["# old comment", "NAME Zed", "future-setting 42", "colour blue"]);

        // Act
        var config = MultiplayerConfig.Read(path);
        config.Write(path);
        var lines = File.ReadAllLines(path);

        // Assert
        Assert.That(config.Name, Is.EqualTo("Zed"));
        Assert.That(lines, Does.Contain("future-setting 42"));
        Assert.That(lines, Does.Contain("colour blue"));
    }

    [TestCase("false", false)]
    [TestCase("0", false)]
    [TestCase("off", false)]
    [TestCase("true", true)]
    [TestCase("yes", true)]
    [TestCase("", true)]
    public void MultiplayerConfig_Open_Port_Is_On_Unless_Turned_Off(string value, bool expected)
    {
        // Arrange
        var path = PathOf(MultiplayerConfig.FileName);
        File.WriteAllLines(path, [$"open-port {value}"]);

        // Act / Assert
        Assert.That(MultiplayerConfig.Read(path).OpenPort, Is.EqualTo(expected));
    }
}
