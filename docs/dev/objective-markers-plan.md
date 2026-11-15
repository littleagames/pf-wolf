# Objective markers plan

In King of the Hill, Domination and Capture the Flag it's hard to know where the objective
is. The hill moves every minute ("The hill has moved!") and nothing says where it went. The
automap only shows a flag once you've seen its floor. This plan adds **objective markers**:
an icon over the objective in the 3D view, seen through walls, and an arrow at the edge of
the view when it's off to the side or behind you.

## Concept

```
 ┌──────────────────────────── 3D view ────────────────────────────┐
 │                Red 12   Blue 9    4:31                          │  match line (as now)
 │                Hill: Blue   moves in 0:42                       │
 │                                                                 │
 │◄B                          ◆A                                   │  B is off to the left
 │ 14                         Red                                  │
 │                            8                                    │  A is ahead, 8 tiles
 │                                                                 │
 │                                                       C►        │  C is off to the right
 │                                                       22        │
 └─────────────────────────────────────────────────────────────────┘
```

- **In view.** A small diamond floats over the objective's tile, at about head height. Under
  it: a short label (`A`, `Hill`, `Flag`, `Base`) and the distance in tiles. It's drawn over
  everything, so you see it through walls. It's colored by the team that holds it (red, blue,
  or white for nobody's).
- **Out of view.** The marker is pinned to the left or right edge of the view, with an arrow
  pointing the way to turn. Something behind you goes to the side that's the shorter turn.
  Several markers on one edge stack so they don't overlap.
- **Wolf3D only turns left and right**, so the left/right position is all that matters for
  direction. Height on screen is only for looks (Y-shearing still moves it up and down).
- **Draw-only.** Each machine works out its own markers from the objective's state, which
  every machine already has (lockstep). No net protocol, no save version, no TicCmd changes.

### What each mode shows

| Mode | Markers |
|---|---|
| King of the Hill | The hill: label `Hill`, holder's color. It pulses for 5 s after it moves. In the last 10 s before a move, the label shows the countdown. |
| Domination | Points `A` `B` `C`, each in its holder's color. While one is being taken, the label shows its progress (`B 40%`). |
| Capture the Flag | Depends on what you're doing (below). |
| Heist | None for now (loot is everywhere). |
| Horde | Optional, phase 3: when 3 or fewer enemies are left, a marker on each, so the last stragglers can be found. |
| Prop Hunt, DM, TDM | None (a marker would give hiders away). |

CTF, from the viewer's side:

- **The enemy flag**: always marked. At home: `Flag` at their base. Dropped: `Flag` where it
  lies, with its return countdown. Carried by your teammate: on the carrier, `Escort`.
- **Your own flag**: only marked when it isn't home. Dropped: `Return`. Carried by an enemy:
  on the carrier, `Kill`. (Showing the carrier is standard CTF and stops a capture from
  going unseen.)
- **You carry the enemy flag**: your own base is marked `Capture` (or `Base` while your flag
  is away, as you can't score until it's back).

A watching player (dead, or `spectate`) sees the markers from the camera's view and team.

## How it fits the code

| Need | Already there |
|---|---|
| Where objectives are | `ControlZones.Zones` (`At`, `Owner`, `Progress`), `KingOfTheHill.Hill`/`MovesAt`, `CaptureTheFlag.Flags` (`Home`, `At`, `State`, `Carrier`) |
| Per-mode extension point | `IMatchObjective` default methods (`StatusLine`, `TeamHome`, ...) |
| World to view pixels | `PointDrawing.ProjectPoint` (uses `RenderState.ViewX/ViewY/ViewCos/ViewSin/Scale/CenterX/CenterY`) |
| Drawing over the view | `NetOverlay.DrawNetOverlay` → `DrawMatchLine`, in 320x200 layout coords; `ViewRect` and `VideoManager.ToLayoutX/Y` convert from screen pixels |
| Team colors | `flag-red` / `flag-blue` / `flag-neutral` translations, `#FC0000` / `#0000FC` already used by Heist's rope |
| Options | Options → Game Options (`setting-defaults`) |

## Phases

### Phase 1: markers in the view (KOTH, Domination, CTF) (done 2026-11-14, b19f7da3)

