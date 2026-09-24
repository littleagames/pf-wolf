using Wolf3D.Assets;
using Wolf3D.Assets.Sounds;
using Wolf3D.Entities;
using Wolf3D.Entities.Actors;
using Wolf3D.Loaders;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Wolf3D.Managers;

internal class AssetManager
{
    Dictionary<string, Asset> _assets = new Dictionary<string, Asset>();
    // Menus are drawn every redraw; build each one once
    private readonly Dictionary<string, MenuMetadata> _menuCache = new();
    // Built once per language; menus look text up on every redraw
    private readonly Dictionary<string, LanguageMetadata?> _languageCache = new();
    private string _gamePackId = "";
    private bool strict = false;

    /// <summary>
    /// Sets whether you must specify if an asset exists, and will hard fail, or just quietly continue
    /// </summary>
    /// <param name="strict">If true, the application will prevent further process if the asset does not exist</param>
    public void SetStrictAssets(bool strict)
    {
        this.strict = strict;
    }

    /// <param name="gamePackId">The running game pack ("wolf3d", "spear"), whose gamepacks/ assets override the shared ones</param>
    public void Load(string gamePackId)
    {
        _gamePackId = gamePackId;
        Dictionary<string, Asset> assets = new();
        var pfWolfBasePk3Loader = new PfWolfPk3Loader("pfwolf.pk3");
        assets = pfWolfBasePk3Loader.GetAssets();

        foreach (var kvp in assets)
        {
            if (!_assets.ContainsKey(kvp.Key))
                _assets[kvp.Key] = kvp.Value;
        }

        var rawDataMap = Find<RawDataMapAsset>("wolf3d/raw-data-map");

        var audioLoader = new Wolf3dAudioFileLoader("audiot", "wl6", "audiohed", "wl6");
        assets = audioLoader.GetAssets(rawDataMap?.Audio ?? [], rawDataMap?.Music ?? []);

        foreach (var kvp in assets)
        {
            string assetType = kvp.Value.GetType().Name;
            var key = GetKey(kvp.Key, assetType);
            if (!_assets.ContainsKey(key))
                _assets[key] = kvp.Value;
        }

        var mapLoader = new Wolf3dMapFileLoader("maphead", "gamemaps", "wl6");
        assets = mapLoader.GetAssets(rawDataMap?.Maps ?? []);

        foreach (var kvp in assets)
        {
            string assetType = kvp.Value.GetType().Name;
            var key = GetKey(kvp.Key, assetType);
            if (!_assets.ContainsKey(key))
                _assets[key] = kvp.Value;
        }

        // numFonts isn't stored in the VGAGRAPH file itself, so it must be supplied here.
        var vgaGraphicLoader = new Wolf3dVgaFileLoader("vgahead", "vgagraph", "vgadict", "wl6", numFonts: 2);
        assets = vgaGraphicLoader.GetAssets(rawDataMap?.Graphics ?? []);

        foreach (var kvp in assets)
        {
            string assetType = kvp.Value.GetType().Name;
            var key = GetKey(kvp.Key, assetType);
            if (!_assets.ContainsKey(key))
                _assets[key] = kvp.Value;
        }

        var vswapLoader = new Wolf3dVswapFileLoader("vswap", "wl6");
        assets = vswapLoader.GetAssets(rawDataMap?.Walls ?? [], rawDataMap?.Sprites ?? [], rawDataMap?.DigitizedAudio ?? []);

        foreach (var kvp in assets)
        {
            string assetType = kvp.Value.GetType().Name;
            var key = GetKey(kvp.Key, assetType);
            if (!_assets.ContainsKey(key))
                _assets[key] = kvp.Value;
        }
    }

    public bool Exists<T>(string assetName) where T : Asset
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return false;

        return _assets.ContainsKey(GetKey(assetName, typeof(T).Name));
    }

    public T? Find<T>(string assetName) where T : Asset
    {
        string assetType = typeof(T).Name;

        if (string.IsNullOrWhiteSpace(assetName))
        {
            if (strict)
            {
                throw new ArgumentException($"Asset name cannot be empty. Asset Type: {assetType}", nameof(assetName));
            }

            Console.WriteLine($"Asset name cannot be empty. Asset Type: {assetType}");
            return null;
        }

        var key = GetKey(assetName, assetType);

        if (_assets.TryGetValue(key, out var foundAsset))
            return foundAsset as T;

        Console.WriteLine($"Asset not found: {assetName} (Type: {assetType})");
        return null;
    }

    /// <summary>
    /// Names of every menu defined in menudefs/
    /// </summary>
    public IEnumerable<string> GetMenuNames()
    {
        var prefix = $"{nameof(MenuAsset)}:".ToLowerInvariant();
        return _assets.Keys
            .Where(key => key.StartsWith(prefix))
            .Select(key => key.Substring(prefix.Length))
            .ToList();
    }

    [Obsolete("Temporary endpoint until the asset types are implemented")]
    public MenuMetadata? GetMenu(string name)
    {
        var normalizedName = name.ToLowerInvariant();
        if (_menuCache.TryGetValue(normalizedName, out var cached))
            return cached;

        var asset = Find<MenuAsset>(normalizedName);
        if (asset != null)
        {
            // TODO: MenuManager?
            var menu = MenuMetadata.BuildFromAsset(asset);
            if (menu != null)
                _menuCache[normalizedName] = menu;
            return menu;
        }

        //if (normalizedName.Equals("game-options"))
        //    return new MenuMetadata
        //    {
        //        Music = "Wondering",
        //        Type = "wolf3d-menu",
        //        Position = new Vector2(48, 20),
        //        Indent = 52,
        //        Components = [
        //            new Background("BORDCOLOR"),
        //            new Graphic("c_mouselback", HorizontalOrientation.Center, 184),
        //        ],
        //        MenuItems = [
        //            new ToggleMenuItem("Unlimited Pushwall Limit", true, false),
        //            new ToggleMenuItem("Fake Hitler Fireballs", true, false),
        //            new ToggleMenuItem("Weapon Pickup Progression", true, false),
        //        ]
        //    };

        return null;
    }

    /// <summary>
    /// Text for a language: language/{lang} shared by every game pack, then the running
    /// pack's gamepacks/{pack}/language/{lang}, whose strings win
    /// </summary>
    [Obsolete]
    public LanguageMetadata? GetText(string language)
    {
        var normalizedName = language.ToLowerInvariant();
        if (_languageCache.TryGetValue(normalizedName, out var cached))
            return cached;

        LanguageMetadata? metadata = null;
        foreach (var assetName in new[] { $"language/{normalizedName}", $"{_gamePackId}/language/{normalizedName}" })
        {
            var asset = Find<LanguageAsset>(assetName);
            if (asset == null)
                continue;

            metadata ??= new LanguageMetadata();
            foreach (var (key, text) in asset.Strings)
                metadata.TextStrings[key] = text;
        }

        _languageCache[normalizedName] = metadata;
        return metadata;
    }

    [Obsolete]
    public ActorMetadata GetActorMetadata()
    {
        try
        {
            var actors = Find<ActorTranslationAsset>("wolf3d/actordefs");
            var data = new ActorMetadata();
            if (actors != null)
                data.AddActors(actors.Actors);

            return data;
        }
        catch (Exception e)
        {
            Console.Write(e);
            throw;
        }
    }

    [Obsolete]
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
    private static string GetKey(string assetName, string assetType)
        => $"{assetType}:{assetName}".ToLowerInvariant();
}
