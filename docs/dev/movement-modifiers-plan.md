# Movement modifiers (ice and friends) plan

Ice, mud, conveyors, wind and boost pads, done in a mod's Lua. The engine gives one primitive,
`actor:move(dx, dy)`; everything else is script.

## How it works

`game.ontick` runs after every actor has thought this tic (`Gameplay/LevelLoop.cs`), so a script
sees how far each actor moved by itself and adds the rest. Per actor, per tic, it keeps a speed
`v` (tiles a tic):

```
d     = where it is now - where the script left it last tic   (the engine's own move this tic)
v     = f*v + (1-f)*d        f = the surface's friction ^ tics
extra = v - d
moved = actor:move(extra)    stopped by walls
v     = d + moved            running into a wall takes the speed out of it
```

Holding forward on ice, `v` builds up to the walking speed (slow to get going); letting go, `d` is
0 and `v` carries on and fades (the slide). A jump of more than a tile (a teleport) or dying resets
`v`, as footsteps.lua does.

| Surface   | Script                                                                 |
|-----------|------------------------------------------------------------------------|
| Ice       | low friction: the momentum above                                       |
| Mud/water | takes part of each step back: `extra = -(1-k)*d`                       |
| Conveyor  | a steady carry each tic; game-info `scrollers:` scroll the flat with it |
| Wind/fan  | a push away from a fan actor or along tagged tiles                     |
| Boost pad | a kick added to `v` (on ice, a launch)                                 |

Chosen by floor texture (`game.floor`) or tag (`game.tag`).

### Monsters and bodies

Living monsters walk tile middle to tile middle; sliding them freely would break their walk, so
`actor:move` uses the knockback shove (`Monster.Knockback.cs`): no more than 3/8 tile off their walk,
easing back as they walk on. On ice they skid at turns and when they stop to shoot. Dead bodies
slide freely; knockback is part of `d`, so corpses glide across the ice.

## Phases

1. **Engine: `actor:move(dx, dy)`** (tiles), returning how far it really went.
   - Players: `PlayerMovement.MoveBy` — `ClipMove` (no wall bump sound) and the tile/area/walk-over/
     floor-trigger settling `Thrust` does, as that player; `onenter` is told of a new tile. Not for
     a player who can't be targeted (dead, spectating).
   - Monsters: `Monster.MoveBy` — the shove's rules (`ClearToShove`), a living one within
     `MaxDisplacement` of its walk, a dead one anywhere (and onto the tile it ends on). Wall
     sprites and in-wall ones don't move.
   - Anything else: (0, 0).
   - Not from local-only hooks (`ChangesTheGame`); long moves are split into steps (no tunnelling),
     at most 4 tiles a call.
   - `ScriptApi.cs` (autocomplete), docs, tests.
   *Done: 12f47cdd.*
2. **Lua library** `examples/mods/movement-demo/scripts/movement.lua`: `movement.floors` by floor
   texture and `movement.tags` by tag (a tag wins), each surface any of `slip` (speed kept a tic,
   0 to <1; ice ~0.97), `scale` (own steps; mud 0.5), `carry = {east, south}` (tiles a second),
   `kick` (tiles a second on stepping onto the tile: a number along the facing, or `{east, south}`;
   dies away by `max(slip, movement.drag)` a tic). Switches `players`, `riders`, `monsters`,
   `bodies`; replaceable `movement.surfacefor(actor)` and `movement.onbump(actor, speed)` (running
   into something faster than `bumpspeed`). Powers by multiplying, not `^`, so machines agree.
   Projectiles are left out; items and decorations don't move (actor:move gives them nothing).
   *Done* (MovementGameTests: ice slides on after letting go, mud about half as far, a conveyor
   carries a standing player); kicks, bumps, monsters and bodies untested.
3. **Example mod `movement-demo`** from `examples/tools/make-movement-demo.ps1`: an ice rink with
   guards, a mud pit, a conveyor hall with scrolling flats, a wind tunnel, boost pads over an ice gap.
4. **Tests and docs**: headless GameData tests (ice keeps you moving after letting go, stone
   doesn't; a conveyor carries a standing player; mud shortens a walk); "(never tried)" items in
   `manual-testing.md`; the examples README.

## Known limits / decisions

- Speeds aren't saved (Lua globals aren't): after a load you start still.
- Vehicles move by `Thrust` too, so ice works on them (planned: yes).
- The engine's step comes first, the slide after: triggers and pickups work as ever (an item slid
  onto is picked up the next tic); the weapon bob follows your own steps, so it stops while gliding.
- Multiplayer/demos stay in step (hooks run in order on every machine), untested.
- `game.actors()` each tic is fine on normal maps; the Swarm Demo would want a nearer lookup.
