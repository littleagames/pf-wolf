using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

public class ActorLightsTests
{
    private const int Tile = 1 << MapConstants.TileShift;

    private MapManager _map = null!;
    private Doors _doors = null!;
    private GameSession _session = null!;
    private Lighting _lighting = null!;
    private ActorLights _lights = null!;

    [SetUp]
    public void MakeLevel()
    {
        // An open map with nothing on it; the tests put walls and doors where they need them
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session, null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        _doors = new Doors(null!, null!, _map, null!, null!, _session, null!);
        ActorLights? lights = null;
        _lighting = new Lighting(null!, LightingTests.GreyVideo(), _map,
            new LightingHooks(() => 0, () => lights?.ColorTintTables.Clear(), tics => lights?.TickActorLights(tics)));
        _lights = lights = new ActorLights(_map, _doors, _lighting, _session);
    }

    // An actor giving off light in the middle of a tile
    private Monster Lamp(int x, int y, int intensity = 255, double radius = 3, string? color = null)
    {
        var lamp = new Monster { Name = $"Lamp{intensity}", X = x * Tile + Tile / 2, Y = y * Tile + Tile / 2, TileX = (byte)x, TileY = (byte)y };
        lamp.Properties["light.intensity"] = intensity;
        lamp.Properties["light.radius"] = radius;
        if (color != null)
            lamp.Properties["light.color"] = color;
        _map.GetActors().AddLast(lamp);
        return lamp;
    }

    // The light added to the cell in the middle of a tile
    private int AddedAt(int x, int y) =>
        _lighting.CellAdd[(((y << Lighting.LightCellShift) + Lighting.LightCells / 2) << 9) + (x << Lighting.LightCellShift) + Lighting.LightCells / 2];

    private void Dim() => _lighting.SetShading(new ShadingSettings("#000000", 0, 16, 0, 64));

    [Test]
    public void A_Lamp_Lights_Its_Own_Tile_Most_And_Less_Further_Off()
    {
        // Arrange
        Dim();
        Lamp(10, 10);

        // Act
        _lights.CastActorLights();

        // Assert
        Assert.That(_lights.HasLights, Is.True);
        Assert.That(AddedAt(10, 10), Is.GreaterThan(AddedAt(11, 10)));
        Assert.That(AddedAt(11, 10), Is.GreaterThan(0));
        Assert.That(AddedAt(14, 10), Is.Zero);          // past its radius
    }

    [Test]
    public void A_Wall_Is_Lit_But_Nothing_Behind_It()
    {
        // Arrange: a wall two tiles east
        Dim();
        _map.TileMap[12, 10] = 1;
        Lamp(10, 10);

        // Act
        _lights.CastActorLights();

        // Assert
        Assert.That(AddedAt(11, 10), Is.GreaterThan(0));
        Assert.That(AddedAt(12, 10), Is.Zero);          // a solid wall's tile isn't entered
        Assert.That(AddedAt(13, 10), Is.Zero);
    }

    [Test]
    public void A_Shut_Door_Is_Lit_But_Stops_The_Light_And_An_Open_One_Doesnt()
    {
        // Arrange: door 0 a tile east
        Dim();
        _map.TileMap[11, 10] = (ushort)(GameConstants.BitDoor | 0);
        _doors.DoorObjList[0] = new DoorObject { TileX = 11, TileY = 10, Action = DoorAction.Closed };
        Lamp(10, 10);

        // Act / Assert
        _lights.CastActorLights();
        Assert.That(AddedAt(11, 10), Is.GreaterThan(0));
        Assert.That(AddedAt(12, 10), Is.Zero);

        _doors.DoorObjList[0].Action = DoorAction.Open;
        _lights.CastActorLights();
        Assert.That(AddedAt(12, 10), Is.GreaterThan(0));
    }

    [Test]
    public void Last_Frames_Light_Is_Gone_When_The_Lamp_Is()
    {
        // Arrange
        Dim();
        var lamp = Lamp(10, 10);
        _lights.CastActorLights();

        // Act
        lamp.IsRemoved = true;
        _lights.CastActorLights();

        // Assert
        Assert.That(AddedAt(10, 10), Is.Zero);
        Assert.That(_lights.HasLights, Is.False);
    }

    [Test]
    public void On_A_Level_With_No_Shading_No_Light_Is_Cast()
    {
        _lighting.SetShading(null);
        Lamp(10, 10);
        _lights.CastActorLights();
        Assert.That(_lights.HasLights, Is.False);
    }

    [Test]
    public void A_Colored_Light_Tints_What_It_Lights()
    {
        // Arrange
        Dim();
        Lamp(10, 10, color: "#FF0000");

        // Act
        _lights.CastActorLights();

        // Assert
        int middle = (((10 << Lighting.LightCellShift) + 4) << 9) + (10 << Lighting.LightCellShift) + 4;
        Assert.That(_lighting.CellTint[middle], Is.Not.Zero);
    }

    [Test]
    public void A_Set_Light_Replaces_The_Class_Light_Until_Set_Back()
    {
        // Arrange
        var lamp = Lamp(10, 10, intensity: 100, radius: 2);

        // Act / Assert
        Assert.That(_lights.SetLightAction(lamp, ["200", "", "#00FF00"]), Is.True);
        Assert.That(_lights.CurrentLight(lamp), Is.EqualTo((200, 2.0, (string?)"#00FF00")));

        Assert.That(_lights.SetLightAction(lamp, []), Is.True);
        Assert.That(_lights.CurrentLight(lamp), Is.EqualTo((100, 2.0, (string?)null)));
    }

