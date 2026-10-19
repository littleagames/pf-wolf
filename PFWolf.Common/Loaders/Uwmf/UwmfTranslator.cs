using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Enums;

namespace PFWolf.Loaders.Uwmf;

/// <summary>
/// Turns a UWMF level into PFWolf's planes, through the running game's mapdefs: a tile entry
/// becomes the mapdefs wall (or door) with its textures, a sector the flats with its textures, a
/// zone an area code, a thing the object-plane code of its class, facing and skills, and the
/// doors', pushwalls' and exits' triggers what the mapdefs make of them. What the mapdefs have
/// nothing like becomes the level's own definitions (MapAsset.LocalDefs) on codes the mapdefs
/// leave free, and things the object plane can't hold go in MapAsset.ExtraThings.
///
/// PFWolf's own data rides on keys ECWolf ignores (namespace "PFWolf"): on a tile, height
/// (stories), patch (id), diagonal (nw, ne, sw, se), flip ("south,west"), the switch keys
/// (switchto: a tile index, switchsides, switchsound, switchactions: "A_Exit; A_OpenDoor()") and
/// id (the mapdefs wall or door code it is); on a sector, lightzone, height (an arch over open
/// floor) and flats (the flat plane's value as is); on a zone, id (the plane 0 code it is); on
/// a thing, id (its object-plane code); on a trigger, id, and action may be a PFWolf trigger
/// action. Thing types $RedTeamStart and $BlueTeamStart are team starts, and $Object (with
/// value) is an object-plane value as is.
/// </summary>
public static class UwmfTranslator
{
    public const string PfWolfNamespace = "PFWolf";

    /// <summary>
    /// The level <paramref name="uwmf"/> describes, through <paramref name="defs"/> (the running
    /// game's mapdefs); what can't be read is left out and <paramref name="warn"/>ed about once
    /// each. Throws InvalidDataException when there's no level to make (no planemap, a bad size).
    /// </summary>
    public static MapAsset ToMapAsset(UwmfMap uwmf, MapObjectTranslationAsset defs, Action<string>? warn = null)
        => new Translation(uwmf, defs, warn ?? (_ => { })).Run();

    /// <summary>ECWolf's action specials by number (lnspecials.h), for maps that give numbers</summary>
    internal static readonly string[] SpecialNames =
        ["", "Door_Open", "Pushwall_Move", "Exit_Normal", "Exit_Secret", "Teleport_NewMap", "Exit_VictorySpin", "Exit_Victory",
         "Trigger_Execute", "StartConversation", "Door_Elevator", "Elevator_SwitchFloor", "Pushwall_MoveNoStop", "Teleport_Relative"];

    internal static readonly string[] Faces = ["north", "south", "east", "west"];

    /// <summary>A face's neighbour: north is up the map (y - 1)</summary>
    internal static (int Dx, int Dy) Step(string face) => face switch
    {
        "north" => (0, -1),
        "south" => (0, 1),
        "east" => (1, 0),
        _ => (-1, 0),
    };

    internal static readonly (string Name, WallShape Shape)[] Diagonals =
        [("nw", WallShape.SolidNW), ("ne", WallShape.SolidNE), ("sw", WallShape.SolidSW), ("se", WallShape.SolidSE)];

    private sealed class Translation(UwmfMap uwmf, MapObjectTranslationAsset defs, Action<string> warn)
    {
        private readonly int _width = uwmf.Width, _height = uwmf.Height;
        private ushort[][] _planes = [];
        private readonly MapObjectTranslationAsset _local = new();
        private readonly List<MapThing> _extra = [];
        private readonly HashSet<string> _warned = [];
        private readonly Dictionary<(int X, int Y), List<UwmfBlock>> _triggersAt = [];
        private readonly HashSet<int> _used0 = [], _used1 = [], _usedFloorFlats = [0], _usedCeilingFlats = [0];
        private readonly Dictionary<string, int> _localWalls = [], _localDoors = [], _localTriggers = [];
        private int[] _zoneCodes = [];
        private int? _areaCount;
        private bool _anyLocal;
        private int _nextObjectCode = 0x6000;

        private bool IsPfWolf => uwmf.Namespace.Equals(PfWolfNamespace, StringComparison.OrdinalIgnoreCase);

        private void Warn(string message)
        {
            if (_warned.Add(message))
                warn(message);
        }

        private int Index(int x, int y) => y * _width + x;
        private bool InMap(int x, int y) => x >= 0 && y >= 0 && x < _width && y < _height;

