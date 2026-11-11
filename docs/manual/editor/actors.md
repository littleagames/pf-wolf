---
title: Actors
order: 9
---

# Actors

**Resources → Actors…** (Ctrl+Shift+A) lists every actor class the game has: the enemies, items,
weapons, projectiles, decorations and player classes in `actordefs`, with your mods' laid over
them. Each one is put together as the game puts it together, with what its parents give it, and
played as the game plays it. What each actordefs field does is in the
[actor properties](../../reference/actor-properties) and [actor flags](../../reference/actor-flags)
references.

The list can be narrowed by kind (*Enemies*, *Items*, *Weapons*...; *Unseen* has the markers and
base classes, and *With problems* the classes with something wrong), and by name, by a class it's
built on, or by a sprite, think or action its states use: *Monster*, *GARD*, *T_Chase*. A ⚠
marks a class with a problem.

Picking one shows what kind of thing it is, the class it's built on and the engine's own class it
ends up as (Monster, Weapon...), and what it's built on from the root down.

## Playing it

The **preview** plays one of its states, picked above it, as the game plays it: each frame for its
tics, at the game's 70 tics a second, going on to the next frame, looping, or holding on a frame
that lasts for good. An enemy that stands still for good starts on its Chase state, a weapon on
Ready, anything else on Spawn.

- **Pause** or **Play** (Space), **Next frame** (Right arrow), **Restart** (Home) the state, and
  play it at 1×, ½× or ¼×.
- **Follow next-state**: as the game does, a state's last frame can go on into another state (a
  guard's Attack into Chase); the preview goes with it, picking that state. Off, the state picked
  starts over instead.
- **View**: a sprite that turns has eight views, 1 its front, 3 its left side, 5 its back and 7
  its right side; a sprite with one view looks the same from every side.
- Under it: the tic, how many of the frame's tics are left, the think it runs every tic and the
  action it runs as it ends (the preview names them; it doesn't run them), and the actions that
  just ran.

The preview doesn't run thinks or actions, so it can't know where an action that may jump will
send the actor (`A_Jump`, `A_JumpIf…`, `A_ReFire`): it waits on that frame, saying so. **Go on**
goes to the frame after it, as if it didn't jump; or pick a state.

The **frames of the state** are shown under it, each as wide as its tics (∞ for one that lasts for
good, ✦ for one with an action), and where the state goes after its last frame. The frame playing
is marked; clicking one stops the preview on it.

Under that are the class's **problems**, the things the game would warn about or wouldn't do:
a parent that isn't there, a next-state it has no state for, an action or think nothing has (the
engine's, or a script's `game.defineaction`), one for another kind of actor (T_Chase on a
decoration), a property or flag only another kind reads, and a frame with no sprite on a class a
level places. A frame with no sprite on a class nothing places is only noted: the shared classes
have frames other games draw.

## The tabs

- **Properties**: its flags and properties, with the class each comes from when it's a parent's
  (the mouse over one says what it does);
- **Used by**: what places or names it: the mapdefs things that are it, the levels with it on
  them and how many, the classes built on it, other classes' properties and actions that name it
  (a drop, a projectile), game-info, factions, scripts that spawn it, and what the engine itself
  makes by name. *Go to* (or a double-click) shows another class here, opens the Definitions
  window at the entry, the script browser at the line, or the level with the first one picked;
- **Defined in**: the actordefs files that have it, and its lines in the last of them.

*Show sprite* shows the frame's sprite in the [textures and sprites](./art) window, *Open in
Definitions* opens the class in the [Definitions window](./definitions)'s Actors tab, where its
fields can be changed, and *Copy name* copies its name.
