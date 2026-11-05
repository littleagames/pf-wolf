# Particle effects

A general particle system: sparks, smoke, blood, debris, steam, embers, fountains, dust,
teleport sparkles. Defined in YAML, attached to actors and map tiles, and driven from Lua.

## How big a lift

**Medium.** About the same as rain and snow (`Rendering/Weather.cs`, ~520 lines) plus its
YAML, actions and Lua glue. Most of the hard renderer work already exists:

| Needed | Already there |
|---|---|
| A 3D point onto the screen, with look up/down and eye height | `Weather.ProjectPoint` |
| Hidden behind walls per column, including short walls in front of tall ones and arch undersides | `Weather.NoteOccluders` / `Occluded` |
| Drawn in among the sprites, farthest first | `DrawWeatherBeyond` interleaved in `SpriteDrawing.DrawScaleds` |
| Lit by the level and colored lights, faded with distance | `Lighting.LightAt` / `ShadeOffset`, `WeatherColor` |
| Shown in front of see-through doors | `SeeThroughPosts.DrawBeyond` |
| Covered tiles: walls by stories, ceiling flats, doors, arches | `Weather.WeatherCovered` |
| Sprites at a height | `SpriteScaler.ScaleShape` (Z, origin) |
| Where a shot lands, for impact effects | `LineOfSight.TraceShot`, `PlayerAttacks.HitPuff` / `MissPuff` |
| Actor actions, trigger actions, a state's own block (`light:`) | `ActorActionRegistry`, `MapTriggerRegistry`, `ActorLights` |
| Lua host, hooks, local-only hooks | `ScriptHost`, `GameScripts(.Hooks)`, `_localHook` / `ChangesTheGame` |

What's new: a pool of particles that move (weather's particles don't; they're computed from
the clock), emitters, a definitions file, translucency tables, and the API surface.

## The key decision: particles are cosmetic

Like weather, particles live only in the view. They have their own random numbers (never
`US_RndT` / `game.random`), aren't saved, and aren't sent over the network. That keeps the
lift small and keeps demos trace-identical and lockstep multiplayer in sync. The cost:

- A loaded game starts with no particles in flight. Emitters come back by themselves, since
  they come from actor classes and states, map tags or Lua (`game.onlevel` runs with
  `"loaded"`).
- In multiplayer, every machine spawns the same effects from the same game events. The
  particles themselves differ a little from machine to machine, which doesn't matter.
- Particles can't hurt, block, trigger or pick up anything. Anything that has to affect the
  game is still an actor (a projectile, a puff actor).

Because they don't change the game, the particle Lua functions work in *every* hook, local
ones included (`onhud`, `onmusic`), and in the console.

## Particle types (`particles.yaml`)

