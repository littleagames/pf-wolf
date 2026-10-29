# Manual testing checklist

A by-hand pass over PFWolf and the editor, for the things the unit tests, `demotest` and headless
runs can't see: how it looks, how it sounds, how it feels to play, real dialogs, real networks
and real controllers. Items marked **(never tried)** have only been covered by tests or not at
all; start with those. Tick an item off once it has been seen working; write what went wrong
under it otherwise.

Work through it per game where a line says so: **W** = `wolf3d`, **A** = `wolf3d-apogee`,
**SW** = `wolf3d-shareware`, **S** = `spear`, **SD** = `spear-demo`, **B** = `blake`,
**PS** = `planetstrike`, **SA** = a stand-alone game (`examples/mods/standalone-demo`).

## Before starting

- [ ] Build x64 (`-p:Platform=x64`) so the exe sits beside the game data; build the editor too.
- [ ] Back up `%APPDATA%\PFWolf` (configs, `controls.cfg`, `mods.cfg`, saves) and
      `%APPDATA%\PFWolf\Editor\editor.json`: several tests below change them.
- [ ] Have the data files for every pack you can (shareware WL1 and Spear demo SDM files were not
      on disk last time, so those packs have never been run by hand).
- [ ] Have a game controller, a second PC on the LAN and, for internet play, a friend outside
      the network, for the sections that need them.
- [ ] Keep a scratch mod folder for test edits, not `examples/mods/*` (the editor auto-loads
      `standalone-demo`, so don't save into it by accident).

---

# Part 1: PFWolf

## 1.1 Startup and game choice

- [ ] With no `--game`, the game is picked from the data files present (try with only WL6, only
      SOD, only Blake files in the folder).
- [ ] `--game` boots each pack: W, A, SW, S, SD, B, PS. Each reaches its title without warnings
      on the signon screen.
- [ ] **(never tried)** `--game spear` with only the SDM files present falls back to the demo.
- [ ] **(never tried)** No `--game` with only SDM files picks `spear-demo`.
- [ ] **(never tried)** The Wolf3D GT files are picked as `wolf3d`, the Apogee ones as
      `wolf3d-apogee` (md5 picking of same-named files).
- [ ] Signon screen (W, S): engine version + commit, game, mods, video, input, sound, content
      lines read right; a warning shows on the warning line and the rest are counted.
- [ ] Window title is the release's title for each pack.
- [ ] **(never tried)** Drag a `.pk3` onto `PFWolf.exe` in Explorer: the game starts with that
      mod (and finds `pfwolf.pk3` though the working folder is elsewhere).
- [ ] Quitting during startup doesn't overwrite `controls.cfg` / `binds.cfg`.

## 1.2 Title sequence and menus

- [ ] W: intro + title loop run in order (signon → PG13 → title → credits → high scores → demo).
- [ ] S: the same with Spear's screens and palettes.
- [ ] **(never tried)** SW and SD title sequences.
- [ ] **(never tried)** B: Blake's intro (JAM movie), title loop, `on-key` actions; a key skips.
- [ ] PS: title fizzle and loop.
- [ ] **(never tried)** `on-key: ignore` on a movie really ignores keys.
- [ ] Each main-menu item opens and returns (New Game, Sound, Control, Load, Save, Options, Read
      This / View Scores / Back to Demo / Quit) in W, S, B.
- [ ] Options submenus: Video, Game Options, Messages, Mods, Controls, Controller. Re-open Sound
      and Control after the Options layout change: nothing overlaps.
- [ ] B: Blake menus (highlight bar, selection pic, checkbox offsets), Instructions / Story /
      Ordering Info pages with page line and music, F1 help text, bevelled message boxes.
- [ ] Read This! (W/SW) pages, and reopening it after the last page.
- [ ] Quit prompt shows a random quit line; Y quits, N returns.
- [ ] Menus with a controller (D-pad/stick move, A select, B back); held buttons don't skip
      through several menus.

## 1.3 Video and display

- [ ] Video menu: resolution list incl. Auto, fullscreen toggle, UI scale (fractional too),
      Uncapped FPS checkbox. Changes apply and are kept after a restart.
- [ ] **(never tried)** Fullscreen + Auto resolution on each monitor (laptop panel and external).
- [ ] Widescreen (16:9, 16:10, 21:9 windowed): Hor+ view, status bar side color, HUD anchors at
      the edges, menus centred, automap fills.
- [ ] UI scale 1, 1.5, 2, 3: menus, status bar, messages, console all scale and stay on screen.
- [ ] Uncapped on: smooth turning and strafing; `vid_maxfps 60/144/0` caps as asked; VSync on in
      a window when uncapped.
- [ ] Uncapped off: classic 70 Hz loop, no stutter.
- [ ] On the docked laptop with two displays (165 Hz panel + 74 Hz external): no periodic hitch
      every ~10 s in either window position.
- [ ] Crosshair (Game Options) sits on the view centre at every resolution, with pitch.
- [ ] **(never tried)** Blake level-start message and PS screens in widescreen.

## 1.4 Controls

- [ ] Keyboard: move, strafe, run, use, fire, weapon slots 1–0 (pressing again cycles in a slot),
      prev/next weapon, automap keys.
- [ ] Mouse: turn, fire, X1/X2 buttons, wheel = prev/next weapon; F12 grabs in a window.
- [ ] Mouse look (`m_look 1`), `m_invert`, look keys PgUp/PgDn/End.
- [ ] Customize Controls screen: scrolls, capture key/mouse/pad, Esc or 6 s cancels, a taken
      input moves from its old action (play and automap kept apart), reset row.
- [ ] `controls.cfg` round trip: rebind, quit, restart, still bound; old `readyknife`-style names
      still load.
- [ ] **(never tried)** A real controller: hotplug in and out mid-game, left stick move + strafe,
      right stick turn, **right stick look up/down**, dead zone, pad bindings column, Controller
      Settings page, menu navigation.
- [ ] **(never tried)** Customize screen rows for look up/down/centre.
- [ ] Always Run (Game Options): run without holding, holding Run walks; feels right in play.
- [ ] Freelook off: pitch stays level for keys, mouse and stick.
- [ ] Console binds on mouse buttons / wheel / pad buttons fire; automap keys don't also fire
      play actions while the automap is up.

## 1.5 Game Options and settings

- [ ] Each Game Options toggle (freelook, messages, automap stats, autosave, weapon bob, always
      run, crosshair) changes the game at once and survives a restart.
- [ ] "Default" state follows a mod's `setting-defaults` (try `moody-lights` or a scratch mod).
- [ ] Blake defaults (messages on) and Planet Strike (weapon bob) differ from Wolf3D as intended.
- [ ] (never tried) Weapon Bob + Bob Side to Side: the weapon swings left and right in an arc,
      dipping in the middle, wider walking than strafing into a wall, settling when you stop and
      holding still while firing; the Game Options window fits the extra row in Wolf3D and Blake.

## 1.6 Wolf3D gameplay (W, then spot-check S)

- [ ] New game on each skill; episode select; skill face pictures.
- [ ] Doors (normal, gold, silver, elevator doors), auto-close, blocked by actors/corpses.
- [ ] Pushwalls: push, can't push into a blocked tile, secret count.
- [ ] **(never tried)** Two pushwalls moving at once side by side: drawn right, no gap; save and
      load mid-move.
- [ ] Every weapon: knife, pistol, MG, chaingun; ammo use, refire, out of ammo switch.
- [ ] **(never tried)** SS drops a machine gun only while you have nothing better.
- [ ] **(never tried)** Switching weapon mid-attack (new weapon starts Ready).
- [ ] Gatling grin face when picking up the chaingun.
- [ ] Pickups: food, dog food, first aid, ammo, treasure (4 kinds), extra life, keys.
- [ ] Enemies: guard, SS, dog, officer, mutant, ghosts, every boss; pain, death, drops.
- [ ] **(never tried)** Ambush tiles (enemies wait), area sound alerting only connected areas.
- [ ] **(never tried)** Secret elevator / `A_SecretExit` goes to the secret level.
- [ ] Elevator switch only from the east/west faces.
- [ ] Death: turn to the killer, red fade, obituary message, lives down, level restart.
- [ ] **(never tried)** Death "see again" text, F5 Change View text, video-mode messages.
- [ ] Intermission: kill/secret/treasure %, par time, bonus counting, font.
- [ ] Boss death cam (Hans, Gretel, Hitler incl. Mecha → Hitler).
- [ ] Episode end: end text with pictures, victory screen (colon spacing), BJ victory run.
- [ ] **(never tried)** High-score name entry with a placing score, then View Scores.
- [ ] Automap: both styles, seen vs revealed, pan/zoom (wheel), stats line.
- [ ] Messages on: pickup texts, locked-door texts, obituaries ("killed by %k"), Spear bosses,
      ghosts, **(never tried)** `%o` player name and a door's own `lock-message`.
- [ ] Cheats: MLI, god mode, warp; turned off in netgames.
- [ ] Pause, Esc menu mid-game, End Game.

## 1.7 Saving and loading

- [ ] Save screen: new save row, name entry, overwrite same name, list sorted newest / by name
      (Tab), Del deletes, thumbnail preview + six info lines.
- [ ] **(never tried)** Load/save screen with the mouse and with a controller.
- [ ] **(never tried)** Left/Right filter "All games" shows saves from another pack.
- [ ] F8 quicksave (no prompt), F9 quickload (confirm), F9 with no quicksave opens the list.
- [ ] Autosave on level entry has a picture; not written after a load, a death restart or a demo;
      off in netgames.
- [ ] **(never tried)** Spear autosave through `A_ChangeMap` (secret level trips).
- [ ] Load a save made with a different mod set: warning, then it loads; the list shows
      "(other mods)".
- [ ] Save and load in the middle of: a door held open by a switch, a zone light fading, a
      strobe, a scroller, a moving pushwall, a boss fight, an animated switch texture.
- [ ] **(never tried)** Save/load mid-raise (medic), mid-follow (faction followers), with
      faction targets and translations, with a script's `temp1`.
- [ ] An old save (older version) is refused cleanly or converted (v7 tall-wall save → v8).

## 1.8 Spear of Destiny (S, SD)

- [ ] Blue menus, single episode skips the episode menu.
- [ ] Spear's weapons/items, Spear bosses, Angel of Death, ending (MAP21) with BJ collapse,
      end screens and captions, won-Spear high score.
- [ ] Secret levels via the elevator.
- [ ] **(never tried)** SD: plays to its last level and ends; map end-message; attract loop skips
      missing demos.
- [ ] Moody Lights's RedLight in Spear is separate (mod is wolf3d-only: check it isn't offered).

