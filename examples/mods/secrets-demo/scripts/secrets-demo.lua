-- The Secrets Demo's script: a word as each secret's found, "Secret 2 of 5", and a hint as the
-- level starts. game.onsecret is called for pushwalls and secret triggers alike.
--
-- How many each level has: the game counts them as the map loads, but a script can't ask, so
-- they're listed here. Each player's temp1 counts what they've found on this level (an actor's
-- temp fields are kept in saved games, as Lua variables aren't).
local SECRETS = { MAP01 = 5 }

game.onlevel(function(reason, map)
  if reason == "new" or reason == "restarted" then
    for _, p in ipairs(game.players()) do p.temp1 = 0 end
  end
  if map == "MAP01" and reason == "new" then
    game.message("Five secrets on this floor. Try the walls that look out of place.")
  end
end)

game.onsecret(function(player, x, y)
  player.temp1 = player.temp1 + 1
  local total = SECRETS[game.map()]
  if total then
    game.message("Secret " .. player.temp1 .. " of " .. total .. " found!")
  else
    game.message("A secret!")
  end
end)
