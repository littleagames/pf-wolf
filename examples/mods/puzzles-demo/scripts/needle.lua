-- The timing needle: a needle sweeps to and fro along a track; press use (or fire) as it crosses
-- a green zone to bypass that part of the lock. Red shorts it out (a fail), and pressing on
-- bare track is a miss (too many is a fail too). Each bypass makes the needle quicker.
--
-- Open it with A_Puzzle("needle") on a switch, a mapdefs trigger or an actor's state, or try it
-- with the console's `puzzle needle` (a cheat). Words after the name change it:
--
--     A_Puzzle("needle", "hits=4", "misses=1", "speed=3")
--
-- This file is meant to be copied: a mod of your own can carry it as it is, or change it.
-- How it ended goes to game.onpuzzle (results.lua here shows a message for each).

local TRACK = 200        -- the track's length, in pixels
local TRACK_X, TRACK_Y = 20, 40
local TRACK_HEIGHT = 16
local SLOTS = 8          -- the track is split into this many slots, a zone to a slot at most
local GREEN_WIDTH, RED_WIDTH = 14, 10

-- Lays fresh zones on the track: greens to hit and reds to keep off, never two in a slot.
-- game.random keeps it the same on every machine (and in a demo).
local function lay_zones(s)
  s.zones = {}
  local used = {}
  local function place(kind, width)
    for _ = 1, 20 do
      local slot = game.random(0, SLOTS - 1)
      if not used[slot] then
        used[slot] = true
        local room = TRACK / SLOTS - width
        local from = slot * (TRACK / SLOTS) + game.random(0, room)
        table.insert(s.zones, { kind = kind, from = from, to = from + width })
        return
      end
    end
  end
  for _ = 1, s.greens do place("green", GREEN_WIDTH) end
  for _ = 1, s.reds do place("red", RED_WIDTH) end
end

local function zone_at(s, pos)
  for i, zone in ipairs(s.zones) do
    if pos >= zone.from and pos <= zone.to then return zone, i end
  end
  return nil
end

local function greens_left(s)
  for _, zone in ipairs(s.zones) do
    if zone.kind == "green" then return true end
  end
  return false
end

game.puzzle("needle", {
  title = "Bypass the lock",
  verb = "hacking",
  width = 240,
  height = 96,
  timelimit = 70 * 20,     -- 20 seconds (70 tics a second)

  start = function(pz, args)
    local s = pz.state
    s.need = args.hits or 3
    s.maxmisses = args.misses or 2
    s.speed = args.speed or 2       -- track pixels a tic
    s.greens = args.greens or 2
    s.reds = args.reds or 2
    s.hits, s.misses = 0, 0
    s.pos, s.dir = 0, 1
    s.flash, s.flashcolor = 0, "White"
    lay_zones(s)
  end,

  tick = function(pz, input)
    local s = pz.state

    -- The needle runs on, turning at the ends
    s.pos = s.pos + s.dir * s.speed * input.tics
    if s.pos >= TRACK then s.pos, s.dir = TRACK, -1 end
    if s.pos <= 0 then s.pos, s.dir = 0, 1 end
    if s.flash > 0 then s.flash = s.flash - input.tics end

    if not (input.pressed.use or input.pressed.fire) then return nil end

    local zone, index = zone_at(s, s.pos)
    if zone and zone.kind == "red" then
      pz:sound("world/hitwall")
      return "fail"
    elseif zone then
      table.remove(s.zones, index)
      s.hits = s.hits + 1
      if s.hits >= s.need then
        pz:sound("misc/k_pkup")
        return "success"
      end
      pz:sound("switches/normbutn")
      s.speed = s.speed + 0.5
      s.flash, s.flashcolor = 12, "#40FF40"
      if not greens_left(s) then lay_zones(s) end
    else
      s.misses = s.misses + 1
      pz:sound("misc/do_nothing")
      s.flash, s.flashcolor = 12, "#FF4040"
      if s.misses > s.maxmisses then return "fail" end
    end
    return nil
  end,

  draw = function(pz, width, height)
    local s = pz.state
    game.drawtext("USE as the needle crosses green.", 8, 4)
    game.drawtext("Red shorts the lock. Esc backs out.", 8, 14)

    -- The track and its zones
    game.drawrect(TRACK_X - 2, TRACK_Y - 2, TRACK + 4, TRACK_HEIGHT + 4, s.flash > 0 and s.flashcolor or "#000000")
    game.drawrect(TRACK_X, TRACK_Y, TRACK, TRACK_HEIGHT, "#282828")
    for _, zone in ipairs(s.zones) do
      local color = zone.kind == "green" and "#00B000" or "#C00000"
      game.drawrect(TRACK_X + zone.from, TRACK_Y, zone.to - zone.from, TRACK_HEIGHT, color)
    end

    -- The needle
    local x = TRACK_X + s.pos
    game.drawline(x, TRACK_Y - 5, x, TRACK_Y + TRACK_HEIGHT + 4, "White", 2)

    -- How it's going
    game.drawtext(string.format("Bypassed %d of %d", s.hits, s.need), 8, 66)
    game.drawtext(string.format("Misses %d of %d", s.misses, s.maxmisses), 150, 66)
    if pz.timeleft then
      local share = pz.timeleft / (70 * 20)
      game.drawrect(8, 82, width - 16, 4, "#000000")
      game.drawrect(8, 82, math.floor((width - 16) * share), 4, share > 0.25 and "#C0C000" or "#FF4040")
    end
  end,
})
