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
