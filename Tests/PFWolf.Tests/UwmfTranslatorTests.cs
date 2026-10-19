using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Enums;
using PFWolf.Loaders.Uwmf;

namespace PFWolf.Tests;

public class UwmfTranslatorTests
{
    // A Wolf3D-like mapdefs: a stone wall, the elevator switch, plain and gold doors, areas from
    // 107 (also the secret exit) with the ambush code 106, guards, SS, a cross, a patrol point,
    // the player starts, the pushwall and victory triggers, a flat and a diagonal marker
    internal static MapObjectTranslationAsset Defs()
    {
        var defs = new MapObjectTranslationAsset();
        defs.Walls[1] = new MapTextureTranslation { North = "GSTONEA1", South = "GSTONEA1", East = "GSTONEA2", West = "GSTONEA2" };
        defs.Walls[21] = new MapTextureTranslation
        {
            North = "ELEV1_1", South = "ELEV1_1", East = "ELEV1_2", West = "ELEV1_2",
            Switch = new MapSwitchTranslation { To = 22, Sides = ["east", "west"], Actions = ["A_Exit"] },
        };
        defs.Walls[22] = new MapTextureTranslation { North = "ELEV2_1", East = "ELEV2_2" };
        defs.Doors[90] = new MapTextureTranslation
            { Vertical = true, East = "DOOR1_2", West = "DOOR1_2", North = "SLOT1_1", South = "SLOT1_1", OpenSound = "doors/open" };
        defs.Doors[92] = new MapTextureTranslation
            { Vertical = true, East = "DOOR3_2", West = "DOOR3_2", North = "SLOT1_1", South = "SLOT1_1", Lock = "GoldKey" };
        defs.Floors.AreaStart = 107;
        defs.Floors.AreaCount = 37;
        defs.Floors.Ambush = 106;
        defs.Floors.SecretExit = 107;
        defs.Things[108] = new MapActorTranslation { Class = "Guard", Angles = 0 };
        defs.Things[109] = new MapActorTranslation { Class = "Guard", Angles = 90 };
        defs.Things[112] = new MapActorTranslation { Class = "Guard", Angles = 0, Patrol = 1 };
        defs.Things[144] = new MapActorTranslation { Class = "Guard", Angles = 0, MinSkill = 2 };
        defs.Things[126] = new MapActorTranslation { Class = "SS", Angles = 0 };
        defs.Things[52] = new MapActorTranslation { Class = "Cross" };
        defs.Things[90] = new MapActorTranslation { Class = "PatrolPoint", Angles = 0 };
        defs.PlayerStarts[19] = new MapPlayerStartTranslation { Angles = 90 };
        defs.PlayerStarts[20] = new MapPlayerStartTranslation { Angles = 0 };
        defs.PlayerStarts[410] = new MapPlayerStartTranslation { Angles = 90, Deathmatch = true };
        defs.Triggers[98] = new MapTriggerTranslation { Action = "A_PushWall(\"world/pushwall\")", Secret = true };
        defs.Triggers[99] = new MapTriggerTranslation { Action = "A_VictoryTile(\"BJVictory\")", Activation = "walk" };
        defs.Flats.Floor[1] = "GSTONEA1";
        defs.Diagonals[400] = new MapDiagonalTranslation { Shape = WallShape.SolidNW };
        defs.Uwmf.Classes["WolfensteinSS"] = "SS";
        defs.Uwmf.Locks[1] = "GoldKey";
        return defs;
    }

    private const string Stone = """tile { texturenorth = "GSTONEA1"; texturesouth = "GSTONEA1"; textureeast = "GSTONEA2"; texturewest = "GSTONEA2"; }""";

    /// <summary>
    /// A level from rows of characters: '#' tile 0 (stone), '.' open floor in zone 0, ',' zone 1,
    /// and whatever <paramref name="spots"/> says for the others ({tile, sector, zone[, tag]})
    /// </summary>
    private static string Level(string[] rows, string blocks = "", Dictionary<char, string>? spots = null, string space = "Wolf3D", string tiles = Stone)
    {
        var entries = rows.SelectMany(row => row.Select(c => c switch
        {
            '#' => "{0, 0, -1}",
            '.' => "{-1, 0, 0}",
            ',' => "{-1, 0, 1}",
            _ => spots![c],
        }));
        return $$"""
            namespace = "{{space}}";
            name = "Test";
            width = {{rows[0].Length}};
            height = {{rows.Length}};
            {{tiles}}
            sector { texturefloor = "#707070"; textureceiling = "#383838"; }
            zone { }
            zone { }
            plane { depth = 64; }
            planemap { {{string.Join(", ", entries)}} }
            {{blocks}}
            """;
    }

