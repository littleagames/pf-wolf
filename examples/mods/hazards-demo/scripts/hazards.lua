-- The Hazards Demo's script: the hazard suit, and a word on each hazard as you step onto it.
--   game.onpickup   the suit taken: until when it lasts, in seconds of the level's clock, kept
--                   on the player's temp1 (so a saved game keeps it)
--   game.ondamage   a floor's hurt (it has no attacker) is nothing while the suit lasts
--   game.onenter    the first step onto each kind of hazard says what it is
--   game.onhud      the suit's seconds left, while it lasts
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
