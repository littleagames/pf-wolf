# To do

Ideas and follow-ups for later, newest last. Move an item out (or delete it) once it's done.

## Example mods as downloads on GitHub Releases

The docs site's mod catalog (website repo, `docs-site/catalog/`, added 2026-10-21) lists the
example mods as `bundled:`: they come with PFWolf in its `mods` folder, so their pages say to
switch them on, and link their folder in this repository, with no file to download. Publishing
each as a `.pk3` on the GitHub release would let someone with an older or newer PFWolf, or just
the catalog, get one on its own.

- On each release, zip each `examples/mods/<id>` folder's contents (not the folder itself) as
  `<id>.pk3` and attach them to the GitHub release, beside the game's own build. A script in
  `examples/tools/` (or a CI workflow on tag push, if pf-wolf gets CI) keeps it repeatable.
- Give the example mods' `modinfo.yaml` an `id` (their folder names) and a `pfwolf` range, now
  that a release pins them to a version.
- In the website repo, `docs-site/scripts/catalog.mjs`: a bundled mod's page also links its
  `.pk3` on the latest release (`<repo>/releases/download/<tag>/<id>.pk3`), alongside "comes with
  PFWolf". Decide whether a bundled entry gives the link itself or it's worked out from the tag.
- Check: the release has a `.pk3` for each example; each loads with `--file <id>.pk3` on that
  release (`mods` lists it, nothing warned); the catalog's links download them.

## Blake Stone: where PFWolf still differs from bstone

Checked against the code on 2026-10-23. bstone's source (the behaviour reference) is at
`D:\projects\Wolf3D\wolf4sdl sources\bstone-master`. The goal (decided 2026-10-27) is parity with
bstone as far as the engine allows, so every difference below matters. Untested items are
in `docs/dev/manual-testing.md` §1.9–1.10, not here. Fixed since the list was made: the random
charge pack, the quit and menu questions, ^AN animations, the lose screen's scrolling, the warp
out and in, no firing during Goldfire's morph, the shootable detonator, door blasts and the
radar's pushwalls, and security lights hearing a noise for a tic longer (bstone's `alerted`).
Then (2026-10-24): Planet Strike's help-page shapes, the points ratio (points earned out of what
the floor's counted enemies and treasure are worth), the random flickering light, lamps lit a
little (`lighten`) rather than drawn unshaded, the water puddle's 15-health rule, the detonator
caught in a blast waiting 20 tics (Death.Blast), the informant's last-points hint, and the
elevator panel's overhead map.

Then (2026-10-24, later): outlet and warp-site timers saved with the level, enemies kept off
the elevators' area 0 (floors monster-keep-out), and bstone's Game Switches (lighting, REBA
attack info, show ceilings, show floors) as Game Options settings on a Blake Switches screen.

Then (2026-10-24, last): the attack info shows each attacker's own bstone message with its
animated picture (actordefs `attackinfo`, info area ^AN/^SH/^FC/^BG/^DM/^LM), ooze hurts again
(it now parents BlakeMonster for A_HurtPlayerHere), and the lamps' `lighten` 6 checked against
bstone's shading: AOG's shade_max 51 / normalshade_div 6 is the same inverse curve as our
fade-start 4.52 / max-fade 80, and -12 of 63 levels is 6 of our 31 steps. A lamp is only a little
brighter than an unlit sprite in bstone too.

