---
title: Player classes
order: 4
---

# Player classes

The player is the actordefs class `Player` (Wolf3D's is in pfwolf.pk3's
`actordefs/wolf3d/player.yaml`). The engine builds the player itself; from the class it reads the
`player.*` properties (start health, lives, items, caps, the status bar face), `tag` (the
player's name in obituaries) and `deathsound`. Its states are only what other players see. The
[actor properties reference](../reference/actor-properties) lists each property.

## Classes of your own

A class with `parent: Player` is a player class. It has every property it doesn't give from
Player:

```yaml
Commando:
  parent: Player
  properties:
    "player.maxhealth": 75
    "player.damagedealt": 1.25
    "player.maxamount":
      Clip: 150
```

game-info's `player-classes` lists the classes a new game can be played as, and how the class
menu shows them. With none or one listed there's no choosing. See the
[game-info reference](../reference/game-info).

## What other players see

In a game with others, each player is drawn where they stand, as their class's states (the
camera sees the same while spectating). The body is never placed on the map, and it turns with
the player: frames A to K have the 8 views, and L to O are seen from the front only. Nothing the
game does depends on it.

| State | When |
| --- | --- |
| `Spawn` | Standing still |
| `See` | Walking |
| `Missile` | While their weapon fires |
| `Pain`, `Pain1` | When hurt: `Pain1` on even health left, if the class has it |
| `Death` | Once dead |

A state the class hasn't got falls back to `See` while walking, else `Spawn`. A class with no
`Spawn` state isn't drawn at all. Don't give a player class a `Chase` state: that's what makes an
actor an enemy.

The weapon in hand changes how the player looks. Its `weapon.bodysprite` is drawn in place of the
body's `Spawn` sprite wherever a frame uses that sprite. Wolf3D's Player stands in `PLMP`, the
machine gun; with the knife it's `PLKN`, the pistol `PLPS`, the gatling gun `PLCG`. A weapon with
no body sprite, or a frame its sprite lacks, shows `PLMP`. Frames with another sprite (the `PLAY`
death frames) are the same whatever's held.

That swap only happens for a class whose `Spawn` sprite is Player's own. A class with states of
its own (looking like a Guard, say) keeps its sprites whatever it holds. Give such a class every
state, as one it leaves out is Player's.
