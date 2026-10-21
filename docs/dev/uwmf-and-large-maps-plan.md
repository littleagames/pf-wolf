# UWMF and large maps

Two things that belong together: levels bigger than 64x64, and ECWolf's text map format, UWMF
(Universal Wolfenstein Map Format: a TEXTMAP lump in maps/NAME.wad). UWMF was put off when maps
in pk3s went in (2026-09-29), because PFWolf has more per-tile data than ECWolf: flats (plane 2),
tall-wall heights (3), tags (4), light zones (5), wall patches (plane 0's high byte) and wall
ids up to 255. This plan reads UWMF into those same planes, adds the PFWolf-only data to it in a
way ECWolf still loads, and frees the map size first, since a UWMF map says its own.

Nothing here changes how the stock 64x64 levels play: `demotest` stays identical after every
phase.

## How maps load today

- **Assets.** `MapAsset` (PFWolf.Common/Assets/MapAsset.cs) is Width, Height, Name and the planes
  (`ushort[][]`, row by row, y * Width + x). Every loaded level has six (`MapConstants.LevelPlanes`):
  walls, objects, flats, heights, tags, light zones; plane 0's high byte is the wall patch.
- **Loaders** (PFWolf.Common/Loaders): `Wolf3dMapFileLoader` (GAMEMAPS/MAPHEAD, the game's own
  pair or one under maps/; throws unless 64x64), `EcWolfMapLoader` (maps/NAME.wad: a marker lump
  then PLANES, WDC3.1 via `WdcMapFile`; throws unless 64x64, and throws `NotSupportedException`
  on a TEXTMAP lump), `WdcMapFile` (WDC .map files, also used by the editor's import).
- **MapManager** (PFWolf/Managers/MapManager.cs). `LoadMap` clones the planes into `mapsegs`,
  splits patches off into `PatchMap`, reads map-info codes, then scans every tile: plane 0
  through the mapdefs walls/doors/floors gives `TileMap` and `ActorAt`, plane 1 through mapdefs
  things/player-starts/triggers spawns actors. Then `SealEdge`, `BuildWallShapes` (diagonal
  markers 400-403 on plane 1), `BuildWallHeights` (plane 3), flats (`BuildFlats`, plane 2).
  Tags and zones are read straight off planes 4 and 5 when used.
- **mapdefs** are per game pack and plane-code based: walls.yaml (id → four face textures,
  flip, switch), doors.yaml (code → faces, vertical, locks, sounds), floors.yaml (area-start,
  area-count, ambush, secret-exit), flats.yaml (index → texture, floor and ceiling), things
  (code → class, angles, patrol, min-skill), player-starts, triggers (code → action, use/walk,
  secret), patches, diagonals, map-info. Per-level properties (sky, wall-height, shading, zones,
  scrollers, default flats, name) live in game-info `maps:`.

## Where 64x64 is assumed

`MapConstants.MapShift = 6`, `MapSize = 64`, `MapArea = 4096` are constants, and `MapManager`
forwards them as statics, so ~75 files use them. By kind:

- **Fixed-size arrays.** MapManager: `TileMap`, `Spotvis`, `Seen`, `ActorAt`, `WallShapes`,
  `StoryMap`, `PatchMap` ([64,64]); `FloorFlat`, `CeilingFlat`, `AnimStart`, `infotiles`
  ([4096]). Lighting.cs `TileLevel`/`TileTint` and the light grid (`GridShift = MapShift + 3`,
  512x512 cells). ActorLights.cs `caststamp`/`visitstamp`. Scrollers.cs six offset arrays.
  AutomapDrawing.cs `automapWalls`, `automapDoorAt`.
- **`<< MapShift` indexing** (y << 6 + x) and `& (MapSize - 1)` / `>> MapShift` decoding:
  `MapSpot`/`SetMapSpot`, AnimStart, texture changes, flats, lighting, scrollers, SkyAndFlats,
  WallPosts, WallHits, Weather, ActorLights, MapManager's area flood fill.
- **Bounds checks against MapSize**: Doors, FloorDamage, HubActions, LevelEntry, Missiles,
  Projectiles, MountedWeapons, PlayerDeath, PlayerMovement, PushWalls, SmartSwitches, Monster,
  BlakeMonster, GameScripts, ConsoleArgs, CheatCommands, Radar, ElevatorScreens, AutomapDrawing,
  AutomapManager, DebugScreens. Many already use `MapWidth`/`MapHeight` (LoadMap, SealEdge,
  BuildFlats), which are set from the asset.
- **Blake specifics**: BlakeMonster's `SeekPointX/Y` are the corners of a 64 map (32, 63, 1).
- **Tile coordinates are bytes**: `Actor.TileX/TileY`, `ActorSnapshot`, `VisibleObject`, with
  `(byte)` casts in ~20 places. Fine up to 256 tiles across; positions themselves are 16.16
  fixed ints, good to 32767 tiles.
- **Save games** (SaveFile v27): `WriteLevelState` writes the planes with their lengths, but
  `TileMap`/`ActorAt`, `PatchMap` and `Seen` as 64x64 with no size, and `ReadLevelState` (static)
  reads them back as 64x64. `CheckLevelState` only compares plane lengths.
- **Netplay**: the mid-game join sends the same level body (NetProtocol v11, NetStateVersion 8).
  The lockstep checksum hashes actors only, so it doesn't care.
- **Limits that bigger maps hit sooner**: `MaxDoors` 128, `MaxVisible` 250 sprites drawn, areas
  per game (floors.yaml area-count, Wolf3D 37, byte `AreaNumber`), 255 wall ids (plane 0's low
  byte, `WallShift` 8). `MaxActors`/`MaxStats` are declared but not enforced.
- **Per-frame whole-map loops**: `MarkSeenFromSpotvis` walks every tile each frame, and the
  spotvis clear; 16x more work at 256x256 (65K tiles), still small, but worth bounding to the
  traced area later.
- **Editor** (PFWolf.Editor) is already almost size-free: MapCanvas, LevelMesh, MapTiles, tools,
  checks and the demo overlay use the map's Width/Height. Only `MapFiles.NewMap` (always 64) and
  `MapImport.Check` (rejects non-64) assume it.

## What UWMF is

From ECWolf's parser (ECWolf src/gamemap_uwmf.cpp), the format ECWolf 1.3/1.4 reads. A TEXTMAP
lump between the marker and ENDMAP, `key = value;` assignments and `block { ... }` blocks:

```
namespace = "Wolf3D";            // or "ECWolf-v12" / "ECWolf-v14"; others warn but load
name = "Wolfenstein 3D map";
tilesize = 64;
width = 64;  height = 64;        // must come before the planemap
// ECWolf-v12/v14 only: defaultlightlevel, defaultvisibility, sky, skyhorizonoffset

tile   { texturenorth = "GSTONEA1"; texturesouth = ...; textureeast = ...; texturewest = ...;
         blockingnorth/south/east/west = true; offsetvertical = false; offsethorizontal = false;
         dontoverlay; mapped = 0; soundsequence = "..."; textureoverhead = "..."; }
sector { texturefloor = "#707070"; textureceiling = "#383838"; }
zone   { }                       // sound zones: Wolf3D's areas
plane  { depth = 64; }           // one plane; its depth is the wall height
planemap { {tile, sector, zone}, {tile, sector, zone, tag}, ... }   // width x height, -1 = none
thing  { x = 29.5; y = 57.5; z = 0; angle = 90; type = "Guard";     // x, y in tiles
         ambush = true; patrol = false; skill1 = true; ... skill4 = true; }
trigger { x = 29; y = 57; z = 0; activatenorth/south/east/west = true;
          action = "Door_Open"; arg0..arg4 = 0;
          playeruse = true; playercross = false; monsteruse = false; repeatable = false;
          secret = false; }
```

Blocks are palettes: the planemap holds indices into the tile, sector and zone lists in the
order they appear. A tile index is a wall (`offsetvertical`/`offsethorizontal` make it a door
slab); -1 is open floor. Things are free-standing (any number per tile, fractional positions);
`type` is an actor class name (a number is a deprecated editor number). Doors, pushwalls and
exits are **triggers** with action specials: `Door_Open(tag, speed, delay, lock, type)`,
`Door_Elevator`, `Pushwall_Move(tag, speed, dir, distance)`, `Pushwall_MoveNoStop`,
`Exit_Normal`, `Exit_Secret`, `Exit_Victory`, `Exit_VictorySpin`, `Teleport_NewMap`,
`Teleport_Relative`, `Elevator_SwitchFloor`, `Trigger_Execute`, `StartConversation`.

ECWolf skips keys it doesn't know inside a block (one value token) and skips unknown top-level
blocks **up to the first `}`** (so an unknown block must not nest braces). It errors on a
planemap entry with more than four numbers.

## How UWMF maps onto PFWolf

The engine stays plane-based. A UWMF level is **translated into the six planes on load**, plus
two things planes can't hold: a per-level set of *local definitions* (walls, doors, flats,
triggers the game's mapdefs don't have) and a list of *extra things*. Everything after the
loader (MapManager, renderer, saves, netplay, the editor's plane layers) keeps working on planes.

| UWMF | PFWolf |
|---|---|
| `width`, `height` | MapAsset Width/Height (phase 1 makes any size work) |
| `tilesize` | must be 64; anything else warns and is read as 64 |
| `plane { depth }` | the level's wall height in stories (depth / 64), unless game-info says |
| `name`, `sky`, `defaultlightlevel`, `defaultvisibility` | level defaults under game-info `maps:` (game-info wins) |
| tile entry | a wall id: the mapdefs wall with the same four textures (and flip), else a new local wall id from the codes the mapdefs leave free |
| tile with `offsetvertical`/`offsethorizontal` + a `Door_Open` trigger on it | a door code: the mapdefs door with those textures and lock, else a local door (`vertical` from offsetvertical, lock from arg3 via a lock table) |
| sector entry | flat plane value: floor and ceiling textures → mapdefs flats indices, else local ones (a `#RRGGBB` color → no flat, the color) |
| zone entry | an area code (floors.yaml area-start + n); see the area-count question |
| planemap tag (4th value) | tag plane 4, as is |
| thing | plane 1 code: the mapdefs thing with that class, angle, patrol and skills; things that don't fit (a second on a tile, a skill set min-skill can't say, a class with no code) go to the extra things, spawned by class after the plane scan |
| thing `ambush` | the ambush floor code under it, or the actor's ambush flag for an extra thing |
| `$Player1Start`..`$Player4Start`, `$DeathmatchStart` | player-starts codes (a team start is a PFWolf extension) |
| `Pushwall_Move` on a wall | the pushwall trigger code on plane 1 (mapdefs triggers, A_PushWall), `secret` kept |
| `Exit_Normal`/`Exit_Secret` used on a wall | a wall switch (walls.yaml `switch:` with A_Exit / A_SecretExit), the switched texture taken from a mapdefs wall with the same switch when there is one |
| `Exit_Normal` etc. crossed on floor | a walk trigger (A_Exit; Exit_VictorySpin → A_VictoryTile) |
| other specials | warned and left out in the first pass |

Class names: PFWolf's actordefs mostly use ECWolf's names (Guard, Officer, Mutant, Dog, Hans,
Schabbs, Gretel, PatrolPoint, Chalice, Cross, Crown, ...). The ones that differ (WolfensteinSS →
SS, Hitler → RealHitler, ChestofJewels → ChestOfJewels, Trans → TransGrosse, OneUp → ExtraLife,
...) go in a per-pack alias table, `mapdefs/{pack}/uwmf.yaml`:

```yaml
uwmf:
  classes: { WolfensteinSS: SS, Hitler: RealHitler, ChestofJewels: ChestOfJewels }
  locks: { 1: GoldKey, 2: SilverKey }      # Door_Open's lock argument
```

### PFWolf's own data in UWMF

Namespace `"PFWolf"` (ECWolf warns on it but loads the map). The extra data goes where ECWolf
ignores it, on the palette entries, so a PFWolf level still opens in ECWolf (without the
extras), and nothing needs a new per-tile list:

- **tile**: `height` (stories, plane 3), `patch` (a mapdefs patch id, plane 0 high byte),
  `diagonal = "nw"|"ne"|"sw"|"se"` (the plane 1 marker), `flip = "south,west"`, the switch
  keys (`switchto` = another tile entry's index, `switchsides`, `switchsound`, `switchlock`,
  `switchactions = "A_OpenDoor(); A_Exit()"`) and `id` (the mapdefs wall or door code it is).
  Different heights or patches on the same wall are separate tile entries, as UWMF does for
  anything per-tile.
- **sector**: `lightzone` (plane 5), `height` on open floor (an arch, plane 3), `flats` (the
  flat plane's value as is).
- **zone**: `id` (the plane 0 floor code it is, for codes that aren't areas: ambush, Blake's).
- **thing**: `id` (its object-plane code). Types `$RedTeamStart`, `$BlueTeamStart` (team
  starts) and `$Object` with `value` (an object-plane value as is: map info, Blake's data).
- **trigger**: `id`, and `action` may also be a PFWolf trigger action (`"A_SetZoneLight(3, 64)"`);
  ECWolf warns it can't resolve it and ignores the trigger.
- **planemap tag**: PFWolf's tag plane, unchanged.

ECWolf skips all of these keys quietly, so the writer adds `id` to every entry of a stock level
and still writes it in the `"Wolf3D"` namespace with ECWolf's class names; only a level using
PFWolf's own data is `"PFWolf"` (PFWolf's class names, no aliases).

Writing a stock level and reading it back must give the same planes, code for code, so the
exporter prefers mapdefs entries and the reader looks them up the same way.

## Phases

### 1. Any map size (engine)

Levels of any width and height up to the maximum (see questions), square or not; 64x64 levels
and every demo unchanged.

- **Common** MapConstants: `MapSize`/`MapArea`/`MapShift` become `ClassicMapSize` (64, for
  GAMEMAPS and new levels) and `MaxMapSize`; `EcWolfMapLoader.ToMapAsset` and
  `Wolf3dMapFileLoader` accept any size from 3 to MaxMapSize (planes width x height);
  `WdcMapFile` already reads any size.
- **MapManager**: `MapWidth`/`MapHeight` drive everything: `MapArea` instance property,
  `TileIndex(x, y) => y * MapWidth + x`, `InMap(x, y)`; every array allocated per level in
  `LoadMap`/`RestoreLevelState` at [MapWidth, MapHeight]; `MapSpot`/`SetMapSpot`, AnimStart,
  texture changes, flats, infotiles, `SpawnArea`'s flood fill, `GetSeenBytes`/`SetSeenBytes`
  use it. The static `MapManager.MapSize` forwards go, so every caller is found by the build.
- **Callers** (from the list above): bounds checks → `map.InMap(x, y)` or the map's width and
  height; `(y << MapShift) + x` → `map.TileIndex(x, y)`. Lighting.cs: `TileLevel`/`TileTint` and
  the cell grid sized per level (grid stride = width << 3 instead of `GridShift`), rebuilt in
  `InitLevelShading`; ActorLights stamps likewise; Scrollers' arrays per level; AutomapDrawing's
  arrays per level; DebugScreens zoom from the larger side; BlakeMonster seek points from the
  map's corners (same values on 64x64).
- **Saves v28**: write width and height before the tile grid; `ReadLevelState` reads them, and
  `CheckLevelState` refuses a save whose size doesn't match the map. v27 and older read as 64x64.
  `LevelBodies` reads `Seen` at width x height. NetProtocol 12, NetStateVersion 9.
- **exportmap** writes the real size (it already writes `map.Width/Height`).
- **Tests**: a 100x70 test level built in code (walls round the edge, a door, a guard):
  load, `SealEdge`, walk to the far corner, a door at x 99, flats/lighting/scrollers indices,
  save and load round trip, the automap arrays; existing tests switch from
  `MapManager.MapSize` to the level's size or `ClassicMapSize`.
- **Check**: build, all tests, `demotest` baselines identical, and a 128x128 mod level played
  headless (`--warp` + a scripted demo) and by eye (automap, radar, lighting).

### 2. Large maps in the editor

So bigger levels can be made before UWMF exists.

- New level (`MapFiles.NewMap`): a width and height in the dialog (default 64).
- Resize: Map Properties gets the size, with an anchor (which corner or edge stays), moving
  planes, tags and properties that hold coordinates; one undo step.
- `MapImport.Check` takes any size up to the maximum; importing planes onto a level of another
  size places them at the top left (or refuses, see questions).
- Level checks: warn past the engine's limits (doors > MaxDoors, areas, things per tile).
- Play (`--warp`) needs nothing new.

### 3. Reading UWMF

- **Common/Loaders/Uwmf/**: `UwmfLexer` (identifiers, strings with escapes, ints, floats,
  bools, `//` and `/* */` comments), `UwmfReader` → `UwmfMap` (header values, tile/sector/zone
  palettes, planemap, things, triggers, unknown keys kept per block for round trips). Errors
  carry line numbers.
- **`UwmfMapTranslator`** (Common): `UwmfMap` + the pack's mapdefs + uwmf.yaml →
  MapAsset with the six planes, `LocalDefs` (walls, doors, flats, triggers, things the mapdefs
  don't have, keyed by the codes it gave them) and `ExtraThings` (class, x, y, angle, skills,
  ambush, patrol). Free codes are picked from those no mapdefs wall/door/floor/thing/map-info
  uses; running out warns and drops the rest.
- `EcWolfMapLoader.Load` takes a TEXTMAP lump instead of throwing; `PfWolfPk3Loader` needs no
  change (same maps/NAME.wad path).
- **Engine**: MapManager's `GetMapData()` lays the level's `LocalDefs` over the pack's mapdefs
  for that level (a merged `MapObjectTranslationAsset`, cached per level); `LoadMap` spawns
  `ExtraThings` after the plane scan (at their exact position; skills checked against the
  difficulty); header values feed SetupGameLevel's defaults under game-info. Saves need nothing:
  the planes and actors are saved already, and a restored level reads its local defs from the
  map again.
- Area counts: if a level has more zones than the pack's area-count, its `FloorCodes` gets the
  larger count (and a save checks it per level).
- `mapdefs/wolf3d/uwmf.yaml` and `mapdefs/spear/uwmf.yaml` (aliases, locks); Blake later.
- **Tests**: hand-written TEXTMAPs (every block and key, a door with a lock, a pushwall, an exit
  switch, stacked things, odd skill sets, a 90x40 level, PFWolf extensions, bad input with line
  numbers); ECWolf's own UWMF test maps if any turn up.

### 4. Writing UWMF

- **`UwmfWriter`** (Common): `MapAsset` + mapdefs → TEXTMAP: walls and doors become tile entries
  (one per distinct texture set, height, patch, shape, flip), doors and pushwalls and exit
  switches become triggers, plane 1 things become `thing` blocks by class (ECWolf names through
  the alias table), flats become sectors, areas become zones, tags go in the planemap.
  PFWolf-only data uses the extension keys, and the namespace is `"PFWolf"` only when the level
  uses any.
- `EcWolfMapLoader.SaveUwmf(map, marker)`; `exportmap <map|all> [folder] [uwmf]`.
- **Round trip check**: all 60 WL6 and 21 SOD levels to UWMF and back give identical planes,
  and `demotest` playing the UWMF copies (as a mod) matches the baselines.

### 5. UWMF in the editor

- Open: a UWMF level shows like any other (it's planes after translation); its local walls,
  doors and flats appear in the palette as "this level's" entries.
- Save: keeps the format it was loaded in; Save As offers binary (PLANES) or UWMF; a level with
  local definitions or extra things can only be saved as UWMF (the binary format can't hold
  them), and the editor says so.
- Extra things: placed and moved like things, drawn with a marker, with exact positions.
- Hover text and the Definitions window show local definitions as read-only.

## Status (2026-10-18)

All five phases done on feature/uwmf (uncommitted).

1. **Any map size.** MapConstants `ClassicMapSize`/`MinMapSize` (3)/`MaxMapSize` (256)/
   `CheckMapSize`/`MaxDoors` (256); MapManager `MapArea`, `TileIndex`, `InMap`,
   `TileIndexOrNone`, every per-tile array per level; Lighting `GridWidth`/`GridHeight`/
   `CellIndex`/`SizeToLevel`, ActorLights and Scrollers resize with the level; AutomapDrawing,
   AutomapManager (`Pan` takes the size), Radar, DebugScreens, BlakeMonster seek corners,
   Blake's teleporter overhead (sampled down to its 64x64 picture); `DoorObject.TileX/TileY` are
   bytes now (they were sbyte: doors past x 127 broke). Save v28, NetProtocol 12,
   NetStateVersion 9. GAMEMAPS levels can be other sizes too, though a plane's RLEW length word
   caps them near 181x181.
2. **Editor.** New level asks for a size; Level → Resize level… (size, then an anchor: top-left
   ... bottom-right) as one undo step (`MapDocument.Resize`, `MapResizing`); import of another
   size goes top-left; level check for more than 256 doors.
3. **Reading.** `Loaders/Uwmf/` UwmfReader (lexer, `UwmfMap`, `UwmfBlock`, `UwmfSpot`),
   UwmfTranslator; `maps/NAME.wad` with TEXTMAP and loose `maps/NAME.uwmf`; AssetManager
   translates once the mapdefs are in (`TranslateUwmfLevels`); MapAsset `Format`, `Uwmf`,
   `LocalDefs`, `ExtraThings`, `Defaults`; MapManager reads through `PackMapData().With(LocalDefs)`
   and spawns extra things; LevelSetup takes the file's sky and plane depth under game-info.
   `mapdefs/wolf3d/uwmf.yaml` and `mapdefs/spear/uwmf.yaml` from ECWolf's xlat tables.
4. **Writing.** UwmfWriter, `EcWolfMapLoader.SaveUwmf`, `exportmap <map|all> [folder] uwmf`.
5. **Editor UWMF.** Level → Save as UWMF toggle (new levels are UWMF; a level keeps its format;
   one with its own definitions or extra things is always UWMF); a mod's `maps/NAME.uwmf` stays
   text, and saving one way takes out the other file; MapTiles reads through the level's own
   definitions (palette, checks, 3D view); extra things drawn ringed in blue, and in the hover text.

Checked: every Wolf3D, Spear, Blake Stone and Planet Strike level written to UWMF reads back to
the same planes, with or without the `id` keys (Wolf3D and Spear tested without them too); the
Wolf3D and Spear demos play out as the baselines on UWMF copies of every level; a 128x96 level
(binary, and hand-written UWMF with its own wall, door and an off-centre thing) is played past
tile 64 through a door; `demotest` baselines unchanged. Tests: LargeMapTests, MapResizingTests,
LargeMapGameTests, UwmfReaderTests, UwmfTranslatorTests, UwmfWriterTests, UwmfGameTests,
MapFormatSaveTests.

Editor follow-up (same day): the **Free thing** tool (G) puts the palette's thing (plane 1) down
anywhere as an extra thing, snapped to an eighth of a tile (Shift: the tile's middle); clicking
one picks it up (white ring) to drag; ] [ Shift+H Shift+V turn it, Delete takes it away, Esc
drops a drag. Each change is one undo step (`EditStep.Extras`, `MapDocument.ChangeExtraThings`);
a resize moves them with the level (`MapResizing.ResizedExtras`). The Definitions window has a
read-only "This level's own" tab: the level's own definitions and extra things as mapdefs YAML
(`LevelOwnDefinitions`), kept to the level being edited. The 3D view draws extra things at their
spots (`LevelMesh.AddExtraThing`; a wall sprite stays on its tile). Tests: ExtraThingsEditingTests.

Not done or not checked: the editor's free-thing tool, resize and save-format UI by hand (tested
through the controller and view models only); editing an extra thing's skills, patrol or ambush
(it takes the palette's); a 3D-view click picks an extra thing's tile, not the thing;
Blake Stone's uwmf.yaml (its levels round-trip through ids, but ECWolf-made Blake maps would need
aliases); ECWolf itself reading PFWolf's exports; object-plane values holding coordinates (Blake's tag-links, teleports) aren't moved by a resize.

## Decisions (2026-10-18)

The user took every recommendation below: maps up to 256x256 (byte tile coordinates stay),
non-square allowed, UWMF translated into the planes, PFWolf extras as extra keys on tile and
sector entries in the `"PFWolf"` namespace, per-level area counts past floors.yaml's, MaxDoors
256, loose `maps/NAME.uwmf` files as well as `.wad`s, UWMF the editor's default format and
PFWolf's own map format, unsupported ECWolf specials warned and left out, editor resizing
(phase 2) before UWMF.

## Open questions (answered, see Decisions)

1. **Largest map.** 256x256 keeps byte tile coordinates (no actor or save change for them).
   Bigger means ushort `TileX/TileY` everywhere (saves, snapshots, ~20 casts). Recommend 256.
2. **Non-square maps**: allow them (UWMF and WDC both do)? Recommend yes.
3. **Translate into planes** (this plan) rather than making the engine tile-object based like
   ECWolf's? Translating keeps saves, netplay, the editor and demos as they are; the cost is the
   255 wall-id and per-tile-one-thing limits, handled by local defs and extra things.
4. **Extension style**: extra keys on tile/sector palette entries in a `"PFWolf"` namespace
   (ECWolf still loads the level), or flat per-tile lists (`heightmap { ... }`), or a separate
   sidecar file?
5. **Area counts**: let a level have more areas than floors.yaml says (zones > 37), as above?
6. **Doors**: raise `MaxDoors` from 128 to 256 for big maps (the tile encoding allows up to 511)?
7. **Where UWMF lives**: only maps/NAME.wad with a TEXTMAP lump (ECWolf's way), or also a loose
   `maps/NAME.uwmf` text file, easier to edit by hand and diff?
8. **Editor default**: keep saving new levels as binary PLANES, or make UWMF the default
   (readable, diffs well, holds everything)? And should the "PFWolf-native format" idea simply
   be UWMF in the PFWolf namespace?
9. **ECWolf specials** beyond doors, pushwalls and exits (Teleport_*, Elevator_SwitchFloor,
   Trigger_Execute, StartConversation): leave out with a warning for now, or map some (teleports
   to PFWolf's hub actions)?
10. **Phase order**: editor resize (phase 2) before UWMF reading, as above, or after?
