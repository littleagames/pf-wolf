using System.Reflection;
using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities;
using PFWolf.Entities.Actors;
using PFWolf.Rendering;

namespace PFWolf.Tests;

public class ScrollersTests
{
    private const int Texel = (int)MapConstants.TileGlobal / GameConstants.TextureSize;

    private MapManager _map = null!;
    private Scrollers _scrollers = null!;

    [SetUp]
    public void SetUp()
    {
        _map = new MapManager(new Lazy<AssetManager>(() => new AssetManager()), null!, new GameSession(), null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            MapWidth = MapConstants.ClassicMapSize, MapHeight = MapConstants.ClassicMapSize,
        };
        // empty planes, as a loaded level has
        typeof(MapManager).GetField("mapsegs", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_map, Enumerable.Range(0, MapManager.LevelPlanes).Select(_ => new ushort[MapConstants.ClassicMapArea]).ToArray());
        _scrollers = _map.Scrollers;
    }

    private void Init(Dictionary<int, ScrollerInfo> scrollers) => _scrollers.InitLevel(new MapInfo { Scrollers = scrollers });

    private static int Tile(int x, int y) => (y << 6) + x;

    [Test]
    public void A_Running_Scroller_Moves_Its_Tagged_Tiles_By_Its_Speed_A_Tic()
    {
        // Arrange
        _map.SetTag(5, 6, 3);
        Init(new() { [3] = new ScrollerInfo { X = 2, Y = 0.5 } });

        // Act
        _scrollers.Tick(4);

        // Assert: 8 texels right and east, 2 down and south; an untagged tile not at all
        Assert.That(_scrollers.WallColumn[Tile(5, 6)], Is.EqualTo(8));
        Assert.That(_scrollers.WallRow[Tile(5, 6)], Is.EqualTo(2));
        Assert.That(_scrollers.FloorX[Tile(5, 6)], Is.EqualTo(8 * Texel));
        Assert.That(_scrollers.FloorY[Tile(5, 6)], Is.EqualTo(2 * Texel));
        Assert.That(_scrollers.CeilingX[Tile(5, 6)], Is.EqualTo(8 * Texel));
        Assert.That(_scrollers.WallColumn[Tile(5, 7)], Is.Zero);
    }

    [Test]
    public void A_Scroller_Moves_Only_The_Surfaces_It_Names()
    {
        // Arrange
        _map.SetTag(1, 1, 2);
        Init(new() { [2] = new ScrollerInfo { X = 1, Floor = false, Ceiling = false } });

        // Act
        _scrollers.Tick(3);

        // Assert
        Assert.That(_scrollers.WallColumn[Tile(1, 1)], Is.EqualTo(3));
        Assert.That(_scrollers.FloorX[Tile(1, 1)], Is.Zero);
        Assert.That(_scrollers.CeilingX[Tile(1, 1)], Is.Zero);
        Assert.That((_scrollers.AnyWalls, _scrollers.AnyFloors, _scrollers.AnyCeilings), Is.EqualTo((true, false, false)));
    }

    [Test]
    public void An_Inactive_Scroller_Waits_Until_It_Is_Started()
    {
        // Arrange
        _map.SetTag(1, 1, 2);
        Init(new() { [2] = new ScrollerInfo { X = 1, Active = false } });

        // Act
        _scrollers.Tick(10);
        int before = _scrollers.WallColumn[Tile(1, 1)];
        _scrollers.Level[2].Active = true;
        _scrollers.Tick(10);

        // Assert
        Assert.That(before, Is.Zero);
        Assert.That(_scrollers.WallColumn[Tile(1, 1)], Is.EqualTo(10));
    }

    [Test]
    public void Going_Backwards_Wraps_Round_The_Tile()
    {
        // Arrange
        _map.SetTag(1, 1, 2);
        Init(new() { [2] = new ScrollerInfo { X = -1, Y = -1 } });

        // Act
        _scrollers.Tick(1);

        // Assert
        Assert.That(_scrollers.WallColumn[Tile(1, 1)], Is.EqualTo(63));
        Assert.That(_scrollers.FloorX[Tile(1, 1)], Is.EqualTo(63 * Texel));
        Assert.That(_scrollers.WallRow[Tile(1, 1)], Is.EqualTo(Scrollers.RowWrap - 1));
    }

