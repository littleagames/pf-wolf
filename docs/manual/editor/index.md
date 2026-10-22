---
title: The editor
order: 9
---

# The editor

PFWolf Editor (`PFWolf.Editor.exe`) edits levels and what goes into them:
textures and sprites, sounds, palettes, texts, scripts and the definition files. It reads the game
the way PFWolf does, pfwolf.pk3 with your mods laid over it, and it saves into a mod, a folder or
a `.pk3`, never into pfwolf.pk3 or the original data files.

It opens Wolfenstein 3D (retail, Apogee and shareware), Spear of Destiny and its demo, and the
stand-alone games mods add. Blake Stone's games aren't in it yet.

| Page | What it's for |
| --- | --- |
| [Editing levels](./levels) | The map: planes, the palette, the tools, level properties, checks, playing it |
| [The 3D view](./3d-view) | The level in 3D beside the map, and editing in it |
| [Textures and sprites](./art) | Every picture, what uses it, and the pixel editor (F6) |
| [Sounds and music](./sounds) | Every sound and song, to listen to (F8) |
| [Palettes](./palettes) | Every palette, to look at and change (F9) |
| [Texts](./texts) | The help screens, end texts and briefings, page by page (F2) |
| [Scripts](./scripts) | The Lua the game runs, with what it gives and what calls it (F12) |
| [Demos](./demos) | The demos, and where everyone went in them (Ctrl+Shift+D) |
| [Definitions](./definitions) | The definition files, in fields or as YAML (F11) |

## Loading a game

1. **Game folder**: the folder `PFWolf.exe` and `pfwolf.pk3` are in. The game's data files are
   read from there, as PFWolf reads them.
2. **The game**: pick one, or leave it on *By the data files or mods*, which picks the game the
   data files are (or the stand-alone game a mod adds).
3. **Mods** (on the left): the mods loaded over pfwolf.pk3, later ones winning, as `--file` loads
   them. *Add pk3…* and *Add folder…* add one, *Remove* takes it out, and right-clicking the list
   has the same. pfwolf.pk3 itself can't be added: it's under every mod already.
4. **Load**. The levels appear in the list on the left, and the first one opens.

The editor remembers all of this for next time, and loads it as it starts. Changing the mods
afterwards shows a banner saying so: *Load the game again* reads them (*File → Load the game* does
too, and is also how to see changes you made to a mod outside the editor).

From the command line, `PFWolf.Editor.exe <game folder> [mods...]` opens that folder with those
mods in place of the remembered ones, picking the game by the mods, and `--nosplash` skips the
splash screen.

What couldn't be read goes in **Problems loading** at the bottom: a picture that won't decode, a
YAML value that's wrong. The game itself would warn about the same things.

## Saving into a mod

Whatever you save goes into a mod: the level's own mod when it came from one, else one you pick
(one of those loaded, or another folder or pk3). A level is saved as `maps/NAME.wad` in it, and its
properties into the mod's `game-info.yaml`; a mod a level is saved into is added to the mods.

A pk3 is written whole each time: the new one goes in place of the old, which is kept beside it
as `NAME.pk3.bak`, so the last save can be undone by hand.

With unsaved changes (levels, palettes, definitions), loading the game again or closing the editor
asks whether to save them first.

## Starting a mod

*File → New mod…* makes a new pk3 in the game folder's `mods` folder and loads it:

- **A stand-alone game** needs no other game's files. It starts ready to play: a copy of the
  example stand-alone game's fonts, menu pictures, sounds, player, walls, doors and a level,
  MAP01, with Wolfenstein 3D's palette. Its **game id** is what `--game` calls it.
- **A mod of** one of PFWolf's games starts with just its `modinfo.yaml`.

## Keys

| Key | What it does |
| --- | --- |
| Ctrl+S, Ctrl+Shift+S | Save the level; save it into another mod |
| Ctrl+Z, Ctrl+Y | Undo, redo |
| Ctrl+N, Ctrl+I | New level; import a map |
| F3 | The 3D view |
| F4 | Level properties |
| F5, Shift+F5 | Play the level; play from the tile under the pointer |
| F7 | Check the level |
| F6 | Textures and sprites |
| F8 | Sounds and music |
| F9 | Palettes |
| F2 | Texts |
| F11 | Definitions |
| F12 | Scripts |
| Ctrl+Shift+D | Demos |

[Editing levels](./levels#tools) has the map's own keys.
