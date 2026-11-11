# Lighting in Common: the game's distance shading for the editor

Move the data and math of PFWolf's distance shading (the light rows, fade steps, lighten levels
and the bright / lighten rules, and how game-info shading and zones resolve) out of the game
project into PFWolf.Common, so the editor can draw things shaded exactly as the game does.
First use: the actor editor's "seen from the player" preview, the one piece
`docs/dev/actor-editor-plan.md` phase 4 left out. Later: the map editor's 3D view switches from
its RGB look-alike to the same tables. Branch: feature/editor/actor-editor, with the actor
editor.

Decided (2026-11-11, by the user):
- **Math and rules move, renderer state stays.** Into Common: the light-row builder (the
  palette work), the fade-step math, the lighten levels and bright / lighten rules, game-info
  resolving (`ResolveShading`, `ResolveZones`), and `ZoneState` / `LightEffects` /
  `ShadingSettings` / `ZoneEffect` as plain data. The light grid and cells, zone ticking, the
  `A_*` actions, actor (halo) lights and saves stay in the game's `Lighting` and `ActorLights`.
- **The preview's shading is picked from a level.** It starts on the game's default-map
  shading; a level picker takes that map's fade, light and zones, and a zone picker its light
  and tint. Light and fade can be changed by hand in the preview, and those changes are never
  saved.
- **The 3D view goes exact, in a later phase.** Indexed textures and a light-row lookup texture
  in the shader, so it shows the game's 32 light levels × 32 fade steps and palette matches,
  with shading and zones from Common's resolvers.
- **Shade only, the zoom kept.** The preview stays the zoomed picture, so its detail stays
  visible. It isn't drawn at the size the game would draw it at that distance. A caption gives
  the distance, the fade step and the light level.

## How shading works now

