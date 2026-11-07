using System.Runtime.InteropServices;
using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Tests;

/// <summary>Walls below the floor (game-info story-level), seen down pits (mapdefs flats pits)</summary>
public class StoryLevelTests
{
    private const int Tile = 1 << MapConstants.TileShift;
    private const int Width = 4, Height = 40, Horizon = 10;

    private byte[] _screen = null!;
    private GCHandle _pinned;
    private MapManager _map = null!;
    private RenderState _render = null!;
    private WallPosts _posts = null!;
    private SkyAndFlats _sky = null!;

    private static readonly byte[] Unshaded = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

    [SetUp]
    public void MakeView()
    {
        // A 4x40 view of color 200, looking east from the middle of tile (10, 10), the horizon at
        // row 10, eyes half a story up
        _screen = Enumerable.Repeat((byte)200, Width * Height).ToArray();
        _pinned = GCHandle.Alloc(_screen, GCHandleType.Pinned);
        var video = new VideoManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")))
        {
            BufferPitch = Width,
            YLookup = Enumerable.Range(0, Height).Select(y => (uint)(y * Width)).ToArray(),
        };
        _render = new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.NoShade)
        {
            VbufPtr = _pinned.AddrOfPinnedObject(),
            ViewWidth = Width, ViewHeight = Height, CenterY = Horizon, BaseCenterY = Horizon,
            ViewX = 10 * Tile + Tile / 2, ViewY = 10 * Tile + Tile / 2, ViewCos = 0x10000, ViewSin = 0,
            HeightNumerator = 0x100000, PixelAngle = new short[Width], WallHeight = new short[Width], ViewEyeZ = 32,
            XTileStep = 1, YTileStep = 1,
        };
        _render.SizeCovers(Width);
        var lighting = new Lighting(null!, video, null!, new LightingHooks(() => 0, () => { }, _ => { }));
        lighting.SetShading(null);
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, new GameSession(), null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            StoryLevel = 3,
        };
        _posts = new WallPosts(video, _map, lighting, null!, _render, null!, new GameSession(),
            (int _, int _, string _, out bool mirrored) => { mirrored = false; return null; });
        _sky = new SkyAndFlats(null!, null!, video, _map, lighting, new ActorLights(null!, null!, lighting, new GameSession()), _render, new GameSession(), _ => null);
    }

    [TearDown]
    public void Unpin() => _pinned.Free();

    private byte At(int x, int y) => _screen[y * Width + x];

    // A texture whose every column shows, at each texel up from its bottom, that texel's number
    private static TextureAsset Numbered()
    {
        var data = new byte[64 * 64];
        for (int column = 0; column < 64; column++)
            for (int texel = 0; texel < 64; texel++)
                data[column * 64 + 63 - texel] = (byte)texel;
        return new TextureAsset { Width = 64, Height = 64, RawData = data };
    }

    private void MakePit(int x, int y, TextureAsset side)
    {
        _map.PitSide[_map.TileIndex(x, y)] = side;
        _map.HasPits = true;
        _render.PitsOn = true;
    }

    [Test]
    public void Stories_Below_The_Floor_Run_Down_From_The_Floor_Line_Repeating_The_Texture()
    {
        // Arrange: half a story is 4 pixels, so the floor line is row 14 and a story 8 rows
        _render.PixX = 0;

        // Act: two stories below the floor
        _posts.DrawPost(0, 4, -2, 0, Numbered(), 0, Numbered(), 0, Unshaded, 0);

        // Assert: nothing above the floor line; each story's bottom row shows texel 0 (8 texels a row)
        Assert.That(At(0, 13), Is.EqualTo(200));
        Assert.That(At(0, 14), Is.EqualTo(56));         // just under the floor: the top of story -1
        Assert.That(At(0, 21), Is.EqualTo(0));          // story -1's bottom
        Assert.That(At(0, 22), Is.EqualTo(56));         // story -2's top
        Assert.That(At(0, 29), Is.EqualTo(0));          // story -2's bottom, at the ground
        Assert.That(At(0, 30), Is.EqualTo(200));
    }

    [Test]
    public void A_Post_Below_The_Floor_Is_Only_Drawn_In_Its_Clip_Rows()
    {
        // Act
        _posts.DrawPost(0, 4, -2, 0, Numbered(), 0, Numbered(), 0, Unshaded, 0, cliptop: 18, clipbottom: 25);

        // Assert: the rows drawn show the same texels they would unclipped
        Assert.That(At(0, 17), Is.EqualTo(200));
        Assert.That((At(0, 18), At(0, 21), At(0, 22), At(0, 25)), Is.EqualTo(((byte)24, (byte)0, (byte)56, (byte)32)));
        Assert.That(At(0, 26), Is.EqualTo(200));
    }

    [Test]
    public void A_Patch_On_Every_Story_Repeats_Under_The_Floor_In_Step_With_The_Wall()
    {
        // Act: a story-0 patch over two stories below the floor
        _posts.DrawPatch(0, 4, -2, 0, Numbered(), 0, 0, Unshaded, 0);

        // Assert: as the wall's own texture lies there
        Assert.That((At(0, 14), At(0, 21), At(0, 22), At(0, 29)), Is.EqualTo(((byte)56, (byte)0, (byte)56, (byte)0)));
    }

    [Test]
    public void A_Patch_On_A_Numbered_Story_Is_Not_Drawn_Under_The_Floor()
    {
        _posts.DrawPatch(0, 4, -2, 0, Numbered(), 0, 1, Unshaded, 0);
        Assert.That(_screen.All(pixel => pixel == 200), Is.True);
    }

    [Test]
    public void Leaving_A_Pit_For_Open_Floor_Queues_Its_Side_In_The_Pits_Rows()
    {
        // Arrange: a pit on tile 12, open floor on 13; tracing east
        var side = Numbered();
        MakePit(12, 10, side);
        _posts.StartPits();

        // Act: into the pit at x = 12, out of it at x = 13
        _posts.CrossPitEdge(12, 10, 12 * Tile, 10 * Tile + Tile / 2, vertical: true);
        _posts.CrossPitEdge(13, 10, 13 * Tile, 10 * Tile + Tile / 2, vertical: true);

        // Assert: the side's stories under the floor, from the far edge's floor line down to the near edge's
        short near = _posts.HeightAt(12 * Tile, 10 * Tile + Tile / 2), far = _posts.HeightAt(13 * Tile, 10 * Tile + Tile / 2);
        Assert.That(_render.PostCount, Is.EqualTo(1));
        Assert.That((_render.PostFirstStory[0], _render.PostStories[0], _render.PostLower[0]), Is.EqualTo(((sbyte)-2, (byte)0, side)));
        Assert.That(_render.PostHeights[0], Is.EqualTo(far));
        Assert.That(_render.PostClipTop[0], Is.EqualTo(_posts.FloorRow(far >> 3)));
        Assert.That(_render.PostClipBottom[0], Is.EqualTo(_posts.FloorRow(near >> 3) - 1));
        Assert.That(_render.PitOpen, Is.False);
    }

    [Test]
    public void Leaving_A_Pit_For_A_Wall_Leaves_Its_Rows_For_The_Walls_Stories_Below()
    {
        // Arrange: a pit on tile 12 with a wall on 13
        MakePit(12, 10, Numbered());
        _map.TileMap[13, 10] = 1;
        var brick = Numbered();
        _posts.StartPits();
        _posts.CrossPitEdge(12, 10, 12 * Tile, 10 * Tile + Tile / 2, vertical: true);

        // Act: the trace meets the wall, which queues its post
        _posts.CrossPitEdge(13, 10, 13 * Tile, 10 * Tile + Tile / 2, vertical: true);
        (_render.XIntercept, _render.YIntercept) = (13 * Tile, 10 * Tile + Tile / 2);
        _render.PostHeight = _posts.CalcHeight();
        (_render.PostSource, _render.HitStories) = (brick, 1);
        _posts.ScalePost(below: true);

        // Assert: the wall, then its two stories under the floor, clipped to the pit's rows
        Assert.That(_render.PostCount, Is.EqualTo(2));
        Assert.That((_render.PostFirstStory[0], _render.PostStories[0]), Is.EqualTo(((sbyte)0, (byte)1)));
        Assert.That((_render.PostFirstStory[1], _render.PostStories[1], _render.PostLower[1]), Is.EqualTo(((sbyte)-2, (byte)0, brick)));
        Assert.That(_render.PostClipTop[1], Is.EqualTo(_posts.FloorRow(_render.PostHeight >> 3)));
        Assert.That(_render.PitBeforeWall, Is.False);
    }

    [Test]
    public void A_Wall_With_No_Pit_In_Front_Has_No_Stories_Below()
    {
        // Arrange: a pit on the level, but not before this wall
        MakePit(20, 20, Numbered());
        _map.TileMap[13, 10] = 1;
        _posts.StartPits();
        _posts.CrossPitEdge(13, 10, 13 * Tile, 10 * Tile + Tile / 2, vertical: true);
        (_render.XIntercept, _render.YIntercept) = (13 * Tile, 10 * Tile + Tile / 2);
        _render.PostHeight = _posts.CalcHeight();
        (_render.PostSource, _render.HitStories) = (Numbered(), 1);

        // Act
        _posts.ScalePost(below: true);

        // Assert
        Assert.That(_render.PostCount, Is.EqualTo(1));
    }

    [Test]
    public void Standing_Over_A_Pit_Its_Rows_Run_To_The_Bottom_Of_The_View()
    {
        // Arrange: the view's own tile and the next are a pit; a smaller view, so its far edge is on screen
        _render.HeightNumerator = 0x10000;
        _render.FocalTx = 10;
        _render.FocalTy = 10;
        MakePit(10, 10, Numbered());
        MakePit(11, 10, Numbered());

        // Act
        _posts.StartPits();
        _posts.CrossPitEdge(11, 10, 11 * Tile, 10 * Tile + Tile / 2, vertical: true);
        _posts.CrossPitEdge(12, 10, 12 * Tile, 10 * Tile + Tile / 2, vertical: true);

        // Assert
        Assert.That(_render.PostCount, Is.EqualTo(1));
        Assert.That(_render.PostClipBottom[0], Is.EqualTo(Height - 1));
    }

    [Test]
    public void Past_The_Nearest_Wall_Pits_Are_Out_Of_Sight()
    {
        // Arrange
        MakePit(12, 10, Numbered());
        _posts.StartPits();
        _render.PastWall = true;

        // Act
        _posts.CrossPitEdge(12, 10, 12 * Tile, 10 * Tile + Tile / 2, vertical: true);
        _posts.CrossPitEdge(13, 10, 13 * Tile, 10 * Tile + Tile / 2, vertical: true);

        // Assert
        Assert.That((_render.PitOpen, _render.PostCount), Is.EqualTo((false, 0)));
    }

    [Test]
    public void With_No_Stories_Below_A_Pit_Queues_Nothing()
    {
        // Arrange: the floor on the first story: a pit is just a hole (the floor pass draws its bottom)
        _map.StoryLevel = 1;
        MakePit(12, 10, Numbered());
        _render.PitsOn = _map.HasPits && _map.StoriesBelow > 0;
        _posts.StartPits();

        // Act
        _posts.CrossPitEdge(12, 10, 12 * Tile, 10 * Tile + Tile / 2, vertical: true);
        _posts.CrossPitEdge(13, 10, 13 * Tile, 10 * Tile + Tile / 2, vertical: true);

        // Assert
        Assert.That(_render.PostCount, Is.EqualTo(0));
    }

    [Test]
    public void Only_Open_Floor_Is_A_Pit()
    {
        MakePit(12, 10, Numbered());
        Assert.That(_map.IsPit(12, 10), Is.True);
        _map.TileMap[12, 10] = 1;
        Assert.That(_map.IsPit(12, 10), Is.False);
    }

    [Test]
    public void A_Pits_Bottom_Is_The_Pit_Color_Unless_A_Sky_Is_Behind()
    {
        // Arrange: every tile a pit, no flats
        var pits = Enumerable.Repeat<TextureAsset?>(Numbered(), MapConstants.ClassicMapArea).ToArray();
        _sky.PitColor = 9;
        _sky.SetupFlatColumns();

        // Act / Assert: the near floor rows are the pit color
        _sky.DrawFlats(new TextureAsset?[MapConstants.ClassicMapArea], 3, Horizon, Height - 1, _sky.FloorHalves, behind: false, pits: pits);
        Assert.That(At(0, Height - 1), Is.EqualTo(9));

        // with a floor sky behind, it's left showing
        _screen[(Height - 1) * Width] = 200;
        _sky.DrawFlats(new TextureAsset?[MapConstants.ClassicMapArea], 3, Horizon, Height - 1, _sky.FloorHalves, behind: true, pits: pits);
        Assert.That(At(0, Height - 1), Is.EqualTo(200));
    }

    [Test]
    public void A_Levels_Own_Pits_Lie_Over_The_Games()
    {
        // Arrange
        var game = new MapObjectTranslationAsset();
        game.Flats.Pits[10] = "GSTONEA1";
        game.Flats.Pits[11] = "BSTONEA1";
        var level = new MapObjectTranslationAsset();
        level.Flats.Pits[11] = "WOOD1";

        // Act
        var merged = game.With(level);

        // Assert
        Assert.That(merged.Flats.Pits, Is.EquivalentTo(new Dictionary<int, string> { [10] = "GSTONEA1", [11] = "WOOD1" }));
    }
}
