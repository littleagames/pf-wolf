-- Power routing: there's power for one system only. Up and down pick it, use sends the power
-- there. Not a pass or a fail: it ends with the system's name as its result, and a script's
-- game.onpuzzle decides what that does (results.lua here). It can't be backed out of.
--
--     A_Puzzle("power", "vault", "medbay", "armory")
--
-- The words after the name are the systems (those three when there are none). This file is
-- meant to be copied: carry it in your own mod and change it as you like.

game.puzzle("power", {
  title = "Route the generator",
  verb = "routing power",
  width = 200,
  height = 120,
  cancel = "never",

  start = function(pz, args)
    local s = pz.state
    s.systems = {}
    for i = 1, 6 do
      if args[i] then s.systems[i] = tostring(args[i]) end
    end
    if #s.systems == 0 then s.systems = { "vault", "medbay", "armory" } end
    s.cursor = 1
  end,

  tick = function(pz, input)
    local s = pz.state
    if input.pressed.up then s.cursor = math.max(1, s.cursor - 1) end
    if input.pressed.down then s.cursor = math.min(#s.systems, s.cursor + 1) end
    if input.pressed.use or input.pressed.fire then
      pz:sound("switches/elevbutn")
      return s.systems[s.cursor]
    end
    return nil
  end,

  draw = function(pz, width, height)
    local s = pz.state
    game.drawtext("Power for one system only.", 8, 4)
    for i, name in ipairs(s.systems) do
      local y = 22 + (i - 1) * 20
      local here = i == s.cursor
      game.drawrect(16, y, 120, 14, here and "#405020" or "#202020")
      game.drawframe(16, y, 120, 14, here and "#FFFF40" or "#606060")
      game.drawtext(string.upper(name), 24, y + 4, nil, here and "White" or "#A0A0A0")
      game.drawrect(144, y + 3, 30, 8, here and "#FFD040" or "#302810")
    end
    game.drawtext("Up/down picks, USE routes.", 8, height - 14)
  end,
})
