using System.Text;
using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Enums;

namespace PFWolf.Loaders.Uwmf;

/// <summary>
/// Writes a level as a TEXTMAP (UWMF), through the game's mapdefs: each wall and door code its
/// tile entry (textures, a door's slab and Door_Open trigger), each floor code a zone, each flat
/// plane value a sector, each object-plane code a thing (by class) or a trigger (pushwalls,
/// the victory tile, PFWolf's own), elevator switches Exit_Normal / Exit_Secret triggers. Every
/// entry carries the mapdefs code it came from (id), so UwmfTranslator reads the level back to
/// the same planes. A level using only what ECWolf has is written in its "Wolf3D" namespace with
/// ECWolf's class names; one with PFWolf's own (heights, patches, diagonals, light zones, ...)
/// in the "PFWolf" namespace with PFWolf's.
/// </summary>
public static class UwmfWriter
{
    /// <summary>The level as TEXTMAP text; what can't be written is left out and warned about once each</summary>
    public static string Write(MapAsset map, MapObjectTranslationAsset gameDefs, Action<string>? warn = null)
        => new Writer(map, gameDefs, warn ?? (_ => { })).Run();

    private sealed class Writer(MapAsset map, MapObjectTranslationAsset gameDefs, Action<string> warn)
    {
        private readonly MapObjectTranslationAsset _defs = gameDefs.With(map.LocalDefs);
        private readonly int _width = map.Width, _height = map.Height;
        private readonly HashSet<string> _warned = [];
        private readonly List<UwmfBlock> _tiles = [], _sectors = [], _zones = [], _things = [], _triggers = [];
        private readonly Dictionary<string, int> _tileIndex = [], _sectorIndex = [];
        private readonly Dictionary<int, int> _zoneIndex = [];
        private UwmfSpot[] _spots = [];
        private bool _pfwolf;

        private void Warn(string message)
        {
            if (_warned.Add(message))
                warn(message);
        }

        private int Plane(int plane, int x, int y) => plane < map.MapData.Length ? map.MapData[plane][y * _width + x] : 0;
        private bool InMap(int x, int y) => x >= 0 && y >= 0 && x < _width && y < _height;
        private bool IsGameDef<T>(Dictionary<int, T> game, Dictionary<int, T>? local, int code) => game.ContainsKey(code) && local?.ContainsKey(code) != true;

        public string Run()
        {
            _spots = new UwmfSpot[_width * _height];
            WriteZones();
            for (int y = 0; y < _height; y++)
                for (int x = 0; x < _width; x++)
                    _spots[y * _width + x] = Spot(x, y);
            for (int y = 0; y < _height; y++)
                for (int x = 0; x < _width; x++)
                    WriteObject(x, y);
            foreach (var thing in map.ExtraThings)
                _things.Add(ThingBlock(thing.Class, thing.X, thing.Y, thing.Angle, thing.Patrol, thing.Ambush, thing.Skills, id: null));

            return Text();
        }

        // ---- zones: one per area, in order, so each one's code is its place; other floor codes by id

        private void WriteZones()
        {
            var floors = _defs.Floors;
            int areaStart = floors.AreaStart ?? -1, areaCount = floors.AreaCount ?? 0;
            var codes = new SortedSet<int>();
            for (int y = 0; y < _height; y++)
                for (int x = 0; x < _width; x++)
                    if (!IsSolid(Plane(0, x, y) & MapConstants.Plane0CodeMask))
                        codes.Add(Plane(0, x, y) & MapConstants.Plane0CodeMask);

            bool IsArea(int code) => areaStart >= 0 && code >= areaStart && code < areaStart + areaCount;
            int lastArea = codes.Where(IsArea).Select(code => code - areaStart).DefaultIfEmpty(-1).Max();
            for (int i = 0; i <= lastArea; i++)
            {
                _zoneIndex[areaStart + i] = _zones.Count;
                _zones.Add(new UwmfBlock());
            }
            foreach (var code in codes.Where(code => !IsArea(code)))
            {
                _zoneIndex[code] = _zones.Count;
                var zone = new UwmfBlock();
                zone.Add("id", code);
                _zones.Add(zone);
            }
        }

