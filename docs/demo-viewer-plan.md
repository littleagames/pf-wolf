# Demo viewer (editor)

A browser in the editor for the game's demos: what was pressed, and, by having the game
play the demo, where everyone went.

## What a demo is

id's format, unchanged: byte 0 is the map less one (0 = MAP01), bytes 1-2 the length
(counting the 4-byte header), byte 3 padding, then 3 bytes a frame: a buttons byte (bit n
is ButtonType n: Attack, Strafe, Run, Use, Slot1-4), a signed turn and a signed move. Each frame
is DemoTics (4) tics of 70 a second. They come from the data files' DEMO0-3 chunks and from
recorded DEMO0-9.dmo files in the demos folder.

A demo holds only the controls, so where the player went takes playing it (collisions,
doors, enemies, dying). The editor doesn't guess: the game plays it and writes down what
happened.

## Phases

1. **Reading demos (Common).** `PFWolf.Demos.DemoFile.Parse` reads the header and frames
   (and says what's wrong with a broken one). `DemoTrace` reads the game's trace file.
   Tests.
2. **`demotrace` (engine).** `demotrace <0-9|file.dmo> <out.txt>` plays the demo headless as
   demotest does and writes, frame by frame, the player (spot, angle, health, score, weapon),
   every actor that changed (class, spot, angle, health, state, sprite), removed actors,
   doors that moved and pushwalls on the move, then how it ended. Only changes are written,
   so a long demo stays small. Hooked after each frame of PlayLoop. Tests.
3. **The demo browser (editor, Resources → Demos…, Ctrl+Shift+D; F3 is the 3D view).** Lists
   the game's and the recorded demos (and a .dmo opened from disk): map, frames, length. An
   input timeline (turn and move strips, a lane per button, event ticks) with a scrubber. Picking
   a demo traces it (DemoTraceRunner: the game headless in a temp folder with the editor's mods,
   `--exec "demotrace demo.dmo trace.txt; quit"`; cached per demo until the game reloads) and
   shows the level read-only (MapCanvas with an IMapOverlay, DemoOverlay) with the player's path,
   event markers and a replay of every actor, door, pushwall and changed tile at the frame;
   play/pause, speed, follow the player, an events list that jumps to the frame. Save as .dmo.

## Status (2026-10-18)

All three phases done, uncommitted on feature/editor/demo-visualizer. Tests: DemoFileTests,
DemoTraceTests, DemoCatalogTests, DemoTraceGameTests (GameData: a game demo and a .dmo file).
Checked in the editor on Wolf3D DEMO0 (start, mid, end frames). Not checked: Spear/shareware/
Blake demos in the window, a mod's changed level under a trace, the replay mid-fight by eye,
opening/saving .dmo through the dialogs.

## Ideas left

- Demos in pk3s (`demos/` folder) so mods can ship them.
- "Watch in PFWolf": launch the game windowed playing the demo.
- Draw actors facing their way (the trace would need Dir), or with their sprite view toward
  the player.

## Trace format (text, version 1)

```
pfwolf-demotrace 1
demo <label> map <MAP##> frames <n>
f <frame> <tics>
p <x> <y> <angle> <health> <score> <weapon>
a <id> <class> <x> <y> <angle> <hp> <state> <sprite+frame>    (new or changed)
r <id>                                                        (removed)
d <tx> <ty> <vertical 0|1> <position 0-65535>                 (door that moved)
w <tx> <ty> <dir> <pos 0-63>                                  (pushwall on the move)
end <completed|died|...> <demotest report>
```

x, y are fixed point (65536 a tile). A line not understood is skipped, so later versions
can add lines.
