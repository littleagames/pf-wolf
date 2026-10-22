---
title: The 3D view
order: 2
---

# The 3D view

**View → 3D view** (F3, or the cube button) shows the level in 3D beside the map, drawn through
OpenGL: walls of every height and their arches, doors, flats, the sky, things as their sprites,
patches and scrolling textures. Drag the bar between them to share the window out. The map shows
where the camera is and which way it looks, and it starts on the player start.

## Moving about

| Control | What it does |
| --- | --- |
| W, S (or the up and down arrows) | Forward, back |
| A, D | Sideways |
| Q, E (or Page Down, Page Up) | Down, up |
| Left and right arrows | Turn |
| Shift | Faster |
| Right drag | Look about |
| Middle drag | Slide |
| Wheel | Move along the view |
| V, over the map | Moves the camera onto the tile under the pointer, facing the same way |

## Editing in 3D

The left button works the [map's tools](./levels#tools) on the tile under the pointer, and the
status bar says what the pointer is on (a wall's face and its texture, the floor, a thing) and the
tile an edit goes to. A thing goes on the tile in front of the wall face it's put on, and so does
anything else with Shift held, so a wall can be painted from the room it faces. Delete clears the
tile under the pointer, Ctrl+C, Ctrl+X and Ctrl+V copy, cut and paste there, and ] [ Shift+H and
Shift+V turn a selection or a paste.

A tool's letter picks it here too, except the letters used for moving: Magic wand (W), Scatter (A)
and Erase (E) are picked from the strip or the map while the 3D view has the keyboard.

## What it shows

The View menu has the 3D view's own layers:

- **3D: things**, the level's things;
- **3D: ceilings**, the ceilings, off to look down into rooms;
- **3D: shading**, the level's light, its light zones and distance fade, as the game shows them;
- **3D: markers**: push walls tinted purple and switches green, and, hovering a tagged tile,
  everything with its tag in orange.

It needs OpenGL; where it can't start, the problems list says why, and the map works as ever.
