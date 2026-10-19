using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

public class FrameInterpolationTests
{
    private const int Tile = 1 << MapConstants.TileShift;

    private MapManager _map = null!;
    private GameSession _session = null!;
    private FrameInterpolation _interpolation = null!;

    [SetUp]
    public void Make()
    {
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session, null);
        _interpolation = new FrameInterpolation(_map, _session);
    }

    private Monster Guard(int x, int y)
    {
        var guard = new Monster { Name = "Guard", X = x, Y = y };
        _map.GetActors().AddLast(guard);
        return guard;
    }

    // A frame drawn that far through the batch of tics after BeginTics
    private void DrawAt(double fraction) => (_interpolation.Active, _interpolation.Fraction) = (true, fraction);

    [Test]
    public void An_Actor_Is_Drawn_Between_Where_It_Was_And_Where_It_Is()
    {
        // Arrange: a guard a quarter tile on during the batch
        var guard = Guard(10 * Tile, 10 * Tile);
        _interpolation.BeginTics();
        guard.X += Tile / 4;

        // Act
        DrawAt(0.5);

        // Assert
        Assert.That(_interpolation.PositionOf(guard), Is.EqualTo((10 * Tile + Tile / 8, 10 * Tile)));
    }

    [Test]
    public void Not_Between_Tics_It_Is_Drawn_Where_It_Is()
    {
        var guard = Guard(10 * Tile, 10 * Tile);
        _interpolation.BeginTics();
        guard.X += Tile / 4;

        _interpolation.Fraction = 0.5;          // but not Active: the classic loop, a demo, others

        Assert.That(_interpolation.PositionOf(guard), Is.EqualTo((guard.X, guard.Y)));
    }

    [Test]
    public void A_Jump_Of_More_Than_A_Tile_Isnt_Slid()
    {
        var guard = Guard(10 * Tile, 10 * Tile);
        _interpolation.BeginTics();
        guard.X += 3 * Tile;                    // a teleport

        DrawAt(0.5);

        Assert.That(_interpolation.PositionOf(guard), Is.EqualTo((guard.X, guard.Y)));
    }

    [Test]
    public void One_That_Appeared_During_The_Batch_Is_Drawn_Where_It_Is()
    {
        Guard(10 * Tile, 10 * Tile);
        _interpolation.BeginTics();
        var rocket = Guard(12 * Tile, 10 * Tile);

        DrawAt(0.25);

        Assert.That(_interpolation.PositionOf(rocket), Is.EqualTo((rocket.X, rocket.Y)));
    }

    [Test]
    public void The_View_Turns_The_Short_Way_Past_East()
    {
        // Arrange: the view at 358 degrees turns on to 2
        _session.Camera.SetFixed(10 * Tile, 10 * Tile, 358);
        _interpolation.BeginTics();
        _session.Camera.SetFixed(10 * Tile, 10 * Tile, 2);

        // Act
        DrawAt(0.75);

        // Assert: three quarters of the 4 degrees, not of the 356 the long way
        Assert.That(_interpolation.CameraPose().Angle, Is.EqualTo(1).Within(1e-9));
    }

    [Test]
    public void The_View_Moves_And_Looks_Between_Too()
    {
        _session.Camera.SetFixed(10 * Tile, 10 * Tile, 90, pitch: 0);
        _interpolation.BeginTics();
        _session.Camera.SetFixed(10 * Tile + Tile / 2, 10 * Tile, 100, pitch: 10);

        DrawAt(0.5);
        var pose = _interpolation.CameraPose();

        Assert.That((pose.X, pose.Angle, pose.Pitch), Is.EqualTo((10 * Tile + Tile / 4, 95.0, 5.0)));
    }

    [Test]
    public void A_New_Level_Doesnt_Slide_The_View_From_The_Last()
    {
        _session.Camera.SetFixed(10 * Tile, 10 * Tile, 90);
        _interpolation.BeginTics();
        _interpolation.Forget();
        _session.Camera.SetFixed(10 * Tile + Tile / 2, 10 * Tile, 90);

        DrawAt(0.5);

        Assert.That(_interpolation.CameraPose().X, Is.EqualTo(10 * Tile + Tile / 2));
    }
}
