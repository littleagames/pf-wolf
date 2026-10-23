# Raw data mods plan: playing WDC/ChaosEdit data files with a generated raw-data-map

## Status (2026-10-22): phases 1–4 done, 5–6 not started

This takes over phases 1–2 of the classic-mod-import plan (the 2026-10-12 discussion of a one-way
import into a mod), which becomes optional phase 5 here. Branch `feature/editor/raw-mod-data`.

- **Phases 1–2** (09fcf1db): mod-folder data files, md5 warning, `DataFileChunks`,
  `RawDataModGenerator.Match` / `MatchSection` / `DetectBase`.
- **Phase 3** (cb1d7643): `RawDataModWriter`, `PFWolf --make-mod`.
- **Phase 4**: the editor's File → New mod from data files…, re-runs keeping names (also
  through `--make-mod --overwrite`), the art browser's Show → Needs a name and Name…, and the
  level check for unnamed walls.
- **Checked:**
  - Tests: `RawDataModTests` and `RawDataModGeneratorTests`, 21 of them, most needing the WL6
    files.
  - By hand: `--make-mod` on renamed copies of the WL6 VSWAP/GAMEMAPS/MAPHEAD, then all 4 Wolf3D
    demos play headless with the made mod loaded.
  - On screen (2026-10-22, driven through UI Automation): a WL6 VSWAP with two walls put in
    before the doors.
    - The dialog finds the base and makes the mod, and the report is right.
    - The mod's game loads, with wall 50 in the palette.
    - Show → Needs a name lists both new walls; Name… renamed one, and the files and browser
      followed.
    - F7 warned about the unnamed one on a painted tile.
    - Running the dialog again into the same folder asked to write over and kept the name.
  - Fixed after looking: the mod folder label didn't wrap; "100%" showed for 2 new walls in about
    600 chunks (now "over 99%"); the report didn't wrap; the game list read "Bunker TC
    (Bunker TC)"; the art browser called a new chunk "the game's own". Also added a From data
    files… button beside New mod…, and a typed folder path is looked at as soon as it exists.
