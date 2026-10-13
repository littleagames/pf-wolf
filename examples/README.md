# Examples

## Switch Demo (`mods/switch-demo`)

A small level that replaces Floor 1 (Wolfenstein 3D only) to show off wall switches: walls
you use, like doors, that act on whatever shares their **tag** on the map's tag plane (plane 4).

Building the game copies it to the `mods` folder next to `Wolf3D.exe`. To play it, switch it
on in **Options → Mods** (the game restarts) and start a new game, or run:

```
Wolf3D.exe --file switch-demo
```

The hall has seven switch stations (the elevator's lever panels):

1. **North wall, left:** opens the door next to it and holds it open; use it again to close
   it. The door is gold-locked and the level has no gold key, so only the switch opens it.
2. **North wall, right:** turns the hall's lights on and off (`A_Activate` / `A_Deactivate` on
   `SwitchLightOff` actors).
3. **West wall, top:** pushes the brick wall below it into a secret room, once
   (`A_MoveWall`). The silver key is in there. Its lever sticks for a moment before it goes
   up: the thrown switch's texture is an animation that plays once.
4. **West wall, bottom:** hangs flags on the south wall and takes them down (`A_SetWall`).
5. **East wall:** locked until you have the silver key; then it opens the way to the exit.
6. **North wall, right of station 2:** fades the lights in the dark room behind the archway
   next to it up and down (`A_SetZoneLight` on light zone 1).
