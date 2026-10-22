# PFWolf

**Project-Friendly Wolfenstein 3D**: a source port of Wolfenstein 3D in C# (.NET 10, SDL2), built
so you can make a game, or change one, with data files instead of changing the engine. It comes
with **PFWolf Editor**, a map and content editor for the same files.

Full documentation (manual, tutorials and the reference for every definition file) is on the
website: **<https://www.littleagames.com/pf-wolf/docs/latest/>**

## PFWolf

### Games

Put PFWolf next to the original data files and pick one with `--game` (or from the game selection):

- Wolfenstein 3D: retail (Activision/GT), Apogee and shareware releases
- Spear of Destiny and the Spear of Destiny demo
- Blake Stone: Aliens of Gold and Planet Strike
- Standalone games that need no other game's files (see `examples/mods/standalone-demo`)

The original demos play back frame-identical to vanilla.

### Renderer

- Look up and down (mouse look), widescreen (Hor+) with a UI scale, uncapped frame rate
- Textured floors and ceilings, sky, walls of any height with arches, 45° diagonal walls
- Wall patches (pictures over a wall's faces), see-through wall sprites (fences and gates),
  mirrored, animated and scrolling walls and flats
- Distance fog, night levels, light zones with tints, dynamic colored actor lights and muzzle
  flashes
- Rain and snow, eye height and a death drop

### Gameplay and modding

- Mods as folders, `.zip` or `.pk3` files: `--file`, drag and drop, or the `mods` folder and
  **Options → Mods**. A mod's YAML is laid over the game's key by key (`!remove` takes keys out)
- Data-driven actors, weapons, player classes, doors and keys, walls, floors and map things
- Wall switches and tags, multiple moving pushwalls, factions and followers, sprite color
  translations, medics that revive their side
- Lua scripting: new actions and level/tick hooks, sandboxed so demos, saves and network games
  stay in step
- Your own textures, sprites, sounds and music (PNG, WAV/OGG/MP3/IMF), fonts, texts and palettes
- Maps as ECWolf `.wad`, UWMF or GAMEMAPS/MAPHEAD, up to 256×256
- Timestamped saves with thumbnails, quick and auto saves; rebindable keys, mouse wheel and
  controller; a console with cheats and debug commands

### Multiplayer

Up to 4 players over LAN or online: co-op (with mid-game join), deathmatch, team deathmatch,
capture the flag, heist, domination, king of the hill, horde and prop hunt. The Multiplayer mod
(`multiplayer-pk3/`) adds deathmatch arenas, extra weapons and playing as enemies. See
[Playing online](docs/manual/online-play.md) for hosting, port forwarding and virtual LANs.

## PFWolf Editor

An Avalonia desktop app that reads the game's assets the same way the engine does, and saves
into mods (never into the original data files):

- **Map editing**: every plane (walls, things, flats, heights, tags, light zones, patches),
  brushes, stamps, shaped selections, magic wand, replace, rotate and flip, resize, problem checks
- **3D view**: an OpenGL view of the level beside the map, with picking and editing
- **Play** (F5): runs the level in PFWolf straight from the editor
- **Browsers**: textures, flats, sprites and pictures with a pixel editor (F6), sounds and music
  (F8), palettes (F9), texts with page preview (F2), Lua scripts with highlighting and
  autocomplete (F12), and recorded demos with a ghost replay
- **Definitions** (F11): field editors for game-info, game packs, actors, mapdefs, factions and
  animations, plus raw YAML
- **New mod…**: starts a mod or a ready-to-play standalone game; Ctrl+I imports WDC map planes

## Building

```bash
dotnet build PFWolf.sln -p:Platform=x64
```

Run `PFWolf.exe` from a folder with the game's data files (for example `VSWAP.WL6` and the rest).
`PFWolf.exe --help` lists the command-line options.

## More to read

- [Getting started](docs/manual/getting-started.md): loading mods and how a mod is laid out
- [Tutorials](docs/manual/tutorials/index.md): your first mod, script, switch, enemy and weapon
- [How files combine](docs/manual/combining-files.md): the merge rules for mods
- [Example mods](examples/README.md): what each mod in `examples/mods` shows
- [The editor](docs/manual/editor/index.md): loading a game, editing levels, the 3D view and each
  browser
- Manual pages: [weapons](docs/manual/weapons.md), [player classes](docs/manual/player-classes.md),
  [actor states](docs/manual/actor-states.md), [built-in classes](docs/manual/built-in-classes.md),
  [factions](docs/manual/factions.md), [sprite colors](docs/manual/sprite-colors.md)

## License

GPL-2.0; see [LICENSE](LICENSE). Game data files are not included.
