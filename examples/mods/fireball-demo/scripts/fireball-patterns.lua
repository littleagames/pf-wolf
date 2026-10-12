-- Fireball patterns for the traps in actordefs/fireball-traps.yaml. Each A_ function here is an
-- action the actordefs states call like a built-in one: A_FireRing("Fireball", 8, 22).
-- `self` is the actor running it; the rest are the state's arguments.

-- A_FireRing(projectile, count[, turn]): count projectiles evenly spaced all the way round.
-- With a turn (degrees), each ring starts that much further round than the last: the ring's
-- start is kept in temp1, which saved games keep (Lua variables aren't saved).
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
