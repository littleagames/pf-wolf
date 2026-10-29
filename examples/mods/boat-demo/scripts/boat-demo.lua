-- The Boat Demo's script: the river (mapdefs/flats.yaml's damage) hurts a player on foot but
-- not one in the boat, a word as the level starts and on falling in, and a hint while the boat
-- is alongside a dock.

local WATER = "BDWATER"
local DOCK = "BDDOCK"

game.onlevel(function(reason, map)
  if map == "MAP01" and reason == "new" then
    game.message("Take the boat downriver: face it from the pier and press use.")
  end
end)

-- The floor's damage has no attacker; in the boat, the water doesn't reach the player
game.ondamage(function(victim, amount, attacker, type)
  if victim.isplayer and victim.vehicle ~= nil and attacker == nil
      and game.floor(victim.tilex, victim.tiley) == WATER then
    return 0
  end
end)

-- The tile behind the gun nest has tag 2 (the map's tag plane)
local BEHIND_NEST = 2

game.onenter(function(actor, x, y)
  if not actor.isplayer then return end
  if actor.vehicle == nil and game.floor(x, y) == WATER then
    game.message("The water's freezing! Get back to the boat.")
  elseif game.tag(x, y) == BEHIND_NEST then
    game.message("Face the gun and press use to man it; use again to step off.")
  end
end)

-- Whether a dock is next to a tile (the docks are the tiles with the dock planks)
local function besidedock(x, y)
  return game.floor(x + 1, y) == DOCK or game.floor(x - 1, y) == DOCK
      or game.floor(x, y + 1) == DOCK or game.floor(x, y - 1) == DOCK
end

-- While the boat's beside a dock, a hint in its own place (a keyed message: kept up, taken down)
local hinting = false
game.ontick(function(tics)
  local near = false
  for _, p in ipairs(game.players()) do
    if p.vehicle ~= nil and besidedock(p.tilex, p.tiley) then near = true end
  end
  if near ~= hinting then
    hinting = near
    game.message(near and "Press use to step onto the dock" or nil, nil, "boatdock")
  end
end)
