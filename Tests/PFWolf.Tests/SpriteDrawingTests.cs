using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities.Actors;
using PFWolf.Enums;

namespace PFWolf.Tests;

public class SpriteDrawingTests
{
    private const int Tile = 1 << MapConstants.TILESHIFT;

    private RenderState _render = null!;
    private HashSet<string> _sprites = null!;
    private SpriteDrawing _drawing = null!;

    [SetUp]
    public void Make()
    {
        // Looking east from the middle of tile (10, 10), the view 320 wide
        _render = new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.noshade)
        {
            viewx = 10 * Tile + Tile / 2, viewy = 10 * Tile + Tile / 2, viewcos = 0x10000, viewsin = 0, viewangle = 0,
            centerx = 160, projectionwidth = 320, scale = 160, heightnumerator = 0x100000,
        };
        _sprites = [];
        _drawing = new SpriteDrawing(null!, null!, null!, null!, null!, null!, null!, _render, null!, new GameSession(),
            new SpriteDrawingHooks(() => null, ob => ob.Name == "Body", _ => null, ob => ob.Name, _sprites.Contains,
                () => null, () => 1));
    }

    private Monster At(double tilesAhead, double tilesRight) => new()
    {
        Name = "Guard",
        X = _render.viewx + (int)(tilesAhead * Tile),
        Y = _render.viewy + (int)(tilesRight * Tile),
    };

    [Test]
    public void An_Actor_Straight_Ahead_Is_In_The_Middle_Of_The_View()
    {
        // Arrange
        var guard = At(3, 0);

        // Act
        _drawing.TransformActor(guard);

        // Assert
        Assert.That(guard.ViewX, Is.EqualTo(_render.centerx));
        Assert.That(guard.TransX, Is.EqualTo(3 * Tile - GameConstants.ACTORSIZE));
        Assert.That(guard.ViewHeight, Is.EqualTo(_render.heightnumerator / ((3 * Tile - GameConstants.ACTORSIZE) >> 8)));
    }

    [Test]
    public void One_Off_To_The_Right_Is_Right_Of_Middle_And_Farther_Is_Smaller()
    {
        var near = At(2, 1);
        var far = At(4, 0);

        _drawing.TransformActor(near);
        _drawing.TransformActor(far);

        Assert.That(near.ViewX, Is.GreaterThan(_render.centerx));
        Assert.That(far.ViewHeight, Is.LessThan(near.ViewHeight));
    }

    [Test]
    public void One_Too_Close_Or_Behind_Isnt_Drawn()
    {
        var behind = At(-2, 0);

        _drawing.TransformActor(behind);

        Assert.That(behind.ViewHeight, Is.EqualTo(0));
    }

    [Test]
    public void From_Another_View_It_Goes_By_That_View()
    {
        // Arrange: seen from three tiles further east, looking west, the guard is ahead
        var guard = At(1, 0);

        // Act
        _drawing.TransformActorFrom(guard, _render.viewx + 3 * Tile, _render.viewy, 0, -0x10000);

        // Assert
        Assert.That((guard.ViewX, guard.TransX), Is.EqualTo(((short)_render.centerx, 2 * Tile - GameConstants.ACTORSIZE)));
    }

    [TestCase(11, 10, true)]        // the next tile ahead: within grabbing distance
    [TestCase(13, 10, false)]       // too far
    [TestCase(11, 11, false)]       // off to the side
    public void A_Tile_Is_Grabbed_When_Just_Ahead(int tx, int ty, bool grabbed)
    {
        short dispx = 0, dispheight = 0;
        Assert.That(_drawing.TransformTile(tx, ty, ref dispx, ref dispheight), Is.EqualTo(grabbed));
    }

    [TestCase("east", 1)]
    [TestCase("west", 5)]
    [TestCase("north", 7)]
    [TestCase("south", 3)]
    public void The_View_Shown_Goes_By_Which_Way_It_Faces(string dir, int view)
    {
        // Arrange: the viewer looks west at a guard in the middle of the view
        _render.viewangle = 180;
        var guard = At(0, 0);
        guard.ViewX = (short)_render.centerx;
        guard.Dir = Enum.Parse<objdirtypes>(dir);

        // Act / Assert: facing east it faces the viewer (view 1), west it shows its back (5)
        Assert.That(_drawing.CalcRotate(guard), Is.EqualTo(view));
    }

    [Test]
    public void Facing_No_Way_It_Faces_The_Viewer()
    {
        var guard = At(2, 0);
        guard.Dir = objdirtypes.nodir;
        Assert.That(_drawing.CalcRotate(guard), Is.EqualTo(1));
    }

    [Test]
    public void Projectiles_And_Player_Bodies_Turn_By_Their_Angle()
    {
        // Arrange: the viewer looks west; both head west, away from it, with a Dir saying east
        _render.viewangle = 180;
        var rocket = new Monster { Name = "Rocket", Flags = ["PROJECTILE"], Angle = 180, Dir = objdirtypes.east, ViewX = (short)_render.centerx };
        var body = new Monster { Name = "Body", Angle = 180, Dir = objdirtypes.east, ViewX = (short)_render.centerx };

        // Act / Assert: their backs
        Assert.That((_drawing.CalcRotate(rocket), _drawing.CalcRotate(body)), Is.EqualTo((5, 5)));
    }

    [Test]
    public void A_Frame_With_Views_Shows_The_One_For_Its_Angle()
    {
        // Arrange
        _sprites.UnionWith(["GARDA1", "GARDA5", "GARDB0"]);
        _render.viewangle = 180;
        var guard = At(0, 0);
        (guard.ViewX, guard.Dir) = ((short)_render.centerx, objdirtypes.west);

        // Act / Assert
        Assert.That(_drawing.HasDirectionalSprites("GARD", "A"), Is.True);
        Assert.That(_drawing.HasDirectionalSprites("GARD", "B"), Is.False);
        Assert.That(_drawing.ActorShapeName("GARD", "A", guard), Is.EqualTo("GARDA5"));
        Assert.That(_drawing.ActorShapeName("GARD", "B", guard), Is.EqualTo("GARDB0"));
    }

    [Test]
    public void A_View_The_Frame_Lacks_Falls_Back_To_Its_Single_View()
    {
        // Arrange: a mod's views 2-8 beside the game's single front view
        _sprites.UnionWith(["GARDF0", "GARDF2"]);
        _render.viewangle = 180;
        var guard = At(0, 0);
        (guard.ViewX, guard.Dir) = ((short)_render.centerx, objdirtypes.west);

        // Act / Assert: view 5 is missing
        Assert.That(_drawing.ActorShapeName("GARD", "F", guard), Is.EqualTo("GARDF0"));
    }

    [Test]
    public void From_The_Front_Is_View_One_Else_The_Single_View()
    {
        _sprites.Add("GARDA1");
        Assert.That((_drawing.FrontShapeName("GARD", "A"), _drawing.FrontShapeName("HANS", "A")), Is.EqualTo(("GARDA1", "HANSA0")));
    }

    [Test]
    public void Whether_A_Frame_Has_Views_Is_Looked_Up_Once()
    {
        _drawing.HasDirectionalSprites("GARD", "A");
        _sprites.Add("GARDA3");

        Assert.That(_drawing.HasDirectionalSprites("GARD", "A"), Is.False);
    }
}
