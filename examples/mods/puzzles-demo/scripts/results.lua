-- What the puzzles' ends do here. The switches' own fields do most of it (mapdefs/walls.yaml:
-- a win throws the switch and opens its closet, a fail sounds the alarm or breaks it); this
-- script shows a message for each end, and sees to power routing's choice, which a switch
-- can't (its result is the system's name, not a win or a fail).
--
-- The messages are keyed ("puzzle"), so they show even with the messages setting off.

local said = {
  success = "Done!",
  fail = "It failed.",
  cancel = "You leave it be.",
}

-- Power routing (power.lua): what each system gets, on the station's tag
local routed = {
  vault = { action = 'A_OpenDoor("hold")', message = "Power to the vault: its door slides open." },
  medbay = { action = 'A_GiveInventory("FirstAidKit")', message = "Power to the medbay: it dispenses a first aid kit." },
  armory = { action = 'A_GiveInventory("ClipBox")', message = "Power to the armory: it dispenses ammo." },
}

game.onpuzzle(function(player, name, result, x, y, tag)
  local route = name == "power" and routed[result]
  if route then
    if tag > 0 then game.trigger(tag, route.action) end
    game.message(route.message, nil, "puzzle")
    return
  end
  game.message(said[result] or ("Puzzle " .. name .. ": " .. result), nil, "puzzle")
end)
