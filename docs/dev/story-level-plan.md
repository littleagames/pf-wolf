# Story level and pits

The player's floor sits at a story of its own, and walls carry on down below it as tall walls
carry on up. The floor is solid, so what's below only shows through **pits**: tiles with no
floor, looking down past walls' lower stories to a bottomless drop.

## Decisions (2026-11-07)

- **Global per level**: one story level for the whole map, not per-tile floor heights.
- **Heights count up from the player's floor**: plane 3 means what it means today, and every
  wall also gets `story-level - 1` stories below. Story level 1 (the default) looks exactly as
  now, so existing maps and mods don't change.
- **Pits are a mapdefs flat flag**, on the flat-plane floor index, beside `floor-skies:`.
- **Pits are visual only**: players, enemies, shots and sight cross them as floor. No falling,
  no collision change, so demos, saves and multiplayer are untouched.
- **The bottom is a void**: the tile's floor sky, else the level's floor sky, else a plain
  color. No second floor plane at ground level.

## Data

```yaml
# game-info: default-map and per map
default-map:
  story-level: 1          # 1-8; the player's floor is this many stories up
maps:
  MAP01:
    story-level: 3
    pit-color: Black      # the void with no floor sky (left out: black)

# mapdefs flats.yaml: floor index -> the texture on the pit's sides
flats:
  pits:
    10: GSTONEA1
    11: GSTONEA1
  floor-skies:
    10: Clouds            # optional: clouds at the bottom of pit 10
  floor:
    11: GRATE             # optional (phase 2): a grate over pit 11, see-through pixels show the pit
```

A pit's side texture is what shows under the solid floor beyond the pit's edge (from the floor
line down to the ground). An index in `pits:` with no texture isn't a pit (warned). A pit on a
door or wall tile is ignored (warned). With `story-level: 1` a pit has no depth: it shows only
its void, a hole in the floor.

## How it renders

- **Pit spans.** While the ray trace walks a column, before its first wall, each pit tile it
  crosses adds the screen rows its floor would have covered: `FloorRow(exit)` to
  `FloorRow(entry)`. A view standing on a pit (it's walkable) starts the span at the bottom of
  the screen, as `OpenFocalArch` does for arches.
- **Below-floor posts are clipped to the pit spans.** Everything below the floor line is drawn
  only inside them, so solid floor in front hides it. Painter's order (farthest first) handles
  the rest: a nearer pit's far side covers what's past it.
- **Pit sides.** Leaving a pit into an open tile that isn't one (or a door's tile) queues a post
  at that edge from the floor line down to the ground, `story-level - 1` stories, in the pit's
  side texture. Its light is the pit tile's.
- **Walls' lower stories.** The first wall a column hits gets its stories below the floor too,
  the wall's own texture repeating per story (DrawPost with a negative first story). Walls the
  trace goes past for tall walls don't: the first wall's lower stories hide theirs.
- **The void.** Pit rows nothing covers keep what's behind: DrawFlats treats a pit tile as having
  no floor (its floor sky, the level's floor sky, else `pit-color`).
- **Covers.** Below-floor posts record their rows (RenderState.Cover) so DrawLevelFlats skips
  them; MaxCovers may need raising.
- **No pits, no cost.** A level with story level 1 or no pit tiles skips all of it.

Sprites need nothing: they stand on the floor line and clip against the nearest wall as now.

## Phases

Phase 1 done 2026-11-07 (StoryLevelTests; headless screenshots in E1M1). Pushwalls and diagonals
already take the wall path's lower stories, but untested; a diagonal entered by its open half and
a moving pushwall's gap are phase 2. Pit-side textures don't animate or follow A_ChangeTexture.

1. **Story level, pits, walls below (game).** `story-level` / `pit-color` on game-info maps
   (Common) → set in LevelSetup; `storylevel [1-8]` console cheat (not saved, as `wallheight`).
   mapdefs `flats: pits:` on MapFlatsTranslation → MapManager pit map (rebuilt in BuildFlats, no
   save change); `where` shows the pit and story level. Renderer: pit spans in RayTrace (incl.
   the focal tile), pit sides, lower stories on the first wall's posts (plain walls, door frames),
   DrawPost/DrawPatch below the floor (story-0 patches repeat below, numbered ones don't), clip to
   spans, covers, DrawFlats leaving pits bare. Tests (WallPostsTests: lower texel mapping,
   clipping; flats translation parsing; MapManager pit map).
2. **The other wall kinds.** Pushwalls moving next to or over a pit, diagonal walls, wall
   sprites, doors on a pit's edge (their tile's floor is solid: a pit side), see-through doors,
   arches over pits, a `floor:` texture on a pit index drawn over it as a grate, the automap
   marking pits, y-shearing and eye height looking down into pits. Tests.
3. **Editor, example, docs.** Editor 3D view (LevelMesh: walls down to the ground, no floor on
   pit tiles, pit sides, void/floor-sky bottom), 2D view pit mark, Map Properties story level,
   Definitions window `pits:`. Example mod `pits-demo` (generator script, as tall-walls-demo): a
   level at story 3 with a chasm, a bridge, a cloud floor sky in the pit and a grate. flats.yaml
   comments, game-info docs, manual-testing "(never tried)" items.

## Out of scope

Falling into pits, per-tile floor heights (platforms, stairs), a floor at ground level, the eye
above the walls (wall tops), particles/weather falling into pits (they stop at the floor line).
