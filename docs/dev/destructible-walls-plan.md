# Destructible walls

Walls that can be broken or taken away during play: shot down by the right weapon, blown out by
a blast, or made to crumble or vanish by a switch, a floor trigger, an actor's state or a script.
Planned 2026-10-28 on branch feature/destructible-walls; all four phases are done (see Status at the end).

## What there is to build on

- **Pushwalls** (Gameplay/PushWalls.cs) already empty a wall tile mid-game: `TileMap = 0`,
  `ActorAt = null`, a floor code on plane 0, and the height, tag and patch moved along with it.
  A removed wall needs the same steps, minus the move.
- **Switches and triggers** (Gameplay/SwitchActions.cs, MapTriggerRegistry) run actions on
  whatever shares the tile's tag (plane 4). A walk trigger in triggers.yaml (`activation: walk`)
  is the floor trigger; a switch is walls.yaml `switch:`. Both call the same actions, and so can
  an actor state (ActorActionRegistry) and Lua (`game.trigger(tag, ...)`).
- **A_SetWall** turns tagged walls into another wall id, and **A_ChangeTexture** swaps textures;
  both restart a play-once animation (animdefs, MapManager.RestartAnimations). That's the
  "crumbling" look for free: a play-once animation whose last frame is the rubble.
- **Missiles and projectiles** already stop on walls: player missiles in Missiles.Missile,
  enemies' in Projectiles.Projectile, both through ProjectileTryMove, which only says *whether*
  something blocked, not what.
- **Blasts** (A_Explode) spread tile by tile and stop at walls (IsSolidForBlast). It already
  breaks things a specific class's blast is meant for (`monster.blastedby`), and Planet Strike's
  detonator blows doors open for good (A_BlastDoors, DoorObject.Jammed).
- **Hitscan** shots trace to the wall they strike (LineOfSight.TraceShot), for bullet puffs and
  `puffs: false` walls, but the trace doesn't return the wall's tile.
- **Damage types**: an actor's `damagetype` (actordefs) already picks Death.<type> states, and
  a blast with none deals "Blast".
- **Saves**: TileMap, ActorAt, the planes, patches and the area connections are already in the
  level body, so a wall that's simply gone needs no save change. Damage taken and walls
  mid-crumble do.

## What a destroyed wall leaves

One routine, `MapManager.RemoveWall(x, y, leaves)`, used by every way a wall goes:

- TileMap 0 and ActorAt null (or the patroller mark that was there), as a pushwall leaves it.
- Plane 0 gets a floor code: the area of an open neighbour, picked as doors pick theirs
  (Doors.AreaNumberAt). If the wall stood between two areas, they're **joined for good**: their
  AreaConnect count goes up by one each way, as a door that never shuts (already saved), so
  enemies beyond hear shots through the hole and ConnectAreas finds the player's area.
- Plane 1's diagonal marker (400-403) is cleared and WallShapes goes back to square; a door-side
  mark (BitWall) goes with the wall.
- The wall's patch is cleared. Its texture changes and animation start stay, so a floor
  A_ChangeTexture changed under it still shows changed.
- Height (plane 3): `leaves: floor` (the default) clears it; `leaves: arch` keeps it, so a tall
  wall loses its bottom story and the rest stays up as an arch (the tall-walls arch rule: an open
  tile with a height above 1). A hole blown in a castle wall.
- Flats: BuildFlats already gives wall tiles their floor and ceiling (from plane 2), so nothing
  needs rebuilding. A wall's destructible block can name the floor and ceiling flat indices it
  leaves (plane 2); left out, what plane 2 already has.
- The tag stays on the tile, so later actions (scrollers, A_ChangeTexture on the floor) still
  find it.
- Actors in the wall (`in-wall: true` things such as the fireball emitters, lookout points) go
  to their Death state, or are removed if they have none.
- Never a map-edge tile, a moving pushwall's tile, or a door. Those are refused, with a console
  warning.

The automap and renderer read TileMap, so they need nothing more.

## Destructible walls in walls.yaml

A `destructible:` block on a wall id, alongside `switch:` (a new `MapDestructibleTranslation`
on MapTextureTranslation, Common):

```yaml
walls:
  40:
    north: CRACKED1
    destructible:
      damage-types: [Rocket, Blast]  # the damage types that break it
      health: 50               # damage it takes before it breaks; left out, one hit does it
      to: 41                   # turn into wall 41 instead (a more cracked stage); 0: it goes
      animation: WALLCRUMBLE   # a play-once animdefs texture shown before it goes
      sound: world/wallbreak
      spawn: [WallDebris, WallDebris, Explosion]
      leaves: floor            # or arch
      floor-flat: 2            # flat indices left (plane 2), optional
      ceiling-flat: 0
      actions:                 # run with the wall's tag as it breaks, like a switch's
        - A_OpenDoor
      secret: true             # counts toward secrets (total on load, found when it breaks)
      objective: sabotage      # a game-info objective, as switches have
```

