# Menu editor: seeing and laying out menudefs menus (editor)

A menu window in the editor that draws a menudefs menu the way the game draws it and redraws it
on every change, whether the change is typed as YAML, made in fields, or dragged on the
picture. Today menudefs can only be changed as raw text in F11's YAML tab (`YamlFileTypes`
checks it against `MenuAsset` and saves it into a mod). To see what a change looks like you
have to run the game. Branch: not started (feature/menu-editor suggested).

Decided (2026-11-17, by the user):
- **The preview is the game's own drawing.** Phase 0 moves the menu model, the fit-items
  layout and the component and row drawing out of the game project into PFWolf.Common, so the
  game and the editor share them (the font editor's and actor editor's phase 0 did the same).
- **Preview, then fields, then dragging.** Live preview first. Then field editors for the
  menu, its components and its items, saved as mod overlays. Then selecting, moving and
  resizing things on the picture itself.
- **Code-drawn parts get sample stand-ins.** `items-source` rows are the real ones from
  game-info. Slider levels, choice values and checkboxes get sample values. The screens the
  game builds in code (load/save slots, Customize rows, the Mods list, the lobby) get
  placeholder rows. Toggles control the states that change what's shown (in a game or not,
  checkbox on or off, which row the cursor is on).

## The format (what the preview has to draw)

**Files.** `pfwolf-pk3/menudefs/NAME.yaml` is every pack's menu. `menudefs/<pack>/NAME.yaml`
(blake/, planetstrike/) is used in its place by that pack and the packs built on it
(`AssetManager.GetMenu`, `GetMenuPackChain` with `menus-like`). A mod keeps its menus directly
in `menudefs/`. A file with an existing menu's name is laid over the menu the running pack uses
(`MenuAsset.MergeYaml`): top-level keys replace the menu's, and `menu-items` and `components`
merge entry by entry by `id` (a Customize row by `control`), with `remove: true`,
`before:`/`after:`, and `!replace` for the whole list. A new name is a new menu, reached by a
`CP_Menu` item's `menu:`.

**A menu** (`MenuAsset` → `MenuMetadata` in the game): `position` (x, y), `indent`,
`row-height` (13), `visible-rows`, `font` (LargeFont), `item-shadow`, `cursor` (Blake's
flashing bar in place of the gun: x, y-offset, width, height, color, erase-color,
flash-tics), `checkbox-offset` (-24, 3), `items-source` (episodes, skills, player-classes,
mods, lan-games), `spacer-rows`, `default-selection`, `selection-pic`, `stats-panel`,
`fit-items` (rows, min-rows, max-rows, grow, move), `music`, `type` (unused).

**Components**, drawn in order: `Background` (color, or game-info `menu-backdrop` with the
color round it on a wide screen), `Window` (bar + BORD2COLOR/DEACTIVE outline), `Fill`,
`Graphic` (name, x, y, horizontal/vertical-orientation: an orientation overrides that axis),
`Stripe` (y, backing-color, line-color; its shape is game-info `menu-stripe`), and `Label`
(text or $language key, x, y, width, orientation, color, font, and the
shadow/gradient/outline/glow overrides). Each component and item can have `game-packs` (with
`*` and `!pack`) and an `id`.

**Items:** `MenuSwitcher` (action: a CP_ name, or CP_Menu / CP_Article / CP_Lua /
CP_TextScreen with menu / article / function / script), `ToggleMenuItem`, `ChoiceMenuItem`,
`SliderMenuItem`, `MusicMenuItem`, `ControlMenuItem`, `HeaderMenuItem`, `BlankMenuItem`; text,
short-key, enabled, highlighted (READCOLOR), game-packs.