7. **Inside that room, on its far wall:** sets off the hall's alarm, red light strobing across
   the hall (`A_SetZoneLight` with a tint, then `A_SetZoneEffect` on light zone 2), and turns
   it off again. While the alarm is on, its lever flicks up and down (`A_ChangeTexture` swaps
   the lever's texture for a looping animation).

Stations 6 and 7 work on **light zones**: each tile's zone is its value on the zone plane
(plane 5), and the mod's `game-info.yaml` says how each zone is lit. Unlike the other
stations, they name the zone they change rather than acting on a tag.

While the room's lights are off, its floor lamp gives off a warm, flickering glow of its own:
an **actor light** (`light.*` properties in the mod's `actordefs/demo-lights.yaml`). Fire your
gun in there too: the weapons' muzzle flashes light up the dark around you.

The exit room has the usual elevator switch, now just a switch whose action is `A_Exit`.

### What's in it

- `mapdefs/walls.yaml`: the switch walls (ids 50-63), each with its `switch:` block. Every
  field and action is described at wall 21 in `pfwolf-pk3/mapdefs/wolf3d/walls.yaml`.
- `animdefs/switch-demo.yaml`: the two lever animations, made of the game's own lever
  textures. Animations are described in `pfwolf-pk3/animdefs/animdefs.yaml`.
- `mapdefs/things.yaml`: object 500, a `SwitchLightOff`, and 501, the light room's
  `FlickeringLamp`.
- `actordefs/demo-lights.yaml`: `FlickeringLamp`, a floor lamp with an amber, flickering
  light. Actor lights are described in `pfwolf-pk3/actordefs/wolf3d/decorations.yaml` (and
  the switch actions `A_SetLight` / `A_LightOff` at wall 21 in `mapdefs/wolf3d/walls.yaml`).
- `game-info.yaml`: merged into the game's own; it gives the level its light zones (the
  dark room, and the hall with the alarm's strobe timing). The zone fields are described
  under `default-map` in `pfwolf-pk3/gamepacks/wolf3d/game-info.yaml`.
- `maps/MAP01.wad`: the level, an ECWolf binary map with PFWolf's six planes. It's built
  by `tools/make-switch-demo-map.ps1` from an ASCII layout; edit the layout there and run
  it again to change the level:

  ```
  powershell -ExecutionPolicy Bypass -File examples/tools/make-switch-demo-map.ps1
  ```

In game, the `tag` console command (with cheats on) shows or sets a tile's tag, and
`actors` lists each actor's tag. `zone` shows or sets a tile's light zone, `zonelight`
lists the zones and how each is lit, and `lights` lists the actors giving off light.

## Tall Walls Demo (`mods/tall-walls-demo`)

A castle courtyard that replaces Floor 1 (Wolfenstein 3D only), to show off walls taller than
one story, textures drawn for them, arches and a sky.

```
PFWolf.exe --file tall-walls-demo
```

It replaces Floor 1 like the Switch Demo does, so switch on only one of the two at a time.

You start in a 1-story gatehouse. Through the door is the courtyard:

- **The curtain wall** around it stands **3 stories** tall, with a 64x192 texture: rough stone
  at the bottom, a banner across the upper two stories, battlements along the top. The lintel
  over the door you came through is that same wall, 3 stories up.
- **The keep** in the middle is **2 stories** (a 64x128 texture with a window in the upper story).
- **The colonnade** along the west side: brick pillars 2 stories tall with **arches** between
  them, open floor tiles with a block of wall from story 2 up. Walk under one and look up
  (`pitch 20` in the console) to see its underside.
- **The gate** in the north wall: a 3-wide arch, **3 stories**, between 3-story brick pillars.
  The garden behind it has the elevator.
- **The stepped wall** on the east side: the game's own grey stone at 1, 2 and 3 stories.
  A 64x64 texture repeats once per story.

The level has no roof, so `sky` in `game-info.yaml` puts a dusk sky above the walls.

### What's in it

- `textures/`: the tall textures, as PNGs. A picture in a mod's `textures/` folder is a wall
  texture of that name, even in a subfolder (`textures/castle/`; the same goes for `flats/` and
  `sprites/`). It can be any height: each 64 pixels is a story, counted up from the
  floor, and on a wall taller than the picture it starts again from the bottom. Each comes as
  `NAME1` (for north and south faces) and a darker `NAME2` (east and west), like the game's own walls.
  - `TWTOWR1`/`2`: 64x192, the curtain wall
  - `TWSTON1`/`2`: 64x128, the keep
  - `TWARCH1`/`2`: 64x128, the brick pillars. A row of pale voussoirs sits at the bottom of
    the upper story, so an arch beside these pillars has them just over its opening.
- `graphics/TWSKY.png`: the sky, 512 wide, so it goes around the view twice.
- `mapdefs/walls.yaml`: walls 50-52, faced with those textures.
- `game-info.yaml`: the level's `wall-height` (1 story, for every tile the map doesn't raise),
  its `sky` and its floor color.
- `maps/MAP01.wad`: the level. **How tall each wall stands is on the map's height plane
  (plane 3)**, per tile: 0 uses the level's `wall-height`, 1 to 8 is that many stories. A
  height on an open floor tile makes an arch. The arch's face and underside come from the wall
  at the end of its row of arch tiles. A door's height is how high its lintel goes.

Everything in the mod (pictures and level) is made by `tools/make-tall-walls-demo.ps1`. The
pictures are drawn pixel by pixel in the script, and the level comes from an ASCII layout in
it. Edit either and run it again:

```
powershell -ExecutionPolicy Bypass -File examples/tools/make-tall-walls-demo.ps1
```

In game, `where` shows the height of the tile you're facing. With cheats on, `height <0-8>`
changes it and `wallheight` changes the level's default. `sky <name|none>` swaps the sky.

## Patch Demo (`mods/patch-demo`)

A hall that replaces Floor 1 (Wolfenstein 3D only), to show off **wall patches**: pictures
drawn over a wall's faces, their see-through pixels letting the wall show. One wall id can
carry any decoration, and a patch can be a switch the player uses.

```
PFWolf.exe --file patch-demo
```

It replaces Floor 1 like the Switch Demo does, so switch on only one of them at a time.

- **North wall:** the same brass plaque on grey stone, blue stone, wood and brick: one patch
  on four kinds of wall. Further along, an alarm lamp blinking (a patch whose picture is an
  animdefs animation) and two 2-story walls hung with banners (a 64x128 picture covers two
  stories).
- **South wall**, left to right:
  - cracks, just decoration;
  - a first aid cabinet: use it to take it off the wall, but only when you're hurt; at full
    health it stays where it is (`remove: true` with `A_GiveInventory("FirstAidKit")`);
  - a box of ammo on a shelf, taken the same way;
  - a button: pressed once, it stays in and drops a chalice at your feet (`to:` a patch with
    no switch of its own, and `A_SpawnItem("Chalice")`);
  - a lever: opens and closes the door to the exit (`to:` each other, `A_ToggleDoor` on the
    lever's tag, 1).
- **West wall:** the cracked wall next to where you start is a pushwall. Its cracks go with it
  into the secret room.

### What's in it

- `mapdefs/patches.yaml`: the patches, 1 to 10. Every field is described in
  `pfwolf-pk3/mapdefs/wolf3d/patches.yaml`.
- `textures/`: the patch pictures, as PNGs with clear backgrounds (clear pixels are see-through).
- `animdefs/patch-demo.yaml`: the alarm lamp's two frames.
- `maps/MAP01.wad`: the level. **A wall's patch is the high byte of its plane 0 value**: wall
  id + patch id × 256. The game ignores a patch that isn't on a wall.

The pictures and the level are made by `tools/make-patch-demo.ps1` (the layout is an ASCII
drawing in it). Edit it and run it again:

```
powershell -ExecutionPolicy Bypass -File examples/tools/make-patch-demo.ps1
```

In the editor, the **Wall patches** layer (in the plane list) puts patches on walls without
changing the walls, and its palette lists the mapdefs patches. In game, `where` shows the patch
on the wall you face; with cheats on, `patch <0-255>` puts one on it.

## Standalone Demo (`mods/standalone-demo`)

A tiny game of its own that **needs no other game's files**: no WL6, SOD or any other data
files, and none of Wolfenstein 3D's definitions. Everything it has is in the mod (all original,
generated by a script), apart from the menus and language strings every game shares in
pfwolf.pk3.

```
PFWolf.exe --file standalone-demo
```

Dropping the mod on PFWolf.exe works too, and so does a game folder holding only PFWolf and
pfwolf.pk3. You start in an airlock; through the door is a lamp-lit hall with energy cells and
medkits, and beyond the next door the vault, whose switch ends the game.

The editor opens it the same way: pick the PFWolf folder, add the mod and load with "By the data
files or mods" (or start it as `PFWolf.Editor.exe <PFWolf folder> <mod folder>`). Play (F5)
runs it with `--game standalone-demo`.

To start a game of your own, the editor's **New mod…** makes a pk3: a stand-alone game (ready to
play: a copy of this demo's art, sounds, definitions and MAP01 under your game's name and id, with
its own `gamepack-info.yaml`, `modinfo.yaml` and `game-info.yaml`, and its own copy of Wolf3D's
palette, `palettes/wolfpal.pal`, to change in the palette browser) or a mod of one of PFWolf's
games (just its `modinfo.yaml`). The editor carries its copy of this folder, so regenerate the
demo before building the editor when its files change. The editor saves levels, palettes and texts straight into a pk3,
keeping the version before each save as `NAME.pk3.bak`.

