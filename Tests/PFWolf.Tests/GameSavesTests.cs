namespace PFWolf.Tests;

public class GameSavesTests
{
    private GameSession _session = null!;
    private readonly List<bool> _screens = [];
    private GameSaves _saves = null!;

    [SetUp]
    public void Make()
    {
        _session = new GameSession();
        _screens.Clear();
        _saves = new GameSaves(null!, new AssetManager(), null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
            null!, null!, null!, null!, new Configuration.GameSettings { ViewSize = 15 }, _session,
            new GameSavesHooks(saving => { _screens.Add(saving); return 1; }, () => { }));
    }

    [Test]
    public void With_No_Mods_Loaded_A_Save_Made_With_Some_Has_Other_Mods()
    {
        // Act / Assert
        Assert.That(_saves.CurrentSavedMods(), Is.Empty);
        Assert.That(_saves.HasOtherMods(new SaveInfo { Mods = [new SavedMod("Moody Lights", "1.0", "moody-lights.pk3")] }), Is.True);
        Assert.That(_saves.HasOtherMods(new SaveInfo { Mods = [] }), Is.False);
    }

    [Test]
    public void A_Game_With_Several_Players_Isnt_Saved()
    {
        // Arrange
        _session.Players.Add(new Entities.PlayerState { Number = 2 });

        // Act / Assert: refused before anything is drawn or written
        Assert.That(_saves.SaveTheGame("unused.dat", "x", 0, 0), Is.False);
        Assert.That(File.Exists("unused.dat"), Is.False);
    }

    [Test]
    public void With_Autosave_Off_A_New_Level_Isnt_Saved()
    {
        // Arrange
        _saves.AutosaveEnabled = false;
        _saves.AutosavePending = true;

        // Act: no engine, so a save would throw
        _saves.AutoSaveGame();

        // Assert
        Assert.That(_saves.AutosavePending, Is.False);
    }

    [Test]
    public void The_Menus_Load_And_Save_Open_Their_Screens()
    {
        // Act
        _saves.RunLoadGame(0);
        _saves.RunSaveGame(0);

        // Assert
        Assert.That(_screens, Is.EqualTo(new[] { false, true }));
    }

    [Test]
    public void The_Disk_Only_Flops_Where_Asked_To()
    {
        // At 0,0 nothing's drawn (no graphics here: drawing would throw)
        Assert.DoesNotThrow(() => _saves.DiskFlopAnim(0, 0));
    }
}
