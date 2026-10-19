using System.Runtime.InteropServices;
using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Tests;

public class SkyAndFlatsTests
{
    private const int Width = 8, Height = 10;

    private byte[] _screen = null!;
    private GCHandle _pinned;
    private RenderState _render = null!;
    private Lighting _lighting = null!;
    private SkyAndFlats _sky = null!;

    [SetUp]
    public void MakeView()
    {
        // An 8x10 view of color 1, looking east from the middle of tile (10, 10), eyes half a story up
        _screen = Enumerable.Repeat((byte)1, Width * Height).ToArray();
        _pinned = GCHandle.Alloc(_screen, GCHandleType.Pinned);
        var video = new VideoManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")))
        {
            BufferPitch = Width,
            YLookup = Enumerable.Range(0, Height).Select(y => (uint)(y * Width)).ToArray(),
        };
        _render = new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.NoShade)
        {
            VbufPtr = _pinned.AddrOfPinnedObject(),
            ViewWidth = Width, ViewHeight = Height, CenterY = Height / 2, BaseCenterY = Height / 2,
            ViewX = (int)(10.5 * MapConstants.TileGlobal), ViewY = (int)(10.5 * MapConstants.TileGlobal),
            HeightNumerator = 0x10000, PixelAngle = new short[Width], ViewEyeZ = 32,
        };
        _lighting = new Lighting(null!, video, null!, new LightingHooks(() => 0, () => { }, _ => { }));
        _lighting.SetShading(null);
        var actorLights = new ActorLights(null!, null!, _lighting, new GameSession());
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, new GameSession(), null)
        {
            // No level is loaded: an open one with no walls, which DrawFlats reads where a tile has no flat
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        _sky = new SkyAndFlats(null!, null!, video, _map, _lighting, actorLights, _render, new GameSession(), _ => null);
    }

    [TearDown]
    public void Unpin() => _pinned.Free();

    private MapManager _map = null!;

    private byte At(int x, int y) => _screen[y * Width + x];

    [TestCase(32, 0.0)]         // at the eyes
    [TestCase(0, 1.0)]          // the floor: half a story down
    [TestCase(128, 3.0)]        // the top of a two-story wall
    public void Distances_Up_Or_Down_From_The_Eyes_Are_In_Half_Stories(int z, double halves)
    {
        Assert.That(_sky.EyeHalves(z), Is.EqualTo(halves));
    }

    [Test]
    public void The_Ceiling_Is_At_The_Top_Of_The_Levels_Walls()
    {
        Assert.That((_sky.FloorHalves, _sky.CeilingHalves), Is.EqualTo((1.0, 1.0)));
        _map.LevelWallStories = 2;
        Assert.That(_sky.CeilingHalves, Is.EqualTo(3.0));
    }

    [Test]
    public void A_Floor_Flat_Is_Drawn_Below_The_Horizon_And_Nothing_Above()
    {
        // Arrange: every tile's floor color 7
        var stone = new TextureAsset { Width = 64, Height = 64, RawData = Enumerable.Repeat((byte)7, 64 * 64).ToArray() };
        var flats = Enumerable.Repeat<TextureAsset?>(stone, MapConstants.ClassicMapArea).ToArray();

        // Act
        _sky.SetupFlatColumns();
        _sky.DrawFlats(flats, 3, Height / 2, Height - 1, _sky.FloorHalves, behind: false);

        // Assert: the rows nearest the horizon look off the map (left as they were); the near ones are the flat
        Assert.That(Enumerable.Range(0, Height / 2).All(y => Enumerable.Range(0, Width).All(x => At(x, y) == 1)), Is.True);
        Assert.That(Enumerable.Range(Height - 2, 2).All(y => Enumerable.Range(0, Width).All(x => At(x, y) == 7)), Is.True);
    }

    [Test]
    public void Where_A_Tile_Has_No_Flat_The_Color_Under_It_Stays()
    {
        // Act: no flats anywhere, and no light zones or actor light to recolor it
        _sky.SetupFlatColumns();
        _sky.DrawFlats(new TextureAsset?[MapConstants.ClassicMapArea], 3, Height / 2, Height - 1, _sky.FloorHalves, behind: false);

        // Assert
        Assert.That(_screen.All(pixel => pixel == 1), Is.True);
    }

    [TestCase(8, 0, -8, 0)]         // moved 8 texels east: each pixel shows the texel 8 west of it
    [TestCase(0, 5, 0, -5)]         // moved 5 south: the texel 5 north of it
    public void A_Scrolled_Flat_Shows_The_Texel_That_Has_Moved_There(int east, int south, int du, int dv)
    {
        // Arrange: a flat whose texel (u, v) is u * 2 + v % 2, so both ways show; every tile moved
        var ramp = new TextureAsset { Width = 64, Height = 64, RawData = new byte[64 * 64] };
        for (int u = 0; u < 64; u++)
            for (int v = 0; v < 64; v++)
                ramp.RawData[u * 64 + v] = (byte)(u * 2 + v % 2 == SkyAndFlats.FlatMask ? 0 : u * 2 + v % 2);
        var flats = Enumerable.Repeat<TextureAsset?>(ramp, MapConstants.ClassicMapArea).ToArray();
        var none = new int[MapConstants.ClassicMapArea];
        var scrollx = Enumerable.Repeat(east * 1024, MapConstants.ClassicMapArea).ToArray();
        var scrolly = Enumerable.Repeat(south * 1024, MapConstants.ClassicMapArea).ToArray();
        _sky.SetupFlatColumns();
        _sky.DrawFlats(flats, 3, Height / 2, Height - 1, _sky.FloorHalves, behind: false, none, none);
        var still = (byte[])_screen.Clone();

        // Act
        _sky.DrawFlats(flats, 3, Height / 2, Height - 1, _sky.FloorHalves, behind: false, scrollx, scrolly);

        // Assert: the near rows' texels are the unscrolled ones, offset
        for (int y = Height - 2; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                int u = still[y * Width + x] / 2, v = still[y * Width + x] % 2;
                int expected = ((u + du) & 63) * 2 + ((v + dv) & 1);
                Assert.That(At(x, y), Is.EqualTo(expected), $"pixel {x},{y}");
            }
    }

    // A floor sky five rows tall whose row r is color 10 + r, in every column
    private SkyAndFlats FloorSkyView(bool mirror)
    {
        var picture = new TextureAsset { Width = 64, Height = 5, RawData = new byte[64 * 5] };
        for (int u = 0; u < 64; u++)
            for (int r = 0; r < 5; r++)
                picture.RawData[u * 5 + r] = (byte)(10 + r);
        var video = new VideoManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")))
        {
            BufferPitch = Width,
            YLookup = Enumerable.Range(0, Height).Select(y => (uint)(y * Width)).ToArray(),
        };
        return new SkyAndFlats(null!, null!, video, _map, _lighting, new ActorLights(null!, null!, _lighting, new GameSession()),
            _render, new GameSession(), name => name == "RIVER" ? picture : null)
        {
            LevelFloorSky = "RIVER",
            FloorSkyMirror = mirror,
        };
    }

    [TestCase(false, new byte[] { 10, 11, 12, 13, 14 })]   // its top row at the horizon, running down
    [TestCase(true, new byte[] { 14, 13, 12, 11, 10 })]    // mirrored: its bottom row at the horizon
    public void A_Floor_Sky_Fills_The_View_Below_The_Horizon(bool mirror, byte[] rows)
    {
        // Arrange
        var view = FloorSkyView(mirror);

        // Act
        Assert.That(view.LoadFloorSky(), Is.True);
        view.DrawFloorSky();

        // Assert: above the horizon as it was; below, the floor sky's rows in every column
        Assert.That(Enumerable.Range(0, Height / 2).All(y => Enumerable.Range(0, Width).All(x => At(x, y) == 1)), Is.True);
        for (int y = Height / 2; y < Height; y++)
            Assert.That(Enumerable.Range(0, Width).Select(x => At(x, y)), Is.All.EqualTo(rows[y - Height / 2]), $"row {y}");
    }

    [Test]
    public void No_Floor_Sky_Or_None_Loads_Nothing()
    {
        var view = FloorSkyView(mirror: false);
        view.LevelFloorSky = "none";
        Assert.That(view.LoadFloorSky(), Is.False);
        view.LevelFloorSky = null;
        Assert.That(view.LoadFloorSky(), Is.False);
    }

    [Test]
    public void Where_A_Floor_Tile_Has_No_Flat_The_Floor_Sky_Shows()
    {
        // Arrange: the floor sky drawn, then no flats anywhere, drawn over it with the sky behind
        var view = FloorSkyView(mirror: false);
        view.LoadFloorSky();
        view.DrawFloorSky();
        var sky = (byte[])_screen.Clone();

        // Act
        view.SetupFlatColumns();
        view.DrawFlats(new TextureAsset?[MapConstants.ClassicMapArea], 3, Height / 2, Height - 1, view.FloorHalves, behind: true);

        // Assert
        Assert.That(_screen, Is.EqualTo(sky));
    }

    [Test]
    public void A_Flats_See_Through_Pixels_Leave_What_Is_There()
    {
        // Arrange: a see-through flat
        var grate = new TextureAsset { Width = 64, Height = 64, RawData = Enumerable.Repeat(SkyAndFlats.FlatMask, 64 * 64).ToArray() };

        // Act
        _sky.SetupFlatColumns();
        _sky.DrawFlats(Enumerable.Repeat<TextureAsset?>(grate, MapConstants.ClassicMapArea).ToArray(), 3, Height / 2, Height - 1, _sky.FloorHalves, behind: false);

        // Assert
        Assert.That(_screen.All(pixel => pixel == 1), Is.True);
    }
}
