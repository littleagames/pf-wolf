-- The Fonts Demo's script: some of the mod's fonts (fonts.yaml) printed over the view as you
-- play, down its left side, with game.drawtext(text, x, y, font, color). The font brings its
-- shadow, gradient or outline with it. Glows are left out here: a glow fades into a plain
-- background color (a menu window's), which the 3D view doesn't have.
--
-- "fonts" in the console hides them, and shows them again.
--   game.command    the console command
--   game.onhud      drawing over the view each frame
--   game.textwidth  how wide and tall a line is in a font, to space the lines
--   game.onlevel, game.ontick   a message as the level starts, in the mod's own message
--                   style (hud-messages.yaml), which prints in FdOutline

local shown = true

local SAMPLES = {
  { "Shadow", "FdShadowSmall", "White" },
  { "Outline", "FdOutlineSmall", "White" },
  { "All upper case", "FdCaps", "Yellow" },
  { "Italic from a .fnt", "FdItalic", "Light Blue" },
  { "Dot matrix", "FdDots", "Lime" },
  { "GOLD", "FdGold", "White" },
}

game.command("fonts", "Hides or shows the Fonts Demo's font samples over the view", function()
  shown = not shown
end)

-- The message waits for the level's first tick: the level clears the messages as it starts,
-- after game.onlevel has run
local greet = false

game.onlevel(function(reason, map)
  greet = true
end)

game.ontick(function(tics)
  if greet then
    greet = false
    game.message("Font Gallery: in Options", "FontsDemo")
  end
end)

game.onhud(function()
  if not shown then return end

  local width, height = game.viewsize()

  -- Up from the bottom left corner, clear of the messages at the top left
  local total = 0
  for _, sample in ipairs(SAMPLES) do
    local _, h = game.textwidth(sample[1], sample[2])
    total = total + h + 2
  end
  local y = height - total - 2
  for _, sample in ipairs(SAMPLES) do
    local text, font, color = sample[1], sample[2], sample[3]
    game.drawtext(text, 4, y, font, color)
    local _, h = game.textwidth(text, font)
    y = y + h + 2
  end

  -- The level's clock in the seven-segment digits (a graphic font, in its own colors)
  local seconds = math.floor(game.time() / 70)
  local clock = string.format("%02d:%02d", math.floor(seconds / 60) % 100, seconds % 60)
  local clockwidth = game.textwidth(clock, "FdLed")
  game.drawtext(clock, width - clockwidth - 4, 4, "FdLed")
end)
