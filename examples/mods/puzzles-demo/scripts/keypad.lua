-- A keypad: type the code (found somewhere in the level: a note, a body, a radio log). The
-- number keys type it straight in; or move round the keypad with the arrows and press use. Fire
-- (or the keypad's OK) tries it; a code as long as the right one is tried by itself. Three wrong
-- codes are a fail.
--
--     A_Puzzle("keypad", "code=7319", "tries=3")
--
-- This file is meant to be copied: carry it in your own mod and change it as you like.

-- The keypad's keys, row by row: digits, C (clear) and OK
local KEYS = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "OK" }
local KEY_W, KEY_H = 30, 18

local function try(pz, s)
  if s.entered == s.code then
    pz:sound("misc/k_pkup")
    return "success"
  end
  s.strikes = s.strikes + 1
  s.entered = ""
  s.flash = 30
  if s.strikes >= s.tries then
    pz:sound("world/hitwall")
    return "fail"
  end
  pz:sound("misc/do_nothing")
  return nil
end

local function press(pz, s, key)
  if key == "C" then
    s.entered = ""
  elseif key == "OK" then
    return try(pz, s)
  elseif #s.entered < #s.code then
    s.entered = s.entered .. key
    pz:sound("switches/normbutn")
    if #s.entered == #s.code then return try(pz, s) end
  end
  return nil
end

game.puzzle("keypad", {
  title = "Security keypad",
  verb = "typing a code",
  width = 200,
  height = 128,

  start = function(pz, args)
    local s = pz.state
    s.code = tostring(args.code or 1234)
    s.tries = args.tries or 3
    s.entered = ""
    s.strikes = 0
    s.cursor = 1
    s.flash = 0
  end,

  tick = function(pz, input)
    local s = pz.state
    if s.flash > 0 then s.flash = s.flash - input.tics end

    if input.digit then return press(pz, s, tostring(input.digit)) end
    if input.pressed.left and (s.cursor - 1) % 3 > 0 then s.cursor = s.cursor - 1 end
    if input.pressed.right and (s.cursor - 1) % 3 < 2 then s.cursor = s.cursor + 1 end
    if input.pressed.up and s.cursor > 3 then s.cursor = s.cursor - 3 end
    if input.pressed.down and s.cursor < 10 then s.cursor = s.cursor + 3 end
    if input.pressed.use then return press(pz, s, KEYS[s.cursor]) end
    if input.pressed.fire then return try(pz, s) end
    return nil
  end,

  draw = function(pz, width, height)
    local s = pz.state

    -- The display: what's typed, red for a moment after a wrong code
    game.drawrect(10, 4, 80, 18, s.flash > 0 and "#600000" or "#002800")
    game.drawtext(s.flash > 0 and "DENIED" or (s.entered .. string.rep("-", #s.code - #s.entered)), 16, 9, nil,
      s.flash > 0 and "#FF6060" or "#60FF60")
    game.drawtext(string.format("Tries left: %d", s.tries - s.strikes), 100, 9)

    for i, key in ipairs(KEYS) do
      local x = 10 + ((i - 1) % 3) * (KEY_W + 4)
      local y = 30 + math.floor((i - 1) / 3) * (KEY_H + 4)
      game.drawrect(x, y, KEY_W, KEY_H, "#404040")
      game.drawframe(x, y, KEY_W, KEY_H, i == s.cursor and "#FFFF40" or "#808080")
      local w = game.textwidth(key)
      game.drawtext(key, x + math.floor((KEY_W - w) / 2), y + 5, nil, "White")
    end
    game.drawtext("Type the code,", 120, 40)
    game.drawtext("or arrows", 120, 52)
    game.drawtext("and USE.", 120, 64)
    game.drawtext("FIRE tries it.", 120, 80)
  end,
})