As built: `Gameplay/ObjectiveMarker.cs` (`ObjectiveMarker`, `MarkerStyle`), `IMatchObjective.Markers`
in KingOfTheHill / Domination / CaptureTheFlag, `Rendering/ViewProjection.cs` (PointDrawing
delegates to it), `Ui/ObjectiveMarkerDrawing.cs` (drawn first in `GameHost.DrawNetOverlay`), and
`GameOption.ObjectiveMarkers` (`hud_markers`, game-info `setting-defaults: objective-markers`,
Game Options → Objective Markers, default on). The edge position is the view's middle row
(stacked when several share an edge); a marker within a tile of the view is left out. Distance
is straight-line tiles from the camera. Checked: unit tests (MatchScoringTests, in
GameModeTests.Markers.cs) and headless DM02 screenshots (KOTH ahead and behind, Domination with
markers in view and on both edges, CTF blue flag, `hud_markers 0`). Not yet checked: the Game
Options menu with its ninth row, a carried or dropped flag in real play, the pulse by eye,
widescreen and UI scales, spectating.

- `Gameplay/MatchModes/ObjectiveMarker.cs`: `readonly record struct ObjectiveMarker(long X,
  long Y, string Label, int Team, MarkerStyle Style)`. X/Y in global units, so a carried
  flag can follow its carrier's pawn smoothly. `Style` flags: `Pulse`, `Small` (your own
  base, quieter).
- `IMatchObjective.Markers(PlayerState viewer)` → `IEnumerable<ObjectiveMarker>`, default
  empty. Implement it in `KingOfTheHill`, `Domination` and `CaptureTheFlag` (re-list
  `IMatchObjective` already done on each). `KingOfTheHill` keeps a `MovedAt` time for the
  pulse; it's derived from `MovesAt - HillTics`, so nothing new goes in `Write`/`Read`.
- Move the projection math out of `PointDrawing.ProjectPoint` into a static
  `ViewProjection` helper (pure: render state in, screen x/y out), so markers and points share
  it. PointDrawing calls the helper; behavior unchanged.
- `Ui/ObjectiveMarkers.cs` (drawn from `NetOverlay.DrawNetOverlay`, after the match line):
  - Project each marker at eye height. In view (`nx >= MinDist` and x inside the view with
    a margin): draw at that x/y, clamped inside the view.
  - Otherwise: work out the bearing from the view angle; pin to the left or right edge, at
    the view's middle row, stacking by bearing. Draw an arrow.
  - Icon: a 7x7 diamond outline + fill drawn with `video.Bar` rows (no new art), label and
    distance in `SmallFont`. Sizes follow UI scale through the layout coordinates.
  - Hidden while the automap is open, in Prop Hunt's waiting view, and in single player.
- Option: Game Options → `Objective markers: On / Off`, default On.
- Tests (PFWolf.Tests): `ViewProjection` (ahead, left, right, behind, too close); edge
  placement from a bearing; each mode's `Markers` list (CTF: every viewer/flag-state case,
  KOTH pulse and countdown, Domination progress label).
- Check: headless `--warp DM02 --exec "wait; addplayer; gamemode koth; ..."` screenshots
  facing toward, away from and sideways to the hill; same for CTF and Domination.

### Phase 2: the automap (done 2026-11-14)

