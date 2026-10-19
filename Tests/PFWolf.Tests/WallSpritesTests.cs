using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

public class WallSpritesTests
{
    private const long Tile = MapConstants.TileGlobal;

    // A fence in tile (10, 10) whose front faces the given way
    private static Monster Fence(ObjDir facing, double? offset = null, params string[] flags)
    {
        var fence = new Monster { Name = "Fence", TileX = 10, TileY = 10, Dir = facing, Flags = [WallSprites.WallSpriteFlag, .. flags] };
        if (offset is { } texels)
            fence.Properties[WallSprites.WallSpriteOffsetProperty] = texels;
        return fence;
    }

    [Test]
    public void A_Panel_Facing_East_Runs_North_South_Through_The_Middle_Of_Its_Tile()
    {
        // Act
        var span = WallSprites.GetWallSpriteSpan(Fence(ObjDir.East))!.Value;

        // Assert: seen from the front (looking west at it), its left end is the south one
        Assert.That(span, Is.EqualTo(new WallSpriteSpan(10.5 * Tile, 11 * Tile, 10.5 * Tile, 10 * Tile)));
        Assert.That(span.Length, Is.EqualTo((double)Tile));
    }

    [Test]
    public void An_Offset_Moves_It_Toward_Its_Front()
    {
        // Act: 32 texels is half a tile, to the tile's east edge
        var span = WallSprites.GetWallSpriteSpan(Fence(ObjDir.East, offset: 32))!.Value;

        // Assert
        Assert.That((span.X1, span.X2), Is.EqualTo((11.0 * Tile, 11.0 * Tile)));
    }

    [Test]
    public void A_Negative_Offset_Sets_It_Back_Toward_Its_Back()
    {
        // Act: facing south, set back to the tile's north edge (the bridge's south trusses)
        var span = WallSprites.GetWallSpriteSpan(Fence(ObjDir.South, offset: -32))!.Value;

        // Assert: still read west to east from the south
        Assert.That(span, Is.EqualTo(new WallSpriteSpan(10 * Tile, 10 * Tile, 11 * Tile, 10 * Tile)));
    }

    [TestCase(12, 10.5, false)]     // in front of an east-facing panel
    [TestCase(9, 10.5, true)]       // behind it
    [TestCase(11, 9.5, false)]      // off to one side, still in front
    public void A_View_Is_Behind_A_Panel_On_The_Other_Side_From_Its_Front(double x, double y, bool behind)
    {
        var span = WallSprites.GetWallSpriteSpan(Fence(ObjDir.East))!.Value;
        Assert.That(WallSprites.SeenFromBehind(span, x * Tile, y * Tile), Is.EqualTo(behind));
    }

    [Test]
    public void Twosided_Is_A_Flag()
    {
        Assert.That(WallSprites.IsTwoSided(Fence(ObjDir.East)), Is.False);
        Assert.That(WallSprites.IsTwoSided(Fence(ObjDir.East, null, WallSprites.WallSpriteTwoSidedFlag)), Is.True);
    }

    [Test]
    public void A_Diagonal_Panel_Stands_Corner_To_Corner()
    {
        var span = WallSprites.GetWallSpriteSpan(Fence(ObjDir.NorthEast))!.Value;
        Assert.That(span.Length, Is.EqualTo(Math.Sqrt(2) * Tile).Within(1));
    }

    [Test]
    public void No_Facing_Too_Big_An_Offset_Or_Not_A_Wall_Sprite_Is_No_Panel()
    {
        Assert.That(WallSprites.GetWallSpriteSpan(Fence(ObjDir.NoDir)), Is.Null);
        Assert.That(WallSprites.GetWallSpriteSpan(Fence(ObjDir.East, offset: 40)), Is.Null);
        Assert.That(WallSprites.GetWallSpriteSpan(new Monster { Name = "Guard", Dir = ObjDir.East }), Is.Null);
    }

    [TestCase("east", "north-south")]
    [TestCase("north", "east-west")]
    [TestCase("northeast", "northwest-southeast")]
    [TestCase("southeast", "northeast-southwest")]
    [TestCase("nodir", "none")]
    public void Messages_Say_Which_Way_A_Panel_Runs(string facing, string axis)
    {
        Assert.That(WallSprites.WallSpriteAxis(Enum.Parse<ObjDir>(facing, ignoreCase: true)), Is.EqualTo(axis));
    }

    [Test]
    public void A_Box_Across_The_Panel_Hits_It_But_One_Touching_Its_Side_Doesnt()
    {
        // Arrange: the north-south panel at x 10.5 tiles
        var span = WallSprites.GetWallSpriteSpan(Fence(ObjDir.East))!.Value;
        long y = (long)(10.5 * Tile), size = Tile / 4;

        // Act / Assert
        Assert.That(WallSprites.BoxHitsWallSprite(span, (long)(10.5 * Tile), y, size), Is.True);
        Assert.That(WallSprites.BoxHitsWallSprite(span, (long)(10.5 * Tile) - size, y, size), Is.False);      // touching
        Assert.That(WallSprites.BoxHitsWallSprite(span, 10 * Tile, y, size), Is.False);
    }

