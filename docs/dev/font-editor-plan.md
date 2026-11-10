# Font editor: browsing, styling and drawing fonts (editor)

Tools in the editor for the game's fonts: see every font and text style the game prints in,
with sample text drawn exactly as the game draws it, which characters each one has and what
uses it; change the styles in `fonts.yaml`; draw a Wolf-style font's letters; bring fonts in
and out of mods. The engine already loads every kind of font from pk3s and mods (the font
rewrite: `TextStyle` / `TextWindow`, per-pack `fonts.yaml`); today the editor has no font
window at all, and the only place it draws game text is the F2 text browser's plain
`ArticleScreen.DrawText`. Branch: not started (feature/font-editor suggested).

Decided (2026-11-10, by the user):
- **Previews are the game's own drawing.** A first phase moves font building and drawing out
  of the game project into PFWolf.Common, so the game and the editor share it.
- **A glyph editor for vga fonts only.** Graphic and sheet fonts are pictures, so their glyphs
  are drawn in the existing pixel editor (F6's), opened from the font window.
- **No TrueType/OpenType import.** Fonts come in as Wolf `.fnt` / WDC `.wsf` files and PNG sheets.

## The formats

**Font definitions.** `gamepacks/<pack>/fonts.yaml` (a mod's at its root), read into
`FontDefinitionsAsset` (name → `FontDefinition`, case-insensitive). A pack with a base-pack
starts from its fonts; a pack's or mod's entry replaces a font of the same name whole
(`mergeLevels: 1`). A name with no definition falls back to a vga font asset of that name, so
`SmallFont` and `LargeFont` work without one. Three types:
- `vga`: a Wolf font, one-bit glyphs drawn in the text's color. `source` = a font asset's name
  (a VGAGRAPH chunk or a `fonts/` file), defaulting to the definition's own name;
  `upper-case`, `cursor` (the glyph used for the typing cursor, character 128).
- `graphic`: a picture per character. `pattern` (`FONTN{code:000}`) + `chars`, and/or
  `glyphs` (character → picture), `advances` (a character with an advance and no picture is a
  blank), `cell-width`, `height`, `line-height`, `space-width`, `colorize`, `transparent`.
- `sheet`: one picture cut into cells. `image`, `cell-width` (required), `cell-height`,
  `chars` or `first-char`, `proportional` + `spacing`, `colorize`, `transparent`.
- Any type: `shadow`, `gradient` (vga and colorized only), `outline`, `glow`.

**A vga font asset.** Wolf3D's font chunk, and the same bytes as a `fonts/NAME.fnt` file: a
16-bit height, 256 16-bit glyph offsets, 256 byte widths, then each glyph's `height` rows of
`width` bytes, non-zero where it draws (`FontAsset`). Indexed by character code, so 0-255
only. The offsets are 16-bit, so all the glyphs together must fit in 32767 bytes less the
770-byte header. A WDC `.wsf` ("WSF1.0", 8-bit height, 256 widths, glyphs in order, no
offsets) is turned into one on load (`FontAsset.FromWsf`).

**What each game has** (VGAGRAPH font chunks, named in each pack's raw-data-map):
- Wolf3D (all releases), Spear, Spear demo: `SmallFont`, `LargeFont`. The status bar's numbers
  (`FONTN048`...) and the intermission letters (`FONTL048`...) are VGAGRAPH pictures, made
  graphic fonts (`StatusNumbers`, `IntermissionFont`) by `wolf3d/fonts.yaml`.
- Blake Stone (AOG, Planet Strike): `Font1`-`Font5`. Font1's glyphs sit 31 codes below their
  characters, so it can't print plain text; Font3 has capitals only. `blake/fonts.yaml` names
  `SmallFont` (Font5), `LargeFont` (Font2), `BlakeHud`, `BlakeNumbers`, `BlakeInfo`,
  `BlakeTitle`, `BlakeSmall` (cursor "@"), `Presenter1`-`4`, `StatusNumbers` (the `N_*` LED
  pictures); Planet Strike adds `PSHealthDigits` (`STFNG*` pictures).
- pfwolf.pk3 has no `fonts/` folder; examples/mods/fonts-demo shows every setting (a `.fnt`,
  two PNG sheets, LED digit pictures), and its Options → Font Gallery.

**Where a font is named** (what "Used by" has to read):
- game-info.yaml: the menu rows' font and per-row fonts, signon, several screens' text,
  puzzles' `title-font` / `font` (`GameInfoAsset`'s `Font` properties).
- menudefs: a menu's `font`, a Label's `font` (`MenuAsset`, `MenuMetadata`).
- hud-messages.yaml styles, statusbar.yaml / statusbar-fullscreen.yaml elements,
  intermission.yaml, presenter.yaml (Blake's text presenter).
- Lua: `game.drawtext` / `game.textwidth` calls with a font name (literal strings only).
- The engine itself by name: `SmallFont`, `LargeFont`, the menu font, `StatusNumbers`,
  `IntermissionFont`, `HudMessage`, and Blake's names.

## Where things go

- **PFWolf.Common, Fonts/** (phase 0): `Font`, `VgaFont`, `GraphicFont`, the effect records
  (`FontShadow`, `FontGradient`, `FontOutline`, `FontGlow`), and a `FontBuilder` holding what
  `FontManager.Build` does now (definition + asset lookup → font). Drawing goes onto an 8-bit
  surface (bytes, width, height, clip) with a color resolver (colors.yaml names, `#RRGGBB`,
  palette index → index) and the gradient / glow tables, which are palette work. The game's
  `FontManager` and `VideoManager` keep their APIs and call into it; `TextWindow` (layout,
  cursor) stays in the game. A shared `FontNames` lists the names the engine asks for, used by
  the engine and by "Used by", so the two can't drift.
- **PFWolf.Editor, Data/FontCatalog:** every font the game would find: each definition merged
  as the engine merges them, plus each vga font asset with no definition; its type, height,
  origins (data file, pfwolf.pk3, which mod), the characters it has, uses, and problems
  (missing pictures, unknown type or source, a sheet with no image, glyph keys longer than one
  character), as the engine logs them.
- **Font window** (Resources → **Fonts…**, Ctrl+Shift+F; the only F-keys left are F1, kept
  for help, and F10, Windows' menu key): FontBrowserViewModel / FontBrowserWindow, laid out like F8:
  list on the left, the font picked on the right.
- **Saving is mods only**, as everywhere else in the editor: `fonts/NAME.fnt` and pictures
  through `ModFiles` (`PickMod` for one of the game's own); definitions as changed keys in the
  mod's `fonts.yaml` through the Definitions window's machinery (`DefinitionSet` overlays,
  `YamlText` splices, so comments survive). A vga chunk font is replaced by a file of its
  chunk name (`fonts/SmallFont.fnt`), which the engine already prefers. pfwolf.pk3 stays
  read-only.

## Phases

0. **Font drawing in Common.** Before moving anything: snapshot tests drawing a sample line in
   every Wolf3D, Spear and Blake font, plain and with each effect, onto a buffer (bytes
   checked). Then the move (above). Done = the snapshots byte-identical, the full suite
   passing, and before/after screenshots identical: main menu, HUD, intermission, a Blake
   menu, the Fonts Demo gallery pages.
1. **Font browser (read-only).** The list: name, type, height, where it comes from, a mark on
   ones with problems. The font picked: a preview of sample text (editable; a default line and
   "every character") in a text color (palette or colors.yaml name), on a background (the menu
   background color, black, or a picture such as the status bar), zoom 1-6×; the character
   grid 0-255 with what's missing greyed and each glyph's width; its definition as YAML;
   Used by, with Go to (opens the Definitions window on the entry, or the Script browser on
   the line); problems. Export the vga font as `.fnt`; Copy name. Right-click menu like F8's.
2. **Styles.** Fields for every `fonts.yaml` key of the font's type, with shadow / gradient /
   outline / glow sub-editors, all redrawn live in the preview. New font… (vga from a source,
   graphic from a pattern or picked pictures, sheet from a picture), Duplicate as…, Take out
   (a mod's own; the game's comes back). Saved into a mod's `fonts.yaml` as changed keys;
   Edit YAML jumps to the Definitions window. Undo / redo and the `*` like the OPL editors.
3. **Glyph editor (vga).** A `FontDocument` (height, a width and pixels per character)
   and a glyph canvas: draw / erase, the glyph's width (dragging its right edge), shift
   up/down/left/right, flip, clear, copy and paste glyphs (between fonts too), "copy missing
   characters from another font", the font's height (rows added or cut at top or bottom for
   every glyph). A test line typed under the canvas, drawn with the font as it's being edited.
   New vga font (blank, or a copy of one). Save to mod as `fonts/NAME.fnt` (refused past the
   16-bit offset limit, saying by how much); a game chunk font saves under its chunk name and
   replaces it. A graphic or sheet font's glyph opens its picture in the pixel editor (F6's);
   a missing graphic glyph can be made there as a new picture named by the pattern.
4. **Bringing fonts in and out.** Add font to mod… (`.fnt`, `.wsf` turned into `.fnt`),
   optionally with a definition. Export a vga font as a PNG sheet with the `fonts.yaml` entry
   that reads it. Import a PNG sheet as a sheet font (the picture into `graphics/`, the cells
   guessed from the picture and shown over it to adjust) or as a vga font (pixels under a
   brightness threshold left out, proportional widths trimmed per glyph).

## Defaults (open to change)

- A font edited from the game's own is saved under the same name in the mod, replacing it
  everywhere (as art and sounds are); "Duplicate as…" is for a new name.
- Sample text: "The quick brown fox jumps over the lazy dog 0123456789", and for an
  upper-case font the same in capitals.
- Blake's Font1 is shown as it is (its glyphs 31 codes down), with that said in its problems,
  and no fix is offered.

## Open questions

- Characters past 255: the vga format can't hold them and the engine indexes by byte. Leave
  fonts at 0-255 (and say so), or give a font a code-page mapping for language files?
  Default: 0-255.
- Should "Used by" also show where text is drawn with no font named (an engine default)?
  Default: yes, as "the engine's default for …".
- Fonts bigger than the 16-bit offsets allow: refuse (default), or a PFWolf-only extended
  `.fnt` header the engine also reads?
- Should the art browser (F6) mark pictures that are a font's glyphs and link back to the
  font? Default: yes, in its Used by, once phase 1 is in.

## Status

Planned 2026-11-10.

**Phase 0 done 2026-11-10:**
- PFWolf.Common/Fonts: `Font`, `VgaFont`, `GraphicFont` and the effect records moved there
  (`git mv`, now public) and draw onto an `ITextCanvas`; the game's `VideoManager` is one
  (its pixel drawing unchanged), `IndexedTextCanvas` is the editor's (a picture of palette
  indices, one layout pixel each, colors resolved from colors.yaml / #RRGGBB / index as the
  game does). `FontColors` holds the gradient and glow palette math both use. `FontBuilder`
  holds what `FontManager` did (definition → font, effects, upper-case and cursor
  adjustments, `GlyphPictures`); `FontManager` is now its cache.
- Checked: the main menu, Options, the Fonts Demo gallery and its six pages screenshot
  pixel-identical before and after. Tests: FontDrawingTests (glyphs, shadow, outline,
  gradient, glow, colors, builder rules) and GameData FontGameTests (every font in the
  Wolf3D, Spear, shareware, Blake and Planet Strike fonts.yaml builds with no warning and
  draws). The snapshot tests planned first weren't needed: the drawing code moved unchanged,
  and the screenshots show it.

**Phase 1 done 2026-11-10** (the font window, read-only):
- `FontCatalog` (Data): every font, each fonts.yaml definition plus each font asset no
  definition names (Blake's Font1-5); `DefinedIn` (each fonts.yaml file defining it, with its
  lines, read from the asset's origins); uses: `FontNames.EngineUses`, fonts made from it, and a
  reflection walk (`FontReferences`) of game-info, hud-messages, statusbar(-fullscreen),
  intermission, presenter and every menu for string properties ending in Font, `Fonts` lists and
  `font` keys of string maps (menu components' params), with YAML key paths for Go to; Lua
  `game.drawtext` / `game.textwidth` string literals naming a font. `BuildFont` and `Notes`
  (Blake's Font1, unknown type).
- FontBrowserViewModel / FontBrowserWindow (Resources → Fonts…, Ctrl+Shift+F): list with type
  filter and ⚠ for problems; details; preview (text, color, background, zoom 1-8) through
  `IndexedTextCanvas`; the 0-255 character grid (missing cells dark, hover says code and width);
  problems (builder warnings, unknown colors, nothing names it); Used by with Go to (Definitions
  window `ShowEntry` at the field, or the script browser's new `ShowLine`); Defined in with the
  entry's YAML; Copy name, Export font file (.fnt).
- Manual page docs/manual/editor/fonts.md. Tests: FontCatalogTests, GameData
  FontCatalogGameTests (Wolf3D, Spear's fonts from wolf3d's file, Blake's chunks and presenter
  uses, the Fonts Demo mod: HudMessage defined in two files, its vga/graphic/sheet fonts build
  with nothing wrong).
- Checked by screenshots: HudMessage, SmallFont, IntermissionFont (upper-case glyphs, own
  colors), Used by / Defined in, Go to opening game-info high-scores at its Font field. Not
  tried in the window: Export .fnt's dialog, the Fonts Demo's fonts, a Lua use's Go to, Blake.
  Driving recipe: Avalonia's top menus have no UIA expand, so Ctrl+Shift+F went in as SendInput
  scancodes after checking the editor was the foreground window.

**Phase 2 done 2026-11-10** (editing styles):
- `DefinitionKind.Fonts`: fonts.yaml in the Definitions window's machinery (base packs replace a
  font whole, mods merge into it all the way down, as the loader does; a mod's root
  fonts.yaml), with a Fonts tab. `FontSchema` (Common/Definitions): FontDefinition's fields and
  doc comments, type as a choice, pictures/fonts/colors hinted, `AppliesTo(key, type)` — both
  windows show only the keys a font's type reads (plus any set).
- `FieldTree` (Views): the field rows, now shared by the Definitions window's tabs and the font
  window. FieldNode fix: a record written empty (`shadow: {}`) builds back as `{}` (it was
  dropped, losing the shadow).
- The font window: preview at the top, tabs Style / Characters / Used by / Defined in. Style
  edits draw live (the edited definition built through FontBuilder); undo/redo (a field's typing
  one step; a type change shows the new type's fields); Save (into the mod defining it, else one
  picked; just what changed; the game reloads), Revert, New font… (a copy of the one picked; a
  vga copy names its source, which otherwise defaults to the new name), Take out the mod's
  change…, Open in the Definitions window. Unsaved fonts join the editor's "Save first?".
- Tests: DefinitionSetGameTests (fonts merge == loaded for Wolf3D, Spear, Blake, Planet Strike,
  the Fonts Demo; a change saved is just what changed), FontStyleEditingTests (draw/undo/save,
  a new font saved whole, type-specific fields), FieldNodeGameTests covers fonts.
- Checked by screenshots: the Style tab on HudMessage, Shadow Y set (preview redrawn, ●, Save
  on), Revert, the Definitions window's Fonts tab. Not tried by hand: Save from the window, New
  font…, Take out, the "Save first?" prompt.

**Phase 3 done 2026-11-10** (the glyph editor):
- `FontAsset.Write` (Common): a Wolf3D font file from a height and 256 glyphs, refused (by how
  many bytes) when a glyph would start past the 16-bit offsets; `FontAsset.Glyph(ch)`.
- `FontDocument` (Editing): the font's height and each character's width and pixels (drawn
  pixels get the font's own ink value, Wolf3D's 1); strokes as one step, SetWidth (at the right
  or left), Shift, Flip, Clear, Remove, Copy/Paste (`GlyphData`, rows from the top),
  CopyMissingFrom, SetHeight (at the bottom or top); whole-state undo/redo; IsDirty; ToFile.
- `CharacterGrid` (Rendering): the 0-255 grid both windows draw (one cell can be marked).
- GlyphEditorViewModel / GlyphEditorWindow: the grid (click picks), the glyph on the pixel
  editor's `PixelCanvas` (left draws, right erases), width/move/flip/clear/take out, the font's
  height, copy/paste (a clipboard shared by every glyph editor), "the characters it hasn't got,
  from" another font, a line of text drawn in it as it is, Save to mod… / Export .fnt…, close
  prompt. The width is set by numbers and buttons rather than by dragging the right edge.
- The font window's Characters tab: Edit glyphs… (a vga font's source asset), New font file…
  (a copy of the picked vga font's letters, or just a space, 10 high), Edit glyph picture… (a
  click on the grid picks the character; a graphic font's picture for it, a sheet's sheet, in
  F6's pixel editor). Saving: fonts/NAME.fnt into the mod whose file it is (a .wsf of the name
  there taken out), else one picked; then the game reloads.
- Tests: FontDocumentTests (round trip, strokes, each change, undo, paste between heights,
  copy missing, height, the size limit), GameData FontDocumentGameTests (every Wolf3D, Spear,
  Blake and Planet Strike font chunk comes back the same; SmallFont drawn, saved, and the game
  then reads the mod's; IntermissionFont's 'a' opens FONTL065).
- Checked by screenshots: SmallFont in the glyph editor, B picked by a click on the grid, a
  pixel drawn by a click (*, Undo on). Not tried by hand: saving from the window, the other
  tools by mouse, New font file…, Edit glyph picture… opening the pixel editor.

**Phase 4 done 2026-11-10** (fonts in and out); every phase of the plan is done:
- `FontSheets` (Data): `Export` (a vga font's characters 32-255 as a 16-column PNG, white on
  see-through, and the fonts.yaml entry reading it: a colorized sheet with `advances` for each
  width that isn't the cell's — draws pixel-identical to the vga font); `Guess` (of the cell
  sizes 4-64 dividing the picture, a usual character count in 16 columns first, else cells
  nearest 1.3 tall to wide); `ToVgaFont` (a brightness threshold, a see-through color, glyphs
  trimmed plus a gap); `SheetDefinition` (first-char or `chars`, proportional, colorize,
  `transparent`); `ToPicture` (a picture as the game reads a PNG); `CornerColor`,
  `HasTransparency`. `SheetCells` takes characters in order for a sheet not in code order.
- The font window's Files menu: Add font file to mod… (.fnt/.wsf → fonts/NAME.fnt through the
  glyph save), Bring in a sheet… (SheetImportWindow: cells guessed and drawn over the sheet,
  first character or characters in order, the corner's color see-through, sheet font —
  proportional, colorize; Save to mod… writes graphics/NAME.png and the fonts.yaml entry — or vga
  font — threshold, gap; Open in the glyph editor…), Export font file (.fnt)…, Export as a PNG
  sheet… (the entry to the clipboard).
- Tests: FontSheetsTests (TinyFont exported draws the same; read back as vga; guesses incl.
  FDGOLD 13x17; FDGOLD's magenta left out and its characters listed), GameData
  FontSheetsGameTests (SmallFont, LargeFont, Blake's Font2 as sheets draw the same; a .fnt
  added to a mod is printed in by name; a sheet brought in as a sheet font saved and loaded, and
  as a vga font opened).
- Checked by screenshots: the import dialog on FDGOLD (first a wrong 16-column guess and the
  magenta drawn, which led to the better guess, the see-through corner and the characters
  list), then as a vga font. Not tried by hand: the saves from the dialog, Add font file, the
  exports' dialogs. UI recipe: an Avalonia MenuFlyout item doesn't run its Click from a UIA
  Invoke; post a click at it to WindowFromPoint (scratchpad clickname.ps1); its popup's
  elements are listed under the owner window.