## 1.9 Blake Stone: Aliens of Gold (B)

- [ ] Mission select, briefing after skill pick (Esc back to skills), "Get Ready, Blake!".
- [ ] Status bar: score LED, ammo over weapon, charge gauge, card squares, ECG heart, info area.
- [ ] Weapons: ACP charge light, SFP, RAW, DND, PDU grenades (4 ammo/shot, explode on walls).
- [ ] **(never tried)** Grenade and hitscan damage on enemies.
- [ ] Enemies: rent-a-cop, pro guard, SWAT, bio-tech, aliens, pods/eggs, gurney mutants, spit.
- [ ] **(never tried)** Wounding, blowback, pain attack, SMART running away, interrogating an
      informant (hint text), killing an informant warning.
- [ ] Barriers: switch toggles linked barriers, walk through when off.
- [ ] **(never tried)** Arc barrier shock, re-activated barrier blocks again, barrier state after
      save/load.
- [ ] Food units: full, no token, vend, out of order.
- [ ] Elevator floor select; red card unlocks the next floor; teleporters; hub floors keep their
      state when revisited.
- [ ] Linked doors (floor 8 cell doors open together), walk-on color changes darken corridors.
- [ ] Machines: spheres, turrets, crates, floating bomb, generators → victory, outlets spawning
      electro-aliens, liquid alien, Goldfire warp.