        private bool IsSolid(int code) =>
            _defs.Doors.ContainsKey(code) || (code is > 0 and <= MapConstants.MaxWallId && _defs.Walls.ContainsKey(code));

        // ---- the planemap

        private UwmfSpot Spot(int x, int y)
        {
            int value = Plane(0, x, y), code = value & MapConstants.Plane0CodeMask, patch = value >> MapConstants.PatchShift;
            int height = Plane(MapConstants.HeightPlane, x, y), tag = Plane(MapConstants.TagPlane, x, y);
            int tile = -1, zone = -1;

            if (_defs.Doors.TryGetValue(code, out var door))
            {
                tile = DoorTile(code, door, height);
                DoorTrigger(x, y, door);
            }
            else if (IsSolid(code))
            {
                var diagonal = _defs.Diagonals.TryGetValue(Plane(1, x, y), out var marker) ? marker.Shape : WallShape.Square;
                tile = WallTile(code, patch, height, diagonal);
                ExitTriggers(x, y, _defs.Walls[code]);
            }
            else
                zone = _zoneIndex[code];

            int sector = Sector(Plane(MapConstants.FlatPlane, x, y), Plane(MapConstants.ZonePlane, x, y), tile < 0 ? height : 0);
            return new UwmfSpot(tile, sector, zone, tag);
        }

        private static readonly string[] Faces = UwmfTranslator.Faces;

        private void AddTextures(UwmfBlock block, MapTextureTranslation textures, bool wall)
        {
            foreach (var face in Faces)
                block.Add("texture" + face, wall ? textures.WallFace(face) : textures.Side(face));
        }

        private int DoorTile(int code, MapTextureTranslation door, int height)
        {
            var key = $"door|{code}|{height}";
            if (_tileIndex.TryGetValue(key, out var index))
                return index;

            var block = new UwmfBlock();
            AddTextures(block, door, wall: false);
            block.Add(door.Vertical ? "offsetvertical" : "offsethorizontal", true);
            if (IsGameDef(gameDefs.Doors, map.LocalDefs?.Doors, code))
                block.Add("id", code);
            if (height != 0)
            {
                block.Add("height", height);
                _pfwolf = true;
            }
            if (door.Locks.Count > 0 || door.Split || door.OpensFrom.Length > 0 || door.Locked != null || door.TakesKey || door.ShootThrough)
                _pfwolf = true;      // only its id says all of it
            _tiles.Add(block);
            return _tileIndex[key] = _tiles.Count - 1;
        }

        private void DoorTrigger(int x, int y, MapTextureTranslation door)
        {
            var trigger = TriggerAt(x, y, "Door_Open");
            trigger.Add("arg1", 16);
            trigger.Add("arg2", 300);
            if (door.Lock.Length > 0)
            {
                var number = _defs.Uwmf.Locks.Where(l => l.Value.Equals(door.Lock, StringComparison.OrdinalIgnoreCase)).Select(l => l.Key).FirstOrDefault();
                if (number == 0)
                {
                    Warn($"doors locked by {door.Lock} have no number in the mapdefs uwmf locks; ECWolf would open them freely");
                    _pfwolf = true;
                }
                else
                    trigger.Add("arg3", number);
            }
            if (!door.Vertical)
                trigger.Add("arg4", 1);
            trigger.Add("playeruse", true);
            trigger.Add("monsteruse", true);
            trigger.Add("repeatable", true);
            // A door runs across the way it's passed through
            foreach (var face in door.Vertical ? new[] { "north", "south" } : ["east", "west"])
                trigger.Add("activate" + face, false);
        }

        private UwmfBlock TriggerAt(int x, int y, string action)
        {
            var trigger = new UwmfBlock();
            trigger.Add("x", x);
            trigger.Add("y", y);
            trigger.Add("z", 0);
            trigger.Add("action", action);
            _triggers.Add(trigger);
            return trigger;
        }

