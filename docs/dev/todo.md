# To do

Ideas and follow-ups for later, newest last. Move an item out (or delete it) once it's done.

## Horde waves for Blake Stone and Planet Strike

Horde (co-op multiplayer: waves of enemies from game-info `horde: waves:`) is only offered in a
game whose game-info lists waves. Wolf3D, Spear and the shareware/demo packs inherit Wolf3D's;
Blake Stone and Planet Strike have none, so the lobby leaves Horde out there (commit a95e437,
which also removed the old hard-coded 4-Mutant fallback that hung those games).

- Add a `horde:` block to `pfwolf-pk3/gamepacks/blake/game-info.yaml`, shaped like Wolf3D's
  (`waves` of `enemies: { Class: count }`, `grow`, `break-seconds`, `spawn-tics`, `max-alive`).
  Planet Strike's game-info is a diff over Blake's, so it inherits the block; give it its own only
  if Blake's classes aren't all there.
- Use ordinary hostile enemies from `actordefs/blake/` (and `actordefs/planetstrike/`): no
  informants, scientists, machines, `monster.becomes` sleepers or NOTCOUNTED crates. Ramp up over
  about 5 waves, with a tougher class in the later ones.
- Content only: no C# changes expected.
- Check: the test suite passes, and a headless run (`--game blake --nowait --cheats --warp MAP01
  --exec "wait 10; god 2; addplayer; gamemode horde 1; wait 900; match; quit"`, SDL drivers set
  to dummy) shows "Wave 1: N left", not "No Horde waves in this game".

## Missing digitized sounds

Some digitized sounds never play: the game prints `Asset not found: <NAME> (Type:
Wolf3dDigitizedAudio)` when they're asked for. Seen 2026-10-17 during a play-through (older than
the code cleanup, not caused by it):

- `DSPLDETH`, the player's death scream (Wolf3D, on dying)
- `DSSCBATK`, Schabbs' attack (Wolf3D E2M9)
- `DSAGLATK`, the Angel of Death's attack (Spear, the last floor)

- Find out whether the sound is really missing from the game's VSWAP digitized sounds, or whether
  its name doesn't match the one sound-seq/actordefs ask for (pfwolf-pk3 `sound-seq.yaml`, the
  digitized sound list in gamepack-info, `Wolf3dDigitizedAudio`). Wolf3D's own `DIGIMAP` plays a
  death scream and Schabbs' and the Angel's attacks, so the data is there.
- Check the rest of Wolf3D's and Spear's digitized sounds for the same problem while there.
- Check: `--game wolf3d --nowait --cheats --warp MAP01 --exec "wait 10; hurt 200; wait 100; quit"`
  (SDL drivers set to dummy) no longer prints `Asset not found: DSPLDETH`, and the death scream is
  heard in a normal run.

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

## One place for each explanation (the rest of pfwolf-pk3's YAML comments)

The docs site's reference is generated from the code (PFWolf.DocGen: the asset types' doc
comments, Definitions/), so an explanation of a field written again in a pfwolf-pk3 YAML comment
is a second copy to keep up. On 2026-10-21 two were moved: the switch block at wall 21 in
`mapdefs/wolf3d/walls.yaml` and the actor lights atop `actordefs/wolf3d/decorations.yaml`. Each
fact only the comment had went into the code's docs first (MapSwitchTranslation, GameActions,
ActorProperties, ActorSchema; the code had a switch's lock message wrong), the comment became a
pointer to the reference, and what pointed at the comment (examples/README.md, the example mods,
the editor's script hover and completion) was pointed at the reference instead.

`gamepacks/wolf3d/game-info.yaml` followed. Its comments explained nested records (a zone, a
scroller, a title screen) whose types' summaries DocGen had only used for a file's root, so they
never reached the reference; it now exports a nested record's type summary too (the description
of a list's or dictionary's items, else `about`, which the website shows under the section's
heading).

Then `gamepacks/gamepack-info.yaml`, whose header was rules more than fields: how files combine,
`!remove` and `!replace`, standalone games. Those went to a manual page of their own,
`docs/manual/combining-files.md`, checked against the loader (its table had missed factions,
translations, animdefs, graphicdefs and `demos/{pack}/`, and said a mod can't carry menudefs,
which it can since menu hooks).

Left to do the same way, the files with the most explanation of fields:

- the tops of `mapdefs/wolf3d/doors.yaml` and `patches.yaml`, `animdefs/animdefs.yaml`,
  `gamepacks/wolf3d/translations.yaml` and `factions.yaml` (the last two are rules as much as
  fields: keep what isn't a field's meaning, say where an actor's faction or translation comes
  from, in the code's type docs or the manual)

For each: diff the comment's facts against what the reference already says; put the missing ones
in the code's doc comments (checked against the code, as the wall-21 one showed the YAML can be
the right one); make the comment a pointer; grep for what points at it (`described in`, `at the
top of`, the file's name) and repoint. Not field docs, and not part of this: the converter's notes
atop `actordefs/wolf3d/guards.yaml`, `bosses.yaml`, `ghosts.yaml` and `projectiles.yaml` ("DRAFT -
converted from WL_ACT2.C ... please double check"), which want checking and trimming of their own.
- Check: the test suite passes (DocGenTests: every field still described); the reference shows
  what the comments said.
