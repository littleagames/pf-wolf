-- The Boss Demo's script: the fight's rules and its health bar.
--   game.onactorspawn  each phase notes its full health as it comes in (temp1), for the bar
--   game.ondamage      the Warden at half health: he goes to his Enrage state (and the next
--                      phase takes over); the hit that gets him there leaves him standing
--   game.onkill        the enraged Warden dead: the exit door opens, the music goes back
--   game.onhud         the health bar over the top of the view, while he's after you
-- temp1 and temp2 are kept in saved games, so a loaded fight carries on as it was.

local PHASES = { Warden = "THE WARDEN", WardenEnraged = "THE WARDEN, ENRAGED" }
local EXIT_TAG = 1

game.onactorspawn(function(actor)
  if PHASES[actor.name] then actor.temp1 = actor.health end
end)

game.ondamage(function(victim, amount, attacker, type)
  if victim.name ~= "Warden" or victim.temp2 == 1 then return nil end
  local half = math.floor(victim.temp1 / 2)
  if victim.health - amount > half then return nil end
  victim.temp2 = 1
  victim:jump("Enrage")
  game.message("The Warden is enraged!")
  game.music("ZEROHOUR")
  return math.max(victim.health - half, 0)
end)

game.onkill(function(victim, killer, type)
  if victim.name == "WardenEnraged" then
    game.trigger(EXIT_TAG, "A_OpenDoor", "hold")
    game.message("The Warden is dead. The way out is open.")
    game.music(nil)
  end
end)

-- The boss there is now, and its name on the bar
local function boss()
  for name, label in pairs(PHASES) do
    local found = game.actors(name)[1]
    if found and found.health > 0 then return found, label end
  end
  return nil
end

game.onhud(function()
  local b, label = boss()
  if not b or b.temp1 <= 0 or b.state == "Spawn" then return end
  local width = game.viewsize()
  local left = math.floor((width - 164) / 2)
  game.drawpic("BFBAR", left, 4)
  local steps = math.floor(80 * b.health / b.temp1 + 0.5)
  for i = 0, steps - 1 do game.drawpic("BFSEG", left + 2 + 2 * i, 6) end
  game.drawtext(label, left, 17, "SmallFont", "White")
end)