    [Test]
    public void A_Wall_Column_Slides_Right_And_Wraps()
    {
        // Arrange: moved 3 texels right
        _map.SetTag(1, 1, 2);
        Init(new() { [2] = new ScrollerInfo { X = 1 } });
        _scrollers.Tick(3);

        // Act / Assert: column 10 shows what was at 7, column 1 what was at 62
        Assert.That(_scrollers.Column(1, 1, 10 << GameConstants.TextureShift), Is.EqualTo(7 << GameConstants.TextureShift));
        Assert.That(_scrollers.Column(1, 1, 1 << GameConstants.TextureShift), Is.EqualTo(62 << GameConstants.TextureShift));
        Assert.That(_scrollers.Column(2, 1, 10 << GameConstants.TextureShift), Is.EqualTo(10 << GameConstants.TextureShift));
    }

    [Test]
    public void The_Tiles_Follow_A_Tag_That_Moves()
    {
        // Arrange
        _map.SetTag(1, 1, 2);
        Init(new() { [2] = new ScrollerInfo { X = 1 } });
        _scrollers.Tick(1);

        // Act: a pushwall carries it a tile east
        _map.MoveTag(1, 1, 2, 1);
        _scrollers.Tick(1);

        // Assert
        Assert.That(_scrollers.WallColumn[Tile(1, 1)], Is.Zero);
        Assert.That(_scrollers.WallColumn[Tile(2, 1)], Is.EqualTo(2));
    }

    [Test]
    public void Switches_Stop_Start_Toggle_And_Set_A_Scroller_By_Their_Tag()
    {
        // Arrange
        _map.SetTag(1, 1, 4);
        Init(new() { [4] = new ScrollerInfo { X = 1 } });
        Scrollers.RegisterActions(() => _scrollers);
        var trigger = new TriggerActivation(0, 0, ControlDir.North, null!, 4);

        // Act / Assert
        Assert.That(MapTriggerRegistry.Invoke("A_StopScroll", [], trigger), Is.True);
        Assert.That(_scrollers.Level[4].Active, Is.False);
        Assert.That(MapTriggerRegistry.Invoke("A_ToggleScroll", [], trigger), Is.True);
        Assert.That(_scrollers.Level[4].Active, Is.True);
        Assert.That(MapTriggerRegistry.Invoke("A_SetScroll", ["", "0", "-2.5"], trigger), Is.True);
        Assert.That((_scrollers.Level[4].X, _scrollers.Level[4].Y), Is.EqualTo((0.0, -2.5)));

        // a tag the map has no scroller for: refused, but A_SetScroll makes one
        Assert.That(MapTriggerRegistry.Invoke("A_StartScroll", ["9"], trigger), Is.False);
        Assert.That(MapTriggerRegistry.Invoke("A_SetScroll", ["9", "1"], trigger), Is.True);
        Assert.That(_scrollers.Level[9].Active, Is.True);
    }

    [Test]
    public void Saved_Scrollers_Read_Back_As_They_Were()
    {
        // Arrange
        _map.SetTag(1, 1, 2);
        Init(new() { [2] = new ScrollerInfo { X = 1.5, Y = -0.25, Ceiling = false, Active = false } });
        _scrollers.Level[2].OffsetX = 12.75;
        using var stream = new MemoryStream();
        _scrollers.Write(new BinaryWriter(stream));
        stream.Position = 0;

        // Act
        var read = Scrollers.Read(new BinaryReader(stream));

        // Assert
        var scroller = read[2];
        Assert.That((scroller.X, scroller.Y, scroller.Walls, scroller.Floor, scroller.Ceiling, scroller.Active, scroller.OffsetX),
            Is.EqualTo((1.5, -0.25, true, true, false, false, 12.75)));
        Assert.That(stream.Position, Is.EqualTo(stream.Length));
    }
}
