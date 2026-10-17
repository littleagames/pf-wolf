using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Editor.Editing;
using PFWolf.Enums;

namespace PFWolf.Tests;

/// <summary>Rotating and flipping blocks of tiles in the editor, and turning what's on them to match</summary>
public class TileTransformTests
{
    private const ushort Floor = 100, Wall = 1;
    private const ushort VerticalDoor = 90, HorizontalDoor = 91, OneWayEast = 92, OneWaySouth = 93;
    private const ushort GuardEast = 108, GuardNorth = 109, GuardWest = 110, GuardSouth = 111, Lamp = 37, HalfTurner = 200, HalfTurnerNorth = 201;
    private const ushort StartNorth = 19, StartEast = 20, StartSouth = 21, StartWest = 22;
    private const ushort DiagonalNW = 400, DiagonalNE = 401, DiagonalSW = 402, DiagonalSE = 403;
    private const int SouthSign = 5, NorthSign = 6, EastSign = 7;

    private static MapObjectTranslationAsset Defs() => new()
    {
        Walls = { [Wall] = new() { North = "W", East = "W" } },
        Doors =
        {
            [VerticalDoor] = new() { Vertical = true, East = "DOOR_EW", West = "DOOR_EW", North = "SLOT", South = "SLOT", OpenSound = "open" },
            [HorizontalDoor] = new() { North = "DOOR_NS", South = "DOOR_NS", East = "SLOT", West = "SLOT", OpenSound = "open" },
            [OneWayEast] = new() { Vertical = true, East = "D", West = "D", OpensFrom = "east" },
            [OneWaySouth] = new() { North = "D", South = "D", OpensFrom = "south" },
        },
        Floors = new MapFloorsTranslation { AreaStart = Floor, AreaCount = 37, Ambush = -1, SecretExit = -1 },
        Things =
        {
            [GuardEast] = new() { Class = "Guard", Angles = 0 },
            [GuardNorth] = new() { Class = "Guard", Angles = 90 },
            [GuardWest] = new() { Class = "Guard", Angles = 180 },
            [GuardSouth] = new() { Class = "Guard", Angles = 270 },
            [Lamp] = new() { Class = "Lamp" },
            [HalfTurner] = new() { Class = "Turret", Angles = 0 },
            [HalfTurnerNorth] = new() { Class = "Turret", Angles = 90 },
        },
        PlayerStarts =
        {
            [StartNorth] = new() { Angles = 90 },
            [StartEast] = new() { Angles = 0 },
            [StartSouth] = new() { Angles = 270 },
            [StartWest] = new() { Angles = 180 },
        },
        Diagonals =
        {
            [DiagonalNW] = new() { Shape = WallShape.SolidNW },
            [DiagonalNE] = new() { Shape = WallShape.SolidNE },
            [DiagonalSW] = new() { Shape = WallShape.SolidSW },
            [DiagonalSE] = new() { Shape = WallShape.SolidSE },
        },
        Patches =
        {
            [SouthSign] = new() { Texture = "SIGN", Sides = ["south"] },
            [NorthSign] = new() { Texture = "SIGN", Sides = ["north"] },
            [EastSign] = new() { Texture = "OTHER", Sides = ["east"] },
        },
        MapInfo = { [0xFE] = MapInfoCodes.CeilingFloorColors },
    };

    private static ushort Remap(TileTransform transform, int plane, ushort value, out RemapOutcome outcome)
        => new TileRemap(Defs(), transform).Remap(plane, value, out outcome);

    [Test]
    public void A_Clockwise_Turn_Puts_The_Top_Left_Corner_Top_Right()
    {
        // Act: a block 3 wide and 2 high
        var size = TileTransforms.Size(TileTransform.RotateClockwise, 3, 2);
        var topLeft = TileTransforms.Map(TileTransform.RotateClockwise, 0, 0, 3, 2);
        var bottomLeft = TileTransforms.Map(TileTransform.RotateClockwise, 0, 1, 3, 2);

        // Assert
        Assert.That(size, Is.EqualTo((2, 3)));
        Assert.That(topLeft, Is.EqualTo((1, 0)));
        Assert.That(bottomLeft, Is.EqualTo((0, 0)));
    }

    [TestCase(TileTransform.RotateClockwise, 0, 270)]
    [TestCase(TileTransform.RotateCounterClockwise, 0, 90)]
    [TestCase(TileTransform.Rotate180, 90, 270)]
    [TestCase(TileTransform.FlipHorizontal, 0, 180)]
    [TestCase(TileTransform.FlipHorizontal, 90, 90)]
    [TestCase(TileTransform.FlipVertical, 90, 270)]
    [TestCase(TileTransform.FlipVertical, 45, 315)]
    public void Angles_Turn_With_The_Block(TileTransform transform, int angles, int expected)
        => Assert.That(TileTransforms.Angle(transform, angles), Is.EqualTo(expected));

