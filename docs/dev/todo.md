# To do

Ideas and follow-ups for later, newest last. Move an item out (or delete it) once it's done.

## Holstering and raising weapons (A_Lower, A_Raise)

Switching weapons is instant, as in Wolf3D and Blake Stone (`Gameplay/WeaponInHand.cs`
SyncWeaponSprite puts the new weapon straight on its Ready state). A weapon could instead holster
and the next one come up, ZDoom-style, for the mods that want it. The base packs keep the instant
switch the originals have. Their unused `Select`/`Deselect` states, which ran `A_Raise`/`A_Lower`
that nothing handled, were taken out of the YAML on 2026-10-21.

- A weapon change with a `Deselect` state on the old weapon plays it first: `A_Lower` drops the
  weapon sprite a step a tic, and once it's off the bottom of the view the new weapon goes on its
  `Select` state, where `A_Raise` brings it up a step a tic until it's in place, then on to Ready.
  A weapon without those states switches at once, as now, so no existing content changes.
- Firing and switching again are ignored while lowering or raising (ZDoom's rule); work out what
  a weapon picked up mid-attack does (SyncWeaponSprite carries the attack over today).
- The speed as an argument (`A_Lower(6)`, `A_Raise(6)`) or a `weapon.*` property; add it to
  Definitions/ActorProperties.cs if it's a property.
- It draws as a vertical offset on the weapon sprite, added to the walking bob that already
  exists (`Gameplay/WeaponBob.cs`, game-info `weapon-bob`). Keep it out of saved games, as the bob
  is: a loaded game starts with the weapon up.
- Register `A_Lower` and `A_Raise` (actor states, any weapon) and add them to
  Definitions/GameActions.cs, so the YAML check accepts them.
- An example mod (`examples/mods/`) that gives a weapon or two `Select`/`Deselect` states, to
  show it off and document it.
- Check: the test suite passes (with tests for the lower/raise timing and the instant switch
  without the states), and in the example mod a weapon change visibly holsters and raises.

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
