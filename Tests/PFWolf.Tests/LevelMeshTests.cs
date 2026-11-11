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
    private const ushort Wall = 1, FourSided = 2, TwoSided = 3, Flipped = 4, Floor = 107, VerticalDoor = 90, HorizontalDoor = 91, Start = 19, Diagonal = 400;

    private static GameContent Content() => GameContent.FromDefinitions(new MapObjectTranslationAsset
    {
        Walls =
        {
            [Wall] = new() { North = "WALLNS", East = "WALLEW", South = "WALLNS", West = "WALLEW" },
            [FourSided] = new() { North = "WALLN", South = "WALLS", East = "WALLE", West = "WALLW" },
            [TwoSided] = new() { North = "WALLNS", East = "WALLEW" },
            [Flipped] = new() { North = "WALLNS", East = "WALLEW", Flip = ["South", "west"] },
        },
        Doors =
        {
            [VerticalDoor] = new() { East = "DOOR", West = "DOOR", North = "DOORSIDE", South = "DOORSIDE", Vertical = true },
            [HorizontalDoor] = new() { North = "DOOR", South = "DOOR", East = "DOORSIDE", West = "DOORSIDE" },
        },
        Floors = new MapFloorsTranslation { AreaStart = Floor, AreaCount = 37, Ambush = -1, SecretExit = -1 },
        PlayerStarts = { [Start] = new MapPlayerStartTranslation { Angles = 90 } },
        Diagonals = { [Diagonal] = new MapDiagonalTranslation { Shape = WallShape.SolidNW } },
        Flats = new MapFlatsTranslation { Ceiling = { [2] = "CEIL2" } },
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
    public void Each_Face_Shows_Its_Own_Texture()
    {
        // Arrange: a pillar with a different texture on every face
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, FourSided);

        // Act
        var mesh = Build(document);

        // Assert
        Assert.That(At(mesh, SurfaceKind.Wall, 10, 10, Face.North).Single().Texture.Name, Is.EqualTo("WALLN"));
        Assert.That(At(mesh, SurfaceKind.Wall, 10, 10, Face.South).Single().Texture.Name, Is.EqualTo("WALLS"));
        Assert.That(At(mesh, SurfaceKind.Wall, 10, 10, Face.East).Single().Texture.Name, Is.EqualTo("WALLE"));
        Assert.That(At(mesh, SurfaceKind.Wall, 10, 10, Face.West).Single().Texture.Name, Is.EqualTo("WALLW"));
    }

    [Test]
    public void A_Face_The_Wall_Does_Not_Give_Shows_The_Opposite_One()
    {
        // Arrange: a pillar giving only North and East
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, TwoSided);

        // Act
        var mesh = Build(document);

        // Assert
        Assert.That(At(mesh, SurfaceKind.Wall, 10, 10, Face.South).Single().Texture.Name, Is.EqualTo("WALLNS"));
        Assert.That(At(mesh, SurfaceKind.Wall, 10, 10, Face.West).Single().Texture.Name, Is.EqualTo("WALLEW"));
    }

    [Test]
    public void A_Flipped_Face_Runs_Its_Texture_Right_To_Left()
    {
        // Arrange: a pillar flipping its south and west faces
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, Flipped);

        // Act
        var mesh = Build(document);

        // Assert: u goes 0 to 1 across a face from its left, the other way on a flipped one
        float U(Face face) => At(mesh, SurfaceKind.Wall, 10, 10, face).Single().Uvs[0].X;
        Assert.That(U(Face.North), Is.EqualTo(0));
        Assert.That(U(Face.East), Is.EqualTo(0));
        Assert.That(U(Face.South), Is.EqualTo(1));
        Assert.That(U(Face.West), Is.EqualTo(1));
    }

    [Test]
    public void A_Lintel_Shows_The_Face_Of_The_Wall_On_Its_Side()
    {
        // Arrange: a 2-story horizontal door set into four-sided walls
        var document = Room();
        EditorMaps.Set(document, 0, 9, 10, FourSided);
        EditorMaps.Set(document, 0, 11, 10, FourSided);
        EditorMaps.Set(document, 0, 10, 10, HorizontalDoor);
        EditorMaps.Set(document, MapConstants.HeightPlane, 10, 10, 2);

        // Act
        var mesh = Build(document);

        // Assert
        static string Lintel(IEnumerable<Surface> door) => door.Single(s => s.Points.Min(p => p.Y) == 1).Texture.Name;
        Assert.That(Lintel(At(mesh, SurfaceKind.Door, 10, 10, Face.North)), Is.EqualTo("WALLN"));
        Assert.That(Lintel(At(mesh, SurfaceKind.Door, 10, 10, Face.South)), Is.EqualTo("WALLS"));
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
        EditorMaps.Set(document, MapConstants.HeightPlane, 10, 10, 3);

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
        EditorMaps.Set(document, MapConstants.HeightPlane, 10, 10, 2);

        // Act
        var door = At(Build(document), SurfaceKind.Door, 10, 10, Face.North).ToList();

        // Assert: the door to story 1, its lintel in the wall's north texture above it
        Assert.That(door.Single(s => s.Points.Max(p => p.Y) == 1).Texture.Name, Is.EqualTo("DOOR"));
        Assert.That(door.Single(s => s.Points.Min(p => p.Y) == 1).Texture.Name, Is.EqualTo("WALLNS"));
    }

    [Test]
    public void A_Lintel_Is_Flush_With_The_Wall_With_An_Underside_Over_The_Doorway()
    {
        // Arrange: a 2-story horizontal door between two walls
        var document = Room();
        EditorMaps.Set(document, 0, 9, 10, Wall);
        EditorMaps.Set(document, 0, 11, 10, Wall);
        EditorMaps.Set(document, 0, 10, 10, HorizontalDoor);
        EditorMaps.Set(document, MapConstants.HeightPlane, 10, 10, 2);

        // Act
        var mesh = Build(document);

        // Assert: the lintels on the tile's north and south edges, not the door plane, and under
        // them the face of the wall beside the door that looks into the doorway, one story up
        var north = At(mesh, SurfaceKind.Door, 10, 10, Face.North).Single(s => s.Points.Min(p => p.Y) == 1);
        var south = At(mesh, SurfaceKind.Door, 10, 10, Face.South).Single(s => s.Points.Min(p => p.Y) == 1);
        Assert.That(north.Points.All(p => p.Z == 10));
        Assert.That(south.Points.All(p => p.Z == 11));
        var underside = At(mesh, SurfaceKind.Door, 10, 10, Face.Bottom).Single();
        Assert.That(underside.Points.All(p => p.Y == 1));
        Assert.That(underside.Texture.Name, Is.EqualTo("WALLEW"));
        Assert.That(Vector3.Dot(underside.Normal, -Vector3.UnitY), Is.EqualTo(1).Within(1e-5));
    }

    [Test]
    public void A_Door_Set_Into_No_Wall_Carries_On_Up()
    {
        // Arrange: a 2-story door standing alone in the room
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, HorizontalDoor);
        EditorMaps.Set(document, MapConstants.HeightPlane, 10, 10, 2);

        // Act
        var door = At(Build(document), SurfaceKind.Door, 10, 10).ToList();

        // Assert: the door texture up to story 2 on the door plane, and no underside
        Assert.That(door.Where(s => s.Points.Min(p => p.Y) == 1).All(s => s.Texture.Name == "DOOR" && s.Points.All(p => p.Z == 10.5f)));
        Assert.That(door.Any(s => s.Ref.Face == Face.Bottom), Is.False);
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
    public void An_Arch_Is_A_Block_From_Story_Two_With_The_Wall_Ending_Its_Run_As_Texture()
    {
        // Arrange: an arch from story 2 to 3 (0x0203) across the room's width at row 10, walls at each end (the room's edge)
        var document = Room();
        for (int x = 1; x < 63; x++)
            EditorMaps.Set(document, MapConstants.HeightPlane, x, 10, 0x0203);

        // Act
        var arch = At(Build(document), SurfaceKind.Arch, 20, 10).ToList();

        // Assert: north and south faces from story 1 to 3 in the end walls' North texture, an
        // underside at story 1 facing down; no east or west faces inside the run
        Assert.That(arch.Select(s => s.Ref.Face), Is.EquivalentTo(new[] { Face.North, Face.South, Face.Bottom, Face.Top }));
        var north = arch.Single(s => s.Ref.Face == Face.North);
        Assert.That(north.Texture.Name, Is.EqualTo("WALLNS"));
        Assert.That((north.Points.Min(p => p.Y), north.Points.Max(p => p.Y)), Is.EqualTo((1f, 3f)));
        var underside = arch.Single(s => s.Ref.Face == Face.Bottom);
        Assert.That(underside.Points.All(p => p.Y == 1) && Vector3.Dot(underside.Normal, -Vector3.UnitY) > 0.99f);
    }

    [Test]
    public void A_Wall_With_Two_Blocks_Has_A_Gap_Between_Them()
    {
        // Arrange: a pillar from story 1 to 1 and 3 to 4
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, Wall);
        EditorMaps.Set(document, MapConstants.HeightPlane, 10, 10, 0x3041);

        // Act
        var north = At(Build(document), SurfaceKind.Wall, 10, 10, Face.North).ToList();

        // Assert: its north face runs 0 to 1 and 2 to 4, with each block's underside and top
        Assert.That(north.Select(s => (s.Points.Min(p => p.Y), s.Points.Max(p => p.Y))).Order(), Is.EqualTo(new[] { (0f, 1f), (2f, 4f) }));
        Assert.That(At(Build(document), SurfaceKind.Wall, 10, 10, Face.Bottom).Select(s => s.Points[0].Y), Is.EqualTo(new[] { 2f }));
    }

    [Test]
    public void A_Face_Is_Hidden_Only_Where_The_Block_Beside_It_Reaches()
    {
        // Arrange: a wall 1 to 4 beside a floor block 1 to 2
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, Wall);
        EditorMaps.Set(document, MapConstants.HeightPlane, 10, 10, 4);
        EditorMaps.Set(document, MapConstants.HeightPlane, 11, 10, 2);

        // Act
        var east = At(Build(document), SurfaceKind.Wall, 10, 10, Face.East).Single();

        // Assert: the wall's east face shows above the block only
        Assert.That((east.Points.Min(p => p.Y), east.Points.Max(p => p.Y)), Is.EqualTo((2f, 4f)));
    }

    [Test]
    public void On_A_Higher_Story_Doors_And_Things_Stand_On_The_Players_Floor()
    {
        // Arrange: the player on story 3, a door in a wall across the room, a floor block under the start
        var document = Room();
        document.SetProperties(new MapProperties { Story = 3, WallHeight = 4 });
        for (int y = 1; y < 63; y++)
            EditorMaps.Set(document, 0, 20, y, Wall);
        EditorMaps.Set(document, 0, 20, 10, VerticalDoor);
        EditorMaps.Set(document, 1, 5, 5, Start);

        // Act
        var mesh = Build(document);

        // Assert: the door from 2 to 3 with its frame below it, the start's arrow at 2
        var door = At(mesh, SurfaceKind.Door, 20, 10, Face.West).ToList();
        Assert.That(door.Select(s => (s.Points.Min(p => p.Y), s.Points.Max(p => p.Y))), Does.Contain((2f, 3f)));
        Assert.That(At(mesh, SurfaceKind.Door, 20, 10).Any(s => s.Points.Min(p => p.Y) == 0 && s.Points.Max(p => p.Y) == 2), Is.True, "the frame");
        Assert.That(At(mesh, SurfaceKind.PlayerStart, 5, 5).Single().Points[0].Y, Is.EqualTo(2.01f).Within(0.001f));
    }

    [Test]
    public void A_Block_Below_The_Players_Floor_Is_Topped_With_The_Floor()
    {
        // Arrange: on story 3, a floor block from story 1 to 2 (its top the player's floor)
        var document = Room();
        document.SetProperties(new MapProperties { Story = 3, WallHeight = 4 });
        EditorMaps.Set(document, MapConstants.HeightPlane, 10, 10, 2);

        // Act
        var top = At(Build(document), SurfaceKind.Arch, 10, 10, Face.Top).Single();

        // Assert: the floor color (no flat), at the player's floor
        Assert.That((top.Texture.Source, top.Points[0].Y), Is.EqualTo((TextureSource.Solid, 2f)));
    }

    [TestCase(0, TextureSource.Texture, "WALLEW")]
    [TestCase(2, TextureSource.Flat, "CEIL2")]
    public void An_Arches_Underside_Is_Its_Ceiling_Flat_Unless_The_Ceiling_Index_Is_0(int ceiling, TextureSource source, string name)
    {
        // Arrange: an arch from story 2 to 3 (0x0203) across the room at row 10
        var document = Room();
        for (int x = 1; x < 63; x++)
            EditorMaps.Set(document, MapConstants.HeightPlane, x, 10, 0x0203);
        EditorMaps.Set(document, MapConstants.FlatPlane, 20, 10, (ushort)(ceiling << 8));

        // Act
        var arch = At(Build(document), SurfaceKind.Arch, 20, 10).ToList();

        // Assert: the underside's texture; the top keeps the wall's
        var underside = arch.Single(s => s.Ref.Face == Face.Bottom);
        Assert.That((underside.Texture.Source, underside.Texture.Name), Is.EqualTo((source, name)));
        Assert.That(arch.Single(s => s.Ref.Face == Face.Top).Texture.Name, Is.EqualTo("WALLEW"));
    }

    [Test]
    public void An_Arch_With_No_Wall_To_Take_A_Texture_From_Is_Left_Open()
    {
        // Arrange: a single arch tile in the middle of the room: its runs end on open floor
        var document = Room();
        EditorMaps.Set(document, MapConstants.HeightPlane, 20, 20, 0x0203);

        // Act
        var mesh = Build(document);

        // Assert
        Assert.That(At(mesh, SurfaceKind.Arch, 20, 20), Is.Empty);
    }

    [Test]
    public void Surfaces_Take_The_Light_Of_Their_Zone_And_Faces_Of_The_Tile_They_Face()
    {
        // Arrange: a pillar with zone 1 (light 51, red) on the floor west of it
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, Wall);
        EditorMaps.Set(document, MapConstants.ZonePlane, 9, 10, 1);
        document.SetProperties(new MapProperties { Light = 255, Zones = [new ZoneProperties(1, Light: 51, Color: "#FF0000")] });

        // Act
        var mesh = Build(document);

        // Assert: the west face and the zoned floor in the zone's light level (51 of 255: 6 of 31)
        // tinted red, as the game's light grid gives the tile; the east face full light
        var dim = new SurfaceLight(ShadeMath.LightLevelOf(51), 0xFF0000, 9, 10);
        Assert.That(mesh.Shaded, Is.True, "a level with zones is shaded");
        Assert.That(At(mesh, SurfaceKind.Wall, 10, 10, Face.West).Single().Light, Is.EqualTo(dim), "lit from the tile it faces");
        Assert.That(At(mesh, SurfaceKind.Floor, 9, 10).Single().Light, Is.EqualTo(dim));
        Assert.That(At(mesh, SurfaceKind.Wall, 10, 10, Face.East).Single().Light, Is.EqualTo(new SurfaceLight(ShadeMath.MaxLightLevel, 0xFFFFFF, 11, 10)));
        Assert.That(At(mesh, SurfaceKind.Wall, 10, 10, Face.West).Single().Color, Is.EqualTo(Vector4.One), "the light isn't a color now");
    }

    [Test]
    public void A_Running_Scrollers_Tagged_Walls_And_Floors_Move_Their_Textures_As_The_Game_Does()
    {
        // Arrange: a pillar and a flipped pillar with tag 3, floor west of the first with it too;
        // tag 3 moves 2 texels right (east) and 1 down (south) a tic, but not on ceilings
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, Wall);
        EditorMaps.Set(document, 0, 12, 10, Flipped);
        foreach (var (x, y) in new[] { (10, 10), (12, 10), (9, 10) })
            EditorMaps.Set(document, MapConstants.TagPlane, x, y, 3);
        document.SetProperties(new MapProperties { DefaultFloor = "FLOOR", Scrollers = [new ScrollerProperties(3, X: 2, Y: 1, Ceiling: false)] });

        // Act
        var mesh = Build(document);

        // Assert: a point shows the texel left of it and above it (u runs the other way on a
        // flipped face); a floor's the texel west and north of it; untagged tiles stay still
        const float Texel = 1 / 64f;
        Assert.That(mesh.Scrolls, Is.True);
        Assert.That(At(mesh, SurfaceKind.Wall, 10, 10, Face.West).Single().Scroll, Is.EqualTo(new Vector2(-2 * Texel, -Texel)));
        Assert.That(At(mesh, SurfaceKind.Wall, 12, 10, Face.South).Single().Scroll, Is.EqualTo(new Vector2(2 * Texel, -Texel)));
        Assert.That(At(mesh, SurfaceKind.Floor, 9, 10).Single().Scroll, Is.EqualTo(new Vector2(-2 * Texel, -Texel)));
        Assert.That(At(mesh, SurfaceKind.Floor, 9, 11).Single().Scroll, Is.EqualTo(Vector2.Zero));
        Assert.That(At(mesh, SurfaceKind.Wall, 10, 10, Face.Top).Single().Scroll, Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void A_Stopped_Scroller_Moves_Nothing()
    {
        // Arrange
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, Wall);
        EditorMaps.Set(document, MapConstants.TagPlane, 10, 10, 3);
        document.SetProperties(new MapProperties { Scrollers = [new ScrollerProperties(3, X: 2, Active: false)] });

        // Act
        var mesh = Build(document);

        // Assert
        Assert.That(mesh.Scrolls, Is.False);
        Assert.That(mesh.Surfaces.All(surface => surface.Scroll == Vector2.Zero), Is.True);
    }

    [Test]
    public void The_Level_Fades_By_Its_Shading_And_Shading_Can_Be_Turned_Off()
    {
        // Arrange
        var document = Room();
        document.SetProperties(new MapProperties { FadeColor = "#808080", FadeStart = 2, FadeEnd = 10, MaxFade = 50, Light = 128 });
        var tiles = new MapTiles(Content(), document.Map, () => document.Properties);

        // Act
        var shaded = LevelMesh.Build(tiles);
        var plain = LevelMesh.Build(tiles, shade: false);

        // Assert: the game's fade steps for these settings, and its light level for 128
        Assert.That(shaded.Shaded, Is.True);
        Assert.That(shaded.Fade, Is.EqualTo(FadeParams.From(new ShadingSettings("#808080", 2, 10, 50, 128))));
        Assert.That(At(shaded, SurfaceKind.Wall, 0, 5).First().Light, Is.EqualTo(new SurfaceLight(ShadeMath.LightLevelOf(128), 0xFFFFFF, 1, 5)));
        Assert.That(plain.Shaded, Is.False);
        Assert.That(plain.Fade.MaxFadeStep, Is.Zero);
        Assert.That(At(plain, SurfaceKind.Wall, 0, 5).First().Light, Is.EqualTo(SurfaceLight.Full));
    }

    [Test]
    public void Floor_And_Ceiling_Colors_Are_Game_Colors_Marks_Are_The_Editors()
    {
        // Arrange
        var document = Room();
        document.SetProperties(new MapProperties { FloorColor = "#707070", Light = 200 });
        EditorMaps.Set(document, 1, 6, 6, Start);

        // Act
        var mesh = Build(document);

        // Assert: a floor color is lit (the shader takes its nearest palette entry); marks aren't
        var floor = At(mesh, SurfaceKind.Floor, 5, 5).Single();
        Assert.That(floor.Light, Is.EqualTo(new SurfaceLight(ShadeMath.LightLevelOf(200), 0xFFFFFF, 5, 5)));
        Assert.That(floor.Color, Is.EqualTo(new Vector4(0x70 / 255f, 0x70 / 255f, 0x70 / 255f, 1)));
        Assert.That(At(mesh, SurfaceKind.PlayerStart, 6, 6).Single().Light, Is.EqualTo(SurfaceLight.Editor));
    }

    [Test]
    public void The_Levels_Light_Grid_Is_The_Games_As_It_Starts()
    {
        // Arrange: the room, dim (light 64), a wall stub and a shut door, an amber lamp and a dim
        // set light as extra things, as the game would cast them
        var lamp = new ActorData();
        lamp.Properties["light.intensity"] = 220;
        lamp.Properties["light.radius"] = 4;
        lamp.Properties["light.color"] = "#FFB060";
        var bulb = new ActorData();
        bulb.Properties["light.intensity"] = 20;
        bulb.Properties["light.radius"] = 3;
        bulb.Properties["light.mode"] = "set";
        var content = GameContent.FromDefinitions(Content().MapDefs, actors: new() { ["Lamp"] = lamp, ["Bulb"] = bulb });
        var document = Room();
        document.SetProperties(new MapProperties { Light = 64, MaxFade = 0 });
        EditorMaps.Set(document, 0, 12, 10, Wall);
        EditorMaps.Set(document, 0, 10, 12, HorizontalDoor);
        document.Map.ExtraThings = [new MapThing("Lamp", 10.5, 10.5, 0), new MapThing("Bulb", 20.5, 20.5, 0)];

        // Act
        var mesh = LevelMesh.Build(new MapTiles(content, document.Map, () => document.Properties));

        // The game's: the same walls and door (shut), the same lights where the level puts them
        var session = new GameSession();
        var map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets")), null!, session, null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        typeof(MapManager).GetField("mapsegs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(map, Enumerable.Range(0, MapManager.LevelPlanes).Select(_ => new ushort[MapConstants.ClassicMapArea]).ToArray());
        for (int i = 0; i < MapConstants.ClassicMapSize; i++)
            map.TileMap[i, 0] = map.TileMap[i, 63] = map.TileMap[0, i] = map.TileMap[63, i] = 1;
        map.TileMap[12, 10] = 1;
        map.TileMap[10, 12] = GameConstants.BitDoor;
        var doors = new Doors(null!, null!, map, null!, null!, session, null!);
        doors.DoorObjList[0] = new DoorObject { TileX = 10, TileY = 12, Action = DoorAction.Closed };
        ActorLights? lights = null;
        var lighting = new Lighting(null!, LightingTests.GreyVideo(), map,
            new LightingHooks(() => 0, () => lights?.ColorTintTables.Clear(), tics => lights?.TickActorLights(tics)));
        lights = new ActorLights(map, doors, lighting, session);
        lighting.SetShading(new ShadingSettings("#000000", 0, 16, 0, 64));
        lighting.UpdateLightGrid();
        foreach (var (name, x, y, data) in new[] { ("Lamp", 10.5, 10.5, lamp), ("Bulb", 20.5, 20.5, bulb) })
        {
            var actor = new PFWolf.Entities.Actors.Monster { Name = name, X = (int)(x * 65536), Y = (int)(y * 65536), TileX = (byte)x, TileY = (byte)y };
            foreach (var (key, value) in data.Properties)
                actor.Properties[key] = value;
            map.GetActors().AddLast(actor);
        }
        lights.CastActorLights();

        // Assert: cell for cell, tile for tile, tint for tint
        var grid = mesh.Grid!;
        Assert.That(grid.TileLevel, Is.EqualTo(lighting.TileLevel));
        Assert.That(grid.CellAdd, Is.EqualTo(lighting.CellAdd));
        Assert.That(grid.CellSet, Is.EqualTo(lighting.CellSet));
        Assert.That(grid.CellTint, Is.EqualTo(lighting.CellTint));
        Assert.That(grid.Tints, Is.EqualTo(lighting.Tints));
        Assert.That(grid.CellAdd.Any(add => add > 0), Is.True);
    }

    // The game's level for the light tests: the room's walls, empty planes, its Lighting and ActorLights
    private static (MapManager Map, Lighting Lighting, ActorLights Lights) GameRoom()
    {
        var session = new GameSession();
        var map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets")), null!, session, null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        typeof(MapManager).GetField("mapsegs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(map, Enumerable.Range(0, MapManager.LevelPlanes).Select(_ => new ushort[MapConstants.ClassicMapArea]).ToArray());
        for (int i = 0; i < MapConstants.ClassicMapSize; i++)
            map.TileMap[i, 0] = map.TileMap[i, 63] = map.TileMap[0, i] = map.TileMap[63, i] = 1;
        ActorLights? lights = null;
        var lighting = new Lighting(null!, LightingTests.GreyVideo(), map,
            new LightingHooks(() => 0, () => lights?.ColorTintTables.Clear(), tics => lights?.TickActorLights(tics)));
        lights = new ActorLights(map, new Doors(null!, null!, map, null!, null!, session, null!), lighting, session);
        return (map, lighting, lights);
    }

    [TestCase(10)]
    [TestCase(35)]
    [TestCase(61)]
    public void A_Zones_Pulse_Moves_Its_Tiles_Light_As_The_Games_Does(int tics)
    {
        // Arrange: zone 1 over a patch of the room, pulsing from 200 down to 40 every 70 tics
        var document = Room();
        for (int x = 20; x < 24; x++)
            EditorMaps.Set(document, MapConstants.ZonePlane, x, 20, 1);
        document.SetProperties(new MapProperties { Light = 120, MaxFade = 0, Zones = [new ZoneProperties(1, Light: 200, Effect: "pulse", Low: 40, Tics: 70)] });
        var mesh = Build(document);

        var (map, lighting, _) = GameRoom();
        for (int x = 20; x < 24; x++)
            map.SetZone(x, 20, 1);
        lighting.LevelZones = ShadingResolver.ResolveZones(document.Properties.ToShadingMapInfo(), new DefaultMapInfo());
        lighting.SetShading(new ShadingSettings("#000000", 0, 16, 0, 120));
        lighting.UpdateLightGrid();

        // Act
        bool moved = mesh.TickEffects(tics);
        lighting.TickZoneLights((uint)tics);
        lighting.UpdateLightGrid();

        // Assert
        Assert.That(mesh.Animates, Is.True);
        Assert.That(moved, Is.True);
        Assert.That(mesh.Grid!.TileLevel, Is.EqualTo(lighting.TileLevel));
    }

    [TestCase(12)]
    [TestCase(40)]
    public void A_Lamps_Pulse_Is_Cast_As_The_Games_Is(int tics)
    {
        // Arrange: a pulsing amber lamp in the dim room
        var lamp = new ActorData();
        foreach (var (key, value) in new (string, object)[] { ("light.intensity", 220), ("light.radius", 4), ("light.color", "#FFB060"),
                     ("light.effect", "pulse"), ("light.low", 30), ("light.tics", 50) })
            lamp.Properties[key] = value;
        var content = GameContent.FromDefinitions(Content().MapDefs, actors: new() { ["Lamp"] = lamp });
        var document = Room();
        document.SetProperties(new MapProperties { Light = 64, MaxFade = 0 });
        document.Map.ExtraThings = [new MapThing("Lamp", 10.5, 10.5, 0)];
        var mesh = LevelMesh.Build(new MapTiles(content, document.Map, () => document.Properties));

        var (map, lighting, lights) = GameRoom();
        lighting.SetShading(new ShadingSettings("#000000", 0, 16, 0, 64));
        lighting.UpdateLightGrid();
        var actor = new PFWolf.Entities.Actors.Monster { Name = "Lamp", X = (int)(10.5 * 65536), Y = (int)(10.5 * 65536), TileX = 10, TileY = 10 };
        foreach (var (key, value) in lamp.Properties)
            actor.Properties[key] = value;
        map.GetActors().AddLast(actor);
        lights.CastActorLights();

        // Act
        mesh.TickEffects(tics);
        lights.TickActorLights((uint)tics);
        lights.CastActorLights();

        // Assert
        Assert.That(mesh.Grid!.CellAdd, Is.EqualTo(lighting.CellAdd));
        Assert.That(mesh.Grid.CellTint, Is.EqualTo(lighting.CellTint));
    }

    // The 3D view's fragment shader's FadeStep (LevelView3D.FragmentShader), in the same 32-bit
    // float arithmetic: it has to give ShadeMath.ShadeOffset's steps
    private static int ShaderFadeStep(float nx, FadeParams fade)
    {
        int most = fade.MaxFadeStep;
        float start = fade.Start, end = fade.End;
        if (nx <= start || most == 0)
            return 0;
        if (fade.Inverse)
            return most - (int)MathF.Floor(most * start / nx);
        if (nx >= end)
            return most;
        return (int)MathF.Floor((nx - start) * most / (end - start));
    }

    [TestCase(4.52, 16, 80, true)]      // Blake Stone's
    [TestCase(1.07, 16, 57, true)]      // a Planet Strike floor's
    [TestCase(2, 10, 50, false)]
    [TestCase(0, 16, 100, false)]
    public void The_Shaders_Fade_Steps_Are_The_Games(double start, double end, int maxFade, bool inverse)
    {
        var fade = FadeParams.From(new ShadingSettings("#000000", start, end, maxFade, 255, inverse));
        for (float tiles = 0; tiles <= 64; tiles += 1 / 64f)
        {
            float nx = tiles * 65536f;
            Assert.That(ShaderFadeStep(nx, fade), Is.EqualTo(ShadeMath.ShadeOffset((long)nx, fade) / 256), $"{tiles} tiles");
        }
    }

    [Test]
    public void A_Sky_The_Game_Has_Replaces_The_Ceiling_Color_But_Not_A_Missing_One()
    {
        // Arrange: no picture of that name here, so the ceiling stays
        var document = Room();
        document.SetProperties(new MapProperties { Sky = "NOSUCHSKY" });

        // Act
        var mesh = Build(document);

        // Assert
        Assert.That(mesh.Sky, Is.Null);
        Assert.That(At(mesh, SurfaceKind.Ceiling, 5, 5), Is.Not.Empty);
    }

    private static bool Near(Vector4 a, Vector4 b) => Vector4.Distance(a, b) < 1e-4f;

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