    [TestCase("300")]
    [TestCase("100|-1")]
    [TestCase("100|2|green")]
    public void A_Set_Light_With_Bad_Arguments_Changes_Nothing(string arguments)
    {
        var args = arguments.Split('|');
        var lamp = Lamp(10, 10, intensity: 100, radius: 2);
        Assert.That(_lights.SetLightAction(lamp, args), Is.False);
        Assert.That(_lights.CurrentLight(lamp).Intensity, Is.EqualTo(100));
    }

    [Test]
    public void Light_Off_Puts_It_Out_Until_Set_Again()
    {
        // Arrange
        var lamp = Lamp(10, 10, intensity: 100);

        // Act / Assert
        _lights.LightOff(lamp);
        Assert.That(_lights.CurrentLight(lamp).Intensity, Is.Zero);
        _lights.SetLightAction(lamp, ["150"]);
        Assert.That(_lights.CurrentLight(lamp).Intensity, Is.EqualTo(150));
    }

    // A set light (light.mode: set) in the middle of a tile, and the level's planes for the
    // tiles' base lights it goes from (no zones)
    private Monster SetLamp(int x, int y, int intensity, double radius = 3)
    {
        typeof(MapManager).GetField("mapsegs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(_map, Enumerable.Range(0, MapManager.LevelPlanes).Select(_ => new ushort[MapConstants.ClassicMapArea]).ToArray());
        var lamp = Lamp(x, y, intensity, radius);
        lamp.Name = $"SetLamp{intensity}";
        lamp.Properties["light.mode"] = "set";
        return lamp;
    }

    // The light level at the middle of a tile: its base light, with what lights did to its cell
    private int LevelAt(int x, int y)
    {
        int cell = (((y << Lighting.LightCellShift) + Lighting.LightCells / 2) << 9) + (x << Lighting.LightCellShift) + Lighting.LightCells / 2;
        return Math.Clamp(_lighting.TileLevel[_map.TileIndex(x, y)] + _lighting.CellAdd[cell] + _lighting.CellSet[cell], 0, Lighting.MaxLightLevel);
    }

    [Test]
    public void A_Set_Light_Darkens_What_It_Reaches_On_A_Level_At_Full_Light()
    {
        // Arrange: no shading of the level's own
        _lighting.SetShading(new ShadingSettings("#000000", 0, 16, 0, 255));
        Assert.That(_lighting.Shading, Is.False);
        SetLamp(10, 10, intensity: 64);

        // Act
        _lights.CastActorLights();

        // Assert: shading's turned on for it; its own tile and the next take its light, far off the level's
        Assert.That(_lighting.Shading, Is.True);
        int full = Lighting.MaxLightLevel, its = Lighting.LightLevelOf(64);
        Assert.That(LevelAt(10, 10), Is.EqualTo(its));
        Assert.That(LevelAt(11, 10), Is.EqualTo(its));
        Assert.That(LevelAt(12, 10), Is.InRange(its + 1, full - 1));    // blending out
        Assert.That(LevelAt(14, 10), Is.EqualTo(full));                 // past its radius
    }

    [Test]
    public void A_Set_Light_Lightens_A_Dim_Level_And_Is_Stopped_By_Walls()
    {
        // Arrange
        Dim();
        _map.TileMap[12, 10] = 1;
        SetLamp(10, 10, intensity: 255);
        _lighting.UpdateLightGrid();

        // Act
        _lights.CastActorLights();

        // Assert
        Assert.That(LevelAt(10, 10), Is.EqualTo(Lighting.MaxLightLevel));
        Assert.That(LevelAt(13, 10), Is.EqualTo(_lighting.AmbientLevel));    // behind the wall
    }

    [Test]
    public void A_Set_Lights_Dark_Frame_Sets_Its_Lower_Light_And_None_Puts_It_Out()
    {
        // Arrange: on at 255; its state's light: 32 (a bulb's dark frame), then light: none
        Dim();
        var lamp = SetLamp(10, 10, intensity: 255);
        _lighting.UpdateLightGrid();
        lamp.CurrentState = new ActorStateFrame { StateName = "Off", Sprite = "BULB", FrameLetter = "B", Light = StateLight.Parse("32") };

        // Act / Assert
        _lights.CastActorLights();
        Assert.That(LevelAt(10, 10), Is.EqualTo(Lighting.LightLevelOf(32)));

        lamp.CurrentState = new ActorStateFrame { StateName = "Off", Sprite = "BULB", FrameLetter = "B", Light = StateLight.Parse("none") };
        _lights.CastActorLights();
        Assert.That(LevelAt(10, 10), Is.EqualTo(_lighting.AmbientLevel));
    }

    [Test]
    public void Where_Set_Lights_Overlap_The_One_Nearer_Has_The_Cell()
    {
        // Arrange: a bright one and a dark one three tiles apart
        Dim();
        SetLamp(10, 10, intensity: 255);
        SetLamp(13, 10, intensity: 0);
        _lighting.UpdateLightGrid();

        // Act
        _lights.CastActorLights();

        // Assert
        Assert.That(LevelAt(10, 10), Is.EqualTo(Lighting.MaxLightLevel));
        Assert.That(LevelAt(13, 10), Is.Zero);
    }

    [Test]
    public void The_Lights_Command_Says_What_Each_Lit_Actor_Gives_Off()
    {
        // Arrange
        Lamp(10, 10, intensity: 100, radius: 2, color: "#FF0000");
        _map.GetActors().AddLast(new Monster { Name = "Guard" });     // no light: not listed

        // Act
        var lines = _lights.DescribeActorLights().ToList();

        // Assert
        Assert.That(lines, Is.EqualTo(new[] { "Lamp100 at 10,10: 100 over 2 tiles, #FF0000" }));
    }
}
