-- The Episode Demo's briefing: as the episode's first level starts afresh, the briefing
-- (texts/NFBRIEF.txt) is shown as an article before you play (game.showtext; the level's clock
-- doesn't count the reading). Not on a loaded game, a restart after dying, or in a demo.
game.onlevel(function(reason, map)
  if map == "MAP61" and reason == "new" then
    game.showtext("NFBRIEF")
  end
end)
