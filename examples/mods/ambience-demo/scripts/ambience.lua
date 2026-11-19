-- The Ambience Demo's script. Each room's own loop is its zone's (game-info.yaml); this adds:
--   birds        now and then, from one of the courtyard's trees (their tiles have tag 11):
--                game.playsound(sound, x, y), heard from that side and fainter far off
--   thunder      now and then while you're in the courtyard: game.playsound(sound), unplaced
--   generators   A_GeneratorUse, their Use action: off, all of them, or on again (their drone
--                is their actordefs loopsound, and stops with their Off state). While they're
--                off the generator room is silent: game.ambient("none", tics) holds the
--                ambience over the zone's buzz until game.ambient(nil, tics) gives it back
--   the radio    A_RadioUse: the next station, or off
--   messages     a word about each room the first time you walk into it
-- Rooms are told apart by the tag plane (plane 4): each room's tiles have its own tag.
-- The birds and thunder pick their times with a little generator of the script's own: only for
-- show, so the game's numbers (game.random) are left as they are.

local MAP = "MAP01"
local COURTYARD, SEWER, GENERATORS, GUARDS, RADIO, ELEVATOR = 1, 2, 3, 4, 5, 6
local TREES = 11
local QUIET_FADE = 105          -- tics the generator room's hum takes to die away, or come back

local ROOMS = {
  [COURTYARD] = "Courtyard: wind and rain, birds in the trees, thunder far off.",
  [SEWER] = "Sewer: drips all round you, and water pouring out at the far end.",
  [GENERATORS] = "Generators: walk round them, Use one, or hear them from the hall, door shut.",
  [GUARDS] = "Guard post: tense while you're unseen. Get spotted: the alarm.",
  [RADIO] = "Radio room: no ambience, only the radio. Use it to change station.",
  [ELEVATOR] = "No zone here: the hall's hush carries on. The elevator's ahead.",
}

-- Only for show, so a saved game needn't keep them: they start again with the level
local seen = {}
local trees = nil
local nextbird, nextthunder = 0, 0
local seed = 1

local function rnd(n)           -- 0 to n - 1 (Park and Miller's generator)
  seed = (seed * 16807) % 2147483647
  return seed % n
end

game.onlevel(function(reason, map)
  if map ~= MAP then return end
  seen = {}
  trees = nil
  nextbird, nextthunder = 0, 0
  seed = 12345 + game.time()
  if reason == "new" then
    game.message("Ambience Demo: every room sounds different. Walk in and listen.")
  end
end)

-- A generator's Use: every generator goes off, or on again (temp1 says which they are)
function A_GeneratorUse(self)
  local off = self.temp1 == 0
  for _, g in ipairs(game.actors("AmbienceGenerator")) do
    g.temp1 = off and 1 or 0
    g:jump(off and "Off" or "PowerOn")
  end
end

-- The radio's Use: station one, station two, off, and round again
local STATIONS = { "Spawn", "StationTwo", "Off" }
function A_RadioUse(self)
  self.temp1 = (self.temp1 + 1) % #STATIONS
  self:jump(STATIONS[self.temp1 + 1])
end

local function generatorsoff()
  local g = game.actors("AmbienceGenerator")[1]
  return g ~= nil and g.temp1 == 1
end

game.ontick(function(tics)
  if game.map() ~= MAP then return end
  local player = game.players()[1]
  if not player then return end
  local room = game.tag(player.tilex, player.tiley)
  local now = game.time()

  if ROOMS[room] and not seen[room] then
    seen[room] = true
    game.message(ROOMS[room])
  end

  -- The generator room with its generators off: silence, over the zone's hum
  if room == GENERATORS and generatorsoff() then
    game.ambient("none", QUIET_FADE)
  else
    game.ambient(nil, QUIET_FADE)
  end

  if not trees then trees = game.taggedtiles(TREES) end
  if #trees > 0 and now >= nextbird then
    local tree = trees[rnd(#trees) + 1]
    game.playsound("ambience/bird", tree.x + 0.5, tree.y + 0.5)
    nextbird = now + 105 + rnd(280)
  end

  if room == COURTYARD and now >= nextthunder then
    if nextthunder > 0 then game.playsound("ambience/thunder") end
    nextthunder = now + 700 + rnd(1050)
  end
end)
