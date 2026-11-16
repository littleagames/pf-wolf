using System.Text;
using PFWolf.Assets;
using PFWolf.Editor.Data;

namespace PFWolf.Tests;

/// <summary>
/// Aliens of Gold's releases: v3.0 (blake), v1.0 (blake-v1) and v2.1 (blake-v2). Their VGAGRAPHs
/// have 225, 212 and 223 chunks, so each its own graphics list, which must name every chunk as
/// blake's names the same picture or text. Their files have the same names, so only one release
/// sits in a folder: the built game folder's, or PFWOLF_GAMEDATA's (the others' tests are then
/// ignored).
/// </summary>
[Category("GameData")]
public class BlakeReleasesGameTests
{
    private static GameContent Load(string game, string release)
    {
        var gameBin = Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64");
        var folder = new[] { Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA"), Path.Combine(gameBin, "Debug", "net10.0"), Path.Combine(gameBin, "Release", "net10.0") }
            .FirstOrDefault(folder => !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, "pfwolf.pk3")));
        if (folder == null)
            Assert.Ignore("No built game folder with pfwolf.pk3; build PFWolf for x64 or set PFWOLF_GAMEDATA");

        var workingFolder = Directory.GetCurrentDirectory();
        try
        {
            // Asked for with another release's files, the game plays those (GameTypes.PickGameType)
            var content = GameContent.Load(folder!, game, []);
            if (!content.Game.ReleaseId.Equals(release, StringComparison.OrdinalIgnoreCase))
                Assert.Ignore($"The data files in {folder} are {content.Game.ReleaseId}'s");
            return content;
        }
        catch (PFWolf.Exceptions.DataFilesException)
        {
            Assert.Ignore($"{game}'s data files aren't in {folder}");
            throw;
        }
        finally
        {
            Directory.SetCurrentDirectory(workingFolder);
        }
    }

    private static int Pixels(GameContent content, string name)
    {
        var graphic = content.Find<GraphicAsset>(name);
        Assert.That(graphic, Is.Not.Null, name);
        return graphic!.Width * graphic.Height;
    }

    private static string Text(GameContent content, string name)
    {
        var text = content.Find<TextAsset>(name);
        Assert.That(text, Is.Not.Null, name);
        return Encoding.ASCII.GetString(text!.RawData);
    }

    [TestCase("blake", "blake-aog")]
    [TestCase("blake-v1", "blake-v1")]
    [TestCase("blake-v2", "blake-v2")]
    public void The_Graphics_Lists_Name_Each_Chunk_As_Blake_Does(string game, string release)
    {
        // Arrange / Act
        var content = Load(game, release);

        // Assert: pictures from the start, the middle and past Tile8, and texts to the very end
        Assert.That(Pixels(content, "Title"), Is.EqualTo(320 * 200));
        Assert.That(Pixels(content, "StatusBar"), Is.EqualTo(15360));
        Assert.That(Text(content, "FloorNames"), Does.Contain("STABILIZATION").Or.Contain("STABALIZATION"));
        Assert.That(Text(content, "Transmission"), Is.Not.Empty);
        if (release == "blake-v1")
        {
            Assert.That(content.Find<GraphicAsset>("H_L"), Is.Null);
            Assert.That(content.Find<TextAsset>("BADSAVE"), Is.Null);
            return;
        }
        Assert.That(Pixels(content, "H_L"), Is.EqualTo(384));
        Assert.That(Pixels(content, "H_Shift"), Is.EqualTo(640));
        Assert.That(Text(content, "BADSAVE"), Does.StartWith("The selected saved game"));
        if (release == "blake-aog")
            Assert.That(Pixels(content, "PSPromo1"), Is.EqualTo(51072));
        else
            Assert.That(content.Find<GraphicAsset>("PSPromo1"), Is.Null);
    }
}
