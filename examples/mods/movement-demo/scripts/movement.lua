-- Movement modifiers: floors that change how players and monsters move. Ice keeps them sliding,
-- mud slows them, conveyors and wind carry them, and pads kick them on their way.
--
-- It's all in the movement table below. A mod of your own can carry this script as it is and
-- fill in the table, or leave it here and fill it in from a script of its own (scripts run in
-- load order, so one loaded after this sees it):
--
--     movement.floors.MYICE = { slip = 0.97 }
--     movement.tags[7] = { carry = { 2, 0 } }       -- tagged tiles carry east, 2 tiles a second
--
-- A surface is a table of any of these:
--
--   slip   How much of the way it was going an actor keeps each tic (70 a second), from 0 (as
--          ever: it goes where it walks) up to, but not, 1. Ice is about 0.97: getting going
--          takes a moment, and a walking player let go glides about 2.5 tiles.
--   scale  How far its own steps take it: 0.5 is half as far (mud), 1.5 half as far again.
--   carry  { east, south } in tiles a second: the floor carries whatever's on it that way
--          (a conveyor, wind). Negative is west or north.
--   kick   On stepping onto the tile, a shove in tiles a second that dies away as it slides:
--          a number is the way the actor faces, { east, south } a way of its own.
--
-- A floor is the texture on it (mapdefs flats, or the map's default-floor); a tag is the one on
-- the map's tag plane, and a tagged tile's surface wins over its floor's.
--
-- Players move as they walk (stopped by walls, sliding along them). Monsters walk from tile to
-- tile, so the engine lets a living one go no more than 3/8 tile off its way (actor:move): on
-- ice it skids where it turns or stops, it doesn't glide, and mud doesn't slow it for long. A
-- dead one slides freely: shot on the ice, a body carries on the way it was going.
--
-- Nothing of this is saved: after a load, everyone starts out standing still.

movement = {
  -- Modifiers on, or off
  enabled = true,
  -- Who's moved: players on foot, players riding a vehicle, living monsters, and dead ones
  players = true,
  riders = true,
  monsters = true,
  bodies = true,
  -- Surfaces by floor texture
  floors = {},
  -- Surfaces by tag, which win over the floor's
  tags = {},
  -- How much of a kick is kept each tic off the ice (where the floor's slip is less)
  drag = 0.9,
  -- Running into a wall faster than this (tiles a second), movement.onbump is told
  bumpspeed = 4,
}

-- The surface under an actor: its tile's tag's, else its floor's; nil for none
function movement.surfacefor(actor)
  local tag = game.tag(actor.tilex, actor.tiley)
  if tag ~= 0 and movement.tags[tag] then return movement.tags[tag] end
  local floor = game.floor(actor.tilex, actor.tiley)
  if floor then
    return movement.floors[floor] or movement.floors[string.upper(floor)]
  end
end

-- Told when an actor runs into something faster than bumpspeed (tiles a second). Replace it to
-- play a sound, say: actor:playsound("world/hitwall")
function movement.onbump(actor, speed)
end

-- Per actor: where the script left it last tic, the way it's going (tiles a tic) and what's left
-- of a kick, and the tile it was on. A level starts it afresh.
local movers = {}

game.onlevel(function(reason, map)
  movers = {}
end)

-- k to the power tics, by multiplying (so every machine in a network game gets the same)
local function power(k, tics)
  local result = 1
  for _ = 1, tics do result = result * k end
  return result
end

-- The way an actor faces, as a step east and south
local function facing(actor)
  local radians = math.rad(actor.angle)
  return math.cos(radians), -math.sin(radians)
end

-- Starts an actor afresh where it is, going nowhere
local function still(mover, actor)
  mover.x, mover.y = actor.x, actor.y
  mover.vx, mover.vy, mover.kx, mover.ky = 0, 0, 0, 0
  mover.tilex, mover.tiley = actor.tilex, actor.tiley
end

-- A tic of one actor's movement
local function move(actor, mover, tics)
  local x, y = actor.x, actor.y
  local dx, dy = x - mover.x, y - mover.y      -- how far it went by itself
  if dx * dx + dy * dy > 1 then
    -- teleported, or put somewhere: it isn't going anywhere
    still(mover, actor)
    return
  end

  local surface = movement.surfacefor(actor) or {}
  local slip = math.min(math.max(surface.slip or 0, 0), 0.999)
  local scale = surface.scale or 1

  -- Its own steps, keeping some of the way it was going
  local keep = power(slip, tics)
  mover.vx = keep * mover.vx + (1 - keep) * dx / tics * scale
  mover.vy = keep * mover.vy + (1 - keep) * dy / tics * scale
  local ex, ey = mover.vx * tics - dx, mover.vy * tics - dy

  -- A kick, stepping onto its tile
  local kick = surface.kick
  if kick and (actor.tilex ~= mover.tilex or actor.tiley ~= mover.tiley) then
    local kx, ky
    if type(kick) == "table" then
      kx, ky = kick[1] or 0, kick[2] or 0
    else
      local fx, fy = facing(actor)
      kx, ky = fx * kick, fy * kick
    end
    mover.kx, mover.ky = mover.kx + kx / 70, mover.ky + ky / 70
  end
  ex, ey = ex + mover.kx * tics, ey + mover.ky * tics
  local fade = power(math.max(slip, movement.drag), tics)
  mover.kx, mover.ky = mover.kx * fade, mover.ky * fade

  -- What the floor carries
  local carry = surface.carry
  if carry then
    ex, ey = ex + (carry[1] or 0) * tics / 70, ey + (carry[2] or 0) * tics / 70
  end

  if math.abs(ex) > 1e-6 or math.abs(ey) > 1e-6 then
    local mx, my = actor:move(ex, ey)
    local blockedx, blockedy = math.abs(mx - ex) > 1e-4, math.abs(my - ey) > 1e-4
    if blockedx or blockedy then
      local speed = math.sqrt((blockedx and mover.vx * mover.vx or 0) + (blockedy and mover.vy * mover.vy or 0)) * 70
      -- running into something takes the way it was going out of it
      if blockedx then mover.vx, mover.kx = 0, 0 end
      if blockedy then mover.vy, mover.ky = 0, 0 end
      if speed > movement.bumpspeed then movement.onbump(actor, speed) end
    end
  end

  mover.x, mover.y = actor.x, actor.y
  mover.tilex, mover.tiley = actor.tilex, actor.tiley
end

-- Whether the script moves an actor now
local function moves(actor)
  if actor.isplayer then
    if not movement.players or actor.health <= 0 then return false end
    return movement.riders or not actor.vehicle
  end
  if actor.health > 0 then return movement.monsters end
  return movement.bodies
end

-- One actor's tic: tracked from now on (or no longer), and moved
local function tick(actor, tics, seen)
  local mover = movers[actor]
  if not moves(actor) then
    return                  -- left out: it starts afresh when it's in again
  end
  if not mover then
    mover = {}
    still(mover, actor)
  else
    move(actor, mover, tics)
  end
  seen[actor] = mover
end

game.ontick(function(tics)
  if not movement.enabled or tics <= 0 then return end
  local seen = {}
  for _, player in ipairs(game.players()) do
    tick(player, tics, seen)
  end
  if movement.monsters or movement.bodies then
    for _, actor in ipairs(game.actors()) do
      if not actor.isplayer and not actor:hasflag("PROJECTILE") then
        tick(actor, tics, seen)
      end
    end
  end
  movers = seen             -- what's gone from the level is forgotten
end)
