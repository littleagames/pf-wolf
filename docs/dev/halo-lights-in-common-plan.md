# Halo lights in Common: the game's light grid and actor lights for the editor

Move PFWolf's light grid and actor ("halo") light casting into PFWolf.Common, so the editor draws
an actor's own light and the light things cast on a level exactly as the game does. This is the
follow-up `docs/dev/lighting-in-common-plan.md` left open. That plan moved the light rows, fade
steps and lighten rules, so the actor preview and the 3D view are now dimmed and faded as in the
game, but neither has the light things give off. In the game a lamp with `light:` brightens its
own sprite and the floor, walls and things around it; in the editor it doesn't (the moody-lights
door lit by a lamp is brighter in the game). Branch: feature/editor/actor-editor, after the
lighting plan.

Decided (2026-11-11, by the user):
- **The grid and the casting move.** A Common `LightGrid` holds each tile's base light, the 8×8
  cells per tile and `LightAt`. Casting goes with it: spreading, the line of sight, falloff,
  dither, set lights and tints, over a small map interface. So do the light rules: a class's
  light from its properties, a state's `light:`, effects and A_SetLight's override as data.
  The game keeps A_SetLight / A_LightOff, ticking, which actors light, the weapon in hand, and
  saves.
- **The 3D view lights per cell, exactly.** The cell grid goes up as a texture, and the shader
  picks each fragment's cell as the game does: walls half a cell toward the view, floors and
  ceilings at the point. It then picks the light row for that cell's level and tint. Things are
  lit on the CPU at their spot.
- **Effects move in the 3D view.** Actor light flicker / pulse / strobe and zone effects (shown
  frozen today) tick on the view's clock, on their own Random as in the game, and the lights are
  recast.
- **The actor preview shows a frame's light on itself.** The sprite is lit as the game lights it
  at its own spot, with its cell's added light and tint, and the caption says how much it adds.

## How it works now

**The light grid** (`PFWolf/Rendering/Lighting.cs`):
- **Per tile:** a base light level and tint index, from its plane-5 zone's light, else the
  level's (`TileLevel`, `TileTint`). `UpdateLightGrid` rebuilds them when the zones or the zone
  plane change.
- **Per cell:** each tile is split into 8×8 light cells (`LightCellShift` 3), each with:
  - `CellAdd`: levels added by lights;
  - `CellStrong` / `CellTint`: the strongest light's strength, and its tint;
  - `CellSet` / `CellSetWeight`: a set light's signed shift from the base, and how much of the
    cell it holds.
- **`LightAt(tile, gx, gy)`:** the tile's base level plus its cell's add and set, clamped to
  0-31. The tint is the cell's if it has one, else the tile's. The result is a light row
  (`RowOf(tint, level)`, made by `VideoManager` through Common's `LightRowBuilder`).
- **Tints:** a list, index 0 for none, filled as zones and lights need them (`TintIndex`), and
  forgotten when shading is rebuilt.

**Where the renderer samples it:**
- Walls: `WallPosts.WallLight`, the cell half a cell from the hit point toward the view, with
  the base of the open tile the trace came from.
- Floors, ceilings and arch undersides: each pixel's point and tile.
- Sprites: the actor's X/Y and tile (statics and wall sprites at their tile's centre).
- The weapon in hand: the player's spot, with no fade.
- Particles and weather: their point.

**Actor lights** (`PFWolf/Rendering/ActorLights.cs`, `PFWolf/Entities/Actors/ActorLight.cs`):
- **A class's light (`ActorLightInfo.Of`):** actordefs `light.intensity` (0-255),
  `light.radius` (tiles, default 2), `light.color`, `light.effect` / `low` / `tics` /
  `brighttics`, and `light.mode` (add, or set: a zone-like light that can darken). A class with
  none and no state lights has no light.
- **A frame's light:** a state's `light:` (`StateLight`, already in Common on
  `ActorStateFrame.Light`) changes intensity, radius and colour frame by frame, or puts it out.
- **Overrides:** A_SetLight / A_LightOff set a `LightOverride`, which is saved (save v15).
- **`CurrentLight`:** works the light out from the info, the override, the state light and the
  effect (`LightEffects.Apply`, already in Common).
