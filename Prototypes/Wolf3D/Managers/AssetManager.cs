using System.Numerics;
using Wolf3D.Entities;
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
            }
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
                    new Background(0x29),
                    new Graphic("c_mouselback", HorizontalOrientation.Center, 184),
                    new Stripe(10),
                    new Window(68, 52, 178, 136, "wolf3d-theme"),
                    new Graphic("c_options", HorizontalOrientation.Center, 0)
                ],
                MenuItems = [
                    new MenuSwitcher("New Game", true, "CP_NewGame"),
                    new MenuSwitcher("Sound", true, "CP_Sound"),
                    new MenuSwitcher("Control", true, "CP_Control"),
                    new MenuSwitcher("Load Game", true, "CP_LoadGame"),
                    new MenuSwitcher("Save Game", true, "CP_SaveGame"),
                    new MenuSwitcher("Change View", true, "CP_ChangeView"),
                    new MenuSwitcher("Read This!", true, "CP_ReadThis"),
                    new MenuSwitcher("View Scores", true, "CP_ViewScoresOrEndGame"),
                    new MenuSwitcher("Back To Demo", true, "CP_DemoOrPlayGame"),
                    new MenuSwitcher("Quit", true, "CP_Quit"), // MenuConfirm
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
                    new Background(0x29),
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
                // TODO:
            };

        if (normalizedName.Equals("game-options"))
            return new MenuMetadata
            {
                Music = "Wondering",
                Type = "wolf3d-menu",
                Position = new Vector2(48, 20),
                Indent = 52,
                Components = [
                    new Background(0x29),
                    new Graphic("c_mouselback", HorizontalOrientation.Center, 184),
                ],
                MenuItems = [
                    new ToggleMenuItem("Unlimited Pushwall Limit", true, false),
                    new ToggleMenuItem("Fake Hitler Fireballs", true, false),
                ]
            };

        return null;
    }
}
