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

## Changing a menu

A menu is changed in its files, in the Definitions window's **YAML files** tab (F11): pick a
`menudefs` file (or **Edit…** in the menu window) and the menu is drawn beside the text, again as
you type, before anything is saved. A mod's file is drawn laid over the menu as the game lays it
(only the keys it gives change, and `menu-items` and `components` entries by their `id`); a file
the running game doesn't use (another pack's own) is drawn on its own.
