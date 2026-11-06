-- Frequency tuning: line your wave (yellow) up with the signal (green). Left and right change
-- its frequency, up and down its height; the mouse or a stick does both at once. Held on the
-- signal for a second, it's locked on. Running out of time is a fail.
--
--     A_Puzzle("frequency", "seconds=25")
--
-- This file is meant to be copied: carry it in your own mod and change it as you like.

local LEFT, MID, WIDTH = 8, 52, 204
local LOCK_TICS = 70

local function near(s) return math.abs(s.freq - s.target) < 0.15 and math.abs(s.height - s.goal) < 3 end

local function wave(freq, height, phase, color)
  local last_x, last_y = nil, nil
  for x = 0, WIDTH, 4 do
    local y = MID + height * math.sin(2 * math.pi * freq * x / WIDTH + phase)
    if last_x then game.drawline(LEFT + last_x, last_y, LEFT + x, y, color) end
    last_x, last_y = x, y
  end
end

game.puzzle("frequency", {
  title = "Tune in the signal",
  verb = "tuning a radio",
  width = 220,
  height = 128,
  timelimit = 70 * 25,

  start = function(pz, args)
    local s = pz.state
    s.target = game.random(20, 60) / 10       -- waves across the screen
    s.goal = game.random(12, 34)              -- pixels high
    s.freq = s.target > 4 and 1.5 or 6
    s.height = s.goal > 22 and 8 or 38
    s.locked = 0
    s.phase = 0
  end,

  tick = function(pz, input)
    local s = pz.state
    local t = input.tics
    s.phase = s.phase + 0.05 * t
    if input.held.left then s.freq = s.freq - 0.02 * t end
    if input.held.right then s.freq = s.freq + 0.02 * t end
    if input.held.up then s.height = s.height + 0.4 * t end
    if input.held.down then s.height = s.height - 0.4 * t end
    s.freq = math.max(0.5, math.min(8, s.freq + input.dx * 0.002))
    s.height = math.max(2, math.min(44, s.height - input.dy * 0.05))

    if near(s) then
      s.locked = s.locked + t
      if s.locked >= LOCK_TICS then
        pz:sound("misc/k_pkup")
        return "success"
      end
    else
      s.locked = math.max(0, s.locked - 2 * t)
    end
    return nil
  end,

  draw = function(pz, width, height)
    local s = pz.state
    game.drawrect(LEFT, MID - 46, WIDTH, 92, "#001008")
    game.drawline(LEFT, MID, LEFT + WIDTH, MID, "#204030")
    wave(s.target, s.goal, s.phase, "#30C060")
    wave(s.freq, s.height, s.phase, near(s) and "#FFFFFF" or "#E0D020")

    game.drawtext("Signal", LEFT, 104)
    game.drawrect(LEFT + 40, 106, 100, 5, "#303030")
    game.drawrect(LEFT + 40, 106, math.floor(100 * s.locked / LOCK_TICS), 5, "#40C040")
    if pz.timeleft then game.drawtext(string.format("%d s", math.ceil(pz.timeleft / 70)), LEFT + 160, 104) end
    game.drawtext("Arrows or mouse tune it.", LEFT, 118)
  end,
})
