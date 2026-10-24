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
`D:\projects\Wolf3D\wolf4sdl sources\bstone-master`. Whether the goal is exact parity or just
"plays right" is still undecided, and that decides how many of these matter. Untested items are
in `docs/dev/manual-testing.md` §1.9–1.10, not here. Fixed since the list was made: the random
charge pack, the quit and menu questions, ^AN animations, the lose screen's scrolling, the warp
out and in, no firing during Goldfire's morph, the shootable detonator, door blasts and the
radar's pushwalls.

**Aliens of Gold: gameplay**
- WaterPuddle's max amount is a fixed 16 (`items.yaml`).
- Lamps are drawn full-bright; bstone only lightens them by 12 shade levels
  (`actordefs/blake/decorations.yaml`, `bright` flags).
- Score items count toward the treasure ratio, and the "points" ratio is the treasure ratio.
- FlickeringLight uses a fixed pattern; bstone's is random: 4–13 seconds on, a few dim tics
  (`actordefs/blake/machines.yaml`).
- Outlet and Goldfire warp-site timers aren't saved: `LevelAI` re-rolls them when a level is
  set up, including after a load (`PFWolf/Managers/LevelAI.cs`).
- Security lights go off on `MadeNoise`, which lasts only the tic it's made in. bstone's
  `alerted` is set with it but counts down over two tics, so a light that thinks before the
  noise's maker in the actor list still sees it next tic; ours can miss it
  (`BlakeMonster.Machines.cs`, T_SecurityLight). bstone also never reads `alerted_areanum`.
- Actors aren't kept off area-0 elevator floors.
- The informant's points-% hint is missing (room hints, gifts and the kill warning are done).
- The electro-alien, Goldfire and the liquid alien's attack timing are simplified; compare with
  bstone.

**Aliens of Gold: level flow and UI**
- Text presenter: no `^DS` display strings or `^PM` music by number. None of the game's texts
  use them.
- Planet Strike's help pages use its own `^SH` shape numbers past AOG's (211 on: detonator,
  automapper, promo pictures); presenter.yaml only has AOG's.
- Blake Stone's shareware data (`.BS1`) isn't supported.
- Check whether bstone's AOG elevator shows an overhead map; `gamepacks/blake/elevator.yaml`
  sets none up.
- Compare the Blake menus with the real game, screen by screen.

**Planet Strike**
- A detonator caught in another blast goes off at once; bstone's waits the blast's damage in
  tics (20). Only the fission detonator blasts doors, as in bstone.
- Check whether the walled-in barrier switches are just how the maps were built.