        public MapAsset Run()
        {
            if (MapConstants.CheckMapSize(_width, _height) is { } badSize)
                throw new InvalidDataException(badSize);
            if (uwmf.PlaneMaps.Count == 0)
                throw new InvalidDataException("it has no planemap");
            if (uwmf.TileSize != 64)
                Warn($"tilesize {uwmf.TileSize} is read as 64 (PFWolf's tiles are 64 units)");
            if (uwmf.PlaneMaps.Count > 1)
                Warn($"it has {uwmf.PlaneMaps.Count} planes; PFWolf plays the first");
            foreach (var block in uwmf.UnknownBlocks.Distinct(StringComparer.OrdinalIgnoreCase))
                Warn($"its {block} blocks are skipped (not part of UWMF as PFWolf reads it)");
            if (!uwmf.Namespace.Equals("Wolf3D", StringComparison.OrdinalIgnoreCase) && !IsPfWolf
                && !uwmf.Namespace.StartsWith("ECWolf", StringComparison.OrdinalIgnoreCase))
                Warn($"namespace \"{uwmf.Namespace}\" isn't Wolf3D, ECWolf's or PFWolf's; it's read as Wolf3D");

            _planes = new ushort[MapConstants.LevelPlanes][];
            for (int p = 0; p < _planes.Length; p++)
                _planes[p] = new ushort[_width * _height];

            NoteUsedCodes();
            GatherTriggers();
            ReadZones();
            ReadSpots(uwmf.PlaneMaps[0]);
            FillUnzonedFloor(uwmf.PlaneMaps[0]);
            ReadThings();
            ReadTriggers();

            // More areas than the mapdefs have, if the level still has the codes past them (an
            // ambush thing's tile, say, gives its zone up for the ambush code)
            if (_areaCount != null && defs.Floors.AreaStart is { } areaStart)
            {
                int used = _planes[0].Select(value => (value & MapConstants.Plane0CodeMask) - areaStart + 1).Where(n => n > 0 && n <= _areaCount).DefaultIfEmpty(0).Max();
                if (used > (defs.Floors.AreaCount ?? 0))
                {
                    _local.Floors.AreaCount = used;
                    _anyLocal = true;
                }
            }

            int? wallStories = null;
            if (uwmf.Planes.Count > 0 && uwmf.Planes[0].Int("depth", 64) is var depth and not 64)
            {
                if (depth > 0 && depth % 64 == 0)
                    wallStories = depth / 64;
                else
                    Warn($"plane depth {depth} isn't a whole number of 64-unit stories; the walls are the level's usual height");
            }

            return new MapAsset
            {
                Name = uwmf.Name,
                Width = (ushort)_width,
                Height = (ushort)_height,
                MapData = _planes,
                Format = MapFormat.Uwmf,
                LocalDefs = _anyLocal ? _local : null,
                ExtraThings = _extra,
                Defaults = new MapLevelDefaults
                {
                    Sky = uwmf.Header.String("sky") is { Length: > 0 } sky ? sky : null,
                    WallStories = wallStories,
                },
            };
        }

        // ---- codes the mapdefs use, so the level's own go elsewhere

        private void NoteUsedCodes()
        {
            _used0.Add(0);
            _used0.UnionWith(defs.Walls.Keys);
            _used0.UnionWith(defs.Doors.Keys);
            var floors = defs.Floors;
            if (floors.AreaStart is { } start)
                for (int i = 0; i < (floors.AreaCount ?? 0); i++)
                    _used0.Add(start + i);
            if (floors.HiddenAreaStart is >= 0 and var hidden)
                for (int i = 0; i < (floors.AreaCount ?? 0); i++)
                    _used0.Add(hidden + i);
            foreach (var code in new[] { floors.Ambush, floors.SecretExit })
                if (code is >= 0)
                    _used0.Add(code.Value);
            _used0.UnionWith(floors.Triggers?.Keys ?? Enumerable.Empty<int>());
            _used0.UnionWith(floors.ActorCodes?.Keys ?? Enumerable.Empty<int>());

            _used1.Add(0);
            _used1.UnionWith(defs.Things.Keys);
            _used1.UnionWith(defs.PlayerStarts.Keys);
            _used1.UnionWith(defs.Triggers.Keys);
            _used1.UnionWith(defs.Diagonals.Keys);
            _used1.UnionWith(defs.DoorLocks.Keys);
            _used1.UnionWith(defs.LinkedThings);

            _usedFloorFlats.UnionWith(defs.Flats.Floor.Keys);
            _usedCeilingFlats.UnionWith(defs.Flats.Ceiling.Keys);
        }

        // A plane 0 code no mapdefs wall, door or floor code has (the highest free first, clear of
        // the area codes counting up)
        private int NewPlane0Code(string what)
        {
            for (int code = MapConstants.MaxWallId; code > 0; code--)
            {
                if (_used0.Add(code))
                    return code;
            }
            Warn($"there are no plane 0 codes left for {what}: it's left out");
            return 0;
        }

        private int NewObjectCode()
        {
            while (_used1.Contains(_nextObjectCode) || defs.MapInfo.ContainsKey(_nextObjectCode >> 8))
                _nextObjectCode++;
            _used1.Add(_nextObjectCode);
            return _nextObjectCode++;
        }

        // ---- triggers by spot