    private static (MapAsset Map, List<string> Warnings) Translate(string textmap, MapObjectTranslationAsset? defs = null)
    {
        var warnings = new List<string>();
        var map = UwmfTranslator.ToMapAsset(UwmfReader.Read(textmap), defs ?? Defs(), warnings.Add);
        TestContext.Out.WriteLine(string.Join(Environment.NewLine, warnings));
        return (map, warnings);
    }

    private static int At(MapAsset map, int plane, int x, int y) => map.MapData[plane][y * map.Width + x];

    private static readonly string[] Room =
    [
        "#####",
        "#...#",
        "#.,.#",
        "#####",
    ];

    [Test]
    public void Walls_Zones_And_Things_Become_The_Mapdefs_Codes()
    {
        // Act
        var (map, warnings) = Translate(Level(Room, """
            thing { x = 1.5; y = 1.5; type = "Guard"; angle = 90; skill1 = true; skill2 = true; skill3 = true; skill4 = true; }
            thing { x = 3.5; y = 1.5; type = "$Player1Start"; angle = 0; skill1 = true; skill2 = true; skill3 = true; skill4 = true; }
            thing { x = 2.5; y = 2.5; type = "Cross"; angle = 270; skill1 = true; skill2 = true; skill3 = true; skill4 = true; }
            thing { x = 3.5; y = 2.5; type = "WolfensteinSS"; angle = 0; skill1 = true; skill2 = true; skill3 = true; skill4 = true; }
            """));

        // Assert
        Assert.That((map.Width, map.Height, map.Format), Is.EqualTo(((ushort)5, (ushort)4, MapFormat.Uwmf)));
        Assert.That(At(map, 0, 0, 0), Is.EqualTo(1));
        Assert.That(At(map, 0, 1, 1), Is.EqualTo(107));
        Assert.That(At(map, 0, 2, 2), Is.EqualTo(108), "zone 1");
        Assert.That(At(map, 1, 1, 1), Is.EqualTo(109));
        Assert.That(At(map, 1, 3, 1), Is.EqualTo(20));
        Assert.That(At(map, 1, 2, 2), Is.EqualTo(52), "a cross doesn't care which way it faces");
        Assert.That(At(map, 1, 3, 2), Is.EqualTo(126), "WolfensteinSS is SS here");
        Assert.That(At(map, MapConstants.FlatPlane, 1, 1), Is.Zero, "colors: the level's floor and ceiling colors");
        Assert.That(map.LocalDefs, Is.Null);
        Assert.That(map.ExtraThings, Is.Empty);
        Assert.That(warnings, Is.Empty);
    }

    [Test]
    public void Doors_Are_Tiles_With_A_Door_Open_Trigger_And_A_Lock_Picks_The_Locked_Door()
    {
        // Arrange: tile 1 a plain door, tile 2 the gold one, tile 3 a door the mapdefs don't have
        var tiles = Stone + """

            tile { texturenorth = "SLOT1_1"; texturesouth = "SLOT1_1"; textureeast = "DOOR1_2"; texturewest = "DOOR1_2"; offsetvertical = true; }
            tile { texturenorth = "SLOT1_1"; texturesouth = "SLOT1_1"; textureeast = "DOOR3_2"; texturewest = "DOOR3_2"; offsetvertical = true; }
            tile { texturenorth = "NEWDOOR"; texturesouth = "NEWDOOR"; textureeast = "SLOT1_2"; texturewest = "SLOT1_2"; offsethorizontal = true; }
            """;
        var rows = new[] { "#####", "#.a.#", "#.b.#", "#.c.#", "#####" };
        var spots = new Dictionary<char, string> { ['a'] = "{1, 0, -1}", ['b'] = "{2, 0, -1}", ['c'] = "{3, 0, -1}" };

        // Act
        var (map, _) = Translate(Level(rows, """
            trigger { x = 2; y = 1; action = "Door_Open"; arg1 = 16; arg2 = 300; playeruse = true; activatenorth = false; activatesouth = false; }
            trigger { x = 2; y = 2; action = "Door_Open"; arg1 = 16; arg2 = 300; arg3 = 1; playeruse = true; activatenorth = false; activatesouth = false; }
            trigger { x = 2; y = 3; action = "Door_Open"; arg4 = 1; playeruse = true; }
            """, spots, tiles: tiles));

        // Assert
        Assert.That(At(map, 0, 2, 1), Is.EqualTo(90));
        Assert.That(At(map, 0, 2, 2), Is.EqualTo(92));
        int own = At(map, 0, 2, 3);
        Assert.That(own, Is.EqualTo(255), "the highest free plane 0 code");
        var door = map.LocalDefs!.Doors[own];
        Assert.That((door.North, door.East, door.Vertical, door.OpenSound), Is.EqualTo(("NEWDOOR", "SLOT1_2", false, "doors/open")));
    }