        private int WallTile(int code, int patch, int height, WallShape diagonal)
        {
            var key = $"wall|{code}|{patch}|{height}|{diagonal}";
            if (_tileIndex.TryGetValue(key, out var index))
                return index;

            var wall = _defs.Walls[code];
            var block = new UwmfBlock();
            AddTextures(block, wall, wall: true);
            if (IsGameDef(gameDefs.Walls, map.LocalDefs?.Walls, code))
                block.Add("id", code);
            else if (wall.Switch is { } own && !IsExitSwitch(wall))
            {
                // The level's own switch, in full
                _pfwolf = true;
                if (own.To != 0 && _defs.Walls.ContainsKey(own.To))
                    block.Add("switchto", WallTile(own.To, 0, 0, WallShape.Square));
                if (own.Sides.Count > 0)
                    block.Add("switchsides", string.Join(",", own.Sides));
                if (own.Sound.Length > 0)
                    block.Add("switchsound", own.Sound);
                if (own.Lock.Length > 0)
                    block.Add("switchlock", own.Lock);
                if (own.Locks.Count > 0)
                    block.Add("switchlocks", string.Join(",", own.Locks));
                if (own.LocksNeeded > 0)
                    block.Add("switchlocksneeded", own.LocksNeeded);
                if (own.TakesKey)
                    block.Add("switchtakeskey", true);
                if (own.Actions.Count > 0)
                    block.Add("switchactions", string.Join("; ", own.Actions));
            }
            if (wall.Flip.Count > 0)
            {
                block.Add("flip", string.Join(",", wall.Flip));
                _pfwolf = true;
            }
            if (patch != 0)
            {
                block.Add("patch", patch);
                _pfwolf = true;
            }
            if (height != 0)
            {
                block.Add("height", height);
                _pfwolf = true;
            }
            if (diagonal != WallShape.Square)
            {
                block.Add("diagonal", UwmfTranslator.Diagonals.First(d => d.Shape == diagonal).Name);
                _pfwolf = true;
            }
            if (wall.Switch is { } gameSwitch && !IsExitSwitch(wall) && IsGameDef(gameDefs.Walls, map.LocalDefs?.Walls, code))
                _pfwolf = true;      // a switch ECWolf has nothing like: only its id says it's one
            if (wall.Outlet != null)
                _pfwolf = true;

            // The key may have been taken while writing the switch's target
            if (_tileIndex.TryGetValue(key, out index))
                return index;
            _tiles.Add(block);
            return _tileIndex[key] = _tiles.Count - 1;
        }

        private static bool IsExitSwitch(MapTextureTranslation wall) =>
            wall.Switch is { } s && s.Actions.Count == 1 && s.ResetTics == 0 && s.Link.Length == 0 && s.Objective.Length == 0
            && (s.Actions[0].Equals("A_Exit", StringComparison.OrdinalIgnoreCase) || s.Actions[0].Equals("A_SecretExit", StringComparison.OrdinalIgnoreCase));

        // An elevator switch: Exit_Normal from its faces, Exit_Secret from a face before the secret-exit code
        private void ExitTriggers(int x, int y, MapTextureTranslation wall)
        {
            if (!IsExitSwitch(wall))
                return;
            var sides = wall.Switch!.Sides.Count > 0 ? wall.Switch.Sides.Select(s => s.ToLowerInvariant()).ToList() : [.. Faces];
            bool secretAction = wall.Switch.Actions[0].Equals("A_SecretExit", StringComparison.OrdinalIgnoreCase);
            var secretFaces = sides.Where(face =>
            {
                var (dx, dy) = UwmfTranslator.Step(face);
                // As vanilla (and ECWolf's changetrigger): flipped standing on the secret-exit code
                // (Wolf3D's is also the first area's), the switch goes to the secret level
                return secretAction || (InMap(x + dx, y + dy) && _defs.Floors.SecretExit is >= 0 and var secret
                                        && (Plane(0, x + dx, y + dy) & MapConstants.Plane0CodeMask) == secret);
            }).ToList();
            var normalFaces = sides.Except(secretFaces).ToList();

            foreach (var (action, faces) in new[] { ("Exit_Normal", normalFaces), ("Exit_Secret", secretFaces) })
            {
                if (faces.Count == 0)
                    continue;
                var trigger = TriggerAt(x, y, action);
                trigger.Add("playeruse", true);
                foreach (var face in Faces.Except(faces))
                    trigger.Add("activate" + face, false);
            }
        }

