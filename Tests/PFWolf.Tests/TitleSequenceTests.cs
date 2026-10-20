using PFWolf.Assets;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>game-info's title-sequence: how it reads, and what a key does on each screen</summary>
public class TitleSequenceTests
{
    private static GameInfoAsset Parse(string text) => YamlDataEntryLoader.Deserialize<GameInfoAsset>(text);

    private static GameInfoAsset ReadFile(string path)
    {
        using var stream = File.OpenRead(path);
        return YamlDataEntryLoader.Read<GameInfoAsset>(stream);
    }

    private static string PackFile(string pack) => File.ReadAllText(Path.Combine(TestPaths.Pk3SourceFolder(), "gamepacks", pack, "game-info.yaml"));

    /// <summary>A pack's game-info laid over its base packs', as the loader merges them</summary>
    private static GameInfoAsset ReadPack(params string[] packsFromBase)
    {
        var merged = YamlTree.Parse(PackFile(packsFromBase[0]))!;
        foreach (var pack in packsFromBase.Skip(1))
            YamlTree.DeepMerge(merged, YamlTree.Parse(PackFile(pack))!);
        return YamlDataEntryLoader.Deserialize<GameInfoAsset>(YamlTree.ToText(merged));
    }

    // Only KeyActionFor is used, which needs none of the managers
    private static TitleScreens MakeScreens() => new(null!, null!, null!, null!, null!, null!, null!, null!, null!);

    [Test]
    public void The_Intro_And_Loop_Are_Read_From_Title_Sequence()
    {
        // Act
        var info = Parse("""
            title-sequence:
              intro:
                - pic: LOGO
                  on-key: end-intro
              loop:
                - title: true
                  seconds: 8
                - high-scores: true
            """);

        // Assert
        Assert.That(info.TitleSequence.Intro.Select(screen => (screen.Pic, screen.OnKey)), Is.EqualTo(new[] { ("LOGO", "end-intro") }));
        Assert.That(info.TitleSequence.Loop.Select(screen => (screen.Title, screen.HighScores, screen.Seconds)),
            Is.EqualTo(new[] { (true, false, 8.0), (false, true, 0.0) }));
        Assert.That((info.OldIntro, info.OldTitleLoop), Is.EqualTo(((object?)null, (object?)null)));
    }

    [Test]
    public void The_Old_Top_Level_Lists_Are_Noticed_But_Not_Used()
    {
        // Act
        var info = Parse("""
            intro:
              - pic: LOGO
            title-loop:
              - title: true
            """);

        // Assert
        Assert.That(info.OldIntro, Is.Not.Null);
        Assert.That(info.OldTitleLoop, Is.Not.Null);
        Assert.That(info.TitleSequence.Intro, Is.Empty);
        Assert.That(info.TitleSequence.Loop, Is.Empty);
    }

    [TestCase("next", TitleKeyAction.Next)]
    [TestCase("menu", TitleKeyAction.Menu)]
    [TestCase("end-intro", TitleKeyAction.EndIntro)]
    [TestCase("Finish", TitleKeyAction.Finish)]
    [TestCase(" ignore ", TitleKeyAction.Ignore)]
    public void On_Key_Names_Parse(string name, TitleKeyAction expected)
    {
        Assert.That(TitleKeyActions.TryParse(name, out var action), Is.True);
        Assert.That(action, Is.EqualTo(expected));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("goto")]
    [TestCase("2")]
    public void Unset_Or_Unknown_On_Keys_Do_Not_Parse(string? name)
    {
        Assert.That(TitleKeyActions.TryParse(name, out _), Is.False);
    }

    [TestCase(null, TitleKeyAction.Next, TitleKeyAction.Next)]
    [TestCase(null, TitleKeyAction.Menu, TitleKeyAction.Menu)]
    [TestCase("finish", TitleKeyAction.Menu, TitleKeyAction.Finish)]
    [TestCase("ignore", TitleKeyAction.Next, TitleKeyAction.Ignore)]
    [TestCase("end-intro", TitleKeyAction.Next, TitleKeyAction.EndIntro)]
    [TestCase("next", TitleKeyAction.Menu, TitleKeyAction.Next)]
    public void A_Screens_On_Key_Wins_Over_The_Lists_Default(string? onKey, TitleKeyAction listDefault, TitleKeyAction expected)
    {
        Assert.That(MakeScreens().KeyActionFor(new TitleScreenInfo { OnKey = onKey }, listDefault), Is.EqualTo(expected));
    }

    [Test]
    public void An_Unknown_On_Key_Does_The_Lists_Default()
    {
        Assert.That(MakeScreens().KeyActionFor(new TitleScreenInfo { OnKey = "explode" }, TitleKeyAction.Next), Is.EqualTo(TitleKeyAction.Next));
    }

    [Test]
    public void End_Intro_In_The_Loop_Goes_To_The_Menu()
    {
        Assert.That(MakeScreens().KeyActionFor(new TitleScreenInfo { OnKey = "end-intro" }, TitleKeyAction.Menu), Is.EqualTo(TitleKeyAction.Menu));
    }

    [TestCase("blake")]
    [TestCase("blake", "planetstrike")]
    public void Blake_Stone_Packs_Have_An_Intro_And_A_Loop(params string[] packsFromBase)
    {
        // Act
        var info = ReadPack(packsFromBase);

        // Assert
        Assert.That(info.TitleSequence.Intro, Is.Not.Empty);
        Assert.That(info.TitleSequence.Loop, Is.Not.Empty);
        Assert.That(info.TitleSequence.Intro.Concat(info.TitleSequence.Loop)
            .Where(screen => screen.OnKey != null && !TitleKeyActions.TryParse(screen.OnKey, out _)), Is.Empty);
    }

    [Test]
    public void Planet_Strikes_Title_Finishes_Its_Fizzle_On_A_Key()
    {
        // Act
        var info = ReadPack("blake", "planetstrike");

        // Assert: Planet Strike's lists replace Aliens of Gold's whole, inside title-sequence too
        var title = info.TitleSequence.Loop[0];
        Assert.That((title.FizzlePic, title.OnKey), Is.EqualTo(("TITLE2", "finish")));
        Assert.That(info.TitleSequence.Intro.Select(screen => screen.Pic ?? screen.Movie),
            Is.EqualTo(new[] { "ApogeeLogo", "IntroMovie", "ParentalWarning" }));
    }

    [Test]
    public void The_Standalone_Demo_Has_A_Title_Loop()
    {
        // Act
        var info = ReadFile(Path.Combine(TestPaths.RepoRoot(), "examples", "mods", "standalone-demo", "game-info.yaml"));

        // Assert
        Assert.That(info.TitleSequence.Loop, Has.Count.EqualTo(2));
    }
}