As built: `ObjectiveMarker.Zone` (tiles out from the middle; KOTH and Domination give their
radius, 1), `ObjectiveMarkerDrawing.Current()` (the markers to show, shared by the view and the
automap), and `AutomapDrawing.DrawAutomapObjectives` (after the actors, under the player's
arrow): each zone outlined in its holder's color, then a diamond and a label on a backdrop box
for each marker, seen or not. One beyond the part of the map in view is pulled in along the line
from the middle to the view's edge (`PinInside`), kept under the match's line, its label on the
inside. The same Objective Markers setting turns it off. A carried flag shows where its carrier
is (the carrier's marker), not as a mark on the player arrow. Checked: unit tests for the pinning
and zones, headless DM02 automap shots in Domination, KOTH and CTF (color style) and Domination
in the graphic style and overlay mode. Not yet checked: rotation, panning, other zooms.

- Objective spots are always shown on the automap, seen or not: the zone's square outlined
  (3x3 for KOTH/Domination) in its holder's color, CTF bases and flags, a carrier as a
  flag mark on the player arrow.
- Reuses `Markers(viewer)` plus a zone radius from the objective, so phase 1's per-viewer
  rules apply here too (no seeing your own flag's carrier on the map unless phase 1 shows it).

### Phase 3: polish (done 2026-11-14)

As built, with two changes from the plan below:
- **Seen or hidden** uses the map's `Spotvis` (the tiles the view could see this frame) rather
  than PointDrawing's occluders: a marker's tile in sight draws filled, else hollow (a ring
  in the outline color and one in the team's). Over the view only; the automap draws them filled.
- **Look settings** went in game-info `objective-markers:` (`ObjectiveMarkersInfo`: red, blue,
  neutral, their labels, outline, flash, flash-tics, show-distance, distance-color, font,
  height), not hud-messages.yaml: that file is a plain list of message styles, and game-info
  already holds blocks like `puzzle-panel:` that mods change. The automap uses the colors and
  flash; it keeps its own font. KOTH's 5 s flash after a move stays a constant.
- **Horde stragglers**: `horde: mark-last` (default 3, 0 for never). With none still to come
  and that many or fewer alive, each enemy gets a marker with no label (they'd all say the
  same and crowd the view), in red.
- **Stories**: `ObjectiveMarker.Story` for actors (a flag's carrier, an enemy); a tile marker
  is on its tile's floor (`MapManager.ThingStory`). It floats `height` over its own story's
  floor, and a marker on another story than the view's has a small arrow beside its diamond
  pointing up or down. Over the view only.

Checked: unit tests (look colors and flashing, story heights and hints, stragglers), headless
shots on DM02 (Domination with hidden B and C hollow) and with a scratch mod raising mark-last
and turning markers magenta without distances: Horde on DM02 (filled and hollow in view, three
on an edge, on the automap) and on the Rooftops Demo (mutants in the street two stories down
with down arrows, floating lower than one on the roof). Not yet checked: an up arrow, a
carried flag on another story, a mod font.

The plan as first written:

- **Seen or hidden.** A marker whose spot is in plain sight draws filled; one behind a wall
  draws as an outline. Uses the occluders `PointDrawing` already notes per column.
- **Look settings** in `hud-messages.yaml` (a new `objective-markers:` block): colors, font,
  show distance, edge margin, pulse time. A mod can restyle markers without code.
- **Horde stragglers**: markers on the last few enemies (count from game-info `horde:`).
- **Stories**: a marker on another story (rooftops) gets a small up or down chevron.

### Phase 4: beyond match modes (done 2026-11-14)

As built: markers now show in single player and co-op too. `ObjectiveMarkerDrawing.Current()`
gathers a match mode's markers, the level objectives' (`LevelMarkers`) and the scripts'
(`ScriptMarkers`). `ObjectiveMarker.Color` gives a marker its own color in place of its team's.
- **Lua** (`Scripting/GameScripts.Markers.cs`): `game.marker(id, x, y, label[, color])` on a
  tile, `game.marker(id, actor, label[, color])` following an actor until it's removed, dies or
  is taken off the level (a picked-up item isn't flagged removed, so the list is checked), and
  `game.clearmarker([id])`. color "red"/"blue" is a team's look, anything else a color of its
  own. Setting an id again moves it in its place. Markers belong to the level they were set on
  (LevelSerial) and aren't saved: scripts set them in `game.onlevel`, which a load runs too.
  Listed in ScriptApi.cs.
- **Level objectives** (`Objectives.Markers`): game-info objective `marker: true`, with
  `marker-label` (text or $key) and `marker-color`. Each switch not yet thrown (a wall's or a
  patch's whose switch has the objective; thrown, it's its `to`), destructible wall not yet
  broken, and actor with the objective still alive. The tile scan is cached until the level or
  the objectives' done total changes. A switch with no `to` stays marked until its objective is
  all done.
- **Mission Demo**: radios and officers by game-info, the documents and (once the exit's open)
  the exit by its script; README updated.

Checked: unit tests (script markers: order, move, clear, level, actor gone/dead/picked up,
colors, off-map error; level markers: switches thrown or not, walls, living actors, done or
unmarked objectives) and headless Mission Demo shots (all four kinds in view, at the edges and
on the automap; after wrecking both radios and taking the documents, only the exit and the
officers). Not yet checked: a loaded game re-marking, co-op, patch switches in a level.

The plan as first written:

- Lua: `game.marker(id, x, y, label, color)` / `game.clearmarker(id)`, drawn by the same
  code, so mods can point at their own goals (hub doors, puzzle panels).
- Single-player level objectives (`Gameplay/Objectives.cs`): a game-info objective option
  `markers: true` marks each remaining switch or actor that counts toward it.

## Open decisions

1. **Edge arrows or a compass strip?** Recommended: edge arrows (above). A compass strip
   across the top (markers placed by bearing, like many modern shooters) is the alternative;
   it needs room under the match line, which already takes 20 px at the top of a 200 px view.
2. **Through walls always?** Recommended: yes, it's the point of the feature. Phase 3 makes
   hidden ones look different.
3. **Default on?** Recommended: on, with the Game Options switch to turn it off.
4. **Show enemy carriers of your flag?** Recommended: yes (`Kill` marker on the carrier).
