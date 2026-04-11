using System.Numerics;
using Wolf3D.Entities;
using Wolf3D.Entities.Actors;
using Wolf3D.Mappers;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Wolf3D.Managers;

internal class AssetManager
{
    [Obsolete("Temporary endpoint until the asset types are implemented")]
    public GameInfoMetadata GetGameInfo()
    {
        var yaml = File.ReadAllText(Path.Combine("D:\\projects\\Wolf3D\\PFWolf\\pf-wolf\\pfwolf-pk3\\gamepacks", "game-info.yaml"));
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .Build();
        return deserializer.Deserialize<GameInfoMetadata>(yaml);
    }

    [Obsolete("Temporary endpoint until the asset types are implemented")]
    public MenuMetadata? GetMenu(string name)
    {
        var normalizedName = name.ToLowerInvariant();
        //var yaml = File.ReadAllText(Path.Combine("D:\\projects\\Wolf3D\\PFWolf\\pf-wolf\\pfwolf-pk3\\menudefs", $"{normalizedName}.yaml"));
        //var deserializer = new DeserializerBuilder()
        //    .WithNamingConvention(HyphenatedNamingConvention.Instance)
        //    .Build();
        //return deserializer.Deserialize<MenuMetadata>(yaml);

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

    private MapActorMetadata _mapData = new();
    public MapActorMetadata GetMapActors(string map)
    {
        if (_mapData.Things.Count > 0)
            return _mapData;

        var yaml = File.ReadAllText(Path.Combine("D:\\projects\\Wolf3D\\PFWolf\\pf-wolf\\pfwolf-pk3\\mapdefs\\", "map-data.yaml"));
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .Build();
        _mapData = deserializer.Deserialize<MapActorMetadata>(yaml);
        return _mapData;
    }

    public ActorMetadata GetActorMetadata()
    {
        try
        {
            var data = new ActorMetadata();
            data.AddActors(GetDecorations());
            //data.AddActors(GetActors());
            return data;
        }
        catch (Exception e)
        {
            Console.Write(e);
            throw;
        }
    }
    private Dictionary<string, ActorData> _decorations = new Dictionary<string, ActorData>();
    private Dictionary<string, ActorData> GetDecorations()
    {
        if (_decorations.Count > 0)
            return _decorations;

        var yaml = File.ReadAllText(Path.Combine("D:\\projects\\Wolf3D\\PFWolf\\pf-wolf\\pfwolf-pk3\\actordefs\\wolf3d", "decorations.yaml"));
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .Build();
        _decorations = deserializer.Deserialize<Dictionary<string, ActorData>>(yaml);
        return _decorations;
    }
    private Dictionary<string, ActorData> GetActors()
    {
        return new Dictionary<string, ActorData>();
        /*
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
        */
    }
}
