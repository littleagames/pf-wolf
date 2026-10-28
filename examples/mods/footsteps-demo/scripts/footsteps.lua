-- Footsteps: each player's steps make a sound as they walk, by the floor under them.
--
-- It's all in the footsteps table below. A mod of your own can carry this script as it is and
-- change the table, or leave it here and change the table from a script of its own (scripts
-- run in load order, so one loaded after this sees it):
--
--     footsteps.floors.MYGRATE = "footsteps/metal"
--     footsteps.stride = 3
--
-- The sounds are sound-seq names (sounds/sound-seq.yaml), so a step can be a random pick of
-- several (random:), and a floor is the texture on it: a mapdefs flats entry, or the map's
-- default-floor. Where the floor color shows (no flat), a step makes the default sound.

footsteps = {
  -- Steps on, or off
  enabled = true,
  -- The sound of a step on a floor the list below hasn't got, and where no flat is
  sound = "footsteps/default",
  -- Floor textures, and the sound of a step on each
  floors = {
    FSDIRT = "footsteps/dirt",
    FSGRATE = "footsteps/metal",
  },
  -- How far a player goes between steps, in tiles (Wolf3D walks about 5.6 tiles a second and
  -- runs about 11)
  stride = 2.5,
  -- The fewest tics between two steps (70 a second), so running doesn't rattle
  mintics = 12,
}

-- The sound of a step on a floor (nil where no flat is)
function footsteps.soundfor(floor)
  if floor then
    local sound = footsteps.floors[floor] or footsteps.floors[string.upper(floor)]
    if sound then return sound end
  end
  return footsteps.sound
end

-- A step: the sound, from where the player is. Replace it to do more with each step.
function footsteps.play(player, sound, floor)
  player:playsound(sound)
end

-- Per player: where they were last tick, how far they've come since their last step, and the
-- tics since it. Only sounds come of it, so it needn't be saved; a level starts it afresh.
local walkers = {}

game.onlevel(function(reason, map)
  walkers = {}
end)

game.ontick(function(tics)
  if not footsteps.enabled then return end
  for _, player in ipairs(game.players()) do
    local x, y = player.x, player.y
    local walker = walkers[player]
    if not walker then
      walkers[player] = { x = x, y = y, distance = footsteps.stride / 2, since = footsteps.mintics }
    else
      local dx, dy = x - walker.x, y - walker.y
      local moved = math.sqrt(dx * dx + dy * dy)
      walker.x, walker.y = x, y
      walker.since = walker.since + tics

      if moved > 1 or player.health <= 0 or player.vehicle then
        -- teleported, dead or riding: no step for that
        walker.distance = 0
      elseif moved == 0 then
        -- standing: setting off again, the first step comes half a stride on
        walker.distance = math.max(walker.distance, footsteps.stride / 2)
      else
        walker.distance = walker.distance + moved
        if walker.distance >= footsteps.stride and walker.since >= footsteps.mintics then
          walker.distance = 0
          walker.since = 0
          local floor = game.floor(player.tilex, player.tiley)
          footsteps.play(player, footsteps.soundfor(floor), floor)
        end
      end
    end
  end
end)
