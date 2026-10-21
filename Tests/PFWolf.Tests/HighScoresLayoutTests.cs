using PFWolf.Assets;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>game-info high-scores: the pictures heading the screen</summary>
public class HighScoresLayoutTests
{
    private static string PackFile(string pack) => File.ReadAllText(Path.Combine(TestPaths.Pk3SourceFolder(), "gamepacks", pack, "game-info.yaml"));

    /// <summary>A pack's game-info laid over its base packs', as the loader merges them</summary>
    private static GameInfoAsset ReadPack(params string[] packsFromBase)
    {
        var merged = YamlTree.Parse(PackFile(packsFromBase[0]))!;
        foreach (var pack in packsFromBase.Skip(1))
            YamlTree.DeepMerge(merged, YamlTree.Parse(PackFile(pack))!);
        return YamlDataEntryLoader.Deserialize<GameInfoAsset>(YamlTree.ToText(merged));
    }

    [Test]
    public void Left_Out_The_Pics_Are_Wolf3Ds_Title_And_Column_Headings()
    {
        // Act
        var pics = new HighScoresInfo().Pics;

        // Assert
        Assert.That(pics.Select(pic => (pic.Pic, pic.X, pic.Y)),
            Is.EqualTo(new[] { ("HighScores", 48, 0), ("C_Name", 32, 68), ("C_Level", 160, 68), ("C_Score", 224, 68) }));
    }

    [Test]
    public void Spears_Title_Picture_Carries_Its_Headings()
    {
        // Act
        var pics = ReadPack("wolf3d", "spear").HighScores.Pics;

        // Assert
        Assert.That(pics.Select(pic => (pic.Pic, pic.X, pic.Y)), Is.EqualTo(new[] { ("HighScores", 0, 0) }));
    }

    [Test]
    public void Blake_Stones_Screen_Is_Headed_By_Labels_Alone()
    {
        // Act
        var layout = ReadPack("blake").HighScores;

        // Assert
        Assert.That(layout.Pics, Is.Empty);
        Assert.That(layout.Labels, Has.Count.EqualTo(4));
    }
}
