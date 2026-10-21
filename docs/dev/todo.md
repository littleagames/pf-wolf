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