**Light rows** (`VideoManager.GetLightRow`). The view is palette indices, so shading is a
Doom-style colormap remap. One light row is `FadeSteps` (32) remaps of 256 entries for one
light: a light level (0 to `LightLevels - 1`, 31) and a tint. Each palette color is first
dimmed to level/31 of itself and multiplied by the tint (#FFFFFF for none). Then it is mixed
toward the fade color by step/31 and matched to the closest palette entry **in CIELAB**
(`FindClosestPaletteIndexByLab`; plain RGB turned fogged blue walls purple). Full light with no
tint at step 0 keeps the index itself, not just its color. Rows are cached by (level, levels,
steps, fade, tint), and matches by RGB (`_lightMatches`, shared by every row so rows built
mid-level are cheap). Both caches are cleared when the palette is set (`VideoManager.Init`).
`GetDarkenTable` (menus' dim) and Weather's dim row use the same machinery.

**Fade steps** (`Lighting.ShadeOffset(nx)`). A pixel's step comes from its distance along the
view, `nx` in global units (`TileGlobal` = a tile), as `CalcHeight` measures it. Nothing fades
before `FadeStart`. Linear reaches `MaxFadeStep` at `FadeEnd`. Inverse (Blake's) is
`MaxFadeStep − MaxFadeStep × FadeStart / nx`. The result is an offset into the row
(step × 256). Walls take one step per post (`ShadeOffsetForHeight(height)`, which turns the
post's height back into a distance through `HeightNumerator`). Flats take one per screen row
(`FlatRowShadeOffset`), and sprites one per sprite (`LightenedShadeOffset`: the distance's step
less the actor's lighten steps, clamped to 0..`MaxFadeStep`). Particles and weather take one
per particle or drop. The weapon in hand gets the player tile's light only, no fade. The sky is
never shaded.

**Settings** (`Lighting.RebuildShading`). `ShadingSettings` (fade color, start and end in
tiles, max fade %, light 0-255, inverse) is resolved value by value from the map's game-info
`shading:`, else the default map's, else the defaults #000000 / 0 / 16 / 100 / 255 / linear
(`ResolveShading`). `MaxFadeStep = round(maxFade/100 × 31)`. The ambient level is
`LightLevelOf(light) = round(light/255 × 31)`. A level with zones but no shading fades with
`NoFade` (max fade 0). Shading is on when anything is dimmed, faded, zoned or set-lit, and the
Lighting setting (`r_lighting`) is on.

**The light grid** (renderer state). Each tile has a base light (level + tint index): its plane-5
zone's, else the level's. On top of that, each tile is split into 8×8 cells, and actor lights
add levels to cells (`CellAdd`, `CellSet`, `CellTint`). `LightAt(tile, gx, gy)` is the tile's
row plus its cell's light. Tints are a list per level (`Tints`, `TintRows`) that is reset when
shading is rebuilt. Zones tick with the actors (fades, flicker / pulse / strobe on their own
`Random`, so demos stay in sync) and are saved with the level.

**Bright and lighten** (static rules). `IsBright(actor)` is true for the state's `bright`
modifier or `lighten(bright)` (`ActorStateFrame.Bright`), the actor's `bright` flag, or the
class's `lighten: bright`. `Lighten(actor)` is the class's `lighten` or the frame's
`lighten(level)`, whichever is more when both are set. A level is a number of steps or a name
from game-info `lighten-levels` (the `lighten` console command can change a name for the
session). An unknown name is warned about once and counts as 0. Below 0 is darker. The renderer
only asks while shading is on (`SpriteDrawing`: `visobj.Bright`, `visobj.Lighten`).

**Blake Stone's differences** (data, not code): its default map shades
`fade-start 4.52, max-fade 80, fade-curve inverse`, which is bstone's shade_max 51 and divisor
6. Planet Strike sets its own `fade-start` / `max-fade` per floor. `lighten-levels` has
partial-bright 6 (lamps), lamp-dim −2, explosion 26 and muzzle-flash 5, each about half of
bstone's 63-level numbers. One renderer special: Planet Strike's cloaked sprites darken what's
behind them through step 16 (`SpriteScaler`). That needs the view behind, so it stays out of
the preview.

**What the editor has today:**
- The actor window only says it in words ("drawn bright", "lighten(x)" in `RunningText`).
- `MapTiles.Shading` copies `ResolveShading`, but takes `fade-curve` only from the default map,
  since the editor's `MapProperties` has no fade-curve. `MapTiles.LightAt` gives a zone's 0-255
  light and tint, unquantised. `MapProperties.EffectiveZones` merges zones its own way, which
  isn't checked against `ResolveZones`' ambient-only-zone rule.
- The 3D view (`LevelMesh`, `LevelView3D`) bakes light × tint into vertex colours and fades
  smoothly in GLSL, linear or inverse. That is close to the game, but not stepped and not
  matched to the palette. It has no lighten / bright, no halo lights and no zone effects.

## Where things go

- **PFWolf.Common, Shading/** (phase 0). The namespace is `PFWolf.Shading`, so nothing clashes
  with the game's `PFWolf.Rendering.Lighting`. All types are public:
  - `ShadeMath`: `LightLevels`, `FadeSteps`, `LightLevelOf`, and `ShadeOffset(nx, FadeParams)`,
    `ShadeOffsetForHeight(height, heightNumerator, FadeParams)` and
    `LightenedShadeOffset(...)`, moved unchanged as static functions.
    `FadeParams` (fade RGB, start/end in global units, `MaxFadeStep`, inverse) is built from
    `ShadingSettings` exactly as `RebuildShading` builds it now.
  - `LightRowBuilder`: one per palette. `Row(level, fade, tint)`, the CIELAB match, `ToLab`,
    and both caches, all moved from `VideoManager` unchanged. `VideoManager` holds one, makes a
    new one when the palette is set, and its `GetLightRow` / `GetDarkenTable` forward to it.
    The editor makes one per loaded game's palette.
  - `LightenLevels`: an instance (the name → steps table, `Steps(level)`, `IsBrightLevel`,
    `"bright"`), built from game-info `lighten-levels`. The game keeps one static instance with
    the `lighten` command's changes on top (those stay game-only). Unknown-name warnings go
    through a sink: the game's prints to the console as now, and the editor's lists them as
    problems in the frame panel.
  - `ActorShadingRules`: `IsBright(stateBright, hasBrightFlag, classLighten)` and
    `Lighten(classLighten, stateLighten, LightenLevels)`, the rules `Lighting.IsBright` /
    `Lighten` hold now. The game's versions become one-line calls with the actor's fields.
  - `ShadingResolver`: `ResolveShading`, `ResolveZones`, `IsInverseCurve`, `ParseRgb` and the
    warn-once set, over the game-info records already in Common (`ShadingInfo`, `ZoneInfo`,
    `DefaultMapInfo`).
  - `ShadingSettings`, `ZoneEffect`, `LightEffects`, `ZoneState` (its `Write` / `Read` too:
    the bytes are the save format, and they move unchanged), out of `PFWolf/LightingTypes.cs`.
    `RowLevel` / `RowTint` / `RowColor` are grid bookkeeping, so they stay in the game as a
    `Lighting`-side map, or as internal fields if that's simpler. Phase 0 decides, either way
    without changing a save byte.
- **The game keeps** `Lighting` (grid, cells, tints and their rows, `LightAt`,
  `RebuildShading`, ticking, actions, saves, hooks), `ActorLights`, and every renderer call site.
  `Lighting`'s members stay as thin forwards (`ShadeOffset(nx)` → `ShadeMath.ShadeOffset(nx,
  fade)`), so no renderer file changes. Phase 0 is a move, not a rewrite.
- **PFWolf.Editor:**
  - `Rendering/ShadedPicture`: an `IndexedPicture` remapped through one 256-byte slice of a
    light row (see-through pixels left alone), then `ArtCache.Render` as now. The cache key is
    picture + row + offset.
  - `ActorBrowserViewModel` (+ window): a **Seen from the player** toggle beside zoom, a
    **distance** slider, **Shading from** (game default / a level), **Zone** (none / the
    level's zones), hand overrides (light, fade color, start, end, max fade, curve) behind a
    "More" expander, and the caption. `DrawFrame` picks the row and offset with Common's rules
    on the frame (`Bright`, `LightenLevel`) and the class's `lighten` / `bright` flag.
  - `MapTiles.Shading` / zone resolving switch to `ShadingResolver`, which fixes the ignored
    map fade-curve.
  - `LevelMesh` / `LevelView3D` in the last phase (below).

## Phases

0. **Shading in Common, proven to change nothing.** Before moving anything, add
   `ShadingSnapshotTests` (Content, like `ActorStateSnapshotTests`; set
   `PFWOLF_SHADING_SNAPSHOT=<file>` to dump), which writes:
   - for each game's palette (Wolf3D, WL1, Spear, Spear demo, Blake, Planet Strike, and any
     example mod with its own palette): the bytes (as a hash per row) of every light level 0-31
     × the fade colors and zone/light tints found in every game-info of the packs, the
     Multiplayer mod and the example mods, plus black / #808080 / white × no tint, and the
     darken tables menus use;
   - for every resolved shading in those game-infos (each map's): `FadeParams`, and
     `ShadeOffset` at 0-64 tiles in 1/16-tile steps and `ShadeOffsetForHeight` for heights
     1-8192 at the classic and a widescreen `HeightNumerator`;
   - every resolved zone set (`ResolveZones` per map), and `Bright` / `Lighten` for every frame
     of every class (the 6354 classes the actor snapshot walks), with the lighten warnings.

   Then the move (above). Done = the snapshot files byte-identical before and after;
   `DemoRegressionTests`' eight Wolf3D / Spear demos trace-identical to the reference; the
   existing `LightingTests` and `ActorStateResolverTests` lighten tests passing; the full
   suite passing; and before/after screenshots pixel-identical (headless drive recipe) of
   Blake floor 1 (inverse fade + partial-bright lamps), Planet Strike floor 1, `moody-lights`
   (zones, tints, halo lights), `weather-demo` fog, `switch-demo`'s light room, and Wolf3D E1M1
   (unshaded: proves the off path).
1. **Seen from the player (actor window).** The toggle, distance slider and caption on the
   game's default-map shading. The light comes from the default-map light (no zone). The frame
   is shaded with the step for that distance less its lighten, or drawn unshaded when bright,
   as `SpriteScaler` does. Weapons in hand (player weapon classes' ready / fire states) take
   the light only, with no fade, and the slider is greyed with "weapons in hand don't fade".
   When the game has no default shading (Wolf3D, Spear), the caption says so and points to the
   level picker. Playing the state animates through its frames with the shading on, so a lamp
   flickering via `lighten(lamp-dim)` shows. Tests: `ShadedPicture` against `LightRowBuilder`
   by hand, and the view model picking the expected row/offset for bright, lighten-number,
   lighten-name, class lighten, both (max), negative and weapon frames.
2. **Shading from a level, zones and overrides.** The level picker (the game's levels and the
   loaded mods'), the zone picker with that zone's light and tint, and the hand overrides; all
   resolved by `ShadingResolver`, so a level's preview is the level's resolve. `MapTiles` and
   `MapProperties.EffectiveZones` switch to the shared resolvers, checked on every map of every
   game and example mod against the old results. Differences are listed, and fixed only where
   the game's rule is the right one: the map fade-curve, and ambient-only zones. Docs:
   `docs/manual/editor/actors.md`, manual-testing 2.19 ("(never tried)" items).
3. **The 3D view on the game's tables.** Textures upload as palette indices (R8, nearest).
   The palette becomes a 256×1 texture, and the light rows each mesh needs (one per (level,
   tint) its tiles use; at most 32 a tint) go into a lookup atlas. The fragment shader
   `texelFetch`es index → row[step × 256 + index] → palette color. The step comes from the
   same `ShadeMath` formula on the fragment's depth along the view: one per column on walls
   and one per row on flats, as the game draws them. Billboards take one step per thing from
   their centre's depth (a vertex attribute), less the spawn frame's lighten, or none when
   bright. Lights come from `LightLevelOf` per surface, and the light grid's cells (halo
   lights) are still left out. Done = an editor screenshot and a game screenshot of the same
   spot in Blake floor 1, `moody-lights` and `weather-demo` compared (geometry and filtering
   differ, but colours at the same distance match the palette entries). `LevelMeshTests`
   cover the row/step attributes, the "3D: shading" toggle still works, and the RGB path is
   removed.

## Defaults (open to change)

- Distance: 0 to 32 tiles in 1/8-tile steps, starting at 8. It is measured along the view (the
  game's `nx`), and is turned into a sprite height and back through the classic 320×200 view's
  `HeightNumerator`, as `ShadeOffsetForHeight` does. So the step changes at the distance it
  does in a classic window.
- Caption: "8 tiles: fade step 14 of 31 (lighten −6 → 8), light level 31", and
  "drawn bright: unshaded" for bright frames.
- "Seen from the player" is off by default, remembered in `EditorSettings`, as are the last
  distance and "Shading from" (per game).
- Lighten levels come from the loaded game-info with its mods (so a mod's `lighten-levels`
  shows). An unknown name is a frame problem, not a console line.
- Hand overrides are never saved, and there's no "use these in the map" button. Map Properties
  is where shading is edited.

## Open questions

- **Halo lights on the actor itself.** In the game, an actor with `light:` lights its own cell
  (`LightAt` adds the cell at its centre), so a lamp with a light looks brighter than its
  lighten alone. Showing that needs `ActorLights`' cast falloff in Common too. Default: not in
  these phases. The caption says "its own light isn't shown", and moving the falloff math is a
  follow-up.
- **Zone effects in the preview.** `ZoneState` moves, so a picked zone's flicker / pulse /
  strobe could tick with the preview's clock (on its own `Random`, as the game's). Default: yes,
  in phase 2, since it's cheap once the data is in Common.
- **Translations.** The game recolors (factions, player colors) before shading; the preview
  shows no translation today. Default: leave it until the actor window shows translations at
  all.
- **Map fade-curve in Map Properties.** `MapProperties` has no `fade-curve` field, so a map's
  own curve can't be edited (the game reads it). Default: add the field in phase 2, beside the
  resolver switch.
- **3D view flats and pitch.** The game's flats take a step per screen row because its
  y-shearing keeps rows level. The 3D view's free camera pitches, so per-fragment depth is the
  closest match. Default: per-fragment depth along the view, said in the docs.

## Status

Planned 2026-11-11.

**Phase 0 done 2026-11-11** (shading in Common):
- PFWolf.Common/Shading (namespace `PFWolf.Shading`, a global using in the game and the tests):
  `ShadeMath` (LightLevels, FadeSteps, MaxLightLevel, LightLevelOf, ShadeOffset,
  ShadeOffsetForHeight, `Lightened`) with `FadeParams.From(settings)`; `LightRowBuilder`
  (GetLightRow's tables and the CIELAB match, one per palette); `LightenLevelTable` (named
  that, not `LightenLevels`, which `Lighting` already has as a property; a `Warn` sink, the
  console by default); `ActorShadingRules` (IsBright, Lighten, taking whether the class has a
  `lighten` key, since an empty one still counts); `ShadingResolver` (ResolveShading,
  ResolveZones, IsInverseCurve, ParseRgb, IsRgb, TryParseZoneEffect, BadColors, NoTint).
  `ShadingSettings`, `ZoneEffect`, `LightEffects` and `ZoneState` moved with `git mv`
  (`PFWolf/LightingTypes.cs` → `Shading/ZoneState.cs`, now public). `ZoneState` keeps its
  RowLevel / RowTint / RowColor fields, marked as the game's grid bookkeeping (not saved).
- The game: `Lighting` keeps every member, the moved ones now one-line calls (`Fade` holds the
  level's `FadeParams`; FadeR...InverseFade read from it); `VideoManager.GetLightRow` makes a
  `LightRowBuilder` from the game palette on first use and again after `Init`. No renderer
  file changed. `GetDarkenTable` (menus' dim, RGB-matched) stayed in `VideoManager`: the
  editor doesn't need it.
- Checked: `ShadingSnapshotTests` (Content, ~15 s; `PFWOLF_SHADING_SNAPSHOT=<file>` writes
  it), written and run before anything moved, was byte-identical after (442 KB: all 32 light
  levels of each fade color × tint the games using a palette have, 150 such sets over the
  wolfpal / spearpal / blake palettes (7 fade colors, 18 tints in all), and darken tables;
  each map's shading, its fade fields and fade steps at 0-64 tiles and heights 0-8192 at three
  height numerators with five lighten values; each map's zones with their save bytes; bright /
  lighten for every frame of every class in the 7 games, Multiplayer and 43 example mods).
  Headless screenshots (scratchpad shots.ps1: `--warp`, `--exec "wait 35; screenshot"` under
  SDL's dummy driver) were the same before and after: Wolf3D E1M1, Blake MAP01 + MAP02, Planet
  Strike MAP01, moody-lights, weather-demo and E1M1 with the console's `fog #808080 1 8 90;
  light 140`. switch-demo's light room was left out: its flickering lamp (own Random) differs
  from one run to the next.

**Phase 1 done 2026-11-11** (seen from the player):
- Common: `SpriteShading`. It has the game's ActorSize / MinDist / FocalLength / ViewGlobal (a test
  checks they're GameConstants'), `HeightNumerator(projectionWidth)` (CalcProjection's) and
  `ViewHeight(distance, heightNumerator)` (TransformActor's: measured to the sprite's near side,
  0 when too near). `ClassicProjectionWidth` is 304, the 320×200 window at view size 19.
- Editor: `Rendering/ShadePreview` picks the row and offset as SpriteDrawing / SpriteScaler do:
  - no shading (or one that neither dims nor fades) leaves the picture as it is;
  - bright is unshaded;
  - in hand is the light row at step 0;
  - anything else is `Lightened(ShadeOffsetForHeight(ViewHeight))`.

  `Remap` puts a picture through one remap. `ActorBrowserViewModel.Shading.cs` adds the toggle,
  the 0.75-32 tile slider (1/8 steps, default 8) and the caption, and makes shaded bitmaps per
  (sprite, row, offset). The class's bright flag and `lighten` come from its merged data, the
  frame's from `Bright` / `LightenLevel`, through `ActorShadingRules` with the game's
  `lighten-levels` (`LightenLevelTable` whose warnings go to the caption). "In hand" means a
  Weapon-kind class's frames outside Spawn.
- Tests: `ShadePreviewTests`. The main one, `Each_Distance_Gets_The_Step_The_Games_Lighting_Gives_It`,
  checks every 1/8 tile from 1 to 32 at lighten 0, 6, -2 and 26, for Blake's inverse fade and a
  grey fog, against the game's own `Lighting.LightenedShadeOffset` and `LightRow`. The others
  cover the classic height numerator (`ViewSetup` at 320×200), bright, in hand, unshaded, rows
  equal to `VideoManager.GetLightRow`, and remap keeping transparency. Gotcha: the game's
  integer division gives step 1 right past fade-start, not 0.
- Checked in the editor (Blake AOG, FloorLamp): 2 tiles step 0, 8 tiles step 5, 24 tiles step 15,
  each with lighten +6, and the lamp visibly darker as it goes. Docs: manual actors.md,
  manual-testing 2.19.
- Left from the plan's defaults: the toggle and distance aren't remembered in `EditorSettings`
  (the actor window has no settings access yet), and the slider isn't greyed for weapons (the
  caption says they don't fade).

**Phase 2 done 2026-11-11** (shading from a level, zones, set by hand):
- **Resolver check first:** a one-off test ran the editor's old resolving (MapTiles.Shading's
  copy, `MapProperties.EffectiveZones` + LightAt) against `ShadingResolver` on every map entry of
  the Wolf3D, Spear, Blake and Planet Strike packs, Multiplayer and every example mod (2891 entries, 44 zones). There
  were **no differences**: today no map sets its own fade-curve, and no ambient-only zone sits
  where lighting reads it. The switch was made anyway, so later maps can't drift.
- **Editor onto the shared rules:**
  - `MapProperties` gained `FadeCurve` (read, saved as `fade-curve`, compared, in `HasShading`)
    and `ToShadingMapInfo()`, so the edits resolve through Common.
  - `MapTiles.Shading` is now `ShadingResolver.ResolveShading`, and the `ShadingValues` record is
    gone (Common's `ShadingSettings` is used).
  - New `MapTiles.LightZones` (`ResolveZones`); `LightAt` takes those and gives a zone's
    `Current`.
  - `LevelMesh` lights from them.
  - The zone overlay and level checks still use `Zones` (`EffectiveZones`): they're about the
    plane's ids, not light.
- **Map Properties:** a Fade curve field (linear / inverse, the default map's as placeholder).
- **Common:** `ShadingResolver.NoFade` (the game's `Lighting.NoFade` forwards to it).
- **Preview:**
  - `ShadePreview.Shade` takes whether the level has zones (the game shades a level with zones
    even when nothing dims or fades, through NoFade) and a `PreviewZone` (light, tint), using the
    zone's light level and tinted row as the game's light grid does.
  - The view model (ActorBrowserViewModel.Shading.cs) adds Shading from (the default map, plus
    each game-info map with shading or zones, mods merged) and Zone (the level's resolved zones,
    which tick with the preview on their own Random, redrawing when the level of light moves).
  - It also adds Set by hand (light, fade color, start, end, max fade, curve, with Clear). These
    are laid over the map's own `ShadingInfo` and resolved, so a light alone takes the game's
    fade defaults; bad values are left out and said.
  - The caption says "no fade" when the max fade is 0.
- **Tests:**
  - `ShadePreviewTests`: a zone's row equals the game's `Lighting.LevelRow` for that level and
    tint; a zones-only level is shaded as `Lighting.Shading` says; edited properties resolve like
    the game-info they came from (map fade-curve, ambient-only zone left out).
  - `GameInfoFileTests`: fade-curve written and read back.
  - `ActorShadingGameTests` (GameData): Blake FloorLamp at 24 tiles; switch-demo MAP01 zone 1
    ("no fade, light level 3 of 31 (zone 1)"); Wolf3D shading set by hand (light 128 at 8.25
    tiles: step 15 of 31) and a bad value.
- **Checked in the editor:** Wolf3D + switch-demo, the Guard in MAP01's zone 1 drawn near black.
  Docs: manual actors.md and levels.md, manual-testing 2.19.
