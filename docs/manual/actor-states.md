---
title: Actor states
order: 3
---

# Actor states

Everything that moves or animates is an actor, and an actor's `states` say what it looks like and
what it does, frame by frame: a guard standing, chasing and dying, a lamp flickering, a rocket's
smoke, the weapon in the player's hand. This page is how the engine plays them. The
[actordefs reference](../reference/actordefs) lists each field, and
[Actions](../reference/actions) what a frame can do.

## States and frames

A state is a name and a list of entries. Each entry is one or more frames of a sprite:

```yaml
Guard:
  states:
    Attack:
      - { sprite: GARD, frames: [F], tics-per-frame: 20 }
      - { sprite: GARD, frames: [G], tics-per-frame: 20, action: T_Shoot }
      - { sprite: GARD, frames: [H], tics-per-frame: 20, next-state: Chase }
```

That's Wolfenstein 3D's guard shooting (written shorter than pfwolf.pk3's `guards.yaml` has it):
he raises his gun, fires as `G` ends, and lowers it before going back to chasing.
`frames: [A, B, C]` would be three frames, one after the other, each with the entry's tics, think
and action. A frame shows the sprite with its letter and a view: `GARDA1` to `GARDA8` for an actor
seen from 8 sides, or `GARDA0` for one that looks the same from all of them.

Every actor starts on its `Spawn` state (a patrolling guard on `Path`). What sends it to its other
states depends on what it is: an enemy's `Chase` once it's after the player, `Attack` when it
shoots, `Pain` when it's hurt and `Death` when it dies; a weapon's `Fire` on a press of fire. [Built-in classes](./built-in-classes), [Weapon states](./weapons)
and [Player classes](./player-classes) say which states each kind of actor has.

## Time

The game runs at 70 tics a second, and `tics-per-frame` is how many a frame shows for, rounded to
a whole number. Two numbers mean something else:

- **0** ends the frame the moment it's entered. Its action runs and the actor goes straight on, so
  a 0-tic frame is a way to run an action between two others without being seen.
- **-1** holds the frame for good: a decoration, or the guard's corpse at the end of his
  `Death`. The actor stays on it until something else (a hit, a script) moves it on.

## Think and action

A frame can name two things to call:

- its **think** is called every tic the frame shows. `T_Chase` and `T_Path` are thinks: an enemy
  walks a little each tic.
- its **action** is called once, as the frame ends, before the next frame shows. The guard's
  `T_Shoot` fires as his `G` frame ends, after the player has seen the gun raised for 20 tics.

So an action on a 1-tic frame that loops runs every tic, and an action on a -1 frame never runs,
as the frame never ends. To do something as an actor arrives somewhere, put the action on a 0-tic
frame first.

An action can send the actor to another state (`A_Jump`, `A_ReFire`, a script's
`self:jump("Fire")`). That happens once the action is done: the actor goes to the first frame of
that state rather than the next frame of this one. A few of the enemies' own actions change state
the way id's games did instead: the actor carries on from that state's second frame, which shows
for both frames' tics. After `A_Relaunch`, the state it goes to starts that way, its first frame
never seen.

## How a state ends

After its last frame, a state goes on to its `next-state`:

```yaml
    Pain:
      - { sprite: GARD, frames: [I], tics-per-frame: 10, next-state: Chase }
```

Left out, the state loops back to its own first frame, as a flickering lamp does. Instead of
`next-state`, a state can end with an entry of its own:

- `goto: Chase` goes on to that state, as `next-state` does;
- `stop` stays on the last frame (give it -1 tics, or its action runs again each time round);
- `loop` goes back to the state's first frame, as leaving it out does.

Only the state's last entry ends it. A `next-state` on an entry before the last is never
reached, since the next entry's frames follow, and the game warns about it as it loads.

## Inherited states

A class with a `parent` has its parent's states, and a state it gives replaces the parent's of
that name whole: there's no changing one frame of it. A mod's actordefs are laid over the game's
classes the same way, state by state, so a mod can give the guard a new `Pain` and keep everything
else he does.

A state's frames are its own, sprites and all, so a class built on another and drawn with other
sprites needs its own copy of every state it shows. The [Holster Demo](../../examples/mods/holster-demo)
does that for the blue weapons, built on Wolfenstein 3D's.
