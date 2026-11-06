-- A sliding picture: the picture's cut into tiles with one missing; slide them back into place.
-- An arrow slides the tile next to the gap that way (left slides the tile right of the gap
-- into it). Without the picture, the tiles are numbered.
--
--     A_Puzzle("sliding", "pic=PZSLIDE", "size=3", "shuffle=40")
--
-- pic: any picture (graphics/ in a mod, or the game's own), cut from its top left into
-- size by size tiles. This file is meant to be copied: carry it in your own mod and change it.

local function solved(s)
  for i = 1, s.size * s.size do
    if s.tiles[i] ~= i then return false end
  end
  return true
end

-- Slides the tile at (x, y), if it's beside the gap, into it
local function slide(s, x, y)
  if x < 1 or x > s.size or y < 1 or y > s.size then return false end
  local from, to = (y - 1) * s.size + x, (s.gy - 1) * s.size + s.gx
  s.tiles[to], s.tiles[from] = s.tiles[from], s.tiles[to]
  s.gx, s.gy = x, y
  return true
end

game.puzzle("sliding", {
  title = "Put it back together",
  verb = "working a puzzle",
  width = 220,
  height = 116,

  start = function(pz, args)
    local s = pz.state
    s.size = math.max(2, math.min(4, args.size or 3))
    s.pic = args.pic or "PZSLIDE"
    s.tiles = {}
    for i = 1, s.size * s.size do s.tiles[i] = i end
    s.gx, s.gy = s.size, s.size          -- the gap: the last tile's place

    -- Shuffled by sliding at random from solved, so it can always be solved; never straight back
    local back = nil
    for _ = 1, args.shuffle or 40 do
      local moves = {}
      for _, d in ipairs({ { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } }) do
        local x, y = s.gx + d[1], s.gy + d[2]
        if x >= 1 and x <= s.size and y >= 1 and y <= s.size and not (back and back[1] == -d[1] and back[2] == -d[2]) then
          table.insert(moves, d)
        end
      end
      local d = moves[game.random(1, #moves)]
      slide(s, s.gx + d[1], s.gy + d[2])
      back = d
    end
    if solved(s) then slide(s, s.gx - 1 >= 1 and s.gx - 1 or s.gx + 1, s.gy) end
    s.moves = 0
  end,

  tick = function(pz, input)
    local s = pz.state
    local moved = (input.pressed.left and slide(s, s.gx + 1, s.gy))
      or (input.pressed.right and slide(s, s.gx - 1, s.gy))
      or (input.pressed.up and slide(s, s.gx, s.gy + 1))
      or (input.pressed.down and slide(s, s.gx, s.gy - 1))
    if not moved then return nil end
    s.moves = s.moves + 1
    pz:sound("switches/normbutn")
    if solved(s) then
      pz:sound("misc/k_pkup")
      return "success"
    end
    return nil
  end,

  draw = function(pz, width, height)
    local s = pz.state
    local pw, ph = game.picsize(s.pic)
    local tile = pw and math.floor(math.min(pw, ph) / s.size) or 30
    local left, top = 8, 8
    for i = 1, s.size * s.size do
      local x = left + ((i - 1) % s.size) * (tile + 1)
      local y = top + math.floor((i - 1) / s.size) * (tile + 1)
      local piece = s.tiles[i]
      if piece == s.size * s.size then
        game.drawrect(x, y, tile, tile, "#101010")
      elseif pw then
        local sx, sy = ((piece - 1) % s.size) * tile, math.floor((piece - 1) / s.size) * tile
        game.drawpic(s.pic, x, y, sx, sy, tile, tile)
      else
        game.drawrect(x, y, tile, tile, "#505070")
        game.drawtext(tostring(piece), x + tile / 2 - 3, y + tile / 2 - 3, nil, "White")
      end
    end
    local tx = left + s.size * (tile + 1) + 10
    game.drawtext(string.format("Moves: %d", s.moves), tx, 12)
    game.drawtext("Arrows slide", tx, 36)
    game.drawtext("a tile into", tx, 48)
    game.drawtext("the gap.", tx, 60)
  end,
})
