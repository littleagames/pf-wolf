-- A lockpick: move the pick round the lock (left and right), then hold use to turn the lock. The
-- nearer the pick is to the sweet spot, the further it turns; all the way round and it's open.
-- Forcing a lock that won't turn strains the pick, and it snaps: out of picks is a fail.
--
--     A_Puzzle("lockpick", "picks=3", "spot=12")
--
-- spot: how wide the sweet spot is, in degrees. This file is meant to be copied: carry it in
-- your own mod and change it as you like.

local CX, CY, R = 70, 70, 40
local STRAIN_LIMIT = 50

-- How far the lock turns with the pick where it is: all the way (90) in the sweet spot, less
-- the further off it is, none 60 degrees off
local function allowed(s)
  local off = math.max(0, math.abs(s.pick - s.spot) - s.width)
  return 90 * math.max(0, 1 - off / 60)
end

game.puzzle("lockpick", {
  title = "Pick the lock",
  verb = "picking a lock",
  width = 220,
  height = 128,

  start = function(pz, args)
    local s = pz.state
    s.width = args.spot or 12
    s.spot = game.random(10, 170)
    s.pick = 90
    s.turn = 0
    s.strain = 0
    s.picks = args.picks or 3
  end,

  tick = function(pz, input)
    local s = pz.state
    local forcing = input.held.use or input.held.fire

    if not forcing then
      if input.held.left then s.pick = math.max(0, s.pick - 1.5 * input.tics) end
      if input.held.right then s.pick = math.min(180, s.pick + 1.5 * input.tics) end
      s.turn = math.max(0, s.turn - 3 * input.tics)
      s.strain = math.max(0, s.strain - 2 * input.tics)
      return nil
    end

    local most = allowed(s)
    s.turn = math.min(most, s.turn + 2 * input.tics)
    if s.turn >= 90 then
      pz:sound("misc/k_pkup")
      return "success"
    end
    if s.turn >= most then
      s.strain = s.strain + input.tics
      if s.strain >= STRAIN_LIMIT then
        s.picks = s.picks - 1
        s.strain, s.turn = 0, 0
        pz:sound("world/hitwall")
        if s.picks <= 0 then return "fail" end
      end
    end
    return nil
  end,

  draw = function(pz, width, height)
    local s = pz.state

    -- The lock: a ring, its keyhole turned as far as it's gone
    for i = 0, 31 do
      local a1, a2 = 2 * math.pi * i / 32, 2 * math.pi * (i + 1) / 32
      game.drawline(CX + R * math.cos(a1), CY + R * math.sin(a1), CX + R * math.cos(a2), CY + R * math.sin(a2), "#B08040", 3)
    end
    local a = math.rad(s.turn)
    game.drawline(CX, CY, CX + 14 * math.sin(a), CY - 14 * math.cos(a), "#202020", 4)

    -- The pick, from the bottom of the ring, at its angle (0 left, 180 right)
    local p = math.rad(180 + s.pick)
    game.drawline(CX, CY, CX + (R + 22) * math.cos(p), CY + (R + 22) * math.sin(p), "#E0E0E0", 2)

    -- How far it's turned, how strained the pick is, picks left
    game.drawtext("Turned", 130, 20)
    game.drawrect(130, 32, 80, 6, "#303030")
    game.drawrect(130, 32, math.floor(80 * s.turn / 90), 6, "#40C040")
    game.drawtext("Strain", 130, 46)
    game.drawrect(130, 58, 80, 6, "#303030")
    game.drawrect(130, 58, math.floor(80 * s.strain / STRAIN_LIMIT), 6, "#FF4040")
    game.drawtext(string.format("Picks left: %d", s.picks), 130, 72)
    game.drawtext("Left/right: the pick", 120, 96)
    game.drawtext("Hold USE: turn", 120, 108)
  end,
})
