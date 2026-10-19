using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;
using PFWolf.Editor.Rendering;

namespace PFWolf.Tests;

/// <summary>A UWMF level's extra things in the editor: the free-thing tool, undo, resize, the definitions tab and the 3D view</summary>
public class ExtraThingsEditingTests
{
    private static readonly MapThing Barrel = new("Barrel", 0, 0, 90);

    private static (MapDocument Document, ToolController Tools) Editing(MapThing? toPlace = null)
    {
        var document = EditorMaps.Document();
        var tools = new ToolController { Document = document, Tool = EditTool.FreeThing, NewExtraThing = () => toPlace };
        return (document, tools);
    }

    [Test]
    public void A_Click_Puts_The_Palettes_Thing_Down_Snapped_As_One_Undo_Step()
    {
        // Arrange
        var (document, tools) = Editing(Barrel);

        // Act
        tools.PressAt(10.3, 12.71);
        tools.Release();

        // Assert: to an eighth of a tile
        Assert.That(document.Map.ExtraThings, Is.EqualTo(new[] { Barrel with { X = 10.25, Y = 12.75 } }));
        Assert.That(tools.SelectedExtra, Is.Zero);
        Assert.That(document.UndoDescription, Is.EqualTo("Place Barrel"));
        document.Undo();
        Assert.That(document.Map.ExtraThings, Is.Empty);
        document.Redo();
        Assert.That(document.Map.ExtraThings, Has.Count.EqualTo(1));
    }

    [Test]
    public void Shift_Puts_It_In_Its_Tiles_Middle()
    {
        var (document, tools) = Editing(Barrel);
        tools.Constrain = true;

        tools.PressAt(10.3, 12.71);
        tools.Release();

        Assert.That((document.Map.ExtraThings[0].X, document.Map.ExtraThings[0].Y), Is.EqualTo((10.5, 12.5)));
    }

    [Test]
    public void Clicking_One_Picks_It_Up_And_A_Drag_Moves_It_As_One_Undo_Step()
    {
        // Arrange: one at 10.25,12.75
        var (document, tools) = Editing(Barrel);
        tools.PressAt(10.25, 12.75);
        tools.Release();

        // Act: grabbed a little off its middle, and dragged
        tools.PressAt(10.4, 12.8);
        tools.MoveAt(11.0, 13.0);
        tools.MoveAt(14.4, 20.3);
        tools.Release();

        // Assert: still one, where it was dragged (keeping the grab)
        Assert.That(document.Map.ExtraThings, Is.EqualTo(new[] { Barrel with { X = 14.25, Y = 20.25 } }));
        Assert.That(document.UndoDescription, Is.EqualTo("Move thing"));
        document.Undo();
        Assert.That((document.Map.ExtraThings[0].X, document.Map.ExtraThings[0].Y), Is.EqualTo((10.25, 12.75)));
    }

    [Test]
    public void A_Click_On_One_Without_A_Drag_Leaves_No_Undo_Step()
    {
        var (document, tools) = Editing(Barrel);
        tools.PressAt(10.25, 12.75);
        tools.Release();

        tools.PressAt(10.25, 12.75);
        tools.Release();

        Assert.That(document.UndoDescription, Is.EqualTo("Place Barrel"));
    }

    [Test]
    public void Escape_During_A_Drag_Puts_It_Back()
    {
        var (document, tools) = Editing(Barrel);
        tools.PressAt(10.25, 12.75);
        tools.Release();

        tools.PressAt(10.25, 12.75);
        tools.MoveAt(20, 20);
        tools.Escape();
        tools.Release();

        Assert.That((document.Map.ExtraThings[0].X, document.Map.ExtraThings[0].Y), Is.EqualTo((10.25, 12.75)));
        Assert.That(document.UndoDescription, Is.EqualTo("Place Barrel"));
    }

    [Test]
    public void The_One_Picked_Turns_With_The_Rotate_Keys_And_Delete_Takes_It_Away()
    {
        // Arrange
        var (document, tools) = Editing(Barrel);
        tools.PressAt(10.25, 12.75);
        tools.Release();

        // Act / Assert
        Assert.That(tools.CanTransform, Is.True);
        tools.Transform(TileTransform.RotateClockwise);
        Assert.That(document.Map.ExtraThings[0].Angle, Is.Zero);

        tools.Delete();
        Assert.That(document.Map.ExtraThings, Is.Empty);
        Assert.That(tools.SelectedExtra, Is.EqualTo(-1));
        document.Undo();
        Assert.That(document.Map.ExtraThings.Single().Angle, Is.Zero);
    }