- **damage-types**: the wall's "tag": the damage types that can break it, and nothing else
  does. A hit's damage type is the missile's or projectile's `damagetype` (actordefs), "Blast"
  for an A_Explode whose source gives none, and for a player's weapon its `weapon.damagetype`
  (new), else "Bullet" for a hitscan weapon and "Melee" for a melee one. So a chaingun breaks
  `damage-types: [Bullet]` glass, and only a rocket's `damagetype: Rocket` breaks a
  `[Rocket]` wall. Left out: nothing in play breaks it. Only actions and scripts can.
- **health** (optional): damage from matching hits adds up per tile (`MapManager.WallDamage`,
  sparse) until it reaches the health; left out, the first matching hit breaks it. A
  non-matching hit does nothing. A `to` stage starts with its own health, so stages chain:
  40 → 41 → gone, each with its own art, health and damage types.
- **animation**: the wall stays solid while a play-once animation plays (the existing animstart
  per tile), then goes. A `CrumblingWalls` list ticks them, like MovingWalls.
- **spawn**: actors put on the tile as it goes (rubble sprites, an explosion, a pickup behind a
  breakable crate wall).
- Enemy projectiles break walls too when their damage type matches. A `players-only: true`
  stops that.

Where damage comes in:

1. **Missiles and projectiles**: ProjectileTryMove gets a sibling that says which wall tile
   blocked (`BlockingWallTile`). Missile and Projectile, on a wall, call
   `DestructibleWalls.Hit(x, y, missile, damage)` before the Death state. The damage is the
   missile's own roll.
2. **Blasts**: A_Explode's spread, meeting a wall tile it can't enter, hits it once per blast
   with the blast's damage (falloff by distance if set). Each tile is hit at most once.
3. **Hitscan**: ShotTrace gains the wall tile it ended on. A player's shot that hits the wall
   (the MissPuff path) damages it with the shot's damage.
4. **Melee**: the knife's reach is checked against the wall faced, as Cmd_Use finds a switch.

## Actions (switches, floor triggers, actor states, Lua)

