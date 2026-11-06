-- The Transitions Demo's script: each panel in the gallery (MAP01) plays its fade style as it
-- takes you to the same panel in the other hall. The panel's trigger does the moving
-- (A_TeleportTag, mapdefs/triggers.yaml); this only picks the style, by the panel's tag.
--
--   game.onuse        a panel being used, before its trigger moves you
--   game.transition   from the Hall of Cuts: the view after the move comes in straight over
--                     the one before it, with no color between
--   game.fade         from the Hall of Fades: the screen goes out to black at once, the move
--                     happens out of sight, and it comes back in the same style
--   game.command      `fadetics`, how long they take
--   game.message      the style's name, with a key so it shows with messages off too

-- The styles in tag order: the panel with tag 1 is palette (tools/make-transitions-demo.ps1
-- lists them the same way)
local STYLES = {
  "palette", "fizzle", "melt", "mosaic", "crossfade", "dither", "burn", "iris", "doors",
  "crt", "clock", "spiral", "wipe-left", "wipe-right", "wipe-up", "wipe-down", "blinds",
}

-- The Hall of Cuts is the top hall: its panels are on rows 21 and 27, the Hall of Fades' on
-- 29 and 35
local LAST_CUTS_ROW = 27

-- How long a transition takes, and each half of a fade, in tics (70 a second)
local tics = 50

-- How long the style's name stays up, in tics
local MESSAGE_TICS = 140
local messageLeft = 0

local function say(text)
  game.message(text, nil, "transitions-demo")
  messageLeft = MESSAGE_TICS
end

game.onuse(function(player, x, y, dir)
  local style = STYLES[game.tag(x, y)]
  if style == nil then return end       -- not a panel: the game carries on as it would

  if y <= LAST_CUTS_ROW then
    if style == "palette" then
      -- A palette fade steps every color toward another: between two pictures there's
      -- nothing to step, so it's a plain cut
      game.transition(style, tics)
      say("palette: only fades, so a plain cut here")
    else
      game.transition(style, tics)
      say(style .. ": straight across")
    end
  else
    game.fade(style, tics)
    say(style .. ": out through black")
  end
  -- Nothing returned: the panel's trigger still takes you across
end)

game.ontick(function(t)
  if messageLeft > 0 then
    messageLeft = messageLeft - t
    if messageLeft <= 0 then
      game.message(nil, nil, "transitions-demo")
    end
  end
end)

-- The greeting waits for the level's first tick: the level clears the messages as it starts
local greet = false

game.onlevel(function(reason, map)
  greet = map == "MAP01"
end)

game.ontick(function(t)
  if greet then
    greet = false
    say("Use a panel: this hall cuts straight across, the other fades")
  end
end)

game.command("fadetics", "How long the Transitions Demo's panels take, in tics (70 a second): fadetics 50", function(n)
  if type(n) == "number" and n >= 1 and n <= 700 then
    tics = math.floor(n)
  end
  print("The panels take " .. tics .. " tics")
end)