        private void GatherTriggers()
        {
            foreach (var trigger in uwmf.Triggers)
            {
                int x = trigger.Int("x", -1), y = trigger.Int("y", -1);
                if (!InMap(x, y))
                {
                    Warn($"a trigger at {x},{y} (line {trigger.Line}) is off the level and left out");
                    continue;
                }
                if (trigger.Int("z") != 0)
                {
                    Warn($"a trigger on plane {trigger.Int("z")} (line {trigger.Line}) is left out: PFWolf plays the first plane");
                    continue;
                }
                if (!_triggersAt.TryGetValue((x, y), out var list))
                    _triggersAt[(x, y)] = list = [];
                list.Add(trigger);
            }
        }

        private static string ActionOf(UwmfBlock trigger) => trigger["action"]?.Value switch
        {
            string name => name,
            int number when number > 0 && number < SpecialNames.Length => SpecialNames[number],
            int number => number.ToString(),
            _ => "",
        };

        private static bool IsAction(UwmfBlock trigger, params string[] names) =>
            names.Any(name => ActionOf(trigger).Equals(name, StringComparison.OrdinalIgnoreCase));

        private IEnumerable<UwmfBlock> TriggersAt(int x, int y) => _triggersAt.TryGetValue((x, y), out var list) ? list : [];

        // The faces a trigger can be set off from (all four, unless it turns some off)
        private static List<string> ActivatedFaces(UwmfBlock trigger) =>
            Faces.Where(face => trigger.Bool("activate" + face, true)).ToList();

        // ---- zones: areas

        private void ReadZones()
        {
            var floors = defs.Floors;
            int areaStart = floors.AreaStart ?? -1, areaCount = floors.AreaCount ?? 0;
            _zoneCodes = new int[uwmf.Zones.Count];
            for (int i = 0; i < uwmf.Zones.Count; i++)
            {
                var zone = uwmf.Zones[i];
                if (zone.Has("id"))
                {
                    _zoneCodes[i] = zone.Int("id");
                    if (areaStart >= 0 && _zoneCodes[i] >= areaStart + areaCount && _zoneCodes[i] < areaStart + 255
                        && !defs.Walls.ContainsKey(_zoneCodes[i]) && !defs.Doors.ContainsKey(_zoneCodes[i])
                        && _zoneCodes[i] <= MapConstants.Plane0CodeMask && IsNewArea(_zoneCodes[i]))
                        _areaCount = Math.Max(_areaCount ?? areaCount, _zoneCodes[i] - areaStart + 1);
                    _used0.Add(_zoneCodes[i]);
                    continue;
                }

                if (areaStart < 0)
                {
                    _zoneCodes[i] = NewPlane0Code("a zone");
                    continue;
                }

                int code = areaStart + i;
                if (i >= areaCount)
                {
                    // More rooms than the mapdefs have area codes: the level counts on, while codes are free
                    if (code > MapConstants.Plane0CodeMask || defs.Walls.ContainsKey(code) || defs.Doors.ContainsKey(code) || !IsNewArea(code))
                    {
                        Warn($"it has {uwmf.Zones.Count} zones, more than the {code - areaStart} area codes free from {areaStart}; the rest share the last");
                        code = i > 0 ? _zoneCodes[i - 1] : areaStart;
                    }
                    else
                        _areaCount = Math.Max(_areaCount ?? areaCount, i + 1);
                }
                _zoneCodes[i] = code;
                _used0.Add(code);
            }
        }

        // A code past the mapdefs' areas that isn't some other floor code
        private bool IsNewArea(int code)
        {
            var floors = defs.Floors;
            return code != floors.Ambush && code != floors.SecretExit && floors.Triggers?.ContainsKey(code) != true
                && floors.ActorCodes?.ContainsKey(code) != true
                && !(floors.HiddenAreaStart is >= 0 and var hidden && code >= hidden && code < hidden + (floors.AreaCount ?? 0));
        }

        // ---- the planemap

