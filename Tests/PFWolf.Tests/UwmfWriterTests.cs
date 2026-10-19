using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Enums;
using PFWolf.Loaders.Uwmf;

namespace PFWolf.Tests;

public class UwmfWriterTests
{
    private const ushort Wall = 1, Area = 107, Ambush = 106;

    // A 6x5 level of stone round open floor
    private static MapAsset Room()
    {
        var planes = new ushort[MapConstants.LevelPlanes][];
        for (int p = 0; p < planes.Length; p++)
            planes[p] = new ushort[6 * 5];
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 6; x++)
                planes[0][y * 6 + x] = x == 0 || y == 0 || x == 5 || y == 4 ? Wall : Area;
        return new MapAsset { Name = "Room", Width = 6, Height = 5, MapData = planes };
    }

    private static void Set(MapAsset map, int plane, int x, int y, int value) => map.MapData[plane][y * map.Width + x] = (ushort)value;

    private static (UwmfMap Uwmf, MapAsset Back) RoundTrip(MapAsset map, MapObjectTranslationAsset? defs = null)
    {
        defs ??= UwmfTranslatorTests.Defs();
        var warnings = new List<string>();
        var text = UwmfWriter.Write(map, defs, warnings.Add);
        TestContext.Out.WriteLine(text);
        var uwmf = UwmfReader.Read(text);
        var back = UwmfTranslator.ToMapAsset(uwmf, defs, warnings.Add);
        Assert.That(warnings, Is.Empty);
        return (uwmf, back);
    }

    [Test]
    public void A_Level_Using_Only_What_Ecwolf_Has_Is_Written_In_Its_Namespace_With_Its_Names()
    {
        // Arrange: a door, the elevator, an SS, the player, a pushwall
        var map = Room();
        Set(map, 0, 3, 0, 90);
        Set(map, 0, 5, 2, 21);
        Set(map, 0, 4, 2, Area + 1);        // not the secret-exit code before it
        Set(map, 1, 2, 2, 126);
        Set(map, 1, 1, 1, 19);
        Set(map, 1, 0, 2, 98);

        // Act
        var (uwmf, back) = RoundTrip(map);

        // Assert
        Assert.That(uwmf.Namespace, Is.EqualTo("Wolf3D"));
        Assert.That(uwmf.Things.Select(t => t.String("type")), Is.EquivalentTo(new[] { "$Player1Start", "WolfensteinSS" }));
        Assert.That(uwmf.Triggers.Select(t => t.String("action")), Is.EquivalentTo(new[] { "Door_Open", "Exit_Normal", "Pushwall_Move" }));
        for (int p = 0; p < MapConstants.LevelPlanes; p++)
            Assert.That(back.MapData[p], Is.EqualTo(map.MapData[p]), $"plane {p}");
    }

    [Test]
    public void Pfwolfs_Own_Data_Makes_A_Pfwolf_Level_That_Reads_Back_The_Same()
    {
        // Arrange: a tall patched diagonal wall, a light zone with an arch, flats, a tag, an
        // ambush guard, a red team start and a value the mapdefs don't know
        var map = Room();
        Set(map, 0, 2, 0, Wall | 7 << MapConstants.PatchShift);
        Set(map, MapConstants.HeightPlane, 2, 0, 3);
        Set(map, 1, 2, 0, 400);
        Set(map, MapConstants.ZonePlane, 2, 2, 5);
        Set(map, MapConstants.HeightPlane, 3, 2, 2);
        Set(map, MapConstants.FlatPlane, 1, 3, 1 | 1 << 8);
        Set(map, MapConstants.TagPlane, 4, 3, 12);
        Set(map, 1, 1, 2, 108);
        Set(map, 0, 1, 2, Ambush);
        Set(map, 1, 4, 1, 0x7e05);
        map.Defaults = new MapLevelDefaults { Sky = "SKY1", WallStories = 2 };
        var defs = UwmfTranslatorTests.Defs();
        defs.PlayerStarts[430] = new MapPlayerStartTranslation { Angles = 90, Team = "red" };
        Set(map, 1, 3, 3, 430);

        // Act
        var (uwmf, back) = RoundTrip(map, defs);

        // Assert
        Assert.That(uwmf.Namespace, Is.EqualTo("PFWolf"));
        for (int p = 0; p < MapConstants.LevelPlanes; p++)
            Assert.That(back.MapData[p], Is.EqualTo(map.MapData[p]), $"plane {p}");
        Assert.That(back.Defaults, Is.EqualTo(map.Defaults));
    }

    [Test]
    public void A_Levels_Own_Walls_Doors_And_Extra_Things_Read_Back_As_Its_Own()
    {
        // Arrange: a wall and a switch of the level's own, and two things the plane can't hold
        var map = Room();
        var local = new MapObjectTranslationAsset();
        local.Walls[250] = new MapTextureTranslation { North = "BRICK1", East = "BRICK2", Flip = ["south"] };
        local.Walls[251] = new MapTextureTranslation
        {
            North = "LEVER1", East = "LEVER1",
            Switch = new MapSwitchTranslation { To = 250, Sides = ["north"], Sound = "switches/on", Actions = ["A_OpenDoor()", "A_SetZoneLight(2, 40)"] },
        };
        map.LocalDefs = local;
        Set(map, 0, 1, 0, 250);
        Set(map, 0, 2, 0, 251);
        map.ExtraThings = [new MapThing("Guard", 2.25, 2.75, 180, Skills: 0b0011), new MapThing("Barrel", 3.5, 2.5, 0)];

        // Act
        var (_, back) = RoundTrip(map);

        // Assert: by what they are, not by code
        var walls = back.LocalDefs!.Walls;
        var brick = walls[back.MapData[0][1]];
        var lever = walls[back.MapData[0][2]];
        Assert.That((brick.WallFace("south"), brick.Flip), Is.EqualTo(("BRICK1", new List<string> { "south" })));
        Assert.That(lever.Switch!.To, Is.EqualTo(back.MapData[0][1]));
        Assert.That(lever.Switch.Actions, Is.EqualTo(new[] { "A_OpenDoor()", "A_SetZoneLight(2, 40)" }));
        Assert.That((lever.Switch.Sound, lever.Switch.Sides.Single()), Is.EqualTo(("switches/on", "north")));
        Assert.That(back.ExtraThings, Is.EqualTo(map.ExtraThings));
    }
}
