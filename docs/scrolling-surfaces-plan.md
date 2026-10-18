# Scrolling walls and flats

Textures that slide across walls, floors and ceilings: conveyor belts, waterfalls, moving
water, a lift shaft. From Midnight Assault, where a tile's info-plane code (plane 3 low byte,
100-103) scrolled its wall faces and flats left or right, fast (2 texels a tic) or medium
(1/3 of a texel a tic), by the frame counter.

## Midnight Assault's version (for reference)

- `INFO_SCROLLINGTEXTURES_LR_FAST/LR_MED/RL_FAST/RL_MED` (100-103) on the info plane.
- Walls (`ScrollTexture`, WL_DRAW): the wall tile's own code moves the column it draws, every
  face, left to right or right to left as seen. Door jambs don't scroll.
- Flats (`ScrollFlat`, WL_PLANE): the floor tile's code moves the flat along one map axis,
  floor and ceiling alike.
- Horizontal only, by `frameon` (the frame counter's tics), never stopped.

## PFWolf's version

A tile's **tag** (plane 4) picks its scroller; each map lists its scrollers in game-info, keyed
by tag. A switch or an actor state starts, stops, toggles or re-speeds a scroller by tag, as
the light zone actions do zones.

```yaml
maps:
  MAP01:
    scrollers:
      # the conveyor: tiles tagged 3 scroll right, Midnight Assault's fast
      3: { x: 2 }
      # a waterfall down the walls tagged 4, off until a switch starts it
      4: { y: 0.5, floor: false, ceiling: false, active: false }
```

- `x`, `y`: speed in texels a tic (64 a tile, 70 tics a second); negative goes the other way.
  On a wall face, x is to the right as you look at it and y is down. On a floor or ceiling, x
  is east and y is south.
- `walls`, `floor`, `ceiling`: which surfaces of the tagged tiles scroll (all, left out).
- `active`: scrolling from the level's start (true, left out).

What scrolls: every face of a tagged wall (diagonals too), every story of a tall one; the floor
and ceiling flats of a tagged tile. Not door jambs (as Midnight Assault), doors, lintels, arch
undersides or wall patches (they stay put on the moving wall).

Actions, for switches (walls.yaml `switch:`) and actor states alike; tag left out (or "") is
the switch's tag or the actor's own:

- `A_StartScroll([tag])`, `A_StopScroll([tag])`, `A_ToggleScroll([tag])`
- `A_SetScroll(tag, x, y)`: new speeds (and makes the scroller if the map has none for the tag)

Offsets move with the game's tics, so they stop while the game does. Saved with the level
(save v27; mid-game join's level body too).

## Phases

1. **Data, renderer, saves.** `ScrollerInfo` on game-info maps (Common); `Scrollers`
   (Rendering/Scrollers.cs, on MapManager) ticks offsets and builds per-tile offsets; WallHits
   moves the column, DrawPost the row, DrawFlats the flats. Save v27, net state v8. Tests.
2. **Actions.** The four actions above. Tests.
3. **Example + editor.** A scroller in an example mod (switch-demo), docs; the editor's 3D view
   and Definitions window later.

## Status (2026-10-18)

All three phases done on feature/scrolling-walls-flats (phases 1-2 committed as 1e4e8db).
Also a `scroll` console command (cheat): lists the level's scrollers, or
`scroll <tag> <x> [y]|start|stop|none`. Documented in pfwolf-pk3's
gamepacks/wolf3d/game-info.yaml (under the zones notes) and mapdefs/wolf3d/walls.yaml (switch
actions).

Phase 3: switch-demo's scroll room, south of the hall through an archway (layout in
examples/tools/make-switch-demo-map.ps1, now writing flats and heights too): a conveyor floor
(tag 6, x 2), a two-story waterfall wall with diagonal corners (tag 7, y 1), station 8
(`A_ToggleScroll`, its own tag) and station 9 (`A_SetScroll(7, 0, 4)` and back). Editor: the
Map Properties window has a Scrollers grid (MapProperties.Scrollers, game-info.yaml
`scrollers:`), and the 3D view animates scrolling surfaces (a per-vertex scroll velocity and a
tic clock in the shader; it keeps redrawing while any surface scrolls). The zones grid's
Ambient and Disguised columns got their boxes back.

Tests: ScrollersTests, SkyAndFlatsTests (scrolled flats), LevelMeshTests (scroll velocities),
GameInfoFileTests (writing scrollers), MapPropertiesScrollersTests.

Checked in-game: walls move right and down, floors east and south (measured); door jambs, doors
and untagged walls stay put; both stories of a tall wall and a diagonal scroll; station 8
stops the conveyor and station 9 speeds up the waterfall in play; a save made with the conveyor
stopped loads back stopped at the same spot.

Not checked: moving pushwalls scrolling, a hub level kept and returned to, the editor's 3D view
by eye (the velocities are tested, the GL drawing isn't) and its Scrollers grid by hand.