### What makes it a game of its own

- `gamepack-info.yaml` at the mod's root adds a game, built on `standalone`:

  ```yaml
  standalone-demo:
    title: Standalone Demo
    base-pack: standalone
  ```

  `standalone` (in pfwolf.pk3's `gamepacks/gamepack-info.yaml`) has no data files and no
  definitions, only a default palette. A mod can't change the games pfwolf.pk3 lists, only add
  its own. A game built on `wolf3d` instead would play Wolf3D's data files under its own name.
- With no `--game`, a mod on the command line that adds a game runs that game; `--game` picks one
  when several do. A mod that adds games loads only in them, and it gets its own settings, saves
  and screenshots folder (`%APPDATA%\PFWolf\standalone-demo`).
- Its files load in its own pack's folders, which start empty: no `actordefs/wolf3d`,
  `mapdefs/wolf3d` or `gamepacks/wolf3d` files, and none of Wolf3D's deathmatch arenas or player
  sprites (pfwolf.pk3 keeps those in `maps/wolf3d/` and `sprites/wolf3d/`). Only what's shared
  comes along: `actordefs/native.yaml` (Inventory, Ammo, Health, Weapon...), `language/`,
  `sounds/sound-seq.yaml` (whose Wolf3D sound names the mod points at its own sounds) and the
  menus in `menudefs/`.

### What's in it

- `game-info.yaml`: the whole of it (a standalone game has none to start from): the episode and
  its one level, the skill, the signon screen and title loop, the high scores (`pic: ""`, its
  own fonts, its own default table) and `menu-music: ""` for no music.
- `actordefs/player.yaml`: the `Player` class's properties, the blaster, energy cells, a medkit
  and a lamp that lights the room.
- `mapdefs/map.yaml`: walls 1-4 (stone, panels, the exit switch and the switch thrown), doors 90
  and 91, the floor codes, the player starts and things 23-25.
- `fonts.yaml`, `colors.yaml`, `intermission.yaml`: its fonts (sheets in `graphics/`), its menu
  and automap colors, and its level-end and victory screens.
- `graphics/`: the title, signon screen and font sheets, and the pictures the shared menus and
  the engine draw by name, which Wolf3D's VGAGRAPH would give: `c_cursor1`/`2` (the menu cursor),
  `c_selected`/`c_notselected`, `c_diskloading1`/`2`, and the headings `c_options`, `c_control`,
  `c_customize`, `c_loadgame`, `c_savegame`, `c_fxtitle` and `c_mouselback`.
- `textures/`, `sprites/`, `sounds/` and `maps/MAP01.wad`: the walls, the blaster and pickups,
  the sounds (with `sounds/sound-seq.yaml` naming them) and the level.

Everything but the YAML files is made by `tools/make-standalone-demo.ps1`; edit it and run it
again:

```
powershell -ExecutionPolicy Bypass -File examples/tools/make-standalone-demo.ps1
```

Things a standalone game can't change yet: the shared menus themselves (mods can't carry
`menudefs/`), and the "DEMO" sprite (`DEMOA0`) drawn while a demo plays, which it can only give
as `sprites/DEMOA0.png`.

