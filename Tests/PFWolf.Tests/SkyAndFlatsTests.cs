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
            bufferPitch = Width,
            ylookup = Enumerable.Range(0, Height).Select(y => (uint)(y * Width)).ToArray(),
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
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, new GameSession());
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
        _map.wallstories = 2;
        Assert.That(_sky.CeilingHalves, Is.EqualTo(3.0));
    }

    [Test]
    public void A_Floor_Flat_Is_Drawn_Below_The_Horizon_And_Nothing_Above()
    {
        // Arrange: every tile's floor color 7
        var stone = new TextureAsset { Width = 64, Height = 64, RawData = Enumerable.Repeat((byte)7, 64 * 64).ToArray() };
        var flats = Enumerable.Repeat<TextureAsset?>(stone, MapManager.MapArea).ToArray();

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
        _sky.DrawFlats(new TextureAsset?[MapManager.MapArea], 3, Height / 2, Height - 1, _sky.FloorHalves, behind: false);

        // Assert
        Assert.That(_screen.All(pixel => pixel == 1), Is.True);
    }

    [Test]
    public void A_Flats_See_Through_Pixels_Leave_What_Is_There()
    {
        // Arrange: a see-through flat
        var grate = new TextureAsset { Width = 64, Height = 64, RawData = Enumerable.Repeat(SkyAndFlats.FlatMask, 64 * 64).ToArray() };

        // Act
        _sky.SetupFlatColumns();
        _sky.DrawFlats(Enumerable.Repeat<TextureAsset?>(grate, MapManager.MapArea).ToArray(), 3, Height / 2, Height - 1, _sky.FloorHalves, behind: false);

        // Assert
        Assert.That(_screen.All(pixel => pixel == 1), Is.True);
    }
}