    [TestCase(TileTransform.RotateClockwise, WallShape.SolidNW, WallShape.SolidNE)]
    [TestCase(TileTransform.RotateCounterClockwise, WallShape.SolidNW, WallShape.SolidSW)]
    [TestCase(TileTransform.Rotate180, WallShape.SolidNE, WallShape.SolidSW)]
    [TestCase(TileTransform.FlipHorizontal, WallShape.SolidNW, WallShape.SolidNE)]
    [TestCase(TileTransform.FlipVertical, WallShape.SolidNW, WallShape.SolidSW)]
    public void A_Diagonal_Walls_Solid_Corner_Turns(TileTransform transform, WallShape shape, WallShape expected)
        => Assert.That(TileTransforms.Shape(transform, shape), Is.EqualTo(expected));

    [Test]
    public void A_Quarter_Turn_And_Back_Lands_Where_It_Started()
    {
        // Arrange: an odd difference between width and height
        var rect = new TileRect(10, 10, 14, 11);

        // Act
        var turned = TileTransforms.Place(TileTransform.RotateClockwise, rect, 64, 64);
        var back = TileTransforms.Place(TileTransform.RotateCounterClockwise, turned, 64, 64);

        // Assert
        Assert.That((turned.Width, turned.Height), Is.EqualTo((2, 5)));
        Assert.That(back, Is.EqualTo(rect));
    }

    [Test]
    public void A_Turned_Block_Stays_On_The_Level()
    {
        // Act: a wide block along the top edge, turned upright
        var turned = TileTransforms.Place(TileTransform.RotateClockwise, new TileRect(0, 0, 9, 1), 64, 64);

        // Assert
        Assert.That(turned.Top, Is.EqualTo(0));
        Assert.That(turned.Height, Is.EqualTo(10));
    }

    [Test]
    public void A_Guard_Turns_To_The_Code_Facing_The_New_Way()
    {
        // Act
        var turned = Remap(TileTransform.RotateClockwise, 1, GuardEast, out var outcome);

        // Assert
        Assert.That(turned, Is.EqualTo(GuardSouth));
        Assert.That(outcome, Is.EqualTo(RemapOutcome.Turned));
    }

    [Test]
    public void A_Thing_With_No_Code_For_The_New_Way_Is_Left_And_Counted()
    {
        // Act: the turret only comes facing east or north
        var turned = Remap(TileTransform.Rotate180, 1, HalfTurner, out var outcome);

        // Assert
        Assert.That(turned, Is.EqualTo(HalfTurner));
        Assert.That(outcome, Is.EqualTo(RemapOutcome.Unmatched));
    }

    [Test]
    public void A_Thing_That_Comes_Only_One_Way_Doesnt_Face_Anywhere()
    {
        // Act
        var turned = Remap(TileTransform.RotateClockwise, 1, Lamp, out var outcome);

        // Assert
        Assert.That(turned, Is.EqualTo(Lamp));
        Assert.That(outcome, Is.EqualTo(RemapOutcome.Same));
    }

    [Test]
    public void A_Wall_Sprite_Faces_A_Way_Even_With_One_Code()
    {
        // Act
        new TileRemap(Defs(), TileTransform.RotateClockwise, alwaysFaces: c => c == "Lamp").Remap(1, Lamp, out var outcome);

        // Assert
        Assert.That(outcome, Is.EqualTo(RemapOutcome.Unmatched));
    }

    [Test]
    public void A_Player_Start_Turns()
    {
        // Act
        var turned = Remap(TileTransform.FlipVertical, 1, StartNorth, out var outcome);

        // Assert
        Assert.That(turned, Is.EqualTo(StartSouth));
        Assert.That(outcome, Is.EqualTo(RemapOutcome.Turned));
    }

    [Test]
    public void A_Diagonal_Marker_Turns()
        => Assert.That(Remap(TileTransform.RotateClockwise, 1, DiagonalNW, out _), Is.EqualTo(DiagonalNE));

    [Test]
    public void A_Door_Runs_The_Other_Way_After_A_Quarter_Turn()
    {
        // Act
        var turned = Remap(TileTransform.RotateClockwise, 0, VerticalDoor, out var outcome);
        var flipped = Remap(TileTransform.FlipHorizontal, 0, VerticalDoor, out var flipOutcome);

        // Assert: the DoorNumber alike but for its faces, as Wolf3D's light and dark ones are
        Assert.That(turned, Is.EqualTo(HorizontalDoor));
        Assert.That(outcome, Is.EqualTo(RemapOutcome.Turned));
        Assert.That(flipped, Is.EqualTo(VerticalDoor));
        Assert.That(flipOutcome, Is.EqualTo(RemapOutcome.Same));
    }

