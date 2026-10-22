---
title: Editing levels
order: 1
---

# Editing levels

The editor's main window is a level from above: the levels and mods on the left, the tools in a
strip beside the map, the palette and what's shown on the right, and along the bottom what's under
the pointer, how the last thing went, and what the tool's mouse buttons do.

## Levels

The list on the left has the game's levels, its mods' included; one with unsaved changes is
marked. Right-clicking a level has Play, its properties, a check, and saving.

- **New level…** (Ctrl+N) asks for a name (saved as `maps/NAME.wad`) and a size, 3 to 256
  tiles each way (Wolfenstein 3D's are 64×64), and makes a square of open floor walled round.
- **Import map…** (Ctrl+I) reads a WDC `.map` or an ECWolf `.wad` and puts the planes you pick
  onto the level open (one undo step), or makes a new level of them.

A level saved into a mod is listed in the mod's `game-info.yaml` too, so the game knows it: warp
to it with the [`map`](../../reference/console#map) command.

## Planes

A level is planes of numbers, one per tile each. **Edit** (top right) picks the one the tools work
on:

| Plane | What its numbers are |
| --- | --- |
| Walls, doors and floors (plane 0) | Walls, doors and floor codes, as [mapdefs](../../reference/mapdefs) defines them |
| Wall patches (plane 0's high byte) | Pictures over a wall's faces (mapdefs `patches`); they only go on walls, which stay as they are |
| Things (plane 1) | Enemies, items, decorations and player starts (mapdefs `things`) |
| Flats (plane 2) | Ceiling × 256 + floor, indices into the mapdefs flats tables; 0 is the level's default |
| Heights (plane 3) | A wall's height in stories, 1 to 8; 0 is the level's. Taller than 1 on open floor is an arch |
| Tags (plane 4) | A switch works the doors, walls and things that share its tag; 0 is none |
| Light zones (plane 5) | The level's light zones (its properties), by id; 0 is lit as the level |

The **Show** boxes under the palette (and the View menu) choose which planes the map draws over
each other: walls and things as their pictures, the others as numbers. *View → Grid* turns the
grid off.

## The palette

On planes 0 and 1 the palette lists what the mapdefs define, with its picture: walls, doors and
floor codes, the patches, or the things. *Find in the palette* narrows it down. Picking a thing
shows which way it **faces**, the **skill** it's on from, and whether it **patrols**: the palette
puts down the code with those, or the nearest the mapdefs have. Right-clicking an entry opens its
mapdefs entry, or a thing's actor class, in the [definitions window](#definitions).

The other planes take a number: type it, or **Next unused** for a tag or zone not on the level
yet. On the flats plane, the floor and ceiling flats are picked by name. Under the number is what
it means on that plane.

## Tools

The strip beside the map has the tools, each with its letter key. The options for the one picked
(a brush's size, the box's outline) show in the bar above the map.

| Tool | Key | What it does |
| --- | --- | --- |
| Select | S | Drag a block of tiles; Shift adds, Ctrl takes away. Drag inside it, or use the arrow keys, to move it |
| Magic wand | W | Click to select the tiles joined to it that hold the same (with *Joined only* off, every one on the level) |
| Paint | P | What's picked, on every tile the brush passes over: a size of 1 to 9, square or round |
| Erase | E | Clears what the brush passes over: open floor on plane 0, nothing on the others |
| Scatter | A | What's picked, on some of the tiles the brush passes over (the *Density*), or at random from a set (*Add picked*) |
| Line | L | Drag a line; Shift keeps it straight or diagonal |
| Rectangle | R | Drag a box (*Outline only*: its edge); Shift makes it square |
| Ellipse | O | Drag an ellipse (*Outline only*: its edge); Shift makes it a circle |
| Room | M | Drag a room: walls round the edge, open floor inside |
| Fill | F | Fills the joined tiles that hold the same as the one clicked |
| Stamp | T | Puts down what was last copied at each click; a drag lays copies side by side |
| Free thing | G | Puts the thing picked anywhere, off the tile grid (an eighth of a tile; Shift: a tile's middle). Click one to drag it, Delete takes it away |
| Pick | I | Click a tile to pick what's on it into the palette |

On the map, the middle or right button drags it about, and the wheel zooms (Shift scrolls across,
Alt up and down); Home fits the whole level in.

## Working with a selection

| Key | What it does |
| --- | --- |
| Ctrl+C, Ctrl+X | Copy, cut |
| Ctrl+V | Paste: the copy follows the pointer until a click puts it down (Escape drops it) |
| Ctrl+D | Duplicate: copies the selection and picks the copy up |
| Delete | Clears it |
| ] and [ | Rotate it, or the paste being placed (*Edit → Rotate 180°* too) |
| Shift+H, Shift+V | Flip it |
| Ctrl+A, Escape | Select everything, nothing (*Edit → Invert selection* too) |

The things on it turn to face the new way. Copying works on the plane being edited, or every
plane with *Copy all planes*. With *Paste over*, a paste or a stamp leaves what's under its open
floor (plane 0) and its empty tiles (the other planes).

**Replace…** (Ctrl+H) swaps one value for another on the plane being edited, in the selection or
the whole level: it lists what the level has, most used first.

## Level properties

**Level properties…** (F4) edits the level's entry in game-info, saved into the mod's
`game-info.yaml` with the level. A field left empty takes the default map's value, shown faded:

- **The level**: its name, music, next and secret next levels (no next level ends the episode),
  and par time;
- **How it looks**: floor and ceiling colors, wall height, sky, default flats, weather, and shading
  (light, fade color and distance, for night and fog);
- **Zones**: the light zones plane 5 names, each with its light, color and effect, and the sounds
  looped in it;
- **Scrollers**: for a tag on plane 4, how fast the textures of its walls, floor or ceiling slide,
  and whether it's running as the level starts or waits for a switch.

[game-info](../../reference/game-info) has what each of these does.

## Size and format

**Level → Resize level…** gives the level a new width and height, 3 to 256 tiles each way, keeping
a corner, an edge or the middle where it is (`top-left`, `top`... `center`... `bottom-right`).
What no longer fits is cut off, and new tiles are wall. It's one undo step.

A level saves as **UWMF** text (ECWolf's TEXTMAP), which holds everything, or as binary planes
(WDC 3.1). New levels are UWMF, and a level keeps the format it was read in, unless *Level → Save
as UWMF* changes it. A level with walls, doors, flats or free things of its own can only be UWMF.

## Checking a level

**Check level** (F7) lists, under the map, what the game would trip over (errors) or what's likely
a mistake (warnings). Picking one goes to its tile:

- no player start, or several (the game uses the last);
- values the mapdefs don't know, on any plane, and weather game-info hasn't got;
- more doors than the game takes, and doors without walls on both sides to slide into;
- things on a wall or a door, which aren't placed, and heights outside 1 to 8;
- patches off walls, or on faces that never show;
- switches without a tag, or with one nothing else has, and tags no switch has;
- light zones the level properties don't define;
- open floor on the level's edge, and rooms the player can't get to;
- a level game-info doesn't list, which only the `map` command reaches.

## Playing a level

**Play** (F5) saves the level if it's changed, since the game reads it from its mod, then starts
PFWolf on it with the mods, on the skill picked beside the button. **Play from here** (Shift+F5
over the map) starts on the tile under the pointer instead of the player start; the button starts
on the selection.

## Definitions

**Resources → Definitions…** (F11) edits the definition files (game-info, the game packs, actor
classes, mapdefs and the rest) as fields or as YAML, saved into a mod; pfwolf.pk3's own are read
only. The [definitions reference](../../reference/) has every field.
