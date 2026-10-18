using Microsoft.Extensions.DependencyInjection;
using PFWolf.Assets;

namespace PFWolf.Tests;

public class StartupArgsTests
{
    [Test]
    public void No_Arguments_Are_The_Defaults()
    {
        // Act
        var (gameParams, modPaths) = StartupArgs.Parse([], TextWriter.Null);

        // Assert
        Assert.That(gameParams.NoWait, Is.False);
        Assert.That(gameParams.Warp, Is.Empty);
        Assert.That(modPaths, Is.Empty);
    }

    [Test]
    public void Options_Are_Read()
    {
        // Act
        var (gameParams, _) = StartupArgs.Parse(["--nowait", "--game", "spear", "--warp", "MAP02", "--skill", "2"], TextWriter.Null);

        // Assert
        Assert.That(gameParams.NoWait, Is.True);
        Assert.That(gameParams.Game, Is.EqualTo("spear"));
        Assert.That(gameParams.Warp, Is.EqualTo("MAP02"));
        Assert.That(gameParams.Skill, Is.EqualTo(2));
    }

    [Test]
    public void File_Can_Be_Given_More_Than_Once_Then_Bare_Paths()
    {
        // Act
        // (a bare path straight after --file is taken as another of its values, so it goes first)
        var (_, modPaths) = StartupArgs.Parse(["no-such-c.pk3", "--file", "no-such-a.pk3", "--file", "no-such-b.pk3"], TextWriter.Null);

        // Assert: ones that don't exist are kept as given, for the mods folder to find
        Assert.That(modPaths, Is.EqualTo(new[] { "no-such-a.pk3", "no-such-b.pk3", "no-such-c.pk3" }));
    }

    [Test]
    public void A_Relative_Mod_That_Exists_Is_Made_Full()
    {
        // Arrange: a file in the folder the tests run in
        var name = $"startup-args-{Guid.NewGuid():N}.pk3";
        File.WriteAllText(name, "");
        try
        {
            // Act
            var (_, modPaths) = StartupArgs.Parse(["--file", name], TextWriter.Null);

            // Assert
            Assert.That(modPaths, Is.EqualTo(new[] { Path.GetFullPath(name) }));
        }
        finally
        {
            File.Delete(name);
        }
    }

    [Test]
    public void Bad_Arguments_Are_Reported_And_Run_With_The_Defaults()
    {
        // Arrange
        var help = new StringWriter();

        // Act
        var (gameParams, modPaths) = StartupArgs.Parse(["--no-such-option", "--nowait"], help);

        // Assert
        Assert.That(help.ToString(), Does.Contain("no-such-option"));
        Assert.That(gameParams.NoWait, Is.False);
        Assert.That(modPaths, Is.Empty);
    }
}

public class GameFolderTests
{
    private string _working = "", _exe = "";

    [SetUp]
    public void MakeFolders()
    {
        var root = Path.Combine(Path.GetTempPath(), "pfwolf-gamefolder-" + Guid.NewGuid().ToString("N"));
        _working = Directory.CreateDirectory(Path.Combine(root, "working")).FullName;
        _exe = Directory.CreateDirectory(Path.Combine(root, "exe")).FullName;
    }

    [TearDown]
    public void RemoveFolders() => Directory.Delete(Path.GetDirectoryName(_working)!, recursive: true);

    [Test]
    public void It_Moves_To_The_Exe_When_Only_That_Has_The_Pk3()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_exe, GameFolder.BasePk3), "");

        // Act / Assert
        Assert.That(GameFolder.Pick(_working, _exe), Is.EqualTo(_exe));
    }

    [Test]
    public void It_Stays_When_The_Working_Folder_Has_The_Pk3()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_working, GameFolder.BasePk3), "");
        File.WriteAllText(Path.Combine(_exe, GameFolder.BasePk3), "");

        // Act / Assert
        Assert.That(GameFolder.Pick(_working, _exe), Is.Null);
    }

    [Test]
    public void It_Stays_When_Neither_Has_The_Pk3()
    {
        Assert.That(GameFolder.Pick(_working, _exe), Is.Null);
    }
}

public class CrashReportTests
{
    private string _folder = "";

