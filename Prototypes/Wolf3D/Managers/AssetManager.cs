using System.Numerics;
using Wolf3D.Entities;
using Wolf3D.Entities.Actors;
using Wolf3D.Mappers;

namespace Wolf3D.Managers;

internal class AssetManager
{
    [Obsolete("Temporary endpoint until the asset types are implemented")]
    public GameInfoMetadata GetGameInfo()
    {
        // Wolf3d's
        return new()
        {
            DefaultMap = new()
            {
                CeilingColor = "#383838",
                FloorColor = "#717171"
            },
            Skills = new()
            {
                {
                    "BABY", new()
                    {
                        Name = "Can I play, Daddy?",
                        PicName = "C_BABYMODE",
                        SpawnFilter = [1]
                    }
                },
                {
                    "EASY", new()
                    {
                        Name = "Don't hurt me.",
                        PicName = "C_EASY",
                        SpawnFilter = [1, 2]
                    }
                },
                {
                    "MEDIUM", new()
                    {
                        Name = "Bring 'em on!",
                        PicName = "C_NORMAL",
                        SpawnFilter = [1, 2, 3]
                    }
                },
                {
                    "HARD", new()
                    {
                        Name = "I am Death incarnate!",
                        PicName = "C_HARD",
                        SpawnFilter = [1, 2, 3, 4]
                    }
                },
            },
            Episodes = new()
            {
                {
                    "EP01", new()
                    {
                        Name = "Episode 1\nEscape from Wolfenstein",
                        StartMap = "MAP01",
                        PicName = "C_EPISODE1",
                        Key = 'E'
                    }
                },
                {
                    "EP02", new()
                    {
                        Name = "Episode 2\nOperation: Eisenfaust",
                        StartMap = "MAP11",
                        PicName = "C_EPISODE2",
                        Key = 'O'
                    }
                },
                {
                    "EP03", new()
                    {
                        Name = "Episode 3\nDie, Fuhrer, Die!",
                        StartMap = "MAP11",
                        PicName = "C_EPISODE3",
                        Key = 'D'
                    }
                },
                {
                    "EP04", new()
                    {
                        Name = "Episode 4\nA Dark Secret",
                        StartMap = "MAP31",
                        PicName = "C_EPISODE4",
                        Key = 'A'
                    }
                },
                {
                    "EP05", new()
                    {
                        Name = "Episode 5\nTrail of the Madman",
                        StartMap = "MAP41",
                        PicName = "C_EPISODE5",
                        Key = 'T'
                    }
                },
                {
                    "EP06", new()
                    {
                        Name = "Episode 6\nConfrontation",
                        StartMap = "MAP51",
                        PicName = "C_EPISODE6",
                        Key = 'C'
                    }
                },
            },
            Clusters = new()
            {
                {
                    1,
                    new()
                    {
                        EndText = "ENDART1"
                    }
                },
                {
                    2,
                    new()
                    {
                        EndText = "ENDART2"
                    }
                },
                {
                    3,
                    new()
                    {
                        EndText = "ENDART3"
                    }
                },
                {
                    4,
                    new()
                    {
                        EndText = "ENDART4"
                    }
                },
                {
                    5,
                    new()
                    {
                        EndText = "ENDART5"
                    }
                },
                {
                    6,
                    new()
                    {
                        EndText = "ENDART6"
                    }
                }
            },
            Maps = new()
            {
                // Episode 1
                {
                    "MAP01",
                    new()
                    {
                        Next = "MAP02",
                        SecretNext = "MAP10",
                        FloorNumber = 1,
                        ParTime = 90,
                        Music = "GetThem",
                        Cluster = 1
                    }
                },
                {
                    "MAP02",
                    new()
                    {
                        Next = "MAP03",
                        FloorNumber = 2,
                        ParTime = 120,
                        Music = "Searching",
                        Cluster = 1
                    }
                },
                {
                    "MAP03",
                    new()
                    {
                        Next = "MAP04",
                        FloorNumber = 3,
                        ParTime = 120,
                        Music = "PrisonerOfWar",
                        Cluster = 1
                    }
                },
                {
                    "MAP04",
                    new()
                    {
                        Next = "MAP05",
                        FloorNumber = 4,
                        ParTime = 210,
                        Music = "Suspense",
                        Cluster = 1
                    }
                },
                {
                    "MAP05",
                    new()
                    {
                        Next = "MAP06",
                        FloorNumber = 5,
                        ParTime = 180,
                        Music = "GetThem",
                        Cluster = 1
                    }
                },
                {
                    "MAP06",
                    new()
                    {
                        Next = "MAP07",
                        FloorNumber = 6,
                        ParTime = 180,
                        Music = "Searching",
                        Cluster = 1
                    }
                },
                {
                    "MAP07",
                    new()
                    {
                        Next = "MAP08",
                        FloorNumber = 7,
                        ParTime = 150,
                        Music = "PrisonerOfWar",
                        Cluster = 1
                    }
                },
                {
                    "MAP08",
                    new()
                    {
                        Next = "MAP09",
                        FloorNumber = 8,
                        ParTime = 150,
                        Music = "Suspense",
                        Cluster = 1
                    }
                },
                {
                    "MAP09",
                    new()
                    {
                        FloorNumber = 9,
                        Music = "MarchToWar",
                        Cluster = 1
                    }
                },
                {
                    "MAP10",
                    new()
                    {
                        Next = "MAP02",
                        FloorNumber = 10,
                        Music = "Corner",
                        Cluster = 1,
                        CeilingColor = "#400040"
                    }
                },

                // Episode 2
        
                {
                    "MAP11",
                    new()
                    {
                        Next = "MAP12",
                        SecretNext = "MAP20",
                        CeilingColor = "#585400",
                        FloorNumber = 1,
                        ParTime = 90,
                        Music = "NaziAnthem",
                        Cluster = 2
                    }
                },
                {
                    "MAP12",
                    new()
                    {
                        Next = "MAP13",
                        FloorNumber = 2,
                        CeilingColor = "#585400",
                        ParTime = 210,
                        Music = "Lurking",
                        Cluster = 2
                    }
                },
                {
                    "MAP13",
                    new()
                    {
                        Next = "MAP14",
                        FloorNumber = 3,
                        ParTime = 180,
                        CeilingColor = "#585400",
                        Music = "GoingAfterHitler",
                        Cluster = 2
                    }
                },
                {
                    "MAP14",
                    new()
                    {
                        Next = "MAP15",
                        FloorNumber = 4,
                        ParTime = 120,
                        Music = "PoundingHeadache",
                        Cluster = 2
                    }
                },
                {
                    "MAP15",
                    new()
                    {
                        Next = "MAP16",
                        FloorNumber = 5,
                        ParTime = 240,
                        CeilingColor = "#4040FC",
                        Music = "NaziAnthem",
                        Cluster = 2
                    }
                },
                {
                    "MAP16",
                    new()
                    {
                        Next = "MAP17",
                        FloorNumber = 6,
                        ParTime = 360,
                        CeilingColor = "#585400",
                        Music = "Lurking",
                        Cluster = 2
                    }
                },
                {
                    "MAP17",
                    new()
                    {
                        Next = "MAP18",
                        FloorNumber = 7,
                        ParTime = 60,
                        Music = "PoundingHeadache",
                        Cluster = 2
                    }
                },
                {
                    "MAP18",
                    new()
                    {
                        Next = "MAP19",
                        FloorNumber = 8,
                        ParTime = 180,
                        CeilingColor = "#580000",
                        Music = "GoingAfterHitler",
                        Cluster = 2
                    }
                },
                {
                    "MAP19",
                    new()
                    {
                        FloorNumber = 9,
                        Music = "MarchToWar",
                        Cluster = 2
                    }
                },
                {
                    "MAP20",
                    new()
                    {
                        Next = "MAP12",
                        FloorNumber = 10,
                        CeilingColor = "#4040FC",
                        Music = "Dungeon",
                        Cluster = 2
                    }
                },

                // Episode 3
        
                {
                    "MAP21",
                    new()
                    {
                        Next = "MAP22",
                        FloorNumber = 1,
                        ParTime = 90,
                        Music = "KillTheSOB",
                        Cluster = 3
                    }
                },
                {
                    "MAP22",
                    new()
                    {
                        Next = "MAP23",
                        FloorNumber = 2,
                        ParTime = 90,
                        Music = "NaziRap",
                        Cluster = 3
                    }
                },
                {
                    "MAP23",
                    new()
                    {
                        Next = "MAP24",
                        FloorNumber = 3,
                        ParTime = 150,
                        Music = "TheTwelfthHour",
                        Cluster = 3
                    }
                },
                {
                    "MAP24",
                    new()
                    {
                        Next = "MAP25",
                        FloorNumber = 4,
                        ParTime = 150,
                        Music = "ZeroHour",
                        Cluster = 3
                    }
                },
                {
                    "MAP25",
                    new()
                    {
                        Next = "MAP26",
                        FloorNumber = 5,
                        ParTime = 210,
                        Music = "KillTheSOB",
                        Cluster = 3
                    }
                },
                {
                    "MAP26",
                    new()
                    {
                        Next = "MAP27",
                        FloorNumber = 6,
                        ParTime = 150,
                        CeilingColor = "#580000",
                        Music = "NaziRap",
                        Cluster = 3
                    }
                },
                {
                    "MAP27",
                    new()
                    {
                        Next = "MAP28",
                        SecretNext = "MAP30",
                        FloorNumber = 7,
                        CeilingColor = "#483818",
                        ParTime = 120,
                        Music = "TheTwelfthHour",
                        Cluster = 3
                    }
                },
                {
                    "MAP28",
                    new()
                    {
                        Next = "MAP29",
                        FloorNumber = 8,
                        ParTime = 360,
                        Music = "ZeroHour",
                        Cluster = 3
                    }
                },
                {
                    "MAP29",
                    new()
                    {
                        FloorNumber = 9,
                        Music = "Ultimate",
                        Cluster = 3
                    }
                },
                {
                    "MAP30",
                    new()
                    {
                        Next = "MAP28",
                        FloorNumber = 10,
                        CeilingColor = "#000098",
                        Music = "PacMan",
                        Cluster = 3
                    }
                },

                // Episode 4
        
                {
                    "MAP31",
                    new()
                    {
                        Next = "MAP32",
                        FloorNumber = 1,
                        ParTime = 120,
                        Music = "GetThem",
                        Cluster = 4
                    }
                },
                {
                    "MAP32",
                    new()
                    {
                        Next = "MAP33",
                        FloorNumber = 2,
                        CeilingColor = "#000058",
                        ParTime = 120,
                        Music = "Searching",
                        Cluster = 4
                    }
                },
                {
                    "MAP33",
                    new()
                    {
                        Next = "MAP34",
                        SecretNext = "MAP40",
                        FloorNumber = 3,
                        CeilingColor = "#580000",
                        ParTime = 90,
                        Music = "PrisonerOfWar",
                        Cluster = 4
                    }
                },
                {
                    "MAP34",
                    new()
                    {
                        Next = "MAP35",
                        FloorNumber = 4,
                        CeilingColor = "#483818",
                        ParTime = 60,
                        Music = "Suspense",
                        Cluster = 4
                    }
                },
                {
                    "MAP35",
                    new()
                    {
                        Next = "MAP36",
                        FloorNumber = 5,
                        CeilingColor = "#483818",
                        ParTime = 270,
                        Music = "GetThem",
                        Cluster = 4
                    }
                },
                {
                    "MAP36",
                    new()
                    {
                        Next = "MAP37",
                        FloorNumber = 6,
                        ParTime = 210,
                        CeilingColor = "#000058",
                        Music = "Searching",
                        Cluster = 4
                    }
                },
                {
                    "MAP37",
                    new()
                    {
                        Next = "MAP38",
                        FloorNumber = 7,
                        CeilingColor = "#580000",
                        ParTime = 120,
                        Music = "PrisonerOfWar",
                        Cluster = 4
                    }
                },
                {
                    "MAP38",
                    new()
                    {
                        Next = "MAP39",
                        FloorNumber = 8,
                        CeilingColor = "#716D00",
                        ParTime = 270,
                        Music = "Suspense",
                        Cluster = 4
                    }
                },
                {
                    "MAP39",
                    new()
                    {
                        FloorNumber = 9,
                        Music = "MarchToWar",
                        Cluster = 4
                    }
                },
                {
                    "MAP40",
                    new()
                    {
                        Next = "MAP34",
                        FloorNumber = 10,
                        CeilingColor = "#483818",
                        Music = "Corner",
                        Cluster = 4
                    }
                },

                // Episode 5
        
                {
                    "MAP41",
                    new()
                    {
                        Next = "MAP42",
                        FloorNumber = 1,
                        CeilingColor = "#007070",
                        ParTime = 150,
                        Music = "NaziAnthem",
                        Cluster = 5
                    }
                },
                {
                    "MAP42",
                    new()
                    {
                        Next = "MAP43",
                        FloorNumber = 2,
                        ParTime = 90,
                        Music = "Lurking",
                        Cluster = 5
                    }
                },
                {
                    "MAP43",
                    new()
                    {
                        Next = "MAP44",
                        FloorNumber = 3,
                        CeilingColor = "#580000",
                        ParTime = 150,
                        Music = "GoingAfterHitler",
                        Cluster = 5
                    }
                },
                {
                    "MAP44",
                    new()
                    {
                        Next = "MAP45",
                        FloorNumber = 4,
                        CeilingColor = "#580000",
                        ParTime = 150,
                        Music = "PoundingHeadache",
                        Cluster = 5
                    }
                },
                {
                    "MAP45",
                    new()
                    {
                        Next = "MAP46",
                        SecretNext = "MAP40",
                        FloorNumber = 5,
                        CeilingColor = "#483818",
                        ParTime = 240,
                        Music = "NaziAnthem",
                        Cluster = 5
                    }
                },
                {
                    "MAP46",
                    new()
                    {
                        Next = "MAP47",
                        FloorNumber = 6,
                        ParTime = 180,
                        CeilingColor = "#80502C",
                        Music = "Lurking",
                        Cluster = 5
                    }
                },
                {
                    "MAP47",
                    new()
                    {
                        Next = "MAP48",
                        FloorNumber = 7,
                        ParTime = 270,
                        Music = "PoundingHeadache",
                        Cluster = 5
                    }
                },
                {
                    "MAP48",
                    new()
                    {
                        Next = "MAP49",
                        FloorNumber = 8,
                        ParTime = 210,
                        Music = "GoingAfterHitler",
                        Cluster = 5
                    }
                },
                {
                    "MAP49",
                    new()
                    {
                        FloorNumber = 9,
                        Music = "MarchToWar",
                        Cluster = 5
                    }
                },
                {
                    "MAP50",
                    new()
                    {
                        Next = "MAP46",
                        FloorNumber = 10,
                        CeilingColor = "#580000",
                        Music = "Dungeon",
                        Cluster = 5
                    }
                },
        

                // Episode 6
        
                {
                    "MAP51",
                    new()
                    {
                        Next = "MAP52",
                        FloorNumber = 1,
                        ParTime = 390,
                        Music = "KillTheSOB",
                        Cluster = 6
                    }
                },
                {
                    "MAP52",
                    new()
                    {
                        Next = "MAP53",
                        FloorNumber = 2,
                        ParTime = 240,
                        Music = "NaziRap",
                        Cluster = 6
                    }
                },
                {
                    "MAP53",
                    new()
                    {
                        Next = "MAP54",
                        SecretNext = "MAP60",
                        FloorNumber = 3,
                        ParTime = 270,
                        Music = "TheTwelfthHour",
                        Cluster = 6
                    }
                },
                {
                    "MAP54",
                    new()
                    {
                        Next = "MAP55",
                        FloorNumber = 4,
                        ParTime = 360,
                        Music = "ZeroHour",
                        Cluster = 6
                    }
                },
                {
                    "MAP55",
                    new()
                    {
                        Next = "MAP56",
                        FloorNumber = 5,
                        CeilingColor = "#483818",
                        ParTime = 300,
                        Music = "KillTheSOB",
                        Cluster = 6
                    }
                },
                {
                    "MAP56",
                    new()
                    {
                        Next = "MAP57",
                        FloorNumber = 6,
                        ParTime = 330,
                        CeilingColor = "#483818",
                        Music = "NaziRap",
                        Cluster = 6
                    }
                },
                {
                    "MAP57",
                    new()
                    {
                        Next = "MAP58",
                        FloorNumber = 7,
                        CeilingColor = "#007070",
                        ParTime = 330,
                        Music = "TheTwelfthHour",
                        Cluster = 6
                    }
                },
                {
                    "MAP58",
                    new()
                    {
                        Next = "MAP59",
                        CeilingColor = "#483818",
                        FloorNumber = 8,
                        ParTime = 510,
                        Music = "ZeroHour",
                        Cluster = 6
                    }
                },
                {
                    "MAP59",
                    new()
                    {
                        FloorNumber = 9,
                        CeilingColor = "#483818",
                        Music = "Ultimate",
                        Cluster = 6
                    }
                },
                {
                    "MAP60",
                    new()
                    {
                        FloorNumber = 10,
                        CeilingColor = "#483818",
                        Music = "FunkYou",
                        Cluster = 6
                    }
                },
            },
            EndStrings = [
                "$ENDSTR1",
                "$ENDSTR2",
                "$ENDSTR3",
                "$ENDSTR4",
                "$ENDSTR5",
                "$ENDSTR6",
                "$ENDSTR7",
                "$ENDSTR8",
                "$ENDSTR9"
            ]
        };
    }