        private void ReadSpots(UwmfSpot[] spots)
        {
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    var spot = spots[Index(x, y)];
                    int i = Index(x, y);

                    if (spot.Tile >= 0 && spot.Tile < uwmf.Tiles.Count)
                        ReadTile(x, y, uwmf.Tiles[spot.Tile], spot.Tile);
                    else
                    {
                        if (spot.Tile >= 0)
                            Warn($"tile {spot.Tile} isn't in its tile list ({uwmf.Tiles.Count} tiles); it's open floor");
                        _planes[0][i] = spot.Zone >= 0 && spot.Zone < _zoneCodes.Length ? (ushort)_zoneCodes[spot.Zone] : (ushort)0xffff;
                        if (spot.Zone >= _zoneCodes.Length)
                            Warn($"zone {spot.Zone} isn't in its zone list ({_zoneCodes.Length} zones)");
                    }

                    if (spot.Sector >= 0 && spot.Sector < uwmf.Sectors.Count)
                        ReadSector(x, y, uwmf.Sectors[spot.Sector], open: spot.Tile < 0);
                    else if (spot.Sector >= 0)
                        Warn($"sector {spot.Sector} isn't in its sector list ({uwmf.Sectors.Count} sectors)");

                    if (spot.Tag is < 0 or > ushort.MaxValue)
                        Warn($"tag {spot.Tag} isn't 0 to 65535; it's left out");
                    else
                        _planes[MapConstants.TagPlane][i] = (ushort)spot.Tag;
                }
            }
        }

        private static string[] FacesOf(UwmfBlock tile) =>
            [tile.String("texturenorth"), tile.String("texturesouth"), tile.String("textureeast"), tile.String("texturewest")];

        private static bool SameFaces(MapTextureTranslation def, string[] faces) =>
            def.North.Equals(faces[0], StringComparison.OrdinalIgnoreCase) && def.South.Equals(faces[1], StringComparison.OrdinalIgnoreCase)
            && def.East.Equals(faces[2], StringComparison.OrdinalIgnoreCase) && def.West.Equals(faces[3], StringComparison.OrdinalIgnoreCase);

        // As a wall shows them (a face it leaves out shows the opposite one)
        private static bool SameWallFaces(MapTextureTranslation def, string[] faces)
        {
            var given = new MapTextureTranslation { North = faces[0], South = faces[1], East = faces[2], West = faces[3] };
            return Faces.All(face => def.WallFace(face).Equals(given.WallFace(face), StringComparison.OrdinalIgnoreCase));
        }

        private static List<string> FlipOf(UwmfBlock tile) =>
            tile.String("flip").Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries).Select(face => face.ToLowerInvariant()).ToList();

        private static bool SameFlip(MapTextureTranslation def, List<string> flip) =>
            def.Flip.Select(face => face.ToLowerInvariant()).Order().SequenceEqual(flip.Order());

        private void ReadTile(int x, int y, UwmfBlock tile, int tileIndex)
        {
            int i = Index(x, y);
            var doorTrigger = TriggersAt(x, y).FirstOrDefault(t => IsAction(t, "Door_Open", "Door_Elevator"));
            bool slab = tile.Bool("offsetvertical") || tile.Bool("offsethorizontal");

            int code = doorTrigger != null || slab ? DoorCode(tile, tileIndex, doorTrigger) : WallCode(x, y, tile, tileIndex);
            if (code == 0)
            {
                _planes[0][i] = 0xffff;     // open floor, filled from a neighbour's zone
                return;
            }

            int patch = tile.Int("patch");
            if (patch is < 0 or > 255)
            {
                Warn($"patch {patch} isn't 1 to 255; it's left out");
                patch = 0;
            }
            _planes[0][i] = (ushort)(code | patch << MapConstants.PatchShift);

            if (tile.Int("height") is var height and > 0)
                _planes[MapConstants.HeightPlane][i] = (ushort)height;

            if (tile.String("diagonal") is { Length: > 0 } diagonal)
            {
                var shape = Diagonals.FirstOrDefault(d => d.Name.Equals(diagonal.Trim(), StringComparison.OrdinalIgnoreCase)).Shape;
                var marker = defs.Diagonals.Where(d => d.Value.Shape == shape && shape != WallShape.Square).Select(d => (int?)d.Key).Min();
                if (marker == null)
                    Warn($"diagonal \"{diagonal}\" has no mapdefs diagonal marker (nw, ne, sw or se); the wall is square");
                else
                    _planes[1][i] = (ushort)marker.Value;
            }
        }

        private int DoorCode(UwmfBlock tile, int tileIndex, UwmfBlock? trigger)
        {
            var faces = FacesOf(tile);
            bool vertical = tile.Bool("offsetvertical") || (!tile.Bool("offsethorizontal") && trigger != null && (trigger.Int("arg4") & 1) == 0);
            int lockNumber = trigger?.Int("arg3") ?? 0;
            var lockClass = lockNumber == 0 ? "" : defs.Uwmf.Locks.GetValueOrDefault(lockNumber, "");
            if (lockNumber != 0 && lockClass.Length == 0)
                Warn($"door lock {lockNumber} isn't in the mapdefs uwmf locks; the door isn't locked");

            if (tile.Has("id") && defs.Doors.ContainsKey(tile.Int("id")))
                return tile.Int("id");

            var key = $"{tileIndex}|{lockClass}|{vertical}";
            if (_localDoors.TryGetValue(key, out var known))
                return known;

            var match = defs.Doors
                .Where(d => SameFaces(d.Value, faces) && d.Value.Vertical == vertical && d.Value.Lock.Equals(lockClass, StringComparison.OrdinalIgnoreCase)
                            && d.Value.Locks.Count == 0)
                .Select(d => (int?)d.Key).Min();
            if (match != null)
                return _localDoors[key] = match.Value;

            int code = NewPlane0Code("a door");
            if (code != 0)
            {
                var sounds = defs.Doors.Values.FirstOrDefault(d => d.OpenSound.Length > 0);
                _local.Doors[code] = new MapTextureTranslation
                {
                    North = faces[0], South = faces[1], East = faces[2], West = faces[3],
                    Vertical = vertical, Lock = lockClass,
                    OpenSound = sounds?.OpenSound ?? "", CloseSound = sounds?.CloseSound ?? "",
                };
                _anyLocal = true;
            }
            return _localDoors[key] = code;
        }

        private int WallCode(int x, int y, UwmfBlock tile, int tileIndex)
        {
            // Exits used from the wall's faces: an elevator switch
            var exits = TriggersAt(x, y).Where(t => IsAction(t, "Exit_Normal", "Exit_Secret") && !t.Bool("playercross")).ToList();
            var normalFaces = exits.Where(t => IsAction(t, "Exit_Normal")).SelectMany(ActivatedFaces).Distinct().ToList();
            var secretFaces = exits.Where(t => IsAction(t, "Exit_Secret")).SelectMany(ActivatedFaces).Distinct().ToList();
            int code = TileWall(tile, tileIndex, normalFaces, secretFaces);

            // The secret way out of an elevator that has both: the floor before those faces is the secret-exit code
            if (normalFaces.Count > 0 || (exits.Count > 0 && code != 0 && defs.Walls.ContainsKey(code)))
                foreach (var face in secretFaces)
                    MarkSecretExit(x, y, face);
            return code;
        }

        private static bool IsExitSwitch(MapTextureTranslation wall) =>
            wall.Switch?.Actions.Any(a => a.StartsWith("A_Exit", StringComparison.OrdinalIgnoreCase)
                                          || a.StartsWith("A_SecretExit", StringComparison.OrdinalIgnoreCase)) == true;

        // The wall code of a tile entry used from these faces as an elevator switch (none: not one):
        // the id it gives, a mapdefs wall with its textures, or the level's own
        private int TileWall(UwmfBlock tile, int tileIndex, List<string> normalFaces, List<string> secretFaces)
        {
            if (tile.Has("id") && defs.Walls.ContainsKey(tile.Int("id")) && tile.Int("id") is > 0 and <= MapConstants.MaxWallId)
                return tile.Int("id");

            var key = $"{tileIndex}|{string.Join(",", normalFaces)}|{string.Join(",", secretFaces)}";
            if (_localWalls.TryGetValue(key, out var known))
                return known;

            bool exit = normalFaces.Count > 0 || secretFaces.Count > 0;
            bool ownSwitch = HasSwitchKeys(tile);
            var faces = FacesOf(tile);
            var flip = FlipOf(tile);
            var candidates = ownSwitch ? [] : defs.Walls
                .Where(w => w.Key is > 0 and <= MapConstants.MaxWallId && SameWallFaces(w.Value, faces) && SameFlip(w.Value, flip))
                .OrderBy(w => w.Key)
                .ToList();
            var match = exit
                ? candidates.Where(w => IsExitSwitch(w.Value)).Select(w => (int?)w.Key).FirstOrDefault()
                : candidates.Where(w => w.Value.Switch == null).Select(w => (int?)w.Key).FirstOrDefault()
                  ?? candidates.Select(w => (int?)w.Key).FirstOrDefault();
            if (match != null)
                return _localWalls[key] = match.Value;

            // The level's own: its code taken first, so a switch to a wall switching back finds it
            int code = _localWalls[key] = NewPlane0Code("a wall");
            if (code == 0)
                return 0;
            var wallSwitch = ownSwitch ? SwitchOf(tile)
                : exit ? new MapSwitchTranslation { Sides = [.. normalFaces.Union(secretFaces)], Actions = [normalFaces.Count > 0 ? "A_Exit" : "A_SecretExit"] }
                : null;
            _local.Walls[code] = new MapTextureTranslation
            {
                North = faces[0], South = faces[1], East = faces[2], West = faces[3], Flip = flip, Switch = wallSwitch,
            };
            _anyLocal = true;
            return code;
        }

        // Pending secret-exit floors, set once the zones are in
        private readonly List<(int X, int Y)> _secretExits = [];

        private void MarkSecretExit(int x, int y, string face)
        {
            var (dx, dy) = Step(face);
            if (defs.Floors.SecretExit is not >= 0)
            {
                Warn("an Exit_Secret beside an Exit_Normal needs the mapdefs floors secret-exit code; it goes the normal way");
                return;
            }
            if (InMap(x + dx, y + dy))
                _secretExits.Add((x + dx, y + dy));
        }

        private static bool HasSwitchKeys(UwmfBlock tile) =>
            tile.Has("switchto") || tile.Has("switchactions") || tile.Has("switchsound") || tile.Has("switchsides") || tile.Has("switchlock");

        // A tile's own switch (the PFWolf switch keys), or null
        private MapSwitchTranslation? SwitchOf(UwmfBlock tile)
        {
            if (!HasSwitchKeys(tile))
                return null;

            int to = 0;
            if (tile.Has("switchto"))
            {
                int target = tile.Int("switchto", -1);
                if (target >= 0 && target < uwmf.Tiles.Count)
                    to = TileWall(uwmf.Tiles[target], target, [], []);
                else
                    Warn($"switchto {target} isn't in its tile list");
            }

            static List<string> Split(string text, char separator) =>
                text.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            return new MapSwitchTranslation
            {
                To = to,
                Sides = Split(tile.String("switchsides").ToLowerInvariant(), ','),
                Sound = tile.String("switchsound"),
                Lock = tile.String("switchlock"),
                Actions = Split(tile.String("switchactions"), ';'),
            };
        }

        private void ReadSector(int x, int y, UwmfBlock sector, bool open)
        {
            int i = Index(x, y);
            if (sector.Has("flats"))
                _planes[MapConstants.FlatPlane][i] = (ushort)sector.Int("flats");
            else
            {
                int floor = FlatIndex(sector.String("texturefloor"), defs.Flats.Floor, _local.Flats.Floor, _usedFloorFlats);
                int ceiling = FlatIndex(sector.String("textureceiling"), defs.Flats.Ceiling, _local.Flats.Ceiling, _usedCeilingFlats);
                _planes[MapConstants.FlatPlane][i] = (ushort)(floor | ceiling << 8);
            }

            if (sector.Int("lightzone") is var zone and > 0)
                _planes[MapConstants.ZonePlane][i] = (ushort)zone;
            if (open && sector.Int("height") is var height and > 0)
                _planes[MapConstants.HeightPlane][i] = (ushort)height;
        }

        // A flat's index in the mapdefs flats table, or the level's own: 0 (the level's default, or
        // its color) for none or a #RRGGBB color
        private int FlatIndex(string texture, Dictionary<int, string> table, Dictionary<int, string> local, HashSet<int> used)
        {
            if (texture.Length == 0 || texture.StartsWith('#') || texture == "-")
                return 0;
            foreach (var (index, name) in table)
                if (name.Equals(texture, StringComparison.OrdinalIgnoreCase))
                    return index;
            foreach (var (index, name) in local)
                if (name.Equals(texture, StringComparison.OrdinalIgnoreCase))
                    return index;

            for (int index = 255; index > 0; index--)
            {
                if (used.Add(index))
                {
                    local[index] = texture;
                    _anyLocal = true;
                    return index;
                }
            }
            Warn($"no flat indices are left for {texture}; the floor or ceiling shows its color");
            return 0;
        }

        // Open floor with no zone takes a neighbour's area (as ECWolf's fillzone), else the first
        private void FillUnzonedFloor(UwmfSpot[] spots)
        {
            int fallback = defs.Floors.AreaStart ?? 0;
            bool IsArea(int code) => code != 0xffff && (_zoneCodes.Contains(code) || code == fallback);
            for (int pass = 0; pass < 2; pass++)
            {
                for (int y = 0; y < _height; y++)
                {
                    for (int x = 0; x < _width; x++)
                    {
                        int i = Index(x, y);
                        if (_planes[0][i] != 0xffff)
                            continue;
                        foreach (var (dx, dy) in new[] { (1, 0), (0, -1), (-1, 0), (0, 1) })
                        {
                            if (InMap(x + dx, y + dy) && spots[Index(x + dx, y + dy)].Tile < 0 && IsArea(_planes[0][Index(x + dx, y + dy)]))
                            {
                                _planes[0][i] = _planes[0][Index(x + dx, y + dy)];
                                break;
                            }
                        }
                        if (pass == 1 && _planes[0][i] == 0xffff)
                            _planes[0][i] = (ushort)fallback;
                    }
                }
            }

            foreach (var (x, y) in _secretExits)
                if (spots[Index(x, y)].Tile < 0)
                    _planes[0][Index(x, y)] = (ushort)defs.Floors.SecretExit!.Value;
        }

        // ---- things

        private void ReadThings()
        {
            foreach (var thing in uwmf.Things)
            {
                if (!thing.Has("x") || !thing.Has("y"))
                {
                    Warn($"a thing with no x or y (line {thing.Line}) is left out");
                    continue;
                }
                double x = thing.Double("x"), y = thing.Double("y");
                int tx = (int)Math.Floor(x), ty = (int)Math.Floor(y);
                if (!InMap(tx, ty))
                {
                    Warn($"a thing at {x},{y} (line {thing.Line}) is off the level and left out");
                    continue;
                }
                if (thing.Int("z") != 0 || thing.Double("z") != 0)
                    Warn("things' z is left out (they stand on the floor)");

                int angle = ((thing.Int("angle") % 360) + 360) % 360;
                int skills = 0;
                for (int s = 0; s < 4; s++)
                    if (thing.Bool($"skill{s + 1}"))
                        skills |= 1 << s;
                if (!Enumerable.Range(1, 4).Any(s => thing.Has($"skill{s}")))
                {
                    skills = MapThing.AllSkills;
                    Warn("things with no skill1-skill4 are placed on every skill");
                }
                if (skills == 0)
                    continue;       // on no skill at all: never placed

                bool ambush = thing.Bool("ambush"), patrol = thing.Bool("patrol");
                bool centered = Math.Abs(x - tx - 0.5) < 0.01 && Math.Abs(y - ty - 0.5) < 0.01;

                string type = thing["type"]?.Value switch
                {
                    string name => name,
                    int number when defs.Things.TryGetValue(number, out var byCode) => byCode.Class,
                    int number => $"#{number}",
                    _ => "",
                };
                if (type.Length == 0 || type.StartsWith('#'))
                {
                    Warn($"thing type {(type.Length == 0 ? "(none)" : type[1..])} isn't a class (line {thing.Line}); it's left out");
                    continue;
                }

                if (type.StartsWith('$'))
                {
                    ReadSpecialThing(thing, type, tx, ty, angle);
                    continue;
                }

                // ECWolf's names, but PFWolf's own in a PFWolf level
                var className = IsPfWolf ? type : defs.Uwmf.ClassFor(type);
                int code = ThingCode(thing, className, angle, patrol, skills);
                int i = Index(tx, ty);
                if (code != 0 && centered && _planes[1][i] == 0)
                {
                    _planes[1][i] = (ushort)code;
                    if (ambush)
                        MarkAmbush(tx, ty);
                }
                else
                    _extra.Add(new MapThing(className, x, y, angle, ambush, patrol, skills));
            }
        }

        // Its object-plane code: the id it gives, or a mapdefs thing of its class, patrol and
        // skills facing its way (any way, for a class placed facing one way only); 0 for none
        private int ThingCode(UwmfBlock thing, string className, int angle, bool patrol, int skills)
        {
            if (thing.Has("id") && defs.Things.TryGetValue(thing.Int("id"), out var given)
                && given.Class.Equals(className, StringComparison.OrdinalIgnoreCase))
                return thing.Int("id");

            // Skills min-skill can say: from one skill up to the hardest
            int minSkill = Enumerable.Range(0, 4).First(s => (skills & (1 << s)) != 0);
            if (skills != (MapThing.AllSkills & ~((1 << minSkill) - 1)))
                return 0;

            var candidates = defs.Things
                .Where(t => t.Value.Class.Equals(className, StringComparison.OrdinalIgnoreCase) && (t.Value.Patrol != 0) == patrol
                            && t.Value.MinSkill == minSkill && t.Value.Else.Length == 0 && t.Value.Faction.Length == 0 && t.Value.Translation.Length == 0)
                .OrderBy(t => t.Key)
                .ToList();
            var exact = candidates.FirstOrDefault(t => ((t.Value.Angles % 360) + 360) % 360 == angle);
            if (exact.Value != null)
                return exact.Key;
            // A class the mapdefs place one way only (decorations, bosses) doesn't care which way it faces
            if (candidates.Count > 0 && candidates.Select(t => t.Value.Angles).Distinct().Count() == 1)
                return candidates[0].Key;
            return 0;
        }

        private void ReadSpecialThing(UwmfBlock thing, string type, int tx, int ty, int angle)
        {
            int i = Index(tx, ty);
            int code;
            switch (type.ToLowerInvariant())
            {
                case "$player1start":
                    code = StartCode(s => !s.Deathmatch && s.Team.Length == 0, angle);
                    break;
                case "$deathmatchstart":
                    code = StartCode(s => s.Deathmatch, angle);
                    break;
                case "$redteamstart":
                    code = StartCode(s => s.Team.Equals("red", StringComparison.OrdinalIgnoreCase), angle);
                    break;
                case "$blueteamstart":
                    code = StartCode(s => s.Team.Equals("blue", StringComparison.OrdinalIgnoreCase), angle);
                    break;
                case "$object":
                    code = thing.Int("value");
                    break;
                default:
                    if (type.StartsWith("$Player", StringComparison.OrdinalIgnoreCase))
                        Warn($"{type} is left out: PFWolf has one player start (co-op players start beside it)");
                    else
                        Warn($"thing type {type} isn't one PFWolf knows; it's left out");
                    return;
            }

            if (thing.Has("id") && type.StartsWith("$Player1", StringComparison.OrdinalIgnoreCase) && defs.PlayerStarts.ContainsKey(thing.Int("id")))
                code = thing.Int("id");
            if (code == 0)
            {
                Warn(type.Equals("$Object", StringComparison.OrdinalIgnoreCase)
                    ? "an $Object with no value is left out"
                    : $"the mapdefs have no player start for {type}; it's left out");
                return;
            }
            if (_planes[1][i] != 0)
            {
                Warn($"{type} at {tx},{ty} shares its tile with another thing and is left out");
                return;
            }
            _planes[1][i] = (ushort)code;
        }

        private int StartCode(Func<MapPlayerStartTranslation, bool> kind, int angle)
        {
            var starts = defs.PlayerStarts.Where(s => kind(s.Value)).OrderBy(s => s.Key).ToList();
            return starts.Where(s => ((s.Value.Angles % 360) + 360) % 360 == angle).Select(s => s.Key)
                .Concat(starts.Select(s => s.Key)).FirstOrDefault();
        }

        // An enemy waiting deaf until it sees the player: on the ambush floor code
        private void MarkAmbush(int x, int y)
        {
            if (defs.Floors.Ambush is not >= 0)
            {
                Warn("ambush things need the mapdefs floors ambush code; they don't wait in ambush");
                return;
            }
            int i = Index(x, y);
            if (!defs.Walls.ContainsKey(_planes[0][i] & MapConstants.Plane0CodeMask) && !defs.Doors.ContainsKey(_planes[0][i]))
                _planes[0][i] = (ushort)defs.Floors.Ambush.Value;
        }

        // ---- triggers that aren't doors or elevator switches

        private void ReadTriggers()
        {
            foreach (var ((x, y), triggers) in _triggersAt)
            {
                foreach (var trigger in triggers)
                {
                    var action = ActionOf(trigger);
                    bool onTile = uwmf.PlaneMaps[0][Index(x, y)].Tile >= 0;
                    if (IsAction(trigger, "Door_Open", "Door_Elevator"))
                    {
                        if (!onTile)
                            Warn($"a {action} trigger at {x},{y} isn't on a tile; it's left out");
                        else if (IsAction(trigger, "Door_Elevator"))
                            Warn("Door_Elevator is read as Door_Open (PFWolf's elevators are switches)");
                        continue;
                    }
                    if (IsAction(trigger, "Exit_Normal", "Exit_Secret") && onTile && !trigger.Bool("playercross"))
                        continue;       // the elevator switch: in the wall's own code

                    int code = TriggerCode(trigger, action, onTile);
                    if (code == 0)
                        continue;
                    int i = Index(x, y);
                    if (_planes[1][i] != 0 && _planes[1][i] != code)
                    {
                        Warn($"a {action} trigger at {x},{y} shares its tile with a thing and is left out");
                        continue;
                    }
                    _planes[1][i] = (ushort)code;
                }
            }
        }

        private int TriggerCode(UwmfBlock trigger, string action, bool onTile)
        {
            if (trigger.Has("id") && defs.Triggers.ContainsKey(trigger.Int("id")))
                return trigger.Int("id");

            bool secret = trigger.Bool("secret"), walk = trigger.Bool("playercross") || !onTile;
            string? pfwolfAction = null;
            int Mapdefs(string prefix, bool? isSecret = null) => defs.Triggers
                .Where(t => t.Value.Action.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && (isSecret == null || t.Value.Secret == isSecret))
                .Select(t => t.Key).DefaultIfEmpty(0).Min();

            switch (action)
            {
                case var _ when action.Equals("Pushwall_Move", StringComparison.OrdinalIgnoreCase)
                                || action.Equals("Pushwall_MoveNoStop", StringComparison.OrdinalIgnoreCase):
                    if (!onTile)
                    {
                        Warn("a Pushwall_Move trigger on open floor is left out");
                        return 0;
                    }
                    if (Mapdefs("A_PushWall", secret) is var pushwall and not 0)
                        return pushwall;
                    pfwolfAction = "A_PushWall";
                    walk = false;
                    break;
                case var _ when action.Equals("Exit_VictorySpin", StringComparison.OrdinalIgnoreCase)
                                || action.Equals("Exit_Victory", StringComparison.OrdinalIgnoreCase):
                    if (Mapdefs("A_VictoryTile") is var victory and not 0)
                        return victory;
                    pfwolfAction = "A_Exit";
                    break;
                case var _ when action.Equals("Exit_Normal", StringComparison.OrdinalIgnoreCase):
                    pfwolfAction = "A_Exit";
                    break;
                case var _ when action.Equals("Exit_Secret", StringComparison.OrdinalIgnoreCase):
                    pfwolfAction = "A_SecretExit";
                    break;
                case var _ when action.StartsWith("A_", StringComparison.Ordinal):
                    pfwolfAction = action;
                    walk = trigger.Bool("playercross");
                    break;
                default:
                    Warn($"trigger action {(action.Length == 0 ? "(none)" : action)} isn't one PFWolf plays; it's left out");
                    return 0;
            }

            var translation = new MapTriggerTranslation
            {
                Action = pfwolfAction, Activation = walk ? "walk" : "use", Secret = secret, Repeat = trigger.Bool("repeatable"),
            };
            var key = $"{translation.Action}|{translation.Activation}|{translation.Secret}|{translation.Repeat}";
            if (_localTriggers.TryGetValue(key, out var known))
                return known;
            int code = NewObjectCode();
            _local.Triggers[code] = translation;
            _anyLocal = true;
            return _localTriggers[key] = code;
        }
    }
}
