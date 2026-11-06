-- Pipe flow (as BioShock's hacking): turn the pipe pieces to lay a path from the inlet on the
-- left to the outlet on the right before the flow comes through. Arrows (or the stick) move the
-- cursor, use turns the piece under it, fire opens the valve early. A piece the flow has reached
-- can't be turned. The flow spilling out anywhere but the outlet is a fail.
--
--     A_Puzzle("pipes", "cols=7", "rows=4", "delay=560", "speed=45")
--
-- delay: tics before the valve opens; speed: tics the flow takes through a piece. This file is
-- meant to be copied: carry it in your own mod and change it as you like.

local N, E, S, W = 1, 2, 4, 8
local DX = { [N] = 0, [E] = 1, [S] = 0, [W] = -1 }
local DY = { [N] = -1, [E] = 0, [S] = 1, [W] = 0 }
local OPPOSITE = { [N] = S, [E] = W, [S] = N, [W] = E }
local CELL = 24

local function has(bits, side) return bit32.band(bits, side) ~= 0 end

-- A piece turned a quarter clockwise: north becomes east, and so on
local function turned(bits)
  local r = 0
  if has(bits, N) then r = r + E end
  if has(bits, E) then r = r + S end
  if has(bits, S) then r = r + W end
  if has(bits, W) then r = r + N end
  return r
end

local function turned_times(bits, times)
  for _ = 1, times do bits = turned(bits) end
  return bits
end

-- Lays a path from the inlet (left of row `inlet`) to the right-hand edge, then fills the rest
-- with straight and corner pieces, and turns every piece at random. game.random keeps it the
-- same on every machine and in a demo.
local function lay(s)
  s.cells = {}
  for y = 1, s.rows do
    s.cells[y] = {}
    for x = 1, s.cols do
      local corner = game.random(1, 3) == 1
      s.cells[y][x] = turned_times(corner and (N + E) or (N + S), game.random(0, 3))
    end
  end

  local x, y, from = 1, s.inlet, W
  local visited = {}
  while true do
    visited[y * 100 + x] = true
    local ways = {}
    local function may(way, weight)
      for _ = 1, weight do table.insert(ways, way) end
    end
    if x < s.cols then may(E, 2) else may("out", 2) end
    if y > 1 and not visited[(y - 1) * 100 + x] then may(N, 1) end
    if y < s.rows and not visited[(y + 1) * 100 + x] then may(S, 1) end
    local way = ways[game.random(1, #ways)]
    local to = way == "out" and E or way
    s.cells[y][x] = turned_times(from + to, game.random(0, 3))
    if way == "out" then
      s.outlet = y
      return
    end
    x, y, from = x + DX[to], y + DY[to], OPPOSITE[to]
  end
end

local function filled(s, x, y) return s.filled[y * 100 + x] == true end

game.puzzle("pipes", {
  title = "Reroute the coolant",
  verb = "hacking",
  width = 240,
  height = 126,

  start = function(pz, args)
    local s = pz.state
    s.cols = math.max(3, math.min(8, args.cols or 7))
    s.rows = math.max(2, math.min(4, args.rows or 4))
    s.delay = args.delay or 70 * 8
    s.speed = args.speed or 45
    s.inlet = game.random(1, s.rows)
    lay(s)
    s.cx, s.cy = 1, s.inlet
    s.filled = {}
    s.flowing = false
    s.fx, s.fy, s.fin, s.progress = 1, s.inlet, W, 0
  end,

  tick = function(pz, input)
    local s = pz.state

    -- The cursor, and turning the piece under it (not one the flow's reached)
    if input.pressed.left then s.cx = math.max(1, s.cx - 1) end
    if input.pressed.right then s.cx = math.min(s.cols, s.cx + 1) end
    if input.pressed.up then s.cy = math.max(1, s.cy - 1) end
    if input.pressed.down then s.cy = math.min(s.rows, s.cy + 1) end
    local current = s.flowing and s.cx == s.fx and s.cy == s.fy
    if input.pressed.use and not filled(s, s.cx, s.cy) and not current then
      s.cells[s.cy][s.cx] = turned(s.cells[s.cy][s.cx])
      pz:sound("switches/normbutn")
    end

    -- The valve: open after the delay, or now with fire
    if not s.flowing then
      if input.pressed.fire or pz.tics >= s.delay then s.flowing = true end
      return nil
    end

    -- The flow fills a piece, then goes on out of its other end
    s.progress = s.progress + input.tics
    if s.progress < s.speed then return nil end
    s.progress = 0
    local bits = s.cells[s.fy][s.fx]
    if not has(bits, s.fin) then
      pz:sound("world/hitwall")
      return "fail"
    end
    s.filled[s.fy * 100 + s.fx] = true
    local out = bits - s.fin
    local nx, ny = s.fx + DX[out], s.fy + DY[out]
    if nx > s.cols and ny == s.outlet then
      pz:sound("misc/k_pkup")
      return "success"
    end
    if nx < 1 or nx > s.cols or ny < 1 or ny > s.rows then
      pz:sound("world/hitwall")
      return "fail"
    end
    s.fx, s.fy, s.fin = nx, ny, OPPOSITE[out]
    return nil
  end,

  draw = function(pz, width, height)
    local s = pz.state
    local left = math.floor((width - s.cols * CELL) / 2)
    local top = 16

    if s.flowing then
      game.drawtext("The coolant is flowing!", 8, 4, nil, "#80C0FF")
    else
      game.drawtext(string.format("Valve opens in %d. Fire opens it now.", math.ceil((s.delay - pz.tics) / 70)), 8, 4)
    end

    -- The inlet and the outlet
    game.drawrect(left - 8, top + (s.inlet - 1) * CELL + 9, 8, 6, "#3070FF")
    game.drawrect(left + s.cols * CELL, top + (s.outlet - 1) * CELL + 9, 8, 6, "#40C040")

    for y = 1, s.rows do
      for x = 1, s.cols do
        local cx, cy = left + (x - 1) * CELL, top + (y - 1) * CELL
        game.drawrect(cx + 1, cy + 1, CELL - 2, CELL - 2, "#202020")
        local bits = s.cells[y][x]
        local color = filled(s, x, y) and "#3070FF" or (s.flowing and x == s.fx and y == s.fy) and "#80B0FF" or "#A0A0A0"
        local mx, my = cx + CELL / 2, cy + CELL / 2
        for _, side in ipairs({ N, E, S, W }) do
          if has(bits, side) then
            game.drawline(mx - 1, my - 1, mx - 1 + DX[side] * (CELL / 2 - 1), my - 1 + DY[side] * (CELL / 2 - 1), color, 4)
          end
        end
      end
    end
    game.drawframe(left + (s.cx - 1) * CELL, top + (s.cy - 1) * CELL, CELL, CELL, "#FFFF40")
    game.drawtext("Arrows move, USE turns a piece.", 8, top + s.rows * CELL + 4)
  end,
})
