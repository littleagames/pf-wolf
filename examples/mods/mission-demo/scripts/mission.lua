-- The Mission Demo's script, showing the event hooks working together:
--   game.onhud        the objectives panel, the time left and a compass to the exit, drawn over
--                     the view every frame (game.drawpic, game.drawtext, game.viewsize)
--   game.onlevel      the documents objective added to the level (A_AddObjective)
--   game.onpickup     taking the documents completes it (A_CompleteObjective)
--   game.onobjective  word once everything the exit needs is done
--   game.ontimeup     reinforcements when the clock runs out, instead of the player dying
--   game.ontally      a line on the level's tally saying how the mission went
--   game.command      an "objectives" console command
-- Hooks that draw (onhud) or report (ontally) run on each machine alone and mustn't change the
-- game; the rest may. Nothing here is kept in Lua variables that a saved game would need: the
-- objectives are the game's own counts, and the time is the level's clock.

local MAP = "MAP01"
local TIME_LIMIT = 240          -- game-info.yaml's MAP01 time-limit, in seconds

local OBJECTIVES = {
  { name = "documents", label = "Documents", task = "Steal the documents" },
  { name = "radios",    label = "Radios",    task = "Wreck the radios" },
  { name = "officers",  label = "Officers",  task = "Kill the officers", optional = true },
}

local function secondsleft()
  return TIME_LIMIT - math.floor(game.time() / 70)
end

-- The documents are an item, not something the level counts by itself: add one to do as the
-- level starts afresh (a loaded game has its counts already)
game.onlevel(function(reason, map)
  if map ~= MAP then return end
  if reason == "new" or reason == "restarted" then
    game.trigger(0, "A_AddObjective", "documents")
  end
  if reason == "new" then
    game.message("Get the documents and wreck the radios, then get out. Four minutes.")
  end
end)

game.onpickup(function(player, item, amount)
  if item.name == "SecretDocuments" then
    game.trigger(0, "A_CompleteObjective", "documents")
  end
end)

-- Once both objectives the exit waits for are done
game.onobjective(function(name, done, total)
  if done < total then return end
  local d, dt = game.objective("documents")
  local r, rt = game.objective("radios")
  if d == dt and r == rt then
    game.message("That's everything. Get to the exit, east of the yard!")
  end
end)

-- Time's up: SS come in at each of the level's reinforcement spots and go straight for you.
-- Returning true says the script has seen to it, so nobody dies of it.
game.ontimeup(function()
  for _, spot in ipairs(game.actors("ReinforcementSpot")) do
    local ss = spot:spawn("SS")
    if ss then ss:jump("Chase") end
  end
  game.message("Out of time: reinforcements!")
  return true
end)

--
-- THE HUD
--

-- The way to something as the player sees it: an arrow and how many tiles
local function compass(player, target)
  local dx, dy = target.x - player.x, player.y - target.y      -- y grows southward
  local bearing = math.deg(math.atan2(dy, dx))
  local turn = (bearing - player.angle + 540) % 360 - 180     -- -180..180, positive to the left
  local arrow
  if math.abs(turn) <= 30 then arrow = "^"
  elseif math.abs(turn) >= 150 then arrow = "v"
  elseif turn > 0 then arrow = "<"
  else arrow = ">" end
  return arrow, math.floor(math.sqrt(dx * dx + dy * dy) + 0.5)
end

game.onhud(function()
  if game.map() ~= MAP then return end
  local width, height = game.viewsize()

  -- the objectives, bottom left (the game's own messages are at the top)
  local top = height - 36
  game.drawpic("MDPANEL", 4, top)
  for i, o in ipairs(OBJECTIVES) do
    local done, total = game.objective(o.name)
    local finished = total > 0 and done >= total
    local text = (finished and "+ " or "- ") .. o.label
    if total > 1 then text = text .. " " .. done .. "/" .. total end
    local color = finished and "Lime" or (o.optional and "Grey" or "White")
    game.drawtext(text, 9, top + 2 + (i - 1) * 9, "SmallFont", color)
  end

  -- the time left, top right; then the reinforcements' warning
  local left = secondsleft()
  if left > 0 then
    local clock = string.format("%d:%02d", math.floor(left / 60), left % 60)
    game.drawtext(clock, width - 36, 6, "SmallFont", left <= 30 and "Maroon" or "Yellow")
  else
    game.drawtext("REINFORCEMENTS", width - 92, 6, "SmallFont", "Maroon")
  end

  -- the compass to the exit, bottom right
  local player = game.players()[1]
  local exit = game.actors("MissionExitMarker")[1]
  if player and exit then
    local arrow, tiles = compass(player, exit)
    game.drawtext(arrow .. " EXIT " .. tiles .. " " .. arrow, width - 70, height - 12, "SmallFont", "Yellow")
  end
end)

-- On the tally: how the mission went
game.ontally(function(map, result)
  if map ~= MAP then return nil end
  local parts = {}
  for _, o in ipairs(OBJECTIVES) do
    local done, total = game.objective(o.name)
    table.insert(parts, o.name .. " " .. done .. "/" .. total)
  end
  return "Mission: " .. table.concat(parts, ", ")
end)

-- In the console: "objectives"
game.command("objectives", "Lists the mission's objectives and how far each has got", function()
  for _, o in ipairs(OBJECTIVES) do
    local done, total = game.objective(o.name)
    print(o.task .. ": " .. done .. " of " .. total .. (o.optional and " (optional)" or ""))
  end
  print("Time left: " .. math.max(secondsleft(), 0) .. " seconds")
end)
