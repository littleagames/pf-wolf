---
title: Built-in classes
order: 4
---

# Built-in classes

Every game starts from the actor classes in pfwolf.pk3's `actordefs/native.yaml`. Each one stands
for something the engine does in code, so a class of your own does that by having it as a
`parent` (or a parent's parent). Their properties are in the
[actor properties reference](../reference/actor-properties).

## Items

| Class | What it is |
| --- | --- |
| `Inventory` | Anything the player picks up. Every item class below descends from it. |
| `CustomInventory` | An item with nothing built in: what it does is its properties (`inventory.use`, say). |
| `Ammo` | Ammunition, kept from level to level. |
| `Health` | Heals as it's picked up, rather than being held. |
| `Key` | Opens the doors and switches that name it as their lock, and is lost at a level change. |
| `ScoreItem` | Treasure: points, counted in the level's treasure ratio. |
| `Weapon` | A weapon, with its `weapon.*` properties. |
| `WeaponGiver` | Hands out another weapon (`weapongiver.weapon`). |
| `BasicArmor` | Armor, put on as it's picked up rather than held. |
| `BackpackItem` | A backpack. |
| `Powerup` | A timed power-up. |

Any item can be kept from some player classes: `inventory.restrictedto` lists the only classes
that pick it up, and `inventory.forbiddento` the ones that don't.

**Armor** works as Doom's does. The player has armor points, and each hit takes
`armor.savepercent` of its damage off them instead of health until they run out. An armor is only
picked up when the player has fewer points than its `armor.saveamount`, and it replaces their
armor outright, points and percentage. With `armor.maxsaveamount` it's a bonus instead: it adds
its points, up to that many, and keeps the armor the player has (or gives its own percentage when
they have none). The player class's `player.maxarmor` caps them all. None of Wolf3D's items are
armor: its `Armor` is the suit of armor decoration.

**A backpack** (End of Destiny's) is held from when it's picked up until a death or a new game.
While it's held, each item type's cap is its `ammo.backpackmaxamount` where it has one. Picking
it up gives each type its `ammo.backpackamount`; another one only gives that.

**A power-up** (End of Destiny's god mode and Knife Fury) is held as the tics it has left, and
each one picked up adds its `powerup.duration`. While it runs it can make the player unhurt, put a
weapon in their hand that kills with every blow, show its own face on the status bar, count down
on screen, keep the screen lit, and play a sound when it's over. A death ends it, and so does a
level change, as `Powerup` keeps none from level to level (`inventory.interhubamount: 0`).

## Enemies

| Class | What it is |
| --- | --- |
| `Monster` | An actor that moves, sees, fights and dies. |
| `BlakeMonster` | Blake Stone's and Planet Strike's enemies and machines: a `Monster` with their ways of chasing, shooting, running, being wounded and being talked to. |

They add no properties of their own; the class is what they bring. An enemy's thinks and actions
(`T_Chase`, `T_Shoot`, `A_FireProjectile`...) only run on an actor that has the class they need
as an ancestor, and the [actions reference](../reference/actions) says which that is. On any
other actor they're skipped, with a warning.
