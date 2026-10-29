-- The Music Demo's script: music that follows the fight, and a hum by the generators.
--   game.onmusic   the level's song as it starts (or starts again after the menu): the calm one,
--                  or the fight one if a fight's on; called on this machine alone
--   game.ontick    each tic, whether anyone's after you: the fight song as soon as someone is,
--                  the calm one again once nobody has been for six seconds (game.music); and in
--                  the generator room (its tiles have tag 1), its hum (game.ambient)
-- The songs are the mod's music/DMCALM.wav and music/DMFIGHT.wav: a file in a mod's music/
-- (OGG, MP3 or WAV) is a song of that name, as Wolf3D's own are.

local MAP = "MAP01"
local CALM, FIGHT = "DMCALM", "DMFIGHT"
local CALM_AFTER = 6 * 70          -- tics with nobody after you before the calm song comes back
local HUM_TAG = 1

-- Only music, which a saved game needn't keep: the song comes back right from these
local fighting = false
local quietsince = 0

local CHASING = { Chase = true, Attack = true, Pain = true, Pain1 = true }

local function hunted()
  for _, a in ipairs(game.actors()) do
    if not a.isplayer and a.health > 0 and CHASING[a.state] then return true end
  end
  return false
end

game.onmusic(function(map, song)
  if map == MAP then return fighting and FIGHT or CALM end
end)

game.onlevel(function(reason, map)
  fighting = false
  quietsince = game.time()
end)

game.ontick(function(tics)
  if game.map() ~= MAP then return end
  if hunted() then
    quietsince = game.time()
    if not fighting then
      fighting = true
      game.music(FIGHT)
    end
  elseif fighting and game.time() - quietsince > CALM_AFTER then
    fighting = false
    game.music(CALM)
  end

  local player = game.players()[1]
  game.ambient(player and game.tag(player.tilex, player.tiley) == HUM_TAG and "musicdemo/hum" or nil)
end)