- **The mod's own palette (2026-10-23):** optional in the dialog (Palette, Browse…, with how it
  read and a swatch of its colors) and as `--make-mod --palette <file>`.
  - Left empty, the one palette file in the data folder is used if there is exactly one.
  - Read by `PaletteFile.Read` (moved to PFWolf.Common from the editor's PaletteCatalog): raw
    768 bytes, 6-bit VGA or 0-255, Adobe .act, JASC-PAL and GIMP .gpl.
  - Written as `palettes/<the base game's game-palette>.pal` (wolfpal), and named in the report.
  - Checked on screen with a red/blue-swapped JASC palette: picked up by itself, swatch shown,
    the mod's game drawn in it. `--make-mod` picked it up too.
- **Not checked:** see the "(never tried)" items in docs/dev/manual-testing.md 1.14 and 2.17.
  - No real WDC/ChaosEdit mod has been tried, and no Spear, Blake or Planet Strike files have
    been through the generator.
  - The dialog's Browse… folder picker hasn't been tried.
  - Pics added to VGAGRAPH are only tested on made-up parts (no test VGAGRAPH writer), so the
    `alias.yaml` remapping hasn't run on real files.
- **Differences from the phases below:**
  - **No game-info.yaml stub:** a mod game inherits the base game's game-info, so the exe-side
    to-do list is in the report (`raw-data-mod.txt`) instead.
  - **The levels' pair stays at the mod's root:** the file-pack names it and it's looked for in
    the mod folder first, so it isn't moved to `maps/`.
  - **New wall ids** are written only for page pairs that are new, moved or changed, past the
    base game's walls and outside its door and floor codes.
  - **Base packs resolve by pack name:** `base-pack: blake` / `planetstrike` now find the
    blake-aog / blake-ps entries (`GamePackInfoAsset.FindBasePack`), so a mod game on Blake gets
    its whole base-pack chain.
  - **Tied base detection:** with only files the Wolf3D releases share (VSWAP, GAMEMAPS), wolf3d
    and wolf3d-apogee tie at 100% and wolf3d is picked. `--base` or the dialog changes it.
  - **The sound browser has no Name… yet:** sounds and music still have to be renamed in
    raw-data-map.yaml by hand.
  - **No test writers for VGAGRAPH or AUDIOT.** VSWAP's is in the tests (`TestVswap`).

Goal: a classic mod made by editing the raw data files (VSWAP, VGAGRAPH/VGAHEAD/VGADICT,
AUDIOHED/AUDIOT, MAPHEAD/GAMEMAPS, with WDC or ChaosEdit) plays in PFWolf as a mod, without
extracting its assets. A generator reads the files, compares them with the base game's, and
writes the mod's raw-data-map and the files that go with it:
- Chunks it recognises keep their stock names.
- New chunks get placeholder names and a comment asking the modder to name them.

The modder fills those in and plays. This works for Wolf3D, Spear, Blake Stone and Planet Strike
mods alike, since they share the file formats.

## Decisions (2026-10-22)

- **The generator runs in the editor and from the engine's command line.** One
  `RawDataModGenerator` in PFWolf.Common. The editor gets a "New mod from data files…" wizard.
  The engine gets `--make-mod <data folder>`, which writes the mod and exits, for modders who
  don't use the editor.
- **Folder mods first, pk3s later.** Phases 1–4 read data files from a mod folder. Phase 6
  extracts a pk3's data files to a cache under the config folder.
- **The data files keep the names they were shipped with** (VSWAP.WL6). The file-pack looks in
  the mod's folder first. A file the mod doesn't have (it only changed VSWAP) comes from the game
  folder.
- **A changed chunk at a stock position keeps the stock name**, with a "changed" comment (a new
  guard drawn over GARDA1 stays GARDA1, so actordefs still use it). Only chunks past the stock
  counts get placeholder names.
- **md5s are written, and a mismatch only warns.** The generated file-pack records each file's
  md5. A file edited again without re-running the generator gives a startup warning ("…has
  changed since its raw-data-map was made: run the generator again") and still plays. No
  `strict-md5`.
- **A raw data mod is its own game.** Its root `gamepack-info.yaml` adds a game (id = the mod's
  name, slugged) with `base-pack: wolf3d` (or spear, blake, …) and its own `file-pack`. This works
  today, and gives the mod its own settings, saves and high scores, as a TC should.
- **The base game is detected, then confirmed.** The generator guesses from the file extensions,
  chunk counts and how many chunks match each stock release by hash. The wizard shows the guess
  and lets the modder change it, and the command takes `--base <pack>` to override it.

## Where things stand

Checked against the code on 2026-10-22.

- **A mod can already add a game with its own data files.** `ModSource.Games` reads a mod's
  root `gamepack-info.yaml`. `GamePackInfoAsset.WithModGames` adds its packs, and
  `GameTypes.PickGame` selects one (`GameSelection.ModGame`, `Type` null). The engine branches on
  pack data, not on `GameType`: only `GameEngineManager` reads `Game.Type`, for old settings. So
  a mod game built on `blake` plays like Blake.
- **`GetFilePack` takes the nearest file-pack along the base-pack chain**, so a pack built on
  `wolf3d` with its own file-pack plays those files and everything else (mapdefs, actordefs,
  game-info) from wolf3d.
- **Data file names resolve against the game folder.**
  - `AssetManager.Load` passes the file-pack's names (`GetDataFile`) straight to the loaders.
  - `FindMissingDataFiles`, `FindMismatchedDataFiles` and `LoadMovies` use `File.Exists`
    relative to the working folder, which is the game folder (the editor sets it too, in
    `GameContent.Load`).
  - `CheckDataFiles` throws `DataFilesException` ("…aren't in the game folder") for any missing
    file except the levels' pair and movies.
  - md5s are compared only for a `strict-md5` file-pack (refused) and by `PickGameType` to tell
    same-named releases apart. A mod game never goes through `PickGameType`.
  - `GamePack` doesn't record which mod it came from.
- **GAMEMAPS can already come from a mod.** `PfWolfPk3Loader.FindMapFilePair` takes a
  `maps/maphead.xxx` + `maps/gamemaps.xxx` pair from any folder or pk3 mod, named as the running
  release names them, over the game's own pair (`AssetManager.LoadLevels`).
- **A mod's raw-data-map and alias.yaml.** A mod keeps them at its root, or in
  `gamepacks/{pack}/` (`ModRootGamePackFiles`). Either way they load as the running pack's
  `gamepacks/{pack}/…`, after the base packs' files, which are renamed into the running pack
  (`GetEntriesInLoadOrder`).
  - raw-data-map merges one level deep (`mergeLevels: 1`). A mod gives only the sections it
    changes (`walls:`, `graphics:`…), and each replaces the base's list whole. wolf3d-apogee and
    wolf3d-shareware already do this, with only `graphics:`.
  - alias.yaml doesn't merge (`mergeLevels: 0`): a mod's alias.yaml has to be complete.
- **The loaders tolerate files that don't match.** `DataMapNames` (PFWolf.Common/Loaders) names
  chunks past raw-data-map's lists with placeholders, and warns only when a chunk with data gets
  a made-up name:
  - `WALL####`, `SPR####` and `DIGI####` (VSWAP), `FONT####`, `PIC####` and `EXTERN####`
    (VGAGRAPH), `MUSIC####` (AUDIOT), `MAP####` (GAMEMAPS).
  - **AUDIOT:** `Wolf3dAudioFileLoader` splits the file into blocks at each `!ID!` tag chunk.
    The first block is the sounds: PC speaker, AdLib and digitized thirds of (count + 2) / 3, the
    last slot being the tag. Sounds past the map's names become `PCSND####`, `ALSND####` and
    `DSSND####` within their third. Music is the next block. Since the split follows the tag, a
    file with more sounds still splits correctly.
  - **VGAGRAPH:** `Wolf3dVgaFileLoader` takes STRUCTPIC as chunk 0, then `font-count` font
    chunks (from the file-pack: 2, Blake 5), then as many pics as STRUCTPIC's length gives, then
    Tile8, then externs. raw-data-map's names are matched on its `Tile8` entry, so added pics get
    `PIC####` and the externs after Tile8 keep their names.
  - **VSWAP:** walls are pages 0 to PMSpriteStart − 1, sprites up to PMSoundStart − 1, then the
    digitized sounds from the sound-info page. Empty pages get no asset.
- **Names are what the rest of the game uses.** mapdefs walls.yaml and doors.yaml name textures,
  actordefs name sprites, sound-seq names sounds, fonts.yaml and statusbar.yaml name graphics, and
  alias.yaml maps VGAGRAPH chunk numbers to names for the help/end texts' ^G/^T commands. As long
  as a chunk keeps its stock name, everything that refers to it keeps working.
- **Things that shift when WDC adds chunks:**
  - Doors use the last wall pages: the exe's DOORWALL is PMSpriteStart − 8 in Wolf3D/Spear. WDC
    adds walls before them, so they move along. Wolf3D's raw-data-map lists them by position
    (DOOR1_1 … at indexes 98–105), so with walls added the door names land on new walls and the
    doors get `WALL####`.
  - Pics added to VGAGRAPH push Tile8 and the externs along, and alias.yaml's chunk numbers with
    them. The loader's Tile8 alignment fixes the names but not alias.yaml.
  - A new wall tile id needs a walls.yaml entry (tile n uses wall pages 2n − 2 and 2n − 1) before
    the game draws it.
- **Some of a classic mod can't come from its data files.** What the exe held has to be written
  into game-info and actordefs by hand: the palette, level music, par times, ceiling colors,
  episode names, and new actors and their behaviour. The mod's game inherits all of these from
  the base pack's game-info, so only the differences need writing.

## The generated mod

```
MyMod/
  gamepack-info.yaml            # adds game "mymod": base-pack, file-pack (names as shipped + md5)
  raw-data-map.yaml             # only the sections that differ from the base pack's
  alias.yaml                    # only when VGAGRAPH's chunk numbers moved (complete file)
  game-info.yaml                # title + "# TODO" notes for exe-side settings; the rest inherited
  mapdefs/walls.yaml            # entries for wall tile ids the base game doesn't have
  VSWAP.WL6, VGAGRAPH.WL6, ...  # the mod's own data files, as shipped
  maps/MAPHEAD.WL6, maps/GAMEMAPS.WL6   # moved here so FindMapFilePair picks them up
  raw-data-mod.txt              # the report: matched / changed / new per section
```

Root files load into the mod game's own pack folders, as for any mod. Comments in
raw-data-map.yaml say how each name was found: "changed", or "new: name this" on the
placeholders. Unchanged sections are left out, so they come from the base pack.

## Phases

### Phase 1: data files from the mod's folder (small)

- `GamePack` gets a `Folder` (not from YAML): the mod's full path, set in
  `GamePackInfoAsset.WithModGames` from the `ModSource` that gave it. It's null for
  pfwolf.pk3's packs.
- `GamePackInfoAsset.ResolveDataFile(releaseId, file)`: when the file-pack comes from a pack with
  a `Folder` and `Path.Combine(Folder, file)` exists, that path; otherwise the name as now
  (relative to the game folder).
  - `GetDataFile`, `FindMissingDataFiles`, `FindMismatchedDataFiles` and `AssetManager.LoadMovies`
    use it. `GetFilePack` has to say which pack the file-pack came from, for the folder.
  - `DataFilesException`'s message names both places looked in.
- md5 warning: after `CheckDataFiles`, a non-strict file-pack from a mod with md5s gets
  `WarningLog.Write` per mismatched file ("…changed since its raw-data-map was made: run the
  generator again").
- The levels' pair: the mod keeps it in `maps/`. A pair at the mod's root is also tried through
  `ResolveDataFile`, so a hand-made mod doesn't have to move it.
- Folder mods only: a pk3 mod game whose file-pack names files the pk3 can't give falls through
  to the game folder (phase 6 changes that).
- Hand-write `examples/mods/raw-data-demo`: a gamepack-info adding `raw-data-demo` on wolf3d with
  a VSWAP copy that has one wall replaced, made by a test helper script (no Apogee data checked
  in). Check it plays.
- Tests (`Tests/PFWolf.Tests`):
  - `RawDataModTests` (GameData category): a temp mod folder with copies of the stock WL6 files
    loads as its own pack, its assets match the stock game's, and a file missing from the mod
    comes from the game folder.
  - An edited copy gives the md5 warning.
  - `GamePackInfoAsset` unit tests for `ResolveDataFile`.

### Phase 2: reading and matching chunks (medium)

- `PFWolf.Common/Loaders/DataFileChunks.cs`: a light read of each file's chunk list, returning
  per section a list of `(index, length, hash)` for non-empty chunks. It reuses the header parsing
  in `Wolf3dVswapFileLoader`, `Wolf3dVgaFileLoader`, `Wolf3dAudioFileLoader` and
  `Wolf3dMapFileLoader` (factor those out where needed, no behaviour change):
  - **VSWAP:** walls, sprites, digitized sounds (by sound-info entry).
  - **VGAGRAPH:** fonts, pics, Tile8, externs, from STRUCTPIC and the `font-count` the base
    pack's file-pack gives.
  - **AUDIOT:** the PC, AdLib and digitized thirds, and music, split at the `!ID!` tags.
  - **GAMEMAPS:** levels, with each level's planes hashed together and the header name.
- `PFWolf.Common/Managers/RawDataModGenerator.cs`, part 1, `Match(dataFolder, basePackId)`:
  1. A chunk with the same hash as a base chunk takes that chunk's name, wherever it now is.
  2. Else, at a stock position in a section whose count didn't change, or below the stock count,
     it keeps the stock name, flagged changed.
  3. Else it gets a placeholder, flagged new, named as `DataMapNames` would (`WALL0106`,
     `PIC0145`, `PCSND0087`…), so the generated names and the loaders' agree.
  - **Doors:** the base pack's door pages are the wall names doors.yaml uses that sit at the end
    of its `walls:` list (8 in Wolf3D/Spear; Blake counted the same way). The last that many wall
    pages of the mod's file take those names, and placeholders fill the gap before them.
  - Duplicate hashes (two identical stock chunks) go by position first.
- `DetectBase(dataFolder)`: for each built-in release whose data files have the same extensions,
  or with any extension when there's no match, score the share of chunks whose hashes match.
  Returns the ranked list for the wizard or command to confirm.
- Tests (`RawDataModGeneratorTests`, GameData category):
  - The stock files match themselves with no flags, for wolf3d, wolf3d-apogee, spear, and blake
    when present.
  - Test-built files with walls, sprites and sounds added give placeholders in the right places
    and the door names at the end. They're written by a small `VswapWriter` / `AudioWriter` in
    the test project (both formats are uncompressed).
  - Pics added to VGAGRAPH keep the externs' names. This needs a test `VgaWriter` with a Huffman
    encoder (VGAGRAPH is always Huffman-compressed against VGADICT), or the stock files re-encoded
    with one extra pic. Building it is part of this phase.
  - A replaced chunk is flagged changed.
  - `DetectBase` picks wolf3d-apogee over wolf3d for Apogee files.

### Phase 3: writing the mod (medium)

- `RawDataModGenerator`, part 2, `Write(match, modFolder, gameId, title)`, returning the files as
  text plus the data files to copy:
  - **raw-data-map.yaml:** only sections with any change, each as a full list. Comments: a header
    saying it was generated and from which base, `# changed` and `# new: name this` on each
    flagged line, and the hash in a trailing comment so a re-run can recognise renamed chunks.
  - **gamepack-info.yaml:** the game with `title`, `base-pack`, and a `file-pack` holding each
    loader's files with md5s. The `font-count` and `JamMovieFileLoader` movies are copied from the
    base pack's file-pack (movie files from the data folder when present).
  - **alias.yaml:** when any VGAGRAPH chunk moved, the base pack's alias.yaml with each number
    remapped (complete, since it doesn't merge). A number whose chunk is gone is commented out
    with a note.
  - **mapdefs/walls.yaml:** an entry for each tile id past the base pack's walls whose pages
    exist: North/South = page 2n − 2, East/West = page 2n − 1, by their raw-data-map names. Door
    tiles keep the base doors.yaml.
  - **game-info.yaml:** `title` and `# TODO` lines for level music, par times, ceiling colors and
    episode names, saying they're inherited from the base game until changed.
  - **raw-data-mod.txt:** per section, how many chunks matched, changed and are new, which levels
    changed, and the exe-side list.
- Copy the data files into the mod folder as named, the levels' pair into `maps/`. Never
  overwrite an existing mod's files without the caller's say-so: the result reports clashes.
- Engine: `--make-mod <data folder>` in `PFWolf/Configuration/GameParams.cs`, with `--base
  <pack>` and `--out <mod folder>` (default `mods/<folder name>`). `GameHost` runs it before the
  window opens, prints the report and the detected base, and exits; it needs pfwolf.pk3 for the
  base packs. `--nowait`-style: no keypress needed.
- Tests:
  - Generating from test-built files and loading the result with `AssetManager` (GameData)
    gives the matched names, the placeholders, and doors that draw (doors.yaml names resolve).
  - Write then re-match is stable.
  - A YAML round trip of each written file deserialises (`RawDataMapAsset`, `GamePackInfoAsset`,
    `AliasAsset`).

### Phase 4: editor wizard, naming and re-running (medium)

- `PFWolf.Editor/Editing/RawDataModWizard.cs` + a dialog built in code like the other
  `IEditorDialogs` ones, behind File → "New mod from data files…":
  1. Pick the data folder.
  2. Confirm the detected base (ranked list).
  3. Show the report (matched / changed / new per section).
  4. Pick the mod's name and folder, and write.

  It then opens the mod with its game selected: the mod game appears in `GameChoices` via
  `RefreshModGames`.
- **Naming placeholders:** the art browser gets a "Needs a name" filter for assets whose
  raw-data-map line is flagged new. Rename (the existing art rename) writes the new name into the
  mod's raw-data-map.yaml (YamlText splice, as GameInfoFile does), and into mapdefs/walls.yaml
  where a generated entry used it. The sound browser gets the same for sounds and music.
- **Re-running** (wizard "Update from data files…", or `--make-mod` on an existing mod): a chunk
  whose hash is in the old raw-data-map's trailing comments keeps the name the modder gave it.
  Removed chunks are listed in the report, not dropped silently. Hand edits outside the
  generated sections are kept, since the YAML is spliced, not rewritten.
- Level checks get one more warning: an object or wall code in a level whose mapdefs entry
  points at a placeholder-named texture or sprite ("WALL0106 has no name yet").
- Tests: wizard steps through `IEditorDialogs` fakes; rename updates raw-data-map.yaml and
  walls.yaml; re-run keeps names.

### Phase 5 (optional): import into pk3 assets (medium to large)

- The earlier classic-mod-import idea: copy the mod's changed and new chunks out into
  `textures/`, `sprites/`, `graphics/`, `sounds/`, `texts/` and `maps/*.wad`, so the mod no longer
  needs its raw files and can be edited like any other.
- Only worth doing if modders want to keep working on a classic mod in the PFWolf editor.

### Phase 6: pk3 raw data mods (small to medium)

- A pk3 mod game whose file-pack names files inside the pk3: extract them to
  `%APPDATA%\PFWolf\<game>\raw-cache\<pk3 hash>\` on first load and resolve against that folder.
  Clean up caches whose pk3 is gone.
- The editor's "Save as pk3" for a raw data mod includes the data files.
- Tests: the phase 1 tests with the mod zipped.

## Risks

1. **Position guesses.** A changed chunk at a stock position might not be a replacement (a WDC
   mod that deleted a sprite and added another). The report and the "changed" flags make the
   modder look.
2. **Door page counts.** Wolf3D/Spear's 8 is certain. Blake's door pages need checking against
   its raw-data-map and doors.yaml in phase 2 before trusting the "names doors.yaml uses at the
   end" rule.
3. **VGAGRAPH variants.** Apogee vs GT layouts, the shareware's extra pics, Spear's palette
   chunks, Blake's 5 fonts. Base detection has to pick the right release, or the alignment is off
   from the first pic. The ranked list and the confirm step are the safety net.
4. **The exe-side gap.** A mod whose exe was changed (new actors, new wall behaviour) plays wrong
   until the modder writes actordefs and game-info. The report says so, but it can't fix it.
5. **Test data.** The stock files can't be checked in, so most generator tests build small
   synthetic files with test-only writers. The GameData tests run only where the game data is.

## Follow-up (not this work)

- An engine `--data <folder>` that plays a raw folder with a raw-data-map generated in memory, no
  mod written.
- Writers for the raw formats (VSWAP, Huffman VGAGRAPH, Carmack GAMEMAPS) so the editor could save
  back into them. Probably never: WDC stays the raw editor. The test-only writers in phase 2 are
  minimal: GAMEMAPS isn't written at all, since its levels already come from `.wad` files in
  tests.
