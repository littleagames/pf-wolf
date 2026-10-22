---
title: A new weapon
order: 5
---

# A new weapon

In this tutorial you make a burst pistol. It fires three quick shots at a press of fire, each a
little weaker than the pistol's, and keeps firing bursts while fire is held. It shares the
pistol's slot, the player starts with it, and it's put away and brought up as weapons are
changed. You build it on the pistol the game already has, so it needs no pictures.

It follows on from [Your first mod](./first-mod), which covers making a mod's folder and
`modinfo.yaml`.

## 1. Make the mod

Make a folder `burst-pistol` in the `mods` folder, with this `modinfo.yaml`:

```yaml
name: Burst Pistol
version: "1.0"
author: Your name
description: A pistol that fires three shots at a press.
game-packs: [wolf3d]
```

## 2. Make the class

A weapon is an actor class, like an enemy. Make a folder `actordefs` in the mod, and in it
`burst-pistol.yaml`:

```yaml
BurstPistol:
  parent: Pistol
  properties:
    "inventory.type": BurstPistol
    "weapon.selectionorder": 1800
```

`parent: Pistol` builds it on Wolfenstein 3D's pistol, which gives it a lot:

- the pistol's pictures and states, so it's drawn and fires as the pistol does, for now;
- `weapon.slot` 2, so the 2 key picks it;
- the ammo it shoots, one clip round a shot;
- its sound, its status bar picture, and the 6 rounds it comes with.

Two properties are its own. `inventory.type` is the item a class is held as. The pistol's is
`Pistol`, and without its own the burst pistol would inherit that and be held as a second pistol.
That's what Wolfenstein 3D's own variants want (the `BluePistol` counts as the pistol), but not
this.

`weapon.selectionorder` says how good a weapon is: lower is better. The pistol's is 1900 and the
machine gun's 1300, so 1800 puts the burst pistol between them. A weapon better than any the
player holds goes into their hand as it's picked up, and when the machine gun runs dry it's the
next one tried. Every `weapon.*` property is in [Actor properties](../../reference/actor-properties).

## 3. Try it out

Start the game with the mod and cheats on:

```
PFWolf.exe --file burst-pistol --cheats
```

Start a new game, open the console and type `give BurstPistol`. It's better than the pistol, so
it goes straight into your hand. It looks and fires just like the pistol, because so far that's
all it is.

Press 2: you have the pistol back. Press 2 again for the burst pistol. Two weapons in one slot
take turns, the better one first.

## 4. Make it fire in bursts

The weapon in hand plays its states: `Ready` while it waits, `Fire` when fire is pressed. The
burst pistol has the pistol's, so to fire differently it needs a `Fire` state of its own. Add
this under the class, after its properties:

```yaml
  states:
    Fire:
      - { sprite: PISG, frames: [B], tics-per-frame: 3 }
      - { sprite: PISG, frames: [C], tics-per-frame: 3, modifiers: [bright], action: "A_GunAttack(6, 9, 9)" }
      - { sprite: PISG, frames: [D], tics-per-frame: 3 }
      - { sprite: PISG, frames: [C], tics-per-frame: 3, modifiers: [bright], action: "A_GunAttack(6, 9, 9)" }
      - { sprite: PISG, frames: [D], tics-per-frame: 3 }
      - { sprite: PISG, frames: [C], tics-per-frame: 3, modifiers: [bright], action: "A_GunAttack(6, 9, 9)" }
      - { sprite: PISG, frames: [D], tics-per-frame: 6, action: A_ReFire }
      - { sprite: PISG, frames: [E], tics-per-frame: 6, next-state: Ready }
```

Each line is a frame of the pistol's pictures (`PISG`), shown for so many tics, 70 to a second.
`B` raises the gun, `C` is the muzzle flash (`bright`, so it isn't darkened by the room), and `D`
and `E` bring it back down. The pistol shows each for 6 tics. The burst pistol shows them for 3,
and goes from `D` back to `C` twice more.

A frame's `action` runs as the frame ends. [`A_GunAttack`](../../reference/actions#a-gunattack)
fires one shot, so there are three, 6 tics apart. Each takes a round, and once there are none
left the rest of the burst fires nothing. A hit deals a random 0 to 255, divided by the first
number within 2 tiles, by the second within 4, and by the third beyond. Left out they're 4, 6
and 6, the pistol's. At 6, 9 and 9, each shot of the burst does two-thirds of a pistol shot, but
there are three of them.

[`A_ReFire`](../../reference/actions#a-refire), as `D` ends, checks whether fire is still held.
If it is, and there's a round, the weapon goes back to its `Hold` state, or to `Fire` for a weapon
with no `Hold`, as here. So holding fire keeps the bursts coming, and letting go lets the attack
finish on `E` and go back to `Ready`.

Restart the game, give yourself the burst pistol and tap fire: the ammo drops by three. Hold
fire and it empties the clip a burst at a time. With no ammo left, the knife comes out.

## 5. Start with it

A new game's weapons are the player class's `player.startitem`. Wolfenstein 3D's player starts
with the knife, the pistol and 8 rounds. Add this to the same file, after the burst pistol:

```yaml
Player:
  properties:
    "player.startitem":
      BurstPistol: 1
```

A mod's actordefs are laid over the game's key by key, so this adds the burst pistol to the
player's start items, and keeps the knife, the pistol and the ammo. The best weapon a new game
gives is the one in hand: start a new game, and you're holding the burst pistol.

## 6. Put it away and bring it up

In Wolfenstein 3D a weapon change is instant. A weapon with a `Deselect` state is put away first
instead, and one with a `Select` state is brought up. Add these under `states:`, after `Fire`:

```yaml
    Deselect:
      - { sprite: PISG, frames: [A], tics-per-frame: 1, action: A_Lower(8), next-state: Deselect }
    Select:
      - { sprite: PISG, frames: [A], tics-per-frame: 1, action: A_Raise(8), next-state: Select }
```

Each is the gun at rest (`A`), on a frame of 1 tic that loops onto itself, so its action runs
every tic. [`A_Lower`](../../reference/actions#a-lower) takes the weapon 8 further down each tic,
out of 96 for the whole way, so it's out of view in 12 tics. Then the next weapon comes in.
[`A_Raise`](../../reference/actions#a-raise) brings it back up the same way, and on to `Ready`
once it's all the way up. While a weapon goes down or comes up, fire does nothing.

Restart, start a new game, and press 2. The burst pistol goes down, and the pistol appears at once:
each weapon's own states decide its half of the change, and the pistol has none. Press 2 again:
the pistol vanishes and the burst pistol comes up.

## Where next

- [Weapon states](../weapons) covers every state a weapon can have, with reloading and magazines,
  zooming in, and what waits while a weapon is changed.
- The [Holster Demo](../../../examples/mods/holster-demo) gives Wolfenstein 3D's own weapons
  `Deselect` and `Select` states.
- To put the burst pistol on the levels as a pickup, it needs a `Spawn` state with a picture of
  it lying on the floor, and a map number in mapdefs, as in [A new enemy](./new-enemy).
- [Actions](../../reference/actions) lists what a weapon's states can do: missiles, punches, a
  shot at whatever's in the sights.