- **`CastActorLights`, before each frame:**
  - It clears the cells it lit last frame.
  - If a set light is on a level with no shading, it turns shading on (`ActorShading`).
  - It casts every lit actor: those on the level, the players' bodies, and the weapon in hand
    at the player's spot.
- **`CastLight`:**
  - A breadth-first spread from the light's tile through tiles light can pass: open floor and
    doors that aren't shut (`LightPasses`). Tiles it can reach are lit (`LightEnters`): floor,
    doors and diagonals.
  - Only tiles within the radius count, and only those a straight line from the light can see
    (`LightSees`: to the tile's middle, or to its nearest point an eighth in, sampled every
    eighth of a tile).
  - Each cell gets `levels × smoothstep falloff` plus a 4×4 Bayer dither, in whole levels.
  - Its tint is the strongest light's, paler over 4 steps as the light falls off (`ColorTints`).
  - A set light shifts cells toward its own level instead, holding all of it to half the radius
    and blending out over the rest (`SetHold`), the nearest set light winning.
- **Ticking:** effects tick with the zones (`TickActorLights`), on `Lighting.ZoneRandom` (not
  the game's random numbers, so demos stay in sync).

**The editor today:**
- The actor preview lights a frame with the level's or a zone's light, not its own; the caption
  says "its own light isn't shown".
- The 3D view lights each surface with its tile's base light (`SurfaceLight`), with no cells,
  and zones at their starting light.

## Where things go

- **PFWolf.Common, Shading/** (phase 0):
  - **`LightGrid`:** the arrays above, `SizeTo(width, height)`, `TileIndex`, `CellIndex`,
    `SetTile(index, level, tint)`, `ClearCells(tile)`, and `LightAt(tile, gx, gy)` returning
    (level, tint index).
  - **The tint list, `TintIndex`:** the RGB of each tint; the rows stay the game's or the
    editor's to make.
  - **Moved constants:** `LightCellShift`, `LightCells`, `CellGlobalShift`.
  - **The game's `Lighting`:** owns one `LightGrid`, and its `LightAt` becomes
    `RowOf(grid.LightAt(...))`. Every renderer call site is unchanged.
- **`ILightMap`:** what casting asks of a level: its size, whether a tile is open, a door and
  whether that's shut, and whether it's a diagonal (`LightEnters`, `LightPasses`). The game's
  `MapManager` + `Doors` give one; the editor's `MapTiles` gives one as the level starts (doors
  shut, pushwalls in place).
- **`LightCaster`:** `CastLight`, `LightSees`, `ClearLine`, `TileInReach`, `LightCells`,
  `SetCells`, `SetHold`, the Bayer table and `ColorTints`, all moved unchanged, writing into a
  `LightGrid` and keeping the list of tiles it lit so they can be cleared.
- **The light rules:**
  - `ActorLightInfo`, `ActorLightInfo.From(properties, resolved states, warn)`: today's `Of`
    without the game's `Actor`.
  - `LightOverride`, with its save bytes unchanged.
  - `ActorLightRules.Current(info, override, frame light, phase, flicker)`: today's
    `CurrentLight` as data.

  The game's `ActorLights` keeps the actions, `LitActors`, ticking, the weapon in hand, `lights`
  and `ActorLightState`, calling into these.
- **PFWolf.Editor:**
  - **The actor preview:** casts the frame's light (its class light, its frame's `light:`) on a
    small open patch round the frame, then reads `LightAt` at its own spot, so both its light and
    the level's or zone's go through the same rules. In hand, the light is at the player's spot.
  - **`LevelMesh` / `LevelView3D`:**
    - The level's `LightGrid` is built as the game builds it at the level's start: the zone
      plane, then each shown thing's light from its class and Spawn frame.
    - That grid goes up as a cells texture: per cell, the added level, a signed set shift
      packed, and the tint index. Alongside it, a per-tile base texture, plus a (tint, level) →
      row lookup.
    - The rows atlas grows to every (level, tint) the grid can give.
    - The fragment shader samples the cell at the fragment's world point (walls moved half a
      cell toward the eye on each axis, as `WallLight`), adds, clamps, and picks the row.
    - Things take `LightAt` at their spot on the CPU.
  - **Effects:** a ticking clock in the view (35 Hz) moves zone and light effects, recasts the
    lights that move, re-uploads the cells and redraws, like scrollers.

## Phases

0. **Grid and casting in Common, proven to change nothing.** Before moving anything:
   - A debug console command `lightgrid` prints a hash of the grid: tile bases, cells, tints in
     order.
   - A GameData test runs it headless on lit levels, as the level starts and after some tics:
     the switch demo's light room, moody-lights E1M1 and its lamps, Blake floors with lamps,
     Planet Strike's flickering lights (with a fixed `ZoneRandom` seed for the run), and a set
     light. The hashes are written to a file.
   - A `LightCaster` unit snapshot casts lights on small hand-made maps (walls, a closed and an
     open door, diagonals, corners, overlapping coloured lights, set lights) and hashes the cells.

   Then the move. Done means:
   - the hashes are identical before and after;
   - `DemoRegressionTests`' demos are trace-identical;
   - `ShadingSnapshotTests` is unchanged and the full suite passes;
   - the phase-0 screenshots of the lighting plan are pixel-identical (moody-lights, Blake
     MAP01), plus a switch-demo light room shot with the flicker seeded.
1. **The frame's own light in the actor preview.**
   - The preview casts the frame's light on a 5×5 open patch, then reads `LightAt` at its own
     spot, for the level light or zone picked.
   - The caption gives the added levels and tint ("its light adds 4 levels, #FFB060").
   - It's redrawn as the state plays, so a flash or a flickering lamp's dim frames show, and as a
     light effect ticks.
   - The weapon in hand is lit at the player's spot, as the game does.
   - Tests check the cast against the game's `ActorLights` on the same patch.
2. **Halo lights in the 3D view.**
   - The level's grid at its start: zones, then the shown things' lights from their Spawn
     frames, doors shut.
   - The cells and base textures, the row lookup, and the shader's cell sampling as above.
   - Things lit at their spot, and wall sprites given one fade step each, as the game does (a
     phase-3 leftover of the lighting plan).
   - Done means editor and game screenshots of the same spots agree in light and tint: the
     switch-demo light room (lamp on, flicker seeded), a moody-lights corridor and a Blake lamp.
     `LevelMeshTests` check the grid against the game's cast on the same map.
3. **Effects in the 3D view.**
   - Zone effects and light effects tick on the view's clock, on their own Random, as the game's
     do.
   - Only moving lights are recast: their old cells cleared, then cast again, then the cells
     texture updated.
   - A redraw loop runs while any effect is on the level; the "3D: shading" toggle off stops it.

## Defaults (open to change)

- The 3D view lights the level as it starts: doors shut, pushwalls in place, things in their
  Spawn frame, the things it shows (the skill picked).
- Lights cast only on a shaded level, as in the game. A set light turns shading on for a level
  that has none (`ActorShading`), in the editor too.
- The player's weapon light (a muzzle flash) isn't in the 3D view: no one is holding a weapon
  there.
- The cells texture is sized to the level: 8 cells a tile, so 2048 × 2048 for a 256-tile level,
  RGBA8.
- The effect clock runs at the game's 70 tics a second; light is recast and uploaded at most 35
  times a second.

## Open questions

- **Pushwalls and doors in the 3D view:** doors shut as the level starts (the default), or open,
  to show where light would reach? Default: shut.
- **Skill:** light only the things of the skill picked (default), or every thing?
- **The cost of recasting** on big levels with many flickering lights: recast only the moving
  ones (the default), or all of them, as the game does every frame?
- **A separate "3D: lights" toggle**, or halos with "3D: shading"? Default: with shading.

## Status

Planned 2026-11-11.

**Phase 0 done 2026-11-11** (the grid and casting in Common):
- PFWolf.Common/Shading:
  - `LightGrid`: the arrays, `SizeTo`, `CellIndex`, `TintIndex` and the tint list, and
    `LightAt` → (level, tint).
  - `LightCaster`: casting, `LightSees`, `ClearLine`, `TileInReach`, `SetHold`, the dither, the
    two cell writers, `ColorTints` taking a tint-index delegate (so the game's light rows keep
    in step), `BeginFrame` (clears what it lit last time) and `LitTiles`.
  - `ILightMap`.
- PFWolf.Common/Entities/Actors/ActorLight.cs, moved with `git mv` and made public:
  - `ActorLightInfo.From(name, properties, resolved states)`, which was `Of(actor)`;
  - `LightOverride`, with unchanged save bytes;
  - `ActorLightRules.Current`, which was the game's `CurrentLight`;
  - `ActorLightState`, which moved too, since it's plain data the game's `Actor` holds (the plan
    had it staying).
- The game:
  - `Lighting` owns a `LightGrid` (`Grid`). Its grid members forward to it, and `LightAt` is
    `RowOf(Grid.LightAt(...))`.
  - `TintIndex` keeps `TintRows` growing alongside the grid's tints.
  - `ActorLights` is the game's `ILightMap` (its `LightEnters` / `LightPasses` as before) and
    casts through a `LightCaster`.
  - New: the console `lightgrid [seed N]` (a hash of the grid; seed makes `ZoneRandom` repeat),
    with `ZoneRandom` now settable.
- Checked:
  - `LightGridSnapshotTests` (7 hand-made scenes): the grid hash and `LightAt`'s rows every 1/8
    tile over 48×48 tiles. The scenes cover open floor, walls with shut / open / opening doors
    and a corner, diagonals, overlapping coloured lights, set lights at full light, set + added
    lights in a tinted zone, and state lights / A_SetLight / A_LightOff / pulse / strobe /
    flicker part way.
  - `LightGridGameTests` (GameData, headless `lightgrid seed 7; killall; wait 2; lightgrid`):
    moody-lights, switch-demo, weather-demo, particles-demo and hub-demo MAP01, Blake and
    Planet Strike MAP01. Repeatable run to run.
  - Both of those, and the lighting plan's `ShadingSnapshotTests`, were byte-identical before
    and after the move; `ActorLightsTests` and `LightingTests` pass.
  - The lighting plan's headless screenshots were the same before and after: Wolf3D E1M1, Blake
    MAP01, Planet Strike, moody-lights, weather-demo and E1M1 with `fog`. Blake MAP02 differed by
    104 pixels where a far enemy stands, and of two more shots, one matched the before image
    exactly: the enemy varies run to run.
  - The switch-demo light room still isn't repeatable even seeded (the flicker depends on how
    tics fall into frames); the seeded `lightgrid` hash covers its lamp.
- Committed adea2e4f (the full suite, 3842, passed).

**Phase 1 done 2026-11-11** (the frame's own light in the actor preview):
- `ShadePreview.LightOnItself(base level, base tint, SelfLight?)`:
  - a 5×5 `LightGrid` of open floor (an `ILightMap` that lets light everywhere), every tile at
    the zone's or level's light and tint;
  - the frame's light is cast from the middle of the middle tile by `LightCaster`, with
    `ColorTints` on the grid's tints;
  - then it reads `LightAt` at that spot, giving the level, the tint and how much the light
    added.
- `Shade(...)` takes a `SelfLight` (intensity, radius, colour, set): a set light turns shading
  on for an unshaded level, as `Lighting.ActorShading` does. The caption adds "its light +N
  levels" and the tint.
- The view model reads the class's light with `ActorLightInfo.From`, once per entry shown.
  `ActorLightRules.Current` (no override) gives its light now, with the frame's `light:`. Its
  effect ticks with the preview on the zones' Random and redraws. A light on a level at full
  light is said ("its light can't show at full light").
- Found: every lit decoration in the content (the game's lamps, moody-lights', the demo mods')
  is drawn bright, so unshaded, so its own light doesn't show on it in the game either. The
  preview agrees.
- Tests:
  - `ShadePreviewTests.A_Things_Own_Light_On_Itself_Is_The_Games`: 7 lights (white, amber,
    flash, small, set at full light, tinted set on a dark level, a dark set light) against the
    game's `ActorLights.CastActorLights` + `Lighting.LightAt` at the lamp's spot: same row.
  - `ActorShadingGameTests.A_Thing_Is_Lit_By_Its_Own_Light_And_Its_Pulse_Plays`: a temp mod's
    green pulsing guard on moody-lights.
- Checked in the editor: that guard tinted green, its brightness moving with the pulse. Docs:
  manual actors.md, manual-testing 2.19.