- [x] Security light alert, steam, oozes, all six mission bosses (2026-10-27, headless:
      BlakeMachinesGameTests). Steam never hurts (bstone's doesn't); it only puffs while seen.
      **(never tried)** by hand: a boss fight to the death with real weapons, the bosses' attack
      info pictures, a level's own steam vents and oozes by eye.
- [ ] **(never tried)** End movie after a real win.
- [ ] Lose screen ("Goldfire Transmission") typed out, scrolling up as it fills.
- [ ] **(never tried)** High-score name entry with a placing score (ratio column).
- [ ] Weapon charge after save/load.
- [ ] Charge packs give 1–8 units, the message says how many.
- [ ] Elevator panel: the floor's overhead map (seen tiles, keys, Blake); **(never tried)** a
      hidden area in the darker green.
- [ ] Elevator stats: total points rises with kills and treasure (`count` shows Points).
      **(never tried)** an informant at 97–99% points saying where an enemy or treasure is left.
- [ ] Flickering lights: on for seconds, then a flicker, its dim frames plain and darker
      (`lighten lamp-dim -20` exaggerates the darker one). Lamps lit a little (side by side with
      an unlit and a bright copy, AOG floor 1: a small difference, as bstone's math gives).
      `lighten partial-bright N` retunes every lamp live (game-info lighten-levels); with
      Lighting, Show Floors and Show Ceilings on.
- [ ] Attack info: a bio-tech's animated picture beside "ATTACKING: BIO-TECH". **(never tried)**
      other enemies' pictures and texts (bosses, ooze, arc barrier, explosions, Planet Strike's
      own). Standing in ooze hurting is tested headless (BlakeMachinesGameTests).
- [ ] Status bar text as bstone's: health and ammo numbers plain (no shadow) with the ECG grid
      behind the health; the attack info's text with one shadow; the console keeps "ATTACKING:
      ..." as words, once while it's up. **(never tried)** Planet Strike's ammo number and the
      top bar's black shadow by eye.
- [ ] An electro-alien firing a stream of bolts (about 14) each volley.
- [ ] A new mission's first floor: "You must find the RED access card..." over the view, then
      with "Press a key." after ~2 seconds; not after loading a game. **(never tried)** Planet
      Strike's (area 0) text.
- [ ] The info area says "R.E.B.A. / AGENT: BLAKE STONE / ALL SYSTEMS READY." as a game starts
      and after a death, until the first message; then "NO MESSAGES.". **(never tried)** going
      on to the next floor keeps "NO MESSAGES.".
- [ ] Info area messages laid out as bstone's (centered lines, item picture, colored heading):
      seen for NO MESSAGES, a food token (TOKENS: 01), the slow fire protector, interrogating
      an informant (hint indented, gift) and the killed-informant warning. **(never tried)** the
      door, barrier, food unit, generator, transporting and Planet Strike detonator/teleporter
      messages, and the status bar hidden (they show as plain words over the view).
- [ ] Aliens of Gold's idle info area has a blank line above "NO MESSAGES." and above "FOOD
      TOKENS: n" (matched to DOS screenshots headlessly), and a food token says "FOOD TOKEN: /
      1 CREDIT" with no count; Planet Strike's keep bstone's tighter idle text and "TOKENS: nn".
      An AOG barrier switch adds "ON FLOOR n" under (DE)ACTIVATING BARRIER (seen on MAP02, game-info
      floor-text). **(never tried)** "ON SECRET FLOOR 1/2" on a secret floor's switch, and
      Planet Strike's in-game (no floor line).
- [ ] Palette cycling (game-info palette-cycles, bstone's CycleColors): the elevator map's
      player dot flashes black/white (seen). **(never tried)** keys flashing red on the elevator
      map, anything cycling in the menus, briefings and Planet Strike's teleporter map, and
      Wolf3D's menus unchanged (it has no cycles).
- [ ] TAB's popup map (game-info stats-popup, bstone's PopupAutoMap): Aliens of Gold shows the
      FloorStats picture with the whole floor map and quick stat bars, Planet Strike the stats
      only; any key closes it and play goes on (seen for both). The rotated map about Blake was
      seen with the default flipped. **(never tried)** Shift+TAB with a real keyboard (posted
      Shift didn't reach the game), a smaller view size's border after closing, the `automap`
      console command still opening the automap in Blake, and Wolf3D's TAB unchanged.
- [ ] **(never tried)** The auto charge pistol on WAIT: fire does nothing (no animation, flash or
      sound); held through, it fires as soon as READY lights.
- [ ] **(never tried)** Doors beside a floor trigger join their rooms: mission 2 floor 1's doors
      at 18,4 and 18,8 (west of them is door trigger 158); enemies through them hear shots, and
      their sound plays when the player's area is joined.
- [ ] **(never tried)** Explosions (floating bomb, transport, turret, generator) unshaded at a
      distance and the bomb's and transport's fading back as they burn; a generator lit a little
      before it blows; a distant guard a little brighter just after firing.
- [ ] Planet Strike's floors lit as bstone's per floor: area 0 from 1.07 tiles up to 57%
      (`fog`), area 3 to black far off. **(never tried)** the others by eye against bstone; a
      security cube's explosion slightly shaded on areas 2-3.
- [ ] **(never tried)** A water puddle only drunk at 15 health or less, healing past 16.
- [ ] Game Options → Switches: Lighting off (no shading), Show Floors / Show Ceilings off (plain
      colors), REBA Attack Info ("ATTACKING:" and the enemy when hit). **(never tried)** Show
      Ceilings by eye; the switches kept in config.cfg after a restart.
- [x] Enemies never step onto an area 0 floor (code 108, bstone's ElevatorFloor), nor off one.
      Checked 2026-10-27, headless on AOG MAP01 (BlakeKeepOutGameTests): row 18 of the room at
      6-20,14-22 is all area 0, and the guards chasing the player across it stop at row 19; with
      the rule off they step onto it. `actors` marks an enemy on such a tile "keep-out", and
      `where` shows plane 0's code. Area 0 isn't just the elevators: MAP01's start is area 1.
      **(never tried)** an enemy that starts on area 0 staying put.
- [x] Wall outlets and Goldfire's warp-in keep their timers through a save and load (2026-10-27,
      headless on AOG MAP04: BlakeLevelAIGameTests; `levelai` shows the waits). **(never tried)**
      with Goldfire already warped in once (last/chosen site, the later wait).
- [ ] Help pages (Instructions): ^AN animations (posts, arcs, turret, transport, guards) run
      while the page is up.
- [ ] Quit, End Game and new-game questions in bstone's words.
- [ ] Warp walls: on the same floor (MAP21 60,39) "transporting out", the view turns away and
      fizzles out, then "transporting..." where it leads; to another floor (MAP01 13,7) the
      same, arriving on the new floor. **(never tried)** Saving right after a warp. Checked
      2026-10-27 to another floor (MAP01 13,7, from 13,6): after "Get Ready, Blake!" the new
      floor's view fizzles in from black with "transporting...". Before that fix it faded in.
      The info area now shows "transporting..." on the black view and through the fizzle
      (checked by the PS teleporter, which takes the same warp in); a warp wall's not rechecked.

### Aliens of Gold v3.0 shareware (`--game blake-shareware`, the BS1 files)

- [x] Intro without the anti-piracy screen, v3.0's LINC screen in the title loop and menus (the
      terminal cleared in its color 4), missions 2-6 greyed and skipped by the cursor, mission 1's
      briefing, floor 1 plays; F10 quit shows the two order screens, then closes; Ordering Info's
      Planet Strike pages. Checked 2026-10-27; `--game blake` with only BS1 files plays it.