Then (2026-10-24, after that): the electro-alien fires a stream of bolts (bstone runs its T_Shoot
as a think, so one a tic for 14 tics); Goldfire and the liquid alien were checked and already
match bstone. Lighting as bstone's `lighting` field: a state modifier `lighten(N)`; explosions
(floating bomb, transport, turret, generator, security cube) lighten(26) for bstone's
EXPLOSION_SHADING, the bomb's and transport's dying back; a hitscan shooter's frame after it
fires lighten(5) (T_Shoot's -10); the generator and security cube lit as lamps (lighten 6)
rather than bright. Planet Strike's floors take bstone's per-floor shading (area number mod 6)
instead of all having Aliens of Gold's.

Then (2026-10-26): the flickering light's dim frames darken as bstone's (LAMP_ON_SHADING + 12
or + 16): lit is lighten(partial-bright), the first dim frame plain, the second lighten(lamp-dim),
-2 steps. A lighten level below 0 is darker; a frame's level counts alone when the actor has no
`lighten` of its own.

Then (2026-10-27): the outlet and warp-site waits checked through a save and load (headless,
BlakeLevelAIGameTests; the `levelai` cheat shows them). That found a hang: an electro-sphere
next to an elevator floor (hidden area 0, kept out since 2026-10-24) took a way TryWalk refused
and its move loop never ended (AOG MAP04). A refused walk now counts as blocked; every AOG and
Planet Strike floor was run headless for 400 tics without a hang.

Then (2026-10-27, later): security lights, steam, ooze and the six mission bosses played headless
(BlakeMachinesGameTests: each boss sees, hits, dies and drops the gold card). Two fixes: steam
vents never let off steam, as nothing standing still was ever flagged on screen (the renderer now
flags any drawn thing, as bstone's FL_VISIBLE); and ooze rolled its 1-in-8 bite every tic of its
third frame (a think) rather than once a round (now an action), so it hurt about 30 times too
often.

Then (2026-10-27, last): Planet Strike's teleporter map warps out and in, as bstone's
`select_floor` (ex_transported) does; it had just switched floors. A floor arrived at by a warp
fizzles in from black after "Get Ready, Blake!", where it used to fade in, for warp walls too.
The info area says "transporting..." on the black view and through the fizzle, as bstone's
DrawWarpIn (a fizzle-in frame now draws the info area and messages before it fizzles).

**Aliens of Gold: gameplay**
- Nothing known.

**Aliens of Gold: level flow and UI**
- Text presenter: no `^DS` display strings or `^PM` music by number. None of the game's texts
  use them.
- Blake Stone's shareware data (`.BS1`) isn't supported.
- Menus: laid out as bstone's (positions, spacing, cursors checked against 3d_menu.cpp). Game
  Options adds Video and Mods; Game Switches adds the engine's own settings after bstone's four.

**Planet Strike**
- 82 of its 93 barrier switch walls have no open floor beside them, in bstone's data too: they're
  thrown from afar (floor triggers 158-160, an enemy's death linked by floor code 79), which
  already works. Nothing to do.

## Engine gaps found making the example mods

Worked round in the example mods (2026-10-29), not fixed in the engine:

- **A usable actor's `usemessage` stays up while the player mans it.** `PlayerUse.UsableActor`
  picks the gun nest under the player (distance 0) while they're on it, so its "Press Use to man
  the gun" never goes. Boat Demo leaves the message off and has its script give the hint.
- **Nothing keeps a player on foot out of water the boat can cross.** Vehicles stop only at
  docks; a player can walk anywhere the boat goes. Boat Demo makes the river a damaging floor
  and has its script spare a player in the boat (`game.ondamage`). A `blocktile: players` (or a
  vehicle-only floor) would do it properly.
- **No use hint for getting out of a vehicle on Wolf3D's status bar**: `vehicle.exithint` names
  a statusbar.yaml use-hint, which Wolf3D's bar hasn't got. Boat Demo's script shows a keyed
  message beside a dock instead.

## Destructible walls: extras

Left out of the destructible walls work (docs/dev/destructible-walls-plan.md) at the user's
choice, 2026-10-28:

- A tall wall that drops a story at a time until it's gone (its plane-3 height going down every
  so many tics; no renderer change).
- A wall that sinks smoothly into the floor (a sub-story offset in DrawPost; solid until it's
  down).
- Walls that come back: A_RestoreWall puts a removed wall back, refused while something stands
  on the tile.
