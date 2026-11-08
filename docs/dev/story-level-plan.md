# Stories: blocks from one story to another, and the player's story

Every tile can hold a block running from one story up to another (1 to 8, 3 to 5, ...), and the
player is on a story of their own. What a block is depends on where the player is:

- a block that takes in the player's story is a **wall**;
- one that ends below it is **floor**: stood on, or looked down onto from above, its top
  covered by the tile's floor flat;
- one that starts above it is **overhead**: today's arches, its underside the ceiling flat.

So one level can put the player on a rooftop, on the third floor of a building looking out of
its windows, or at the foot of a skyscraper looking up. Falling, death pits and keeping people
off open edges are the mod's to build (blocking tiles, triggers); the engine only draws it and
keeps the player's story solid where it should be.

This replaces the first version of this plan (2026-11-07: one story level for the whole floor,
pits marked in mapdefs). Its phases 1 and 2 are committed (f44b0813, 2847cc7e); its phase 3
(editor, pits-demo) was left uncommitted. The pit code goes (phase 1 below).

## Decisions (2026-11-08)

- **Breaking change is fine.** Nothing's released; the example mods and the user's own mods
  (Midnight Assault, End of Destiny, Trench Warfare) are fixed up afterwards (phase 5).
- **One block per tile**, a start and a stop story, on plane 3.
- **Both kinds of tile can hold one.** A plane-0 wall whose block doesn't take in the player's
  story is walked on (or under) and takes a neighbouring tile's area. A plane-0 floor tile with a
  block is a block whose sides come from a new `sides:` on its flats index (mapdefs flats), else
  today's arch rule (the wall ending its run).
- **The level's ceiling is opaque** unless the level has a sky: blocks taller than it don't show
  through it any more. With a sky, taller blocks show against it.
- **The player's story is set per level** (game-info), with a console cheat to change it. Lifts,
  stairs and an action to change it during play come later, with saving it.