    [Test]
    public void With_No_Thing_In_The_Palette_A_Click_Puts_Nothing_Down()
    {
        var (document, tools) = Editing(toPlace: null);

        tools.PressAt(5.5, 5.5);
        tools.Release();

        Assert.That(document.Map.ExtraThings, Is.Empty);
        Assert.That(document.CanUndo, Is.False);
    }

    [Test]
    public void Other_Tools_Still_Work_On_The_Tile_Under_The_Pointer()
    {
        var (document, tools) = Editing(Barrel);
        tools.Tool = EditTool.Paint;
        tools.Plane = 1;
        tools.Value = 52;

        tools.PressAt(7.9, 3.2);
        tools.Release();

        Assert.That(document[1, 7, 3], Is.EqualTo(52));
        Assert.That(document.Map.ExtraThings, Is.Empty);
    }

    [Test]
    public void An_Undo_That_Takes_The_Picked_One_Away_Unpicks_It()
    {
        var (document, tools) = Editing(Barrel);
        tools.PressAt(10.25, 12.75);
        tools.Release();

        document.Undo();

        Assert.That(tools.SelectedExtra, Is.EqualTo(-1));
    }

    [Test]
    public void Resizing_Moves_The_Extra_Things_With_The_Level_And_Drops_Those_Off_It()
    {
        // Arrange: one near the top left, one near the bottom right of a 64x64 level
        var document = EditorMaps.Document();
        document.Map.ExtraThings = [Barrel with { X = 2.25, Y = 2.5 }, Barrel with { X = 60.5, Y = 61.5 }];

        // Act: to 40x40 keeping the bottom right
        document.Resize("Resize", 40, 40, MapResizing.Resized(document.Map, 40, 40, MapAnchor.BottomRight, 1),
            MapResizing.ResizedExtras(document.Map, 40, 40, MapAnchor.BottomRight));

        // Assert
        Assert.That(document.Map.ExtraThings, Is.EqualTo(new[] { Barrel with { X = 36.5, Y = 37.5 } }));
        document.Undo();
        Assert.That(document.Map.ExtraThings, Has.Count.EqualTo(2));
    }

    [Test]
    public void The_Definitions_Tab_Shows_A_Levels_Own_Definitions_And_Extra_Things_As_Yaml()
    {
        // Arrange
        var map = EditorMaps.Blank();
        var local = new MapObjectTranslationAsset();
        local.Walls[250] = new MapTextureTranslation { North = "BRICK1", East = "BRICK2", Flip = ["south"] };
        local.Doors[251] = new MapTextureTranslation { East = "NEWDOOR", West = "NEWDOOR", Vertical = true, Lock = "GoldKey" };
        local.Flats.Floor[255] = "GRASS";
        local.Triggers[0x6000] = new MapTriggerTranslation { Action = "A_Exit", Activation = "walk" };
        local.Floors.AreaCount = 40;
        map.LocalDefs = local;
        map.ExtraThings = [new MapThing("Guard", 2.25, 3.5, 180, Patrol: true, Skills: 0b0011)];

        // Act
        var text = LevelOwnDefinitions.Describe(map, "MAP07");

        // Assert
        Assert.That(text, Does.Contain("walls:").And.Contain("250:").And.Contain("North: BRICK1").And.Contain("flip:"));
        Assert.That(text, Does.Contain("doors:").And.Contain("Vertical: true").And.Contain("lock: GoldKey"));
        Assert.That(text, Does.Contain("255: GRASS"));
        Assert.That(text, Does.Contain("action: A_Exit").And.Contain("activation: walk"));
        Assert.That(text, Does.Contain("area-count: 40"));
        Assert.That(text, Does.Contain("{ class: Guard, x: 2.25, y: 3.5, angle: 180, patrol: true, skills: [1, 2] }"));
        Assert.That(LevelOwnDefinitions.Describe(EditorMaps.Blank(), "MAP01"), Does.Contain("nothing of its own"));
    }

    [Test]
    public void The_3D_View_Marks_An_Extra_Thing_On_Its_Tile()
    {
        // Arrange: a class the actordefs don't have, so it's marked rather than drawn
        var content = GameContent.FromDefinitions(new MapObjectTranslationAsset(), actors: new() { ["Guard"] = new ActorData() });
        var document = EditorMaps.Document();
        document.Map.ExtraThings = [new MapThing("NoSuchThing", 20.25, 30.75, 0)];

        // Act
        var mesh = LevelMesh.Build(new MapTiles(content, document.Map, () => document.Properties));

        // Assert
        Assert.That(mesh.Surfaces.Where(s => s.Ref.Kind == SurfaceKind.Thing && s.Ref.X == 20 && s.Ref.Y == 30), Is.Not.Empty);
    }
}
