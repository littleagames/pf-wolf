-- The Boat Demo's script: a word as the level starts. The rest (the dock hint, the gun nest's,
-- keeping everyone out of the river) is the actors' YAML.

game.onlevel(function(reason, map)
  if map == "MAP01" and reason == "new" then
    game.message("Take the boat downriver: face it from the pier and press use.")
  end
end)
