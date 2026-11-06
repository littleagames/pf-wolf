-- Lights Out: pressing a switch flips it and the ones beside it (up, down, left, right). Light
-- them all. Arrows move, use flips. The board's made by flipping a lit one at random, so it can
-- always be solved.
--
--     A_Puzzle("lightsout", "size=4", "flips=6")
--
-- This file is meant to be copied: carry it in your own mod and change it as you like.

local CELL = 22

local function flip(s, x, y)
  for _, d in ipairs({ { 0, 0 }, { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } }) do
    local fx, fy = x + d[1], y + d[2]
    if fx >= 1 and fx <= s.size and fy >= 1 and fy <= s.size then
      s.on[fy][fx] = not s.on[fy][fx]
    end
  end
end

local function all_lit(s)
  for y = 1, s.size do
    for x = 1, s.size do
      if not s.on[y][x] then return false end
    end
  end
  return true
end

game.puzzle("lightsout", {
  title = "Restore the power grid",
  verb = "rewiring",
  width = 200,
  height = 124,

  start = function(pz, args)
    local s = pz.state
    s.size = math.max(3, math.min(5, args.size or 4))
    s.on = {}
    for y = 1, s.size do
      s.on[y] = {}
      for x = 1, s.size do s.on[y][x] = true end
    end
    for _ = 1, args.flips or 6 do flip(s, game.random(1, s.size), game.random(1, s.size)) end
    if all_lit(s) then flip(s, 1, 1) end
    s.cx, s.cy = 1, 1
    s.moves = 0
  end,

  tick = function(pz, input)
    local s = pz.state
    if input.pressed.left then s.cx = math.max(1, s.cx - 1) end
    if input.pressed.right then s.cx = math.min(s.size, s.cx + 1) end
    if input.pressed.up then s.cy = math.max(1, s.cy - 1) end
    if input.pressed.down then s.cy = math.min(s.size, s.cy + 1) end
    if input.pressed.use or input.pressed.fire then
      flip(s, s.cx, s.cy)
      s.moves = s.moves + 1
      pz:sound("switches/normbutn")
      if all_lit(s) then
        pz:sound("misc/k_pkup")
        return "success"
      end
    end
    return nil
  end,

  draw = function(pz, width, height)
    local s = pz.state
    local left, top = 10, 8
    for y = 1, s.size do
      for x = 1, s.size do
        local px, py = left + (x - 1) * CELL, top + (y - 1) * CELL
        game.drawrect(px + 2, py + 2, CELL - 4, CELL - 4, s.on[y][x] and "#FFE060" or "#302810")
      end
    end
    game.drawframe(left + (s.cx - 1) * CELL, top + (s.cy - 1) * CELL, CELL, CELL, "#40C0FF")
    local tx = left + s.size * CELL + 12
    game.drawtext("Light them all.", tx, 12)
    game.drawtext(string.format("Moves: %d", s.moves), tx, 30)
    game.drawtext("Arrows move,", tx, 54)
    game.drawtext("USE flips.", tx, 66)
  end,
})
