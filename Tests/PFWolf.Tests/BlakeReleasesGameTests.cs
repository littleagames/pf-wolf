using System.Text;
using PFWolf.Assets;
using PFWolf.Editor.Data;

namespace PFWolf.Tests;

/// <summary>
/// Aliens of Gold v2.1 and v3.0 (blake-v2, blake-v3): their VGAGRAPHs have more chunks than
/// v1.0's, so their own graphics lists, which must name each chunk as blake's names the same
/// picture or text. Their files have v1.0's names, so they can't sit in the built game folder
/// next to v1.0's: point PFWOLF_GAMEDATA at a folder with one release's files and a pfwolf.pk3
/// (the other release's tests are then ignored).
/// </summary>
[Category("GameData")]
public class BlakeReleasesGameTests
{
    private static GameContent Load(string game)
    {
        var folder = Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA");
        if (string.IsNullOrWhiteSpace(folder) || !File.Exists(Path.Combine(folder, "pfwolf.pk3")))
            Assert.Ignore("Set PFWOLF_GAMEDATA to a folder with pfwolf.pk3 and Aliens of Gold v2.1's or v3.0's files");

        var workingFolder = Directory.GetCurrentDirectory();
        try
        {
            // Asked for with the other release's files, the game plays those (GameTypes.PickGameType)
            var content = GameContent.Load(folder!, game, []);
            if (!content.Game.ReleaseId.Equals(game, StringComparison.OrdinalIgnoreCase))
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

    [TestCase("blake-v2")]
    [TestCase("blake-v3")]
    public void The_Graphics_Lists_Name_Each_Chunk_As_Blake_Does(string game)
    {
        // Arrange / Act
        var content = Load(game);

        // Assert: pictures from the start, the middle and past Tile8, and texts to the very end
        Assert.That(Pixels(content, "H_L"), Is.EqualTo(384));
        Assert.That(Pixels(content, "H_Shift"), Is.EqualTo(640));
        Assert.That(Pixels(content, "Title"), Is.EqualTo(320 * 200));
        Assert.That(Pixels(content, "StatusBar"), Is.EqualTo(15360));
        Assert.That(Text(content, "FloorNames"), Does.Contain("STABILIZATION").Or.Contain("STABALIZATION"));
        Assert.That(Text(content, "Transmission"), Is.Not.Empty);
        Assert.That(Text(content, "BADSAVE"), Does.StartWith("The selected saved game"));
        if (game == "blake-v3")
            Assert.That(Pixels(content, "PSPromo1"), Is.EqualTo(51072));
    }
}
