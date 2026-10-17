using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;
using PFWolf.Editor.Rendering;

namespace PFWolf.Tests;

/// <summary>Wall patches (plane 0's high byte) in the editor: reading, the patches layer, the 3D view and the level checks</summary>
public class EditorPatchTests
{
    private const ushort Wall = 1, OtherWall = 2, Floor = 107, Door = 90, Start = 19;
    private const int Sign = 5, SouthSign = 6, Lever = 7, TallSign = 8;

    private static MapObjectTranslationAsset Defs() => new()
    {
        Walls =
        {
            [Wall] = new() { North = "WALLNS", East = "WALLEW", South = "WALLNS", West = "WALLEW" },
            [OtherWall] = new() { North = "OTHER", East = "OTHER" },
        },
        Doors = { [Door] = new() { East = "DOOR", West = "DOOR", North = "DOORSIDE", South = "DOORSIDE", Vertical = true } },
        Floors = new MapFloorsTranslation { AreaStart = Floor, AreaCount = 37, Ambush = -1, SecretExit = -1 },
        PlayerStarts = { [Start] = new MapPlayerStartTranslation { Angles = 90 } },
        Patches =
        {
            [Sign] = new() { Texture = "SIGN" },
            [SouthSign] = new() { Texture = "SIGN", Sides = ["south"] },
            [Lever] = new() { Texture = "LEVER", Switch = new MapSwitchTranslation { Actions = ["A_OpenDoor"] } },
            [TallSign] = new() { Texture = "SIGN", Story = 2 },
        },
    };

    private static GameContent Content() => GameContent.FromDefinitions(Defs());

    /// <summary>Open floor walled in all round, the player starting at (5, 5)</summary>
    private static MapDocument Room()
    {
        var map = MapFiles.NewMap("Test", Wall, Floor);
        map.MapData[1][5 * 64 + 5] = Start;
        return new MapDocument("MAP01", map, properties: new MapProperties());
    }

    private static MapTiles Tiles(MapDocument document) => new(Content(), document.Map, () => document.Properties);

    private static ushort Patched(ushort tile, int patch) => (ushort)(tile | patch << MapConstants.PatchShift);

    [Test]
    public void A_Patched_Wall_Reads_As_Its_Wall_With_Its_Patch()
    {
        // Arrange
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, Patched(Wall, Sign));

        // Act
        var tiles = Tiles(document);

