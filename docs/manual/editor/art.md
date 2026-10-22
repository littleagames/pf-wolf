---
title: Textures and sprites
order: 3
---

# Textures and sprites

**Resources → Textures and sprites…** (F6) lists every picture the game has: wall textures, flats,
sprites and pictures (title screens, the status bar, menus), from its data files, pfwolf.pk3 and
the mods. It opens on what the palette has picked, and stays open beside the map.

## Finding a picture

The list can be narrowed by kind (wall textures, flats, sprites, pictures), by **In use**, **Not in
use** or **From the mods**, and by name or by what uses it: *guard*, *door 90*, *floor flat*. A
picture in use is one a mapdefs wall, door or flat, an actordefs class or a level's sky shows.

Picking one shows it big, with:

- **Where it comes from**: each place that has a picture of that name, the last being the one the
  game uses (a mod's over pfwolf.pk3's over the data files');
- **Used by**: what shows it. *Use in palette* (or a double-click) picks that wall, door, flat or
  thing to put down on the map.

A sprite frame drawn from eight sides shows one at a time: ◀ and ▶ (or , and .) turn it, 1 facing
you and 5 away. An animation (animdefs) plays as the game plays it, 70 tics a second, and its frame
strip goes to a frame. The background behind a sprite's see-through pixels, the zoom and the
palette it's shown in can be changed; a picture the game shows in a palette of its own (a title
or end screen) is shown in that one.

A sprite's **offset** is the point it stands on the floor by, from its top-left: Wolfenstein 3D's
stand on 32, 64. *Set* writes another into the sprite's PNG (its `grAb` chunk, as ZDoom and SLADE
keep it), in its mod or, for the game's own, in a mod picked.

## Changing pictures

| Button | What it does |
| --- | --- |
| Add… | Puts PNG or BMP files in a mod as wall textures, flats, sprites or pictures, named by their file names |
| New… | Draws a new picture in the pixel editor; saving puts it in a mod |
| Edit pixels | Draws on the picture in the pixel editor |
| Edit outside | Opens it in a paint program (*Paint program ▾* picks which) |
| Replace… | Puts a PNG or BMP in its place: over its file in its mod, or, for the game's own, as a file of that name in a mod |
| Rename… | Renames a mod's own picture (what uses it by its old name doesn't follow) |
| Delete | Takes a mod's own picture out of its mod; the game's own of that name shows again |
| Export PNG… | Saves it as a PNG, in the palette it's shown in |
| Copy name | Copies the name of what's showing: the side or frame, `GARDA3` |

A picture of the game's own is never changed where it is: the new one goes into a mod, under the
same name, and shows in place of it. Pictures go in a mod's `textures/`, `flats/`, `sprites/` or
`graphics/` folder.

While a picture is open in another program, the browser watches for it being saved there and
offers to **Bring it in**, in place of the one it was opened from; *Bring saves in at once* does it
each time, and *Stop watching* lets them all go.

## The pixel editor

*Edit pixels* and *New…* open the pixel editor: the picture drawn in the game palette, the left
button in one color and the right in another.

| Tool | Key | What it does |
| --- | --- | --- |
| Pencil | B | Draws |
| Eraser | E | Makes pixels see-through (not on a wall texture, which has none) |
| Fill | F | Fills the joined pixels of the color clicked; with Ctrl, every pixel of that color |
| Picker | I | Picks a pixel's color: the left button for the left color, the right for the right |

Click a color for the left button, right-click it for the right; X swaps them, and dots mark the
colors the picture uses. *See-through* draws see-through with the left button. + and - zoom,
*Grid* shows the pixels' edges, and a sprite's offset shows as a cross. Ctrl+Z and Ctrl+Y undo and
redo.

A picture the game shows in a palette of its own can be drawn in that one: its pixels keep their
color numbers, and it's saved in the game palette's colors, which the game matches back. **Save**
(Ctrl+S) writes a PNG into its mod, or, for the game's own or a new picture, into a mod picked.
