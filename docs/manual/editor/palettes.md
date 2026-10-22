---
title: Palettes
order: 5
---

# Palettes

**Resources → Palettes…** (F9) lists every palette the game has, to look at and change: the game
palette everything is drawn in, and the palettes game-info's screens and the movies draw in. **In
use** lists those (the game palette always is), **From the mods** those of the mods.

Picking one shows its 256 colors, **Used by** (what draws in it; *Show its picture* previews that
picture in it) and **Where it comes from**. The **Preview** shows a picture in the palette as it's
changed: click a pixel there to pick its color, and with *Only the colors picked* every other
color greys out, to see where those are used.

## Changing colors

Click a color to pick it; Shift+click or a drag picks a run of them, and the arrow keys move (with
Shift, extending the run). Then:

| Control | What it does |
| --- | --- |
| R, G, B or Hex | The color picked, by its parts or as `#RRGGBB` (Enter sets it); the bar shows it now over how the game loaded it |
| Gradient (Ctrl+G) | Blends the colors picked evenly from the first to the last (three or more) |
| Copy, Paste (Ctrl+C, Ctrl+V) | The colors picked; a paste goes over the colors from the first picked on |
| Revert | Puts the colors picked back as the game loaded them (*Revert all*: every color) |
| Find | A color by its number (`37`), or the closest to `#RRGGBB` |
| Import… | Replaces the colors with a palette file's: raw 768 bytes (0-255, or 6-bit 0-63), JASC-PAL or GIMP `.gpl` |
| Export ▾ | Saves the colors as a raw `.pal` (as `palettes/` holds them), JASC-PAL or GIMP `.gpl` |

Ctrl+Z and Ctrl+Y undo and redo. **Save** (Ctrl+S) writes `palettes/NAME.pal` into the palette's
mod, where the game reads it in place of its own; *Save to…* into another mod. *Delete…* takes a
mod's own palette out.

A picture's pixels are color numbers, so changing a color changes everything drawn with that
number, in every picture the palette draws.
