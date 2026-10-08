using System.Numerics;
using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;
using PFWolf.Editor.Rendering;
using PFWolf.Enums;

namespace PFWolf.Tests;

public class LevelMeshTests
{
    private const ushort Wall = 1, Floor = 107, VerticalDoor = 90, HorizontalDoor = 91, Start = 19, Diagonal = 400;

    private static GameContent Content() => GameContent.FromDefinitions(new MapObjectTranslationAsset
    {
        Walls = { [Wall] = new() { North = "WALLNS", East = "WALLEW", South = "WALLNS", West = "WALLEW" } },
        Doors =
        {
            [VerticalDoor] = new() { East = "DOOR", West = "DOOR", North = "DOORSIDE", South = "DOORSIDE", Vertical = true },
            [HorizontalDoor] = new() { North = "DOOR", South = "DOOR", East = "DOORSIDE", West = "DOORSIDE" },
        },
        Floors = new MapFloorsTranslation { AreaStart = Floor, AreaCount = 37, Ambush = -1, SecretExit = -1 },
        PlayerStarts = { [Start] = new MapPlayerStartTranslation { Angles = 90 } },
        Diagonals = { [Diagonal] = new MapDiagonalTranslation { Shape = WallShape.SolidNW } },
    });

    /// <summary>Open floor walled in all round</summary>
    private static MapDocument Room() => new("MAP01", MapFiles.NewMap("Test", Wall, Floor), properties: new MapProperties());

    private static LevelMesh Build(MapDocument document) => LevelMesh.Build(new MapTiles(Content(), document.Map, () => document.Properties));

    private static IEnumerable<Surface> At(LevelMesh mesh, SurfaceKind kind, int x, int y, Face? face = null)
        => mesh.Surfaces.Where(s => s.Ref.Kind == kind && s.Ref.X == x && s.Ref.Y == y && (face == null || s.Ref.Face == face));

    private static Vector3 Outward(Face face) => face switch
    {
        Face.North => -Vector3.UnitZ,
        Face.South => Vector3.UnitZ,
        Face.East => Vector3.UnitX,
        _ => -Vector3.UnitX,
    };

    [Test]
    public void Every_Wall_Face_Looks_Out_Onto_Open_Floor()
    {
        // Arrange: a pillar in the room
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, Wall);

        // Act
        var mesh = Build(document);

