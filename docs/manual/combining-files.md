---
title: How files combine
order: 2
---

# How files combine

A game in PFWolf is put together from several layers of YAML files: the games it's built on, its
own files, then each mod in turn. This page covers how the layers combine, and how to take
something out or start a table fresh. What each file's keys mean is in the
[reference](../reference/).

## Games built on other games

Each game in `gamepacks/gamepack-info.yaml` can name a `base-pack`. Spear of Destiny names
`wolf3d`, and the Spear demo names `spear`. A game with a base pack starts from that pack's
files (its `actordefs/wolf3d/`, `mapdefs/wolf3d/`, `gamepacks/wolf3d/` and the rest), and from
that pack's base pack before them. Its own files are laid over them, and the mods over all of that.

A game with no `file-pack` plays the data files of the nearest base pack that has one. A game
built on `standalone` has none along the way, so everything it has comes from its pk3s.

## File over file

Within the game's own files (a pack's over its base pack's, or several files of one kind such
as `actordefs/wolf3d/guards.yaml` and `bosses.yaml`), each kind of file combines its own way:

| Files | How a later one combines with what's there |
| --- | --- |
| `game-info`, `factions` | Key by key, all the way down: a pack gives only what it changes |
| `actordefs` | Class by class: a class replaces one of the same name whole, unless it says `extend: true` (below) |
| `mapdefs`, `intermission`, `presenter`, `elevator`, a pack's own `sound-seq` | Entry by entry in each section (`things:`, `walls:`, `sound-info:`...) |
| `language`, `colors`, `fonts`, `hud-messages`, `statusbar`, `movies`, `translations`, `raw-data-map`, `animdefs`, `graphicdefs` | Entry by entry (a string, a color, a font, an animation...) |
| `alias`, `sounds/sound-seq.yaml` | The later file replaces the earlier whole |
| `menudefs` | A pack's own menu (`menudefs/{pack}/NAME.yaml`) is used in place of the shared one of that name |

A class with `extend: true` changes the class of the same name instead of replacing it. Its
properties and states replace those of the same name, its flags are added to the class's (one
written `~FLAG` is taken away), and its `parent` and `radius`, when given, replace the class's.

## A mod's files

A mod's files merge key by key, all the way down, whatever kind they are. That includes a class
without `extend`. Give only what you change; a key you leave out keeps what's there.

A mod's menu file merges into the menu of the same name: its keys replace the menu's, apart
from `menu-items` and `components`, which merge entry by entry. An entry with the `id` (for a
control row, the `control`) of one the menu has changes that one, key by key. `remove: true`
takes it out. An entry the menu hasn't got is added at the end, or next to another entry with
`before:` or `after:` and that entry's id.

A mod keeps its files at its root, for whichever game is running. `actordefs/`, `mapdefs/`,
`animdefs/`, `graphicdefs/`, `language/` and `menudefs/` take no subfolders. A mod made for
several games can also keep one game's own files in folders named after it: `gamepacks/{pack}/`
(`game-info.yaml` and the other root files, and `language/`), `maps/{pack}/`, `sprites/{pack}/`
and `demos/{pack}/`. Those load only for that game and the games built on it, after the mod's root
files. The multiplayer pack's Wolf3D arenas are kept this way.

## Taking things out and starting fresh

Lists are always replaced whole. Anywhere a merge reaches, a value can be tagged:

- `key: !remove` takes the key out: a class, a property, a state, a map, a setting.
- `key: !replace`, followed by its own value, replaces what's there whole rather than merging
  into it. Spear's game-info gives its own `maps:` this way.

A key that's left out keeps what's there, so to put a setting back to its default, `!remove` it.

To start a table fresh, `!replace` it with your own entries, or `!remove` it to leave it empty. A
mod with its own levels in place of the game's has this in its `game-info.yaml`:

```yaml
skills: !replace
  NORMAL: { name: "Normal" }
demo-skill: NORMAL          # the game's own names one of the skills just taken out
episodes: !replace
  EP01: { name: "My Episode", start-map: MAP01 }
clusters: !remove
maps: !replace
  MAP01: { name: "My First Level", floor-number: 1 }
```

The same works in any table a merge reaches, such as mapdefs' `things:` or a class's `states:`.
Only a file's top level (actordefs' classes, language's strings) has nothing to tag: take those
out one by one.

## A game of your own

A mod can add games of its own with a `gamepack-info.yaml` at its root, written as pfwolf.pk3's
is. It can't change the games already there. A standalone game, which needs no game's data
files, is built on `standalone`:

```yaml
my-game:
  title: My Game
  base-pack: standalone
```

`--game my-game --file my-game.pk3` runs it, and so does dropping the mod on `PFWolf.exe`. Its
files load in `my-game`'s folders, so it starts from nothing but its own files and the shared
ones directly in `actordefs/`, `animdefs/`, `graphicdefs/`, `language/`, `menudefs/` and
`sounds/`. The [standalone demo](../../examples/mods/standalone-demo) is one.
