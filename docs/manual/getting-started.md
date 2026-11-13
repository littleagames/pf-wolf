---
title: Getting started
order: 1
---

# Getting started

PFWolf is a source port of Wolfenstein 3D built so you can make a game, or change one, with data
files instead of changing the engine. This page covers where the game's files go, loading a mod
and how a mod is laid out.

## Where the files go

PFWolf runs on Windows, Linux and macOS. Unzip (or untar) it anywhere: nothing needs installing.
It plays the original games' data files (`VSWAP.WL6`, `GAMEMAPS.WL6` and the rest, from Steam,
GOG or the shareware), which aren't included: copy them beside the game, or into your PFWolf data
folder (on a Mac, always the data folder). Upper or lower case names both work.

| What | Windows | Linux | macOS |
| --- | --- | --- | --- |
| The game's data files, mods, `crash.log` | beside `PFWolf.exe`, else `%LOCALAPPDATA%\PFWolf` | beside `PFWolf`, else `~/.local/share/PFWolf` | `~/Library/Application Support/PFWolf` |
| Settings, saves, screenshots, demos | `%APPDATA%\PFWolf\<game>` | `~/.config/PFWolf/<game>` | `~/Library/Application Support/PFWolf/<game>` |

On Linux there's a tarball or an AppImage. In the tarball's folder, `install-desktop-entries.sh`
adds PFWolf and the editor to the desktop's application menu. The AppImage is one file: make it
executable and run it for the game, or with `--editor` for the editor; its game files are inside
it, so the data files and mods go in `~/.local/share/PFWolf`. On a Mac, PFWolf.app and PFWolf Editor.app aren't notarized yet, so
the first time macOS asks before opening them: right-click the app and choose **Open** (or, on
macOS 15 and later, **System Settings → Privacy & Security → Open Anyway**). In the editor, pick
PFWolf.app as the game folder.

## Playing a mod

A mod is a folder, a `.zip` or a `.pk3` with the game's data files in it. There are three ways to load one:

- **From the menu:** put the mod in the `mods` folder next to the game (or in your PFWolf data
  folder's `mods`), switch it on in **Options → Mods** (the game restarts), and start a new game.
- **From the command line:** name it with `--file`:

  ```
  PFWolf.exe --file moody-lights
  ```

  (`./PFWolf --file moody-lights` on Linux.)
- **Drag and drop:** drop the mod onto `PFWolf.exe`.

## What's in a mod

A mod uses the same folders as the game's own `pfwolf.pk3`. Its files are laid over the game's,
key by key: give only what you change, and what you leave out keeps the game's value. A key tagged
`!remove` takes one out. [How files combine](./combining-files) has the rest.

| Path | What it holds |
| --- | --- |
| `modinfo.yaml` | About the mod: its name, version, author and the games it's made for |
| `game-info.yaml` | Episodes, levels, shading and other game-wide settings |
| `actordefs/` | Actor classes: enemies, items, decorations |
| `mapdefs/` | What each wall, door and thing number on a map means |
| `animdefs/` | Animated walls and flats |
| `textures/`, `flats/`, `sprites/`, `graphics/` | Pictures, as PNG files |
| `sounds/` | Sounds, and `sound-seq.yaml` naming them |
| `maps/` | Levels |

## Learn from the examples

The PFWolf repository has example mods in [`examples/mods`](../../examples/mods), each
explained in its [README](../../examples/README.md). To make one of your own, start with the
[Your first mod](./tutorials/first-mod) tutorial: a few YAML files, no maps or pictures.
