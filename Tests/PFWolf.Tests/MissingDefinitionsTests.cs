using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;

namespace PFWolf.Tests;

public class MissingDefinitionsTests
{
    private const ushort Wall = 1, Floor = 107, Door = 90, Puddle = 23, Patch = 2;

    private static MapTiles Tiles(Action<MapAsset> arrange)
    {
        var content = GameContent.FromDefinitions(new MapObjectTranslationAsset
        {
            Walls = new() { [Wall] = new() { North = "GSTONEA1" } },
            Doors = new() { [Door] = new() { East = "DOOR", Vertical = true } },
            Floors = new MapFloorsTranslation { AreaStart = Floor, AreaCount = 37, Ambush = 106, SecretExit = -1 },
            Things = new() { [Puddle] = new MapActorTranslation { Class = "Puddle" } },
            Patches = new() { [Patch] = new MapPatchTranslation { Texture = "SIGN" } },
        });
        var map = MapFiles.NewMap("Test", Wall, Floor);
        arrange(map);
        return new MapTiles(content, map);
    }

    private static int At(int x, int y) => y * 64 + x;

    [Test]
    public void A_Tile_The_Mapdefs_Know_Has_Nothing_Missing()
    {
        // Arrange
        var tiles = Tiles(map => map.MapData[1][At(5, 5)] = Puddle);

        // Act
        var missing = tiles.MissingDefinitions(5, 5).ToList();

        // Assert
        Assert.That(missing, Is.Empty);
    }

    [Test]
    public void An_Unknown_Plane_0_Value_Could_Be_A_Wall_A_Door_Or_A_Floor_Code()
    {
        // Arrange
        var tiles = Tiles(map => map.MapData[0][At(5, 5)] = 60);

        // Act
        var missing = tiles.MissingDefinitions(5, 5).ToList();

        // Assert
        Assert.That(missing, Is.EqualTo(new[] { ("walls", 60), ("doors", 60), ("floors", 60) }));
    }

    [Test]
    public void Plane_0_Value_0_Is_Not_Offered()
    {
        // Arrange
        var tiles = Tiles(map => map.MapData[0][At(5, 5)] = 0);

        // Act
        var missing = tiles.MissingDefinitions(5, 5).ToList();

        // Assert
        Assert.That(missing, Is.Empty);
    }

    [Test]
    public void An_Unknown_Patch_Is_Missing_By_Its_Id()
    {
        // Arrange: wall 1 with patch 7, which the mapdefs don't have
        var tiles = Tiles(map => map.MapData[0][At(0, 0)] = (ushort)(Wall | 7 << MapConstants.PatchShift));

        // Act
        var missing = tiles.MissingDefinitions(0, 0).ToList();

        // Assert
        Assert.That(missing, Is.EqualTo(new[] { ("patches", 7) }));
    }

    [Test]
    public void An_Unknown_Object_Value_On_Floor_Could_Be_A_Thing_Trigger_Or_Start_But_Not_A_Diagonal()
    {
        // Arrange
        var tiles = Tiles(map => map.MapData[1][At(5, 5)] = 345);

        // Act
        var tables = tiles.MissingDefinitions(5, 5).Select(missing => missing.Table).ToList();

        // Assert
        Assert.That(tables, Is.EqualTo(new[] { "things", "triggers", "player-starts" }));
    }

    [Test]
    public void An_Unknown_Object_Value_On_A_Wall_Could_Be_A_Diagonal_Too()
    {
        // Arrange
        var tiles = Tiles(map => map.MapData[1][At(0, 0)] = 345);

        // Act
        var tables = tiles.MissingDefinitions(0, 0).Select(missing => missing.Table).ToList();

        // Assert
        Assert.That(tables, Does.Contain("diagonals"));
    }

    [Test]
    public void A_Known_Floor_Tile_Goes_To_The_Floors_Codes_And_Its_Thing()
    {
        // Arrange
        var tiles = Tiles(map => map.MapData[1][At(5, 5)] = Puddle);

        // Act
        var known = tiles.KnownDefinitions(5, 5).ToList();

        // Assert
        Assert.That(known, Is.EqualTo(new[] { ("floors", (int)Floor), ("things", (int)Puddle) }));
    }

    [Test]
    public void A_Known_Wall_Goes_To_Its_Wall_And_Its_Patch()
    {
        // Arrange
        var tiles = Tiles(map => map.MapData[0][At(0, 0)] = (ushort)(Wall | Patch << MapConstants.PatchShift));

        // Act
        var known = tiles.KnownDefinitions(0, 0).ToList();

        // Assert
        Assert.That(known, Is.EqualTo(new[] { ("walls", (int)Wall), ("patches", (int)Patch) }));
    }

    [Test]
    public void A_Tile_The_Mapdefs_Dont_Know_Has_Nowhere_To_Go()
    {
        // Arrange
        var tiles = Tiles(map =>
        {
            map.MapData[0][At(5, 5)] = 60;
            map.MapData[1][At(5, 5)] = 345;
        });

        // Act
        var known = tiles.KnownDefinitions(5, 5).ToList();

        // Assert
        Assert.That(known, Is.Empty);
    }
}
