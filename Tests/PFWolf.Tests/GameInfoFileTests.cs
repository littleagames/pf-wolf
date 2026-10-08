using PFWolf.Assets;
using PFWolf.Editor.Editing;
using PFWolf.Loaders;

namespace PFWolf.Tests;

public class GameInfoFileTests
{
    private const string SwitchDemo =
        "# Merged into the game pack's game-info.yaml\n" +
        "maps:\n" +
        "  MAP01:\n" +
        "    zones:\n" +
        "      # The light room\n" +
        "      1: { light: 24 }\n" +
        "      2: { light: 255, low: 90 }\n" +
        "  # The second floor\n" +
        "  MAP02:\n" +
        "    music: SEARCHN\n" +
        "# trailing comment\n";

    private static GameInfoAsset Parse(string text) => YamlDataEntryLoader.Deserialize<GameInfoAsset>(text);

    [Test]
    public void A_New_File_Gets_Maps_And_The_Level()
    {
        // Act
        var text = GameInfoFile.Apply(null, "MAP61", new MapProperties { Name = "The Vault", Next = "MAP02" }, ["name", "next"]);

        // Assert
        Assert.That(text, Is.EqualTo("maps:\n  MAP61:\n    name: \"The Vault\"\n    next: \"MAP02\"\n"));
        Assert.That(Parse(text).Maps["MAP61"].Name, Is.EqualTo("The Vault"));
    }

    [Test]
    public void Changing_A_Level_Keeps_Its_Other_Keys_And_Every_Comment_Elsewhere()
    {
        // Act
        var text = GameInfoFile.Apply(SwitchDemo, "MAP01", new MapProperties { Music = "GETTHEM" }, ["music"]);

        // Assert
        Assert.That(text, Does.StartWith("# Merged into the game pack's game-info.yaml\nmaps:\n  MAP01:\n    zones:\n      # The light room\n      1: { light: 24 }\n"));
        Assert.That(text, Does.Contain("    music: \"GETTHEM\"\n  # The second floor\n  MAP02:\n    music: SEARCHN\n# trailing comment\n"));
        var parsed = Parse(text);
        Assert.That(parsed.Maps["MAP01"].Music, Is.EqualTo("GETTHEM"));
        Assert.That(parsed.Maps["MAP01"].Zones![1].Light, Is.EqualTo(24));
        Assert.That(parsed.Maps["MAP02"].Music, Is.EqualTo("SEARCHN"));
    }

    [Test]
    public void A_Changed_Key_Is_Replaced_And_A_Cleared_One_Taken_Out()
    {
        // Arrange
        var properties = new MapProperties { Zones = [new ZoneProperties(3, Light: 100, Color: "#FF0000")] };

        // Act: zones changed, music cleared on MAP02
        var text = GameInfoFile.Apply(SwitchDemo, "MAP01", properties, ["zones"]);
        text = GameInfoFile.Apply(text, "MAP02", new MapProperties(), ["music"]);

        // Assert
        var parsed = Parse(text);
        Assert.That(parsed.Maps["MAP01"].Zones!.Keys, Is.EqualTo(new[] { 3 }));
        Assert.That(parsed.Maps["MAP01"].Zones![3].Color, Is.EqualTo("#FF0000"));
        Assert.That(parsed.Maps.ContainsKey("MAP02"), Is.False, "a level left with nothing is taken out, not left as an empty MAP02:");
        Assert.That(text, Does.Contain("# trailing comment"));
    }

    [Test]
    public void Writing_Nothing_For_A_Level_The_File_Lacks_Changes_Nothing()
    {
        // Act
        var text = GameInfoFile.Apply(SwitchDemo, "MAP61", new MapProperties(), ["music"]);

        // Assert
        Assert.That(text, Is.EqualTo(SwitchDemo));
    }

