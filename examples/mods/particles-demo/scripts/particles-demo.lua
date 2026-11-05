-- The Particles Demo's script: particle effects from Lua (the scripting reference lists the
-- functions). Particles are only for show, so these change nothing in the game: they work in
-- any hook, game.onhud too, and a network game's machines needn't throw out the same ones.

-- Blood where an enemy's hit, sprayed away from whoever hit it, more for a harder hit. The
-- emitter's angle is turned by the way the victim faces, so the way away from the attacker is
-- given against that.
game.ondamage(function(victim, amount, attacker, type)
  if victim.isplayer then return end
  local away = 0
  if attacker then away = victim:angleto(attacker) + 180 - victim.angle end
  victim:particles("BloodSpray", { count = math.min(4 + amount, 30), angle = away })
end)

-- Gibs as one dies
game.onkill(function(victim, killer, type)
  victim:particles("Gibs")
end)

-- A sparkle where treasure's picked up (the item's still there as this is called)
local treasure = { Cross = true, Chalice = true, ChestOfJewels = true, Crown = true }
game.onpickup(function(player, item, amount)
  if treasure[item.name] then
    game.particles("Sparkle", item.x, item.y, 16)
  end
end)

-- The magic orb: an emitter of the script's own, made as each level starts (emitters a script
-- makes end with their level: a death or a loaded game starts it again) and moved round the
-- pillar with tag 2 each tic
local orb, cx, cy
game.onlevel(function(reason, map)
  orb = nil
  local pillar = game.taggedtiles(2)[1]
  if pillar then
    cx, cy = pillar.x + 0.5, pillar.y + 0.5
    orb = game.emitter("Orb", cx + 1.3, cy, 40)
  end
end)

game.ontick(function(tics)
  if not orb then return end
  local a = game.time() / 35
  orb:moveto(cx + 1.3 * math.cos(a), cy + 1.3 * math.sin(a), 36 + 10 * math.sin(a * 2))
end)

-- How many particles there are, top left of the view: try Options > Video > Particles
game.onhud(function()
  game.drawtext("Particles: " .. game.particlecount(), 4, 4)
end)
