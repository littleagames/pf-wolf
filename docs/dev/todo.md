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
radar's pushwalls, and security lights hearing a noise for a tic longer (bstone's `alerted`).
Then (2026-10-24): Planet Strike's help-page shapes, the points ratio (points earned out of what
the floor's counted enemies and treasure are worth), the random flickering light, lamps lit a
little (`lighten`) rather than drawn unshaded, the water puddle's 15-health rule, the detonator
caught in a blast waiting 20 tics (Death.Blast), the informant's last-points hint, and the
elevator panel's overhead map.

**Aliens of Gold: gameplay**
- Outlet and Goldfire warp-site timers aren't saved: `LevelAI` re-rolls them when a level is
  set up, including after a load (`PFWolf/Managers/LevelAI.cs`).
- Actors aren't kept off area-0 elevator floors.
- The electro-alien, Goldfire and the liquid alien's attack timing are simplified; compare with
  bstone.
- The lamps' `lighten` 6 is a conversion of bstone's -12 of 64 shade levels to our 32 fade
  steps; check it by eye against bstone on a dark floor.

**Aliens of Gold: level flow and UI**
- Text presenter: no `^DS` display strings or `^PM` music by number. None of the game's texts
  use them.
- Blake Stone's shareware data (`.BS1`) isn't supported.
- Menus: laid out as bstone's (positions, spacing, cursors checked against 3d_menu.cpp). Ours
  has Video, Messages, Automap Stats and Mods in Game Options; bstone's Switches submenu
  (lighting, REBA attack info, show ceilings, show floors) isn't there.

**Planet Strike**
- 82 of its 93 barrier switch walls have no open floor beside them, in bstone's data too: they're
  thrown from afar (floor triggers 158-160, an enemy's death linked by floor code 79), which
  already works. Nothing to do.