## Fireball Demo (`mods/fireball-demo`)

A short gauntlet that replaces Floor 1 (Wolfenstein 3D only): walls that spit fireballs. The
traps are actor and map definitions, a level and a Lua script of fireball patterns; the engine
gives any actor the actions they use.

```
PFWolf.exe --file fireball-demo
```

It replaces Floor 1 like the other level demos, so switch on only one of them at a time.

Work north from the start to the exit. The trap shooters sit inside eagle walls and fire out
of their faces once they see you:

- **The pattern hall,** where you start, shows the script's patterns: the skull pile in the
  middle sprays a two-armed spiral while you're within 4 tiles, the walls either side sweep
  their fire back and forth across the hall, and the wall by the start launches homing
  fireballs that steer after you for a while, then fly straight on.
- **The fire lanes:** four walls fire straight across the passage every second and a half.
- **The bone room:** with you within 4 tiles, the pile of bones in the middle throws a ring
  of eight fireballs every way, each ring turned a little from the last (a Lua script). The
  fireballs burn the two guards beside it too.
- **The corridor:** a wall at its west end fires along it. The switch at its east end, where
  you come in, turns it off and on.
- **The passage north:** a wall either side fires at you, two at a time and now and then a third.
- **The exit room:** the north wall fans five at a time across you.

### What's in it

