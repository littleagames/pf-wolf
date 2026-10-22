---
title: Sounds and music
order: 4
---

# Sounds and music

**Resources → Sounds and music…** (F8) lists every sound and song the game has, to listen to:
the sounds `sounds/sound-seq.yaml` names, the digitized, AdLib and PC speaker sounds, and the
music, from the data files, pfwolf.pk3 and the mods (sounds as files in a mod's `sounds/` folder
included). Sound only plays on Windows for now.

The list can be narrowed by kind, by **In use**, **Not in use** or **From the mods**, and by name,
by what a sound plays or by what plays it: *guard*, *door 1*, *DSDROPN*. A sound is in use when
actordefs, mapdefs, sound-seq or game-info plays it; the engine itself plays some by name too.

Picking one shows:

- for a sound-seq sound, what it plays on each device. The game plays the best it has of those
  switched on in its sound options (a sound file of its own, else the digitized sound, else AdLib,
  else the PC speaker); pick one to hear just that. *Go to* shows that sound, or the one an alias
  stands for;
- **Where it comes from**, the last of them being the one the game uses;
- **Played by**: the actordefs, mapdefs, sound-seq and game-info entries that play it.

Space (or Enter, or a double-click in the list) plays it, from where the bar under it is clicked
too, with **Loop** to play it again as it ends, as songs loop in the game, and a volume.
*Export WAV…* saves a sound as a 16-bit WAV, as the game plays it, and *Copy name* copies its
name. *Delete…* (in the list's right-click menu) takes a mod's own sound or song out of its mod,
saying first what plays instead.