        // Assert: the pillar's four faces look out of it, and nothing else of it is drawn but its top
        var pillar = At(mesh, SurfaceKind.Wall, 10, 10).ToList();
        Assert.That(pillar.Select(s => s.Ref.Face), Is.EquivalentTo(new[] { Face.North, Face.East, Face.South, Face.West, Face.Top }));
        foreach (var face in pillar.Where(s => s.Ref.Face != Face.Top))
            Assert.That(Vector3.Dot(face.Normal, Outward(face.Ref.Face)), Is.EqualTo(1).Within(1e-5), face.Ref.ToString());
        Assert.That(Vector3.Dot(pillar.Single(s => s.Ref.Face == Face.Top).Normal, Vector3.UnitY), Is.EqualTo(1).Within(1e-5));
    }

    [Test]
    public void Faces_Between_Walls_And_On_The_Level_Edge_Are_Left_Out()
    {
        // Act
        var mesh = Build(Room());

        // Assert: the corner wall only faces into the room, and the edge walls only inward
        Assert.That(At(mesh, SurfaceKind.Wall, 0, 0).Where(s => s.Ref.Face != Face.Top), Is.Empty);
        Assert.That(At(mesh, SurfaceKind.Wall, 0, 5).Select(s => s.Ref.Face), Is.EquivalentTo(new[] { Face.East, Face.Top }));
    }

    [Test]
    public void North_And_South_Faces_Show_The_North_Texture_East_And_West_The_East()
    {
        // Arrange
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, Wall);

        // Act
        var mesh = Build(document);

        // Assert
        Assert.That(At(mesh, SurfaceKind.Wall, 10, 10, Face.North).Single().Texture.Name, Is.EqualTo("WALLNS"));
        Assert.That(At(mesh, SurfaceKind.Wall, 10, 10, Face.South).Single().Texture.Name, Is.EqualTo("WALLNS"));
        Assert.That(At(mesh, SurfaceKind.Wall, 10, 10, Face.East).Single().Texture.Name, Is.EqualTo("WALLEW"));
        Assert.That(At(mesh, SurfaceKind.Wall, 10, 10, Face.West).Single().Texture.Name, Is.EqualTo("WALLEW"));
    }

    [Test]
    public void A_Wall_Face_Reads_Left_To_Right_Seen_From_Outside()
    {
        // Arrange
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, Wall);

        // Act: the west face, seen looking east, has north on its left
        var face = At(Build(document), SurfaceKind.Wall, 10, 10, Face.West).Single();

        // Assert: u 0 at its north end, as the game's HitVertWall
        int left = Array.FindIndex(face.Uvs, uv => uv == new Vector2(0, 1));
        Assert.That(face.Points[left], Is.EqualTo(new Vector3(10, 0, 10)));
    }

    [Test]
    public void A_Taller_Wall_Shows_Above_A_Shorter_Neighbour()
    {
        // Arrange: two walls side by side, 3 stories and 1
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, Wall);
        EditorMaps.Set(document, 0, 11, 10, Wall);
        EditorMaps.Set(document, MapConstants.HEIGHTPLANE, 10, 10, 3);

        // Act
        var mesh = Build(document);

        // Assert: the tall one's east face runs from story 1 to 3; the short one's west face isn't there
        var east = At(mesh, SurfaceKind.Wall, 10, 10, Face.East).Single();
        Assert.That(east.Points.Min(p => p.Y), Is.EqualTo(1));
        Assert.That(east.Points.Max(p => p.Y), Is.EqualTo(3));
        Assert.That(At(mesh, SurfaceKind.Wall, 11, 10, Face.West), Is.Empty);
    }

    [Test]
    public void The_Walls_Beside_A_Door_Show_Its_Side()
    {
        // Arrange: a vertical door between two walls in an east-west corridor
        var document = Room();
        EditorMaps.Set(document, 0, 10, 9, Wall);
        EditorMaps.Set(document, 0, 10, 11, Wall);
        EditorMaps.Set(document, 0, 10, 10, VerticalDoor);

        // Act
        var mesh = Build(document);

        // Assert
        Assert.That(At(mesh, SurfaceKind.Wall, 10, 9, Face.South).Single().Texture.Name, Is.EqualTo("DOORSIDE"));
        Assert.That(At(mesh, SurfaceKind.Wall, 10, 11, Face.North).Single().Texture.Name, Is.EqualTo("DOORSIDE"));
        var door = At(mesh, SurfaceKind.Door, 10, 10).ToList();
        Assert.That(door.Select(s => s.Ref.Face), Is.EquivalentTo(new[] { Face.West, Face.East }));
        Assert.That(door.All(s => s.Texture.Name == "DOOR" && s.Points.All(p => p.X == 10.5f)));
    }

    [Test]
    public void Above_A_Door_Is_The_Wall_It_Is_Set_Into()
    {
        // Arrange: a 2-story door tile
        var document = Room();
        EditorMaps.Set(document, 0, 9, 10, Wall);
        EditorMaps.Set(document, 0, 11, 10, Wall);
        EditorMaps.Set(document, 0, 10, 10, HorizontalDoor);
        EditorMaps.Set(document, MapConstants.HEIGHTPLANE, 10, 10, 2);

        // Act
        var door = At(Build(document), SurfaceKind.Door, 10, 10, Face.North).ToList();

        // Assert: the door to story 1, its lintel in the wall's north texture above it
        Assert.That(door.Single(s => s.Points.Max(p => p.Y) == 1).Texture.Name, Is.EqualTo("DOOR"));
        Assert.That(door.Single(s => s.Points.Min(p => p.Y) == 1).Texture.Name, Is.EqualTo("WALLNS"));
    }

    [Test]
    public void Floors_Face_Up_And_Ceilings_Down_At_The_Level_Height()
    {
        // Arrange
        var document = Room();
        document.SetProperties(new MapProperties { WallHeight = 2 });

        // Act
        var mesh = Build(document);

        // Assert
        var floor = At(mesh, SurfaceKind.Floor, 5, 5).Single();
        var ceiling = At(mesh, SurfaceKind.Ceiling, 5, 5).Single();
        Assert.That(floor.Points.All(p => p.Y == 0) && Vector3.Dot(floor.Normal, Vector3.UnitY) > 0.99f);
        Assert.That(ceiling.Points.All(p => p.Y == 2) && Vector3.Dot(ceiling.Normal, -Vector3.UnitY) > 0.99f);
    }

    [Test]
    public void A_Diagonal_Faces_Its_Open_Corner_With_Floor_In_Its_Open_Half()
    {
        // Arrange: solid north-west, open to the south-east
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, Wall);
        EditorMaps.Set(document, 1, 10, 10, Diagonal);

        // Act
        var mesh = Build(document);

        // Assert
        var slant = At(mesh, SurfaceKind.Wall, 10, 10, Face.Diagonal).Single();
        Assert.That(Vector3.Dot(slant.Normal, Vector3.Normalize(new Vector3(1, 0, 1))), Is.EqualTo(1).Within(1e-5));
        Assert.That(At(mesh, SurfaceKind.Wall, 10, 10).Select(s => s.Ref.Face),
            Is.EquivalentTo(new[] { Face.North, Face.West, Face.Diagonal, Face.Top }));
        var floor = At(mesh, SurfaceKind.Floor, 10, 10).Single();
        Assert.That(floor.Points, Has.Length.EqualTo(3));
        Assert.That(floor.Points, Has.Some.EqualTo(new Vector3(11, 0, 11)));
    }

    [TestCase(Face.North, 10, 9)]
    [TestCase(Face.South, 10, 11)]
    [TestCase(Face.East, 11, 10)]
    [TestCase(Face.West, 9, 10)]
    public void In_Front_Of_A_Wall_Face_Is_The_Tile_It_Looks_Onto(Face face, int x, int y)
    {
        // Act
        var target = LevelMesh.TargetTile(new SurfaceRef(SurfaceKind.Wall, 10, 10, face), inFront: true);

        // Assert
        Assert.That(target, Is.EqualTo((x, y)));
    }

    [TestCase(SurfaceKind.Wall, Face.West, false)]
    [TestCase(SurfaceKind.Wall, Face.Top, true)]
    [TestCase(SurfaceKind.Floor, Face.None, true)]
    [TestCase(SurfaceKind.Thing, Face.None, true)]
    public void Other_Surfaces_Edit_Their_Own_Tile(SurfaceKind kind, Face face, bool inFront)
    {
        // Act
        var target = LevelMesh.TargetTile(new SurfaceRef(kind, 10, 10, face), inFront);

        // Assert
        Assert.That(target, Is.EqualTo((10, 10)));
    }

    [Test]
    public void Painting_A_Picked_Wall_Changes_That_Wall_And_Undoes()
    {
        // Arrange: looking north from (5, 5) at a wall at (5, 3)
        var document = Room();
        EditorMaps.Set(document, 0, 5, 3, Wall);
        var camera = new Camera3D();
        camera.PlaceOn(5, 5, 90);
        var (origin, direction) = camera.Ray(0, 0, 1.5f);
        var hit = Build(document).Pick(origin, direction, camera)!.Value;
        var controller = new ToolController { Document = document, Plane = 0, Value = 2 };

        // Act
        var (x, y) = LevelMesh.TargetTile(hit.Ref, inFront: false);
        controller.Press(x, y);
        controller.Release();

        // Assert
        Assert.That(document[0, 5, 3], Is.EqualTo(2));
        document.Undo();
        Assert.That(document[0, 5, 3], Is.EqualTo(Wall));
    }

    [Test]
    public void Erasing_A_Tile_Clears_The_Plane_Being_Edited()
    {
        // Arrange
        var document = Room();
        EditorMaps.Set(document, 1, 5, 5, Start);
        var controller = new ToolController { Document = document, Plane = 1 };

        // Act
        controller.EraseTile(5, 5);

        // Assert
        Assert.That(document[1, 5, 5], Is.EqualTo(0));
        Assert.That(document.UndoDescription, Is.EqualTo("Erase"));
    }

    [Test]
    public void Picking_Finds_The_Wall_In_Front_Of_The_Player_Start()
    {
        // Arrange: start at (5, 5) facing north, a wall two tiles ahead
        var document = Room();
        EditorMaps.Set(document, 1, 5, 5, Start);
        EditorMaps.Set(document, 0, 5, 3, Wall);
        var mesh = Build(document);
        var camera = new Camera3D();
        camera.PlaceOn(5, 5, 90);

        // Act: straight through the middle of the view
        var (origin, direction) = camera.Ray(0, 0, 1.5f);
        var hit = mesh.Pick(origin, direction, camera);

        // Assert
        Assert.That(hit?.Ref, Is.EqualTo(new SurfaceRef(SurfaceKind.Wall, 5, 3, Face.South)));
        Assert.That(hit!.Value.Distance, Is.EqualTo(1.5f).Within(1e-4));
    }
}
