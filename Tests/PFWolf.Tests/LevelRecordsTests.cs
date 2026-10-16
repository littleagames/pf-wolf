using PFWolf.Gameplay;
using PFWolf.Scripting;

namespace PFWolf.Tests;

public class LevelRecordsTests
{
    private string _path = null!;

    [SetUp]
    public void MakeFile() => _path = Path.Combine(Path.GetTempPath(), $"pfwolf-records-{Guid.NewGuid():N}.cfg");

    [TearDown]
    public void RemoveFile()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }

    private static LevelResult Finish(int kills, int secrets, int time, bool underPar, int intel, bool undetected = false) => new()
    {
        Kills = kills, KillTotal = 10, Secrets = secrets, SecretTotal = 4, Items = 1, ItemTotal = 1,
        Time = time, Par = 60, UnderPar = underPar, Undetected = undetected,
        Objectives = new(StringComparer.OrdinalIgnoreCase) { ["intel"] = (intel, 3) },
    };

    [Test]
    public void Each_Finish_Only_Betters_A_Maps_Record_Field_By_Field_And_It_Is_Kept_For_The_Next_Game()
    {
        // Arrange
        var records = new LevelRecords(() => _path);

        // Act: all the kills slowly, then all the secrets and intel quickly
        records.Record("CAMP01", Finish(kills: 10, secrets: 1, time: 9000, underPar: false, intel: 1, undetected: true));
        records.Record("CAMP01", Finish(kills: 2, secrets: 4, time: 3000, underPar: true, intel: 3));

        // Assert: read back as the next game would
        var best = new LevelRecords(() => _path).Get("camp01")!;
        Assert.That((best.Kills, best.KillTotal), Is.EqualTo((10, 10)));
        Assert.That((best.Secrets, best.SecretTotal), Is.EqualTo((4, 4)));
        Assert.That(best.Time, Is.EqualTo(3000));
        Assert.That(best.UnderPar, Is.True);
        Assert.That(best.Undetected, Is.True);
        Assert.That(best.Objectives["intel"], Is.EqualTo((3, 3)));
        Assert.That(new LevelRecords(() => _path).Get("CAMP02"), Is.Null);
    }

    [Test]
    public void Records_Can_Be_Forgotten()
    {
        var records = new LevelRecords(() => _path);
        records.Record("CAMP01", Finish(1, 1, 100, false, 0));
        records.Record("CAMP02", Finish(1, 1, 100, false, 0));

        Assert.That(records.Clear("CAMP01"), Is.True);
        Assert.That(new LevelRecords(() => _path).Maps, Is.EqualTo(new[] { "CAMP02" }));
        Assert.That(records.Clear(null), Is.True);
        Assert.That(new LevelRecords(() => _path).Maps, Is.Empty);
    }

    [Test]
    public void A_Level_End_Hook_Gets_The_Finish_Reads_Records_And_Unlocks()
    {
        // Arrange: CAMP01 already done under par
        var console = new ConsoleManager();
        var records = new LevelRecords(() => _path);
        records.Record("CAMP01", Finish(10, 4, 3000, true, 3));
        var unlocked = new List<string>();
        var scripts = new GameScripts(null!, null!, null!, null!, console, null, null!, new GameSession(), (_, _) => { })
        {
            Unlock = name => { unlocked.Add(name); return true; },
            Record = records.Get,
        };
        scripts.Cmd_Lua(["x = 1"]);
        scripts.Scripts!.Load("""
            game.onlevelend(function(map, r)
              print('end', map, r.exit, r.kills, r.killtotal, r.objectives.intel.done, r.underpar)
              local first = game.record('CAMP01')
              if first.underpar and r.underpar then game.unlock('CHAL04') end
              print('none', game.record('CAMP09'))
            end)
            """, "test.lua");

        // Act
        scripts.RunLevelEndHooks("CAMP02", Finish(7, 2, 2000, true, 2), "secret");

        // Assert
        Assert.That(console.Scrollback, Has.Some.EqualTo("end\tCAMP02\tsecret\t7\t10\t2\ttrue"));
        Assert.That(console.Scrollback, Has.Some.EqualTo("none\tnil"));
        Assert.That(unlocked, Is.EqualTo(new[] { "CHAL04" }));
    }

    [Test]
    public void What_Is_Unlocked_Is_News_Once_For_The_Tally()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"pfwolf-progress-{Guid.NewGuid():N}.cfg");
        try
        {
            var unlocks = new Unlocks(() => path, () => new PFWolf.Assets.GameInfoAsset());

            // Act / Assert
            Assert.That(unlocks.TakeNews(), Is.False);
            unlocks.Unlock("EP02");
            unlocks.Unlock("EP02");
            Assert.That(unlocks.TakeNews(), Is.True);
            Assert.That(unlocks.TakeNews(), Is.False);      // told once

            unlocks.Unlock("EP03");
            unlocks.ForgetNews();                           // a new game started
            Assert.That(unlocks.TakeNews(), Is.False);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