    [Obsolete("Temporary endpoint until the asset types are implemented")]
    public MenuMetadata? GetMenu(string name)
    {
        var normalizedName = name.ToLowerInvariant();
        if (normalizedName.Equals("main-menu"))
            return new MenuMetadata
            {
                Music = "Wondering",
                Type = "wolf3d-menu",
                Position = new Vector2(76, 55),
                Indent = 24,
                Components = [
                    new Background("BORDCOLOR"),
                    new Graphic("c_mouselback", HorizontalOrientation.Center, 184),
                    new Stripe(10),
                    new Window(68, 52, 178, 136, "wolf3d-theme"),
                    new Graphic("c_options", HorizontalOrientation.Center, 0)
                ],
                MenuItems = [
                    new MenuSwitcher("$MENU_NEWGAME", true, "CP_NewGame"),
                    new MenuSwitcher("$MENU_SOUND", true, "CP_Sound"),
                    new MenuSwitcher("$MENU_CONTROL", true, "CP_Control"),
                    new MenuSwitcher("$MENU_LOADGAME", true, "CP_LoadGame"),
                    new MenuSwitcher("$MENU_SAVEGAME", true, "CP_SaveGame"),
                    new MenuSwitcher("$MENU_CHANGEVIEW", true, "CP_ChangeView"),
                    new MenuSwitcher("$MENU_READTHIS", true, "CP_ReadThis"),
                    new MenuSwitcher("$MENU_VIEWSCORES", true, "CP_ViewScoresOrEndGame"),
                    new MenuSwitcher("$MENU_BACKTODEMO", true, "CP_DemoOrPlayGame"),
                    new MenuSwitcher("$MENU_QUIT", true, "CP_Quit"), // MenuConfirm
                ]
            };

        if (normalizedName.Equals("sound"))
            return new MenuMetadata
            {
                Music = "Wondering",
                Type = "wolf3d-menu",
                Position = new Vector2(48, 20),
                Indent = 52,
                Components = [
                    new Background("BORDCOLOR"),
                    new Graphic("c_mouselback", HorizontalOrientation.Center, 184),
                    new Window(40, 17, 250, 45, "wolf3d-theme"),
                    new Window(40, 82, 250, 45, "wolf3d-theme"),
                    new Window(40, 147, 250, 32, "wolf3d-theme"),
                    new Graphic("c_fxtitle", 100, 0),
                    new Graphic("c_digititle", 100, 65),
                    new Graphic("c_musictitle", 100, 130)
                ],
                MenuItems = [
                    new ToggleMenuItem("None", true, false),
                    new ToggleMenuItem("PC Speaker", true, false),
                    new ToggleMenuItem("AdLib/Sound Blaster", true, false),
                    // gap of 2
                    new BlankMenuItem(),
                    new BlankMenuItem(),
                    new ToggleMenuItem("None", true, false),
                    new ToggleMenuItem("Disney Sound Source", false, false),
                    new ToggleMenuItem("Sound Blaster", true, false),
                    // gap of 2
                    new BlankMenuItem(),
                    new BlankMenuItem(),
                    new ToggleMenuItem("None", true, false),
                    new ToggleMenuItem("AdLib/Sound Blaster", true, false),
                ]
            };


        if (normalizedName.Equals("control"))
            return new MenuMetadata
            {
                Music = "Wondering",
                Type = "wolf3d-menu",
                Position = new Vector2(24, 86),
                Indent = 56,
                Components = [
                    new Background("BORDCOLOR"),
                    new Graphic("c_mouselback", HorizontalOrientation.Center, 184),
                    new Stripe(10),
                    new Window(16, 81, 284, 60, "wolf3d-theme"),
                    new Graphic("c_options", HorizontalOrientation.Center, 0)
                ],
                MenuItems = [
                    new ToggleMenuItem("Mouse Enabled", false, false),
                    new MenuSwitcher("Mouse Sensitivity", false, "MouseSensitivity"),
                    new ToggleMenuItem("Joystick Enabled", false, false),
                    new MenuSwitcher("Customize controls", true, "CustomControls")
                ]
            };

        if (normalizedName.Equals("game-options"))
            return new MenuMetadata
            {
                Music = "Wondering",
                Type = "wolf3d-menu",
                Position = new Vector2(48, 20),
                Indent = 52,
                Components = [
                    new Background("BORDCOLOR"),
                    new Graphic("c_mouselback", HorizontalOrientation.Center, 184),
                ],
                MenuItems = [
                    new ToggleMenuItem("Unlimited Pushwall Limit", true, false),
                    new ToggleMenuItem("Fake Hitler Fireballs", true, false),
                    new ToggleMenuItem("Weapon Pickup Progression", true, false),
                ]
            };

        return null;
    }

