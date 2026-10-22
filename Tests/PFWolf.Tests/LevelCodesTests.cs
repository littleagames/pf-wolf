using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;

namespace PFWolf.Tests;

/// <summary>Blake Stone's level codes in the editor: placing, editing and removing them, and the tools keeping a code with its value</summary>
public class LevelCodesTests
{
    private const ushort Floor = 100, Wall = 1, SmartSwitch = 158, Puddle = 23;

    private static GameContent Content() => GameContent.FromDefinitions(new MapObjectTranslationAsset
    {
        Walls = { [Wall] = new() { North = "WALL" } },
        Floors = new MapFloorsTranslation { AreaStart = Floor, AreaCount = 10, Triggers = new() { [SmartSwitch] = "A_SmartSwitch(\"toggle\")" } },
        Things = { [Puddle] = new MapActorTranslation { Class = "Puddle" } },
        MapInfo =
        {
            [0xFE] = MapInfoCodes.CeilingFloorColors,
            [0xFB] = MapInfoCodes.CeilingFloorFlats,
            [0xF8] = MapInfoCodes.TagLink,
            [0xF4] = MapInfoCodes.None,
        },
    });

    private static (MapDocument Document, MapTiles Tiles) Level()
    {
        var document = EditorMaps.Document();
        return (document, new MapTiles(Content(), document.Map, () => document.Properties));
    }

    private static ToolController Tools(MapDocument document, MapTiles tiles, ushort value) => new()
    {
        Document = document,
        Tool = EditTool.Paint,
        Plane = 1,
        Value = value,
        Pairs = tiles,
    };

    [Test]
    public void Level_Colors_Go_In_The_Top_Row_Then_Are_Edited_There()
    {
        // Arrange: the top row's first tile is taken
        var (document, tiles) = Level();
        EditorMaps.Set(document, 1, 0, 0, Puddle);

        // Act
        var place = LevelCodes.PlaceFor(tiles, MapInfoCodes.CeilingFloorColors, 30, 30)!.Value;
        var form = LevelCodes.NewForm(tiles, 0xFE, MapInfoCodes.CeilingFloorColors, place.X, place.Y);
        LevelCodes.Apply(document, form, [0x2F, 0x19], "Colors");
        var again = LevelCodes.PlaceFor(tiles, MapInfoCodes.CeilingFloorColors, 30, 30);
        var edit = LevelCodes.EditForm(tiles, place.X, place.Y)!;
        LevelCodes.Apply(document, edit, [1, 2], "Colors");

        // Assert
        Assert.That(place, Is.EqualTo((1, 0)));
        Assert.That(again, Is.EqualTo((1, 0)), "only the first counts, so it's edited");
        Assert.That(edit.Fields.Select(field => field.Initial), Is.EqualTo(new[] { 0x2F, 0x19 }));
        Assert.That(document[1, 1, 0], Is.EqualTo(0xFE00));
        Assert.That(document[1, 2, 0], Is.EqualTo(0x0102));
        Assert.That(tiles.IsValueTile(2, 0));
        Assert.That(LevelCodes.List(tiles).Select(code => (code.X, code.Y)), Is.EqualTo(new[] { (1, 0) }));
    }

    [Test]
    public void A_Tag_Link_Form_Writes_Its_Floor_And_Target()
    {
        // Arrange
        var (document, tiles) = Level();
        var form = LevelCodes.NewForm(tiles, 0xF8, MapInfoCodes.TagLink, 10, 10);

        // Act
        LevelCodes.Apply(document, form, [0xFF, 20, 21], "Link");
        var link = tiles.LinkFrom(10, 10);

        // Assert
        Assert.That(form.PicksTile);
        Assert.That(document[1, 10, 10], Is.EqualTo(0xF8FF));
        Assert.That(link?.Kind, Is.EqualTo(ObjectLinkKind.TagLink));
        Assert.That((link!.ToX, link.ToY), Is.EqualTo((20, 21)));
        Assert.That(link.OnThisFloor);
    }

    [Test]
    public void A_Smart_Switch_Floor_Code_Names_The_Tile_It_Works()
    {
        // Arrange
        var (document, tiles) = Level();
        EditorMaps.Set(document, 0, 5, 5, SmartSwitch);
        EditorMaps.Set(document, 1, 5, 5, LevelCodes.Tile(12, 13));

        // Act
        var form = LevelCodes.EditForm(tiles, 5, 5)!;
        LevelCodes.Apply(document, form, [7, 8], "Target");

        // Assert
        Assert.That(tiles.ObjectKnown(5, 5));
        Assert.That(form.Fields.Select(field => field.Initial), Is.EqualTo(new[] { 12, 13 }));
        Assert.That(tiles.LinkFrom(5, 5) is { ToX: 7, ToY: 8 });
    }

