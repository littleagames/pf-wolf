using System.Runtime.InteropServices;
using PFWolf.Assets;

namespace PFWolf.Tests;

public class SpriteScalerTests
{
    private const int Width = 8, Height = 10;

    private byte[] _screen = null!;
    private GCHandle _pinned;
    private SpriteScaler _scaler = null!;
    private RenderState _render = null!;

    private static readonly byte[] Unshaded = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

    [SetUp]
    public void MakeScreen()
    {
        // An 8x10 screen of color 10, the view all of it
        _screen = Enumerable.Repeat((byte)10, Width * Height).ToArray();
        _pinned = GCHandle.Alloc(_screen, GCHandleType.Pinned);
        var video = new VideoManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")))
        {
            bufferPitch = Width,
            ylookup = Enumerable.Range(0, Height).Select(y => (uint)(y * Width)).ToArray(),
        };
        _render = new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Unshaded)
        {
            vbufPtr = _pinned.AddrOfPinnedObject(),
            screenofs = 0,
            viewwidth = Width,
            viewheight = Height,
        };
        _scaler = new SpriteScaler(null!, video, null!, _render, half => half);
    }

    [TearDown]
    public void Unpin() => _pinned.Free();

    // A sprite one column wide and two rows tall: color 5 on top, see-through below
    private static SpriteAsset Sprite() => new() { Width = 1, Height = 2, RawData = [5, 7], OpacityMask = [1, 0] };

    private byte At(int x, int y) => _screen[y * Width + x];

    [Test]
    public void A_Column_Is_Scaled_Onto_The_Screen_Skipping_What_Is_See_Through()
    {
        // Act: two pixels a texel, from row 2
        _scaler.ScaleColumn(3, 2, 2 << 16, Sprite(), 0, Unshaded, 0);

        // Assert
        Assert.That(Enumerable.Range(0, Height).Select(y => At(3, y)), Is.EqualTo(new byte[] { 10, 10, 5, 5, 10, 10, 10, 10, 10, 10 }));
        Assert.That(At(2, 2), Is.EqualTo(10));      // only its own column
    }

    [Test]
    public void It_Goes_Through_The_Light_Row_Given()
    {
        // Arrange: a row that adds 100
        var bright = Enumerable.Range(0, 512).Select(i => (byte)((i % 256) + 100)).ToArray();

        // Act: from the second remap in the row
        _scaler.ScaleColumn(3, 0, 1 << 16, Sprite(), 0, bright, 256);

        // Assert
        Assert.That(At(3, 0), Is.EqualTo(105));
    }

    [Test]
    public void It_Is_Clipped_To_The_View()
    {
        // Act: starting above the view, four pixels a texel, and a view only 3 high
        _render.viewheight = 3;
        _scaler.ScaleColumn(3, -2, 4 << 16, Sprite(), 0, Unshaded, 0);

        // Assert: its top two rows are off the view, and nothing goes below it
        Assert.That(Enumerable.Range(0, Height).Select(y => At(3, y)), Is.EqualTo(new byte[] { 5, 5, 10, 10, 10, 10, 10, 10, 10, 10 }));
    }

    [Test]
    public void A_Cloaked_Sprite_Darkens_What_Is_Behind_It()
    {
        // Arrange: a light table that halves
        var dim = Enumerable.Range(0, 256).Select(i => (byte)(i / 2)).ToArray();

        // Act
        _scaler.ScaleColumn(3, 0, 1 << 16, Sprite(), 0, dim, 0, cloaked: true);

        // Assert
        Assert.That(At(3, 0), Is.EqualTo(5));       // what was there (10), halved
    }

    [TestCase(32, 32, 32)]          // half a story: as many pixels as texels
    [TestCase(32, 64, 64)]
    [TestCase(10, 16, 5)]
    [TestCase(-3, 32, -3)]
    public void Texels_Turn_Into_Pixels_At_The_Sprites_Size(int texels, int half, int pixels)
    {
        Assert.That(SpriteScaler.TexelsToPixels(texels, half), Is.EqualTo(pixels));
    }
}
