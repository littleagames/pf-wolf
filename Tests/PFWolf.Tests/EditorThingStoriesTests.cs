using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;

namespace PFWolf.Tests;

/// <summary>The editor's thing stories (plane 6): MapTiles.ThingStory, the tile info and the level checks</summary>
public class EditorThingStoriesTests
{
    private const ushort Wall = 1, Floor = 107, Start = 19, Puddle = 23, Medkit = 24;

    private static GameContent Content() => GameContent.FromDefinitions(new MapObjectTranslationAsset
    {
        Walls = new() { [Wall] = new() { North = "GSTONEA1", East = "GSTONEA2", South = "GSTONEA1", West = "GSTONEA2" } },
        Floors = new MapFloorsTranslation { AreaStart = Floor, AreaCount = 37, Ambush = 106, SecretExit = -1 },
        PlayerStarts = new() { [Start] = new MapPlayerStartTranslation { Angles = 90 } },
        Things = new()
        {
            [Puddle] = new MapActorTranslation { Class = "Puddle" },
            [Medkit] = new MapActorTranslation { Class = "Health" },     // an item kind by its own name
        },
    });

    // Rooftops on story 3: walls 2 stories tall, open floor (a street) at x 10
    private static MapDocument Rooftops()
    {
        var map = MapFiles.NewMap("Test", Wall, Floor);
        map.MapData[1][5 * 64 + 5] = Start;
        var document = new MapDocument("MAP01", map, properties: new MapProperties { Story = 3, WallHeight = 2 });
        for (int y = 1; y < 63; y++)
            for (int x = 1; x < 63; x++)
                EditorMaps.Set(document, 0, x, y, x == 10 ? Floor : Wall);
        return document;
    }

    private static MapTiles Tiles(MapDocument document) => new(Content(), document.Map, () => document.Properties);

    private static List<string> Problems(MapDocument document) =>
        LevelChecks.Run(Tiles(document), inGameInfo: true).Select(p => p.Message).ToList();

    [Test]
    public void A_Thing_Stands_On_The_Floor_Under_It_Or_Its_Plane_6_Story()
    {
        // Arrange
        var document = Rooftops();
        EditorMaps.Set(document, MapConstants.StoryPlane, 11, 20, 5);
        var tiles = Tiles(document);

        // Act / Assert: the roofs at the player's story, the street on the ground, plane 6 over both
        Assert.That(tiles.ThingStory(9, 20), Is.EqualTo(3));
        Assert.That(tiles.ThingStory(10, 20), Is.EqualTo(1));
        Assert.That(tiles.ThingStory(11, 20), Is.EqualTo(5));
        Assert.That(tiles.ThingStory(10, 20, set: 2), Is.EqualTo(2), "an extra thing's own setting");
    }

    [Test]
    public void The_3D_View_Shows_A_Thing_On_A_Roof()
    {
        // Arrange: a puddle on a roof (a wall under the player's story), as the Rooftops Demo's vase on its sign
        var document = Rooftops();
        EditorMaps.Set(document, 1, 9, 20, Puddle);

        // actordefs without a Puddle, so it's marked rather than drawn (the test has no sprites)
        var content = GameContent.FromDefinitions(new MapObjectTranslationAsset
        {
            Walls = new() { [Wall] = new() { North = "GSTONEA1", East = "GSTONEA2", South = "GSTONEA1", West = "GSTONEA2" } },
            Floors = new MapFloorsTranslation { AreaStart = Floor, AreaCount = 37, Ambush = 106, SecretExit = -1 },
            Things = new() { [Puddle] = new MapActorTranslation { Class = "Puddle" } },
        }, actors: new() { ["Guard"] = new ActorData() });

        // Act
        var mesh = PFWolf.Editor.Rendering.LevelMesh.Build(new MapTiles(content, document.Map, () => document.Properties));

        // Assert
        Assert.That(mesh.Surfaces.Where(s => s.Ref.Kind == PFWolf.Editor.Rendering.SurfaceKind.Thing && s.Ref.X == 9 && s.Ref.Y == 20), Is.Not.Empty);
    }

    [Test]
    public void The_Tile_Info_Says_A_Things_Story_Off_The_Players()
    {
        var document = Rooftops();
        EditorMaps.Set(document, 1, 10, 20, Puddle);
        Assert.That(Tiles(document).Describe(10, 20), Has.Some.Contains("stand on story 1"));
    }

    [Test]
    public void An_Item_Off_The_Players_Story_Is_Pointed_Out()
    {
        // Arrange: a medkit in the street, a puddle beside it
        var document = Rooftops();
        EditorMaps.Set(document, 1, 10, 20, Medkit);
        EditorMaps.Set(document, 1, 10, 21, Puddle);

        // Act
        var problems = Problems(document);

        // Assert
        Assert.That(problems, Has.One.Contains("an item on story 1 can't be picked up"));
    }

    [Test]
    public void A_Thing_Set_Inside_A_Block_On_Its_Story_Is_Pointed_Out()
    {
        var document = Rooftops();
        EditorMaps.Set(document, 1, 11, 20, Puddle);
        EditorMaps.Set(document, MapConstants.StoryPlane, 11, 20, 2);     // inside the 2-story wall
        Assert.That(Problems(document), Has.One.Contains("inside a block on that story"));
    }

    [Test]
    public void A_Thing_Story_That_Isnt_One_Is_Pointed_Out()
    {
        var document = Rooftops();
        EditorMaps.Set(document, MapConstants.StoryPlane, 11, 20, 12);
        Assert.That(Problems(document), Has.One.Contains("thing story 12 isn't a story"));
    }
}
