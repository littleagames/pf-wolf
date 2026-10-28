---
title: Your first script
order: 3
---

# Your first script

In this tutorial you give the elite guards from [A new enemy](./new-enemy) a voice: as a level
starts, a message says how many are on the floor, and as each one falls, how many are left. You
write it in Lua, the scripting language PFWolf's mods can carry, and the elite guard's death runs
an action you wrote yourself.

Carry on in the `elite-guards` mod from that tutorial.

## 1. Turn messages on

Messages over the view are off in Wolfenstein 3D unless the player turns them on, as in the
original game. Make `game-info.yaml` in the mod:

```yaml
setting-defaults:
  messages: true
```

That makes them on by default while your mod is loaded. A player who has turned them off in
**Options** keeps them off.

## 2. Write the script

A mod's scripts are in its `scripts` folder, and run as the game starts. Make `scripts` in the
mod, and in it `elite-guards.lua`:

```lua
-- How many elite guards on the level are still standing
local function standing()
  local count = 0
  for _, guard in ipairs(game.actors("EliteGuard")) do
    if guard.state ~= "Death" then
      count = count + 1
    end
  end
  return count
end

-- As each level starts afresh, say how many there are
game.onlevel(function(reason, map)
  if reason == "new" and standing() > 0 then
    game.message(standing() .. " elite guards on this floor")
  end
end)
```

- [`game.actors("EliteGuard")`](../../reference/scripting#game-actors) is a list of every elite
  guard on the level, dead ones too, so `standing` counts those not on their `Death` state:
  [`state`](../../reference/scripting#actor-state) is the state an actor is on.
- [`game.onlevel`](../../reference/scripting#game-onlevel) gives the game a function to call as
  each level starts. `reason` is `"new"` when the level is entered afresh, rather than a saved
  game being loaded or the level restarted after a death.
- [`game.message`](../../reference/scripting#game-message) shows a message over the view.

Start a new game on *Bring 'em on!*. As Floor 1 starts, the message says how many elite guards
there are. The console's [`scripts`](../../reference/console#scripts) command lists the scripts
that loaded, and says what's wrong with one that didn't.

## 3. Write an action

Now the part each elite guard runs as it falls. Add this to the script:

```lua
-- An action the elite guard's Death state runs
function A_EliteDown(self)
  local left = standing()
  if left == 0 then
    game.message("The last elite guard is down")
  else
    game.message("Elite guard down: " .. left .. " left")
  end
end
```

A global function whose name starts with `A_` becomes an action, which an actor's states can run
like one of the game's own [actions](../../reference/actions). `self` is the actor running it.
`scripts` lists it under **Actions**.

## 4. Run it as the guard dies

An actor's states are what it does: standing, chasing, shooting, dying. Each is a list of frames,
and a frame can run an action. The elite guard has the guard's states; to make its death run
`A_EliteDown`, give it a `Death` state of its own. Add `states` to the class in
`actordefs/elite-guard.yaml`:

```yaml
EliteGuard:
  parent: Guard
  properties:
    health: 60
    speed: 768
    points: 250
    translation: elite-guard
  states:
    Death:
      - sprite: GARD
        frames: [K]
        tics-per-frame: 15
        action: A_DeathScream
      - sprite: GARD
        frames: [L]
        tics-per-frame: 15
        action: A_EliteDown
      - sprite: GARD
        frames: [M]
        tics-per-frame: 15
      - sprite: GARD
        frames: [N]
        tics-per-frame: -1
```

A state of the child's replaces the parent's whole state, so this is the guard's own `Death`,
copied from pfwolf.pk3's
[`actordefs/wolf3d/guards.yaml`](../../../pfwolf-pk3/actordefs/wolf3d/guards.yaml), with one
action added: four frames of the `GARD` sprite, 15 tics each (70 tics make a second),
the last held for good (`-1`). The first frame's `A_DeathScream` is his cry; the second's is
yours.

Restart the game, and shoot an elite guard: *Elite guard down: 6 left*.

## 5. Or leave the guard alone

The game also calls hooks as things happen anywhere on the level, so the same message can come
without touching the elite guard's states at all.
[`game.onkill`](../../reference/scripting#game-onkill) calls its function with every monster
killed:

```lua
game.onkill(function(victim, killer)
  if victim.name == "EliteGuard" then
    A_EliteDown(victim)
  end
end)
```

Use this or step 4's state, not both, or the message comes twice. A hook is handy for a class
you'd rather not copy the states of, or for something about every kill.
The other hooks work the same way: `game.ondamage`, `game.onpickup`, `game.onsecret`,
`game.onuse`, `game.onsight`, `game.onspawn`, `game.onplayerdeath` and more; the
[reference](../../reference/scripting) says when each is called.

## Trying things out

With cheats on, the console's [`lua`](../../reference/console#lua) command runs Lua in the
scripts' own state, so you can try a line before it goes in the script:

```
lua print(#game.actors('EliteGuard'))
```

Strings take 'single quotes' there. Scripts run as the game starts, so a change to one needs a
restart; one that fails to load is left out, with the reason on the console and from `scripts`.

A script must leave the game the same on every machine, so that demos and games with others play
out alike: there's no `math.random` (`game.random` is the game's own), and nothing outside the game
(files, the clock). Lua's variables aren't kept in a saved game: keep anything a saved game needs
on the actors, in their `temp1` to `temp3`.

## Where next

- [Lua scripting](../../reference/scripting) lists everything a script can use.
- The [Fireball Demo](../../../examples/mods/fireball-demo) fires fireballs in rings, spirals and
  sweeps from a script, keeping each pattern's progress in `temp1` to `temp3`.