    [Test]
    public void A_Line_Through_The_Panel_Crosses_It_And_One_Along_Or_Beside_It_Doesnt()
    {
        // Arrange
        var span = WallSprites.GetWallSpriteSpan(Fence(ObjDir.East))!.Value;
        long y = (long)(10.5 * Tile);

        // Act / Assert
        Assert.That(WallSprites.LineHitsWallSprite(span, 10 * Tile, y, 11 * Tile, y), Is.True);
        Assert.That(WallSprites.LineHitsWallSprite(span, 10 * Tile, y, (long)(10.4 * Tile), y), Is.False);      // stops short
        Assert.That(WallSprites.LineHitsWallSprite(span, (long)(10.5 * Tile), 9 * Tile, (long)(10.5 * Tile), 12 * Tile), Is.False);     // along it
        Assert.That(WallSprites.LineHitsWallSprite(span, 10 * Tile, 12 * Tile, 11 * Tile, 12 * Tile), Is.False);      // past its end
    }
}

public class WallSpriteBlockingTests
{
    private const long Tile = MapConstants.TileGlobal;

    private MapManager _map = null!;
    private WallSpriteBlocking _blocking = null!;

    [SetUp]
    public void MakeLevel()
    {
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, new GameSession(), null);
        _blocking = new WallSpriteBlocking(_map);
    }

    // A north-south panel through the middle of tile (10, 10)
    private Monster Fence(params string[] flags)
    {
        var fence = new Monster { Name = "Fence", TileX = 10, TileY = 10, Dir = ObjDir.East, Flags = [WallSprites.WallSpriteFlag, .. flags] };
        _map.GetActors().AddLast(fence);
        return fence;
    }

    private WallSpriteSpan? Hit(long x, bool player) =>
        _blocking.WallSpriteHitByBox(9, 9, 11, 11, x, (long)(10.5 * Tile), Tile / 4, player);

    [Test]
    public void A_Box_On_The_Panel_Is_Stopped_By_It()
    {
        Fence();
        _blocking.WallSpriteFromX = (int)(10 * Tile);
        Assert.That(Hit((long)(10.5 * Tile), player: true), Is.Not.Null);
        Assert.That(Hit((long)(10.5 * Tile), player: false), Is.Not.Null);
    }

    [Test]
    public void A_Player_Already_On_It_Can_Step_Off()
    {
        // Arrange: the move started on the panel (it was spawned on top of them)
        Fence();
        (_blocking.WallSpriteFromX, _blocking.WallSpriteFromY) = ((int)(10.5 * Tile), (int)(10.5 * Tile));

        // Act / Assert
        Assert.That(Hit((long)(10.6 * Tile), player: true), Is.Null);
    }

    [Test]
    public void A_Projectile_Through_Panel_Lets_Projectiles_By_But_Not_The_Player()
    {
        Fence(WallSprites.WallSpriteProjectileThroughFlag);
        _blocking.WallSpriteFromX = (int)(10 * Tile);
        Assert.That(Hit((long)(10.5 * Tile), player: false), Is.Null);
        Assert.That(Hit((long)(10.5 * Tile), player: true), Is.Not.Null);
    }

    [Test]
    public void A_Panel_Blocks_Sight_Unless_It_Can_Be_Shot_Through()
    {
        // Arrange
        long y = (long)(10.5 * Tile);
        var fence = Fence();

        // Act / Assert
        Assert.That(_blocking.WallSpriteBlocksLine(8 * Tile, y, 12 * Tile, y), Is.True);
        Assert.That(_blocking.WallSpriteBlocksLine(8 * Tile, y, 10 * Tile, y), Is.False);       // stops short

        fence.Flags = [WallSprites.WallSpriteFlag, WallSprites.WallSpriteShootThroughFlag];
        Assert.That(_blocking.WallSpriteBlocksLine(8 * Tile, y, 12 * Tile, y), Is.False);
    }

    [Test]
    public void A_Passable_Panel_Lets_The_Player_And_Projectiles_Through_But_Still_Blocks_Sight()
    {
        // Arrange
        long y = (long)(10.5 * Tile);
        Fence(WallSprites.WallSpritePassableFlag);
        _blocking.WallSpriteFromX = (int)(10 * Tile);

        // Act / Assert
        Assert.That(Hit((long)(10.5 * Tile), player: true), Is.Null);
        Assert.That(Hit((long)(10.5 * Tile), player: false), Is.Null);
        Assert.That(_blocking.WallSpriteBlocksLine(8 * Tile, y, 12 * Tile, y), Is.True);
    }

    [Test]
    public void A_Destroyed_Panel_Blocks_Nothing()
    {
        // Arrange: a window with health, shootable while it stands
        long y = (long)(10.5 * Tile);
        var window = Fence();
        window.Properties["health"] = 1;
        window.RuntimeFlags |= ObjFlags.Shootable;
        _blocking.WallSpriteFromX = (int)(10 * Tile);
        Assert.That(Hit((long)(10.5 * Tile), player: true), Is.Not.Null);

        // Act: broken (Monster.Kill clears Shootable)
        window.RuntimeFlags &= ~ObjFlags.Shootable;

        // Assert
        Assert.That(Hit((long)(10.5 * Tile), player: true), Is.Null);
        Assert.That(Hit((long)(10.5 * Tile), player: false), Is.Null);
        Assert.That(_blocking.WallSpriteBlocksLine(8 * Tile, y, 12 * Tile, y), Is.False);
    }
}
