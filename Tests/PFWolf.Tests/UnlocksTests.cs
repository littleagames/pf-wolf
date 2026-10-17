using PFWolf.Assets;
using PFWolf.Commands;
using PFWolf.Gameplay;
using PFWolf.Loaders;
using PFWolf.Scripting;
using PFWolf.Ui;

namespace PFWolf.Tests;

public class UnlocksTests
{
    private const string GameInfo = """
        episodes:
          EP01: { name: "Campaign", start-map: MAP01 }
          EP02: { name: "Dog Tag Missions", start-map: MAP02, starts-locked: true, locked-name: "- Locked -" }
          EP03: { name: "Challenges", start-map: MAP03, starts-locked: true }
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
    public void Game_Info_Reads_Starts_Locked_And_The_Locked_Name()
    {
        Assert.That(_info.Episodes["EP01"].StartsLocked, Is.False);
        Assert.That(_info.Episodes["EP02"].StartsLocked, Is.True);
        Assert.That(_info.Episodes["EP02"].LockedName, Is.EqualTo("- Locked -"));
        Assert.That(_info.Episodes["EP02"].Locked, Is.False);      // not the shareware's locked-message
    }

    [Test]
    public void A_Starts_Locked_Episode_Opens_Once_Unlocked_And_Stays_So_In_The_Next_Game()
    {
        // Arrange
        var unlocks = Make();
        Assert.That(unlocks.IsOpen(_info.Episodes["EP01"]), Is.True);
        Assert.That(unlocks.IsOpen(_info.Episodes["EP02"]), Is.False);

        // Act
        bool first = unlocks.Unlock("ep02");
        bool again = unlocks.Unlock("EP02");

        // Assert: case doesn't matter, the second does nothing, and it's in the file for next time
        Assert.That(first, Is.True);
        Assert.That(again, Is.False);
        Assert.That(unlocks.IsOpen(_info.Episodes["EP02"]), Is.True);
        Assert.That(Make().IsOpen(_info.Episodes["EP02"]), Is.True);
        Assert.That(Make().IsOpen(_info.Episodes["EP03"]), Is.False);
    }

    [Test]
    public void Locking_Takes_It_Out_Of_The_File()
    {
        // Arrange
        var unlocks = Make();
        unlocks.Unlock("EP02");
        unlocks.Unlock("secretcode");

        // Act
        bool locked = unlocks.Lock("EP02");

        // Assert
        Assert.That(locked, Is.True);
        Assert.That(Make().Unlocked, Is.EqualTo(new[] { "secretcode" }));
        Assert.That(unlocks.Lock("EP02"), Is.False);
    }

    [Test]
    public void No_File_Means_Nothing_Unlocked_And_The_Lockable_Names_Are_The_Starts_Locked_Episodes()
    {
        var unlocks = Make();
        Assert.That(unlocks.Unlocked, Is.Empty);
        Assert.That(unlocks.Lockable(), Is.EqualTo(new[] { "EP02", "EP03" }));
    }

    [Test]
    public void The_Episode_Menu_Greys_Out_Locked_Episodes_And_Moves_The_Cursor_Off_Them()
    {
        // Arrange: EP02 and EP03 locked, the cursor left on EP02
        var unlocks = Make();
        var rows = _info.Episodes.Values.Select(ep => new ControlPanelItem(1, ep.Name, null, ep)).ToArray();
        var info = new ControlPanelMenuInfo(0, 0, (short)rows.Length, 1, 0);

        // Act
        bool any = NewGameMenu.ShowEpisodeLocks(rows, info, unlocks.IsOpen, text => text);

        // Assert
        Assert.That(any, Is.True);
        Assert.That(rows.Select(r => r.active), Is.EqualTo(new short[] { 1, 0, 0 }));
        Assert.That(rows.Select(r => r.text), Is.EqualTo(new[] { "Campaign", "- Locked -", "Challenges" }));
        Assert.That(info.curpos, Is.EqualTo(0));

        // Unlocked, it has its name back
        unlocks.Unlock("EP02");
        NewGameMenu.ShowEpisodeLocks(rows, info, unlocks.IsOpen, text => text);
        Assert.That(rows[1].active, Is.EqualTo(1));
        Assert.That(rows[1].text, Is.EqualTo("Dog Tag Missions"));
    }

    [Test]
    public void The_Unlock_Command_Unlocks_All_And_Lists_And_Lock_Undoes_It()
    {
        // Arrange
        var console = new ConsoleManager();
        var unlocks = Make();
        var commands = new UnlockCommands(console, unlocks);

        // Act
        commands.UnlockCommand(["all"]);
        commands.UnlockCommand([]);
        commands.LockCommand(["EP03"]);

        // Assert
        Assert.That(console.Scrollback, Has.Some.EqualTo("Unlocked 2 more"));
        Assert.That(console.Scrollback, Has.Some.EqualTo("EP02: unlocked"));
        Assert.That(unlocks.IsUnlocked("EP02"), Is.True);
        Assert.That(unlocks.IsUnlocked("EP03"), Is.False);
    }

    [Test]
    public void A_Script_Unlocks_And_Asks()
    {
        // Arrange
        var console = new ConsoleManager();
        var unlocks = Make();
        var scripts = new GameScripts(null!, null!, null!, null!, console, null, null!, new GameSession(), (_, _) => { })
        {
            Unlock = unlocks.Unlock,
            IsUnlocked = unlocks.IsUnlocked,
        };
        scripts.LuaCommand(["x = 1"]);

        // Act
        scripts.Scripts!.Execute("print(game.unlocked('EP02'), game.unlock('EP02'), game.unlock('EP02'), game.unlocked('ep02'))");

        // Assert
        Assert.That(console.Scrollback, Has.Some.EqualTo("false\ttrue\tfalse\ttrue"));
    }
}
