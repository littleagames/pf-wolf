# Actor editor: seeing and changing actor classes (editor)

A window in the editor for the actordefs classes: every enemy, item, projectile, decoration,
weapon and player class, shown playing its states as the game plays them and turning through
its eight views. It also shows what each class is built on and what uses it. Its properties,
flags and states can be changed, and new classes made, all saved into a mod. Today the
editor has no actor window. A class is changed only as YAML fields in the Definitions window
(F11, Actors tab), and the editor's only actor drawing is the one picture of a thing's Spawn
frame on the map (`GameContent.ThingSprite`). Branch: not started (feature/actor-editor
suggested).

Decided (2026-11-10, by the user):
- **Only the state rules move into PFWolf.Common.** These are the state resolver, the frame
  data and the rules for merging a class with its parents. The editor steps frames itself
  (tics, next-state, loops), names the thinks and actions a frame runs, and never runs them.
- **States are edited in a visual timeline.** Frames show as blocks as wide as their tics.
  They can be added, dragged to reorder, copied and deleted, and the picked frame's fields
  show underneath, with pickers.
- **Every class is covered**: enemies, items, projectiles, decorations, weapons (their states
  in the player's hand) and player classes, for Wolf3D, Spear, Blake Stone and Planet Strike,
  with a filter for each kind.
- **New actor… copies a class or builds on it**, the user's choice each time. It can also
  give the class a mapdefs thing id, so it can be placed on a map.

The window as sketched (2026-11-10): the class list on the left (search, kind filters, ⚠ for
problems, ● for unsaved, the parent tree under it). On the right are the class's header
(parent, file, mapdefs ids), the preview (a state picker, play/pause/step, speed, "follow
next-state", eight view buttons, the picture's name, the tic, and the think and action
running), the class's sounds and key numbers, the states timeline, the fields, and Used by.

## The formats

**Actor classes.** `actordefs/` in pfwolf.pk3 and mods: files directly in it (`native.yaml`,
`deathcam.yaml`, `prophunt.yaml`) are shared by every pack, and `actordefs/<pack>/*.yaml` are
that pack's (`{pack}/actordefs` assets, `mergeLevels: 1`). Each is read into
`ActorTranslationAsset` (name → `ActorData`), and `ActorMetadata.AddActors` lays them over each
other (`ActorData.Combine`). A class loaded later replaces one of its name whole, unless it has
`extend: true`. Then its properties and states replace those of the same name, its flags are
added, and `!remove`, `!replace` and `~FLAG` take parts away (`ActorTranslationAsset.MergeYaml`).
A class's fields:
- `parent`: the class it's built on. A class has its parents' flags, and their properties and
  states unless it sets its own, nearest parent first (`ActorMetadata.CreateActor`). The C#
  type it becomes is the nearest of it or its parents that names one (`Monster`,
  `BlakeMonster`, `Inventory`, `Weapon`, `PlayerPawn`...); a class with none is a plain
  `Actor`.
- `radius`, `flags` (`ActorFlags.All`: name, what it does, the class it needs),
  `properties` (`ActorProperties.All`: name, type, default, hint for a sound, class, sprite or
  picture, the class it needs; `health.SKILL`-style patterns).
- `states`: name → list of entries. An entry is either frames (`sprite`, `frames: [A, B]`,
  `tics-per-frame` (-1 holds, 0 ends at once), `think` (every tic), `action` (as the frame ends;
  a 0-tic frame's at once; can have arguments, `A_FireProjectile("Rocket")`), `next-state`,
  `modifiers` (`bright`, `lighten(level)`), `light`, `particles`), or a marker (`stop`,
  `loop`, `{ goto: State }`).

**How states play** (`ActorStateResolver.Resolve`, `Actor.AdvanceFrames`,
`MapManager.DoActor`): each entry's letters become one frame each, linked in order. The last
frame's `next-state` or marker says what follows it, and a state that has neither loops to
its own first frame. A marker that isn't on a state's last entry is never reached (warned).
A class with Death and no Raise gets a Raise made of its Death frames backwards
(`AddRaiseFromDeath`). The game counts tics down at 70 a second, runs the action as a frame
ends, follows Next, and ends 0-tic frames in the same tic. Frames that hold forever only run
their think. Actions can jump elsewhere (`A_Jump`, `JumpTo`, Monster's Pain/Pain1 choice),
and the preview can't know where to, so it says so rather than guessing.

**Sprites.** A frame's picture is `{sprite}{letter}{view}`: view 0 for one that looks the same
from every side, 1 (front) to 8 for one that turns (`ActorStateFrame.ShapeNames`). `TNT1` is
nothing. A weapon's states are drawn in the player's hand at the bottom of the view.
`weapon.bodysprite` is how other players see it held. Sprites come from VSWAP, `sprites/`
folders (subfolders only for order) and mods. F6 lists them, and `ArtCatalog` knows which
classes use them.

**Where a class is named** (what "Used by" reads):
- mapdefs `things:` (`{ class, angles, patrol, min-skill, ... }`) → the levels that place
  those ids (plane 1).
- other classes: `parent`; properties hinted as a class (`dropitem`, `weapon.ammotype1`,
  `powerup.weapon`, player classes' start items...); action arguments naming a class
  (`A_FireProjectile("Rocket")`, `A_SpawnThing("Smoke")`, `A_GiveInventory("Clip", 25)`).
- game-info (player classes and the class menu, starting items, match modes like Horde's
  waves and Prop Hunt's), weapons.yaml / player.yaml, factions, mapdefs switches' and triggers'
  actions with class arguments.
- Lua: `actor:spawn`, `actor:spawnprojectile`, `game.actors`, `game.tagged` with a literal
  class name.
- The engine itself by name (the player pawn, DeathCam, PatrolPoint and the like), listed
  once in Common so the engine and "Used by" can't drift, as `FontNames` does for fonts.

**What each game has:** pfwolf.pk3's `actordefs/wolf3d` (guards, bosses, ghosts, decorations,
items, weapons, projectiles, player, victory, wall sprites), `spear`, `blake`,
`blake-shareware`, `planetstrike`, plus the shared `native.yaml` base classes. Many example
mods add classes (`examples/mods/*/actordefs`), and `multiplayer.pk3` adds the enemy player
classes.

## Where things go

- **PFWolf.Common, Actors/** (phase 0): `ActorStateResolver` and the data side of
  `ActorStateFrame` (sprite, letter, tics, think, action, modifiers, light, particles, Next,
  `ShapeNames`, known modifiers), and a `ClassResolver` holding `ActorMetadata`'s parent
  rules (nearest parent first for properties, states and radius; flags added; cycles and
  missing parents reported). `Bright` needs `Lighting.IsBrightLevel`, so the game keeps that
  part, or the check moves with it. The game's `ActorMetadata`, `Actor` and `DoActor` keep
  their APIs and call into Common. Stepping, actions and the C# actor types stay in the game.
- **A `StatePlayer` (Common or the editor):** a frame and a tic count. It steps the way
  `AdvanceFrames` does (ending 0-tic frames in the same tic, following Next, holding on -1),
  reports each think and action it passes, and stops at an action that may jump.
- **PFWolf.Editor, Data/ActorCatalog:** every class as the game merges them, with its kind
  (from its C# type and flags: enemy, item, projectile, decoration, weapon, player, marker),
  where it's defined (file and lines, through `DefinitionFile`), the states it resolves to,
  uses and problems. Problems are: a sprite the game can't find for a frame or view; an
  unknown think or action, or one for another kind of class (`GameActions`' `Needs`); a
  property or flag that does nothing on this class (`MisplacedSettings`); and the resolver's
  warnings.
- **Actor window** (Resources → **Actors…**, Ctrl+Shift+A, free): ActorBrowserViewModel /
  ActorBrowserWindow, laid out as the sketch. Rendering/StateTimeline draws the timeline, and
  the preview draws through `ArtCache` like F6 does.
- **Saving is mods only:** changed keys go into the mod's actordefs file through
  `DefinitionSet` (overlays, `YamlText` splices so comments survive). A class of the game's own
  is saved with `extend: true` and just what changed; a new class is saved whole, in
  `actordefs/<pack>/` of the mod picked. mapdefs ids for new classes go in the mod's mapdefs.
  pfwolf.pk3 stays read-only.

## Phases

0. **State rules in Common.** First, a test that resolves every class of Wolf3D, Spear, Blake
   Stone, Planet Strike, multiplayer.pk3 and every example mod. It writes down each state's
   frames (sprite, letter, tics, think, action, next) and the warnings, and must be the same
   after the move. Then the move (above). Done = that test unchanged, the full suite passing,
   and demotest's eight demos still identical.
1. **Actor window (read-only).** The list: search (name, sprite, think, action), kind filters,
   ⚠, the parent tree. The class picked: header, preview (state, play/pause/step, 1×/½×/¼×,
   follow next-state, eight views, a view the sprite lacks shown with its name and a ⚠),
   sounds and key numbers, the timeline (a click picks a frame and shows its fields),
   properties and flags (its own, and its parents' greyed with where they come from), problems,
   Used by with Go to (Definitions window at the field, the map at a level's first such thing,
   the script browser at a line), and Defined in. Weapons preview in the hand, at the bottom
   of a view-sized box, with `weapon.bodysprite` shown too. Right-click: Copy name, Edit in the
   Definitions window, Show sprite in F6.
2. **Fields.** Properties, flags, parent and radius as FieldTree rows, the properties and flags
   that do nothing on this class hidden unless they're set. Changing parent or
   adding a property redraws the preview and problems. New actor…: copy (its states and
   everything its parents gave it, written out, nothing shared) or build on (`parent:` it,
   nothing else yet), named as asked, and optionally a mapdefs thing id (the next free one in
   the mod's range) with angles/patrol variants for an enemy. Undo/redo, Save, Revert, Take out
   the mod's change, the unsaved ones in the editor's "Save first?".
3. **States.** The timeline edits: add a frame (the sprite's next letter), duplicate, delete,
   drag to reorder, drag between states, set tics by number or by dragging a block's edge.
   Under it, the picked frame's sprite (picker of the sprite's letters with thumbnails),
   think and action (pickers of `GameActions` that fit the class's kind, with what each does
   and its arguments), next-state (the class's states), modifiers, light and particles. Add,
   rename (renaming its next-states too) and delete states, and add a usual state (Pain,
   Melee, Raise) from the parent's. Saved as the class's whole state when it changes (actordefs
   states are replaced whole by name). A Raise the engine makes from Death is drawn in the
   timeline as made, never saved.
4. **Extras.** Play the class's sounds as the preview passes their actions (`A_PlaySound`,
   seesound on Spawn→Chase, death sounds); off by default. Edit the frame's picture in F6's
   pixel editor; a missing view made there. Place the class: picks it in the map's thing
   palette. A "seen from the player" preview with distance shading (`lighten`, `bright`).

## Defaults (open to change)

- The preview starts on Spawn, facing the viewer (view 1), playing at 1×. An enemy's Spawn
  that holds forever starts on Chase instead, so something moves.
- Thinks are named, never run: a frame with T_Chase shows "T_Chase every tic". An action that
  may jump (`A_Jump`, `A_JumpIf…`, `A_ReFire`, Monster's pain choice) pauses the preview and
  offers each place it could go.
- A game class changed in a mod is saved with `extend: true`, so the game's later fixes to the
  rest of the class still reach it.
- Blake Stone's map-info and actor codes stay the map editor's (Level → Edit code…); the actor
  window only shows the classes.

## Open questions

- Should Used by count the things placed on every level (needs every level loaded; slower on
  first open)? Default: yes, counted in the background with the level names shown as they come.
- Weapons: a separate Weapons kind with a view-sized hand preview (default), or a weapon
  window of its own later?
- Should F6 link each sprite back to the classes using it (it knows them already) with a Go to
  the actor window? Default: yes, once phase 1 is in.
- The engine-only classes (the C# types: `Monster`, `BlakeMonster`, `Inventory`...) listed as
  read-only roots of the tree (default), or hidden?

## Status

Planned 2026-11-10.
