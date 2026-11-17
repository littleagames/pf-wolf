---
title: Menus
order: 9
---

# Menus

**Resources → Menus…** (Ctrl+Shift+M) lists every menu the game has (its `menudefs`), each drawn
exactly as the game draws it: with the game's own drawing code, its pictures, fonts and colors.
What each menudefs key does is in the [menudefs reference](../../reference/menudefs).

A menu is listed by the name the game opens it by (`main-menu`, `options`, a mod's own). The one
listed is the one the running game uses: the pack's own (`menudefs/blake/main-menu.yaml`, marked
*blake's own*) in place of the shared one, with any mod's file of the same name laid over it
(marked *changed by a mod*; a mod's new menu is *a mod's own*). A ⚠ marks a menu with a problem.
Menus nothing in the game opens are faded: Blake Stone has Wolf3D's multiplayer menus, say, but
nothing in it gets to them. The list can be narrowed by name, file or mod, and to the menus the
game gets to, the pack's own, the ones mods change or add, or the ones with problems.

## The picture

Picking a menu shows what it is, how its rows are laid out, and the menu itself. Things the game's
code puts on a menu that menudefs don't say are made up:

- an **items-source** menu's rows are the game's own episodes, skills and player classes, or sample
  mods and network games; **Rows** makes up more or fewer of them, to see what `fit-items` does;
- toggles get every other **checkbox on** (or all off), sliders and choices a sample value;
- **In a game** shows the main menu as it is during a game (Back to Game, Save game open);
- the New Game menus' pictures (the highlighted skill's face, an episode's, a class's) go where the
  game puts them, and a `stats-panel` shows a made-up best;
- the screens the game draws over a menu's components (saved games, Customize controls, the lobby)
  say so under the picture.

**Wide** draws it on a 16:10 screen, with the backdrop and stripe as the game draws them round the
320x200 middle; **Zoom** makes it 2, 3 or 4 times bigger. Blake Stone's highlight bar **flashes**
as the game's does.

The cursor moves as the game's does: click the picture, then Up and Down (skipping greyed-out
rows, round from the end), Home and End, or an item's letter. Clicking a part of the picture picks
it (outlined, and named under the picture): a row also takes the cursor there. Double-click an
item that opens a menu to go to that menu, or a picture to see it in the art window (F6).

## The tabs

- **Fields**: the menu's keys (where the rows go, how far apart, its font, cursor, music,
  items-source, fit-items...), then its **components** and **menu items**, each a row of fields with
  what it does. **+** adds a component or row, **↑ ↓** move one, **✕** takes one out. A
  component's params are the ones its type reads (a Window's place and size, a Graphic's picture,
  a Label's text and its shadow, outline, gradient and glow), and change when its type does. Item
  types, actions and items-sources are picked from the game's; a `CP_Menu` row's menu, a
  `CP_Article` row's text, pictures, fonts and music offer the game's names. Every change is drawn
  at once, and a changed menu is marked ● in the list. **Undo** and **Redo** (Ctrl+Z, Ctrl+Y
  outside a field) step through the changes; **Revert** goes back to the menu as its files make
  it. Clicking a part of the picture opens its entry here.

  **Save** (Ctrl+S) puts the menu in a mod: the last mod with its own file of it, else one you
  pick (pfwolf.pk3 is read-only). The file says only what the mod changes: keys that differ, and
  rows and components by their `id` with only their changed keys, `remove: true` for one taken
  out and `before:`/`after:` for one moved or added in the middle. An entry with no `id` can't be
  matched that way, so changing or moving one writes its whole list, tagged `!replace`, and the
  editor asks first: that list then won't take in later changes pfwolf.pk3 makes to it (give its
  entries ids to avoid that). The saved file is checked by merging it as the game does before it's written. The
  game is then loaded again with the mod.

  **New menu…** (Ctrl+N) starts a copy of the menu picked, or with a name starting `+` a blank
  one (a window and a Back row), and asks for a menu to add a row to that opens it (`CP_Menu`).
  Both are saved like any change. Closing the editor or loading the game again with menus
  changed asks to save them first.
- **Files and uses**: the files the menu is made from, in order: pfwolf.pk3's, then each mod's
  laid over it. **Edit…** opens a file in the Definitions window's YAML tab, which draws the menu
  beside it again as you type (pfwolf.pk3's own is read-only there: *Copy to mod* makes a mod's own
  to change). Then what opens it (the engine, game-info's high scores, another menu's item, with
  *Go to*), and its **problems**: what the game would say or quietly leave out (an unknown
  component or item type, an action the game hasn't, a `CP_Menu` to a menu that isn't there,
  a picture, font or music it hasn't got, `fit-items` naming a component id the menu hasn't, a
  `default-selection` past the last row, a color colors.yaml hasn't got).
- **YAML**: the menu as its files merge, to read. Clicking a part of the picture selects its
  entry here.

## Changing a menu as YAML

A menu can also be changed in its files, in the Definitions window's **YAML files** tab (F11): pick a
`menudefs` file (or **Edit…** in the menu window) and the menu is drawn beside the text, again as
you type, before anything is saved. A mod's file is drawn laid over the menu as the game lays it
(only the keys it gives change, and `menu-items` and `components` entries by their `id`); a file
the running game doesn't use (another pack's own) is drawn on its own.
