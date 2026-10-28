using PFWolf.Constants;

namespace PFWolf.Tests;

/// <summary>Blake Stone's linked doors (doors.yaml link: object-plane): a door works the next door of its chain</summary>
public class LinkedDoorsTests
{
    private MapManager _map = null!;
    private Doors _doors = null!;

    [SetUp]
    public void MakeLevel()
    {
        var session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, session, null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        _doors = new Doors(null!, null!, _map, null!, null!, session, null!);
    }

    // Door n at (10 + n, 10), linked to the tile given (or to none), locked by a card
    private void Door(int n, (int X, int Y)? link)
    {
        _map.TileMap[10 + n, 10] = (ushort)(GameConstants.BitDoor | n);
        _doors.DoorObjList[n] = new DoorObject
        {
            TileX = (byte)(10 + n), TileY = 10, Action = DoorAction.Closed, MapLock = "RedCard",
            LinkX = link?.X ?? -1, LinkY = link?.Y ?? -1,
        };
        _doors.LastDoorObj = Math.Max(_doors.LastDoorObj, n + 1);
    }

    [Test]
    public void Opening_A_Linked_Door_Opens_And_Unlocks_The_Chain()
    {
        // Arrange: 0 links to 1, which links to 2; 3 is on its own
        Door(0, (11, 10));
        Door(1, (12, 10));
        Door(2, null);
        Door(3, null);

        // Act
        _doors.OpenDoor(0);

        // Assert
        Assert.That(Enumerable.Range(0, 3).Select(n => _doors.DoorObjList[n].Action), Has.All.EqualTo(DoorAction.Opening));
        Assert.That(_doors.DoorObjList[1].IsLocked || _doors.DoorObjList[2].IsLocked, Is.False, "the linked ones are unlocked");
        Assert.That(_doors.DoorObjList[0].IsLocked, "the door itself is opened as it always is");
        Assert.That(_doors.DoorObjList[3].Action, Is.EqualTo(DoorAction.Closed));
    }

    [Test]
    public void A_Script_Can_Keep_A_Door_Shut_And_Is_Told_Who_Opened_It()
    {
        // Arrange: a script that keeps door 1 (at 11,10) shut
        var asked = new List<(int X, int Y, bool Opening, Entities.Actors.Actor? By)>();
        _doors = new Doors(null!, null!, _map, null!, null!, new GameSession(), new DoorHooks((_, _, _) => { }, () => { },
            (x, y, opening, by) => { asked.Add((x, y, opening, by)); return x != 11; }));
        Door(0, null);
        Door(1, null);
        var guard = new Entities.Actors.Monster { Name = "Guard" };

        // Act
        _doors.OpenDoor(0, guard);
        _doors.OpenDoor(0, guard);      // already opening: not asked again
        _doors.OpenDoor(1);

        // Assert
        Assert.That(_doors.DoorObjList[0].Action, Is.EqualTo(DoorAction.Opening));
        Assert.That(_doors.DoorObjList[1].Action, Is.EqualTo(DoorAction.Closed));
        Assert.That(asked, Is.EqualTo(new (int, int, bool, Entities.Actors.Actor?)[] { (10, 10, true, guard), (11, 10, true, null) }));
    }

    [Test]
    public void A_Chain_That_Loops_Works_Each_Door_Once()
    {
        // Arrange: 0 -> 1 -> 2 -> 1, which bstone would follow for ever
        Door(0, (11, 10));
        Door(1, (12, 10));
        Door(2, (11, 10));

        // Act
        _doors.OpenDoor(0);

        // Assert
        Assert.That(Enumerable.Range(0, 3).Select(n => _doors.DoorObjList[n].Action), Has.All.EqualTo(DoorAction.Opening));
    }

    [Test]
    [Category("GameData")]
    public void An_Aog_Trap_Opens_Its_Whole_Row_Of_Cells()
    {
        // Arrange: AOG floor 8's cells, 45,59 to 61,59, each linked to the one west of it
        var workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-doors-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workFolder);
        try
        {
            HeadlessGame.PrepareFolder(workFolder, "blake-aog");

            // Act: the trap's smart switch opens 61,59
            var (exitCode, output) = HeadlessGame.Run(workFolder, "blake", "wait 10; doors open 61 59; wait 2; doors; quit",
                TimeSpan.FromMinutes(2), extraArgs: ["--warp", "MAP09"]);
            var cells = output.Where(line => System.Text.RegularExpressions.Regex.IsMatch(line, @"^door \d+ at (4[5-9]|5\d|6[01]),59:")).ToList();

            // Assert
            Assert.That(exitCode, Is.Zero, string.Join(Environment.NewLine, output.TakeLast(30)));
            Assert.That(cells, Has.Count.EqualTo(9));
            Assert.That(cells, Has.All.Contains(": Opening"));
        }
        finally
        {
            try { Directory.Delete(workFolder, recursive: true); } catch (IOException) { }
        }
    }

    [Test]
    public void A_Link_To_A_Tile_That_Isnt_A_Door_Does_Nothing()
    {
        // Arrange
        Door(0, (30, 30));

        // Act / Assert
        Assert.DoesNotThrow(() => _doors.OpenDoor(0));
        Assert.That(_doors.DoorObjList[0].Action, Is.EqualTo(DoorAction.Opening));
    }
}
