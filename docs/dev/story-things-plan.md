# Things on any story: items, decorations and enemies off the player's story

The stories plan (docs/dev/story-level-plan.md) gives every tile blocks from one story to
another and puts the player on a story of their own. Everything else still lives on the
player's story: a barrel placed on a street tile two stories below the roofs hangs in the air
at roof level, and an enemy can't stand on the street and shoot up at the roof. This plan lets
any thing stand on any story: decorations and items drawn where they stand, and enemies that
walk, see and fight from their own story.

## Decisions (2026-11-08)

- **A thing stands on the floor under it**, by default: the player's floor where its tile is
  walkable on the player's story, else the top of the highest block below that story (or the
  ground). **A new map plane (plane 6, "thing stories") can set a story explicitly**, 1 to 8,
  painted in the editor like light zones; 0 is the default.
- **The player hits an enemy on another story by auto-aim**, as Wolf3D aims now: the closest
  enemy in the middle of the view with a clear line (in 3D) gets the shot, whatever its height
  on screen.
- **Doors and pushwalls stay on the player's story.** On other stories a door's tile is solid
  (its frame below, its lintel above), so enemies there can't use it; a pushwall only moves on
  the player's story, as now.
- **Sound by area, blasts in 3D.** Enemies hear by area as now, whatever their story (a shot on a
  roof wakes the street). Splash damage measures 3D distance.
- Not now (docs/dev/todo.md): things moving between stories (stairs, lifts, a helicopter).

## The model

A thing's **story** S is where it stands: its feet at (S − 1) × 64 texels up from the ground.
Everything on the player's story (S = P) works exactly as today, so a level with no blocks and
no plane 6 plays and draws as it always has (demos identical).

- **Spawning.** Plane 6 (0, or 1 to 8) on the thing's tile, else the floor under it: P if the
  tile's walkable on story P (a roof, open floor at the player's height); else the story just
  over the highest block top under P (a block from story 1 to 1 stands things on story 2); else
  story 1, the ground (the street under the roofs). A thing in a wall (an in-wall shooter) keeps
  P. Each actor keeps its `Story` (saved with it).
- **Drawing.** A thing is drawn (Story − P) × 64 texels up (the actors' `Z`, which only grenade
  arcs use now, stays an offset on top). One on story P clips against the nearest wall as now;
  one off it clips against whatever's queued in front of it in its column (the posts the
  renderer already lists for rain and particles, PointDrawing's occluders).
- **Solid, per story.** What's solid on story P is TileMap, as now. For any other story a grid
  per story (rebuilt with ApplyStory): a tile is solid there if one of its blocks takes the story
  in, if it's a door's or a moving pushwall's tile, or on the map's edge.
- **Who's where.** ActorAt keeps marking the player's story only. Actors on other stories mark
  a grid of their own story, so the player walks over an enemy on the street, and a guard on the
  street walks under the roofs.
- **Pickups** only on the player's story.
- **Sight and shots** follow a line in 3D: from eye height on one story to the middle of the
  other actor on its own, tile by tile as LineOfSight does, blocked where the line's height at a
  tile is inside one of that tile's blocks (or, on the player's story, where it's blocked now).
  Two actors on the same story get today's 2D check, unchanged.
- **Projectiles** fly from their shooter's height toward their target's, hit blocks in 3D
  (MapManager.InBlock) and actors by a 3D box; splash damage measures 3D distance.
- **Melee** (the knife, a dog's bite, a mutant's swipe) only reaches the same story.

## Phases

1. **Thing stories: data.** Plane 6 (MapConstants.LevelPlanes 6 → 7: map loading and saving in
   WAD, GAMEMAPS and UWMF — a thing's `story` there — and the save format), the default (floor
   under it), Actor.Story set at spawn and saved (save version bump), `where` and a console
   cheat to move a thing's story, pickups on the player's story only. Tests; demos identical.
2. **Drawing things at their story.** Sprites and wall sprites raised (or lowered) by their story,
   clipping against the posts in front of them off the player's story; the automap marking
   things on other stories apart. Headless screenshots at story 1 identical, a street of barrels
   under the Rooftops Demo's roofs.
3. **Enemies walking their story.** The per-story solid grids and actor grids; Monster movement
   (TryWalk, CheckSide, CheckDiag, ActorMark) on its own story; the player's movement ignoring
   actors on other stories; patrols on their story; chasing as now, toward the player's tile on
   their own story's map. Tests; demos identical.
