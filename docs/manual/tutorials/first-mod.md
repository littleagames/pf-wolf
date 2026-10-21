---
title: Your first mod
order: 1
---

# Your first mod

In this tutorial you make a mod that darkens Wolfenstein 3D's levels and lets their lamps light
the rooms, each in its own color. It's three small YAML files: no maps, no pictures, no code.
It's a smaller version of the [Moody Lights](../../../examples/mods/moody-lights) example mod.

You need PFWolf with Wolfenstein 3D's data files, and a text editor.

## 1. Make the mod's folder

A mod can be a plain folder while you work on it. Make one called `my-first-mod` in the `mods`
folder next to `PFWolf.exe`:

```
PFWolf/
  PFWolf.exe
  pfwolf.pk3
  mods/
    my-first-mod/
```

## 2. Say what it is

In that folder, make `modinfo.yaml`:

```yaml
name: My First Mod
version: "1.0"
author: Your name
description: Darker levels, lit by their own lamps.
game-packs: [wolf3d]
```

`game-packs` says which games it's for, so it's only loaded in Wolfenstein 3D.
[modinfo.yaml](../../reference/modinfo) has every field.

Start the game with it:

```
PFWolf.exe --file my-first-mod
```

`--file` finds a mod by name in the `mods` folder (or give it a full path). You can also switch
it on in **Options → Mods**. Open the console with the <kbd>~</kbd> key (left of <kbd>1</kbd>) and type `mods`. It
lists **My First Mod 1.0 by Your name**, and would say if anything were wrong with it.

## 3. Darken the levels

Make `game-info.yaml` in the mod's folder:

```yaml
default-map:
  shading:
    light: 64
    fade-start: 3
    fade-end: 18
    max-fade: 90
```

A mod's files are laid over the game's own, key by key. This one adds shading to `default-map`,
the settings every level starts from, and leaves the rest of the game's `game-info.yaml` as it
was. `light` drops the level's light from 255 (full) to 64, and the view fades toward black from
3 tiles away to 18, up to 90%. Every field is in [shading](../../reference/game-info#default-map-shading).

Start a new game. The levels are dark, and the floor lamps and chandeliers now light the rooms
around them. They gave off light all along: the game's own definitions give them some, but
light only shows where a level is darker than full light.

## 4. Light the ceiling lights

The ceiling lights give off none yet. Make a folder `actordefs` in the mod, and in it
`lamps.yaml`:

```yaml
CeilingLight:
  properties:
    "light.intensity": 150
    "light.radius": 3
    "light.color": "#FFF0D0"
```

`CeilingLight` is the game's own actor class, so this adds to it rather than making a new one.
Its sprite and states stay as they were; it just gets three new properties: how bright its
light is (0-255), how far it reaches in tiles, and a warm white tint. Classes built on it get
them too, so `CeilingLight2`, the other ceiling light, is lit as well.

## 5. Make the chandeliers flicker

Add this to `lamps.yaml`:

```yaml
Chandelier:
  properties:
    "light.intensity": 180
    "light.radius": 3.5
    "light.color": "#FFC070"
    "light.effect": flicker
    "light.low": 150
    "light.tics": 10
```

The chandeliers already had a light. Naming a property again replaces its value, so their light
now reaches a little further, turns amber, and flickers between 150 and 180 like candles. The
`light.*` properties are all in [Actor properties](../../reference/actor-properties); `pulse`
and `strobe` are the other effects.

## 6. Try values without restarting

The game reads the mod as it starts, so each change to a file needs a restart. To find the
values you want first, try them in the console, with cheats on (start with `--cheats`, or
press <kbd>Shift</kbd>+<kbd>Alt</kbd>+<kbd>Backspace</kbd>):

- [`light 40`](../../reference/console#light) sets the level's light, until you leave the level.
- [`fog #000000 2 12 100`](../../reference/console#fog) tries a fade.
- [`lights`](../../reference/console#lights) lists every actor giving off light, and how much.
  Use it to check a property took: one with a misspelt name is ignored, and the lamp just stays
  as it was.

## 7. Share it

To hand your mod to someone, zip the files inside the folder (not the folder itself), and
rename the zip to `my-first-mod.pk3`. A `.pk3` loads just like the folder: put it in `mods`, or
give it to `--file`, or drop it onto `PFWolf.exe`.

## Where next

- [Moody Lights](../../../examples/mods/moody-lights) does the same for every light decoration,
  with a pulsing red warning light and a stuttering bulb.
- [Getting started](../getting-started) lists the folders a mod can have.
- The [definitions reference](../../reference/) covers every file a mod can carry.
