---
title: Your first puzzle
order: 6
---

# Your first puzzle

In this tutorial you put a puzzle in front of the light switch from
[Your first switch](./first-switch): before the lights come on, the player has to steady a
reactor, keeping its needle in the green for three seconds while it drifts. Let it run into the
red and it shocks them. The puzzle is a Lua script; the switch only says which puzzle it wants.

Carry on in the `my-first-mod` mod from that tutorial.

## 1. Write the puzzle

A puzzle is made by a script, like an action. Make the `scripts` folder in the mod if it isn't
there, and in it `reactor.lua`:

```lua
-- Keep the reactor's needle in the green for three seconds. It drifts; left and right push it back.
game.puzzle("reactor", {
  title = "Steady the reactor",
  width = 200,
  height = 80,
  timelimit = 70 * 15,

  start = function(pz, args)
    pz.state.needle = 50      -- 0 to 100: the green is 40 to 60
    pz.state.drift = 0
    pz.state.steady = 0       -- tics it's been in the green
  end,

  tick = function(pz, input)
    local s = pz.state
    -- Now and then the drift changes, at random
    if game.random(1, 20) == 1 then s.drift = game.random(-10, 10) / 10 end
    s.needle = s.needle + s.drift * input.tics
    if input.held.left then s.needle = s.needle - 1.5 * input.tics end
    if input.held.right then s.needle = s.needle + 1.5 * input.tics end
    s.needle = math.max(0, math.min(100, s.needle))

    if s.needle < 5 or s.needle > 95 then return "fail" end
    if s.needle >= 40 and s.needle <= 60 then
      s.steady = s.steady + input.tics
      if s.steady >= 70 * 3 then return "success" end
    else
      s.steady = 0
    end
  end,

  draw = function(pz, width, height)
    local s = pz.state
    game.drawrect(10, 10, 180, 16, "#400000")                       -- the gauge, red at the ends
    game.drawrect(10 + 180 * 0.4, 10, 180 * 0.2, 16, "#006000")     -- and green in the middle
    game.drawrect(10 + 180 * s.needle / 100 - 1, 6, 3, 24, "White") -- the needle
    game.drawtext("Left and right steady it.", 10, 40)
    game.drawtext(string.format("Steady: %d of 3 seconds", math.floor(s.steady / 70)), 10, 54)
  end,
})
```

[`game.puzzle`](../../reference/scripting#game-puzzle) makes a puzzle called `reactor` from a
table of its settings and three functions:

- `title` is shown over it, `width` and `height` are its panel's size, and `timelimit` is how
  long the player has, in tics (70 make a second): running out is a fail.
- `start` runs as it opens. `pz.state` is the puzzle's own table: keep everything it needs there.
- `tick` runs as the game thinks, while it's open, with the player's controls in `input`:
  `input.held.left` is the left key (or the turn key, or a stick) held down, `input.pressed.use`
  a fresh press of use, `input.tics` how many tics this think covers. Returning `"success"` or
  `"fail"` ends it; returning nothing keeps it going.
- `draw` draws it each frame, from the panel's top left. It can only draw: it runs on the
  player's machine alone, so changing the game there would make other machines (and demos)
  come out differently.

`game.random` is the game's own random numbers, so the drift is the same on every machine and
when a demo plays back. Lua's `math.random` isn't there for that reason.

## 2. Try it

Restart the game with cheats on, open the console and type `puzzle reactor`. The panel opens
over the view in the menu's red, and the level stops while it's up. Steady the needle, or let it
run off, or press Esc to back out. The console says what's wrong if the script has a mistake;
`scripts` lists the scripts that loaded.

## 3. Put it on the switch

Give the switch in `mapdefs/light-switch.yaml` the puzzle:

```yaml
walls:
  '7':
    switch:
      to: 70
      sound: switches/elevbutn
      puzzle: reactor
      actions:
        - A_SetShading(off)
      fail:
        - A_HurtPlayer(10)
      retry: 140
      retry-message: "The reactor needs a moment."
```

Now using the cell opens the reactor first, and only a win throws the switch: its `to`, its
sound and its `actions`. A fail runs `fail` instead ([`A_HurtPlayer`](../../reference/actions#a-hurtplayer)`(10)`:
a shock), and `retry` keeps the switch shut for two seconds, with `retry-message` if it's used
meanwhile. Backing out does nothing.

A switch's other puzzle fields are in [mapdefs](../../reference/mapdefs): `puzzle-args` to give
the puzzle settings of its own, `cancel` for actions as it's backed out of, `tries` and `broken`
to break it for good after so many fails, and `skip-item` for an item that throws it with no
puzzle.

## 4. When it ends

Every puzzle's end goes to [`game.onpuzzle`](../../reference/scripting#game-onpuzzle) too, with
the tile and tag it was started from, for anything a switch can't do itself:

```lua
game.onpuzzle(function(player, name, result, x, y, tag)
  if name == "reactor" and result == "fail" then
    game.message("The reactor sparks!")
  end
end)
```

## Keeping it

An open puzzle is kept in a saved game, `pz.state` and all, and carries on where it was after
loading. So keep `pz.state` to plain data: true and false, numbers, strings and tables of them.
A function or an actor there can't be kept, and the console says which key it was.

## Where next

- The [Puzzles Demo](../../../examples/mods/puzzles-demo) has eleven puzzles to copy, from a
  keypad to BioShock's pipe flow, each one script, and a hall of terminals to try them on.
- [Lua scripting](../../reference/scripting) lists everything a puzzle can draw with and read.
