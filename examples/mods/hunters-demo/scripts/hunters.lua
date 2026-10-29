-- The Hunters Demo's script: hunters find their way.
--
-- An enemy chasing someone picks its next way at each tile's middle, by Wolf3D's rules: toward
-- you if it can, else round to the side, which in a maze sends it into dead ends. game.onchasedir
-- is asked first, as it chooses: return a way ("north", "northeast"...) and it goes that way if it
-- can; return nil and the game chooses as ever. monster:pathto(target) finds the way by A*: round
-- walls, through the doors it can open, along the shortest path it can walk.
--
-- Only the Hunter and Tracker classes are given the path; Wolf3D's own guards and dogs are left
-- to their usual ways, for you to compare. Mode is "chase", "dodge", "run", "wander" or
-- "follow"; the hunters path to you whenever they're after you (chasing or closing in).

local HUNTERS = { Hunter = true, Tracker = true }

game.onchasedir(function(monster, target, mode)
  if not HUNTERS[monster.name] or not target then return nil end
  if mode ~= "chase" and mode ~= "dodge" then return nil end
  return monster:pathto(target)
end)

game.onlevel(function(reason, map)
  if map == "MAP01" and reason == "new" then
    game.message("Fire a shot. The orange ones will find you.")
  end
end)