    [Test]
    public void A_Wall_The_Mapdefs_Have_No_Textures_For_Is_The_Levels_Own()
    {
        // Arrange
        var tiles = Stone + """

            tile { texturenorth = "BRICK1"; textureeast = "BRICK2"; }
            """;

        // Act
        var (map, _) = Translate(Level(["###", "#x#", "###"], spots: new() { ['x'] = "{1, 0, -1}" }, tiles: tiles));

        // Assert
        Assert.That(At(map, 0, 1, 1), Is.EqualTo(255));
        Assert.That(map.LocalDefs!.Walls[255].WallFace("south"), Is.EqualTo("BRICK1"));
        Assert.That(At(map, 0, 0, 0), Is.EqualTo(1));
    }

    [Test]
    public void An_Elevator_Switch_Is_The_Mapdefs_Exit_Wall_And_Its_Secret_Side_Marks_The_Floor()
    {
        // Arrange: the elevator at 2,1; Exit_Normal used from the west, Exit_Secret from the east
        var tiles = Stone + """

            tile { texturenorth = "ELEV1_1"; texturesouth = "ELEV1_1"; textureeast = "ELEV1_2"; texturewest = "ELEV1_2"; }
            """;
        var rows = new[] { "#####", "#.e,#", "#####" };

        // Act
        var (map, _) = Translate(Level(rows, """
            trigger { x = 2; y = 1; action = "Exit_Normal"; playeruse = true; activatenorth = false; activatesouth = false; activateeast = false; }
            trigger { x = 2; y = 1; action = "Exit_Secret"; playeruse = true; activatenorth = false; activatesouth = false; activatewest = false; }
            """, new() { ['e'] = "{1, 0, -1}" }, tiles: tiles));

        // Assert
        Assert.That(At(map, 0, 2, 1), Is.EqualTo(21));
        Assert.That(At(map, 0, 3, 1), Is.EqualTo(107), "the secret-exit code east of it");
        Assert.That(At(map, 1, 2, 1), Is.Zero);
    }

    [Test]
    public void Pushwalls_And_The_Victory_Tile_Become_The_Mapdefs_Triggers()
    {
        // Act
        var (map, _) = Translate(Level(Room, """
            trigger { x = 0; y = 1; action = "Pushwall_Move"; arg1 = 8; arg2 = 2; arg3 = 2; playeruse = true; secret = true; }
            trigger { x = 2; y = 1; action = "Exit_VictorySpin"; playercross = true; }
            """));

        // Assert
        Assert.That(At(map, 1, 0, 1), Is.EqualTo(98));
        Assert.That(At(map, 1, 2, 1), Is.EqualTo(99));
    }