    [SetUp]
    public void MakeFolder() => _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "pfwolf-crash-" + Guid.NewGuid().ToString("N"))).FullName;

    [TearDown]
    public void RemoveFolder() => Directory.Delete(_folder, recursive: true);

    [Test]
    public void The_Report_Has_The_Time_And_The_Error()
    {
        // Act
        var text = CrashReport.Text(new InvalidOperationException("boom"), new DateTime(2026, 10, 14, 9, 5, 3));

        // Assert
        Assert.That(text, Does.StartWith("PFWolf crashed at 2026-10-14 09:05:03"));
        Assert.That(text, Does.Contain("System.InvalidOperationException: boom"));
    }

    [Test]
    public void It_Is_Saved_In_The_First_Folder_That_Takes_It()
    {
        // Arrange: a folder that doesn't exist can't
        var missing = Path.Combine(_folder, "missing");

        // Act
        var path = CrashReport.Save("report", [missing, _folder]);

        // Assert
        Assert.That(path, Is.EqualTo(Path.Combine(_folder, CrashReport.FileName)));
        Assert.That(File.ReadAllText(path!), Is.EqualTo("report"));
    }

    [Test]
    public void Nowhere_To_Save_It_Is_Null()
    {
        Assert.That(CrashReport.Save("report", [Path.Combine(_folder, "missing")]), Is.Null);
    }
}

public class StartupOutputTests
{
    [Test]
    public void It_Keeps_The_Lines_And_Writes_Them_Through()
    {
        // Arrange
        var console = Console.Out;
        var original = new StringWriter();
        var output = new StartupOutput(original);

        // Act
        output.Write("first\r\nsec");
        output.Write('o');
        output.Write("nd\nunfinished");
        var lines = output.Stop();
        Console.SetOut(console);        // Stop puts back the writer it was made with

        // Assert
        Assert.That(lines, Is.EqualTo(new[] { "first", "second", "unfinished" }));
        Assert.That(original.ToString(), Is.EqualTo("first\r\nsecond\nunfinished"));
    }
}

public class GameServicesTests
{
    [TestCase(typeof(GameEngineManager))]
    [TestCase(typeof(VideoManager))]
    [TestCase(typeof(AudioManager))]
    [TestCase(typeof(InputManager))]
    [TestCase(typeof(GraphicManager))]
    [TestCase(typeof(FontManager))]
    [TestCase(typeof(MapManager))]
    [TestCase(typeof(AssetManager))]
    [TestCase(typeof(InventoryManager))]
    [TestCase(typeof(ConsoleManager))]
    [TestCase(typeof(AutomapManager))]
    [TestCase(typeof(HudMessageManager))]
    [TestCase(typeof(FactionManager))]
    [TestCase(typeof(TranslationManager))]
    public void Each_Manager_Is_One_Of_A_Kind(Type manager)
    {
        // Act
        var services = GameServices.Register(new ServiceCollection(), new GameSession(), new Configuration.GameSettings());

        // Assert
        Assert.That(services.Where(s => s.ServiceType == manager).Select(s => s.Lifetime),
            Is.EqualTo(new[] { ServiceLifetime.Singleton }));
    }

    [Test]
    public void The_Managers_Are_Given_The_Games_Own_Session()
    {
        // Arrange
        var session = new GameSession();
        using var provider = GameServices.Register(new ServiceCollection(), session, new Configuration.GameSettings()).BuildServiceProvider();

        // Act
        var map = provider.GetRequiredService<MapManager>();
        session.SetActing(session.LocalPlayer);
        session.LocalPlayer.Pawn = new Entities.Actors.PlayerPawn();

        // Assert: the map's player is the session's
        Assert.That(provider.GetRequiredService<GameSession>(), Is.SameAs(session));
        Assert.That(map.Player, Is.SameAs(session.LocalPlayer.Pawn));
    }

