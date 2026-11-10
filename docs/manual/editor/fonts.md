---
title: Fonts
order: 8
---

# Fonts

**Resources → Fonts…** (Ctrl+Shift+F) lists every font the game prints in, each drawn exactly as
the game draws it: the fonts the game pack's `fonts.yaml` defines (with a mod's laid over them),
and the Wolf3D fonts nothing defines by their own name, such as Blake Stone's Font1 to Font5. What
each `fonts.yaml` key does is in the [fonts reference](../../reference/fonts).

The list can be narrowed by type (vga, graphic, sheet, or the fonts with problems) and by name, by
the font its glyphs come from, or by what prints in it: *LargeFont*, *Font3*, *hud-messages*. A
⚠ marks a font with a problem, and a ● one changed and not saved.

Picking one shows what kind of font it is, how high, and where its glyphs come from (a vga font's
font chunk or file, a graphic or sheet font's pictures), then a **preview** of some text in it,
with its shadow, outline, gradient and glow, as it is with any changes not saved yet. The text,
its color and the color behind it (a colors.yaml name, `#RRGGBB` or a palette index) can be
changed, and it can be zoomed. Under the preview are its **problems**: a picture or font it's made
from that isn't there, a type the game doesn't know, a color colors.yaml hasn't got, a font
nothing names, and Blake Stone's Font1, whose glyphs sit 31 codes below their characters.

The tabs under that:

- **Style**: its `fonts.yaml` fields, as the Definitions window has them, each with what it does.
  Only the fields its type reads are shown (a vga font's source and cursor, a sheet's image and
  cells, a graphic font's pattern and glyphs), plus any that are set; changing the type brings the
  new type's. Every change is drawn in the preview as it's made;
- **Characters**: codes 0 to 255, sixteen to a row, each drawn plain. A dark cell is a character
  the font hasn't got; the game leaves room for it rather than drawing anything. The mouse over a
  cell says its code and how wide it is;
- **Used by**: the game-info sections, menus, message styles, status bars, intermission and
  presenter entries that name it, scripts calling `game.drawtext` or `game.textwidth` with its
  name, the fonts made from it, and what the engine prints in it by itself. *Go to* (or a
  double-click) opens a definition in the Definitions window, at its field, or a script at the line;
- **Defined in**: the `fonts.yaml` files that define it, and its lines in the last of them, the
  one the game uses. *Open in the Definitions window* shows it in that window's Fonts tab, where
  each file's YAML can be opened.

## Changing a font

**Save** (Ctrl+S) writes just what's changed into a mod: the mod that has its own of the font
already, else one you pick (pfwolf.pk3 is read-only). The game is loaded again with it, so the
list shows the font as the game now has it. **Revert** goes back to the font as the files have
it; **Undo** and **Redo** (Ctrl+Z, Ctrl+Y, outside a field) step through the changes, each field
typed in being one step. Changes are kept while you look at other fonts, and loading the game
again or closing the editor asks whether to save them first.

**New font…** makes a font that's only in the window until it's saved: a copy of the font picked
(a font chunk's: a vga font of its letters), or a vga font of the small font's letters. It's named
as you ask, and saved whole. *Take out the mod's change…* (in Defined in, and the list's right-click
menu) takes the last mod's say in the font out of its `fonts.yaml`, so it's what the game and the
mods before make it.

*Copy name* copies a font's name, and *Export font file (.fnt)…* saves a vga font's glyphs as a
Wolf3D font file, the kind a mod's `fonts/` folder takes.
