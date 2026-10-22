---
title: Your first switch
order: 4
---

# Your first switch

In this tutorial you turn a wall on Wolfenstein 3D's Floor 1 into a light switch. Use it, and the
darkness from [Your first mod](./first-mod) lifts: the level is at full light, and the skeleton in
the wall's cell is gone. Use it again, and it's dark, with the skeleton back. It's one mapdefs
file, with no map editing.

Carry on in the `my-first-mod` mod from that tutorial: the switch turns its shading off and on.

## 1. Find the wall

Start a new game. Ahead of you is a door: go through it into the corridor, turn right, and walk
down the middle of the corridor into the room at its end. Its far wall is a row of prison cells,
and the one straight ahead has a skeleton in it, the only one on Floor 1. (With cheats on,
`tp 34 62 270` puts you in front of it.)

Every wall on a map is a number on its first plane, and mapdefs' `walls` say what each number
looks like. The skeleton's cell is wall 7 in pfwolf.pk3's
[`mapdefs/wolf3d/walls.yaml`](../../../pfwolf-pk3/mapdefs/wolf3d/walls.yaml):

```yaml
    '7':
        North: BSTCELB1
        South: BSTCELB1
        East: BSTCELB2
        West: BSTCELB2
```

a texture for each side the wall is seen from (the east and west ones darker, as all of
Wolfenstein 3D's walls are).

## 2. Make it a switch

Make a folder `mapdefs` in the mod, and in it `light-switch.yaml`:

```yaml
walls:
  '7':
    switch:
      to: 70
      sound: switches/elevbutn
      actions:
        - A_SetShading(off)
  '70':
    North: BSTCELA1
    South: BSTCELA1
    East: BSTCELA2
    West: BSTCELA2
    switch:
      to: 7
      sound: switches/elevbutn
      actions:
        - A_SetShading(on)
```

A `switch` makes a wall one the player can use, as they'd open a door. Wall 7 keeps its textures:
this file is laid over the game's, key by key, so it only adds the switch.

- `to` is the wall it turns into once it's used: 70, a new wall, which is the same cell, empty
  (`BSTCELA`, the cell the other cells on Floor 1 are). Wall 70 has a switch of its own, back to
  7, so using it again turns it back: a switch either way round makes a toggle.
- `sound` plays as it's used: the elevator switch's click.
- `actions` run as it's used. [`A_SetShading`](../../reference/actions#a-setshading)`(off)` takes
  the level's shading away, so it's at full light; `(on)` puts the level's own back, here the
  darkness from your `game-info.yaml`.

Every field a switch can have is in [mapdefs](../../reference/mapdefs), and every action in
[Actions](../../reference/actions), under *Switches and triggers* and *Actors and switches alike*.

## 3. Try it

Restart the game, walk to the cell and use it: click, and the lights come on, and the skeleton is
gone. Use it again: dark, and he's back.

## Switches and tags

This switch works on the whole level. Most switches work on something on the map instead: they
open a door, move a wall, change a wall further off. They find it by its **tag**, a number on the
map's fifth plane: a switch acts on whatever shares its tile's tag. `A_OpenDoor` opens the doors
with the switch's tag, `A_SetWall(51)` turns the walls with its tag into wall 51, and so on.

Tags are set on the map itself, so they need a map editor (in PFWolf Editor, the tags plane:
[Editing levels](../editor/levels#planes)), or the scripts the example mods' levels are made with. Floor 1 has no tags, which is why this switch uses an action that needs none.

Being a wall id, this switch is every skeleton cell in the game, not only Floor 1's. A mod with a
level of its own would give its switch a new wall id, used only where it's placed.

## Where next

- The [Switch Demo](../../../examples/mods/switch-demo) has nine switch stations on tagged tiles:
  a door, lights, a wall pushed into a secret room, flags hung and taken down, light zones and
  scrolling walls.
- The [Patch Demo](../../../examples/mods/patch-demo) puts switches on pictures hung on walls
  (patches), so any wall can carry one.