    [Test]
    public void A_Level_The_File_Lacks_Goes_After_The_Last_One()
    {
        // Act
        var text = GameInfoFile.Apply(SwitchDemo, "MAP61", new MapProperties { Name = "New" }, ["name"]);

        // Assert
        Assert.That(text, Does.Contain("  MAP02:\n    music: SEARCHN\n  MAP61:\n    name: \"New\"\n# trailing comment\n"));
        Assert.That(Parse(text).Maps.Keys, Is.EquivalentTo(new[] { "MAP01", "MAP02", "MAP61" }));
    }

    [Test]
    public void A_File_Without_Maps_Gets_Them_At_The_End()
    {
        // Arrange
        const string colors = "default-map:\n  floor-color: \"#000000\"\n";

        // Act
        var text = GameInfoFile.Apply(colors, "MAP01", new MapProperties { WallHeight = 3 }, ["wall-height"]);

        // Assert
        Assert.That(text, Is.EqualTo("default-map:\n  floor-color: \"#000000\"\nmaps:\n  MAP01:\n    wall-height: 3\n"));
    }

    [Test]
    public void Shading_Is_Written_As_A_Block_Of_What_Is_Set()
    {
        // Arrange
        var properties = new MapProperties { FadeColor = "#808080", FadeEnd = 12.5, Light = 200 };

        // Act
        var text = GameInfoFile.Apply(null, "MAP01", properties, ["shading"]);

        // Assert
        var shading = Parse(text).Maps["MAP01"].Shading!;
        Assert.That(shading.FadeColor, Is.EqualTo("#808080"));
        Assert.That(shading.FadeEnd, Is.EqualTo(12.5));
        Assert.That(shading.Light, Is.EqualTo(200));
        Assert.That(shading.FadeStart, Is.Null);
    }

    [Test]
    public void A_Flow_Style_Level_Is_Rewritten_As_A_Block()
    {
        // Arrange
        const string flow = "maps:\n  MAP01: { next: MAP02, cluster: 1 }\n  MAP02: { next: MAP03 }\n";

        // Act
        var text = GameInfoFile.Apply(flow, "MAP01", new MapProperties { Name = "Start" }, ["name"]);

        // Assert
        var parsed = Parse(text);
        Assert.That(parsed.Maps["MAP01"].Name, Is.EqualTo("Start"));
        Assert.That(parsed.Maps["MAP01"].Next, Is.EqualTo("MAP02"));
        Assert.That(parsed.Maps["MAP01"].Cluster, Is.EqualTo(1));
        Assert.That(parsed.Maps["MAP02"].Next, Is.EqualTo("MAP03"));
    }

    [Test]
    public void Windows_Line_Ends_Are_Kept()
    {
        // Arrange
        var crlf = SwitchDemo.Replace("\n", "\r\n");

        // Act
        var text = GameInfoFile.Apply(crlf, "MAP01", new MapProperties { Name = "X" }, ["name"]);

        // Assert
        Assert.That(text.Replace("\r\n", ""), Does.Not.Contain("\n"));
        Assert.That(Parse(text).Maps["MAP01"].Name, Is.EqualTo("X"));
    }

    [Test]
    public void Quotes_In_Values_Survive()
    {
        // Act
        var text = GameInfoFile.Apply(null, "MAP01", new MapProperties { Name = "The \"Big\" One \\ two" }, ["name"]);

        // Assert
        Assert.That(Parse(text).Maps["MAP01"].Name, Is.EqualTo("The \"Big\" One \\ two"));
    }

    [Test]
    public void ChangedKeys_Lists_Only_What_Differs()
    {
        // Arrange
        var before = new MapProperties { Name = "A", Zones = [new ZoneProperties(1, 24)] };
        var after = before with { Music = "GETTHEM", Zones = [new ZoneProperties(1, 24)] };

        // Act
        var keys = after.ChangedKeys(before);

        // Assert
        Assert.That(keys, Is.EqualTo(new[] { "music" }));
    }
}