    [Test]
    public void The_Map_Takes_The_Actor_World_Only_Once_It_Needs_It()
    {
        // Arrange: the world isn't built yet when the map is (it needs the map)
        Entities.Actors.ActorWorld? world = null;
        var session = new GameSession();
        using var provider = GameServices.Register(new ServiceCollection(), session, new Configuration.GameSettings(), () => world!)
            .BuildServiceProvider();
        var map = provider.GetRequiredService<MapManager>();

        // Act: built afterwards, as GameHost does at the end of InitServices
        world = new Entities.Actors.ActorWorld(null!, null!, null!, null!, null!, map, null!, null!, null!, null!, null!, null!,
            null!, null!, null!, null!, null!, null!, null!, null, null, session);

        // Assert
        Assert.That(map.World, Is.SameAs(world));
    }
}

public class GameInfoSettingsTests
{
    [Test]
    public void Nothing_Set_Is_The_Defaults()
    {
        // Act
        var settings = GameInfoSettings.Read(new GameInfoAsset());

        // Assert
        Assert.That(settings.ScreenFadeStyle, Is.EqualTo(FadeStyle.Palette));
        Assert.That(settings.ScreenFadeTics, Is.Null);
        Assert.That((settings.MenuFadeStyle, settings.MenuFadeTics), Is.EqualTo((FadeStyle.Palette, 20)));
        Assert.That((settings.DeathFadeStyle, settings.DeathFadeTics), Is.EqualTo((FadeStyle.Fizzle, 70)));
        Assert.That((settings.LevelFadeStyle, settings.LevelFadeTics), Is.EqualTo((FadeStyle.Fizzle, 20)));
        Assert.That(settings.DeathFadeColor, Is.EqualTo("Maroon"));
        Assert.That((settings.DeathTurnSpeed, settings.DeathHoldTics), Is.EqualTo((2, 100)));
        Assert.That((settings.DeathDropHeight, settings.DeathDropSpeed), Is.EqualTo(((int?)null, 1)));
        Assert.That(settings.WeaponScale, Is.EqualTo(1.0));
    }

    [Test]
    public void The_Menu_Fade_Follows_The_Screen_Fade()
    {
        // Act
        var settings = GameInfoSettings.Read(new GameInfoAsset { ScreenFadeStyle = "Fizzle" });

        // Assert
        Assert.That(settings.MenuFadeStyle, Is.EqualTo(FadeStyle.Fizzle));
    }

    [Test]
    public void Styles_Are_Read_By_Name_Whatever_The_Case()
    {
        Assert.That(GameInfoSettings.Read(new GameInfoAsset { DeathFadeStyle = "palette" }).DeathFadeStyle, Is.EqualTo(FadeStyle.Palette));
    }

    [TestCase("sparkle")]
    [TestCase("0")]         // not by number
    public void An_Unknown_Style_Is_The_Default(string style)
    {
        Assert.That(GameInfoSettings.Read(new GameInfoAsset { LevelFadeStyle = style }).LevelFadeStyle, Is.EqualTo(FadeStyle.Fizzle));
    }

    [TestCase(0)]
    [TestCase(-5)]
    public void Fades_Of_No_Tics_Are_The_Default(int tics)
    {
        // Act
        var settings = GameInfoSettings.Read(new GameInfoAsset { ScreenFadeTics = tics, MenuFadeTics = tics });

        // Assert
        Assert.That(settings.ScreenFadeTics, Is.Null);
        Assert.That(settings.MenuFadeTics, Is.EqualTo(20));
    }

    [Test]
    public void Values_Are_Kept_In_Range()
    {
        // Act
        var settings = GameInfoSettings.Read(new GameInfoAsset
        {
            DeathTurnSpeed = 0,
            DeathHoldTics = -1,
            DeathDropHeight = 1000,
            DeathDropSpeed = 0,
            WeaponScale = 5,
        });

        // Assert
        Assert.That(settings.DeathTurnSpeed, Is.EqualTo(1));
        Assert.That(settings.DeathHoldTics, Is.EqualTo(0));
        Assert.That(settings.DeathDropHeight, Is.EqualTo(GameConstants.MaxEye));
        Assert.That(settings.DeathDropSpeed, Is.EqualTo(1));
        Assert.That(settings.WeaponScale, Is.EqualTo(2));
    }

    [Test]
    public void A_Blank_Death_Color_Is_Maroon()
    {
        Assert.That(GameInfoSettings.Read(new GameInfoAsset { DeathFadeColor = " " }).DeathFadeColor, Is.EqualTo("Maroon"));
    }
}