- [ ] **(never tried)** Logoff from the main menu shows the same order screens (same hook as F10).
- [ ] **(never tried)** Instructions' H_L / H_Shift key pictures; no `--game` with only BS1 files.
- [ ] **(never tried)** Mission 1 to its end: the shuttle movie, end briefing, high scores.
- [ ] **(never tried)** By ear: each enemy's one death sound, no SWAT wound sound; the digitized
      sounds the shareware left empty (8 bytes) stay silent.

## 1.10 Planet Strike (PS)

- [ ] **(never tried, no demos exist)** A real play-through of the first floors.
- [ ] Sector guard / tech warrior / alien protector names and sprites; bosses (giant stalker,
      spector demon, armored stalker, crawler beast); security cube.
- [ ] Morphing posts → spider mutant; killing a linked reptilian drops its posts.
- [ ] Cloaked cyborg shimmer and hit frame.
- [ ] **(never tried)** Door triggers 158–160 (smart switches) in play.
- [ ] Teleporter, detonator, gold morph, ending.
- [x] The teleporter map warps you out and in, as a warp wall does (bstone's ex_transported).
      Checked 2026-10-27, floor 0 (panel 29,63) to Bio-Tech Labs: the view fades back in after
      the map, "transporting out", it turns and fizzles out, "Get Ready, Blake!", then the new
      floor fizzles in with "transporting...".
- [x] Shooting a dropped detonator sets it off at once (and breaks the cube); no "your ally"
      message (2026-10-27, PS floor 1: dropped by the cube at 8,44, shot from 2-3 tiles off
      well before its timer). It's small and on the floor, so it sits behind the gun when
      straight ahead.
- [ ] A detonator blows the closed doors round it open for good (`doors` says "blown open").
      Checked 2026-10-27 headless on PS MAP01 (`summon PlasmaDetonatorArmed` beside doors): the
      doors beside two went open for good, one left rubble; the locked door 54,36 stayed shut,
      and a door two tiles off wasn't touched. Only the detonator blasts doors, as in bstone
      (its only BlastNearDoors call). **(never tried)** by eye: the doorway's explosion; a
      one-way door left shut; a blown door after save/load.
- [x] No firing while Goldfire morphs; firing again once he's morphed. Checked 2026-10-27 on
      PS MAP20 (summoned, `killall`, fire tapped throughout): no shot from his death until
      GoldfireMorphed came on, 139 tics (2 s at 70 a second, as bstone's 60 + 8 x 10), then
      the first tap fired. A game saved mid-morph loads with firing still off until he's done
      (save v36, headless: BlakeLevelAIGameTests; `levelai` shows "Player fire"); bstone
      doesn't save its noShots. **(never tried)** by hand, in the window.
- [ ] Radar at 4x shows pushwalls seen beside somewhere seen (cyan), not at 1x or 2x.
- [ ] Radar turned so the way you face is up: a thing on the tile ahead shows above you, the
      room in front at the top; facing east, what's south of you is on the right.
- [ ] Help pages and story: Planet Strike's own pictures (keys 6 and 7, cannon, detonator,
      the story's pictures, the cube animation).
- [ ] **(partly checked)** A detonator caught in another's blast goes off 20 tics later.

## 1.11 Rendering features

Use `--cheats` and the console (`tp`, `summon`, `where`) to get to them quickly.

**Tall walls, arches, sky** (`tall-walls-demo`)
- [ ] 2- and 3-story walls, tall textures, door lintels, arches with textured undersides, sky.
- [ ] Arch underside uses the ceiling flat when the tile has one.
- [ ] A tall door's lintel is flush with the wall, with an underside over the doorway (shut, open,
      opening); **(never tried)** standing in the doorway looking up, and a door set into no wall.
- [ ] **(never tried)** A pushwall carrying its height; diagonals with heights.

**Diagonal walls**
- [ ] Slide along a diagonal face; automap both styles.
- [ ] **(never tried)** Enemy sight and hitscan through the open half; rockets hitting the face;
      area number while standing in a diagonal tile.

**Wall sprites (fences, gates)**
- [ ] Seen from front, behind, oblique; collision and slide; guard can't see through a gate but
      can through a fence.
- [ ] **(never tried)** Rockets hitting a panel; knife reaching through a panel (expected to).

**Flats**
- [ ] Level-wide and per-tile floor/ceiling flats; see-through (0xFF) pixels.
- [ ] **(never tried)** An ECWolf map with flats; flats in Spear; high `vid_scale` frame rate.

**Footsteps** (`footsteps-demo`)
- [ ] **(never tried)** By ear: stone, dirt and metal steps sound right and sit under the other
      sounds; the walking and running cadence feels right (`stride` 2.5, `mintics` 12).
- [ ] **(never tried)** Another player's steps heard from where they are (network game).

**Wall patches + mirrored textures** (`patch-demo`)
- [ ] Ammo/button/lever/first-aid patches on the right faces; patch switches give items.
- [ ] **(never tried)** `flip:` mirrored faces (truck sides), patches stay unflipped, lintels and
      arches follow.
- [ ] **(never tried)** See-through door jambs.

**See-through doors** (`grate-doors-demo`)
- [ ] **(never tried)** A shut gate shows the room, floor, ceiling and guards behind it; a gate
      sliding open; two gates in a row; a guard standing in front of a gate and one behind it.
- [ ] **(never tried)** A guard behind a shut gate sees you, comes and opens it, but doesn't shoot
      through it; your shots stop at it; with `shoot-through: true` both get through.
- [ ] **(never tried)** Gates under a tall door's lintel; rain and snow behind and in front of a
      gate; a gate on the automap; save and load beside one.

**Shading** (`switch-demo` stations 6/7, `moody-lights`)
- [ ] `fog`, `light`, night levels; zone lights and tints; fade, strobe, pulse, flicker.
- [ ] **(never tried)** Zone effects set in game-info YAML; actions from actor states; a fade
      saved half way; arches/undersides and wall sprites in zones.

**Halo lights**
- [ ] Lamp halos, chandeliers, flickering lamp, colored lights, `lights` listing, muzzle flashes
      lighting a dark corner.
- [ ] **(never tried)** Doors opening/closing changing the spread; lights near diagonals;
      SwitchLight on/off in the dark; BareLightBulb.

**Weather**
- [ ] `weather rain/heavy-rain/snow/blizzard/none`: covered under ceilings, flats, arches, doors;
      splashes; gusts.
- [ ] **(never tried)** Weather with look up/down, widescreen, Blake/Spear palettes, fog/night
      levels, high `vid_scale`.

**Scrolling and animated surfaces** (`switch-demo` scroll room, stations 8/9)
- [ ] Scrolling walls and flats; switch starts/stops them; animated switch textures
      (STICKYLEVER, ALARMLEVER).
- [ ] **(never tried)** Scrolling on pushwalls, tall upper stories, diagonals, hub floors;
      save/load mid-scroll; animations by eye in general.

**View**
- [ ] Y-shearing: look up/down, centre, pitch reset on death; sprites and walls stay put.
- [ ] `eyeheight 6/32/56`; death drop from a mod with `death-drop-height`.
- [ ] **(never tried)** Eye height with diagonals, wall sprites, pushwalls, enemies close up.

**Big levels**
- [ ] **(never tried)** A real map using wall ids 65–255.
- [ ] A large (up to 256×256, non-square) UWMF level: plays, automap, save/load.

## 1.12 Gameplay features

**Switches** (`switch-demo`)
- [ ] Every station: doors open/close/hold, pushwall move, set wall, lights on/off, locked switch
      message, toggles, door-frame switches.
- [ ] **(never tried)** `A_MoveWall` with several tagged walls at once.

**Removing walls** (`removewall`, `A_RemoveWall`)
- [ ] **(never tried)** A switch and a walk trigger running `A_RemoveWall` on tagged walls; an
      actor state's (its own tag).
- [ ] **(never tried)** Walking and shooting through the hole; enemies on the far side hearing
      shots once it joins two areas; the automap showing open floor there.
- [ ] **(never tried)** A diagonal wall, a wall beside a door (its jamb), a patched wall and a
      wall with an in-wall shooter removed; a removed secret pushwall counting as found.
- [ ] A tall wall removed with `arch` (the upper stories stay as an arch) and without.

**Destructible walls** (walls.yaml `destructible:`)
- [ ] Pistol, knife, an enemy rocket of a listed damage type and a blast break a wall (seen with a
      scratch mod); a type it doesn't list does nothing.
- [ ] **(never tried)** Health adding up over several hits, then a save, a load, and the rest of
      the hits; `to` stages; `spawn` rubble; `actions` opening a tagged door.
- [ ] **(never tried)** players-only walls ignoring enemy rockets; `secret` adding to the ratio;
      `objective` counting; floor/ceiling flats left behind; shotgun pellets (A_FirePellets).
- [ ] A wall with an `animation` crumbles, solid, then goes; saved mid-crumble and loaded, it
      finishes (seen headless with `breakwall`).
- [ ] **(never tried)** A switch running `A_BreakWall` / `A_DamageWall`; a script's
      `game.onwallbreak` keeping a wall standing; a `to` stage after an animation.
- [ ] `destructible-demo` played through: glass, boards, cracked wall and secret, lever and arch,
      floor trigger, then the elevator (each seen in pieces with posted keys; never in one go
      by hand, nor heard).
- [ ] Editor: destructible-demo's MAP01 opened; the status bar's "breaks" text, the palette's
      "(breaks)", Check Level clean; the "B" badges, the lines from the floor trigger with Tags
      on, and the orange-red tint in 3D (all seen through UI Automation, not by hand).
- [ ] Editor: right-click a plain wall → Make breakable…, fill in the block, save it into a
      mod, play it, and shoot the wall down (the menu was only driven by UI Automation and
      reverted, never saved or played).

**Multi-key doors** (`key-demo`)
- [ ] **(never tried)** Open doors 80 (red+blue), 81 (all four), 82 (any two) in play; refusal
      messages name the missing key; the 2×2 key block on the status bar.

**Factions** (`faction-demo`)
- [ ] Allies fight axis; your shots don't wake allies; neutral brute only fights back.
- [ ] Prisoner: Use → joins and follows, steps aside to let you pass.
- [ ] **(never tried)** Followers through doors and across areas, fighting while following,
      retargeting to a nearer foe, `A_Follow`, ally-killed message, projectiles/blasts sparing
      allies, patrollers giving up.

**Medics** (`medic-demo`)
- [ ] **(never tried)** A real play-through by hand: medic revives corpses, bosses never raised,
      revive sounds by ear.

**Player classes**
- [ ] Class menu; damage/speed/ammo factors felt in play.
- [ ] **(never tried)** `damagedealt` in combat, `sidemove`, `restrictedto` items, armor placed
      in maps, armor reset on respawn.

**Holster Demo** (`holster-demo`)
- [ ] Switching weapons lowers the old one and raises the new one.

**Bob Demo** (`bob-demo`)
- [ ] **(never tried)** By eye, each weapon's style as the README's table says (only the pistol's
      U and the gatling gun's heave have been screenshotted); `give` the blue guns and Knife2 for
      the rest; a bogus `weapon.bobstyle` warns once; with Holster Demo on too.

**Camera and spectating**
- [ ] `spectate`: cycle actors, player body sprite.
- [ ] **(never tried)** Automap while spectating; save/load while spectating; spectating in Blake.

**Scripting** (`fireball-demo`)
- [ ] Fireball wall, `onlevel` messages, eruption every 840 tics.
- [ ] **(never tried)** Recording + playing a demo with hooks; hooks in a netgame (join reason
      "loaded"); hub return reason "returned" (Blake).
- [ ] **(never tried)** Event hooks in real play (`ondamage`/`onkill`/`onsight`/`onspawn` "new" are
      covered by ScriptHookGameTests): `ontrypickup` refusing an item and `onpickup` amounts;
      `onsecret` on a pushwall; `onuse` taking a door or switch press; `onplayerdeath` from a
      shot, time running out and being caught (stealth); `onspawn` "restarted", and "respawn" and
      "join" in a netgame.
- [ ] **(never tried)** The second batch of hooks in real play (`onactorspawn` at level start,
      `onfire` refusing, `onenter` for the player and `onhud` drawing are covered by
      ScriptHookGameTests): `ondoor` keeping a door shut against the player and an enemy;
      `onpushwall` and `onswitch` refusing; `onexit` holding a level open; `onteleport` on
      A_Warp/A_TeleportTag (and an enemy on a "monsters" pad); `onenter` for enemies; `onrevive`
      (medic-demo); `onfactionchange` (faction-demo prisoner); `onweaponswitch`, `ongive`,
      `onpowerup`; `onobjective`, `onchain`, `onstealthkill` (Midnight Assault-style mods);
      `ontimeup` on a timed level; `onfrag`, `onflag`, `onmatchend` in a netgame; `onmusic`
      and `game.music` by ear; `ontally` text on the Wolf3D tally (intermission.yaml `script`,
      top centre) and how it looks; a `game.command` typed in the console.
- [ ] **(never tried)** A* chasing in real levels (`game.onchasedir` returning `monster:pathto(target)`;
      ScriptHookGameTests covers one guard round one wall): many enemies at once (frame time with
      `perf`), locked doors and NOLOCKEDDOORS/NODOORS enemies, dogs (they dodge), Blake Stone's
      enemies, a 256x256 UWMF level, a pushwall moving across a path, and a netgame staying in step.

## 1.13 Sound and music (by ear)

- [ ] AdLib music in each game, incl. drum re-strikes; PC speaker and AdLib sound effects;
      digitized sounds.
- [ ] Sound and music volume sliders; music off/on.
- [ ] **(never tried)** Mod `music/` OGG/MP3/WAV/IMF: loudness next to IMF, loop seam, long MP3
      start-up time.
- [ ] **(never tried)** Mod `sounds/` WAV/OGG/MP3/.pcs/.adl.
- [ ] `playsound`, `changemus`.
- [ ] Sounds of new features: CTF/heist/domination cues, taunts, medic raise, switch sounds.

## 1.14 Mods

- [ ] `--file` with a folder, a `.pk3`, several at once (later wins).
- [ ] `mods/` folder next to the exe; Options → Mods: toggle, paging past 9, other-game mods
      greyed, restart prompt, `mods.cfg` written.
- [ ] **(never tried)** Options → Mods restart inside a stand-alone game.
- [ ] A mod with `key: !remove` takes base keys out.
- [ ] Mod `texts/` replaces HELPART / an end text; **(never tried)** Blake texts from `texts/`.
- [ ] Mod `maps/` `.wad` and `.uwmf` levels replace and add levels.
- [ ] Mod `graphicdefs/` crops (multiplayer-pk3 class faces).
- [ ] Pictures in nested `textures/…/` and `sprites/…/` folders load.
- [ ] Stand-alone game (SA): boots with no Wolf3D data, plays MAP01.
- [ ] **(never tried)** A mod game built on `wolf3d`; two game mods at once; saves and
      multiplayer in a stand-alone game.
- [ ] **(never tried)** A real WDC/ChaosEdit mod through `--make-mod <folder>`: the base game it
      picks, the report, then `--file` the made mod and play a few levels (new walls, sprites,
      pics and sounds by their placeholder names; an edited data file gives the "changed since"
      warning).
- [ ] **(never tried)** `--make-mod` on Spear, Blake and Planet Strike files; `--overwrite` after
      renaming chunks keeps the names.
- [ ] **(never tried)** Hub Demo (`--file hub-demo`), played by hand: each elevator lever and the
      pool go to their level and the lever flips; each level's elevator comes back in the right
      spot facing out; the emblems survive the trips and the vault door names the missing one,
      then opens with all three; the vault elevator wins (victory tally, no end text). Also:
      dying in a level and in the hub, saving and loading mid-hub (kept levels, entrance), and a
      second trip into a finished level (back at its elevator, enemies still dead).

## 1.15 Multiplayer

Loopback first (`--host` / `--join 127.0.0.1`, several windows), then LAN, then internet.

- [ ] Lobby: name, class, color, team rows; ready; chat; "N more" when scrolled; rules row;
      host's LAN/net address note.
- [ ] **(never tried)** Two real PCs on the LAN: find the game, join by address.
- [ ] **(never tried)** Internet: Check Internet Port, UPnP Open Port success, a friend joining.
- [ ] **(never tried)** Laptop docked with Ethernet + Wi-Fi both up: the right local address.
- [ ] Streamer Mode: IPs hidden, masked join field, Copy Address works.
- [ ] Co-op: shared kills/secrets, doors, any player's exit ends the level, intermission at
      different times, death + respawn.
- [ ] **(never tried)** Secret exit and victory in a netgame; Spear netgame.
- [ ] Mid-game join; leave with Esc+Y; host leaving shows a message to clients.
- [ ] `net_status` checksums match throughout.
- [ ] **(never tried)** 3–4 players over a real network; **5–8 players** at all.
- [ ] Deathmatch on DM01–DM04 (`multiplayer-pk3` on): frags, frag/time limit, scoreboard, item
      respawn, rocket/flamethrower frags **(never tried over the network)**.
- [ ] Game modes: TDM, CTF (drop/return), Heist, Domination, KOTH, Horde (HE01 too).
- [ ] **(never tried)** Mid-game join into an objective mode; TDM/Domination/KOTH over a network.
- [ ] Prop Hunt on PH01: disguise with Use, hide timer, hunt, decoy penalty, snap when still,
      taunts, lamp light, chase cam, lobby hide/hunt times.
- [ ] Play as enemies: lobby class pick, dog bite **(never tried: damage)**, enemy class faces on
      the status bar; **(never tried)** class menu in single player with the mod.
- [ ] Player colors in a real netgame and after a mid-game join.

## 1.16 Demos

- [ ] All W and S demos play in sync in the attract loop.
- [ ] Record a demo (hold Tab leaving the title), play it back with `playdemo`.
- [ ] A mod's `demos/` demo plays by name; title loop cycles DEMO0–9.
- [ ] Demos stay in the classic loop with uncapped frames on.

## 1.17 Console sweep

- [ ] Console opens/closes, scrollback holds the signon warnings, history works.
- [ ] Spot-check each: `where`, `tp`, `summon`, `actors`, `count`, `doors`, `tag`, `height`,
      `wallheight`, `sky`, `fog`, `light`, `zone`, `zonelight`, `zoneeffect`, `lights`,
      `weather`, `scroll`, `eyeheight`, `pitch`, `spectate`, `setfaction`, `translations`,
      `playercolor`, `saves`/`save`/`load`, `exportmap`, `mods`, `perf`, `msg`, `scoreboard`.
- [ ] Single-player-only commands refuse in a netgame; cheats refuse without cheats on.

## 1.18 Performance

- [ ] `perf` during play at 960×600 and at the largest resolution: frame time and garbage per
      frame stay low; no GC hitches.
- [ ] Heavy scenes: many lights + fog + weather + tall walls together.

---

# Part 2: The editor

## 2.1 Start-up and loading

- [ ] Splash shows load status, closes when loaded; `--nosplash` skips it.
- [ ] Command line `PFWolf.Editor.exe <game folder> [mods...]` opens that folder and those mods.
- [ ] Each game loads: W, A, S, SD, SW, B, PS, SA; *By the data files or mods* picks right.
- [ ] Mods list: Add pk3…, Add folder…, Remove, right-click menu; pfwolf.pk3 can't be added.
- [ ] Changing mods shows the reload banner; *Load the game again* applies it.
- [ ] Problems loading panel lists a broken picture / bad YAML from a scratch mod.

## 2.2 Map view

- [ ] Level list; open each level; Blake hub names "M1 F2" and dimmed unused slots.
- [ ] Plane toggles (walls, patches, things, flats, heights, tags, zones) draw their layer.
- [ ] Hover info in the status bar for walls, things, codes, links.
- [ ] Zoom; pan with middle/right/Alt drag.
- [ ] **(never tried)** Shift+wheel and Alt+wheel pan; letting go of Alt doesn't focus the menu.
- [ ] Things show facing notches, skill badges, ambush "A"; wall sprites; switch → tag lines.

## 2.3 Tools

- [ ] Select (S): Shift adds, Ctrl subtracts, drag to move, arrow-key nudge.
- [ ] Magic wand (W), joined only on/off.
- [ ] Paint (P) brush sizes 1–9, square/round; Erase (E); Scatter (A) density and random set.
- [ ] Line (L), Rectangle (R), Ellipse (O) with Shift and outline; Room (M); Fill (F).
- [ ] Stamp (T) click and drag; Pick (I).
- [ ] Free thing (G): place, drag, turn, Delete; Shift snaps to tile middle.
- [ ] Copy/cut/paste (paste follows the pointer, Esc drops), Ctrl+D duplicate, Delete,
      Ctrl+A / Escape, Invert selection, Ctrl+H Replace.
- [ ] Rotate `]` / `[`, flip Shift+H / Shift+V: enemies, doors and starts turn with it; status
      line counts what couldn't turn.
- [ ] Undo/redo every tool, many steps deep.
- [ ] **(never tried)** The new tools used while the 3D view is open.

## 2.4 Palette and properties

- [ ] Palette groups per plane; thing variants (facing, skill, patrol).
- [ ] Right-click a palette entry → edit its map definition / actor class opens Definitions.
- [ ] Level properties (F4): name, music, par, colors, sky, wall height, fog, zones, weather
      picker, scrollers grid; saved to the mod's `game-info.yaml` as only the changed keys.
- [ ] **(never tried)** Blake Floor number and Cluster fields.

## 2.5 Checks, saving, new levels

- [ ] F7 checks: problems listed, click goes to the tile; stock levels give no errors.
- [ ] Ctrl+S into the level's mod; Ctrl+Shift+S into another; into a folder and a `.pk3`
      (`.bak` kept, no `.tmp` left).
- [ ] Unsaved-changes prompt on reload and on close (levels, palettes, definitions).
- [ ] Known gap: closing the main window with a dirty pixel editor open; check whether it asks.
- [ ] Ctrl+N new level; resize a level (things move with it); UWMF as default save format.
- [ ] Ctrl+I import WDC `.map` / `.wad` planes onto a level or as a new one;
      **(never tried)** a `.map` written by WDC itself.

## 2.6 3D view (F3)

- [ ] Split view; camera keys; V puts the camera marker.
- [ ] Walls, doors, flats, tall walls, sky, wall sprites, patches, extra things.
- [ ] **(never tried)** Arches, fog, tag highlight under the pointer, patch quads, scroll
      animation by eye, see-through doors (`grate-doors-demo`) clear between the bars.
- [ ] Shading and markers toggles.
- [ ] Blake AOG floor 1: octagon floor + panel ceiling from its codes.
- [ ] Pick and edit from the 3D view; **(never tried)** clicking an extra thing.

## 2.7 Playing from the editor

- [ ] F5 saves a dirty level and launches the game on it; skill picker remembered.
- [ ] Shift+F5 starts on the hovered tile.
- [ ] Play into B floor 1 and PS floor 2.
- [ ] **(never tried)** Play a Blake hub floor after another floor of the same mission was played
      in the same run (hub state leak).

## 2.8 Blake level codes

- [ ] Level codes panel lists codes; picking one goes there; Edit… (Ctrl+E), Delete,
      Colors…/Flats….
- [ ] Badges (C, F, H, L, V, #, +) and link lines; door links drawn.
- [ ] **(never tried)** Paint a code from the palette by hand with a real click; "Pick on the
      map…" for a link's target with a real click.
- [ ] Copy/move a selection keeps code + value pairs together; Delete clears the value too.
- [ ] Save an edited Blake level with the editor's own Save and play it.

## 2.9 Textures and sprites (F6)

- [ ] Grid, search, origins, "used by", Use in palette, animation playback, cropped pictures tab.
- [ ] **(never tried)** Export PNG; Copy name (clipboard); flats from a real mod; Spear art.
- [ ] **(never tried)** Add / replace / rename / delete / offset art into a mod through the
      dialogs.
- [ ] **(never tried)** Edit outside with a real paint program, Bring in at once.
- [ ] Pixel editor: draw, undo, close prompt, save into a mod.

## 2.10 Sounds and music (F8)

- [ ] **(never tried by ear)** Play digitized, AdLib and PC sounds and every song; loudness.
- [ ] Go to sound → its sound-seq entry; variants list.
- [ ] **(never tried)** Export WAV; Copy name; music files from a mod; Spear's sounds.

## 2.11 Palettes (F9)

- [ ] List, grid, gradient, edit R/G/B, undo, "only the colors picked", Show filter.
- [ ] **(never tried)** Save / Save to dialogs; Import / Export; Copy/Paste by keyboard;
      Spear and Blake palettes.

## 2.12 Texts (F2)

- [ ] Article pages, HELPART warnings jump, live edit, Revert, click a word.
- [ ] **(never tried)** Save (Ctrl+S), Save to…, New… (Ctrl+N) through the dialogs; Spear and
      Blake texts; palette recolor of the open page.

## 2.13 Definitions (F11)

- [ ] Tabs for game-info, game packs, actors, mapdefs, factions, animations, Blake extras.
- [ ] Field edit → saves a one-key overlay into the mod; raw YAML tab; Edit YAML jumps to the line.
- [ ] New / Delete / Take out (`!remove`) and restore.
- [ ] pfwolf.pk3 and the pfwolf-pk3 folder refuse every save.
- [ ] "This level's own" read-only tab for UWMF levels.

## 2.14 Scripts (F12)

- [ ] Actions list, callers (click goes to the actor state in Definitions), hooks, problems.
- [ ] Highlighting, autocomplete, hover tooltips, F12 go to definition / Alt+Left back.
- [ ] A syntax error shows live; sandbox-missing names flagged.
- [ ] **(never tried)** Save / Save to / New dialogs; scripts in a `.pk3` mod.

## 2.15 Demos (Ctrl+Shift+D)

- [ ] Timeline, traced path, ghost replay for W demos.
- [ ] Watch in PFWolf.
- [ ] **(never tried)** Spear and Blake demos in the window; `.dmo` open dialogs; Add to mod…;
      replay scrubbed into a fight by eye.

## 2.16 Menus, right-click and delete

- [ ] Every menu bar item; tool strip tooltips (name, what it does, key).
- [ ] **(never tried)** Right-click on every list selects the row under the pointer first.
- [ ] **(never tried)** Right-click → Delete in each browser: confirm dialog, mod files only,
      the browser reloads.
- [ ] **(never tried)** Map grid: a right-drag still pans and shows no menu; a right click shows
      Create in mapdefs (greyed out on a tile with nothing unknown). On a red plane-0 tile it
      offers Wall/Door/Floor code, on a red "?" thing Thing/Trigger/Player start (plus Diagonal on
      a wall), and on a "?" patch badge the Patch. Picking one opens F11 on a new entry with that
      number (an existing one is just shown); Floor code opens floors. Save it into a mod and the
      red mark goes away.
- [ ] **(never tried)** Map grid and 3D view right click → Go to map definition (greyed out on a
      tile the mapdefs know nothing of): lists the tile's wall/door (or the floors codes), patch,
      and its object value's thing/player start/trigger/diagonal/door lock/map-info entry; picking
      one opens F11 on it. In 3D a right-drag still looks around with no menu, and the menu is for
      the surface clicked (a wall face is the wall's tile, not the floor in front of it).

## 2.17 New mod

- [ ] File → New mod… → stand-alone game: it plays at once in PFWolf.
- [ ] File → New mod… → mod of Wolf3D / Spear / Blake: modinfo only, levels editable.
- [ ] File → New mod from data files… (or From data files… under the mods): a folder typed or
      browsed to fills the base list, title and mod folder follow its name, Make it shows the
      report and loads the game; again on the same mod folder asks to write over and keeps the
      names. **(never tried)** Browse… (the folder picker) and the base list's other choices.
- [ ] New mod from data files… with renamed files (FOXTROT1.WL6...): Which file is which… opens by
      itself, sizes and "looks like" shown, a file can't be picked twice, the made mod plays.
      **(never tried)** a real mod's renamed files (Spear's, Blake's); a mod with only some files.
- [ ] New mod from data files… Palette: a .pal beside the data files fills it in, with a swatch;
      the mod's game is drawn in it. **(never tried)** Palette Browse…, a raw VGA palette and an
      .act; a palette that won't read stops Make it with its reason.
- [ ] Art browser: Show → Needs a name lists the made-up names; Name… renames one
      (raw-data-map.yaml and mapdefs walls follow); a level using an unnamed wall gets the F7
      check warning.

---

## When done

- Move each failure into an issue or `docs/dev/todo.md`.
- Restore the backed-up `%APPDATA%\PFWolf` and `editor.json`.