    [Test]
    public void Painting_Leaves_A_Codes_Value_And_Takes_It_With_The_Code()
    {
        // Arrange: a colors code at (3, 3), its value (a puddle's number) at (4, 3)
        var (document, tiles) = Level();
        EditorMaps.Set(document, 1, 3, 3, 0xFE00);
        EditorMaps.Set(document, 1, 4, 3, Puddle);
        var tools = Tools(document, tiles, 0);

        // Act / Assert: painting nothing over the value leaves it
        tools.Press(4, 3);
        tools.Release();
        Assert.That(document[1, 4, 3], Is.EqualTo(Puddle));
        Assert.That(tiles.Thing(4, 3), Is.Null, "a value is never a thing");

        // Act / Assert: over the code, both go
        tools.Press(3, 3);
        tools.Release();
        Assert.That(document[1, 3, 3], Is.EqualTo(0));
        Assert.That(document[1, 4, 3], Is.EqualTo(0));
        document.Undo();
        Assert.That(document[1, 4, 3], Is.EqualTo(Puddle), "one undo step puts both back");
    }

    [Test]
    public void Painting_A_Code_Puts_Its_Value_After_It()
    {
        // Arrange
        var (document, tiles) = Level();
        var tools = Tools(document, tiles, 0xF8FF);
        tools.PairValue = LevelCodes.Tile(30, 31);

        // Act: a stroke along a row puts codes down with a value after each
        tools.Press(10, 10);
        tools.Move(13, 10);
        tools.Release();

        // Assert
        Assert.That(document[1, 10, 10], Is.EqualTo(0xF8FF));
        Assert.That(document[1, 11, 10], Is.EqualTo(LevelCodes.Tile(30, 31)));
        Assert.That(document[1, 12, 10], Is.EqualTo(0xF8FF));
        Assert.That(document[1, 13, 10], Is.EqualTo(LevelCodes.Tile(30, 31)));
    }

    [Test]
    public void Deleting_A_Code_Takes_Its_Value_With_It()
    {
        // Arrange
        var (document, tiles) = Level();
        EditorMaps.Set(document, 1, 3, 3, 0xFB00);
        EditorMaps.Set(document, 1, 4, 3, 0x0A0B);

        // Act
        LevelCodes.Delete(document, tiles, 3, 3);

        // Assert
        Assert.That(document[1, 3, 3], Is.EqualTo(0));
        Assert.That(document[1, 4, 3], Is.EqualTo(0));
    }

    [Test]
    public void Clearing_A_Selection_With_A_Code_Clears_Its_Value_Outside_It()
    {
        // Arrange
        var (document, tiles) = Level();
        EditorMaps.Set(document, 1, 3, 3, 0xFE00);
        EditorMaps.Set(document, 1, 4, 3, 0x0102);
        var tools = Tools(document, tiles, 0);
        tools.Tool = EditTool.Select;
        tools.Press(3, 3);
        tools.Release();

        // Act
        tools.Delete();

        // Assert
        Assert.That(document[1, 3, 3], Is.EqualTo(0));
        Assert.That(document[1, 4, 3], Is.EqualTo(0));
    }

    [Test]
    public void The_Checks_Find_A_Second_Level_Code_And_A_Link_To_Nothing()
    {
        // Arrange: two colors codes, and a smart switch working an empty tile then a puddle
        var (document, tiles) = Level();
        EditorMaps.Set(document, 1, 0, 0, 0xFE00);
        EditorMaps.Set(document, 1, 1, 0, 0x0102);
        EditorMaps.Set(document, 1, 10, 10, 0xFE00);
        EditorMaps.Set(document, 1, 11, 10, 0x0304);
        EditorMaps.Set(document, 0, 5, 5, SmartSwitch);
        EditorMaps.Set(document, 1, 5, 5, LevelCodes.Tile(20, 20));
        EditorMaps.Set(document, 0, 6, 5, SmartSwitch);
        EditorMaps.Set(document, 1, 6, 5, LevelCodes.Tile(21, 20));
        EditorMaps.Set(document, 1, 21, 20, Puddle);

        // Act
        var problems = LevelChecks.Run(tiles, inGameInfo: true).Select(problem => problem.ToString()).ToList();

        // Assert
        Assert.That(problems, Has.One.Contains("1 more ceiling-floor-colors code: the game takes only the first, at (0, 0)"));
        Assert.That(problems, Has.One.Contains("smart switch to (20, 20): there's nothing there to work"));
        Assert.That(problems, Has.None.Contains("(21, 20)"));
        Assert.That(problems, Has.None.Contains("isn't in the mapdefs"), "codes, values and links are known");
    }

    [Test]
    public void A_Low_Byte_Code_Has_No_Value_Tile()
    {
        // Arrange: a teleporter's floor code (0xF4nn) keeps its value in its own low byte
        var (document, tiles) = Level();

        // Act
        var form = LevelCodes.NewForm(tiles, 0xF4, MapInfoCodes.None, 6, 6);
        LevelCodes.Apply(document, form, [3], "Floor");

        // Assert
        Assert.That(document[1, 6, 6], Is.EqualTo(0xF403));
        Assert.That(tiles.HasValueTile(6, 6), Is.False);
        Assert.That(LevelCodes.ValueTile(tiles, 6, 6), Is.Null);
    }
}
