# Puzzles (mini-games)

Small puzzles the player plays in the middle of a level to get something done: hack a door
panel, pick a lock, crack a safe, reroute power, turn a turret. Each one ends in **success**,
**fail** or **cancel**, and each result can set off things in the level (open a door, sound an
alarm, hurt the player, switch an enemy's side). Like BioShock's hacking: BioShock 1 stops the
world while you route the pipes, BioShock 2 keeps it going while you time the needle.

The aim: **the engine gives the frame, Lua makes the puzzles.** Every puzzle is a Lua script.
The engine only does what a script can't do by itself, and ships no puzzles of its own: they
live in the `puzzles-demo` mod, for mods to copy.

Named `puzzle` everywhere in YAML, Lua and the docs (`game.puzzle`, `A_Puzzle`, a switch's
`puzzle:`).

## Decisions (2026-11-06)

1. **The level freezes by default** while a puzzle is open playing alone (`pause: true`); a
   puzzle can set `pause = false`. With others it never freezes.
2. **No puzzles in pfwolf-pk3.** The library lives in the `puzzles-demo` mod; mods copy the
   scripts they want.
3. **Cancel (Esc) counts as nothing** by default: no result, try again. A puzzle can set
   `cancel = "fail"` or `"never"`.
4. **Built-in outcome actions**: `A_Alarm`, `A_HurtPlayer(n)`, retry delay / tries / broken on
   the switch, and a side switch (`A_SetFaction` as a trigger action on tagged actors). Anything
   else is left to scripts.
5. **Cursor from movement**: mouse and stick deltas (`ControlX`/`ControlY`), no absolute
   pointer. Works the same on every machine and in demos.
6. **Auto-hack item** (`skip-item`) is in phase 2.
7. **Per-game panel look**: game-info picks the panel's frame and font, so Blake Stone's
   panels look like Blake's and Wolf3D's like Wolf3D's.

## How big a lift

**Medium**, about the size of particles. About 1,200–1,500 lines of C#, plus the demo mod's Lua
puzzles. Most of what's needed already exists:

| Needed | Already there |
|---|---|
| Starting it from a switch, a trigger, an actor's state | switch `actions:` (walls.yaml, patches), `MapTriggerRegistry`, `ActorActionRegistry`, `game.trigger` |
| Starting it from a script on use | `game.onuse` (returning true takes the press) |
| Results that act on the level | every trigger action (`A_OpenDoor`, `A_Activate`, `A_SetZoneLight`, `A_MoveWall`...), run with the switch's tag and tile |
| Changing an enemy's side | `Monster.SetFaction` (today only `A_SetFaction` in an actor's own state, Entities/Actors/Monster.cs) |
| Drawing over the view | `game.onhud`'s `drawtext`/`drawpic`; `VideoManager.Bar`, `DrawLineScaledCoord`, `ShadeRegionScaledCoord` |
| Code that may only draw | `_localHook` / `ChangesTheGame` (Scripting/GameScripts.Hooks.cs) |
| Keys for digits, directions, use, fire, back | `ButtonType`: Slot0–9, MoveForward/Backward, TurnLeft/Right, StrafeLeft/Right, Use, Attack, Esc |
| A cursor that follows the mouse | `TicCmd.ControlX/ControlY` (mouse and stick movement) |
| Freezing the world, playing alone | the console's `worldPaused` in `LevelLoop` |
| The same game on every machine and in a demo | lockstep `TicCmd` (Networking/NetPlay.cs), demos replaying controls |

What's new: a runner for an open puzzle, a Lua API to define and drive one, a few drawing
functions, the panel's look in game-info, results on switches, retries and lockouts, three
actions, and saving an open puzzle.

## The key design: a puzzle is played through the player's controls

There are two ways to build this.

**A. Played on this machine, the result sent on.** The puzzle reads the keyboard directly and
runs only on the player's computer. When it ends, the result goes to the other machines in a new
network message. Simple for a single player, but a demo can't replay it, a game with others
needs a new message type and an exact tic to act on, and nobody else's machine knows the puzzle
is open.

