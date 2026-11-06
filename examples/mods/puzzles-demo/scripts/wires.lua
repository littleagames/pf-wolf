-- Wire cutting: the wires tangle from the cutters on the left to the right-hand terminals. One
-- of those (the lit one) is the detonator: cut the wire that runs to it. Up and down pick a wire,
-- use cuts it. A wrong wire is a fail (or one of `mistakes` allowed).
--
--     A_Puzzle("wires", "wires=5", "mistakes=0")
--
-- This file is meant to be copied: carry it in your own mod and change it as you like.

local COLORS = { "#E03030", "#3060E0", "#E0D020", "#30B030", "#E0E0E0", "#C060E0" }
local LEFT, RIGHT, TOP, GAP = 24, 206, 12, 17

game.puzzle("wires", {
  title = "Defuse the charge",
  verb = "defusing a bomb",
  width = 230,
  height = 128,

  start = function(pz, args)
    local s = pz.state
    s.count = math.max(2, math.min(#COLORS, args.wires or 5))
    s.mistakes = args.mistakes or 0

    -- Where each wire ends on the right (a shuffle), and which terminal is the detonator
    s.ends = {}
    for i = 1, s.count do s.ends[i] = i end
    for i = s.count, 2, -1 do
      local j = game.random(1, i)
      s.ends[i], s.ends[j] = s.ends[j], s.ends[i]
    end
    s.live = game.random(1, s.count)

    -- Two bends in each, at random heights, so they tangle
    s.bends = {}
    for i = 1, s.count do
      s.bends[i] = { game.random(0, (s.count - 1) * GAP), game.random(0, (s.count - 1) * GAP) }
    end

    s.cut = {}
    s.cursor = 1
  end,

  tick = function(pz, input)
    local s = pz.state
    if input.pressed.up then s.cursor = math.max(1, s.cursor - 1) end
    if input.pressed.down then s.cursor = math.min(s.count, s.cursor + 1) end
    if not (input.pressed.use or input.pressed.fire) or s.cut[s.cursor] then return nil end

    s.cut[s.cursor] = true
    pz:sound("switches/normbutn")
    if s.ends[s.cursor] == s.live then
      pz:sound("misc/k_pkup")
      return "success"
    end
    s.mistakes = s.mistakes - 1
    if s.mistakes < 0 then
      pz:sound("world/hitwall")
      return "fail"
    end
    return nil
  end,

  draw = function(pz, width, height)
    local s = pz.state
    for i = 1, s.count do
      local y1 = TOP + (i - 1) * GAP
      local y4 = TOP + (s.ends[i] - 1) * GAP
      local y2, y3 = TOP + s.bends[i][1], TOP + s.bends[i][2]
      local color = COLORS[i]
      if s.cut[i] then
        -- cut: only its two ends are left
        game.drawline(LEFT, y1, LEFT + 20, y1, color, 2)
        game.drawline(RIGHT - 20, y4, RIGHT, y4, color, 2)
      else
        game.drawline(LEFT, y1, 80, y2, color, 2)
        game.drawline(80, y2, 150, y3, color, 2)
        game.drawline(150, y3, RIGHT, y4, color, 2)
      end
      game.drawrect(LEFT - 8, y1 - 3, 8, 7, "#808080")
    end
    for t = 1, s.count do
      game.drawrect(RIGHT, TOP + (t - 1) * GAP - 3, 8, 7, t == s.live and "#FF2020" or "#606060")
    end
    game.drawframe(LEFT - 12, TOP + (s.cursor - 1) * GAP - 6, 16, 13, "#FFFF40")
    game.drawtext("Cut the wire to the red terminal.", 8, height - 14)
  end,
})