- **Pits are gone**: `pits:`, `pit-color`, grates, `DrawGrates`, pit spans, the automap's pit
  marks, `PitEdge` and pits-demo. A drop is a tile with nothing under the player's story: the
  blocks around it go down, and the ground (or a lower block's top) shows at its bottom.
- **No falling.** Things cross a drop at the player's story as if it were floor. The mod blocks
  it, or makes it a death trap.
- Later, not now: enemies moving up and down (a helicopter), lifts and stairs between stories.

Added 2026-11-08, for phase 3:

- **A tile can hold two blocks**: a lower and an upper one, with a gap between (a window in a
  wall, a hole a pushwall leaves, a bridge under a beam).
- **A pushwall moves all of its stories, or only some**, set on each pushwall (its A_PushWall,
  and A_MoveWall for switches), with a game-info default for the level and the game; all if
  neither says:
  - `all`: the whole column goes (as today);
  - `above`: the player's story and everything above it go; what's below stays (the floor);
  - `story`: only the player's story goes; what's below and above stay (a hole with a lintel).

## The model

Story n runs from (n − 1) × 64 to n × 64 texels up from the ground. The player on story P
stands at (P − 1) × 64, eyes eye-height above that.

### Plane 3

A tile's block: **start in the high byte, stop in the low byte**, each 1 to 8; 0 is the default.

| plane 3 | wall tile (plane 0 a wall) | floor tile (plane 0 a floor code) |
|---|---|---|
| 0 | stories 1 to wall-height | no block: open down to the ground |
| 0x0003 (just a stop, today's tall wall) | 1 to 3 | 1 to 3 |
| 0x0305 | 3 to 5 | 3 to 5 |

A plain stop keeps today's tall walls as they are. **Today's arches break**: an open tile with
plane 3 = 3 was a block from story 2 to 3 over the walkway; now it's a block from 1 to 3. An arch
is written 0x0203. (Phase 5 converts the mods.)

**A second block (phase 3)** takes the nibbles the first one leaves free: its stop in bits 4-7
and its start in bits 12-15, so 0x0305 stays one block and 0x7385 is stories 3 to 5 and 7 to 8
(0x7000 the second's start, 0x0080 its stop). The second must start above the first's
top with a gap (touching blocks are one block); anything else is ignored as today. A wall with
no block of its own still runs from 1 to the wall height.

### Pushwalls and stories (phase 3)

`A_PushWall("move sound", "blocked sound", "direction", "message", "stories")` and
`A_MoveWall(..., "stories")` take `all`, `above` or `story`; left out, game-info
`pushwall-stories` (per map, else default-map), else `all`. The part that moves goes with the
moving wall (MovingPushWall keeps its start and stop, saved with it: a save version bump); what's
left stays on the tile, which keeps its wall id on plane 0 (a walkable wall's, area from its
neighbours). Where the piece stops it joins the tile's own blocks (touching ones merge). A push
that would leave a tile, or end on one, needing more than two blocks is refused: the wall stays
put with its blocked sound.

### Game-info

```yaml
maps:
  MAP01:
    story: 3            # the player's story (1-8, default 1); replaces story-level
    wall-height: 8      # walls with no block of their own run from story 1 to this one
    sky: CITYSKY        # the ceiling, else at the top of story wall-height, hides what's above it
```

`pit-color` goes. A level whose plain walls don't reach the player's story (wall-height < story)
gets a startup warning: nothing would be solid but the tiles with blocks of their own.

### What's solid

At level start (and on the `story` cheat) each tile is solid if its block takes in story P:

- a wall tile whose block doesn't is walkable: its wall id is kept for drawing (a new per-tile
  block map), and it takes the area of the nearest walkable floor tile (a flood fill from its
  neighbours), for sound, doors and enemies;
- a floor tile whose block does is a wall: drawn with its flats `sides:` texture.

Doors stay on story P: the door, the floor under it (its frame going down to the ground), and a
lintel above it up to its block's stop (0: the level's wall height), as door lintels are now.
Pushwalls carry their block as they carry their height now; one that doesn't take in story P is
just a block. Diagonals keep their slanted face over their whole block, and are solid only when
it takes in story P. Sight, shots and collision stay 2D on story P, so their code is untouched:
only TileMap and ActorAt change.

With no blocks of its own and story 1, a level is exactly what it was: demotest stays identical.

### Drawing

The renderer goes from "walls grow up from the one floor" to blocks at any height:

- **Heights are measured from the ground**, the eye at (P − 1) × 64 + eye height.
  `FloorRow(half)` becomes a row for any height; textures repeat per story up from the ground,
  so story-1 walls look as they do now.
- **Each tile the trace crosses** queues its block's near face (all of it) and its caps: the top
  (floor flat) of a block below the eye, the bottom (ceiling flat) of one above it, and the
  ground (floor flat at height 0) under a tile with nothing below the eye. Caps are drawn per
  column between where the trace enters and leaves the tile, as arch undersides are now
  (`DrawUnderside`, generalised). They're drawn farthest first, so nearer ones cover farther
  ones: per column on a grid this is exact.
- **Floors at the player's own floor height and the level's ceiling** stay with the row-by-row
  `DrawLevelFlats` pass into the rows nothing else painted, as now: the common case stays fast.
- **The trace stops** when nothing farther could show: each column keeps the rows still open,
  narrowed from the top and bottom by faces, caps, the player's floor and an opaque ceiling. With
  a sky, rows above the tallest block in the level (at the current distance) count as closed;
  rows below the ground always are. Today's "go on past a wall while a taller one could show"
  becomes this.
- **Sprites** stand on story P and clip against the nearest wall that takes in story P (as
  `WallHeight` does now): blocks below the floor or overhead never cover them.
- Arches, door arches and the pit code are replaced by the general block code.

## Phases

1. **Data and gameplay.** Plane-3 start/stop (MapManager: block per tile, `BlockSpan`, default
   by tile kind), game-info `story` (replacing `story-level`) and the wall-height warning,
   solidity per story (TileMap/ActorAt, the block map, areas for walkable wall tiles), flats
   `sides:` (MapFlatsTranslation), `story [1-8]` cheat (replacing `storylevel`), `where` and
   `height` showing and setting start and stop. Remove the pit code. Tests; demotest identical.
   The renderer still draws story 1 only (as now) until phase 2.
2. **Renderer core.** Ground-measured heights, faces and caps per tile, coverage-based trace
   termination, the global passes only at the player's floor and the level's ceiling, opaque
   ceilings, sprites, rain and particles' occluders (PointDrawing). Arches and pits replaced.
   Tests; headless screenshots at story 1 (unchanged), 3 and 8; `perf` against today on E1M1
   and a worst case.
3. **Two blocks per tile, pushwall stories, and the rest of the renderer and play.** In order:
   - two blocks per tile: the plane-3 encoding above, MapManager's blocks and spans, what's
     solid (either block taking in the player's story), `height` taking `1-2,4-8`, `where`; the
     renderer's tiles queuing both blocks' faces and every surface that shows (each block's top
     below the eyes and underside above them, the ground under the lowest, the higher of two
     tops drawn over the lower), also fixing the ground not drawn under a floating floor block;
   - pushwall stories (done): the A_PushWall/A_MoveWall argument and game-info `pushwall-stories`,
     the moving piece's start and stop on MovingPushWall (save version bump), splitting and
     joining blocks where it starts and stops, refusing pushes that won't fit;
   - doors on any story (frame below, lintel above), moving pushwalls drawn at their piece's
     height, diagonals at any height, see-through doors, wall sprites, destructible walls
     (`removewall arch` becomes a block), weather falling to the ground, the automap (walls at
     the player's story; drops and overheads shown apart), UWMF levels' plane 3, tile floor
     skies on caps.
4. **Editor.** A start/stop brush for plane 3 (with a second block), Level properties' Story and
   pushwall stories (and the pit fields go),
   the 3D view's blocks, caps and story (LevelMesh: replacing arches and pits), the 2D view's
   span badges, tile info, map checks (the start on a solid tile, walls not reaching the story).
5. **Mods.** A tool to convert old arch values (and a check for them), then the example mods
   (tall-walls-demo, skies-demo, architecture-demo, ...), a new example level (a rooftop and a
   street below), and the user's mods. Docs, manual-testing items.

## Status

Phase 1 done 2026-11-08 (StoriesTests). MapManager keeps each tile's block (`BlockStarts`,
`BlockStops`, `BlockSpan`, `BlocksStory`, `SetBlock`) and works out what's solid as the map
loads (LevelSetup now sets `Story` and the wall height first) and on `ApplyStory` (the `story`,
`height` and `wallheight` cheats). Plane 0 stays the record of a tile's wall: a pushwall now
writes its id where it stops, so `ApplyStory` can rebuild from the planes. A walkable wall's
area comes from `FloorCode`, which every area lookup now goes through; a floor tile's solid
block is `BlockWall` in TileMap, drawn with `BlockSideTexture`. `removewall arch` leaves a block
from the story above the player's. Pit code gone. Done early from phase 4: Level properties'
Story field. At story 1 the renderer already draws floor tiles' solid blocks (their sides) and
overhead blocks from their own start story with their own texture; any other story is phase 2.

Phase 2 done 2026-11-08. The trace counts heights in stories from the player's floor
(RenderState.ZBase/GroundStory/CeilingStory/TallestStory, set by WallPosts.StartFrame); textures
repeat up from the ground. Each tile crossed is opened (OpenTile) and closed (CloseTile): an open
tile's block queues its near face and, overhead, its underside; a tile whose floor isn't the
player's queues a floor cap at its height (a run of them at one height as one post, each pixel
taking its tile's floor flat). Caps are `DrawUnderside` generalised (up or down, any story).
Everything queued is clipped to the rows nothing nearer covered yet (OpenTop..OpenBottom),
because the player's floor and the level's ceiling are still the row-by-row DrawLevelFlats pass
after the walls; the column stops once no rows are open (that replaces "go past a wall while a
taller one could show"). An opaque ceiling (no sky, no tile skies) covers rows from the top and
clips walls at its height; with a sky, rows above the tallest block at the current distance count
as covered. Runs of plain tiles (no block, floor at the player's height) are only measured when
they end, to keep flat levels fast. Arches are open tiles' overhead blocks now (OpenArch is gone);
door lintels keep CloseArch.

Checked: 14 story-1 screenshots (E1M1, tall-walls-demo, skies-demo, architecture-demo, with
pitch) pixel-identical to phase 1's build (only BJ's status-bar face differs, which animates at
random); headless screenshots at stories 2, 3 and 8 (a bridge, a raised row with flats sides,
an overhead block under a sky, rooftops). Walls cost at 1080p (E1M1): Release 1.32 → 1.61 ms,
Debug about 4.7 → 5.6 ms. Left for phase 3: doors on a story above 1 (nothing under them yet),
see-through doors, diagonals' open halves, pushwalls, wall sprites, sprites standing on lower
floors, weather and particles at height, tile floor skies on caps, the automap.

Phase 3, part 1 (two blocks per tile) done 2026-11-08. MapManager keeps a second block
(`UpperStarts`/`UpperStops`, `UpperSpan`, `DecodeBlocks`/`EncodeBlocks`, `SetBlocks`); a tile is
solid if either takes in the player's story (`SolidSpan`); `WallStories` is the higher one's stop;
pushwalls move both; `removewall arch` keeps what's above the player's story of each. `height`
takes `1-2,4-8`; `where` describes both. The renderer: OpenTile sees each block that isn't the one
a wall is solid with (SetHitBlock takes that one), queuing faces bottom up, undersides clipped to
above the block under them, and every floor that shows (each block's top below the eyes, the
ground under a block that doesn't stand on it): the highest is the tile's floor, the others are
caps clipped to below the block over them. The player's floor is drawn as a cap, not left to the
level's flats, where rows under it are still open (a gap under the block): farther things seen
through the gap would otherwise paint its rows first. Story 1 still pixel-identical (14 shots);
headless shots of a window at story 2 and a slab with a gap under it at story 5.

Phase 3, part 2 (pushwall stories) done 2026-11-08 (PushwallStoriesTests). `A_PushWall`'s fifth
argument and `A_MoveWall`'s fifth (after the object) say `all`, `above` or `story`; else game-info
`pushwall-stories` (MapManager.PushwallStories, set by LevelSetup), else all. PushWalls.SplitStories
works out the piece and what's left, and refuses (blocked sound) when what's left, or any open tile
it could stop on joined with the piece, needs more than two blocks. The piece rides on
MovingPushWall (Start/Stop/Start2/Stop2; a Stop of 0 is a whole wall with no block of its own,
which stays that way where it stops): save version 44. The tiles it crosses keep their own blocks
(nothing moves block by block any more); what's left keeps the wall id on plane 0, walked on or
under, and the areas are rebuilt (ApplyStory) once it's open. Where it stops it joins the tile's
blocks (MapManager.JoinBlocks/SetTileBlocks). Drawn at its piece's stories (WallPosts.SetHitSpan;
two parts drawn as one, gap filled, while it moves); a moving pushwall's tiles show their own blocks
as an open tile's. A classic push is unchanged: demos identical, the finished push pixel-identical.

## Later

- Lifts and stairs: an action to change the player's story, saved with the level.
- Enemies that move up and down (a helicopter): actors with a height of their own.
- Falling and death pits as engine features, if mods want more than blocking tiles.