**B. Played in the game's own think, from the player's `TicCmd` (chosen).** While a player
has a puzzle open, their controls drive the puzzle instead of their legs and gun. The
puzzle's `tick` runs in the simulation every tic, on every machine, from the same controls, so
it comes out the same everywhere. Only `draw` is local, and it can only draw.

B costs almost nothing in networking: controls already go everywhere. Demos replay it as long
as the puzzle uses what a demo keeps (the first eight buttons: attack, strafe, run, use, slots
1–4, plus turn and forward/back). `game.random` works in `tick`, because it runs the same on
every machine. The price is that a puzzle's state lives in a Lua table that has to be saved
(phase 3) and sent to a player who joins mid-game.

So:

- **`tick` changes the game** (it's in the simulation, like `ontick`), and ends the puzzle.
- **`draw` is local** (like `onhud`): drawing only, so `ChangesTheGame` makes anything else an
  error there.
- **The result** runs the switch's or trigger's actions at that tic, with the tag, tile and
  player it was started with, on every machine.

## What the player sees

- Using a puzzle switch opens the puzzle over the view, with the view dimmed behind it
  (`ShadeRegionScaledCoord`) and the game's panel frame around it. The panel is a 320×200
  layout scaled by UI Scale and centered, so puzzles don't need to know the screen size.
- The player can't move, turn, fire or use while it's open. Their controls go to the puzzle.
- Esc backs out (**cancel**); the menu comes up only after that. Cancel counts as nothing
  unless the puzzle says otherwise.
- `pause` (on by default) freezes the level while it's up, as BioShock 1 does. In a game with
  others the level can't stop for one player, so `pause` is ignored there and the player can be
  shot while they work (BioShock 2). Other players see them standing still, and a HUD line can
  say "Alice is hacking" (the puzzle's `verb`).

### The panel's look (game-info.yaml)

```yaml
puzzle-panel:
  frame: menu            # menu: the game's own menu window (Wolf3D's blue box, Blake's
                         # frame); or a picture name, drawn 9-sliced; or none
  font: small            # the font puzzles get by default (fonts.yaml)
  title-color: Yellow    # the title line over the panel
  dim: 0.5               # how dark the view behind gets
```

Wolf3D, Spear and Blake Stone each get one in their base pack. A puzzle can still draw over the
whole panel, and `game.pack()` (or the like) tells it which game it's in.

## Defining one (Lua)

Puzzles are named and added when a script loads, like hooks, so every machine has the same
ones:

```lua
game.puzzle("pipes", {
  title = "Bypass the lock",
  verb = "hacking",           -- "Alice is hacking" for others
  pause = true,               -- the default: freeze the level (playing alone)
  cancel = "nothing",         -- the default; "fail" | "never"
  timelimit = 35 * 30,        -- tics; running out is a fail (nil: no limit)

  start = function(pz, args)  -- pz.state is the puzzle's own table; args come from the trigger
    pz.state.grid = makegrid(args.size or 5)
  end,

  tick = function(pz, input)  -- each tic; return "success" / "fail" to end it
    if input.pressed.left then pz.state.cx = pz.state.cx - 1 end
    if input.pressed.use then rotate(pz.state) end
    if flowed(pz.state) then return "success" end
  end,

  draw = function(pz, width, height)  -- each frame, on this machine alone
    game.drawrect(0, 0, width, height, "#101818")
    drawgrid(pz.state)
  end,
})
```

**`pz`**: `state` (the puzzle's table), `name`, `player`, `x`, `y`, `tag` (where it was started),
`args`, `tics` (since it opened), `timeleft`, `pz:sound(name)` (heard only by the player
working it), `pz:finish(result)` (the same as returning a result from `tick`).

**`input`** (from the player's `TicCmd`, the same on every machine):
- `held.up/down/left/right` and `pressed.up/...` (just pressed this tic), from MoveForward/Back,
  TurnLeft/Right, StrafeLeft/Right, and turn/forward movement (so demos have them)
- `held.use`, `pressed.use`, `held.fire`, `pressed.fire`, `pressed.cancel`
- `digit`: 0–9 for a slot key just pressed (keypads), else nil
- `dx`, `dy`: cursor movement from the mouse or stick (`ControlX`/`ControlY`)

**New drawing functions** (in `draw` and in `onhud`, panel or view coordinates):
`game.drawrect(x, y, w, h, color)`, `game.drawframe(x, y, w, h, color)`,
`game.drawline(x0, y0, x1, y1, color[, thickness])`, `game.drawshade(x, y, w, h, amount)`,
`game.drawpic(name, x, y[, sx, sy, sw, sh])` (a cut-out of a picture: sliding puzzles),
`game.textwidth(text[, font])` (centering). Colors work as they do now (names or `#RRGGBB`, the
palette's nearest).

**Starting one from a script**: `game.startpuzzle(player, "pipes", args)` in a simulation hook
(`onuse`, `ontick`...), never in a local one. **Results**:
`game.onpuzzle(function(player, name, result, x, y, tag) ... end)`.

## Starting one from a map (YAML)

A trigger action, for switches, mapdefs triggers, actor states and `game.trigger`:

```
A_Puzzle("pipes", "size=6")
```

On a switch (walls.yaml or a patch), the switch is thrown (`to:`) **only on success**, and each
result has its own actions:

```yaml
'70':
    North: TERMINAL
    # ...
    switch:
        to: 71
        puzzle: pipes
        puzzle-args: { size: 6 }
        actions:                      # success
            - A_OpenDoor("hold")
            - A_SetFaction("allies")  # the tagged turret now fights for the player
        fail:
            - A_Alarm                 # every enemy in the area comes
            - A_HurtPlayer(10)
        cancel: []                    # nothing, unless a list is given
        retry: 350                    # tics before it can be tried again after a fail
        tries: 3                      # fails before it breaks for good...
        broken: 72                    # ...and becomes this wall
        skip-item: AutoHackTool       # used up for an instant success (BioShock's auto-hack)
```

Retries, lockouts and broken switches are per tile, and saved.

### New actions

- **`A_Alarm`** (trigger and actor action): every enemy in the activator's area (and the areas
  joined to it by open doors) hears the player, as a shot does (`MadeNoise`) but for the whole
  area at once. An optional sound.
- **`A_HurtPlayer(n)`** (trigger action): `n` damage to the player who set it off, as from
  nothing (no killer, its own obituary).
- **`A_SetFaction("faction"[, "follow"])` as a trigger action**: today it only works in an
  actor's own state. The trigger version calls `Monster.SetFaction` on each actor with the tag,
  the same way `A_Activate` acts on tagged actors (Gameplay/SwitchActions.cs).

## Puzzle ideas (all Lua, in the demo mod)

| Puzzle | How it plays | Controls | Fits |
|---|---|---|---|
| **Pipe flow** | Turn tiles to lay a path before the flow reaches the end (BioShock 1) | arrows + use | doors, safes, turrets |
| **Timing needle** | A needle sweeps; press use in the green, red shocks you, blue is a bonus (BioShock 2) | use | quick hacks, not paused |
| **Keypad** | Type a code found on a note (`game.showtext`), a body, a radio log | digits | locked rooms, hub levels |
| **Safe dial** | Turn the dial; a click sounds near each number (`pz:sound`) | left/right + use | safes with treasure |
| **Lockpick** | Find the sweet spot; turning the lock too hard breaks a pick (an item) | left/right + use | doors, chests |
| **Wire cut** | Cut the right wires from clues; the wrong one is a fail | arrows + use | bombs, alarms |
| **Sequence (Simon)** | Repeat a growing pattern of lights and tones | digits / arrows | security panels |
| **Lights Out / switch grid** | Flip tiles until all are lit | arrows + use | power rooms (Blake Stone) |
| **Sliding picture** | Put a scrambled wall texture back together (`drawpic` cut-outs) | arrows | secrets |
| **Frequency tuning** | Line up two waves with the cursor | mouse / stick | radios, jammers |
| **Code breaker** | Mastermind: guess a code from hits and near misses | digits | hard locks |
| **Power routing** | Send power to two of three systems (lights / doors / turrets) | arrows + use | a choice, not pass/fail |

Results don't have to be pass or fail. Power routing returns a choice, and `game.onpuzzle`
gets whatever `tick` returned (`"lights"`, `"doors"`), so a script can act on it.

## What's in the engine, what's in Lua

**Engine (C#), the things a script can't do:**
1. **The runner** (Gameplay/Puzzles.cs): the open puzzle for each player, open and close,
   time limit, cancel, the switch's retries, tries and broken state.
2. **Taking the player's controls**: the player's think hands their `TicCmd` to the puzzle and
   skips moving, turning, firing and use; their weapon is lowered.
3. **Freezing the world** for `pause` when playing alone: actors, doors and timers don't think,
   the puzzle does; `TimeCount` and the level timer don't count it.
4. **Drawing**: the dimmed view, the panel's frame, place and scale (game-info
   `puzzle-panel`), the new `game.draw*` functions, and Esc going to cancel before the menu.
5. **Results into the level**: the switch's `puzzle` fields, `A_Puzzle`, result actions run
   with the activation they were started with, `game.onpuzzle`, and the three new actions.
6. **Saving and joining** (phase 3): the open puzzle and its `state` table saved (plain data:
   numbers, strings, booleans, tables), and sent to a player joining mid-game.

**Lua, everything else:** every puzzle's rules, look and sounds; outcomes beyond what YAML can
say. A modder makes a new puzzle with no engine change.

## Phases

**Phase 1 — runner and API, playing alone (done 2026-11-06).** What it came to: Gameplay/Puzzles.cs
(the runner, input, freeze, A_Puzzle), Scripting/GameScripts.Puzzles.cs (game.puzzle, game.startpuzzle,
the pz and input tables, game.drawrect/drawframe/drawline/drawshade/textwidth), Ui/PuzzlePanel.cs (the
panel), game-info `puzzle-panel` (Wolf3D and Blake base packs), the `puzzle` command, PuzzleTests, and
`examples/mods/puzzles-demo` with the needle. Not done here, moved to phase 4: `game.drawpic` cut-outs.
Directions come from the movement/turn/strafe buttons and the stick's forward and strafe push, not
the mouse's turn (that's `dx`).

**Phase 1 — as planned:** `game.puzzle`, `game.startpuzzle`, `pz`, `input`,
the new drawing functions, taking the controls, `pause`, cancel and time limit, `A_Puzzle`,
`game.onpuzzle`, the `puzzle-panel` block (Wolf3D, Spear and Blake base packs), and a
`puzzle <name> [args]` console command (cheat) to try one. One puzzle, the timing needle, in a
test mod. Refused in a demo and with others until phase 2 (as `game.showtext` is). Saving with
one open saves without it. Tests: the runner, input edges, `ChangesTheGame` in `draw`, results
running the actions.

**Phase 2 — switches, outcomes, others (done 2026-11-06).** What it came to: the switch fields
on MapSwitchTranslation (walls and patches), Gameplay/PuzzleSwitches.cs (fails, lockouts,
broken, skip item; saved: save version 42), PlayerUse split into Use/Throw so only a success
throws, A_Alarm (ambushers in the players' reach woken, noise this tic and the next the world
moves: GameSession.AlarmPending), A_HurtPlayer, A_SetFaction for switches. Allowed in demos
(ReadKeys reads only the walk/turn, use/fire and slots 1-4 there; no Esc) and with others (no
net version bump: puzzles ride the TicCmd as it is). A joiner closes every open puzzle as
"cancel" on every machine at the join step (AddNetPlayer), since it isn't in the join state; the
puzzle switches' lockouts aren't sent to a joiner either (nor are timed switches). The others'
line is drawn by Ui/PuzzlePanel.cs. Tests: PuzzleSwitchesTests, PuzzleGameTests (a scripted
demo through a switch: win, fail, lockout, alarm, faction).

**Phase 2 — as planned:** The switch block's `puzzle`, `puzzle-args`, `fail`,
`cancel`, `retry`, `tries`, `broken`, `skip-item` (walls.yaml and patches); retries and broken
state saved (save version bump); `A_Alarm`, `A_HurtPlayer`, trigger `A_SetFaction`; allowed with
others and in demos, `pause` ignored with others; the "is hacking" line for others (net version
bump if needed). Tests: a scripted headless demo that plays a puzzle and checks the door opened
(the same trace twice), each new action, lockout and broken.

**Phase 3 — saving and joining mid-game (done 2026-11-06).** What it came to: the switch a
puzzle's for is data (SwitchPuzzle: patch, tile, dir, tag, wall/patch id, keys) rather than a
callback, so its result finds the switch again by id (PuzzleSwitches.Ended); Puzzles.Write/Read
keep an open puzzle (name, tile, tag, tics, held keys, errors, its switch) and the script's
pz.state and pz.args as plain data (GameScripts.WritePuzzleState: booleans, numbers, strings,
tables of them; a function, an actor, a loop or more than 32 deep is refused, naming the key,
and the puzzle's left out of the save). Save version 43 and join state version 18 carry it (and
the join state now carries the puzzle switches' lockouts too). A joiner closes only the puzzles
that can't be sent (Puzzles.CloseUnsendable, the same answer on every machine). The "nothing
left open" reset moved from PlayLoop to SetupGameLevel, so a loaded or joined game's puzzle is
put back after it. A player dying closes their puzzle. Note `wait` in the console doesn't count
while a pausing puzzle stops the level. Tests: PuzzleTests (kept and taken up, a function
refused, joiners, death, a puzzle gone from the scripts), PuzzleGameTests (saved and loaded in a
game).

**Phase 3 — as planned:** The open puzzle and its `state` table in the save
(and in a joiner's state). A table that holds functions or actors gets an error that names the
key.

**Phase 4 — the `puzzles-demo` mod (done 2026-11-06).** What it came to: ten more puzzles
(pipes, keypad, safedial, lockpick, wires, sequence, lightsout, sliding, frequency, power), one
script each; game.drawpic cut-outs and game.picsize (GraphicManager.DrawPicRegion was there); a
level made by examples/tools/make-puzzles-demo.ps1 (a hall of eleven terminals, walls 64-74,
each opening a gold-locked closet; the note, a prisoner, a barracks for the alarm, the auto-hack
tool, thing 590); texts/PZNOTE.txt; the README section; the tutorial docs/manual/tutorials/first-puzzle.md
(its code is run by PuzzleDemoScriptsTests). Found in play and fixed: a panel taller than the
view left its frame on the border and status bar, which aren't redrawn every frame, so the
default panel is 240 x 128 and the play screen is drawn again as a panel goes
(PuzzlePanel's redrawPlayScreen). pz:sound goes through GameScripts.PuzzleSound when set
(tests). Tests: PuzzleDemoScriptsTests plays, draws, keeps and takes up every demo puzzle.

**Phase 4 — as planned:** Pipe flow, keypad, safe dial, lockpick, wire cut,
sequence, Lights Out, sliding picture, frequency tuning, power routing as scripts in the mod,
written to be copied (one file each, no shared helpers they can't live without). A level with
one station per puzzle, a note with the keypad code, a turret to turn, an alarm to set off. The
docs (Lua reference, switch fields, the new actions, a tutorial: "Make your own puzzle"), and
entries in manual-testing.md.

**Phase 5 (if wanted) — editor.** The switch block's new fields in the walls editor, and
"Try this puzzle" from a switch's right-click menu (runs it in the game with `--warp`).
