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
  is saved as just what changed (a mod's `actordefs/x.yaml` loads into the running pack's
  classes and merges into them key by key, so no `extend: true` is needed; found in phase 2); a
  new class is saved whole. mapdefs ids for new classes go in the mod's mapdefs.
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
- A game class changed in a mod is saved as just what changed, so the game's later fixes to the
  rest of the class still reach it (mods' classes merge key by key; `extend: true` isn't needed).
- Blake Stone's map-info and actor codes stay the map editor's (Level → Edit code…); the actor
  window only shows the classes.

## Open questions

- Should Used by count the things placed on every level (needs every level loaded; slower on
  first open)? Default: yes, counted in the background with the level names shown as they come.
- Weapons: a separate Weapons kind with a view-sized hand preview (default), or a weapon
  window of its own later?
- Should F6 link each sprite back to the classes using it (it knows them already) with a Go to
  the actor window? Default: yes, once phase 1 is in. Done 2026-11-10: F6's Used by has Show in
  Actors for a class's sprite frames and status bar faces (`ArtUse.ActorClass`).
- The engine-only classes (the C# types: `Monster`, `BlakeMonster`, `Inventory`...) listed as
  read-only roots of the tree (default), or hidden? Decided 2026-11-11 (the user): editable, and saving one asks first, saying how many classes are built on it (`ActorBrowserViewModel.BuiltOn`, `BaseClassWarning`).

## Status

Planned 2026-11-10.

**Phase 0 done 2026-11-10** (the state rules in Common):
- PFWolf.Common/Entities/Actors: `ActorStateFrame` and `ActorStateResolver` moved there (`git mv`,
  now public, namespace unchanged). A frame's picture is `ShapeName(view)`; the game's
  `GetShapeName(ObjDir)` is an extension (`ActorStateFrameViews`), and `Bright` reads
  `lighten(bright)` itself rather than through the game's `Lighting`. `ActorInheritance` holds
  the parent rules `ActorMetadata.CreateActor` had: `Ancestors` (nearest first; a missing
  parent of the class itself or a loop is an error, a missing one further up ends the line)
  and `Inherit` (properties, states and radius from the nearest that has them, every flag).
  `CreateActor` still picks the C# type and builds the actor.
- Checked: `ActorStateSnapshotTests` writes every class of the seven games, the Multiplayer
  mod and the 27 example mods with actordefs (6354 classes): type, radius, flags, properties,
  each state's frames with where each goes, and the warnings. With `PFWOLF_ACTOR_SNAPSHOT` set
  it saves that to a file; before and after the move the files were byte-identical. The full
  suite passes (3772, incl. DemoRegressionTests' demos); one run of three had a single failure
  that didn't come back, which the summary didn't name.

**Phase 1 done 2026-11-10** (the actor window, read-only):
- Common: `ActorClasses` (the engine's actor classes and what each is built on, kept in step
  with the engine's types by a test; `EngineClass`, `IsA`; `EngineUses`, the classes the engine
  makes by name), `StatePlayer` (steps frames as DoActor/AdvanceFrames do, telling each action
  as its frame ends; stops at an action that may jump; tested tic by tic against the game's
  Actor), `ActorInheritance.Merged` (a merged copy, the class as written left alone).
- `ActorCatalog` (Data): every class merged and resolved; kind (enemy, item, weapon,
  projectile, decoration, player, unseen) from its engine class, flags and Spawn sprite;
  problems (can't be made, unknown modifiers, a next-state it hasn't got, actions nothing has
  or for another kind, properties and flags the engine doesn't read or only another kind
  reads); a frame with no sprite is a problem only on a class a level places, else a note
  (shared classes draw frames only other games have). Uses: the engine's, classes built on it,
  class-hinted properties and action arguments naming it, mapdefs things (one line per class,
  ids as runs), game-info and factions values (names and shown text skipped), Lua spawn /
  spawnprojectile / actors / tagged string literals, and every level placing it (count, first
  tile). Defined in: each actordefs file's lines.
- ActorBrowserViewModel / ActorBrowserWindow (Resources → Actors…, Ctrl+Shift+A): list with
  kind filter and ⚠; preview (state picker, Play/Pause, Next frame, Restart, 1×/½×/¼×, Follow
  next-state, views 1-8, zoom; a 35 Hz timer stepping two tics); the timeline (frames as wide as
  their tics, a click shows one); problems and notes; tabs Properties (with the parent each comes
  from), Used by (Go to: a class here, the Definitions window at an entry, a script's line, a
  level with the first one's tile picked), Defined in. Show sprite (F6), Open in Definitions,
  Copy name. Manual page docs/manual/editor/actors.md, manual-testing 2.19.
- Tests: ActorCatalogTests (calls, runs, engine classes), GameData ActorCatalogGameTests (every
  Wolf3D, Spear, Blake and Planet Strike class with no problems; the Guard's kind, states, file,
  uses and level Go to target; views; the window playing Guard and Pistol), StatePlayerTests.
- Checked by screenshots: BJVictory and the Guard playing, view 3, Attack followed into Chase,
  Used by (which found a `tag: Guard` and a game-info player-class name counted as uses, and one
  line per mapdefs thing: fixed), Go to MAP02 (tile 18,2 picked, "Object 144: Guard"), the
  Pistol's Ready (which found Next frame running 10000 tics on a frame looping on itself and the
  ran-actions line growing without end: fixed). Not tried by hand: Blake, a mod's classes, a ⚠
  class, Go to into the Definitions window and a script, Show sprite, the jump wait.

**Phase 2 done 2026-11-10** (fields):
- `ActorBrowserViewModel.Fields` (the font window's style editing, for classes): a class's own
  parent, radius, flags and properties as FieldTree rows, through `ClassSchema` (ActorSchema with
  `properties` as ActorProperties' record, so each known property is a typed field with its
  description, default and sound/class names to pick; unknown keys stay as YAML). Only the
  properties it sets are shown; Add property lists those its engine class reads (Needs) and it
  hasn't got, and `FieldNode.ShowField` puts the one picked in, empty. States stay off the rows
  (phase 3's). Each change rebuilds the class (`ActorCatalog.Rebuild`: the class as edited among
  the game's others, its uses and files kept), so the preview, problems, numbers, timeline and
  the "From its parents" list are the class as changed; the state playing stays. Undo/redo,
  Revert, Save (just what changed, through `DefinitionSet.PlanWrite`, into the mod that has it,
  else one picked; the game reloads), Take out the mod's change, ● in the list, unsaved classes
  in the editor's "Save first?".
- New actor (top right): Built on this one… (`parent:` it) or A copy of this one… (its own
  fields as written, the same parent: phase 1's plan said "everything its parents gave it,
  written out", but a copy can't drop its parent without losing its engine class, so it's the
  class as written). Named (no spaces, quotes, brackets...), then mapdefs things: one, or eight
  for an enemy (angles 0/90/180/270, standing then patrol: 1), from the first free run at 1000,
  or none; saved with the class as one change to the mod's things table.
- Tests: ActorEditingTests (GameData: a health change played, undone, saved as just that and the
  game with the mod keeping the Guard's other fields and states; Add property; a parent that's no
  class; a class built on the Guard saved with things 2000-2007 and read back; a copy).
- Checked by screenshots: the Fields tab on the Guard, health set to 50 (● , Changed, numbers,
  still playing), Revert. Not tried by hand: saving from the window, New actor (its menu, the
  questions), Take out, the "Save first?" prompt.

**Phase 3 done 2026-11-10** (states):
- `StateFrames` (Editing): a state's YAML read into single frames (`FrameSpec`: sprite, letter,
  tics, think, action, modifiers, light and particles as written) and how it ends (`StateEnd`:
  loop, stop, next), as the game reads it (the last frame entry's next-state, else the first
  marker after it); written back with frames alike but for the letter grouped as one entry. A
  next-state or marker that's never reached is left out (the game ignores it). Checked: every
  state of pfwolf-pk3, the Multiplayer mod and the example mods (871) plays the same written back.
- `ActorBrowserViewModel.States`: the Frame tab, on the frame the preview's paused on: sprite
  (the game's sprite names), letter (the sprite's letters with pictures), tics, think and action
  (pickers of the engine's actions that fit the class's engine class, and its scripts'; what they
  do shown), modifiers, light, particles (YAML on a line); + Frame (next letter, no action),
  Duplicate, Take out (not the last), ←/→, drag along the timeline; how the state ends (loops,
  stops, goes on to a state); + State… (a parent's of the name, else the frame shown, looping),
  Rename… (its own states going on to it follow), Take out state. Each is one step to undo,
  writing the state whole into the class's fields (a parent's state becomes the class's own),
  the preview kept on the frame. The engine's Raise made from Death is shown, not changeable.
  Drag was within a state only and tics set by number at first; both gaps closed 2026-11-11:
  a block's right edge drags its tics (`TimelineFrame.DragTics`, set on release), and a frame
  dropped on a state's name (the chips under the timeline) goes to its end (`MoveFrameToState`;
  Ctrl copies; a state's only frame is copied).
- Tests: StateFramesTests (reading, grouping, markers, the 871-state round trip),
  ActorEditingTests (a frame's tics changed, undone, saved as the state and the game playing it;
  frames added, moved, taken out; endings; states added, renamed, taken out; a parent's state
  becoming the class's own; Raise not changeable).
- Checked by screenshots: the Frame tab on the Guard's Chase (paused by Next frame after Attack
  went on into it), the letter picker's picture. Not tried by hand: drag, saving a state change,
  light/particles.

**Phase 4 done 2026-11-10** (extras); every phase of the plan is done:
- `ActorSoundPreview` (Data): the sound an action makes on a class (A_PlaySound's or A_Taunt's
  first argument; deathsound for A_DeathScream, activesound, meleesound, attacksound for T_Shoot
  and the projectile actions) and its seesound going from Spawn/Path into Chase/See, played as the
  game picks (sound-seq, the best device it has). The window's Sounds box (off by default) plays
  them; the line under the preview says them either way.
- Edit picture… (Frame tab, More): the paused frame's sprite from the view picked in F6's pixel
  editor, or, a view it lacks, `ArtBrowserViewModel.NewPicture` blank under its name.
- Put it on the map (Used by, More): `MainWindowViewModel.PlaceClass` picks the class's thing in
  the things plane's palette; a class no mapdefs thing places says so.
- Left out: the "seen from the player" preview with distance shading. Matching the game would
  mean moving its shading (Lighting's fade tables and lighten levels) into Common first, a phase of
  its own; a preview that didn't match the game would go against the plan's first decision.
- Tests: ActorEditingTests (the sounds said for the Guard's Attack and Death with sounds off;
  SoundOf / SoundOfGoing; Edit picture's request for a sprite and a missing view; Put it on the
  map for a placed class and for Monster).
- Checked by screenshots: Put it on the map (the Guard picked in the things palette), Edit
  picture… (the pixel editor on GARDD1). Not tried: the sounds by ear (kept quiet on this
  machine), a missing view started blank and saved.
