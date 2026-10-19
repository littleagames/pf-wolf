using PFWolf.Assets;
using PFWolf.Entities.Actors;
using PFWolf.Gameplay;
using PFWolf.Loaders;
using PFWolf.Ui;

namespace PFWolf.Tests;

/// <summary>An episode's level select (game-info level-select) and actors' text (Gameplay/ActorText.cs)</summary>
public class LevelSelectTests
{
    private const string GameInfo = """
        episodes:
          EP01: { name: "Campaign", start-map: MAP01, level-select: [MAP01, MAP02], level-select-after: campaign-done }
          EP02: { name: "Dog Tag Missions", start-map: DT01, level-select: [DT01, DT02, DT03] }
          EP03: { name: "Challenges", start-map: CH01 }
        maps:
          MAP01: { name: "Shore", select-name: "1 - Shore" }
          MAP02: { name: "City" }
          DT01: { name: "P.O.W.", starts-locked: true, locked-name: "- Locked -" }
          DT02: { name: "Hospital", starts-locked: true }
        """;

    private string _path = null!;
    private GameInfoAsset _info = null!;

    [SetUp]
    public void MakeFile()
    {
        _path = Path.Combine(Path.GetTempPath(), $"pfwolf-progress-{Guid.NewGuid():N}.cfg");
        _info = YamlDataEntryLoader.Deserialize<GameInfoAsset>(GameInfo);
    }

    [TearDown]
    public void RemoveFile()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }

    private Unlocks Make() => new(() => _path, () => _info);

    [Test]
    public void An_Episode_Has_A_Level_Select_Once_Its_After_Name_Is_Unlocked()
    {
        var unlocks = Make();
        Assert.That(unlocks.HasLevelSelect(_info.Episodes["EP01"]), Is.False);
        Assert.That(unlocks.HasLevelSelect(_info.Episodes["EP02"]), Is.True);
        Assert.That(unlocks.HasLevelSelect(_info.Episodes["EP03"]), Is.False);

        unlocks.Unlock("campaign-done");
        Assert.That(unlocks.HasLevelSelect(_info.Episodes["EP01"]), Is.True);
    }

    [Test]
    public void Its_Rows_Are_Its_Levels_The_Locked_Ones_Greyed_Out()
    {
        // Arrange: the second dog tag found; DT03 isn't a map the game has
        var unlocks = Make();
        unlocks.Unlock("DT02");

        // Act
        var rows = NewGameMenu.LevelRows(_info.Episodes["EP02"], _info.Maps, unlocks.IsOpen, text => text);

        // Assert
        Assert.That(rows.Select(r => (r.Active, r.Text, r.Data)), Is.EqualTo(new[]
        {
            ((short)0, "- Locked -", (object)"DT01"),
            ((short)1, "Hospital", (object)"DT02"),
        }));
    }

    [Test]
    public void A_Rows_Name_Is_Its_Select_Name_Else_Its_Name()
    {
        var rows = NewGameMenu.LevelRows(_info.Episodes["EP01"], _info.Maps, Make().IsOpen, text => text);
        Assert.That(rows.Select(r => r.Text), Is.EqualTo(new[] { "1 - Shore", "City" }));
    }

    [Test]
    public void Unlock_All_Covers_Locked_Levels_And_After_Names()
    {
        Assert.That(Make().Lockable(), Is.EquivalentTo(new[] { "campaign-done", "DT01", "DT02" }));
    }

    [Test]
    public void Actor_Text_Fills_In_The_Tag_And_Counts()
    {
        // Arrange: a dog tag numbered 3, with two of nine held
        var inventory = new InventoryManager(ActorDefs.Metadata("""
            DogTag:
              properties:
                "inventory.amount": 1
                "inventory.maxamount": 9
            """));
        var session = new GameSession();
        inventory.SetHolder(session.PlayerState.Items);
        inventory.Give("DogTag", 2);
        var tag = inventory.CreateActor("DogTag")!;
        tag.Tag = 3;

        // Act
        var text = ActorText.Expand("Dog tag #{tag} found! ({left:DogTag} remain, {count:DogTag} held) {other}", tag, inventory);

        // Assert
        Assert.That(text, Is.EqualTo("Dog tag #3 found! (7 remain, 2 held) {other}"));
        Assert.That(ActorText.Expand("DT0{TAG}", tag, null), Is.EqualTo("DT03"));
    }
}
