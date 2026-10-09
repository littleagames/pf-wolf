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
   (`A_MoveWall`). The silver key is in there.
4. **West wall, bottom:** hangs flags on the south wall and takes them down (`A_SetWall`).
5. **East wall:** locked until you have the silver key; then it opens the way to the exit.
6. **North wall, right of station 2:** fades the lights in the dark room behind the archway
   next to it up and down (`A_SetZoneLight` on light zone 1).
7. **Inside that room, on its far wall:** sets off the hall's alarm, red light strobing across
   the hall (`A_SetZoneLight` with a tint, then `A_SetZoneEffect` on light zone 2), and turns
   it off again.

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
  texture of that name. It can be any height: each 64 pixels is a story, counted up from the
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
