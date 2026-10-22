# Blake Stone editor plan: editing Aliens of Gold and Planet Strike levels

## Status (2026-10-22): plan only

Nothing is built. The open questions are listed below.

Goal: the editor opens Blake Stone: Aliens of Gold (`blake`) and Planet Strike (`planetstrike`)
the way it opens Wolf3D and Spear today, and a modder can edit their levels in full: map-info
codes, barrier and smart-switch links, transporters and warps, hub floors. The levels are saved
as `maps/MAPnn.wad` in a mod and played with F5. As with Wolf3D, the editor never writes MAPTEMP.

## Open questions

- **Level names in the list.** "Mission 2 · Floor 5  TECHNICAL PLANNING OFFICES" from episodes +
  `floor-number`, or game-info's name alone as now? The unused slots (four per mission) could be
  hidden or shown dimmed.
- **Map-info code editing UI.** One "Level codes" panel listing every code on the level, or
  per-tile editing from the properties of the hovered/selected tile, or both?
- **Where new map-info codes go.** Blake puts them in the object plane's top row. The editor
  could place new ones there by itself (first free pair), or let the modder place them by hand.
- **Fidelity of the 3D view.** Match the game's shading exactly, or show flat lighting with the
  right colors and flats?

## Where things stand

- **Loading is generic.** `GameContent.Load` (PFWolf.Editor/Data/GameContent.cs) goes through
  `AssetManager`, which already reads Blake's data files: RLEW-only MAPTEMP
  (Wolf3dMapFileLoader), VGAGRAPH with `font-count: 5`, VSWAP, AUDIOT and the JAM movies.
- **The game list is hard-coded.** `MainWindowViewModel.GameChoices` lists the Wolf3D and Spear
  packs only, with a comment saying Blake comes later. Games added by mods are appended.
- **Plane 0 is data-driven.** `MapTiles.FloorCode` reads floors.yaml: areas 108–152, hidden
  areas from 162, ambush 106, triggers 157–160, and Planet Strike's `actor-codes` 72–80. Triggers
  show as "trigger A_SmartSwitch(...)", and actor codes only as "enemy code".
- **Map-info codes are recognised but not editable.** `MapTiles.ObjectKnown` treats a code
  (mapdefs/blake/mapinfo.yaml, keyed by the high byte) and the value tile east of it as known.
  `TileRemap` moves the pair together when rotating or flipping. The value itself can't be read or
  set except as a raw number, and nothing stops a paint stroke from overwriting the value tile.
- **The 3D view uses game-info colors only.** `LevelMesh` takes `MapTiles.FloorColor` /
  `CeilingColor` (game-info or default-map), not the level's FE code, and flats only from plane
  2, not the level's FB code. Doors are drawn whole, not split.
- **Level properties** (MapProperties / GameInfoFile) don't carry `floor-number` or `cluster`.
  NewMod.cs writes a `floor-number` only into its template.
- **The text browser** already knows presenter scripts (`TextCatalog.IsBlake`,
  `TextFormat.Presenter`). The palette and art browsers are generic.
- **Links the engine makes from level data** that the editor doesn't draw:
  - F8xx barrier switch → barrier at the x,y in the next tile (`tag-link`), on floor xx.
  - Planet Strike barrier switches link by their own object value x<<8|y (walls.yaml
    `link: object-plane`).
  - Smart switch floor codes 158–160 act on the tile their object-plane value names.
  - F5 warps go to the x,y in the next tile, and F4xx transporters go to floor xx.
  - Floor code 79 (Planet Strike) links an enemy to the one east of it.

## Phases

### Phase 1: open Blake, read-only (small)

- Add `blake` and `planetstrike` to `GameChoices` and check that `GameLaunch` passes the right
  `--game` id.
- Load both games and fix what goes wrong: warnings in the problems list, sprites the palette
  can't find (actordefs classes with Blake's sprite names), music names, the Blake palette, and
  palette entries from mapdefs/blake and mapdefs/planetstrike.
- Level list: missions in order from episodes, with the open question above settled.
- Hover text: name each map-info code and its value ("F8: barrier switch → barrier at 31,52 on
  floor 3", "FE: ceiling 0x2F, floor 0x19"), the floor triggers ("smart switch: toggles 22,14"),
  and actor codes by what they do ("drops a red card", "cloaked").
- Tests (GameData category): every AOG and Planet Strike level loads, and round-trips through
  `.wad` identically (MapRoundTripTests); `LevelChecks` finds no errors on stock levels.

### Phase 2: map-info codes and links (medium, the main work)

- Palette entries for each map-info kind in mapinfo.yaml. Placing one asks for its value: a
  floor number, a hint number (with the hint's text shown), two colors from the palette, two flat
  indices, or a target tile picked on the map.
- A way to edit a placed code's value, per the open question (panel or per-tile).
- Protect the pair: paint, erase, fill, scatter and stamp leave the value tile of a map-info code
  alone (or move the pair, for selections), and deleting a code clears its value tile too.
- Draw links like the switch → tag lines: barrier switch → barrier (both kinds), smart switch →
  target, warp → target, linked enemies (79). A "pick target" mode sets a link by clicking the
  target tile.
- Floor-plane codes: palette entries with readable names for triggers 157–160 and the actor
  codes. Smart switches take their target as a link like the others.
- Undo covers all of it as one step per edit.

### Phase 3: 3D view and level properties (medium)

- 3D view: the level's FE ceiling/floor colors and FB flats win over game-info (as
  `ceiling-floor-colors` / `ceiling-floor-flats` do in the game), split doors (`split: true`),
  locked door faces, and the inverse fade curve if the shading question says so.
- Level properties: `floor-number` and `cluster` fields, written to the mod's game-info by
  GameInfoFile. Hub levels show which mission they're in.
- New mod… for Blake: check the template writes a level the game can start (episode, floor 1,
  cluster).
- elevator.yaml and presenter.yaml stay in the Definitions window's raw YAML tab.

### Phase 4: checks and play-testing (small to medium)

- LevelChecks for Blake:
  - a map-info value tile that has been overwritten or is off the level's edge;
  - more than one code of a kind (only the first counts);
  - an F8 link or smart switch pointing at a tile with nothing it can act on;
  - an F4 transporter or elevator to a floor the mission doesn't have;
  - a hint number past the end of its text;
  - a level with no player start, or no way out (157 or the elevator).
- Play (F5 / Shift+F5): check `--warp` into a hub floor, with the briefing, the floor-1 start rule
  and hub state from a previous run all behaving. If the engine needs a flag to skip the briefing,
  add it.
- Check by eye: edit AOG floor 1 and Planet Strike MAP01 in the editor and play them.

## Risks

1. **Hub state on warp.** Warping into a hub floor may pick up the level-start snapshot or visited
   floors from another run. Phase 4 has to check it.
2. **Value tiles look like things.** Code that walks the object plane (checks, scatter, magic
   wand, TileRemap) must keep treating the tile after a code as data.
3. **Cross-floor links.** F8 links to another floor are skipped by the engine. The editor can
   only show them as "on floor xx" without a line.

## Follow-up (not this work)

- A JAM movie viewer in the art or sound browser.
- Field editors for elevator.yaml and presenter.yaml in the Definitions window.
- Raw data file mods for Blake (docs/dev/raw-data-mods-plan.md): the same generator works for
  `.bs6` and `.vsi` files.