    [Test]
    public void Things_The_Plane_Cant_Hold_Are_Placed_By_Class()
    {
        // Act: a second thing on a tile, one off its tile's middle, one on skills 1 and 3 only,
        // and a class the mapdefs have no code for
        var (map, _) = Translate(Level(Room, """
            thing { x = 1.5; y = 1.5; type = "Cross"; skill1 = true; skill2 = true; skill3 = true; skill4 = true; }
            thing { x = 1.5; y = 1.5; type = "Guard"; skill1 = true; skill2 = true; skill3 = true; skill4 = true; }
            thing { x = 2.25; y = 1.75; type = "Cross"; skill1 = true; skill2 = true; skill3 = true; skill4 = true; }
            thing { x = 3.5; y = 1.5; type = "Guard"; skill1 = true; skill3 = true; ambush = true; }
            thing { x = 1.5; y = 2.5; type = "Barrel"; skill1 = true; skill2 = true; skill3 = true; skill4 = true; }
            thing { x = 2.5; y = 2.5; type = "Guard"; skill3 = true; skill4 = true; }
            """));

        // Assert
        Assert.That(At(map, 1, 1, 1), Is.EqualTo(52));
        Assert.That(At(map, 1, 2, 2), Is.EqualTo(144), "skills 3 and 4: min-skill 2");
        Assert.That(map.ExtraThings, Is.EqualTo(new[]
        {
            new MapThing("Guard", 1.5, 1.5, 0),
            new MapThing("Cross", 2.25, 1.75, 0),
            new MapThing("Guard", 3.5, 1.5, 0, Ambush: true, Skills: 0b0101),
            new MapThing("Barrel", 1.5, 2.5, 0),
        }));
    }

    [Test]
    public void An_Ambush_Thing_Stands_On_The_Ambush_Code()
    {
        var (map, _) = Translate(Level(Room, """
            thing { x = 1.5; y = 1.5; type = "Guard"; ambush = true; skill1 = true; skill2 = true; skill3 = true; skill4 = true; }
            """));

        Assert.That(At(map, 1, 1, 1), Is.EqualTo(108));
        Assert.That(At(map, 0, 1, 1), Is.EqualTo(106));
    }

    [Test]
    public void A_Pfwolf_Level_Uses_Pfwolfs_Class_Names()
    {
        var (map, _) = Translate(Level(Room, """
            thing { x = 1.5; y = 1.5; type = "SS"; skill1 = true; skill2 = true; skill3 = true; skill4 = true; }
            thing { x = 2.5; y = 1.5; type = "WolfensteinSS"; skill1 = true; skill2 = true; skill3 = true; skill4 = true; }
            """, space: "PFWolf"));

        Assert.That(At(map, 1, 1, 1), Is.EqualTo(126));
        Assert.That(map.ExtraThings.Single().Class, Is.EqualTo("WolfensteinSS"), "not an alias in PFWolf's namespace");
    }

    [Test]
    public void More_Zones_Than_Area_Codes_Count_On_From_The_Last()
    {
        // Arrange: 40 zones, three more than the mapdefs' 37
        var zones = string.Concat(Enumerable.Repeat("zone { }\n", 38));
        var rows = new[] { "####", "#zy#", "####" };

        // Act
        var (map, _) = Translate(Level(rows, zones, new() { ['z'] = "{-1, 0, 39}", ['y'] = "{-1, 0, 36}" }));

        // Assert
        Assert.That(At(map, 0, 1, 1), Is.EqualTo(107 + 39));
        Assert.That(At(map, 0, 2, 1), Is.EqualTo(107 + 36));
        Assert.That(map.LocalDefs!.Floors.AreaCount, Is.EqualTo(40));
    }

    [Test]
    public void Pfwolfs_Own_Keys_Give_Heights_Patches_Diagonals_Light_Zones_And_Arches()
    {
        // Arrange
        var tiles = Stone + """

            tile { texturenorth = "GSTONEA1"; texturesouth = "GSTONEA1"; textureeast = "GSTONEA2"; texturewest = "GSTONEA2"; height = 3; patch = 5; diagonal = "nw"; }
            sector { texturefloor = "GSTONEA1"; textureceiling = "NEWCEIL"; lightzone = 4; height = 2; }
            """;
        var rows = new[] { "###", "#t#", "#s#", "###" };

        // Act
        var (map, _) = Translate(Level(rows, "", new() { ['t'] = "{1, 0, -1}", ['s'] = "{-1, 0, 0, 9}" }, space: "PFWolf", tiles: tiles));

        // Assert
        Assert.That(At(map, 0, 1, 1), Is.EqualTo(1 | 5 << 8));
        Assert.That(At(map, MapConstants.HeightPlane, 1, 1), Is.EqualTo(3));
        Assert.That(At(map, 1, 1, 1), Is.EqualTo(400));
        Assert.That(At(map, MapConstants.FlatPlane, 1, 2), Is.EqualTo(1 | 255 << 8));
        Assert.That(map.LocalDefs!.Flats.Ceiling[255], Is.EqualTo("NEWCEIL"));
        Assert.That(At(map, MapConstants.ZonePlane, 1, 2), Is.EqualTo(4));
        Assert.That(At(map, MapConstants.HeightPlane, 1, 2), Is.EqualTo(2), "an arch over open floor");
        Assert.That(At(map, MapConstants.TagPlane, 1, 2), Is.EqualTo(9));
    }

