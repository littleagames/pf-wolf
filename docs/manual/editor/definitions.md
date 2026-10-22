---
title: Definitions
order: 9
---

# Definitions

**Resources → Definitions…** (F11) edits the definition files the game is made of, as fields or
as YAML: game-info, the game packs, actor classes, map definitions, animations, cropped pictures
(graphicdefs) and factions. pfwolf.pk3's own are read-only: a change is saved into a mod, as just
what it changes, and laid over the game's as the game lays mods over it ([How files
combine](../combining-files)).

**Save into** picks the mod changes go to, one of those loaded or *Other mod…*. What's saved is
in the levels, the palette and the rest of the editor once the game's loaded again (*Load the game
again*).

## The tabs

Each tab lists its entries, found by name: game-info's sections, the actor classes, the mapdefs
walls, doors, things and the rest. Picking one shows its fields, each with what it does:

- an actor class shows what it has *From its parents*, as well as its own fields;
  **⟲** empties a field, so the game uses its default, or what's there before the mod;
- **…** picks from the names the game has (a class, a sound, a sprite);
- a list's items can be added (**+**), moved (**↑** **↓**) and taken out (**✕**);
- *Where it comes from* lists the files the entry is made of, and *Edit YAML* shows it as text.

**New…** makes an entry: a class takes the one picked as its parent, and a map definition asks
for its table. **Delete** takes an entry out once it's saved: from a mod it's written `!remove`.
*Take out the mod's change* leaves it as the game, and the mods before, make it. **Save**
(Ctrl+S) writes into the mod just what's changed; *Revert* goes back to it as the files have it.

**This level's own** shows the open level's own walls, doors, flats, triggers and extra things (a
UWMF level's), read-only.

## YAML files

The **YAML files** tab has every definition file as text, the mods' and, with *pfwolf.pk3's
files*, the game's, found by path, by mod or by what it is. What's wrong with one is listed by line
as it's typed, read as the game reads it, and *Only problems* lists just the files with some.

*Copy to mod* starts a mod's file from one of pfwolf.pk3's: *as an empty file* (give it only the
keys you change, and the rest stays pfwolf.pk3's) or *as a whole copy* (to trim down to what you
change). **Save** (Ctrl+S) writes it back into its mod, and *Delete…* takes a mod's file out.

The [definitions reference](../../reference/) has every file and field.