**What game code adds on top** (the stand-ins): the gun cursor (`c_cursor1`, unless game-info
`menu-cursor: none`) or the cursor bar; the colors for disabled/normal/highlighted/read rows
(`MenuDrawing.colorHlite/colorNorml`); checkboxes (`c_selected`/`c_notselected`, `_hi` under a
bar); sliders and choice values at `MenuSliderOffset`; scroll marks; the selection picture; the
stats panel (`LevelStatsPanel`); the main menu's Back to Demo/Game row; and whole screens
drawn in code over their menu's components: load/save slots, Customize rows, the Mods list,
join-game/lobby/net-rules, the jukebox, Blake's high scores.

## Where things go

- **PFWolf.Common, Menus/** (phase 0, done), namespace `PFWolf.Menus`:
  - The model (`MenuMetadata.cs`): `MenuMetadata` and `BuildFromAsset`, the component records,
    the item records and the orientation enums, made public. `Window`, `Label`, `Graphic` and
    `Fill` clash with Avalonia's controls, so the editor uses an alias
    (`using MenuWindow = PFWolf.Menus.Window`) or the full names. Renaming them would change
    the YAML `type:` names, which must not change.
  - The rows (`ControlPanelItem.cs`): `ControlPanelItem` and `ControlPanelMenuInfo` moved
    whole (still in namespace `PFWolf`), so the painter draws the game's own rows; the editor
    makes `ControlPanelItem`s with no routine.
  - The layout: `MenuFitting` (`MenuFit`, `Fit`, `Fitted`, `GrownRight`) and `MenuRows`
    (`FromItems`, `Layout`: the part of `MenuDefs.LoadMenu` that builds rows from menu-items and
    works out the position, row height, scrolling, font, cursor and fit-items).
  - The drawing: `MenuPainter` does what `GraphicManager.DrawComponent`, `DrawMenuBackground`,
    `DrawStripe`, `MenuDefs.DrawLabel` (and the `Label*` effect rules), and `MenuDrawing`'s
    `DrawMenu`, `PrintMenuItem`, `DrawMenuBar`, `DrawMenuCheckbox`, `DrawMenuSlider`,
    `DrawMenuChoice`, `DrawMenuScrollMarks`, `DrawMenuGun`, `DrawCustomArrow` and the
    window/outline helpers did. It draws on an `IMenuCanvas : ITextSurface` (bar, text, fill
    screen, a band across the whole screen, pictures by name, margins, the backdrop and stripe).
    `GraphicManager` is the game's (`graphics.Menus` is its painter) and `IndexedMenuCanvas` the
    editor's (a palette-index picture with the 320x200 layout centered in it). `MenuDrawing` and
    `GraphicManager` keep their APIs and call into it.
  - `StatsPanel` (the stats panel's `Text` and `Draw`) and the `LevelStats` record; working out
    the bests from records.cfg stays in the game's `LevelStatsPanel`.
  - In Common/Fonts: `TextStyle` and `TextWindow` moved there. `TextWindow` prints on an
    `ITextSurface` (bar, draw text, measure text), and `TextDrawing.Draw` holds the style-over-font
    rules `GraphicManager.DrawText` had.
  - Phase 1 added `MenuNames` (the menus the engine opens, the actions and the menus each opens,
    the items-sources: `MenuNamesTests` checks them against the game's code and pfwolf.pk3) and
    `LanguageStrings` (the $NAME lookup, which the game's `GetText` now uses too).
- **PFWolf.Editor, Data/MenuCatalog:** every menu the running game pack would find: the shared
  menu or the pack's own, with the mod overlays merged by `MenuAsset.MergeYaml`. For each one:
  where it's defined (file and line per layer), which menus open it (`CP_Menu` items) or "the
  game's code" (the names the game asks for, a shared `MenuNames` list), and its problems as the
  game would log them (unknown component or item type, unknown param, unknown action, missing
  picture/font/music, a `CP_Menu` to a menu that doesn't exist, default-selection out of range,
  fit-items naming ids that aren't there).
- **PFWolf.Editor, Rendering/MenuPreview:** builds the picture: `MenuMetadata` + the game pack
  (for game-packs filtering) + stand-ins → `IndexedMenuCanvas` → bitmap, plus a hit map (which
  component or row is under each pixel) for clicking and dragging.
- **PFWolf.Editor, Rendering/MenuSamples:** the stand-ins, keyed by menu name and item type:
  sample values for sliders, choices and toggles, the main menu's in-game state, placeholder
  rows for the code-built screens, and the stats panel with made-up bests. The real
  items-source rows come from game-info.
- **Menu window** (Resources → **Menus…**, Ctrl+Shift+M, free today): MenuBrowserViewModel /
  MenuBrowserWindow, laid out like the font and actor windows. On the left, the menu list
  (search; filter: all / the pack's own / a mod's / with problems), showing each menu's name,
  where it's from and "opened by". In the middle, the preview at 2x/3x/4x (nearest neighbour),
  with a 320x200 or Hor+ wide toggle (the game's widths), a game pack picker, and the stand-in
  toggles. On the right, tabs: **Fields**, **YAML**, **Used by** / **Problems**.
- **F11's YAML tab** gets a preview pane for menudefs files too: the same `MenuPreview`, redrawn
  from the text as you type (after a short pause), with the merged result shown when the file
  is a mod's overlay. That gives a preview straight away for the way menus are edited now.

## Phases

### Phase 0: menu drawing in Common

Move the model, layout and painter described above. Nothing the game draws changes.

Gate:
- A snapshot test (Content, like `ActorStateSnapshotTests`) draws every menu of every game
  (wolf3d, wolf3d-apogee, wolf3d-shareware, spear, spear-demo, blake, blake-shareware,
  planetstrike, multiplayer.pk3, every example mod with menudefs: fonts-demo, the showcase
  mods) onto an `IndexedMenuCanvas` and writes each picture's hash. Set
  `PFWOLF_MENU_SNAPSHOT=<file>` to dump it before and after.
- The game's menu screens are pixel-identical before and after (the drive.ps1 recipe:
  PostMessage keys + PrintWindow), at least main, options, sound, video, control,
  new-game/episode/skill, the Blake main menu and the Font Gallery pages.
- `MenuScreensTests`, `MenuHooksTests` and the rest of the suite pass; the demo tests are
  unchanged.

### Phase 1: read-only menu window + live preview

- MenuCatalog, MenuPreview, MenuSamples, the menu window with list, preview, YAML (read-only
  here), Used by and Problems.
- The preview behaves like the menu: Up/Down/Home/End and short-keys move the cursor (skipping
  rows that can't be picked, as `HandleMenu` does), `visible-rows` scrolls with scroll marks,
  `selection-pic` follows the cursor, the cursor bar flashes at `flash-tics` (a pause toggle),
  and fit-items is shown at the game's real item count, with a "pretend N items" spinner to
  try it at other counts.
- Clicking a row or component selects it (outlined on the picture) and shows its YAML lines.
  Double-clicking a `CP_Menu` item opens that menu. A `Graphic` opens in F6, a font in the font
  window, music in F8.
- F11's YAML tab preview pane for menudefs files.
- Manual: docs/manual/editor/menus.md. Manual testing: a new 2.x section.

### Phase 2: fields + saving

- `DefinitionKind.Menus` in the Definitions window (a Menus tab) and in the menu window's
  Fields tab, using the shared `FieldTree`. `MenuSchema` comes from the `MenuAsset` records'
  doc comments (`FieldSchema`). Component `params` get a hand-made schema per component type
  from the `PFWolf.Menus` component records, since params are a list of one-key maps.
- Pickers: actions (`MenuNames.Actions`, in Common since phase 1), menus (CP_Menu), articles (texts), Lua functions (script
  globals, as the script browser lists them), pictures, fonts (FontCatalog), colors (colors.yaml
  names + palette), music, controls (controls.cfg names), game packs.
- Add / delete / reorder items and components (up/down, or drag in the list). Undo/redo
  snapshots, as the font Style tab does.
- Saving writes a mod's `menudefs/NAME.yaml` (`ModPathFor` already maps it) as an overlay in
  MergeYaml's terms, with a new `DefinitionSet` replay rule for menus (MergeYaml, not
  `YamlTree` levels):
  - top-level keys that changed are written as they are;
  - a changed item or component with an `id` (or `control`) is written as that entry with only
    its changed keys (a component's params whole, as MergeYaml replaces them);
  - a new entry gets `before:`/`after:` its neighbour's id; a moved one is written with its
    `before:`/`after:`; a deleted one becomes `id: x` + `remove: true`;
  - changing, moving or deleting an entry with no id (main-menu's Windows have none) can't be
    keyed. The editor offers to write the whole list `!replace`, naming what that costs (the
    mod then misses later pfwolf.pk3 changes to that list).
  - a mod's own new menu is written whole.
- **New menu…**: blank, or a copy of a menu as a mod's own new menu under a new name,
  optionally adding a `CP_Menu` item that opens it to a menu you pick (options, say).
- Unsaved menu edits join the main window's "Save first?" (`ResolveUnsaved`).
- Round-trip test: every menu of every game through FieldNode and back gives the same
  `MenuAsset`. Overlay tests: an edit saved as an overlay and merged by the loader gives the
  edited menu, for keyed, before/after, remove and !replace cases.

### Phase 3: laying out on the picture

- Select on the picture (click; Tab cycles through what's under the mouse), with the selection
  outlined and its fields shown.
- Drag to move: components (x, y); the item block (position, so every row moves); the cursor
  bar (x, y-offset); selection-pic; the stats panel. An axis set by an orientation (a Center
  Graphic or Label) stays locked, with a hint; Alt-drag switches that axis to an explicit x/y.
- Handles to resize Windows, Fills, the stats panel, the cursor bar and a Label's width;
  dragging between rows changes `row-height`, and the indent handle changes `indent`.
- Arrow keys nudge 1 px, Shift+arrows 8 px. An optional 8 px grid / snap, and guides at the
  screen's centre and the 320x200 edges on a wide preview.
- Each drag is one undo step and goes through the same writes as phase 2, so it saves the same
  way.

### Phase 4: extras

- Show fit-items' grow/move components and min/max rows on the picture while the spinner
  changes the count.
- A "Test in game" button: starts the game with the mod and opens that menu, using a new
  `menu NAME` console command or `--menu NAME` switch (needs a small engine change, so it's
  last).
- Compare: the menu as pfwolf.pk3 has it next to the mod's version, or a flicker toggle.
- Export the preview as a PNG.

## Open questions (later, not blocking phase 0)

- Whether the Customize, load/save and lobby stand-ins should later be drawn by the game's own
  screen code moved into Common too. They're large and code-driven, so the plan uses
  placeholders.
- Whether a Lua `onhud`/menu hook drawing should ever show in the preview (it can't without
  running scripts, so it's left out).

## Status

Plan written 2026-11-17.

**Phase 0 done (2026-11-17).** What moved is under "Where things go". The check is
`MenuDrawingSnapshotTests` (GameData: needs the built x64 game folder, or PFWOLF_GAMEDATA). It
draws every menu of wolf3d, wolf3d-shareware, spear, spear-demo, blake, blake-shareware,
planetstrike, multiplayer-pk3, each example mod with menudefs and a stats-panel probe mod. It
draws them with the game's code (MenuDefs, MenuDrawing, a VideoManager on an 8-bit SDL surface)
on 320x200, 384x200 and 640x400 (2x) screens, plus a pass with the cursor on the last row. Each
menu gets sample checkboxes, sliders and choices, the gun or bar, and the stats panel.
- Set `PFWOLF_MENU_SNAPSHOT=<file>` to write the hashes, and `PFWOLF_MENU_PICTURES=<folder>` for
  .bmp pictures.
- The snapshot taken before the move and the one after are identical (222 lines). The
  stats-panel probe was added after the move, so the panel's before/after rests on its code
  being moved unchanged.
- The test also draws each 1x screen with Common alone (`MenuPainter` on `IndexedMenuCanvas`,
  fonts from `FontBuilder`) and fails unless every pixel matches the game's. It passes for every
  menu: the editor's preview will be the game's picture.
- wolf3d-apogee's data files aren't on the dev machine, so it isn't in the snapshot.

**Phase 1 done (2026-11-17).**
- PFWolf.Common: `MenuNames`, `LanguageStrings`, and `MenuMetadata.BuildFromAsset(asset, warn)`,
  which hands its warnings to the editor instead of the console.
- Editor `Data/MenuCatalog`: every menu by the name the game opens it by, its asset (the pack's
  own or the shared one), and its files from the asset origins. `Merge` replays them as the
  loader does: pfwolf.pk3's replaces, a mod's is laid over with `MenuAsset.MergeYaml`, and a
  file that doesn't read is left out. Any file's text can be swapped in for the live preview.
  It also works out Opened by (the engine, game-info high-scores frames, other menus' CP_Menu
  items), `Reached` (from main-menu and the jukebox by each menu's items in the running pack)
  and `Problems`.
- `Rendering/MenuPreview` + `MenuSamples`: the picture (`IndexedMenuCanvas`), plus where each
  part went, for picking.
- `MenuPreviewModel` / `MenuPreviewView`: the preview control both windows use (zoom, wide, in
  a game, checkboxes, flash, rows, keys, click and double-click).
- `MenuBrowserViewModel` / `MenuBrowserWindow`: Resources → Menus…, Ctrl+Shift+M.
- F11's YAML tab: `YamlFilesViewModel.ShowMenu` draws a menudefs file's menu beside it, from the
  text as it stands. `DefinitionsViewModel.ShowYamlFile` is what the menu window's Edit… calls.
- Manual: docs/manual/editor/menus.md. Manual testing: 2.20.

Checks:
- `MenuCatalogGameTests`: every menu of 7 games, multiplayer-pk3 and the example mods, merged
  again, equals the loaded asset; the menus the game gets to have no problems; a mod's unsaved
  overlay is laid over; a bad CP_Menu and action are problems.
- `MenuDrawingSnapshotTests` now also requires the preview of each plain menu (menu-items with
  no values, pictures or stats) to equal the game's pixels, at 320 and 384 wide.
- `MenuNamesTests`: the actions and items-sources are the ones the game registers, and the
  menus the engine opens are in pfwolf.pk3.
- Driven in the editor (Wolf3D, a scratch mod with live typing, Blake); see manual-testing 2.20.

Not as planned:
- The code-built screens (saved games, Customize rows, lobby) get a note under the picture, not
  drawn placeholder rows. Mods, network games and the level select get sample rows.
- Double-click opens menus and pictures only, not fonts or music.

**Phase 2 done (2026-11-17).**
- Common: `MenuSchema` (MenuAsset's fields with item and component types, actions and
  items-sources as choices; one params record of every component type's, `ParamAppliesTo`) and
  `FieldHint.Menu` / `FieldHint.Article`.
- Editor `Editing/MenuEditing` (a component's params, a list of one-key mappings, as one mapping
  to edit and back, keeping their order), `Editing/MenuOverlay` (the overlay in MergeYaml's terms:
  changed keys, entries by id/control with only changed keys, `remove: true`, `before:`/`after:`
  only where the order needs one, `!remove` for keys taken out, `!replace` for a list whose
  id-less entries changed; every overlay checked by laying it over), `Editing/MenuSaver` (the
  mod's own file of the menu or `menudefs/NAME.yaml`, laid over the files before that mod's,
  checked by `MenuCatalog.Merge` reading it back, the file's top comments kept; pfwolf.pk3
  refused). A save that writes a list whole asks first, naming what that costs.
- `MenuBrowserViewModel.Fields`: the Fields tab (the shared `FieldTree`), undo/redo snapshots,
  Save/Revert, a part picked on the picture opens its entry, New menu… (a copy, or `+name` blank,
  plus a CP_Menu row on a menu named), unsaved menus in `ResolveUnsaved`.
- Checks: `MenuOverlayTests` (10 cases), `MenuSavingGameTests` (every menu of wolf3d, spear,
  blake and planetstrike through the fields and back unchanged; an edit and a new menu saved into
  a scratch mod read back as edited; pfwolf.pk3 refused). Driven: an indent change saved into a
  scratch mod (only `indent: 80` added, its existing entry kept, the game reloaded with it); a
  click on the picture opening the Window's entry; New menu… (+my-test from options) drawn, with
  the options row added.

Not as planned:
- No `DefinitionKind.Menus` / Menus tab in the Definitions window: a menu is a document of its own
  merged by entry id, which DefinitionSet's key-path writes don't fit. F11's YAML files tab
  (live preview since phase 1) is the Definitions window's way to edit menus.
- Pickers: menus, texts, pictures, fonts, music, actions, types and items-sources; not Lua
  functions, controls or game packs (typed).

**Phase 3 done (2026-11-17).**
- Editor `Editing/MenuLayoutEdit`: a part dragged by (dx, dy) in a mode (move, width, height,
  corner, row spacing) as changes to the fields' menu: components' params (an oriented axis kept
  unless unlocked, which drops the orientation for an x/y where it is), the rows' position, indent
  and row-height, the cursor bar, selection-pic (made where the picture is), stats-panel; snapping
  to 8. Components are found as the menu reads them (unknown types skipped).
- `MenuPreviewModel` drags (`IMenuLayoutTarget`: drawn as dragged, one change on drop), edge
  zones for resizing, pointer shapes, Ctrl+arrow nudges, Esc, Snap to 8, guides while dragging.
  Only the menu window has a target; F11's preview still only picks.
- `MenuBrowserViewModel.Layout`: the target. A drag works from the fields as they were when it
  started and becomes one undo step through `ShowFields`; nudges a second apart merge.
- Checks: `MenuLayoutEditTests` (7). Driven on sound with a scratch mod: the Window dragged +20,
  +10 and its bottom edge −10 (two steps, Ctrl+Z undid the second alone), a row dragged moving
  `position` 24,62 → 14,72.

Not as planned: no Tab to cycle through the parts under the mouse; row spacing is a Shift-drag
on a row rather than handles between rows; the guides show only while dragging.

**Phase 4 done (2026-11-17): every phase is done.**
- Game: the `menu NAME` console command (GameHost.Ui `MenuCommand`) sets
  `ControlPanel.PendingMenu`, which the control panel opens before the main menu: an engine menu
  by the action `MenuNames.ActionMenus` says opens it (`MenuDefs.Action`), any other as a
  CP_Menu row does (`MenuDefs.SubMenu`). With `--nowait` it's at once; Esc goes on to the main menu.
- Editor: `GameLaunch.MenuArguments` and the menu window's Test in game (F5; a changed menu saved
  first if wanted, a new one has to be); "As pfwolf.pk3 has it" (`MenuBrowserViewModel.Compare`:
  the base layers' menu in the picture, no dragging meanwhile); Save picture… (the picture at its
  zoom, `PngFile`); fit-items' line under the picture and, with Rows set, its grow (green) and move
  (orange) components outlined (`MenuPreviewModel.MarkFit`).
- Checks: `MenuNamesTests` (MenuArguments; every engine menu but main-menu and the jukebox has an
  action that opens it). Driven: the game with `--exec "menu sound"` on Sound, Esc to the main
  menu; in the editor, compare on a mod's sound, Rows 7 on new-game (the window outlined green and
  grown, the title orange), F5 on new-game starting the game (its episode screen first).
- Not tried by hand: Save picture… (its file dialog), Test in game on a mod's own menu and with a
  changed menu (the save question).

Found: Blake Stone's and Planet Strike's jukebox, new-class and new-level are the shared menus,
whose key legend picture (`c_mouselback`) Blake hasn't got. The game draws nothing there. The
window shows them with ⚠, and `MenuCatalogGameTests` lists them as known.
