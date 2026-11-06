-- Notes on the walls: using a wall with tag 50 shows texts/PZNOTE.txt (the keypad's code), and
-- the game does nothing else with the press. game.showtext does nothing in a demo or with
-- others, so there it just says what the note says.

local NOTE_TAG = 50

game.onuse(function(player, x, y, dir)
  if game.tag(x, y) ~= NOTE_TAG then return nil end
  game.showtext("PZNOTE")
  game.message("A note: \"Keypad code 7319. Don't tell Hans.\"", nil, "puzzle")
  return true
end)