All of them act on the tagged walls (the switch's own tile excepted, as now), or an actor's own
tag from a state:

- `A_RemoveWall(["arch"])`: the tagged walls are gone at once, without a sound or effects
  ("simply be removed").
- `A_BreakWall(["animation"], ["sound"])`: the tagged walls break as their destructible block
  says (animation, sound, spawn, actions, secret). A wall with no block breaks with the
  animation and sound given here, else at once. "Animate to disappear."
- `A_DamageWall(damage[, "type"])`: damage to the tagged destructible walls, as a hit of that
  type would deal (a timed charge).

Lua: `game.removewall(x, y[, "arch"])`, `game.breakwall(x, y)`, `game.wallhealth(x, y)`, and an
`onwallbreak(x, y, wall, damagetype)` hook (Lua event hooks), which can refuse a break, as MayPush does
for pushwalls. Console (cheat): `breakwall [x y]` and `removewall [x y]`, the wall faced when no
tile is given.

They go in GameActions.cs and ScriptApi's lists; the tests that check registrations against
those lists will catch anything missed.

## Saves and net play

- Save v37: the per-tile damage (count, then x, y, damage) and the crumbling walls (x, y, start
  tic, the wall id) at the end of the level body. NetProtocol/NETSTATEVERSION bump for the
  mid-game join body.
- Breaking is game logic run the same on every machine (missile hits, rolls on US_RndT), so
  lockstep stays in step. Stock maps have no destructible walls, so the demos don't change. The
  demotest is still run to make sure.

## Phases

1. **Removal.** MapManager.RemoveWall (every step above), A_RemoveWall for switches, triggers and
   actor states, the console commands, game.removewall. Tests: area joining, arch vs floor,
   diagonal, door-side wall, edge refusal, save/load round trip (no format change expected).
2. **Destructible walls.** walls.yaml `destructible:` (Common record, YAML docs in
   mapdefs/wolf3d/walls.yaml), DestructibleWalls (Gameplay), damage from missiles, projectiles,
   blasts, hitscan and melee, damage-type matching (`weapon.damagetype` on weapons, Bullet and
   Melee defaults), optional health and `to` stages, sound, spawn, actions, secret, objective.
   Save v37. Tests.
3. **Breaking with an animation.** CrumblingWalls (solid until the animation ends), A_BreakWall,
   A_DamageWall, game.breakwall/wallhealth, the onwallbreak hook. Tests.
4. **Example, editor, docs.** A destructible-demo mod (or a room in switch-demo) built by a
   script, with original art (cracked stages, a crumble animation, debris) and a room for each
   way: shoot it, blow it up, a switch that crumbles a wall, a floor trigger that removes one.
   Editor: the Definitions window's walls schema gets the block, the 2D map marks destructible
   walls, and LevelChecks warns on a `to` that isn't a wall, a damage type nothing in the game
   deals, and destructible map-edge walls. Docs reference, manual-testing checklist entries.

Later, if wanted: a wall that **sinks** into the floor smoothly (a sub-story offset in DrawPost,
the wall solid until it's down), or one that drops a story at a time (plane-3 height going down;
no renderer change), and walls that come back (A_RestoreWall).

## Decided (user, 2026-10-28)

1. **What breaks a wall**: damage types listed on the wall (`damage-types`), not classes or a
   flag on the weapon.
2. **Health**: optional; without it, one matching hit breaks the wall.
3. **Bullets**: they can, like anything else, when their damage type matches and they've done
   the wall's health.
4. **Joining areas**: a hole joins the two areas for good.
5. **Tall walls**: per wall, `leaves: floor` (the default, every story goes) or `leaves: arch`.
6. **Crumbling**: the wall stays solid until its animation ends.
7. **Enemy projectiles**: they break walls whose damage types match, unless the wall has
   `players-only: true`.
8. **The example**: a new destructible-demo mod in examples/mods.
9. **Extras**: sinking walls, story-by-story drops and rebuilt walls are later (docs/dev/todo.md).

## Status

**Phase 1 done (2026-10-28, committed b8385172).**
- `MapManager.RemoveWall(x, y, area, arch)` does the tile itself. `Gameplay/DestructibleWalls.cs`
  does the rest:
  - `WhyNot` refuses doors, open floor, moving pushwalls and the map's edge.
  - The tile takes the area of the open floor beside it: neighbours whose own floor code is an
    area first, north and west first, as a door's does.
  - The areas round it are joined for good (AreaConnect counted up once, saturating at 255),
    then ConnectAreas runs.
  - Actors inside the wall go to their Death state, or are removed if they have none.
  - A trigger on the wall's object plane is cleared; a secret one counts as found.
- `A_RemoveWall(["arch"])` works from switches, triggers and actor states (GameActions'
  "Actors and switches alike").
- `removewall [arch] [tilex tiley]` (cheat) is in LevelEditCommands, and
  `game.removewall(x, y[, "arch"])` is in GameScripts and ScriptApi.
- No save change: the planes, TileMap, ActorAt, patches and AreaConnect were already in the
  level body.
- Tests: DestructibleWallsTests.
- Checked headless in E1M1:
  - Removal; refusals for a door, open floor and the edge.
  - Save then load: the tile stays open. `tp` onto it is refused on a fresh level and works
    after the load.
  - `arch` on a 3-story wall, screenshotted: the upper stories stay as an arch. An arch's faces
    come from the walls in line with it (the tall-walls rule), not the removed wall's own
    texture.

Not checked in play: switch/trigger/actor use of A_RemoveWall, enemies hearing through a joined
hole, diagonals, door jambs, in-wall shooters, a removed secret pushwall
(docs/dev/manual-testing.md 1.12).

**Phase 2 done (2026-10-28, committed 5f7b8b87).**
- **The block:** `MapDestructibleTranslation` (Common), as `destructible:` on a walls.yaml wall,
  documented in pfwolf-pk3's mapdefs/wolf3d/walls.yaml header and the docs' mapdefs reference.
  Its fields: damage-types, health, to, sound, spawn, leaves, floor-flat, ceiling-flat (named
  floor/ceiling until phase 4, when the editor's name hints would have made them texture
  pickers), actions, secret,
  objective, players-only.
- **DestructibleWalls:**
  - `Hurts` checks the damage type case-insensitively, players-only, and that the wall can go.
  - `Hit` rolls the damage only once the wall takes it, so stock levels draw the same random
    numbers as before. The damage adds up in `MapManager.WallDamage` until it reaches the health.
  - `Break` plays the sound, counts the secret and the objective, then either swaps in the `to`
    wall (keeping the door-side mark and restarting play-once animations, with fresh damage) or
    removes the wall. Removal sets the floor and ceiling flats and spawns `spawn`. Then the
    actions run as a switch's would, as the player who broke it.
  - `HitByMissile` finds the wall the missile's box reached into, a diagonal only on its solid
    side. `HitByBlast` uses the source's damagetype, else Blast.
  - `LevelObjectives` feeds Objectives.CountLevel.
- **Where hits come from:**
  - Player missiles: Missiles.Missile, through MissileHooks.MissileHitWall.
  - Enemy projectiles: Projectiles.Projectile, through ProjectileHooks.HitWall.
  - Blasts: A_Explode collects the wall tiles its spread runs into within the radius. Each is
    hit once, with falloff measured to the wall's middle.
  - Player gunshots: GunAttack's two misses, FireBullet's miss and each pellet. ShotTrace now
    carries the wall tile it struck. A gunshot's wall damage is the roll for that many tiles
    away, with no miss roll.
  - The knife: a missed blow traces 1.5 tiles ahead.
  - The player's damage share (player.damagedealt) applies, as on enemies. The damage type is
    the weapon's `weapon.damagetype`, else Bullet or Melee.
- Destructible walls with `secret: true` add to the level's secret total as it loads.
- Save v37 (WallDamage after the objectives); NetStateVersion 15.
- Tests: DestructibleWallsTests (types, stages, last stage's secret/objective/sound/actions,
  floor flat, players-only, missile box, blast type, saved damage).
- Checked in play in E1M1, with a scratch mod making walls 1-20 destructible:
  - Pistol shots (health 60, windowed with posted key presses, screenshot) broke the wall.
  - The knife broke it.
  - Lua-fired `TestRocket`s (parent Rocket, damagetype Rocket) broke it on the second rocket.
  - A summoned A_Explode actor beside it (Blast) broke it.

Behaviour to know about:
- A rocket with a damagetype hits a wall twice when its Death state explodes: once flying
  into it, once from its own blast, of the same type.
- `spawn` only happens when the wall is gone, not on a stage change.
- A secret counts on the stage that has `secret: true`.
- Not hurt by anything yet: enemies' hitscan (Monster's own MissPuff), a bouncing missile's
  bounce, Blake Stone's T_Bounce.