        // Assert
        Assert.That(tiles.KindAt(10, 10), Is.EqualTo(TileKind.Wall));
        Assert.That(tiles[0, 10, 10], Is.EqualTo(Wall));
        Assert.That(tiles.Patch(10, 10), Is.EqualTo(Sign));
        Assert.That(tiles.PatchDef(10, 10)!.Texture, Is.EqualTo("SIGN"));
        Assert.That(tiles.Describe(10, 10), Has.Some.StartsWith($"Patch {Sign}: SIGN"));
    }

    [Test]
    public void A_Lever_Patch_Makes_Its_Wall_A_Switch()
    {
        // Arrange
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, Patched(Wall, Lever));

        // Act / Assert
        Assert.That(Tiles(document).IsSwitch(10, 10), Is.True);
    }

    private static ToolController Tools(MapDocument document, bool patches, ushort value)
    {
        var defs = Defs();
        var tools = new ToolController
        {
            Document = document,
            Tool = EditTool.Paint,
            Plane = 0,
            Value = value,
            EraseValue = p => p == 0 ? Floor : (ushort)0,
        };
        (tools.Combine, tools.Read) = patches ? PaletteEntries.PatchLayer(defs) : PaletteEntries.TileLayer(defs);
        return tools;
    }

    [Test]
    public void The_Patches_Layer_Puts_Patches_On_Walls_Only_And_Keeps_The_Walls()
    {
        // Arrange: a wall and open floor side by side
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, OtherWall);
        var tools = Tools(document, patches: true, (ushort)(Sign << MapConstants.PatchShift));

        // Act: a stroke across both
        tools.Press(10, 10);
        tools.Move(11, 10);
        tools.Release();

        // Assert
        Assert.That(document[0, 10, 10], Is.EqualTo(Patched(OtherWall, Sign)));
        Assert.That(document[0, 11, 10], Is.EqualTo(Floor));
    }

    [Test]
    public void The_Patches_Eyedropper_Picks_The_Patch()
    {
        // Arrange
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, Patched(Wall, Lever));
        var tools = Tools(document, patches: true, 0);
        tools.Tool = EditTool.Pick;
        ushort picked = 0;
        tools.ValuePicked += (_, value) => picked = value;

        // Act
        tools.Press(10, 10);

        // Assert
        Assert.That(picked, Is.EqualTo(Lever << MapConstants.PatchShift));
    }

    [Test]
    public void Erasing_On_The_Patches_Layer_Takes_The_Patch_Off_And_Leaves_The_Wall()
    {
        // Arrange
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, Patched(Wall, Sign));
        var tools = Tools(document, patches: true, 0);

        // Act
        tools.EraseTile(10, 10);

        // Assert
        Assert.That(document[0, 10, 10], Is.EqualTo(Wall));
    }

    [Test]
    public void A_Wall_Put_Over_A_Patched_Wall_Keeps_The_Patch_And_Floor_Drops_It()
    {
        // Arrange
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, Patched(Wall, Sign));
        EditorMaps.Set(document, 0, 11, 10, Patched(Wall, Sign));

        // Act
        var walls = Tools(document, patches: false, OtherWall);
        walls.Press(10, 10);
        walls.Release();
        var floor = Tools(document, patches: false, Floor);
        floor.Press(11, 10);
        floor.Release();

        // Assert
        Assert.That(document[0, 10, 10], Is.EqualTo(Patched(OtherWall, Sign)));
        Assert.That(document[0, 11, 10], Is.EqualTo(Floor));
    }

    [Test]
    public void The_Wall_Fill_Sees_A_Patched_Wall_As_Its_Wall()
    {
        // Arrange: a run of wall, one of them patched, in the room
        var document = Room();
        for (int x = 10; x <= 12; x++)
            EditorMaps.Set(document, 0, x, 10, OtherWall);
        EditorMaps.Set(document, 0, 11, 10, Patched(OtherWall, Sign));
        var tools = Tools(document, patches: false, Wall);
        tools.Tool = EditTool.Fill;

        // Act
        tools.Press(10, 10);

        // Assert: the whole run, the patch kept
        Assert.That(document[0, 12, 10], Is.EqualTo(Wall));
        Assert.That(document[0, 11, 10], Is.EqualTo(Patched(Wall, Sign)));
    }

    [Test]
    public void The_Palette_Lists_The_Patches_By_Their_High_Byte()
    {
        // Act
        var entries = PaletteEntries.Patches(Defs());

        // Assert
        Assert.That(entries[0].Value, Is.Zero);
        Assert.That(entries.Single(entry => entry.Value == Lever << MapConstants.PatchShift).Label, Does.Contain("LEVER").And.Contain("switch"));
    }

    [Test]
    public void A_Patch_Shows_In_Front_Of_The_Faces_It_Is_On()
    {
        // Arrange: a pillar with a patch on its south face only
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, Patched(Wall, SouthSign));

        // Act
        var mesh = LevelMesh.Build(Tiles(document));

        // Assert: two surfaces on the south face (the wall, the patch just in front of it), one elsewhere
        var south = mesh.Surfaces.Where(s => s.Ref is { Kind: SurfaceKind.Wall, X: 10, Y: 10, Face: Face.South }).ToList();
        Assert.That(south, Has.Count.EqualTo(2));
        var patch = south.Single(s => s.Texture.Name == "SIGN");
        Assert.That(patch.Texture.Source, Is.EqualTo(TextureSource.Flat), "see-through");
        Assert.That(patch.Points.All(p => p.Z > 11), Is.True, "in front of the face");
        Assert.That(mesh.Surfaces.Count(s => s.Ref is { X: 10, Y: 10, Face: Face.North }), Is.EqualTo(1));
    }

    [Test]
    public void A_Patch_On_A_Story_The_Wall_Lacks_Is_Not_Drawn_And_One_It_Has_Is_There()
    {
        // Arrange: a story-2 patch on a 1-story pillar and on a 2-story one
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, Patched(Wall, TallSign));
        EditorMaps.Set(document, 0, 20, 10, Patched(Wall, TallSign));
        EditorMaps.Set(document, MapConstants.HeightPlane, 20, 10, 2);

        // Act
        var mesh = LevelMesh.Build(Tiles(document));

        // Assert
        Assert.That(mesh.Surfaces.Where(s => s.Ref is { X: 10, Y: 10 } && s.Texture.Name == "SIGN"), Is.Empty);
        var tall = mesh.Surfaces.First(s => s.Ref is { X: 20, Y: 10 } && s.Texture.Name == "SIGN");
        Assert.That(tall.Points.Min(p => p.Y), Is.EqualTo(1));
        Assert.That(tall.Points.Max(p => p.Y), Is.EqualTo(2));
    }

    private static List<LevelProblem> Check(MapDocument document) => LevelChecks.Run(Tiles(document), inGameInfo: true);

    [Test]
    public void The_Checks_Point_Out_Patches_Off_Walls_Unknown_Ones_And_Ones_That_Never_Show()
    {
        // Arrange: a patch on floor, one the mapdefs don't have, and a south-only one whose south
        // side is against another wall
        var document = Room();
        EditorMaps.Set(document, 0, 8, 8, Patched(Floor, Sign));
        EditorMaps.Set(document, 0, 10, 10, Patched(Wall, 99));
        EditorMaps.Set(document, 0, 20, 10, Patched(Wall, SouthSign));
        EditorMaps.Set(document, 0, 20, 11, Wall);

        // Act
        var messages = Check(document).Select(problem => problem.Message).ToList();

        // Assert
        Assert.That(messages, Has.Some.Contains("patch 5 is on open floor"));
        Assert.That(messages, Has.Some.Contains("patch 99 isn't in the mapdefs patches"));
        Assert.That(messages, Has.Some.Contains($"patch {SouthSign} isn't on a face that looks onto open floor"));
    }

    [Test]
    public void A_Patched_Level_Is_Otherwise_Fine()
    {
        // Arrange
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, Patched(Wall, Sign));

        // Act / Assert
        Assert.That(Check(document), Is.Empty);
    }

    [Test]
    public void A_Lever_Patch_Without_A_Tag_Is_A_Switch_With_Nothing_To_Act_On()
    {
        // Arrange
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, Patched(Wall, Lever));

        // Act / Assert
        Assert.That(Check(document).Select(problem => problem.Message), Has.Some.Contains("switch without a tag"));
    }
}
