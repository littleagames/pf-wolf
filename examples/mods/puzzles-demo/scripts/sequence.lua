-- A sequence (as Simon): the four lamps light up in turn; repeat them with the arrows (up,
-- right, down, left) or the keys 1 to 4. Each round adds one more, up to `length`. A wrong lamp
-- is a fail.
--
--     A_Puzzle("sequence", "length=6", "start=3")
--
-- This file is meant to be copied: carry it in your own mod and change it as you like.

local LIT, GAP, PAUSE, ECHO = 24, 10, 30, 10   -- tics: a lamp lit, between lamps, before a round, a press lit

-- The lamps: where each is (a diamond), its color dark and lit, and the way that presses it
local LAMPS = {
  { x = 60, y = 14, off = "#402020", on = "#FF4040", way = "up" },
  { x = 96, y = 48, off = "#204020", on = "#40FF40", way = "right" },
  { x = 60, y = 82, off = "#202040", on = "#4080FF", way = "down" },
  { x = 24, y = 48, off = "#404020", on = "#FFFF40", way = "left" },
}
local SIZE = 30

local function begin_round(s)
  s.phase = "show"
  s.timer = -PAUSE
  s.shown = 1
  s.pos = 1
end

game.puzzle("sequence", {
  title = "Match the signal",
  verb = "hacking",
  width = 220,
  height = 124,

  start = function(pz, args)
    local s = pz.state
    s.length = math.max(1, args.length or 6)
    s.round = math.min(s.length, args.start or 3)
    s.seq = {}
    for i = 1, s.length do s.seq[i] = game.random(1, 4) end
    s.lit, s.litfor = 0, 0
    begin_round(s)
  end,

  tick = function(pz, input)
    local s = pz.state
    if s.litfor > 0 then
      s.litfor = s.litfor - input.tics
      if s.litfor <= 0 then s.lit = 0 end
    end

    -- Showing the round: each lamp lit in turn, then it's the player's go
    if s.phase == "show" then
      s.timer = s.timer + input.tics
      if s.timer >= LIT + GAP then
        s.timer = 0
        s.shown = s.shown + 1
        if s.shown > s.round then s.phase = "input" end
      end
      return nil
    end

    local pressed = input.digit
    if pressed == nil then
      for i, lamp in ipairs(LAMPS) do
        if input.pressed[lamp.way] then pressed = i end
      end
    end
    if pressed == nil or pressed < 1 or pressed > 4 then return nil end

    s.lit, s.litfor = pressed, ECHO
    pz:sound("switches/normbutn")
    if pressed ~= s.seq[s.pos] then
      pz:sound("world/hitwall")
      return "fail"
    end
    s.pos = s.pos + 1
    if s.pos > s.round then
      s.round = s.round + 1
      if s.round > s.length then
        pz:sound("misc/k_pkup")
        return "success"
      end
      begin_round(s)
    end
    return nil
  end,

  draw = function(pz, width, height)
    local s = pz.state
    local showing = s.phase == "show" and s.timer >= 0 and s.timer < LIT and s.seq[s.shown] or nil
    for i, lamp in ipairs(LAMPS) do
      local on = showing == i or s.lit == i
      game.drawrect(lamp.x, lamp.y, SIZE, SIZE, on and lamp.on or lamp.off)
      game.drawframe(lamp.x, lamp.y, SIZE, SIZE, "#909090")
      game.drawtext(tostring(i), lamp.x + 12, lamp.y + 11, nil, on and "Black" or "#909090")
    end
    game.drawtext(string.format("Round %d of %d", math.min(s.round, s.length), s.length), 140, 20)
    game.drawtext(s.phase == "show" and "Watch..." or "Your turn!", 140, 36, nil, s.phase == "show" and "#C0C0C0" or "#60FF60")
    game.drawtext("Arrows or 1-4", 140, 60)
    game.drawtext("repeat the lamps.", 140, 72)
  end,
})