    public ColorMetadata? GetColors(string theme)
    {
        var normalizedTheme = theme.ToLowerInvariant();
        if (normalizedTheme.Equals("wolf3d-theme"))
            return new ColorMetadata
            {
                Colors256 = new Dictionary<string, byte>
                {
                    { "BORDCOLOR", 0x29 },
                    { "BORD2COLOR", 0x23 },
                    { "DEACTIVE", 0x2b },
                    { "BKGDCOLOR", 0x2d },
                    { "STRIPE", 0x2c },
                    { "READCOLOR", 0x4a },
                    { "READHCOLOR", 0x47 },
                    { "VIEWCOLOR", 0x7f },
                    { "TEXTCOLOR", 0x17 },
                    { "HIGHLIGHT", 0x13 },
                    { "Black", 0x00 },
                    { "White", 0x0f },
                    { "Grey", 0x17 },
                    { "Green", 0x67 },
                    { "DarkGreen", 0x6b },
                    { "Yellow", 0x48 },
                    { "Purple", 0xab },
                    { "Dark Blue", 0x9e },
                    { "Navy", 0x9a },
                    { "Blue", 0x92 },
                    { "Maroon", 0x04 },
                    { "Light Blue", 0x82 },
                    { "FIRSTCOLOR", 0x32 },
                    { "SECONDCOLOR", 0x37 },
                    { "Lime", 0x0a },
                    { "Bright Yellow", 0x0e },
                    { "Dark Yellow", 0x4f }
                },
                Colors = new Dictionary<string, Color>
                {
                    { "BORDCOLOR", Color.FromByteRGB("137 0 0") },// 0x29
                    { "BORD2COLOR", Color.FromByteRGB("214 0 0") }, //0x23
                    { "DEACTIVE", Color.FromByteRGB("113 0 0") }, // 0x2b
                    { "BKGDCOLOR", Color.FromByteRGB("89 0 0") }, // 0x2d 
                    { "STRIPE", Color.FromByteRGB("101 0 0") }, // 0x2c
                    { "READCOLOR", Color.FromByteRGB("182 174 0") }, // 0x4a
                    { "READHCOLOR", Color.FromByteRGB("255 246 0") }, // 0x47
                    { "VIEWCOLOR", Color.FromByteRGB("0 64 64") }, // 0x7f
                    { "TEXTCOLOR", Color.FromByteRGB("141 141 141") }, // 0x17
                    { "HIGHLIGHT", Color.FromByteRGB("194 194 194") }, // 0x13
                    { "Black", Color.FromByteRGB("0 0 0") }, // 0x00
                    { "White", Color.FromByteRGB("255 255 255") }, // 0x0f
                    { "Grey", Color.FromByteRGB("141 141 141") },
                    { "Green", Color.FromByteRGB("4 165 0") }, // 0x67
                    { "DarkGreen", Color.FromByteRGB("4 113 0") }, // 0x6b
                    { "Yellow", Color.FromByteRGB("230 218 0") },
                    { "Purple", Color.FromByteRGB("97 0 157") },
                    { "Dark Blue", Color.FromByteRGB("0 0 76") },
                    { "Navy", Color.FromByteRGB("0 0 125") },
                    { "Blue", Color.FromByteRGB("0 0 226") },
                    { "Maroon", Color.FromByteRGB("170 0 0") },
                    { "Light Blue", Color.FromByteRGB("32 170 255") },
                    { "FIRSTCOLOR", Color.FromByteRGB("255 157 157") },
                    { "SECONDCOLOR", Color.FromByteRGB("255 0 0") },
                    { "Lime", Color.FromByteRGB("85 255 85") },
                    { "Bright Yellow", Color.FromByteRGB("255 255 85") },
                    { "Dark Yellow", Color.FromByteRGB("64 64 0") },
                }
            };
        if (normalizedTheme.Equals("spear-theme"))
            return new ColorMetadata
            {
                Colors256 = new Dictionary<string, byte>
                {
                    { "BORDCOLOR", 0x99 },
                    { "BORD2COLOR", 0x93 },
                    { "DEACTIVE", 0x9b },
                    { "BKGDCOLOR", 0x9d },
                    { "STRIPE", 0x9c },
                    { "READCOLOR", 0x4a },
                    { "READHCOLOR", 0x47 },
                    { "VIEWCOLOR", 0x7f },
                    { "TEXTCOLOR", 0x17 },
                    { "HIGHLIGHT", 0x13 },
                    { "Black", 0x00 },
                    { "White", 0x0f },
                    { "Grey", 0x17 },
                    { "Green", 0x67 },
                    { "DarkGreen", 0x6b },
                    { "Yellow", 0x48 },
                },
                Colors = new Dictionary<string, Color>
                {
                    { "BORDCOLOR", Color.FromByteRGB("0 0 137") },// 0x99
                    { "BORD2COLOR", Color.FromByteRGB("0 0 214") }, //0x93
                    { "DEACTIVE", Color.FromByteRGB("0 0 113") }, // 0x9b
                    { "BKGDCOLOR", Color.FromByteRGB("0 0 89") }, // 0x9d 
                    { "STRIPE", Color.FromByteRGB("0 0 101") }, // 0x9c
                    { "READCOLOR", Color.FromByteRGB("182 174 0") }, // 0x4a
                    { "READHCOLOR", Color.FromByteRGB("255 246 0") }, // 0x47
                    { "VIEWCOLOR", Color.FromByteRGB("0 64 64") }, // 0x7f
                    { "TEXTCOLOR", Color.FromByteRGB("141 141 141") }, // 0x17
                    { "HIGHLIGHT", Color.FromByteRGB("194 194 194") }, // 0x13
                    { "Black", Color.FromByteRGB("0 0 0") }, // 0x00
                    { "White", Color.FromByteRGB("255 255 255") }, // 0x0f
                    { "Grey", Color.FromByteRGB("141 141 141") },
                    { "Green", Color.FromByteRGB("4 165 0") }, // 0x67
                    { "DarkGreen", Color.FromByteRGB("4 113 0") }, // 0x6b
                }
            };

        return null;
    }

