using System.Numerics;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;
using PFWolf.Editor.Rendering;

namespace PFWolf.Tests;

/// <summary>
/// The editor 3D view's things that turn: each shows the view toward the camera, as the game's
/// CalcRotate picks it from the way the thing faces. The level tests need a built game folder
/// with Wolf3D's data files.
/// </summary>
public class EditorThingViewsTests
{
    private static readonly Vector3 Spot = new(10.5f, 0, 10.5f);

    // An eye a few tiles from the spot, the game's way (degrees anticlockwise from east, north up the map)
    private static Vector3 EyeAt(float degrees)
    {
        float radians = degrees * MathF.PI / 180;
        return Spot + new Vector3(MathF.Cos(radians), 0.5f, -MathF.Sin(radians)) * 3;
    }

    [TestCase(0, 0, 0)]         // facing east, seen from the east: its front (view 1)
    [TestCase(0, 20, 0)]        // still within the front's eighth
    [TestCase(0, 30, 1)]        // past it: the next view round
    [TestCase(0, 90, 2)]        // seen from the north: view 3
    [TestCase(0, 180, 4)]       // from behind: view 5
    [TestCase(0, 270, 6)]
    [TestCase(90, 90, 0)]       // facing north, seen from the north
    [TestCase(90, 270, 4)]
    [TestCase(270, 0, 2)]       // facing south, seen from the east
    public void A_Thing_Shows_The_View_Toward_The_Eye(int facing, float eye, int view)
    {
        Assert.That(Billboard.ViewIndex(Spot, facing, EyeAt(eye)), Is.EqualTo(view));
    }

    [Test]
    public void A_Thing_Facing_No_Way_Shows_Its_Front_To_Everyone()
    {
        Assert.That(new[] { 0f, 90, 180, 270 }.Select(eye => Billboard.ViewIndex(Spot, null, EyeAt(eye))), Is.All.EqualTo(0));
    }

    [Test]
    public void A_Billboard_That_Doesnt_Turn_Looks_The_Same_From_Everywhere()
    {
        var barrel = new Billboard { Base = Spot, Texture = new TextureRef(TextureSource.Sprite, "Barrel") };
        Assert.That(barrel.SeenFrom(EyeAt(180)), Is.SameAs(barrel));
    }

    [Test, Category("GameData")]
    public void E1M1s_Guards_Show_Their_Front_From_Ahead_And_Their_Back_From_Behind()
    {
        // Arrange
        var content = Load();
        var document = new MapDocument("map01", content.FindMap("map01")!);
        var mesh = LevelMesh.Build(new MapTiles(content, document.Map, () => document.Properties));
        var guards = mesh.Billboards.Where(sprite => sprite.Views != null && sprite.Facing != null).ToList();
        Assume.That(guards, Is.Not.Empty);

        // Act: each seen from a tile ahead of it and a tile behind it
        var seen = guards.Select(guard =>
        {
            float radians = guard.Facing!.Value * MathF.PI / 180;
            var ahead = new Vector3(MathF.Cos(radians), 0, -MathF.Sin(radians));
            return (Front: guard.SeenFrom(guard.Base + ahead).Texture, Back: guard.SeenFrom(guard.Base - ahead).Texture);
        }).ToList();

        // Assert: GARDA1 from ahead, GARDA5 from behind, each by its own name
        Assert.That(seen.Select(view => view.Front), Has.Some.EqualTo(new TextureRef(TextureSource.SpriteView, "GARDA1")));
        Assert.That(seen.Select(view => view.Back), Has.Some.EqualTo(new TextureRef(TextureSource.SpriteView, "GARDA5")));
        Assert.That(seen.All(view => view.Front.Name.EndsWith('1') && view.Back.Name.EndsWith('5')), Is.True);
    }

    [Test, Category("GameData")]
    public void A_Views_Picture_Is_Found_By_Its_Own_Name()
    {
        // Arrange
        var content = Load();
        using var art = new ArtCache(content);

        // Act
        var pixels = art.IndexedPixels(new TextureRef(TextureSource.SpriteView, "GARDA5"));

        // Assert
        Assert.That(content.ThingViews("Guard"), Is.EqualTo(Enumerable.Range(1, 8).Select(view => $"GARDA{view}")));
        Assert.That(pixels, Is.Not.Null);
    }

    private static GameContent? _content;

    private static GameContent Load()
    {
        if (_content != null)
            return _content;
        var gameBin = Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64");
        var folder = new[] { Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA"), Path.Combine(gameBin, "Debug", "net10.0"), Path.Combine(gameBin, "Release", "net10.0") }
            .FirstOrDefault(folder => !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, "pfwolf.pk3")));
        if (folder == null)
            Assert.Ignore("No built game folder with pfwolf.pk3; build PFWolf for x64 or set PFWOLF_GAMEDATA");

        var workingFolder = Directory.GetCurrentDirectory();
        try
        {
            return _content = GameContent.Load(folder!, "wolf3d", []);
        }
        catch (PFWolf.Exceptions.DataFilesException)
        {
            Assert.Ignore($"Wolf3D's data files aren't in {folder}");
            throw;
        }
        finally
        {
            Directory.SetCurrentDirectory(workingFolder);
        }
    }
}