A new definitions file, per pack, merged across base packs and mods like the others (YamlTree,
`!remove` / `!replace`). Two maps: `types` (one particle's look and motion) and `emitters`
(what spawns them).

```yaml
types:
  Spark:
    shape: dot            # dot | square | streak | sprite
    size: 1               # texels (dot/square); a streak's width
    size-end: 1           # grows or shrinks over its life
    color: [ "#FFFFC0", "#FFC040", "#C04000" ]   # a ramp over its life (palette's nearest)
    life: 20-35           # tics (a range is random per particle)
    gravity: 0.15         # texels a tic, a tic (down)
    drag: 0.02            # share of speed lost a tic
    bright: true          # ignores the level's light
    blend: additive       # opaque | translucent25 | translucent50 | translucent75 | additive
    collide: bounce       # none | die | bounce | stick (walls, floor, ceiling, flats heights)
    bounce: 0.4
    on-death: Ember       # optional: a type spawned where it dies (one level deep)

  Smoke:
    shape: sprite
    sprite: SMOKA         # a sprite name; a frame sequence animates over its life
    frames: A-D
    scale: 0.25-0.4
    scale-end: 1.0
    blend: translucent50
    life: 60
    rise: 0.3             # texels a tic up (negative gravity, kept separate for readability)

emitters:
  TorchSparks:
    type: Spark
    rate: 4               # particles a second (continuous)
    burst: 0              # or a count at once
    offset: [0, 0, 48]    # from the actor's spot: x, y along its facing, z texels up
    spread: 30            # degrees around its direction
    direction: up         # up | down | forward | out | [angle, pitch]
    speed: 1-2.5          # texels a tic
    range: 12             # tiles: not spawned farther from the view than this
```

Validation follows the usual warnings (unknown type, unknown sprite, bad color) at startup,
listed with the signon warnings.

## Hooking it up

1. **Actor classes** (actordefs): `particles: [TorchSparks]` gives every actor of the class its
   emitters, like `light:`. A state's `particles:` replaces them while it's in that state
   (`particles: none` stops them): a barrel smoking only once it's hit.
2. **Actions** (actor states, and switches/triggers on the tagged actors or tiles):
   - `A_SpawnParticles(type|emitter, [count], [z])`: one burst
   - `A_StartEmitter(emitter)` / `A_StopEmitter([emitter])`
3. **Map tiles by tag** (plane 4): game-info `maps:` `emitters: { 7: SteamVent }`, as
   scrollers do, for steam out of floor grates or drips from a ceiling. Started and stopped by
   tag with the same actions.
4. **Map things**: an `Emitter` actor class with a TNT1 sprite and mapdefs ids in the mod's range,
   so a mapper can place one in the editor.
5. **Lua** (below).

## Lua API

```lua
-- one-off bursts: at a point (tiles, z in texels) or at an actor
game.particles("Spark", x, y, z, { count = 12, speed = "1-3", spread = 360, color = "#80FF80" })
actor:particles("Blood", { count = 6, z = 40 })

-- emitters: a handle you can move, change and stop
local e = game.emitter("TorchSparks", x, y, z)       -- or game.emitter("TorchSparks", actor)
e:set{ rate = 10 }
e:moveto(x, y, z)
e:stop()

-- types and emitters made in Lua, merged over particles.yaml
game.defineparticle("GreenSpark", { base = "Spark", color = { "#C0FFC0", "#00A000" } })
game.defineemitter("Fountain", { type = "Water", rate = 40, direction = "up", speed = "3-4" })

-- how many are live (for a script to back off)
game.particlecount()
```

Combined with the hooks we already have, this covers most uses with no engine work:
`ondamage` → blood, `onkill` → gibs, `onpickup` → sparkle, `onteleport` → swirl, `ondoor` →
dust, `onpushwall` → grinding dust, `onwallbreak` → debris, `onfire` → muzzle smoke,
`onrevive` → medic glow, `onfrag` / `onflag` in multiplayer.

**Not planned: a Lua function per particle per frame.** A few thousand Lua calls a frame
would be slow and run into the loop guard. Instead, motion is declared (gravity, drag, rise,
wind, ramps, `on-death`). If something has to change while it plays, a script changes it on
the *emitter* (`e:set{...}` in `game.ontick`), which is cheap.

## Renderer

- **`ParticleSystem`** (Rendering/Particles.cs): a fixed pool (struct array, no per-frame
  allocation, its limit from the `r_particles` setting, 4096 at High), simulated by fractional tics each
  frame so it's smooth with uncapped frame rates and stops while the game is paused. Collision
  is per tile: `TileMap`, wall stories, flats floor/ceiling heights, doors.
- **Shared point drawing**: pull the occluder and draw code out of `Weather` into a small
  `PointSprites` helper that both use, so weather and particles sort into one list among the
  sprites. Weather keeps its stateless, computed-from-the-clock particles.
- **Sprite particles** get a scale (SpriteScaler draws at a story's size today) and draw
  through the same blend path.
- **Translucency**: 8-bit, so blending means lookup tables: 256×256 per mode (25/50/75%,
  additive), built from the palette with the nearest-color search Lighting already uses, made
  when the palette loads (Wolf3D, Spear and Blake each get their own). About 64 KB a table.
  This is the one truly new renderer piece. It's also what later translucent sprites or
  windows would need.
- Distance culling (`range`), plus a near cull like weather's quarter tile.

## Phases

1. **Core.** `ParticleInfo` / `EmitterInfo` schema (Common), `particles.yaml` loading and
   merging, `ParticleSystem` pool and simulation, dot / square / streak shapes, opaque only,
   the shared point drawing with `Weather`. The `r_particles` setting and its Options → Video
   item (see Decisions). Console: `particles` (live count, types),
   `particle <type> [count]` (a burst in front of you, Cheat). Tests: YAML parse, simulation
   (gravity, drag, life, collision), demotest unchanged.
2. **Emitters and actors.** Class and state `particles:`, `A_SpawnParticles` /
   `A_StartEmitter` / `A_StopEmitter` for actors and triggers, tile emitters by tag, the
   `Emitter` map thing. Save load re-derives them (nothing in the save).
3. **Lua.** `game.particles`, `actor:particles`, `game.emitter` handle userdata,
   `game.defineparticle` / `defineemitter`, `game.particlecount`; allowed in local hooks.
   Docs in the scripting reference.
4. **Looks.** Blend tables (translucent / additive), sprite particles with animated frames and
   scale, color ramps, `on-death` chaining.
5. **Example mod + editor.** The base packs stay untouched. A `particles-demo` mod: torches, a steam grate by tag, a fountain,
   a smoking barrel, Lua blood/gibs/teleport sparkles, a script-made emitter. Editor: the
   Definitions window (`particles.yaml` as a new DefinitionKind), the `Emitter` thing in the
   palette, LevelChecks for unknown emitter names. The 3D-view preview is left for later.
   Entries in manual-testing.md.

**Phase 1 as built (2026-11-04):**

- `PFWolf.Common/Assets/ParticlesAsset.cs`: `ParticlesAsset` (`types`, `emitters`),
  `ParticleTypeInfo`, `EmitterInfo`, and `ParticleRange` ("35", "20-35", "-2--1").
  Changes from the sketch above: `colors:` is a list beside a single `color:`; `rise` was
  dropped (negative `gravity` rises); the emitter's way is `angle` (from its facing) and `pitch`
  (up), with `spread` the cone's half-angle (180 every way); `count` is a burst's size and
  `rate` a running emitter's (phase 2). `blend`, `sprite` and `on-death` wait for phase 4;
  `offset` for phase 2.
- Loaded from `gamepacks/{pack}/particles.yaml` and a mod's root `particles.yaml`, laid over
  base packs all the way down (as factions.yaml). In DocGen.
- `Rendering/PointDrawing.cs`: Weather's occluders, point list, sort and drawing moved here
  (square, dot, streak, splash), shared by weather and particles in one farthest-first list.
- `Rendering/Particles.cs`: the pool (1024 / 2048 / 4096 by the setting), bursts in a cone,
  moved per tic in `LevelLoop` (gravity, drag, collide die / bounce / stop / none against
  walls by story, shut doors, the floor and the ceiling unless the tile shows sky), drawn
  between tics with the frame's fraction. Color ramps of 16 nearest palette colors. Cleared at
  `PlayLoop` start (new level, death, load). Startup warnings for bad types and emitters.
- `r_particles` (off/low/medium/high), saved as one byte appended to config.cfg; Options →
  Video "Particles" in Wolf3D's and Blake's menus.
- Console: `particles` (types, emitters, count), `particle <emitter|type> [count]` (Cheat).
- Not done in phase 1: arch undersides as a ceiling, diagonal walls (a diagonal tile is solid
  to particles).

**Phase 2 as built (2026-11-05):**

- actordefs `particles` property (one emitter, a list, or commas between) and a state's
  `particles:` (in place of the class's while the frame shows; `none` for none), read by
  `StateDataNodeDeserializer.EmitterNames`, kept on `ActorStateFrame.Particles`.
- Running emitters in `Particles.EmitFromActors`, each tic before the particles move: `rate` a
  second times the setting's share, the fraction carried over per actor and emitter; from the
  actor's spot and `Z`, turned by its facing (its `Dir`, else its `Angle`); none farther than
  `range` tiles from the camera (nothing piles up meanwhile).
- `A_SpawnParticles("name"[, count][, z])`, `A_StartEmitter(["name"])`,
  `A_StopEmitter(["name"])` for actor states, and for switches on the tagged actors (so
  `game.trigger` has them too). What the last two leave (`ParticleOverride`: off, or the names
  running) is **saved** with the actor: save v41, net state v17. A stopped emitter coming back
  after a load would be a bug, so this one piece of particle state is kept; particles in the
  air still aren't.
- **Tile emitters by tag were dropped** for a shared `ParticleEmitter` class (actordefs
  native.yaml, a TNT1 spot): a mod's class on it lists `particles` and gets a mapdefs thing id,
  and a switch reaches it by its tile's tag. That does what game-info `emitters:` by tag would,
  with one way to do it instead of two. In the manual's Built-in classes.
- Console `emitters` lists the actors running emitters and why.

**Phase 3 as built (2026-11-05):**

- `Scripting/GameScripts.Particles.cs`: `game.particles(name, x, y[, z[, changes]])`,
  `actor:particles(name[, changes])`, `game.emitter(name, x, y[, z[, changes]])` /
  `game.emitter(name, actor[, changes])`, `game.defineparticle(name, fields)`,
  `game.defineemitter(name, fields)`, `game.particlecount()`. None calls `ChangesTheGame`, so
  they work in `game.onhud` and the other local hooks.
- `changes` and `fields` are tables of an emitter's or its type's fields as particles.yaml
  names them; `size-end`, `size_end` and `sizeend` all work (hyphens and underscores dropped,
  any case). Set by reflection over `EmitterInfo` / `ParticleTypeInfo`, so a field added to the
  YAML is in Lua too. A type changed for one call is read afresh and cached by its changes.
- The emitter handle is a table of closures: `stop`, `start`, `running`, `moveto`, `set`,
  `remove` (ScriptApi's new `EmitterMethods`, kind `EmitterMethod`, a section of its own in the
  scripting reference).
- **Script emitters belong to a level** (`MapManager.LevelSerial` when made), not cleared at
  `PlayLoop` start: `game.onlevel` runs before `PlayLoop`, so clearing there would have
  wiped what it just made. One on an actor goes when the actor's removed.
- `defineparticle` / `defineemitter` write into a copy of particles.yaml's definitions, kept
  until the game's closed (scripts run once, at startup); `base` copies another first. Names
  made this way work everywhere a particles.yaml name does (actordefs, actions, console).
- Not done: `print(handle)` shows a table; editor autocomplete after `e:` shows actor methods,
  not the emitter's.

**Phase 4 as built (2026-11-05):**

- Type fields: `blend` (opaque, translucent = translucent50, translucent25, translucent75,
  additive), `fade` (out over the last half of its life through 75/50/25%, then gone; not
  additive, where a color ramp ending in black does the same), `shape: sprite` with `sprite`
  (four letters), `frames` (A-D, ACE), `frame-tics` (0: spread over its life once), `scale`
  and `scale-end` (1: as big as an actor's sprite there), and `on-death` (an emitter's burst
  where it died, facing the way it went, or one particle of a type going on with its speed;
  what's left leaves nothing). The plan's per-particle `scale` range became two numbers.
- `PointDrawing`: points carry a `Blend`; `Plot` goes through a 256×256 table for it
  (`BlendTable`), made the first time each blend is drawn from the palette through a 32³
  nearest-color cube. Sprite points (`AddSprite`) draw the picture scaled, its see-through
  pixels left out, lit through the light row at the particle, occluded per pixel like the rest.
- A sprite particle is drawn no bigger than half the view's height (its shape kept): each of
  its pixels checks the walls, so one in the view's face would otherwise cost a screenful.
- Found and fixed while testing: `points[Next(nx)]` took the array before `Next` grew it.
- Not done: tables aren't remade if the palette changes mid-game (it doesn't today); sprite
  particles don't take color translations; Wolf3D's palette has few in-between greys and
  teals, so translucent smoke over a teal door reads mostly grey.

Phases 1–3 give a usable system (solid particles, from YAML, actors and Lua). Phase 4 is what
makes smoke and magic look good.

## Decisions

User, 2026-11-04:

- **Vanilla stays vanilla.** No base pack (wolf3d, spear, blake...) uses particles: no
  `particles.yaml` entries, no `particles:` on their actors. The engine supports them, and the
  `particles-demo` example mod (phase 5) shows them off.
- **A setting in Options.** Options → Video gets a **Particles** choice: *Off / Low / Medium /
  High* (`r_particles`, 0-3, default High, saved in config.cfg, also a console variable).
  - Off: nothing is spawned or drawn (emitters still count time, so turning it back on doesn't
    make a burst).
  - Low / Medium / High: the pool's limit (1024 / 2048 / 4096) and a share of every emitter's
    rate and burst count (25% / 50% / 100%, at least 1 for a burst of any size).
  - Vanilla has nothing to show, so the setting only matters with a mod; it's there for
    everyone anyway, like the other Video items.
  - The Video window grows by one row (height 110 → 123, "13 per row plus 6"). Blake's own
    `menudefs/blake/video.yaml` gets the item too. Planet Strike uses Blake's.
  - It's the player's own setting, not the game's: never in demos, saves or net rules, so a
    host's setting doesn't change a client's.
- **`particles.yaml` is its own file**, per pack under `gamepacks/{pack}/` and in mods, merged
  like the other definitions files. Added to the loader's known YAML files, DocGen and the
  editor's Definitions window.

Also settled (no objection to the suggestions):

- **Not saved.** In-flight particles and Lua-made emitters aren't in saves; save version
  unchanged.
- **Weather stays as it is.** Its computed-from-the-clock particles are far cheaper for
  10,000 drops; only the drawing is shared.

Setting work lands in phase 1 (the cvar and the Off/limit behavior) so every later phase is
tested against it; the menu item can go in with phase 1 too.
