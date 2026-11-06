-- A safe's dial: turn it left and right to each number of the combination in turn and press use
-- to set it. A tumbler clicks (and the lamp glows) as the dial reaches the number it wants.
-- Setting a wrong number starts the combination again; three times is a fail.
--
--     A_Puzzle("safedial", "numbers=3", "size=40", "strikes=3")
--
-- This file is meant to be copied: carry it in your own mod and change it as you like.

local CX, CY, R = 70, 66, 46     -- the dial: its middle and radius

-- How long a turn key's held before it repeats, and how fast it repeats then (tics)
local REPEAT_AFTER, REPEAT_EVERY = 14, 3

local function turn(pz, s, by)
  s.dial = (s.dial + by) % s.size
  if s.dial == s.combo[s.step] then pz:sound("switches/normbutn") end
end

game.puzzle("safedial", {
  title = "Crack the safe",
  verb = "cracking a safe",
  width = 220,
  height = 128,

  start = function(pz, args)
    local s = pz.state
    s.size = math.max(10, args.size or 40)
    s.combo = {}
    for i = 1, math.max(1, args.numbers or 3) do s.combo[i] = game.random(0, s.size - 1) end
    s.dial = 0
    s.step = 1
    s.strikes = 0
    s.maxstrikes = args.strikes or 3
    s.held = 0
  end,

  tick = function(pz, input)
    local s = pz.state

    -- Turning: a press moves it one; held, it runs on
    local way = (input.held.left and -1) or (input.held.right and 1) or 0
    if way == 0 then
      s.held = 0
    else
      s.held = s.held + input.tics
      if input.pressed.left or input.pressed.right then
        turn(pz, s, way)
      elseif s.held >= REPEAT_AFTER and (s.held - REPEAT_AFTER) % REPEAT_EVERY < input.tics then
        turn(pz, s, way)
      end
    end

    if input.pressed.use or input.pressed.fire then
      if s.dial == s.combo[s.step] then
        s.step = s.step + 1
        if s.step > #s.combo then
          pz:sound("misc/k_pkup")
          return "success"
        end
        pz:sound("doors/close")
      else
        s.strikes = s.strikes + 1
        s.step = 1
        pz:sound("misc/do_nothing")
        if s.strikes >= s.maxstrikes then return "fail" end
      end
    end
    return nil
  end,

  draw = function(pz, width, height)
    local s = pz.state

    -- The dial: a ring, its numbers turned by where it is, the mark fixed at the top
    local segments = 32
    for i = 0, segments - 1 do
      local a1, a2 = 2 * math.pi * i / segments, 2 * math.pi * (i + 1) / segments
      game.drawline(CX + R * math.sin(a1), CY - R * math.cos(a1), CX + R * math.sin(a2), CY - R * math.cos(a2), "#C0C0C0", 2)
    end
    for n = 0, s.size - 1, 5 do
      local a = 2 * math.pi * (n - s.dial) / s.size
      local label = tostring(n)
      game.drawtext(label, CX + (R - 12) * math.sin(a) - game.textwidth(label) / 2, CY - (R - 12) * math.cos(a) - 3)
    end
    for n = 0, s.size - 1 do
      local a = 2 * math.pi * (n - s.dial) / s.size
      local inner = (n % 5 == 0) and R - 6 or R - 3
      game.drawline(CX + inner * math.sin(a), CY - inner * math.cos(a), CX + R * math.sin(a), CY - R * math.cos(a), "#808080")
    end
    game.drawrect(CX - 1, CY - R - 8, 3, 8, "#FF4040")

    -- What it's on, the lamp, and the numbers set so far
    game.drawtext(string.format("%02d", s.dial), 140, 20, nil, "White")
    game.drawrect(170, 18, 10, 10, s.dial == s.combo[s.step] and "#FFE060" or "#403820")
    for i = 1, #s.combo do
      game.drawrect(140 + (i - 1) * 16, 40, 12, 8, i < s.step and "#40C040" or "#303030")
    end
    game.drawtext(string.format("Strikes %d of %d", s.strikes, s.maxstrikes), 132, 58)
    game.drawtext("Left/right turn.", 132, 80)
    game.drawtext("USE sets a number.", 132, 92)
  end,
})
