-- What the puzzles' ends do here: a message for each. A mod's own script would open a door,
-- sound an alarm or hand out a prize, using the tile and tag the puzzle was started from:
--
--     game.onpuzzle(function(player, name, result, x, y, tag)
--       if result == "success" and tag > 0 then game.trigger(tag, 'A_OpenDoor("hold")') end
--     end)
--
-- The messages are keyed ("puzzle"), so they show even with the messages setting off.

local said = {
  success = "Lock bypassed!",
  fail = "The lock shorted out.",
  cancel = "You leave the lock be.",
}

game.onpuzzle(function(player, name, result, x, y, tag)
  game.message(said[result] or ("Puzzle " .. name .. ": " .. result), nil, "puzzle")
end)
