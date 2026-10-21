---
title: Getting started
order: 1
---

# Getting started

PFWolf is a source port of Wolfenstein 3D built so you can make a game, or change one, with data
files instead of changing the engine. This page covers loading a mod and how a mod is laid out.

## Playing a mod

A mod is a folder, a `.zip` or a `.pk3` with the game's data files in it. There are three ways to load one:

- **From the menu:** put the mod in the `mods` folder next to the game, switch it on in
  **Options → Mods** (the game restarts), and start a new game.
- **From the command line:** name it with `--file`:

  ```
  PFWolf.exe --file moody-lights
  ```

- **Drag and drop:** drop the mod onto `PFWolf.exe`.

## What's in a mod

A mod uses the same folders as the game's own `pfwolf.pk3`. Its files are laid over the game's,
key by key: give only what you change, and what you leave out keeps the game's value. A key tagged
`!remove` takes one out.

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