        private int Sector(int flats, int lightZone, int archHeight)
        {
            var key = $"{flats}|{lightZone}|{archHeight}";
            if (_sectorIndex.TryGetValue(key, out var index))
                return index;

            var block = new UwmfBlock();
            if (_defs.Flats.Floor.TryGetValue(flats & 0xff, out var floor))
                block.Add("texturefloor", floor);
            if (_defs.Flats.Ceiling.TryGetValue(flats >> 8, out var ceiling))
                block.Add("textureceiling", ceiling);
            if (flats != 0)
                block.Add("flats", flats);
            if (lightZone != 0)
            {
                block.Add("lightzone", lightZone);
                _pfwolf = true;
            }
            if (archHeight != 0)
            {
                block.Add("height", archHeight);
                _pfwolf = true;
            }
            _sectors.Add(block);
            return _sectorIndex[key] = _sectors.Count - 1;
        }

        // ---- the object plane: things, triggers, raw values

        private void WriteObject(int x, int y)
        {
            int code = Plane(1, x, y);
            if (code == 0)
                return;
            int floor = Plane(0, x, y) & MapConstants.Plane0CodeMask;

            if (_defs.Diagonals.ContainsKey(code) && IsSolid(floor) && !_defs.Doors.ContainsKey(floor))
                return;     // the wall's diagonal key
            bool id = true;

            if (_defs.Things.TryGetValue(code, out var thing) && (!IsSolid(floor) || thing.InWall))
            {
                id = IsGameDef(gameDefs.Things, map.LocalDefs?.Things, code);
                int skills = MapThing.AllSkills & ~((1 << Math.Clamp(thing.MinSkill, 0, 4)) - 1);
                if (skills == 0)
                    skills = 1 << 3;       // past the four UWMF skills: the hardest
                bool ambush = _defs.Floors.Ambush is >= 0 and var ambushCode && floor == ambushCode;
                if (thing.InWall || thing.Else.Length > 0 || thing.Faction.Length > 0 || thing.Translation.Length > 0 || thing.MinSkill > 3)
                    _pfwolf = true;
                _things.Add(ThingBlock(thing.Class, x + 0.5, y + 0.5, thing.Angles, thing.Patrol != 0, ambush, skills, id ? code : null));
                return;
            }

            if (_defs.PlayerStarts.TryGetValue(code, out var start) && !IsSolid(floor))
            {
                var type = start.Deathmatch ? "$DeathmatchStart"
                    : start.Team.Equals("red", StringComparison.OrdinalIgnoreCase) ? "$RedTeamStart"
                    : start.Team.Equals("blue", StringComparison.OrdinalIgnoreCase) ? "$BlueTeamStart"
                    : "$Player1Start";
                if (type.Contains("Team"))
                    _pfwolf = true;
                _things.Add(ThingBlock(type, x + 0.5, y + 0.5, start.Angles, false, false, MapThing.AllSkills,
                    IsGameDef(gameDefs.PlayerStarts, map.LocalDefs?.PlayerStarts, code) ? code : null));
                return;
            }

            if (_defs.Triggers.TryGetValue(code, out var trigger))
            {
                WriteTrigger(x, y, code, trigger);
                return;
            }

            // Anything else on the object plane (map info, a value for something to read): as it is
            _pfwolf = true;
            var raw = ThingBlock("$Object", x + 0.5, y + 0.5, 0, false, false, MapThing.AllSkills, id: null);
            raw.Add("value", code);
            _things.Add(raw);
        }

