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

The exit room has the usual elevator switch, now just a switch whose action is `A_Exit`.

### What's in it

- `mapdefs/walls.yaml`: the switch walls (ids 50-63), each with its `switch:` block. Every
  field and action is described at wall 21 in `pfwolf-pk3/mapdefs/wolf3d/walls.yaml`.
- `mapdefs/things.yaml`: object 500, a `SwitchLightOff`.
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
`actors` lists each actor's tag. `zone` shows or sets a tile's light zone, and `zonelight`
lists the zones and how each is lit.
