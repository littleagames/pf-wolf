using PFWolf.Configuration;
using PFWolf.Gameplay;
using PFWolf.Loaders;

namespace PFWolf.Tests;

public class ModScoresTests
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

    private ModSource Mod(string folderName, string? id = null)
    {
        var path = Path.Combine(_folder, folderName);
        Directory.CreateDirectory(path);
        if (id != null)
            File.WriteAllText(Path.Combine(path, "modinfo.yaml"), $"id: {id}\n");
        return ModSource.TryOpen(path, [])!;
    }

    [Test]
    public void KeyFor_Is_The_Sorted_Lower_Case_Ids_And_Empty_Without_Mods()
    {
        // Arrange
        var lights = Mod("lights-folder", "Moody-Lights");
        var cars = Mod("cars");

        // Act / Assert
        Assert.That(ModScores.KeyFor([], null), Is.Empty);
        Assert.That(ModScores.KeyFor([lights, cars], null), Is.EqualTo("cars+moody-lights"));
        Assert.That(ModScores.KeyFor([cars, lights], null), Is.EqualTo("cars+moody-lights"), "the load order doesn't matter");
    }

    [Test]
    public void KeyFor_Leaves_Out_The_Mod_A_Standalone_Game_Comes_From()
    {
        // Arrange
        var game = Mod("my-game");
        var extra = Mod("extra");

        // Act / Assert
        Assert.That(ModScores.KeyFor([game], game.FullPath + Path.DirectorySeparatorChar), Is.Empty);
        Assert.That(ModScores.KeyFor([game, extra], game.FullPath), Is.EqualTo("extra"));
    }

    [Test]
    public void Write_Then_Read_Round_Trips_Each_Table()
    {
        // Arrange
        var path = Path.Combine(_folder, ModScores.FileName);
        var tables = new Dictionary<string, HighScore[]>
        {
            ["cars"] = [new HighScore { Name = "Driver", Score = 1234, Completed = 3, Episode = 1, Ratio = 87 }],
            ["cars+moody-lights"] = [new HighScore { Name = "A" }, new HighScore { Name = "B", Score = 5 }],
        };

        // Act
        using (var stream = File.Create(path))
            ModScores.Write(stream, tables);
        var read = ModScores.Read(path);

        // Assert
        Assert.That(read.Keys, Is.EquivalentTo(tables.Keys));
        var driver = read["cars"].Single();
        Assert.That((driver.Name, driver.Score, driver.Completed, driver.Episode, driver.Ratio), Is.EqualTo(("Driver", 1234, (ushort)3, (ushort)1, (ushort)87)));
        Assert.That(read["cars+moody-lights"].Select(s => s.Name), Is.EqualTo(new[] { "A", "B" }));
    }

    [Test]
    public void Read_Missing_Or_Garbage_Is_No_Tables()
    {
        // Arrange
        var garbage = Path.Combine(_folder, "garbage.dat");
        File.WriteAllBytes(garbage, [1, 2, 3]);

        // Act / Assert
        Assert.That(ModScores.Read(Path.Combine(_folder, "missing.dat")), Is.Empty);
        Assert.That(ModScores.Read(garbage), Is.Empty);
    }
}
