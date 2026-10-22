# Raw data mods plan: playing WDC/ChaosEdit data files with a generated raw-data-map

## Status (2026-10-22): plan only

Nothing is built. The open questions are listed below. This takes over phases 1–2 of the
classic-mod-import plan (the 2026-10-12 discussion of a one-way import into a mod). That import
becomes an optional later phase here.

Goal: a classic mod made by editing the raw data files (VSWAP, VGAGRAPH/VGAHEAD/VGADICT,
AUDIOHED/AUDIOT, MAPHEAD/GAMEMAPS, with WDC or ChaosEdit) plays in PFWolf as a mod, without
extracting its assets. A generator reads the files, compares them with the base game's, and
writes the mod's `gamepacks/{pack}/raw-data-map.yaml` and the files that go with it. Chunks it
recognises keep their stock names. New or unrecognised ones get placeholder names and a comment
asking the modder to name them. The modder fills those in and plays.

This works for Wolf3D, Spear, Blake Stone and Planet Strike mods alike, since they share the
file formats.

## Open questions

- **Where the generator runs.** An editor wizard ("New mod from data files…") only, or also an
  engine command (`--make-raw-data-map <folder>`) for modders who don't use the editor?
- **Folder mods only, or pk3s too?** The data file loaders read files by path. A pk3 holding
  raw files would need them extracted to a cache first, or loaders that read streams.
- **Stock-named files.** A classic mod usually ships `VSWAP.WL6` etc. with the stock names,
  meant to be copied over the game's. The generator could keep those names, which works once the
  file-pack is resolved against the mod folder, or rename them to the mod's own extension.
- **Changed chunks at stock positions.** A replaced texture or sprite is usually what the
  modder meant (a new guard at GARDA1). Keep the stock name and flag it as changed (the
  recommendation), or give it a placeholder name too?
- **md5 in the generated file-pack.** Writing md5s catches a modder who edits the files again
  without re-running the generator, but refuses nothing unless `strict-md5` is set.

## Where things stand

- **A mod can already add a game with its own data files.** A mod's `gamepack-info.yaml` adds a
  pack (`GamePackInfoAsset.WithModGames`) with a `base-pack` and its own `file-pack`.
  `GetFilePack` takes the nearest one along the base-pack chain, so a pack built on `wolf3d`
  with its own file-pack plays those files and everything else (mapdefs, actordefs, game-info)
  from wolf3d. A mod's `gamepacks/{pack}/raw-data-map.yaml` is read like the base packs'.
- **But data file names resolve against the game folder.** `AssetManager.Load` passes the
  file-pack's names to the loaders as they are, and the working folder is the game folder (the
  editor sets it too, in `GameContent.Load`). A mod's data files only load if they're copied into
  the game folder.
- **The loaders tolerate files that don't match.** `DataMapNames` (PFWolf.Common/Loaders) names
  chunks past raw-data-map's lists WALL####, SPR####, DIGI####, FONT####, PIC####, EXTERN####,
  MAP####, MUSIC####, and AUDIOT sounds PCSND/ALSND/DSSND#### per third. It warns only when a chunk
  that has data gets a made-up name. VGA pics line up on the map's "Tile8" name.
- **Names are what the rest of the game uses.** mapdefs walls.yaml names textures, actordefs
  name sprites, sound-seq names sounds, fonts.yaml and statusbar.yaml name graphics, and alias.yaml
  maps VGAGRAPH chunk numbers to names for the help/end texts' ^G/^T commands. As long as a chunk
  keeps its stock name, everything that refers to it keeps working.
- **Things that shift when chunks are added:**
  - Door textures are the last 8 wall pages (DOORWALL = PMSpriteStart − 8), so added walls push
    the door names along. Wolf3D's raw-data-map lists them as DOOR1_1 … DOOR3_2 at the end of
    `walls:`.
  - Pics added to VGAGRAPH push every later chunk (Tile8, externs, the help texts) along, and
    alias.yaml's chunk numbers with them.
  - A new wall tile id needs a walls.yaml entry (tile n uses wall pages 2n−2 and 2n−1) before
    the game draws it.
- **Some of a classic mod can't come from its data files.** What the exe held (the palette,
  level music, par times, ceiling colors, new actors and their behaviour) has to be filled into
  game-info and actordefs by hand.

## The generated mod

