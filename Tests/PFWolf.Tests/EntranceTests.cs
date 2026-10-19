using PFWolf.Assets;

namespace PFWolf.Tests;

/// <summary>Levels entered by an entrance (game-info next-entrance, entrance-briefings; GameState.Entrance)</summary>
public class EntranceTests
{
    [Test]
    public void A_Saved_Game_Keeps_The_Entrance_The_Level_Was_Entered_By()
    {
        // Arrange
        var game = new GameState { MapOn = "CAMP09", Entrance = "labs" };
        using var stream = new MemoryStream();
        using (var bw = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            game.Write(bw, new Entities.PlayerState { PlayerClass = "Player" });
        stream.Position = 0;

        // Act
        using var br = new BinaryReader(stream);
        var (read, _) = GameState.Read(br);

        // Assert
        Assert.That((read.MapOn, read.Entrance), Is.EqualTo(("CAMP09", "labs")));
    }

    [TestCase("labs", "BRIEF_CAMP09_LABS")]
    [TestCase("LABS", "BRIEF_CAMP09_LABS")]     // any case
    [TestCase("", "BRIEF_CAMP09")]
    [TestCase(null, "BRIEF_CAMP09")]
    [TestCase("roof", "BRIEF_CAMP09")]          // an entrance without a briefing of its own
    public void An_Entrance_Can_Have_A_Briefing_Of_Its_Own(string? entrance, string expected)
    {
        var map = PFWolf.Loaders.YamlDataEntryLoader.Deserialize<MapInfo>(
            "briefing: BRIEF_CAMP09\nentrance-briefings:\n  labs: BRIEF_CAMP09_LABS\n");
        Assert.That(MainGameLoop.BriefingFor(map, entrance), Is.EqualTo(expected));
    }

    [Test]
    public void A_Map_Reads_The_Entrances_Its_Next_Levels_Are_Entered_By()
    {
        var map = PFWolf.Loaders.YamlDataEntryLoader.Deserialize<MapInfo>("next: CAMP09\nnext-entrance: labs\nsecret-next-entrance: roof\n");
        Assert.That((map.NextEntrance, map.SecretNextEntrance), Is.EqualTo(("labs", "roof")));
    }
}