    public LanguageMetadata? GetText(string language)
    {
        var normalizedName = language.ToLowerInvariant();
        if (normalizedName == "en-us")
            return new LanguageMetadata
            {
                TextStrings = new Dictionary<string, string>
                {
                    { "$CURGAME", "You are currently in\na game. Continuing will\nerase old game. Ok?" },
                    { "$GAMESVD", "There's already a game\nsaved at this position.\n      Overwrite?" },
                    { "$ENDGAMESTR", "Are you sure you want\nto end the game you\nare playing? ($YESBUTTONNAME or $NOBUTTONNAME):" },
                    { "$MENU_NEWGAME", "New Game" },
                    { "$MENU_SOUND", "Sound" },
                    { "$MENU_CONTROL", "Control" },
                    { "$MENU_LOADGAME", "Load Game" },
                    { "$MENU_SAVEGAME", "Save Game" },
                    { "$MENU_CHANGEVIEW", "Change View" },
                    { "$MENU_READTHIS", "Read This!" },
                    { "$MENU_VIEWSCORES", "View Scores" },
                    { "$MENU_ENDGAME", "End Game" },
                    { "$MENU_BACKTODEMO", "Back to Demo" },
                    { "$MENU_BACKTOGAME", "Back to Game" },
                    { "$MENU_QUIT", "Quit" },
                    { "$STR_LOADING", "Loading"},
                    { "$STR_SAVING", "Saving"},

                    { "$STR_LGC", "Load Game called\n\""},
                    { "$STR_EMPTY", "empty"},
                    { "$STR_CALIB", "Calibrate"},
                    { "$STR_JOYST", "Joystick"},
                    { "$STR_MOVEJOY", "Move joystick to\nupper left and\npress button 0\n"},
                    { "$STR_MOVEJOY2", "Move joystick to\nlower right and\npress button 1\n"},
                    { "$STR_ESCEXIT", "ESC to exit"},

                    { "$STR_NONE", "None"},
                    { "$STR_PC", "PC Speaker"},
                    { "$STR_ALSB", "AdLib/Sound Blaster"},
                    { "$STR_DISNEY", "Disney Sound Source"},
                    { "$STR_SB", "Sound Blaster"},

                    { "$STR_MOUSEEN", "Mouse Enabled"},
                    { "$STR_JOYEN", "Joystick Enabled"},
                    { "$STR_PORT2", "Use joystick port 2"},
                    { "$STR_GAMEPAD", "Gravis GamePad Enabled"},
                    { "$STR_SENS", "Mouse Sensitivity"},
                    { "$STR_CUSTOM", "Customize controls"},

                    { "$STR_MOUSEADJ", "Adjust Mouse Sensitivity"},
                    { "$STR_SLOW", "Slow"},
                    { "$STR_FAST", "Fast"},

                    { "$STR_CRUN", "Run"},
                    { "$STR_COPEN", "Open"},
                    { "$STR_CFIRE", "Fire"},
                    { "$STR_CSTRAFE", "Strafe"},

                    { "$STR_LEFT", "Left"},
                    { "$STR_RIGHT", "Right"},
                    { "$STR_FRWD", "Frwd"},
                    { "$STR_BKWD", "Bkwrd"},
                    { "$STR_THINK", "Thinking"},

                    { "$STR_SIZE1", "Use arrows to size"},
                    { "$STR_SIZE2", "ENTER to accept"},
                    { "$STR_SIZE3", "ESC to cancel"},

                    { "$STR_YOUWIN", "you win!"},

                    { "$STR_TOTALTIME", "total time"},

                    { "$STR_RATKILL", "kill    %"},
                    { "$STR_RATSECRET", "secret    %"},
                    { "$STR_RATTREASURE", "treasure    %"},

                    { "$STR_BONUS", "bonus"},
                    { "$STR_TIME", "time"},
                    { "$STR_PAR", " par"},

                    { "$STR_RAT2KILL", "kill ratio    %"},
                    { "$STR_RAT2SECRET", "secret ratio    %"},
                    { "$STR_RAT2TREASURE", "treasure ratio    %"},

                    { "$STR_DEFEATED", "defeated!"},

                    { "$STR_CHEATER1", "You now have 100% Health,"},
                    { "$STR_CHEATER2", "99 Ammo and both Keys!"},
                    { "$STR_CHEATER3", "Note that you have basically"},
                    { "$STR_CHEATER4", "eliminated your chances of"},
                    { "$STR_CHEATER5", "getting a high score!"},

                    { "$STR_NOSPACE1", "There is not enough space"},
                    { "$STR_NOSPACE2", "on your disk to Save Game!"},

                    { "$STR_SAVECHT1", "Your Save Game file is,"},
                    { "$STR_SAVECHT2", "shall we say, \"corrupted\"."},
                    { "$STR_SAVECHT3", "But I'll let you go on and"},
                    { "$STR_SAVECHT4", "play anyway...."},

                    { "$STR_SEEAGAIN", "Let's see that again!"},

                    // Wolfenstein 3D
                    { "$ENDSTR1", "Dost thou wish to\nleave with such hasty\nabandon?" },
                    { "$ENDSTR2", "Chickening out...\nalready?" },
                    { "$ENDSTR3", "Press {NOBUTTONNAME} for more carnage.\nPress {YESBUTTONNAME} to be a weenie." },
                    { "$ENDSTR4", "So, you think you can\nquit this easily, huh?" },
                    { "$ENDSTR5", "Press {NOBUTTONNAME} to save the world.\nPress {YESBUTTONNAME} to abandon it in\nits hour of need." },
                    { "$ENDSTR6", "Press {NOBUTTONNAME} if you are brave.\nPress {YESBUTTONNAME} to cower in shame." },
                    { "$ENDSTR7", "Heroes, press \" {NOBUTTONNAME} \".\nWimps, press {YESBUTTONNAME}." },
                    { "$ENDSTR8", "You are at an intersection.\nA sign says, 'Press {YESBUTTONNAME} to quit.'\n>" },
                    { "$ENDSTR9", "For guns and glory, press {NOBUTTONNAME}.\nFor work and worry, press {YESBUTTONNAME}." },

                    // Spear of Destiny
                    { "$ENDSTR10", "Heroes don't quit, but\ngo ahead and press {YESBUTTONNAME}\nif you aren't one." },
                    { "$ENDSTR11", "Press {YESBUTTONNAME} to quit,\nor press {NOBUTTONNAME} to enjoy\nmore violent diversion." },
                    { "$ENDSTR12", "Depressing the {YESBUTTONNAME} key means\nyou must return to the\nhumdrum workday world." },
                    { "$ENDSTR13", "Hey, quit or play,\n{YESBUTTONNAME} or {NOBUTTONNAME}:\nit's your choice." },
                    { "$ENDSTR14", "Sure you don't want to\nwaste a few more\nproductive hours?" },
                    { "$ENDSTR15", "I think you had better\nplay some more. Please\npress {NOBUTTONNAME}...please?" },
                    { "$ENDSTR16", "If you are tough, press {NOBUTTONNAME}.\nIf not, press {YESBUTTONNAME} daintily." },
                    { "$ENDSTR17", "I'm thinkin' that\nyou might wanna press {NOBUTTONNAME}\nto play more. You do it." },
                    { "$ENDSTR18", "Sure. Fine. Quit.\nSee if we care.\nGet it over with.\nPress {YESBUTTONNAME}." },
                }
            };

        return null;
    }

