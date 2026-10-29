-- The Stealth Demo's script: the alarm, and a word for each silent kill and at the tally.
--   game.onsight         the first enemy to notice you sounds the alarm: the lights go red and
--                        strobe (switch actions on light zone 1, through game.trigger) and the
--                        music changes (game.music)
--   game.onstealthkill   a kill made unseen with the knife
--   game.ontally         how stealthy the level was: unseen or not, and the silent kills
-- The zone's state is the game's own, so a saved game keeps the alarm going; temp1 on the
-- player marks it sounded, so it goes off once.

local ZONE = 1

game.onlevel(function(reason, map)
  if map ~= "MAP01" then return end
  if reason == "new" or reason == "restarted" then
    for _, p in ipairs(game.players()) do p.temp1 = 0 end
  end
  if reason == "new" then
    game.message("Find a uniform: the storeroom's coat rack. Knife the unwary from behind.")
  end
end)

game.onsight(function(monster, target)
  if not target or not target.isplayer or target.temp1 == 1 then return end
  target.temp1 = 1
  game.trigger(0, "A_SetZoneLight", ZONE, 255, 0, "#FF4040")
  game.trigger(0, "A_SetZoneEffect", ZONE, "strobe")
  game.music("SEARCHN")
  game.message("ALARM!")
end)

game.onstealthkill(function(player, victim)
  game.message("A silent kill.")
end)

game.ontally(function(map, result)
  if map ~= "MAP01" then return nil end
  local seen = result.undetected and "never seen" or "seen"
  return "You were " .. seen .. ", with " .. result.stealthkills .. " silent kills."
end)
