---
title: Factions
order: 8
---

# Factions

Every player and enemy is on a side, a faction, and each faction treats the others as hostile,
neutral or allies. The game's factions are in `factions.yaml`, and the
[factions reference](../reference/factions) has each field.

Without a `factions.yaml`, every enemy is on `monsters` and every player on `players`, hostile to
each other, which is how the original games play. Wolfenstein 3D's puts the player on `allies` and
the enemies on `axis`.

## Which side an actor is on

An actor's side is set as it comes into the level, from the first of these it has:

1. A mapdefs thing's `faction:` (`things: 400: { class: Guard, faction: allies }`), for one placed
   on another side than its class.
2. Its actordefs class's `faction` property, inherited through `parent:` as any property is. A
   player class can have one too.
3. `player-faction` for a player, `monster-faction` for an enemy (a `Monster`).

Anything else, such as a decoration or a pickup, is on no side.

What an actor brings into being (a projectile it throws, an alien out of its canister, Hitler out
of his armor) is on the side of the actor it came from, unless its class has a `faction` of its
own.

With cheats on, the `setfaction` console command moves the nearest enemy, or every actor of a
class, onto a faction, and `factions` lists who's on each.

## How one faction treats another

A faction's `relations:` entry for the other, else its `default:`, else the file's
`default-relation`:

- **hostile**: goes after them on sight, and they're who its shots and projectiles are for.
- **neutral**: leaves them be, unless they hurt it.
- **ally**: never goes after them, and its shots and projectiles pass them by.

A faction's own members are always its allies. Relations go one way: for two factions to be
allies, each says so of the other.

## Faction colors

A faction's `translation` is what its enemies are drawn through when nothing of their own says
otherwise, so the player's allies can be told from the axis. `class-translations` gives one by
class in its place, since each sprite wears its own colors. See [Sprite colors](./sprite-colors).
