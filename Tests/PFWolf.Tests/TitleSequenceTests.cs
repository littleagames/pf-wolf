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

    /// <summary>A screen's first picture, if it has any</summary>
    private static string? FirstPic(TitleScreenInfo screen) => screen.Pics.FirstOrDefault()?.Pic;

    [Test]
    public void The_Intro_And_Loop_Are_Read_From_Title_Sequence()
    {
        // Act
        var info = Parse("""
            title-sequence:
              intro:
                - pics: [{ pic: LOGO }]
                  on-key: end-intro
              loop:
                - pics: [{ pic: TOP }, { pic: BOTTOM, y: 80 }]
                  palette: TITLEPAL
                  seconds: 8
                - high-scores: true
            """);

        // Assert
        Assert.That(info.TitleSequence.Intro.Select(screen => (FirstPic(screen), screen.OnKey)), Is.EqualTo(new[] { ("LOGO", "end-intro") }));
        Assert.That(info.TitleSequence.Loop[0].Pics.Select(pic => (pic.Pic, pic.X, pic.Y)), Is.EqualTo(new[] { ("TOP", 0, 0), ("BOTTOM", 0, 80) }));
        Assert.That(info.TitleSequence.Loop.Select(screen => (screen.Palette, screen.HighScores, screen.Seconds)),
            Is.EqualTo(new[] { ("TITLEPAL", false, 8.0), (null, true, 0.0) }));
    }

    [Test]
    public void A_Screen_Is_Left_Out_Only_When_The_Game_Has_None_Of_Its_Pics()
    {
        // Arrange
        var has = new HashSet<string> { "credits" };
        TitleScreenInfo Screen(params string[] pics) => new() { Pics = [.. pics.Select(pic => new PicPlacement { Pic = pic })] };

        // Act / Assert
        Assert.That(TitleScreens.CanShowPics(Screen(), has.Contains), Is.True);
        Assert.That(TitleScreens.CanShowPics(Screen("pg13"), has.Contains), Is.False);
        Assert.That(TitleScreens.CanShowPics(Screen("pg13", "credits"), has.Contains), Is.True);
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
        Assert.That(info.TitleSequence.Intro.Select(screen => FirstPic(screen) ?? screen.Movie),
            Is.EqualTo(new[] { "ApogeeLogo", "IntroMovie", "ParentalWarning" }));
    }

    [Test]
    public void Wolf3Ds_Intro_Is_The_Notice_Then_The_Rating_And_Its_Loop_Ends_In_A_Demo()
    {
        // Act
        var sequence = ReadPack("wolf3d").TitleSequence;

        // Assert
        var notice = sequence.Intro[0];
        Assert.That((notice.MenuBackground, notice.Stripe, notice.WaitForKey), Is.EqualTo((true, (int?)10, true)));
        Assert.That(notice.Lines.Select(line => (line.X, line.Y, line.Color)), Is.EqualTo(new[] { (110, 15, "READHCOLOR"), (40, 60, "HIGHLIGHT") }));
        Assert.That(notice.Lines[1].Text, Does.StartWith("This game is NOT shareware.\nPlease do not distribute it.\n"));
        var rating = sequence.Intro[1];
        Assert.That((rating.Pics.Single(), rating.Background, rating.Music), Is.EqualTo((new PicPlacement { Pic = "pg13", X = 216, Y = 110 }, "Light Blue", "NAZI_NOR")));
        Assert.That(sequence.Loop.Select(screen => (FirstPic(screen), screen.HighScores, screen.Demo, screen.Seconds)),
            Is.EqualTo(new[] { ("Title", false, false, 15.0), ("credits", false, false, 10.0), (null, true, false, 10.0), (null, false, true, 0.0) }));
    }

    [TestCase("wolf3d-shareware", "NAZI_NOR")]
    [TestCase("spear", "XTOWER2")]
    public void Packs_That_Were_Never_Sold_As_Registered_Show_Only_The_Rating(string pack, string music)
    {
        // Act
        var sequence = ReadPack("wolf3d", pack).TitleSequence;

        // Assert
        Assert.That(sequence.Intro.Select(screen => (FirstPic(screen), screen.Music)), Is.EqualTo(new[] { ("pg13", music) }));
    }

    [Test]
    public void The_Shareware_Keeps_Wolf3Ds_Loop()
    {
        // Act
        var loop = ReadPack("wolf3d", "wolf3d-shareware").TitleSequence.Loop;

        // Assert
        Assert.That(loop, Is.EqualTo(ReadPack("wolf3d").TitleSequence.Loop)
            .Using<TitleScreenInfo>((a, b) => a.Pics.SequenceEqual(b.Pics) && a.HighScores == b.HighScores && a.Demo == b.Demo));
    }

    [Test]
    public void Spears_Loop_Is_Wolf3Ds_With_Its_Two_Half_Title_In_Its_Own_Palette()
    {
        // Act
        var loop = ReadPack("wolf3d", "spear").TitleSequence.Loop;
        var wolf3d = ReadPack("wolf3d").TitleSequence.Loop;

        // Assert
        Assert.That(loop[0].Pics.Select(pic => (pic.Pic, pic.Y)), Is.EqualTo(new[] { ("Title1", 0), ("Title2", 80) }));
        Assert.That(loop[0].Palette, Is.EqualTo("TITLEPAL"));
        Assert.That(loop.Skip(1), Is.EqualTo(wolf3d.Skip(1))
            .Using<TitleScreenInfo>((a, b) => a.Pics.SequenceEqual(b.Pics) && a.HighScores == b.HighScores && a.Demo == b.Demo && a.Seconds == b.Seconds));
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
