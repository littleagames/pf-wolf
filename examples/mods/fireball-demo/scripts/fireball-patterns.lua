-- Fireball patterns for the traps in actordefs/fireball-traps.yaml. Each A_ function here is an
-- action the actordefs states call like a built-in one: A_FireRing("Fireball", 8, 22).
-- `self` is the actor running it; the rest are the state's arguments.
--
-- Anything a pattern has to remember from one call to the next (how far round a spiral has
-- turned, which way a sweep is going) is kept in the actor's temp1..temp3, which saved games
-- keep: Lua variables aren't saved, and every actor has its own temps.

-- Hooks: functions the game calls as each level starts, and each tic.

-- A word for the player as they come into a level afresh (not on coming back to it, dying or
-- loading). It shows with in-game messages on (Options, or msg_enabled 1 in the console).
game.onlevel(function(reason, map)
  if reason == "new" then
    game.message("Mind the eagles: they spit fire.")
  end
end)

-- Every 12 seconds of the level's time, each skull pile and bone pile with a player within 6
-- tiles of it erupts: a ring of 12 fireballs at once, besides its usual pattern. Whether it's
-- time is worked out from game.time() alone, which saved games keep, so nothing has to be
-- remembered in Lua. game.time() is the level's time as this tic began; tics are about to pass.
local ERUPTION_TICS = 12 * 70

game.ontick(function(tics)
  local now = game.time()
  if math.floor((now + tics) / ERUPTION_TICS) == math.floor(now / ERUPTION_TICS) then
    return
  end
  for _, class in ipairs({ "FireballRing", "FireballSpiral" }) do
    for _, pile in ipairs(game.actors(class)) do
      local player = pile:nearestplayer()
      if player and pile:distanceto(player) <= 6 then
        A_FireRing(pile, "Fireball", 12)    -- the action below, called as a Lua function
      end
    end
  end
end)

-- A_FireRing(projectile, count[, turn]): count projectiles evenly spaced all the way round.
-- With a turn (degrees), each ring starts that much further round than the last (temp1).
function A_FireRing(self, projectile, count, turn)
  count = count or 8
  local step = 360 / count
  for i = 0, count - 1 do
    self:spawnprojectile(projectile, self.temp1 + i * step, "absolute")
  end
  if turn then
    self.temp1 = (self.temp1 + turn) % 360
  end
end

-- A_FireSpiral(projectile, step[, arms]): one projectile per arm (arms evenly spaced round),
-- then turns step degrees for the next call (temp1). Called every few tics, the arms wind out
-- into a spiral.
function A_FireSpiral(self, projectile, step, arms)
  arms = arms or 1
  for i = 0, arms - 1 do
    self:spawnprojectile(projectile, self.temp1 + i * 360 / arms, "absolute")
  end
  self.temp1 = (self.temp1 + (step or 15)) % 360
end

-- A_FireBurst(projectile, count, spread): count projectiles fanned evenly across spread
-- degrees, centred on the nearest player
function A_FireBurst(self, projectile, count, spread)
  count = count or 3
  spread = spread or 30
  local target = self:nearestplayer()
  if not target then
    return
  end
  local centre = self:angleto(target)
  for i = 0, count - 1 do
    local offset = 0
    if count > 1 then
      offset = (i / (count - 1) - 0.5) * spread
    end
    self:spawnprojectile(projectile, centre + offset, "absolute")
  end
end

-- A_Sweep(projectile, arc, step): one projectile, turned from the way the actor faces by its
-- sweep so far (temp1), which then moves step degrees on, turning back at either edge of the
-- arc (temp3 is the way it's going: 1 left, -1 right; 0 to start with, taken as left)
function A_Sweep(self, projectile, arc, step)
  arc = arc or 90
  step = step or 15
  local half = arc / 2
  local way = self.temp3
  if way == 0 then
    way = 1
  end
  self:spawnprojectile(projectile, self.temp1, "relative")
  local nextoffset = self.temp1 + way * step
  if nextoffset > half or nextoffset < -half then
    way = -way
    nextoffset = self.temp1 + way * step
  end
  self.temp1 = nextoffset
  self.temp3 = way
end

-- A_Home(turn, frames): a projectile's action, steering it toward the nearest player it can
-- see by up to turn degrees each time it's run, for its first frames runs (counted in temp2;
-- after that it flies straight on, so it can be outrun)
function A_Home(self, turn, frames)
  if self.temp2 >= (frames or 30) then
    return
  end
  self.temp2 = self.temp2 + 1
  local target = self:nearestplayer()
  if not target or not self:cansee(target) then
    return
  end
  -- how far to turn, the short way round: -180 to 180
  local wanted = (self:angleto(target) - self.angle + 540) % 360 - 180
  turn = turn or 10
  if wanted > turn then
    wanted = turn
  elseif wanted < -turn then
    wanted = -turn
  end
  self.angle = self.angle + wanted
end
