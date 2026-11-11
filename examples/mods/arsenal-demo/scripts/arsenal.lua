-- The Arsenal Demo's own action (the engine hasn't got it: Midnight Assault defines it in its
-- Lua too).

-- A_JumpIfOnFloor("State"): goes to the state when its height (z) is down at its `zfloor` (0
-- when unset). The thrown grenade's Death runs it, for the lower blast of one lying on the floor.
function A_JumpIfOnFloor(self, state)
  if state == nil then
    return
  end
  local floor = self:property("zfloor") or 0
  if self.z <= floor + 0.5 then
    self:jump(state)
  end
end