    [Test]
    public void A_One_Way_Door_Opens_From_The_Turned_Side()
        => Assert.That(Remap(TileTransform.RotateClockwise, 0, OneWayEast, out _), Is.EqualTo(OneWaySouth));

    [Test]
    public void A_Patch_Moves_To_The_Turned_Face_And_Keeps_Its_Wall()
    {
        // Act
        var flipped = Remap(TileTransform.FlipVertical, 0, (ushort)(Wall | SouthSign << MapConstants.PatchShift), out var outcome);
        var turned = Remap(TileTransform.RotateClockwise, 0, (ushort)(Wall | EastSign << MapConstants.PatchShift), out var turnOutcome);

        // Assert
        Assert.That(flipped, Is.EqualTo((ushort)(Wall | NorthSign << MapConstants.PatchShift)));
        Assert.That(outcome, Is.EqualTo(RemapOutcome.Turned));
        Assert.That(turned, Is.EqualTo((ushort)(Wall | EastSign << MapConstants.PatchShift)));
        Assert.That(turnOutcome, Is.EqualTo(RemapOutcome.Unmatched));
    }

    [Test]
    public void A_Map_Info_Code_Is_Counted_As_Reading_Its_Neighbour()
    {
        // Act
        Remap(TileTransform.FlipHorizontal, 1, 0xFE00, out var outcome);

        // Assert
        Assert.That(outcome, Is.EqualTo(RemapOutcome.Paired));
    }

    private static (MapDocument Document, ToolController Tools) Setup()
    {
        var document = EditorMaps.Document();
        var tools = new ToolController
        {
            Document = document,
            Tool = EditTool.Select,
            EraseValue = p => p == 0 ? Floor : (ushort)0,
            MakeRemap = transform => new TileRemap(Defs(), transform),
        };
        return (document, tools);
    }

    private static void Select(ToolController tools, int x0, int y0, int x1, int y1)
    {
        tools.Press(x0, y0);
        tools.Move(x1, y1);
        tools.Release();
    }

    [Test]
    public void Rotating_The_Selection_Turns_Its_Tiles_And_Things_As_One_Undo_Step()
    {
        // Arrange: a 3 Ã— 1 strip with a WallId at its west end and a guard facing east in the middle
        var (document, tools) = Setup();
        EditorMaps.Set(document, 0, 10, 10, Wall);
        EditorMaps.Set(document, 1, 11, 10, GuardEast);
        Select(tools, 10, 10, 12, 10);
        TransformReport? report = null;
        tools.Transformed += (_, r) => report = r;

        // Act
        tools.Transform(TileTransform.RotateClockwise);

        // Assert: upright around the middle tile, the WallId on top, the guard facing south
        Assert.That(tools.Selection!.Bounds, Is.EqualTo(new TileRect(11, 9, 11, 11)));
        Assert.That(document[0, 11, 9], Is.EqualTo(Wall));
        Assert.That(document[0, 10, 10], Is.EqualTo(Floor));
        Assert.That(document[1, 11, 10], Is.EqualTo(GuardSouth));
        Assert.That(report!.Counts, Is.EqualTo(new RemapCounts(1, 0, 0)));

        document.Undo();
        Assert.That(document[0, 10, 10], Is.EqualTo(Wall));
        Assert.That(document[1, 11, 10], Is.EqualTo(GuardEast));
    }

    [Test]
    public void Rotating_A_Paste_Turns_What_Will_Be_Put_Down()
    {
        // Arrange: copy a guard facing east
        var (document, tools) = Setup();
        EditorMaps.Set(document, 1, 5, 5, GuardEast);
        Select(tools, 5, 5, 6, 5);
        tools.Copy();

        // Act: pick it up at (20, 20), turn it, put it down
        tools.StartPaste(20, 20);
        tools.Transform(TileTransform.RotateCounterClockwise);
        tools.Press(20, 20);
        tools.Release();

        // Assert: a 1 Ã— 2 paste, the guard (the strip's west end) now at the bottom, facing north
        Assert.That(tools.Selection!.Bounds, Is.EqualTo(new TileRect(20, 20, 20, 21)));
        Assert.That(document[1, 20, 21], Is.EqualTo(GuardNorth));
        Assert.That(document[1, 5, 5], Is.EqualTo(GuardEast));
    }
}
