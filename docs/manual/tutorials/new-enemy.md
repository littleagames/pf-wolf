---
title: A new enemy
order: 2
---

# A new enemy

In this tutorial you make an elite guard: tougher and faster than Wolfenstein 3D's guard, in a
grey uniform, and on the levels in place of some of the guards on the harder skills. You build
it on the guard the game already has, so it needs no pictures and no map editing.

It follows on from [Your first mod](./first-mod), which covers making a mod's folder and
`modinfo.yaml`.

## 1. Make the mod

Make a folder `elite-guards` in the `mods` folder, with this `modinfo.yaml`:

```yaml
name: Elite Guards
version: "1.0"
author: Your name
description: Tougher, faster guards in grey on the harder skills.
game-packs: [wolf3d]
```

## 2. Make the class

Every enemy, item and decoration is an actor class, defined in an `actordefs` file. Make a folder
`actordefs` in the mod, and in it `elite-guard.yaml`:

```yaml
EliteGuard:
  parent: Guard
  properties:
    health: 60
    speed: 768
    points: 250
```

`parent: Guard` builds it on the game's guard: it has his sprites, his states (standing,
patrolling, chasing, shooting, dying), his sounds and the ammo he drops. Then its properties
change what's different. The guard has 25 health, moves at 512 and is worth 100 points; the
elite guard has 60, moves half as fast again, and is worth 250. Every property an actor can have
is in [Actor properties](../../reference/actor-properties).

## 3. Try it out

You can meet a class before it's on any level. Start the game with the mod and cheats on:

```
PFWolf.exe --file elite-guards --cheats
```

Start a new game, open the console and type:

```
god 2
summon EliteGuard
```

[`god 2`](../../reference/console#god) keeps you alive without the screen flashing red, since the
guard you summon starts shooting at once. [`summon`](../../reference/console#summon) puts one on
the tile you face, facing you. It looks just like a guard, but takes more shots to bring down.
[`actors Elite`](../../reference/console#actors) lists the elite guards on the level, with their
health.

## 4. Give it a uniform of its own

To tell the elite guards apart, draw them in other colors. A color translation recolors a
sprite's colors as it's drawn, so the guard's sprites can serve for both. Make `translations.yaml`
in the mod:

```yaml
elite-guard: ["207:223=23:31", "198:206=19:22 below 10"]
```

and give the class the translation, in its properties:

```yaml
    translation: elite-guard
```

Each range recolors some of the palette's colors onto others. The guard's tan uniform is the
palette's skin-to-brown colors, 198 to 223, which are also his face's. So the first range turns
the uniform's darker tones, 207 to 223, into the greys 23 to 31, and the second turns the lighter
tones, 198 to 206, grey too, but only from 10 rows below the top of the sprite, below his head.
His face keeps its color.

Restart the game and summon another: it's in grey. [translations.yaml](../../reference/translations)
explains the ranges, and the comments at the top of pfwolf.pk3's
[`gamepacks/wolf3d/translations.yaml`](../../../pfwolf-pk3/gamepacks/wolf3d/translations.yaml) list
which colors are which in Wolfenstein 3D's palette.

## 5. Put it on the levels

A level doesn't name the actors on it, only numbers: each tile on the map's object plane holds a
number, and the `things` in mapdefs say which class each number places. The guard has 24 numbers,
for the way he faces, whether he stands or patrols, and the easiest skill he's on. Numbers 144 to
151 are the guards a level adds from the third skill, *Bring 'em on!*, up.

Make a folder `mapdefs` in the mod, and in it `elite-guards.yaml`:

```yaml
things:
  144: { class: EliteGuard }
  145: { class: EliteGuard }
  146: { class: EliteGuard }
  147: { class: EliteGuard }
  148: { class: EliteGuard }
  149: { class: EliteGuard }
  150: { class: EliteGuard }
  151: { class: EliteGuard }
```

Like any mod file, this is laid over the game's own key by key. Each number only gets a new
`class`, and keeps the way it faces, whether it patrols, and its skill.

Start a new game on *Bring 'em on!* and type `actors Guard`: the elite guards are among them.
Start one on *Can I play, Daddy?* and there are none.

## Where next

- The [Faction Demo](../../../examples/mods/faction-demo) puts guards on your side, and the
  [Medic Demo](../../../examples/mods/medic-demo) builds a medic on the guard that brings the fallen
  back to life.
- [Actions](../../reference/actions) lists what an actor's states can do, for a class with states
  of its own.
- [mapdefs](../../reference/mapdefs) covers the other things a map number can be: walls, doors,
  triggers.
