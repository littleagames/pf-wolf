using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

public class AutomapDrawingTests
{
    private MapManager _map = null!;
    private GameSession _session = null!;
    private AutomapDrawing _automap = null!;

    [SetUp]
    public void MakeLevel()
    {
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")))
        {
            tilemap = new byte[MapManager.MAPSIZE, MapManager.MAPSIZE],
            actorat = new Actor?[MapManager.MAPSIZE, MapManager.MAPSIZE],
            seen = new SeenFlags[MapManager.MAPSIZE, MapManager.MAPSIZE],
        };
        _session = new GameSession();
        _automap = new AutomapDrawing(null!, null!, null!, null!, null!, _map, null!, null!, null!, null!, _session,
            new AutomapHooks(() => (0, 0, 320, 160), () => false, map => map, tics => ""));
    }

    [Test]
    public void The_View_Puts_The_Map_Centre_In_The_Middle_At_The_Zoom()
    {
        // Arrange: 8 pixels a tile, unturned, centred on tile (10, 20)
        var view = new AutomapDrawing.AutomapView(0, 0, 320, 160, 8, 10, 20, 1, 0, 1);

        // Act / Assert
        Assert.That(view.ToScreen(10, 20), Is.EqualTo((160f, 80f)));
        Assert.That(view.ToScreen(11, 22), Is.EqualTo((168f, 96f)));
    }

    [Test]
    public void Turned_A_Right_Angle_East_Becomes_South()
    {
        var view = new AutomapDrawing.AutomapView(0, 0, 320, 160, 8, 10, 20, 0, 1, 1);
        Assert.That(view.ToScreen(11, 20), Is.EqualTo((160f, 88f)));
    }

    [TestCase(0, false)]                                // open floor
    [TestCase(5, true)]                                 // a wall
    [TestCase(GameConstants.BIT_DOOR | 2, false)]             // a door shows as floor (drawn on its own)
    [TestCase(GameConstants.BIT_WALL, false)]                 // a moving pushwall's tile (drawn at its offset)
    public void Walls_Are_What_Shows_As_Wall(int tile, bool wall)
    {
        _map.tilemap[3, 4] = (byte)tile;
        Assert.That(_automap.IsAutomapWall(3, 4), Is.EqualTo(wall));
    }

    [Test]
    public void Off_The_Map_Is_Wall()
    {
        Assert.That(_automap.IsAutomapWall(-1, 0), Is.True);
        Assert.That(_automap.IsAutomapWall(0, MapManager.MAPSIZE), Is.True);
    }

    [Test]
    public void An_Item_Shows_Once_Its_Floor_Is_Seen_Or_Revealed()
    {
        // Arrange
        var clip = new CustomInventory { Name = "Clip", TileX = 3, TileY = 4 };
        clip.SetState(new ActorStateFrame { StateName = "Spawn", Sprite = "CLIP", FrameLetter = "A", TicTime = -1 });

        // Act / Assert
        Assert.That(_automap.GetAutomapMark(clip, reveal: false), Is.EqualTo(AutomapDrawing.AutomapMark.None));
        Assert.That(_automap.GetAutomapMark(clip, reveal: true), Is.EqualTo(AutomapDrawing.AutomapMark.Item));
        _map.seen[3, 4] = SeenFlags.Floor;
        Assert.That(_automap.GetAutomapMark(clip, reveal: false), Is.EqualTo(AutomapDrawing.AutomapMark.Item));
    }

    [Test]
    public void Solid_Decor_Shows_And_What_You_Walk_Through_Doesnt()
    {
        // Arrange
        _map.seen[3, 4] = SeenFlags.Floor;
        var spawn = new ActorStateFrame { StateName = "Spawn", Sprite = "BARL", FrameLetter = "A", TicTime = -1 };
        var barrel = new Monster { Name = "Barrel", TileX = 3, TileY = 4, Flags = ["SOLID"] };
        var puddle = new Monster { Name = "Puddle", TileX = 3, TileY = 4 };
        barrel.SetState(spawn);
        puddle.SetState(spawn);

        // Act / Assert
        Assert.That(_automap.GetAutomapMark(barrel, reveal: false), Is.EqualTo(AutomapDrawing.AutomapMark.Decor));
        Assert.That(_automap.GetAutomapMark(puddle, reveal: false), Is.EqualTo(AutomapDrawing.AutomapMark.None));
    }