- `actordefs/fireball-traps.yaml`: `Fireball`, a glowing projectile (Fake Hitler's flame)
  that bursts on whatever it hits and, with the `HURTMONSTERS` flag, hurts enemies as well as
  you. Then the traps: `WallFireball` (straight out), `WallFireballFan`, `WallFireballAimed`,
  `SwitchedWallFireball` (its `Active` and `Inactive` states are for the switch),
  `WallFireballSweep`, `WallFireballHoming`, `FireballRing` and `FireballSpiral`, with
  `HomingFireball` and the slower `SpiralFireball` (both `parent: Fireball`). The wall ones are plain actors drawn as nothing (`TNT1`); they wait with
  `A_JumpIfInSight` and fire with `A_SpawnProjectile` (`relative` to their facing, or `aim`).
  The aimed one uses `A_Jump` for its odd third shot. The actions are described in
  pfwolf-pk3's `actordefs/wolf3d/projectiles.yaml`. The ring and the
  spiral wait with `A_JumpIfPlayerWithin`. The patterns are actions of the mod's own script:
- `scripts/fireball-patterns.lua`, in Lua (see [Scripting](#scripting) below):
  `A_FireRing(projectile, count, turn)` (the bone room), `A_FireSpiral(projectile, step, arms)`,
  `A_FireBurst(projectile, count, spread)` (the exit room's, centred on you),
  `A_Sweep(projectile, arc, step)`, and `A_Home(turn, frames)`, which `HomingFireball` runs
  on itself each frame to steer. Its hooks show a message as a level starts and make the piles
  erupt every 12 seconds. Each keeps what it must remember between calls (how far a
  spiral has turned, which way a sweep is going) in the actor's `temp1`..`temp3`, so saved
  games keep it.
- `mapdefs/things.yaml`: objects 510-534, the wall traps, each facing east, north, west and
  south. They have `in-wall: true`, so they're placed on wall tiles, and fire out of the face
  their facing points out of. 526 is the ring and 535 the spiral, on open floor.
- `mapdefs/walls.yaml`: walls 50 and 51, the switch (a toggle: `A_Deactivate` /
  `A_Activate` on tag 1).
- `game-info.yaml`: dims the level a little, so the fireballs' light shows.
- `maps/MAP01.wad`: the level, built by `tools/make-fireball-demo-map.ps1` from an ASCII
  layout:

  ```
  powershell -ExecutionPolicy Bypass -File examples/tools/make-fireball-demo-map.ps1
  ```

### Scripting

A mod (or pfwolf.pk3) can carry Lua scripts: `scripts/*.lua`, in any subfolders. They all run
when the game starts, pfwolf.pk3's first, then each mod's in load order; a mod's script of the
same file name replaces the one before. Each global function a script defines whose name
starts with `A_` or `T_` becomes an action (or think) that actordefs states call like a
built-in one:

```lua
function A_FireRing(self, projectile, count, turn) ... end
```

```yaml
action: A_FireRing("Fireball", 8, 22)
```

`self` is the actor running the state; the rest are the state's arguments (numbers arrive as
numbers). A built-in action of the same name wins, and the script's is ignored with a warning.

What a script can use:

| | |
| --- | --- |
| Actor fields | `name`, `x`, `y` (in tiles; a tile's middle is n + 0.5), `tilex`, `tiley`, `angle` (degrees, 0 east, 90 north: the way it faces, or a projectile flies), `health`, `speed`, `state`, `tag`, `inwall`, `removed`, `isplayer`, `shooter` (who fired a projectile), `temp1`..`temp3` |
| Fields it can set | `temp1`..`temp3` (whole numbers, kept in saved games), `angle`, `speed` |
| Actor methods | `jump(state)`, `spawnprojectile(class[, angle[, "relative"\|"absolute"\|"aim"]])` (returns the projectile), `spawn(class)`, `playsound(sound[, positional])`, `property(key)`, `hasflag(flag)`, `distanceto(other)` (tiles), `angleto(other)`, `cansee(player[, tiles])`, `nearestplayer()`, `remove()` |
| `game` | `random()` (0-255), `random(n)` (1 to n), `random(m, n)`, `players()`, `actors([class])` (every actor, or those of a class, in the order they think), `tagged(tag[, class])` (the actors with that tag on the map's tag plane, as switches find them), `taggedtiles(tag)` (the tiles with it, each `{x, y}`: tagged doors and walls), `tag(x, y)` (a tile's tag, 0 for none), `trigger(tag, action, ...)` (below), `time()`, `tics()`, `map()`, `skill()`, `message(text[, style])` |
| Hooks | `game.onlevel(function(reason, map) ... end)` and `game.ontick(function(tics) ... end)` (below) |
| Lua | the basic, `string`, `table`, `math` and `bit32` libraries, and `print` (to the console) |

`game.trigger(tag, action, ...)` runs a switch action on a tag, as a switch with that tag
would: `game.trigger(3, "A_OpenDoor", "hold")`, or the whole call as walls.yaml has it,
`game.trigger(3, 'A_OpenDoor("hold")')`. It returns whether the action did anything. The
actions are the ones listed at wall 21 in `pfwolf-pk3/mapdefs/wolf3d/walls.yaml` (doors,
`A_MoveWall`, `A_SetWall`, `A_ChangeTexture`, `A_Activate`/`A_Deactivate`, light zones,
`A_Message`, `A_Exit`...). It acts as if the first player had used the switch (their facing is
`A_MoveWall`'s direction when none is given). A script has no switch tile of its own, so
`A_ChangeTexture` changes only the tagged tiles, and the actions that work on the switch's own
tile (`A_PushWall`, `A_Vend`, `A_SmartSwitch`, `A_FloorSelect`, `A_Teleport`) aren't allowed.

Hooks are functions the game calls itself, not from a state. Add them when the script loads
(at its top level); each mod's run, in load order:

- `game.onlevel(fn)`: as each level starts, once it's built and the players are on it, before
  its first tic: `fn(reason, map)`. `reason` is `"new"` (entered afresh, including a demo),
  `"returned"` (a hub level come back to, as it was left), `"restarted"` (again after dying) or
  `"loaded"` (a saved game loaded, or a network game joined). Only the loading machine sees
  `"loaded"`, so it mustn't change the game; it's for rebuilding anything the script keeps in
  Lua variables.
- `game.ontick(fn)`: each frame the world moves (not while it's paused), after the actors:
  `fn(tics)`, the tics going by (70 a second; usually 1 or 2). `game.time()` is the level's
  time as they began.

A hook that errors three times is switched off. The demo's script uses both: a word for the
player as a level starts, and an eruption of fireballs from the bone and skull piles every 12
seconds, worked out from `game.time()` alone so that nothing needs saving.

So that demos, network games and saved games come out the same everywhere, a script works only
from what's on the actors and the game's own random numbers: there's no `math.random`, nothing
reaching outside the game (`io`, `os`, `require`, `dofile`, `load`), and Lua variables aren't
saved. Keep anything a saved game should remember in `temp1`..`temp3`. A script that errors is
reported on the console with its file and line (and at startup, on the signon screen); one that
runs over a million instructions in one call is stopped.

In the editor, **Resources → Scripts…** (F12) lists the scripts the game runs, in that order,
with the actions each gives and the actordefs states that call them, its hooks, and what's wrong
with it, checked as you type: whether it compiles (the game leaves out one that doesn't), and
anything the game's scripts haven't got (`math.random`, `io`, `require`...). It's colored as Lua,
and offers what can come next as you type, with each one's signature and what it does: after
`game.` the game's functions, after `actor:` an actor's methods and after `actor.` its fields, the
library functions after `string.`, `math.` and the rest, Lua's own names and the actions there
are as a word is typed, and in strings the game's names (actor classes for `spawnprojectile("`,
switch actions for `game.trigger(tag, "`, states for `jump("`...). Ctrl+Space asks for it
anywhere; Enter, Tab or `(` takes the one picked. Rest the pointer on a name for what it is: the
game's functions, an actor's methods and fields, Lua's, an action (the line it's defined on and
what calls it), or a class, state or switch action in a string. F12 or Ctrl+click on an action
goes to its definition: the one the game runs, which is the last script to define it, in
whichever script that is (a function of the script's own goes to its line); Alt+Left goes back.
The other way, each action's callers in the list beside the script are links that open that
actor class in the Definitions window, at the state's frame that calls it; Shift+F12 on an
action in the script lists them by the caret.
Changes not saved stay with each script while another's shown. A script can be
changed and saved back into its mod, or a new one made (New…, Ctrl+N); pfwolf.pk3's go into a
mod picked, in their place. The game runs scripts as it starts, so Play (F5) to try one.

In the console, `scripts` lists the scripts that loaded, any that didn't and why, and the
actions they give. With cheats on, `lua <code>` runs Lua in the scripts' state, with `me` as
the player: `lua print(me.health, me:nearestplayer())`. Use 'single quotes' for strings there.

## Moody Lights (`mods/moody-lights`)

Darkens every level (Wolfenstein 3D only) and lets the light decorations light the rooms
around them, each in its own color. It's all data: no maps, no pictures, two YAML files.

```
PFWolf.exe --file moody-lights
```

- `game-info.yaml`: `default-map` shading for every level: the light drops to 64 (of 255)
  and fades to black from 3 tiles out to 18. A map can still set its own shading.
- `actordefs/moody-lights.yaml`: `light.*` properties added to the base game's lights.
  Mod YAML merges into the base actors key by key, so naming an actor and its `properties`
  is enough; sprites and states stay as they were. Inherited lights come along too
  (`CeilingLight2`, `Chandelier2`, `TallFloorLamp`, the switch demo's `SwitchLight`).

  | Actor | Light |
  | --- | --- |
  | `CeilingLight` | warm white, steady |
  | `Chandelier` | amber, flickering gently like candles |
  | `FloorLamp` | soft yellow |
  | `RedCeilingLight` | red, pulsing slowly like a warning light |
  | `BareLightBulb` | pale, stuttering now and then; its sprite is also made `bright` |

Your weapon's muzzle flashes light the dark too. To try other values without restarting,
use `light 0-255` and `fog` in the console (cheats on), and `lights` to list each light.

## Faction Demo (`mods/faction-demo`)

A level that replaces Floor 1 (Wolfenstein 3D only) where it isn't just you against everyone:
a squad follows you, prisoners you free join it, and in the hall ahead the axis and a third
side, the mutants, are already fighting each other. It's YAML, a level and a small script.

```
PFWolf.exe --file faction-demo
```

It replaces Floor 1 like the other level demos, so switch on only one of them at a time.

- **The start room:** your squad, two guards drawn in your green. Once they've seen you they
  go about with you: they walk after you when you get more than 3 tiles away, stand by when
  you stop, step aside when you walk into them, and fight whatever they see. Your shots pass
  them by, and they're not in the kill count.
- **The cell block:** four prisoners, drawn grey. Face one and press use: they turn green,
  join your squad and follow you.
- **The hall:** guards and SS (the axis) at the west end, red mutants at the east. Open the
  door and they're at it, each side going after the other, and you and your squad, whoever is
  nearest in sight. What one side's shots and rockets hit, the other side pays for; enemies
  killed by others still count toward your kill ratio, but only your own kills score.
- **The exit room,** past the hall.

On the automap your side shows in cyan, and your followers always show.

### What's in it

- `factions.yaml`: the sides, laid over the base game's (`allies`, the player's side, and
  `axis`, every enemy): `prisoners`, neutral to everyone but counting the allies as allies
  (so they're out of the kill total and the squad leaves them be), drawn `grayscale`; and
  `mutants`, hostile to everyone, drawn `mutants-red`.
- `translations.yaml`: `mutants-red`, a desaturated recolor (each pixel's lightness picks a
  shade of red), so the mutants' green clothes can't be taken for your allies.
- `actordefs/faction-demo.yaml`: `SquadGuard` (`faction: allies`, `monster.follow: true`) and
  `Prisoner` (`faction: prisoners`, with a `Use` state running
  `A_SetFaction("allies", "follow")` and the script's `A_Freed`).
- `mapdefs/things.yaml`: objects 540 (the squad), 541 (a prisoner) and 545, a mutant put on
  the mutants' side by the thing itself (`faction: mutants`), so Wolf3D's own mutants elsewhere
  stay axis.
- `scripts/faction-demo.lua`: a hint as the level starts, and `A_Freed`'s thanks.
- `game-info.yaml`: turns messages over the view on, for those.
- `maps/MAP01.wad`: the level, built by `tools/make-faction-demo-map.ps1` from an ASCII layout:

  ```
  powershell -ExecutionPolicy Bypass -File examples/tools/make-faction-demo-map.ps1
  ```

### Factions and color translations

Every actor is on a side, a faction. The rules are written at the top of pfwolf-pk3's
`gamepacks/wolf3d/factions.yaml`; in short:

- An actor's faction is a map thing's `faction:` (mapdefs `things`), else its class's
  `faction` property, else the game pack's `player-faction` (for a player) or
  `monster-faction` (for an enemy). What it throws, or turns into, is on its side.
- A faction treats another as `hostile` (goes after it on sight), `neutral` (leaves it be
  until hurt by it) or `ally` (never goes after it; its shots pass it by): its `relations:`
  entry for it, else its `default:`, else the file's `default-relation`. Its own members are
  always allies. Relations go one way: for two sides to be allies, both say so.
- Enemies only act in rooms joined to yours by open doors, as they always have, so sides
  meeting out of your reach don't fight until you get there.
- `A_SetFaction("allies"[, "follow"])` changes an actor's side (the kill total follows), and
  `A_Follow` / `A_Follow("off")` start and stop it following you. Scripts can set
  `actor.faction` and `actor.follows`. An actor with a `Use` state goes to it when you face it
  and press use.
- In the console, `factions` lists the sides, how each treats the others, and who's on each;
  with cheats on, `setfaction <faction> [class]` moves the nearest enemy (or a class) over.

A translation recolors a sprite as it's drawn, so one set of sprites can come in several colors.
The rules are at the top of pfwolf-pk3's `gamepacks/wolf3d/translations.yaml`:

- Each is a list of ZDoom-style ranges: `"207:223=101:111"` (palette indices onto others),
  `"96:111=[255,0,0]:[64,0,0]"` (onto a gradient) or `"1:254=%[0,0,0]:[2,0,0]"` (desaturated,
  by each pixel's lightness, so it works on any sprite).
- An actor is drawn through a map thing's `translation:`, else its class's `translation`
  property (or a script's `actor.translation`), else for an enemy its faction's `translation`
  (or `class-translations`, one per class, since each sprite wears its own colors), else for a
  player their color.
- Playing with others, everyone picks a color in the lobby (game-info `player-colors`).
- In the console, `translations` lists them; with cheats on, `settranslation <name|none> [class]`
  and `playercolor <name|none> [n]` try them out.