    public MapActorMetadata GetMapActors(string map)
    {
        // Default for all maps, but can be defined per map if needed
        return new MapActorMetadata
        {
            // Tiles
                // tile
                // trigger
                // zones
            Things = new Dictionary<int, ActorSpawnData>
            {
                { 23, new ActorSpawnData("Puddle", 0, 0, 0) }, //oldnum, class, angles, patrol, minskill
                { 24, new ActorSpawnData("GreenBarrel", 0, 0, 0) },
	            { 25, new ActorSpawnData("TableWithChairs",   0, 0, 0) },
	            { 26, new ActorSpawnData("FloorLamp",         0, 0, 0) },
	            { 27, new ActorSpawnData("Chandelier",        0, 0, 0) },
	            { 28, new ActorSpawnData("HangedMan",         0, 0, 0) },
	            { 29, new ActorSpawnData("DogFood",           0, 0, 0) },
	            { 30, new ActorSpawnData("WhitePillar",       0, 0, 0) },
	            { 31, new ActorSpawnData("GreenPlant",        0, 0, 0) },
	            { 32, new ActorSpawnData("SkeletonFlat",      0, 0, 0) },
	            { 33, new ActorSpawnData("Sink",              0, 0, 0) },
	            { 34, new ActorSpawnData("BrownPlant",        0, 0, 0) },
	            { 35, new ActorSpawnData("Vase",              0, 0, 0) },
	            { 36, new ActorSpawnData("BareTable",         0, 0, 0) },
	            { 37, new ActorSpawnData("CeilingLight",      0, 0, 0) },
	            { 38, new ActorSpawnData("KitchenStuff",      0, 0, 0) },
	            { 39, new ActorSpawnData("SuitOfArmor",       0, 0, 0) },
	            { 40, new ActorSpawnData("HangingCage",       0, 0, 0) },
	            { 41, new ActorSpawnData("SkeletonCage",      0, 0, 0) },
	            { 42, new ActorSpawnData("Bones1",            0, 0, 0) },
	            { 43, new ActorSpawnData("GoldKey",           0, 0, 0) },
	            { 44, new ActorSpawnData("SilverKey",         0, 0, 0) },
	            { 45, new ActorSpawnData("BunkBed",           0, 0, 0) },
	            { 46, new ActorSpawnData("Basket",            0, 0, 0) },
	            { 47, new ActorSpawnData("Food",              0, 0, 0) },
	            { 48, new ActorSpawnData("Medikit",           0, 0, 0) },
	            { 49, new ActorSpawnData("Clip",              0, 0, 0) },
	            { 50, new ActorSpawnData("MachineGun",        0, 0, 0) },
	            { 51, new ActorSpawnData("GatlingGunUpgrade", 0, 0, 0) },
	            { 52, new ActorSpawnData("Cross",             0, 0, 0) },
	            { 53, new ActorSpawnData("Chalice",           0, 0, 0) },
	            { 54, new ActorSpawnData("ChestofJewels",     0, 0, 0) },
	            { 55, new ActorSpawnData("Crown",             0, 0, 0) },
	            { 56, new ActorSpawnData("OneUp",             0, 0, 0) },
	            { 57, new ActorSpawnData("Gibs",              0, 0, 0) },
	            { 58, new ActorSpawnData("Barrel",            0, 0, 0) },
	            { 59, new ActorSpawnData("Well",              0, 0, 0) },
	            { 60, new ActorSpawnData("EmptyWell",         0, 0, 0) },
	            { 61, new ActorSpawnData("Blood",             0, 0, 0) },
	            { 62, new ActorSpawnData("Flag",              0, 0, 0) },
	            { 63, new ActorSpawnData("CallApogee",        0, 0, 0) },
	            { 64, new ActorSpawnData("Bones2",            0, 0, 0) },
	            { 65, new ActorSpawnData("Bones3",            0, 0, 0) },
	            { 66, new ActorSpawnData("Bones4",            0, 0, 0) },
	            { 67, new ActorSpawnData("Pots",              0, 0, 0) },
	            { 68, new ActorSpawnData("Stove",             0, 0, 0) },
	            { 69, new ActorSpawnData("Spears",            0, 0, 0) },
	            { 70, new ActorSpawnData("Vines",             0, 0, 0) },
            }
        };
    }