```
MyMod/
  gamepack-info.yaml            # the mod's game: base-pack + file-pack (mod-relative files)
  gamepacks/mymod/
    raw-data-map.yaml           # every chunk named; placeholders marked "# TODO name this"
    alias.yaml                  # only when VGAGRAPH's chunk numbers moved
    game-info.yaml              # a stub: title, maps listed, "# TODO" for exe-side settings
  mapdefs/mymod/walls.yaml      # entries for wall tile ids the base game doesn't have
  VSWAP.WL6, VGAGRAPH.WL6, ...  # the mod's own data files, as shipped
  raw-data-map.report.txt       # what matched, what changed, what's new
```

Comments in raw-data-map.yaml say how each name was found: "same as wolf3d", "changed (was
GARDA1)", "new: name this". The modder only has to look at the flagged ones.

## Phases

### Phase 1: data files from the mod's folder (small)

- A file-pack given by a mod resolves its file names against that mod's folder (GamePack
  remembers where it came from). A file-pack from pfwolf.pk3 still resolves against the game
  folder. Missing-file and md5 checks follow the same rule.
- Folder mods only, unless the open question says pk3s too (then extract to a cache under the
  config folder, keyed by the pk3's hash).
- Hand-write a raw-data-map for one small WDC mod and check it plays, before any generator
  exists.
- Tests: a mod folder with copies of the stock files (GameData category) loads as its own pack
  and matches the stock game's assets.

### Phase 2: the generator (medium, the main work)

- `RawDataMapGenerator` in PFWolf.Common. Input: a folder of data files and a base pack
  (detected by file names and chunk counts, or picked). Output: the files above, in memory, for
  the caller to write.
- Read each file's chunk list:
  - VSWAP: wall, sprite and sound page counts from its header.
  - VGAGRAPH: chunk count from VGAHEAD, picture count from STRUCTPIC, font count (2 or 5) from
    the base pack.
  - AUDIOT: chunk count from AUDIOHED, split into PC / AdLib / digitized sounds and music the
    way DataMapNames does.
  - GAMEMAPS: level count and names from the level headers.
- Name each chunk:
  1. Same hash as a base chunk: that chunk's name, wherever it now is.
  2. Else at a stock position whose count didn't change: the stock name, flagged "changed".
  3. Else a placeholder (WALL0098, PIC0145, …), flagged "new".
  - Doors: the last 8 wall pages always take the door names.
- Write `alias.yaml` only when graphics moved, and walls.yaml entries for each new wall tile id
  (pages 2n−2 / 2n−1, named from the raw-data-map).
- Write the stub game-info: maps listed in episodes of 10 (Wolf3D) or as the base pack's, with
  `# TODO` lines for music, par times and ceiling colors.
- Tests: generating from the stock files gives the stock raw-data-map; files with walls,
  sprites and pics added (built in the test) give placeholders in the right places and the door
  names at the end.

### Phase 3: editor wizard and re-running (medium)

- Editor: "New mod from data files…" picks the folder and the base game, shows the report
  (matched / changed / new per section), writes the mod and opens it.
- The art browser shows placeholder-named chunks first, with a rename that updates
  raw-data-map.yaml (and walls.yaml / actordefs references to that name).
- Re-running on changed files keeps the names the modder gave: a chunk whose hash is in the old
  raw-data-map keeps that name. Removed chunks are reported, not silently dropped.
- Optional engine command (per the open question) that runs the same generator without the
  editor.

### Phase 4 (optional): import into pk3 assets

- The earlier classic-mod-import idea: copy the mod's changed and new chunks out into
  `textures/`, `sprites/`, `graphics/`, `sounds/`, `texts/`, `maps/*.wad`, so the mod no longer
  needs its raw files and can be edited like any other.
- Only worth doing if modders want to keep working on a classic mod in the PFWolf editor.

## Risks

1. **Position guesses.** A changed chunk at a stock position might not be a replacement (a WDC
   mod that deleted a sprite and added another). The report and "changed" flags make the modder
   look.
2. **AUDIOT layout.** A mod that changed the number of sounds moves every third. The generator
   has to check that the split lands on empty slots, and otherwise ask.
3. **The exe-side gap.** A mod whose exe was changed (new actors, new wall behaviour) plays
   wrong until the modder writes actordefs and game-info. The report says so, but it can't fix it.
4. **VGAGRAPH variants.** Apogee vs GT layouts, the shareware's extra pics, Blake's 5 fonts. Base
   pack detection has to pick the right layout or the alignment is off from the first pic.

## Follow-up (not this work)

- An engine `--data <folder>` that plays a raw folder with a raw-data-map generated in memory, no
  mod written.
- Writers for the raw formats (VSWAP, Huffman VGAGRAPH, Carmack GAMEMAPS) so the editor could save
  back into them. Probably never: WDC stays the raw editor.
