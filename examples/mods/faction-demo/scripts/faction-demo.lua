-- The Faction Demo's messages: what to do, as the level starts, and a prisoner freed.

game.onlevel(function(reason, map)
  if map == "MAP01" and reason == "new" then
    game.message("Your squad follows you. Free the prisoners next door: face one and press use.")
  end
end)

-- A prisoner's Use state runs this after A_SetFaction: it says so the first time (temp1 marks it)
function A_Freed(self)
  if self.temp1 == 0 then
    self.temp1 = 1
    game.message("A prisoner joins your squad!")
  end
end
