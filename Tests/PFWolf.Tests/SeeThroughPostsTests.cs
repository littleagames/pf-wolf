using System.Runtime.InteropServices;
using PFWolf.Assets;

namespace PFWolf.Tests;

public class SeeThroughPostsTests
{
    private const int Width = 8, Height = 10;
    private const int Floor = 8;        // the screen row the floor is on, whatever the height

    private byte[] _screen = null!;
    private GCHandle _pinned;
    private RenderState _render = null!;
    private SeeThroughPosts _posts = null!;

    private static readonly byte[] Unshaded = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

    [SetUp]
    public void MakeScreen()
    {
        // An 8x10 screen of color 10, the view all of it
        _screen = Enumerable.Repeat((byte)10, Width * Height).ToArray();
        _pinned = GCHandle.Alloc(_screen, GCHandleType.Pinned);
        var video = new VideoManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")))
        {
            BufferPitch = Width,
            YLookup = Enumerable.Range(0, Height).Select(y => (uint)(y * Width)).ToArray(),
        };
        _render = new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Unshaded)
        {
            VbufPtr = _pinned.AddrOfPinnedObject(),
            ScreenOfs = 0,
            ViewWidth = Width,
            ViewHeight = Height,
        };
        _render.SizeCovers(Width);
        _posts = new SeeThroughPosts(video, new Lighting(null!, null!, null!, null!), _render, _ => Floor);
    }

    [TearDown]
    public void Unpin() => _pinned.Free();

    // A door's texture all one color, its column 0 given by texels up from the floor
    private static TextureAsset Texture(byte color, params (int Texel, byte Color)[] texels)
    {
        var data = Enumerable.Repeat(color, 64 * 64).ToArray();
        foreach (var (texel, c) in texels)
            data[63 - texel] = c;
        return new TextureAsset { RawData = data, Masked = true };
    }

    // Keeps a see-through door in column x, farther than those kept before it (as the trace finds them)
    private void Keep(int x, short height, TextureAsset texture)
    {
        int at = x * RenderState.MaxSeeThrough + _render.SeeThroughCount[x]++;
        _render.SeeThroughHeight[at] = height;
        _render.SeeThroughStories[at] = 1;
        _render.SeeThroughLower[at] = _render.SeeThroughUpper[at] = texture;
        _render.SeeThroughLight[at] = Unshaded;
        _render.AnySeeThrough = true;
    }

    private byte[] Column(int x) => Enumerable.Range(0, Height).Select(y => _screen[y * Width + x]).ToArray();

    [Test]
    public void A_Door_Is_Drawn_Up_From_The_Floor_Leaving_Its_Clear_Pixels()
    {
        // Arrange: a story 4 pixels high (height 16: half a story is 2), so rows 7 up to 4 show
        // texels 0, 16, 32 and 48; texel 16 is clear
        Keep(3, 16, Texture(0, (0, 1), (16, 0xff), (32, 3), (48, 4)));

        // Act
        _posts.DrawRest();

        // Assert
        Assert.That(Column(3), Is.EqualTo(new byte[] { 10, 10, 10, 10, 4, 3, 10, 1, 10, 10 }));
        Assert.That(Column(2), Is.All.EqualTo(10));
    }

    [Test]
    public void Only_The_Doors_Farther_Than_A_Sprite_Are_Drawn_Before_It()
    {
        // Arrange: a near door (a story 8 high) and a farther one (4 high) behind it
        Keep(3, 32, Texture(20));
        Keep(3, 16, Texture(30));

        // Act: something 24 high, between them
        _posts.DrawBeyond(3, 24);

        // Assert: the far one only, and the near one is still to draw
        Assert.That(Column(3), Is.EqualTo(new byte[] { 10, 10, 10, 10, 30, 30, 30, 30, 10, 10 }));
        Assert.That(_render.SeeThroughCount[3], Is.EqualTo(1));

        // Act: the rest
        _posts.DrawRest();

        // Assert: the near one over it
        Assert.That(Column(3), Is.EqualTo(new byte[] { 20, 20, 20, 20, 20, 20, 20, 20, 10, 10 }));
        Assert.That(_render.SeeThroughCount[3], Is.Zero);
        Assert.That(_render.AnySeeThrough, Is.False);
    }

    [Test]
    public void Nothing_Is_Drawn_Twice_Or_Without_A_Door_Kept()
    {
        // Arrange
        Keep(3, 16, Texture(30));
        _posts.DrawRest();
        _screen[7 * Width + 3] = 10;

        // Act: a sprite drawn after them all, outside a frame
        _posts.DrawBeyond(3, int.MaxValue);
        _posts.DrawBeyond(99, int.MaxValue);

        // Assert
        Assert.That(_screen[7 * Width + 3], Is.EqualTo(10));
    }
}