    public ActorMetadata GetActorMetadata()
    {
        try
        {
            var data = new ActorMetadata();
            data.AddActors(GetDecorations());
            data.AddActors(GetActors());
            return data;
        }
        catch (Exception e)
        {
            Console.Write(e);
            throw;
        }
    }

    private Dictionary<string, ActorData> GetDecorations()
    {
        return new Dictionary<string, ActorData>
        {
            {
                "Puddle",
                new ActorData
                {
                    Id = 33,
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "WATR",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Puddle2",
                new ActorData
                {
                    Id = 105,
                    Parent = "Puddle",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "WTR2",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "GreenBarrel",
                new ActorData
                {
                    Id = 34,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "DRUM",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "HorizontalGreenBarrel",
                new ActorData
                {
                    Id = 106,
                    Parent = "GreenBarrel",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "HDRM",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "TableWithChairs",
                new ActorData
                {
                    Id = 35,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "TCHR",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "TableWithChairs2",
                new ActorData
                {
                    Id = 107,
                    Parent = "TableWithChairs",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "TCR2",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "FloorLamp",
                new ActorData
                {
                    Id = 36,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "FLMP",
                                Frames = ["A"],
                                Modifiers = ["bright"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "TallFloorLamp",
                new ActorData
                {
                    Id = 108,
                    Parent = "FloorLamp",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "LMP2",
                                Frames = ["A"],
                                Modifiers = ["bright"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Chandelier",
                new ActorData
                {
                    Id = 37,
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "CHAN",
                                Frames = ["A"],
                                Modifiers = ["bright"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Chandelier2",
                new ActorData
                {
                    Id = 109,
                    Parent = "Chandelier",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "CHN2",
                                Frames = ["A"],
                                Modifiers = ["bright"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "HangedMan",
                new ActorData
                {
                    Id = 38,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "HANG",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "HangedMan2",
                new ActorData
                {
                    Id = 110,
                    Parent = "HangedMan",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "HNG2",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "WhitePillar",
                new ActorData
                {
                    Id = 40,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "COLU",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "CrackedPillar",
                new ActorData
                {
                    Id = 112,
                    Parent = "WhitePillar",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "COL3",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "GreenPlant",
                new ActorData
                {
                    Id = 41,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "PLNT",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "GreenPlant2",
                new ActorData
                {
                    Id = 113,
                    Parent = "GreenPlant",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "PNT2",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "SkeletonFlat",
                new ActorData
                {
                    Id = 42,
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "SKEL",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "SkeletonFlat2",
                new ActorData
                {
                    Id = 114,
                    Parent = "SkeletonFlat",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "SKL2",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Sink",
                new ActorData
                {
                    Id = 43,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "SINK",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "BrownPlant",
                new ActorData
                {
                    Id = 44,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "BPNT",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "BrownPlant2",
                new ActorData
                {
                    Id = 115,
                    Parent = "BrownPlant",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "BPL2",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Vase",
                new ActorData
                {
                    Id = 45,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "VASE",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Vase2",
                new ActorData
                {
                    Id = 116,
                    Parent = "Vase",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "VAS2",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "BareTable",
                new ActorData
                {
                    Id = 46,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "TABL",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "MetalTable",
                new ActorData
                {
                    Id = 117,
                    Parent = "BareTable",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "MTBL",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "CeilingLight",
                new ActorData
                {
                    Id = 47,
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "GLMP",
                                Frames = ["A"],
                                Modifiers = ["bright"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "CeilingLight2",
                new ActorData
                {
                    Id = 118,
                    Parent = "CeilingLight",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "GLP2",
                                Frames = ["A"],
                                Modifiers = ["bright"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "RedCeilingLight",
                new ActorData
                {
                    Id = 85,
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "RLMP",
                                Frames = ["A"],
                                Modifiers = ["bright"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "KitchenStuff",
                new ActorData
                {
                    Id = 48,
                    Radius = 32,
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "POT1",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "SuitOfArmor",
                new ActorData
                {
                    Id = 49,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "ARMR",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "SuitOfArmor2",
                new ActorData
                {
                    Id = 120,
                    Parent = "SuitOfArmor",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "ARM2",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "HangingCage",
                new ActorData
                {
                    Id = 50,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "CAG1",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "HangingCage2",
                new ActorData
                {
                    Id = 121,
                    Parent = "HangingCage",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "CAG6",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "SkeletonCage",
                new ActorData
                {
                    Id = 51,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "CAG2",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "BrokenCage",
                new ActorData
                {
                    Id = 122,
                    Parent = "SkeletonCage",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "CAG7",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Bones1",
                new ActorData
                {
                    Id = 52,
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "BON1",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Bones5",
                new ActorData
                {
                    Id = 123,
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "BON5",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "BunkBed",
                new ActorData
                {
                    Id = 55,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "BUNK",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Basket",
                new ActorData
                {
                    Id = 56,
                    Radius = 32,
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "BASK",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "DeadRat",
                new ActorData
                {
                    Id = 125,
                    Parent = "Basket",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "DRAT",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Barrel",
                new ActorData
                {
                    Id = 68,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "BARL",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "HorizontalBarrel",
                new ActorData
                {
                    Id = 151,
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "BON1",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Well",
                new ActorData
                {
                    Id = 69,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "WEL1",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Well2",
                new ActorData
                {
                    Id = 152,
                    Parent = "Well",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "WEL4",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "EmptyWell",
                new ActorData
                {
                    Id = 70,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "WEL2",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "EmptyWell2",
                new ActorData
                {
                    Id = 153,
                    Parent = "EmptyWell",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "WEL5",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Flag",
                new ActorData
                {
                    Id = 72,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "FLAG",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "TeslaCoil",
                new ActorData
                {
                    Id = 127,
                    Parent = "Flag",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "COIL",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "CallApogee",
                new ActorData
                {
                    Id = 73,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "AARD",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Bones2",
                new ActorData
                {
                    Id = 74,
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "BON2",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Bones6",
                new ActorData
                {
                    Id = 69,
                    Parent = "Bones2",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "BON6",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Bones3",
                new ActorData
                {
                    Id = 75,
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "BON3",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "BareLightBulb",
                new ActorData
                {
                    Id = 130,
                    Parent = "Bones3",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "BULB",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Bones4",
                new ActorData
                {
                    Id = 76,
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "BON4",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "GreenSlime",
                new ActorData
                {
                    Id = 131,
                    Parent = "Bones4",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "SLIM",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Pots",
                new ActorData
                {
                    Id = 77,
                    Radius = 32,
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "POT2",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Stove",
                new ActorData
                {
                    Id = 78,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "STOV",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Spears",
                new ActorData
                {
                    Id = 79,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "RACK",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Vines",
                new ActorData
                {
                    Id = 80,
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "VINE",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Bubbles",
                new ActorData
                {
                    Id = 135,
                    Parent = "Vines",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "BUBL",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "TruckRear",
                new ActorData
                {
                    Id = 91,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "TRUK",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "BJWasHere",
                new ActorData
                {
                    Id = 137,
                    Parent = "TruckRear",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "BJWH",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "CageWithSkulls",
                new ActorData
                {
                    Id = 84,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "CAG4",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "CageWithRat",
                new ActorData
                {
                    Id = 124,
                    Parent = "CageWithSkulls",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "CAG8",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "CageWithGore",
                new ActorData
                {
                    Id = 83,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "CAG3",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "CageWithGore2",
                new ActorData
                {
                    Id = 119,
                    Parent = "CageWithGore",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "CAG5",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "Skewer",
                new ActorData
                {
                    Id = 82,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "SKWR",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "SkullPile",
                new ActorData
                {
                    Id = 150,
                    Parent = "Skewer",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "SKPI",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "CowCarcass",
                new ActorData
                {
                    Id = 86,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "COWC",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "ChemicalTable",
                new ActorData
                {
                    Id = 132,
                    Parent = "CowCarcass",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "CHEM",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "WellWithBlood",
                new ActorData
                {
                    Id = 87,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "WEL3",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "NuclearBarrel",
                new ActorData
                {
                    Id = 133,
                    Parent = "WellWithBlood",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "NUKE",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "SatanStatue",
                new ActorData
                {
                    Id = 88,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "STAT",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "BlueColumn",
                new ActorData
                {
                    Id = 78,
                    Parent = "SatanStatue",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "COL4",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "BrownColumn",
                new ActorData
                {
                    Id = 89,
                    Radius = 32,
                    Flags = ["SOLID"],
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "COL2",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
            {
                "DevilStatue",
                new ActorData
                {
                    Id = 136,
                    Parent = "BrownColumn",
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [new ActorStatesData
                            {
                                Sprite = "STT2",
                                Frames = ["A"],
                                TicsPerFrame = -1
                            }]
                        }
                    }
                }
            },
        };
    }

    private Dictionary<string, ActorData> GetActors()
    {
        return new Dictionary<string, ActorData>
        {
            {
                "WolfensteinMonster",
                new ActorData
                {
                    Id = 11,
                    Properties = new Dictionary<string, object>
                    {
                        { "missilefrequency", 0.08 },
                        { "minmissilechance", 256 },
                        { "height", 64 },
                        { "radius", 42 },
                        { "painchance", 256 },
                        { "meleerange", 42 },
                    },
                    Flags = ["MONSTER", "ALWAYSFAST", "RANDOMIZE", "OLDRANDOMCHASE"],
                    Parent = "WolfensteinMonster"
                }
            },
            {
                "Guard",
                new ActorData
                {
                    Id = 11,
                    Parent = "WolfensteinMonster",
                    Properties = new Dictionary<string, object>
                    {
                        { "points", 100 },
                    },
                    States = new Dictionary<string, List<StateData>>
                    {
                        {
                            "Spawn", [
                                new ActorStatesData
                                {
                                    Sprite = "GARD",
                                    Frames = ["A"],
                                    TicsPerFrame = -1,
                                    Action = "",
                                    Think = "A_Look"
                                },
                                new StopStateData()
                            ]
                        },
                        {
                            "Path", [
                                new ActorStatesData
                                {
                                    Sprite = "GARD",
                                    Frames = ["B"],
                                    TicsPerFrame = 10,
                                    Action = "",
                                    Think = "A_Chase"
                                },
                                new ActorStatesData
                                {
                                    Sprite = "GARD",
                                    Frames = ["B"],
                                    TicsPerFrame = 2.5f,
                                    Action = "",
                                    Think = ""
                                },
                                new ActorStatesData
                                {
                                    Sprite = "GARD",
                                    Frames = ["C"],
                                    TicsPerFrame = 7.5f,
                                    Action = "",
                                    Think = "A_Chase"
                                },
                                new ActorStatesData
                                {
                                    Sprite = "GARD",
                                    Frames = ["D"],
                                    TicsPerFrame = 10,
                                    Action = "",
                                    Think = "A_Chase"
                                },
                                new ActorStatesData
                                {
                                    Sprite = "GARD",
                                    Frames = ["D"],
                                    TicsPerFrame = 2.5f,
                                    Action = "",
                                    Think = ""
                                },
                                new ActorStatesData
                                {
                                    Sprite = "GARD",
                                    Frames = ["E"],
                                    TicsPerFrame = 7.5f,
                                    Action = "",
                                    Think = "A_Chase"
                                },
                                new LoopStateData()
                            ]
                        },
                        {
                            "Chase", [
                                new ActorStatesData
                                {
                                    Sprite = "GARD",
                                    Frames = ["B"],
                                    TicsPerFrame = 5,
                                    Action = "",
                                    Think = "A_Chase"
                                },
                                new ActorStatesData
                                {
                                    Sprite = "GARD",
                                    Frames = ["B"],
                                    TicsPerFrame = 1.5f,
                                    Action = "",
                                    Think = ""
                                },
                                new ActorStatesData
                                {
                                    Sprite = "GARD",
                                    Frames = ["C"],
                                    TicsPerFrame = 4f,
                                    Action = "",
                                    Think = "A_Chase"
                                },
                                new ActorStatesData
                                {
                                    Sprite = "GARD",
                                    Frames = ["D"],
                                    TicsPerFrame = 5,
                                    Action = "",
                                    Think = "A_Chase"
                                },
                                new ActorStatesData
                                {
                                    Sprite = "GARD",
                                    Frames = ["D"],
                                    TicsPerFrame = 1.5f,
                                    Action = "",
                                    Think = ""
                                },
                                new ActorStatesData
                                {
                                    Sprite = "GARD",
                                    Frames = ["E"],
                                    TicsPerFrame = 4f,
                                    Action = "",
                                    Think = "A_Chase"
                                },
                                new LoopStateData()
                            ]
                        },
                        {
                            "Attack", [
                                new ActorStatesData
                                {
                                    Sprite = "GARD",
                                    Frames = ["F", "G"],
                                    TicsPerFrame = 10,
                                    Action = "A_FaceTarget",
                                    Think = ""
                                },
                                new ActorStatesData
                                {
                                    Sprite = "GARD",
                                    Frames = ["H"],
                                    TicsPerFrame = 10,
                                    Modifiers = [ "BRIGHT" ],
                                    Action = "A_WolfAttack",
                                    Think = ""
                                },
                                new GoToStateData("Chase")
                            ]
                        },
                        {
                            "Pain", [
                                new ActorStatesData()
                                {
                                    Sprite = "GARD",
                                    Frames = ["I"],
                                    TicsPerFrame = 5,
                                    Action = "A_JumpIf(health & 1, 1)",
                                    Think = ""
                                },
                                new GoToStateData("Chase"),
                                new ActorStatesData()
                                {
                                    Sprite = "GARD",
                                    Frames = ["J"],
                                    TicsPerFrame = 5,
                                    Action = "",
                                    Think = ""
                                },
                                new GoToStateData("Chase")
                            ]
                        },
                        {
                            "Death", [
                                new ActorStatesData()
                                {
                                    Sprite = "GARD",
                                    Frames = ["K"],
                                    TicsPerFrame = 7.5f,
                                    Action = "A_Fall",
                                    Think = ""
                                },
                                new ActorStatesData()
                                {
                                    Sprite = "GARD",
                                    Frames = ["L"],
                                    TicsPerFrame = 7.5f,
                                    Action = "A_Scream",
                                    Think = ""
                                },
                                new ActorStatesData()
                                {
                                    Sprite = "GARD",
                                    Frames = ["M"],
                                    TicsPerFrame = 7.5f
                                },
                                new ActorStatesData()
                                {
                                    Sprite = "GARD",
                                    Frames = ["N"],
                                    TicsPerFrame = -1
                                },
                                new StopStateData()
                            ]
                        }
                    }
                }
            },
        };
    }
}
