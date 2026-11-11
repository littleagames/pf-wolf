-- The Hazards Demo's script: the hazard suit, and a word on each hazard as you step onto it.
--   game.onpickup   the suit taken: until when it lasts, in seconds of the level's clock, kept
--                   on the player's temp1 (so a saved game keeps it)
--   game.ondamage   a floor's hurt (it has no attacker) is nothing while the suit lasts
--   game.onenter    the first step onto each kind of hazard says what it is
--   game.onhud      the suit's seconds left, while it lasts
--   game.onlevel    the live water's tiles, found once a level starts...
--   game.ontick     ...throwing up sparks near a player while the current's on (particles.yaml,
--                   Midnight Assault's own; game.particles)
-- game.floor(x, y) is the texture a tile's floor shows now: an animation by its own name.

local SUIT_SECONDS = 30
local HAZARDS = {
  HZLAVA = "Lava! Keep to the stone path.",
  HZACID = "Acid. It eats at you slowly.",
  HZSHOCK = "The water's live! Find the switch.",
}

local function now() return math.floor(game.time() / 70) end

game.onpickup(function(player, item, amount)
  if item.name == "HazardSuit" then player.temp1 = now() + SUIT_SECONDS end
end)

game.ondamage(function(victim, amount, attacker, type)
  if victim.isplayer and attacker == nil and HAZARDS[game.floor(victim.tilex, victim.tiley) or ""]
      and victim.temp1 > now() then
    return 0
  end
end)

game.onenter(function(actor, x, y)
  if not actor.isplayer then return end
  local floor = game.floor(x, y)
  local warning = floor and HAZARDS[floor]
  if warning and actor.temp1 <= now() then game.message(warning) end
end)

game.onhud(function()
  local player = game.players()[1]
  if not player then return end
  local left = player.temp1 - now()
  if left > 0 then
    local width = game.viewsize()
    game.drawtext("SUIT " .. left, width - 50, 6, "SmallFont", left <= 5 and "Maroon" or "Lime")
  end
end)

-- Sparks off the live water, as Midnight Assault's own (its scripts/electricwater.lua). Each tic
-- every water tile near a player may throw up a spark; once the switch turns the current off
-- (HZWATER) they stop, and they're back when it's on again. Particles are only for show, so
-- this keeps its own random numbers rather than taking the game's (game.random), which would
-- change how the level plays.
local NEAR = 12             -- tiles from a player that spark
local CHANCE = 1 / 60       -- a tile's chance of a spark each tic
local water = {}            -- the tiles with water, live or not: { x, y }

local seed = 12345
local function rnd()        -- 0 to just under 1 (Park-Miller)
  seed = (seed * 16807) % 2147483647
  return seed / 2147483647
end

game.onlevel(function()
  water = {}
  for y = 0, 255 do
    if not pcall(game.floor, 0, y) then break end
    for x = 0, 255 do
      local ok, floor = pcall(game.floor, x, y)
      if not ok then break end
      if floor == "HZSHOCK" or floor == "HZWATER" then water[#water + 1] = { x, y } end
    end
  end
end)

game.ontick(function(tics)
  if #water == 0 then return end
  local players = game.players()
  if #players == 0 then return end
  local chance = CHANCE * tics
  for _, tile in ipairs(water) do
    if rnd() < chance then
      local x, y = tile[1], tile[2]
      for _, player in ipairs(players) do
        if math.abs(player.tilex - x) <= NEAR and math.abs(player.tiley - y) <= NEAR then
          if game.floor(x, y) == "HZSHOCK" then
            local px, py = x + 0.15 + rnd() * 0.7, y + 0.15 + rnd() * 0.7
            game.particles("HazardWaterZap", px, py)
            game.particles("HazardWaterFlash", px, py)
          end
          break
        end
      end
    end
  end
end)
