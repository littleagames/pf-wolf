-- The Medic Demo's messages: what to do, as the level starts, and the first time each kind of
-- medic brings someone back (its Heal state runs A_MedicHint as it starts).

local told = {}

game.onlevel(function(reason, map)
  told = {}
  if map == "MAP01" and reason == "new" then
    game.message("Medics bring their own side back. Yours follows you: keep your squad alive.")
  end
end)

function A_MedicHint(self)
  if told[self.name] then
    return
  end
  told[self.name] = true
  if self.name == "SquadMedic" then
    game.message("Your medic is getting a fallen squadmate back on their feet.")
  elseif self.name == "Surgeon" then
    game.message("The field surgeon brings back up to three at once!")
  else
    game.message("A medic is reviving a guard! Take the medics out first.")
  end
end
