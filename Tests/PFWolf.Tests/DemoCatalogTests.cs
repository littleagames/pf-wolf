using PFWolf.Assets.Demos;
using PFWolf.Editor.Data;

namespace PFWolf.Tests;

public class DemoCatalogTests
{
    [Test]
    public void A_Dmo_File_Is_Read_As_A_Demo()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"pfwolf-demo-{Guid.NewGuid():N}.dmo");
        File.WriteAllBytes(path, HeadlessGame.ScriptedDemo(4, new HeadlessGame.DemoStep(0, 0, -50, 10)));
        try
        {
            // Act
            var entry = DemoCatalog.Read(path);

            // Assert
            Assert.That(entry, Is.Not.Null);
            Assert.That((entry!.Source, entry.Demo.MapName, entry.Demo.Frames.Count, entry.FilePath), Is.EqualTo((DemoSource.File, "MAP05", 10, path)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void A_Missing_File_Is_No_Demo()
    {
        // Act / Assert
        Assert.That(DemoCatalog.Read(Path.Combine(Path.GetTempPath(), $"no-such-{Guid.NewGuid():N}.dmo")), Is.Null);
    }

    [Test]
    public void A_Recorded_Demo_Replaces_The_Games_Own_Of_Its_Number()
    {
        // Arrange
        var demo = DemoFile.Parse(HeadlessGame.ScriptedDemo(0, new HeadlessGame.DemoStep(0, 0, 0, 1)));
        var game0 = new DemoEntry("DEMO0", DemoSource.Game, demo, 0, null);
        var game1 = new DemoEntry("DEMO1", DemoSource.Game, demo, 1, null);
        var recorded1 = new DemoEntry("DEMO1.dmo", DemoSource.Recorded, demo, 1, "DEMO1.dmo");
        var all = new[] { game0, game1, recorded1 };

        // Act / Assert
        Assert.That(DemoCatalog.IsReplaced(game0, all), Is.False);
        Assert.That(DemoCatalog.IsReplaced(game1, all), Is.True);
        Assert.That(DemoCatalog.IsReplaced(recorded1, all), Is.False);
    }
}