4. **Seeing and fighting across stories.** The 3D line (LineOfSight), enemies' sight and shots,
   the player's auto-aim and hitscan, projectiles and splash in 3D, melee same-story only,
   damage falloff by 3D distance. Tests; play-tested on the Rooftops Demo.
5. **Editor and examples.** Plane 6 in the editor (brush, badges, tile info), the 3D view placing
   things at their story, map checks (a thing inside a block on its story, an item off the
   player's story that can't be reached), the Rooftops Demo with guards on the street and an
   item on the sign's beam, docs, manual-testing items.

## Status

Phase 1 done 2026-11-08 (ThingStoriesTests). MapConstants.LevelPlanes is 7: plane 6
(`StoryPlane`) holds each tile's thing story, read and written by every map format (WAD,
GAMEMAPS as empty, UWMF as a thing's `story`, a UWMF extra thing's own `MapThing.Story`).
`Actor.Story` is 0 for the player's story (most actors, so they follow the `story` cheat) or the
story it stands on; MapManager.SpawnThing sets it from `ThingStory` (plane 6, else the floor under
it), and `StoryOf` / `OnPlayersStory` read it. Saved with the actor (save version 45); the network
protocol is 16 (it also takes in save 44's pushwall stories in the mid-game join's level body,
which part 2 of the stories plan should have bumped). Items are only picked up on the player's
story. `where` lists the faced tile's things and their stories; the `thingstory <0-8> [x y]`
cheat sets plane 6 on a tile and moves its things.

Phase 2 done 2026-11-08. A sprite (and a wall sprite) off the player's story is drawn its
story's height over or under the player's floor (SpriteDrawing.PlaceOnStory adds it to the
sprite's Z) and hidden pixel by pixel: on a frame with any thing off the player's story
(SpriteDrawing.AnyOffStory) the renderer keeps a depth per pixel (RenderState.Depth, as WallHeight:
higher is nearer). Each column starts from DepthRows (the player's floor below the horizon, the
level's ceiling above it where it's opaque, else 0 for the sky: what DrawLevelFlats fills), and
DrawPost and DrawUnderside write theirs; ScaleColumn skips a pixel that's nearer than the sprite.
Sprites on the player's story still clip against the nearest wall, so story 1 is pixel-identical
(14 shots). The automap draws a thing off the player's story as an outline. Checked by headless
shots: E1M1's corpse at the bottom of a drop on story 2; a plant and two lamps on the Rooftops
Demo's street, the lamps hidden by the bridge's deck but for their tops.

Phase 3 done 2026-11-08 (ThingStoriesTests). ActorAt[] stays the player's story's. A story with
things off the player's story gets a grid of its own (MapManager.StoryGrid) for its solid things
and enemies, and `StorySolid(x, y, story)` says what's solid there: a block taking the story in
(the spans), a door's tile, a moving pushwall's, the map's edge. `ActorOn(x, y, story)` puts them
together as ActorAt[] has it, so Monster's walking (TryWalk, CheckSide, CheckDiag, CanPassTile for
A*, Blake Stone's patrol checks and ClipMove, the step aside, rising, a Spectre's return) reads its
own story through `TileEntry`, and MarkActorTile / UnmarkActorTile(monster) mark its story's grid.
The player's movement, projectiles and doors read ActorAt[] alone, so they don't feel anything off
the player's story; an enemy off it isn't stopped by the players (MoveObj). Things spawned by an
actor (death drops, A_Replace, a morph) take its story; SpawnThing takes a story. The grids aren't
saved: a load (and `story`, `thingstory`: MapManager.RebuildStoryGrids, MoveToStory) puts their
things back. `actors` shows a thing's story when it's off the player's. Checked headless: E1M1
on story 2 with `wallheight 1`, two patrollers moved to story 1 (`thingstory 1`) walk their paths
tile for tile as they do on the player's story, and the third stops at the door it opens there.

Approximations: an enemy off the player's story keeps the area of the tile it last had one on (a
wall tile it walks on top of has none), so sound and sleep go by that; chasing heads for the
player's tile, which may not be on its story's floor (it gets as near as it can). An A_BlockTile
mark off the player's story isn't saved.

## Later

- Things moving between stories: stairs and lifts (with the player's), enemies that fly up and
  down (a helicopter), falling.
- Doors and pushwalls on stories other than the player's.