    [Test]
    public void Projectiles_And_Markers_Never_Show()
    {
        // Arrange
        _map.seen[3, 4] = SeenFlags.Floor;
        var rocket = new Monster { Name = "Rocket", TileX = 3, TileY = 4, Active = activetypes.ac_yes };
        rocket.SetState(new ActorStateFrame { StateName = "Spawn", Sprite = "ROCK", FrameLetter = "A", TicTime = -1 });
        var marker = new Monster { Name = "PatrolPoint", TileX = 3, TileY = 4 };
        marker.SetState(new ActorStateFrame { StateName = "Spawn", Sprite = "TNT1", FrameLetter = "A", TicTime = -1 });

        // Act / Assert
        Assert.That(_automap.GetAutomapMark(rocket, reveal: true), Is.EqualTo(AutomapDrawing.AutomapMark.None));
        Assert.That(_automap.GetAutomapMark(marker, reveal: true), Is.EqualTo(AutomapDrawing.AutomapMark.None));
    }
}

public class RadarTests
{
    [Test]
    public void A_Seen_Tile_Shows_As_Floor_Or_Wall()
    {
        // Arrange
        var map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")))
        {
            tilemap = new byte[MapManager.MAPSIZE, MapManager.MAPSIZE],
            actorat = new Actor?[MapManager.MAPSIZE, MapManager.MAPSIZE],
        };
        var session = new GameSession();
        var doors = new Doors(null!, null!, map, null!, null!, session, null!);
        var radar = new Radar(map, null!, null!, null!, null!, null!, null!, doors, session, () => false, () => 0);
        map.tilemap[1, 1] = 5;
        map.tilemap[2, 1] = GameConstants.BIT_DOOR | 0;
        doors.doorobjlist[0] = new doorobj_t { action = dooractiontypes.dr_closed };
        map.tilemap[3, 1] = GameConstants.BIT_DOOR | 1;
        doors.doorobjlist[1] = new doorobj_t { action = dooractiontypes.dr_open };

        // Act
        string Color(int x) => radar.RadarTileColor(x, 1, "unmapped", "floor", "door", "locked");

        // Assert
        Assert.That(Color(0), Is.EqualTo("floor"));
        Assert.That(Color(1), Is.EqualTo("unmapped"));      // a wall shows as nothing seen
        Assert.That(Color(2), Is.EqualTo("door"));          // a closed door with no lock
        Assert.That(Color(3), Is.EqualTo("floor"));         // an open one is floor
    }

    [Test]
    public void A_Locked_Door_Shows_As_Locked_Open_Or_Shut_Until_A_Key_Unlocks_It()
    {
        // Arrange: a gold-key door, closed
        var map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")))
        {
            tilemap = new byte[MapManager.MAPSIZE, MapManager.MAPSIZE],
            actorat = new Actor?[MapManager.MAPSIZE, MapManager.MAPSIZE],
        };
        var session = new GameSession();
        var doors = new Doors(null!, null!, map, null!, null!, session, null!);
        var radar = new Radar(map, null!, null!, null!, null!, null!, null!, doors, session, () => false, () => 0);
        map.tilemap[2, 1] = GameConstants.BIT_DOOR | 0;
        doors.doorobjlist[0] = new doorobj_t { action = dooractiontypes.dr_closed, maplock = "GoldKey" };
        string Color() => radar.RadarTileColor(2, 1, "unmapped", "floor", "door", "locked");

        // Act / Assert: as bstone, the lock wins whether it's open or shut
        Assert.That(Color(), Is.EqualTo("locked"));
        doors.doorobjlist[0].action = dooractiontypes.dr_open;
        Assert.That(Color(), Is.EqualTo("locked"));

        // a key has unlocked it (doors.yaml takes-key): an ordinary door from now on
        doors.doorobjlist[0].unlocked = true;
        Assert.That(Color(), Is.EqualTo("floor"));
        doors.doorobjlist[0].action = dooractiontypes.dr_closed;
        Assert.That(Color(), Is.EqualTo("door"));
    }
}
