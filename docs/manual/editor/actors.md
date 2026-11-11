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

- **Fields**: its own parent, radius, flags and properties, to change (the mouse over one says
  what it does, and a sound or class field has a list to pick from). Only the properties it sets
  are listed; *Add property* has the others its kind of actor reads (a Blake Stone enemy's
  aren't offered for a Wolf3D guard), and puts the one picked in the list, empty, to fill in.
  Under them, *From its parents*: every flag it has, and the properties its parents give it, each
  with the class it comes from;
- **Used by**: what places or names it: the mapdefs things that are it, the levels with it on
  them and how many, the classes built on it, other classes' properties and actions that name it
  (a drop, a projectile), game-info, factions, scripts that spawn it, and what the engine itself
  makes by name. *Go to* (or a double-click) shows another class here, opens the Definitions
  window at the entry, the script browser at the line, or the level with the first one picked;
- **Defined in**: the actordefs files that have it, and its lines in the last of them.

*More* has *Show sprite*, the frame's sprite in the [textures and sprites](./art) window, *Open in
the Definitions window*, the class in the [Definitions window](./definitions)'s Actors tab (its
states as fields, its YAML), and *Copy name*.

## Changing a class

Every change to a field is played and checked as it's made: the preview, the problems, the
numbers and the timeline are the class as changed. **Save** (Ctrl+S) writes just what's changed
into a mod's `actordefs`: the mod that has its own of the class already, else one you pick
(pfwolf.pk3 is read-only). A mod's class is laid over the game's key by key, so the rest of the
class stays the game's, and the game's later fixes to it still come through. The game is loaded
again with it. **Revert** goes back to the class as the files have it; **Undo** and **Redo**
(Ctrl+Z, Ctrl+Y, outside a field) step through the changes. Changes are kept while you look at
other classes, and loading the game again or closing the editor asks whether to save them first.
*Take out the mod's change…* (under *More*, and in the list's right-click menu) takes the last
mod's say in the class out of its actordefs.

**New actor** (top right) makes a class that's only in the window until it's saved:

- **Built on this one…**: `parent:` the class picked. It has everything that one has, and
  whatever that one's changed to later, and changes only what's set on it;
- **A copy of this one…**: written as the class picked is written (its own fields, the same
  parent), so changing the picked one later leaves it be.

It's named as you ask, and given mapdefs things to place it with on the map, unless you leave the
number empty: one, or for an enemy eight in a row, facing east, north, west and south, standing
and then patrolling. They're saved with it, into the mod's mapdefs, and the map's palette has
them once the game is loaded again.
