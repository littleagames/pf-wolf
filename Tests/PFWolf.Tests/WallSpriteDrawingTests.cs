using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

public class WallSpriteDrawingTests
{
    private const long Tile = MapConstants.TILEGLOBAL;

    private MapManager _map = null!;
    private RenderState _render = null!;
    private WallSpriteDrawing _drawing = null!;

    [SetUp]
    public void MakeView()
    {
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, new GameSession())
        {
            spotvis = new bool[MapManager.MAPSIZE, MapManager.MAPSIZE],
        };
        // Looking east from the middle of tile (10, 10)
        _render = new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, new byte[256])
        {
            viewx = (int)(10.5 * Tile), viewy = (int)(10.5 * Tile), viewcos = 0x10000, viewsin = 0, heightnumerator = 0x10000,
        };
        _drawing = new WallSpriteDrawing(null!, _map, null!, _render, null!, half => half);
    }

    // A north-south fence through the middle of a tile
    private static Monster Fence(int x, int y) =>
        new() { Name = "Fence", TileX = (byte)x, TileY = (byte)y, Dir = ObjDir.east, Flags = [WallSprites.WallSpriteFlag] };

    [Test]
    public void Depth_Is_Along_The_View_Not_Straight_Line_Distance()
    {
        Assert.That(_drawing.ViewDepth(13.5 * Tile, 10.5 * Tile), Is.EqualTo(3.0 * Tile));
        Assert.That(_drawing.ViewDepth(13.5 * Tile, 14.5 * Tile), Is.EqualTo(3.0 * Tile));     // off to the side: as far along
        Assert.That(_drawing.ViewDepth(8.5 * Tile, 10.5 * Tile), Is.EqualTo(-2.0 * Tile));
    }

    [Test]
    public void Nearer_Is_Taller_But_Never_Up_To_The_Drawn_Mark()
    {
        Assert.That(_drawing.HeightAtDepth(4 * Tile), Is.LessThan(_drawing.HeightAtDepth(2 * Tile)));
        Assert.That(_drawing.HeightAtDepth(1), Is.EqualTo(_drawing.HeightAtDepth(GameConstants.MINDIST)));     // nearer than MINDIST is as at it
        _render.heightnumerator = 0x1000000;
        Assert.That(_drawing.HeightAtDepth(GameConstants.MINDIST), Is.EqualTo(31999));
    }

    [Test]
    public void A_Panel_Ahead_Goes_In_The_Sprite_List_At_Its_Middles_Height()
    {
        // Arrange
        var fence = Fence(13, 10);
        var vis = new VisibleObject();

        // Act
        bool drawn = _drawing.TransformWallSprite(fence, vis);

        // Assert: its middle is 3 tiles along the view
        Assert.That(drawn, Is.True);
        Assert.That(vis.wallsprite, Is.SameAs(fence));
        Assert.That(vis.viewheight, Is.EqualTo(_drawing.HeightAtDepth(3 * Tile)));
    }

    [Test]
    public void One_Wholly_Behind_The_View_Isnt()
    {
        Assert.That(_drawing.TransformWallSprite(Fence(7, 10), new VisibleObject()), Is.False);
    }

    [TestCase(13, 10, true)]        // its own tile seen
    [TestCase(12, 10, true)]        // the tile beside it (a panel on its edge shows from there)
    [TestCase(11, 10, false)]
    public void A_Panel_Shows_Once_Its_Tile_Or_One_Beside_It_Is_Seen(int seenX, int seenY, bool shows)
    {
        _map.spotvis[seenX, seenY] = true;
        Assert.That(_drawing.WallSpriteSeen(Fence(13, 10)), Is.EqualTo(shows));
    }
}
