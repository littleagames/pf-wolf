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
⚠ marks a font with a problem.

Picking one shows:

- what kind of font it is, how high, and where its glyphs come from: a vga font's font chunk or
  file, a graphic or sheet font's pictures;
- a **preview** of some text in it, with its shadow, outline, gradient and glow. The text, its
  color and the color behind it (a colors.yaml name, `#RRGGBB` or a palette index) can be changed,
  and it can be zoomed;
- its **characters**: codes 0 to 255, sixteen to a row, each drawn plain. A dark cell is a
  character the font hasn't got; the game leaves room for it rather than drawing anything. The
  mouse over a cell says its code and how wide it is;
- its **problems**: a picture or font it's made from that isn't there, a type the game doesn't
  know, a color colors.yaml hasn't got, a font nothing names, and Blake Stone's Font1, whose glyphs
  sit 31 codes below their characters;
- **Used by**: the game-info sections, menus, message styles, status bars, intermission and
  presenter entries that name it, scripts calling `game.drawtext` or `game.textwidth` with its
  name, the fonts made from it, and what the engine prints in it by itself. *Go to* (or a
  double-click) opens a definition in the Definitions window, at its field, or a script at the line;
- **Defined in**: the `fonts.yaml` files that define it, and its lines in the last of them, the
  one the game uses.

*Copy name* copies its name, and *Export font file (.fnt)…* saves a vga font's glyphs as a Wolf3D
font file, the kind a mod's `fonts/` folder takes.