    [Test]
    public void Ids_Name_The_Mapdefs_Codes_Exactly()
    {
        // Arrange: the elevator textures but id 22's; zone id 106; a guard by code 112
        var tiles = """
            tile { texturenorth = "ELEV1_1"; texturesouth = "ELEV1_1"; textureeast = "ELEV1_2"; texturewest = "ELEV1_2"; id = 22; }
            """;
        var rows = new[] { "###", "#.#", "###" };

        // Act
        var (map, _) = Translate(Level(rows, """
            zone { id = 106; }
            thing { x = 1.5; y = 1.5; type = "Guard"; patrol = true; id = 112; skill1 = true; skill2 = true; skill3 = true; skill4 = true; }
            """, spots: new() { ['.'] = "{-1, 0, 2}" }, tiles: tiles).Replace("{-1, 0, 0}", "{-1, 0, 2}"));

        // Assert
        Assert.That(At(map, 0, 0, 0), Is.EqualTo(22));
        Assert.That(At(map, 0, 1, 1), Is.EqualTo(106));
        Assert.That(At(map, 1, 1, 1), Is.EqualTo(112));
    }

    [Test]
    public void A_Pfwolf_Action_Is_A_Trigger_Of_The_Levels_Own()
    {
        var (map, _) = Translate(Level(Room, """
            trigger { x = 1; y = 1; action = "A_SetZoneLight(3, 64)"; playercross = true; repeatable = true; }
            """, space: "PFWolf"));

        int code = At(map, 1, 1, 1);
        Assert.That(map.LocalDefs!.Triggers[code], Is.EqualTo(new MapTriggerTranslation
            { Action = "A_SetZoneLight(3, 64)", Activation = "walk", Repeat = true }));
    }

    [Test]
    public void The_Planes_Depth_And_The_Sky_Are_The_Levels_Defaults()
    {
        var (map, _) = Translate(Level(Room).Replace("depth = 64", "depth = 192").Replace("name = \"Test\";", "name = \"Test\"; sky = \"SKY1\";"));

        Assert.That(map.Defaults, Is.EqualTo(new MapLevelDefaults { Sky = "SKY1", WallStories = 3 }));
    }

    [Test]
    public void What_Pfwolf_Doesnt_Play_Is_Left_Out_With_A_Warning()
    {
        var (_, warnings) = Translate(Level(Room, """
            trigger { x = 1; y = 1; action = "Teleport_NewMap"; arg0 = 2; playercross = true; }
            thing { x = 2.5; y = 1.5; type = "$Player2Start"; skill1 = true; skill2 = true; skill3 = true; skill4 = true; }
            """).Replace("tilesize = 64;", ""));

        Assert.That(warnings, Has.Some.Contains("Teleport_NewMap"));
        Assert.That(warnings, Has.Some.Contains("$Player2Start"));
    }

    [Test]
    public void A_Level_With_No_Planemap_Is_Refused()
    {
        Assert.That(() => Translate("namespace = \"Wolf3D\"; width = 4; height = 4;"),
            Throws.InstanceOf<InvalidDataException>().With.Message.Contains("planemap"));
    }

    [Test]
    public void A_Textmap_Wad_Loads_As_A_Uwmf_Level_To_Translate()
    {
        // Arrange
        var textmap = System.Text.Encoding.UTF8.GetBytes(Level(Room));
        var wad = Loaders.WadFile.Write([new Loaders.WadFile.Lump("MAP01", []), new Loaders.WadFile.Lump("TEXTMAP", textmap), new Loaders.WadFile.Lump("ENDMAP", [])]);

        // Act
        var map = Loaders.EcWolfMapLoader.Load(wad);

        // Assert
        Assert.That(map.NeedsTranslation, Is.True);
        Assert.That((map.Width, map.Height, map.Format), Is.EqualTo(((ushort)5, (ushort)4, MapFormat.Uwmf)));
    }
}