        private void WriteTrigger(int x, int y, int code, MapTriggerTranslation trigger)
        {
            bool gameDef = IsGameDef(gameDefs.Triggers, map.LocalDefs?.Triggers, code);
            UwmfBlock block;
            if (trigger.Action.StartsWith("A_PushWall", StringComparison.OrdinalIgnoreCase) && !trigger.IsWalkOver)
            {
                block = TriggerAt(x, y, "Pushwall_Move");
                block.Add("arg1", 8);
                block.Add("arg2", 2);
                block.Add("arg3", 2);
                block.Add("playeruse", true);
            }
            else if (trigger.Action.StartsWith("A_VictoryTile", StringComparison.OrdinalIgnoreCase) && trigger.IsWalkOver)
            {
                block = TriggerAt(x, y, "Exit_VictorySpin");
                block.Add("playercross", true);
            }
            else
            {
                _pfwolf = true;
                block = TriggerAt(x, y, trigger.Action);
                block.Add(trigger.IsWalkOver ? "playercross" : "playeruse", true);
                if (trigger.Repeat)
                    block.Add("repeatable", true);
            }
            if (trigger.Secret)
                block.Add("secret", true);
            if (gameDef)
                block.Add("id", code);
            if (trigger.Class.Length > 0)
                _pfwolf = true;
        }

        private UwmfBlock ThingBlock(string className, double x, double y, int angle, bool patrol, bool ambush, int skills, int? id)
        {
            var block = new UwmfBlock();
            block.Add("x", x);
            block.Add("y", y);
            block.Add("z", 0.0);
            block.Add("angle", ((angle % 360) + 360) % 360);
            block.Add("type", className);
            if (ambush)
                block.Add("ambush", true);
            if (patrol)
                block.Add("patrol", true);
            for (int s = 0; s < 4; s++)
                block.Add($"skill{s + 1}", (skills & (1 << s)) != 0);
            if (id is { } code)
                block.Add("id", code);
            return block;
        }

        // ---- the text

        private string Text()
        {
            // ECWolf's names unless the level is PFWolf's own
            if (!_pfwolf)
            {
                foreach (var thing in _things)
                {
                    var type = thing.String("type");
                    var ecwolf = _defs.Uwmf.Classes.FirstOrDefault(c => c.Value.Equals(type, StringComparison.OrdinalIgnoreCase)).Key;
                    if (ecwolf != null)
                        thing["type"] = ecwolf;
                }
            }

            var text = new StringBuilder();
            void Line(string line) => text.Append(line).Append('\n');
            Line($"namespace = \"{(_pfwolf ? UwmfTranslator.PfWolfNamespace : "Wolf3D")}\";");
            Line($"name = {new UwmfValue(map.Name.Split('\0')[0]).ToText()};");
            Line("tilesize = 64;");
            Line($"width = {_width};");
            Line($"height = {_height};");
            if (map.Defaults.Sky is { } sky)
                Line($"sky = {new UwmfValue(sky).ToText()};");
            Line("");

            void Blocks(string name, List<UwmfBlock> blocks)
            {
                foreach (var block in blocks)
                {
                    Line(name);
                    Line("{");
                    foreach (var (key, value) in block.Pairs)
                        Line($"\t{key} = {value.ToText()};");
                    Line("}");
                }
            }

            Blocks("tile", _tiles);
            Blocks("sector", _sectors);
            Blocks("zone", _zones);
            Line("plane");
            Line("{");
            Line($"\tdepth = {64 * (map.Defaults.WallStories ?? 1)};");
            Line("}");
            Line("planemap");
            Line("{");
            for (int y = 0; y < _height; y++)
            {
                var row = new StringBuilder("\t");
                for (int x = 0; x < _width; x++)
                {
                    var spot = _spots[y * _width + x];
                    row.Append(spot.Tag != 0 ? $"{{{spot.Tile},{spot.Sector},{spot.Zone},{spot.Tag}}}" : $"{{{spot.Tile},{spot.Sector},{spot.Zone}}}");
                    if (x < _width - 1 || y < _height - 1)
                        row.Append(',');
                }
                Line(row.ToString());
            }
            Line("}");
            Blocks("thing", _things);
            Blocks("trigger", _triggers);
            return text.ToString();
        }
    }
}