Not checked in play: players-only, `to` stages, spawn, actions, floor/ceiling flats, a secret's
count, objectives, pellets (A_FirePellets), FireBullet, a save mid-damage and loaded back,
diagonals.

**Phase 3 done (2026-10-28, committed 6d061e4a).**
- **Crumbling.** The block's `animation` names an animdefs animation that plays once. While it
  plays, the wall stays solid and every face (and a diagonal's) shows it through
  A_ChangeTexture's per-tile swaps, starting from that tic.
  - The walls crumbling are in `MapManager.CrumblingWalls` (x, y, end tic, animation), in the
    order they started.
  - `DestructibleWalls.Tick`, called from LevelLoop after the timed switches
    (LevelLoopHooks.TickWalls), finishes each one once its end tic comes: it undoes the face
    swaps (`MapManager.UndoTextureChanges`), then puts in the `to` stage or removes the wall.
  - Hits on a crumbling wall do nothing. A_RemoveWall on one stops it at once.
  - The sound, secret, objective and actions go off when the wall starts to crumble; the removal
    and `spawn` happen at the end.
  - A name that isn't a play-once animation breaks the wall at once, with a console warning.
- **Break order now:** game.onwallbreak (DestructibleWallHooks.MayBreak) can refuse, and the
  damage stays. Then the sound, secret, objective and actions. Then crumbling, or the finish
  straight away.
- **Actions:** `A_BreakWall(["animation"][, "sound"])` uses the block, or the given animation and
  sound for a wall without one. `A_DamageWall(damage[, "type"])`: with no type, it hurts whatever
  types the wall lists. Both work from switches and actors.
- **Console:** `breakwall [tilex tiley]` (cheat).
- **Lua:** `game.breakwall(x, y)`, `game.wallhealth(x, y)` (1 for a wall without health, nil if
  it isn't destructible), and the hook `game.onwallbreak(function(x, y, wall, type, by))`.
- Save v38 (the crumbling walls after the wall damage); NetStateVersion 16.
- Tests: crumbling and finishing, removal mid-crumble, a script keeping a wall standing,
  HealthLeft, A_DamageWall with and without a type, A_BreakWall on a plain wall, a bad
  animation, saved crumbling walls, the Lua functions and hook.
- Checked headless in E1M1, with the scratch mod's CRUMBLE (GSTONEA1 then BSTCELA1, 35 tics
  each):
  - `breakwall` showed both frames in turn while the tile refused `tp`.
  - A save made 10 tics in, loaded back, kept the wall solid until the end, then it broke.

Not checked in play: A_BreakWall / A_DamageWall from a real switch, the onwallbreak hook in a
level, a `to` stage after an animation.

**Phase 4 done (2026-10-28, uncommitted).**
- **examples/mods/destructible-demo**, built by examples/tools/make-destructible-demo.ps1, with
  original art and sounds drawn and synthesized there:
  - Textures: DWSTON, DWCRKA/DWCRKB (cracked stages), DWCRMA-C (crumble frames), DWGLAS,
    DWBORD, DWLEVR/DWLEVD (the lever).
  - DWRBA0, the rubble sprite.
  - Four WAVs, named in sounds/sound-seq.yaml.
  - The level, MAP01.
  - The hand-written YAML: mapdefs walls 70-78 and trigger 460, animdefs DWCRUMBLE, actordefs
    DemoRubble.
- **The demo's rooms:**
  - Glass panes (Bullet).
  - Boards (Melee, health 20, spawn FirstAidKit).
  - A two-stage cracked wall (Bullet/Melee, 40 + 40, the secret on the first stage, then the
    crumble and rubble).
  - A lever (A_BreakWall, tag 1) that crumbles a 3-story stretch, leaving an arch.
  - A walk trigger (A_RemoveWall, tag 2) that opens the way to the elevator.
  - The stone was first warm sandstone, which mapped onto the palette as grey blotched with
    orange, so it's grey now.
- **Played windowed (posted keys, screenshots):**
  - One bullet broke a pane.
  - Pistol shots took the cracked wall through both stages and the crumble; the secret counted
    1/1 and the rubble showed.
  - The knife broke the boards.
  - The lever was thrown and the arch stood with rubble under it.
  - Walking onto the trigger opened the exit room.
- **Editor:**
  - LevelChecks: A_RemoveWall/A_BreakWall/A_DamageWall count as tag actions and as wall-moving
    ones for reach, as do floor triggers' actions. A wall with damage types counts as passable.
    A floor trigger, or a destructible wall with actions, counts as using its tag.
  - New warnings: a `to` that isn't a wall, an animation that isn't a play-once animdefs one,
    and a destructible wall on the level's edge.
  - The status bar's tile line shows ", breaks (types; health)", and the palette labels walls
    "(breaks)".
  - The sound catalog has "Wall N breaking", and the art catalog has "Wall N crumbling".
  - The Definitions window and the docs' reference pick up the block by reflection. FieldSchema
    hints `animation` as a texture and `spawn` as a class. The flat fields became
    `floor-flat`/`ceiling-flat` so they don't get a texture picker.
- **Tests:** LevelChecksTests (reach behind breaking, lever and floor-trigger walls; trigger tags;
  the destructible warnings). LevelChecksGameTests now loads destructible-demo too: no errors,
  no warnings.

### Editor follow-up (2026-10-28)

- **Marks:** the 2D map puts a "B" badge on breakable walls (zoomed to 12 px or more), and the
  3D view's markers tint them orange-red beside push walls (purple) and switches (green).
  With tags shown, the yellow lines now run from every tile that acts on its tag
  (MapTiles.ActsOnTag): switches, triggers other than push walls', and breakable walls with
  actions. Before, only switches had them.
- **Make breakable…** on the map's and 3D view's right-click menu ("Edit how it breaks…" on a
  wall that breaks already) opens the wall's mapdefs entry in the Definitions window at its
  destructible block. A block with nothing set is started as `damage-types: [Bullet]`
  (EntryEditorViewModel.StartDestructible), to fill in and save into a mod. `damage-types` has
  its own field hint (FieldHint.DamageType), so it's one line ("Bullet, Melee"), not a row per
  item.
- **More level checks:**
  - Stages that go round (72 → 73 → 72).
  - A `spawn` class the actordefs don't have.
  - A damage type nothing deals. GameContent.DamageTypesDealt is Bullet, Melee, Blast and every
    class's `damagetype` and `weapon.damagetype`.
  - The last two are skipped when there are no actordefs.
- **Tests:** FieldNodeTests (the block started, and a block already there left alone) and
  LevelChecksTests (stages going round, a type nothing deals, a missing spawn, nothing checked
  without actordefs, ActsOnTag and IsBreakable).
- **Checked in the editor** (destructible-demo, driven with UI Automation and posted mouse
  messages):
  - The badges and the trigger's lines showed.
  - Level checks stayed at 0.
  - "Edit how it breaks…" opened wall 70 at its block.
  - "Make breakable…" started wall 1's block, which was then reverted.
  - The 3D view tinted the arch walls.

Not checked: clicking by hand; a Make breakable… saved into a mod and played.
