using System.Reflection;
using PFWolf.Constants;

namespace PFWolf.Tests;

/// <summary>
/// The areas a door joins (bstone's GetAreaNumber): a floor code beside it that isn't an area,
/// such as Blake Stone's door trigger, makes it look round for one
/// </summary>
public class DoorAreasTests
{
    private const int AreaTile = 108;
    private const int DoorTrigger = 158;

    private MapManager _map = null!;
    private GameSession _session = null!;
    private Doors _doors = null!;

    [SetUp]
    public void MakeLevel()
    {
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session, null)
        {
            MapWidth = MapConstants.ClassicMapSize, MapHeight = MapConstants.ClassicMapSize,
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        typeof(MapManager).GetField("mapsegs", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_map, Enumerable.Range(0, MapManager.LevelPlanes).Select(_ => new ushort[MapConstants.ClassicMapArea]).ToArray());
        typeof(MapManager).GetField("_floors", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_map, new FloorCodes(AreaTile, 45, 106, -1, 162));
        _session.Areas.Reset(45);
        _doors = new Doors(null!, null!, _map, null!, null!, _session, null!);
    }

    private void Floor(int x, int y, int code) => _map.SetMapSpot(x, y, 0, (ushort)code);

    [Test]
    public void A_Tile_Thats_No_Area_Takes_The_First_Area_Round_It()
    {
        // Arrange: a door trigger with area 5 north of it and area 6 south
        Floor(9, 10, DoorTrigger);
        Floor(9, 9, AreaTile + 5);
        Floor(9, 11, AreaTile + 6);

        // Act / Assert
        Assert.That(_doors.AreaNumberAt(9, 10), Is.EqualTo(5));
        Assert.That(_doors.AreaNumberAt(9, 11), Is.EqualTo(6));
    }

    [Test]
    public void A_Wall_Or_Off_The_Map_Is_No_Area()
    {
        // Arrange
        Floor(9, 10, AreaTile + 5);
        _map.TileMap[9, 10] = 12;

        // Act / Assert
        Assert.That(_doors.AreaNumberAt(9, 10), Is.EqualTo(-1));
        Assert.That(_doors.AreaNumberAt(-1, 10), Is.EqualTo(-1));
    }

    [Test]
    public void A_Door_Beside_A_Trigger_Still_Joins_Its_Areas()
    {
        // Arrange: a north-south door at 10,10, a door trigger west of it in area 5, area 7 east
        Floor(9, 10, DoorTrigger);
        Floor(9, 9, AreaTile + 5);
        Floor(11, 10, AreaTile + 7);
        _map.TileMap[10, 10] = (ushort)GameConstants.BitDoor;
        _doors.DoorObjList[0] = new DoorObject { TileX = 10, TileY = 10, Vertical = true, Action = DoorAction.Opening };
        _doors.LastDoorObj = 1;

        // Act: it starts to open
        _doors.DoorOpening(0);

        // Assert
        Assert.That(_session.Areas.AreaConnect[7, 5], Is.EqualTo(1));
        Assert.That(_session.Areas.AreaConnect[5, 7], Is.EqualTo(1));
    }
}
