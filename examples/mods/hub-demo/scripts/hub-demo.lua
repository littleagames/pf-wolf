-- The hub's words for the player: what the hub is for as a new game starts on it, and where each
-- way out goes as they step towards it. Nothing here changes the game, so nothing needs saving.

-- The hub's floor tags (the map's tag plane): each way out's floor, and what it says there
local SIGNS = {
  [1] = "$HUB_TOCELLBLOCK",   -- the west elevator
  [2] = "$HUB_TOARMORY",      -- the east elevator
  [3] = "$HUB_TOCHAPEL",      -- the teleport pad's room
}

-- The sign last shown, so walking about one way out's floor says it once (Lua variables aren't
-- saved: after a load it may say it again, which does no harm)
local shown = nil

game.onlevel(function(reason, map)
  shown = nil
  if map == "MAP01" and reason == "new" then
    game.message("$HUB_WELCOME")
  end
end)

game.onenter(function(actor, x, y)
  if not actor.isplayer or game.map() ~= "MAP01" then
    return
  end
  local sign = SIGNS[game.tag(x, y)]
  if sign ~= shown then
    shown = sign
    if sign then
      game.message(sign)
    end
  end
end)
