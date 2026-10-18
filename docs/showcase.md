# PFWolf showcase list

Features to show off in YouTube videos, Shorts, and the documentation. Shorts are one
visual hook each (15–60 s, vertical). Long videos group related features. "Ready demo"
points at something that already exists to record from.

## Shorts (one hook each)

### Visual / renderer
| # | Hook | What to show | Ready demo |
|---|------|--------------|------------|
| 1 | "Wolf3D can look up now" | Y-shearing: mouse look up/down | any map, `m_look` |
| 2 | "Tall walls in Wolf3D" | 2–3 story walls, arches, sky | `examples/mods/tall-walls-demo` |
| 3 | "45° walls" | Diagonal walls, walking along them, automap | needs a map |
| 4 | "Floors and ceilings" | Textured flats replacing flat colours | ECWolf-style map with flats |
| 5 | "Lights in Wolf3D" | Dynamic actor lights, coloured muzzle flashes, lamps | `examples/mods/moody-lights`, switch-demo lamp |
| 6 | "Fog and night" | Distance fog, night maps, tinted light zones | shading map |
| 7 | "It's raining in the castle" | Rain/snow particles stopping under ceilings | `weather` cmd |
| 8 | "Fences you can see through" | Wall sprites (flat fence/gate panels) | needs a map |
| 9 | "Posters on walls" | Wall patches (overlays per face) | `examples/mods/patch-demo` |
| 10 | "Animated walls" | animdefs: animated walls/flats/sky | needs a sample |
| 11 | "Widescreen Wolf3D" | Hor+ widescreen, UI scale slider | Options → Video |
| 12 | "Death drop" | Eye height + falling camera on death | any map |

### Gameplay
| # | Hook | What to show | Ready demo |
|---|------|--------------|------------|
| 13 | "Switches that do things" | Wall switches opening doors/walls, spawning actors | `examples/mods/switch-demo` |
| 14 | "Multiple pushwalls at once" | Several moving pushwalls | needs a map |
| 15 | "Guards vs SS" | Faction infighting, recoloured followers | `examples/mods/faction-demo` |
| 16 | "Fireball traps with Lua" | Scripted fireball wall | `examples/mods/fireball-demo` |
| 17 | "Pick your class" | Player classes with different speed/damage/armor | class menu |
| 18 | "Play as a dog" | Play-as-enemies in the multiplayer lobby | multiplayer.pk3 |
| 19 | "Spectate the AI" | Camera / `spectate` command | any map |
| 20 | "Pickup messages" | In-game HUD messages, obituaries | Options → Messages |

### Multiplayer
| # | Hook | What to show | Ready demo |
|---|------|--------------|------------|
| 21 | "Wolf3D deathmatch" | 4-player DM arenas | multiplayer.pk3 maps |
| 22 | "Prop Hunt in Wolf3D" | Hiding as a lamp/barrel, hunters searching | PH01 |
| 23 | "Capture the flag" | CTF / TDM / KOTH / Domination / Heist / Horde | HE01 + mode maps |
| 24 | "Co-op Wolf3D" | Co-op with mid-game join | lobby |

### Games and compatibility
| # | Hook | What to show | Ready demo |
|---|------|--------------|------------|
| 25 | "Blake Stone in a Wolf3D port" | Blake Stone + Planet Strike running | `--game` blake packs |
| 26 | "Every version of Wolf3D" | Shareware, Apogee, Activision, Spear, Spear demo | game selection |
| 27 | "Perfect demo playback" | Original demos frame-identical to vanilla | side-by-side with Wolf4SDL |

### Tools
| # | Hook | What to show | Ready demo |
|---|------|--------------|------------|
| 28 | "Edit a map, hit Play" | Editor → Play via `--warp` | editor |
| 29 | "3D view in the editor" | Editor split view with OpenGL 3D | editor |
| 30 | "Pixel-edit a texture" | Art browser + pixel editor, save into a mod | editor F6 |

## Long-form videos

1. **What is PFWolf?** — trailer/overview: game support, modern renderer, multiplayer,
   modding, editor. Montage of the Shorts.
2. **Rendering upgrades** — look up/down, widescreen, tall walls, diagonals, flats, patches,
   wall sprites, mirrored textures, animations, sky.
3. **Lighting and atmosphere** — shading, fog/night, light zones + tints, halo lights,
   bright states, weather.
4. **Multiplayer tour** — lobby, co-op, DM, all match modes, Prop Hunt, play-as-enemies,
   online play (port check, Streamer Mode).
5. **Supported games** — Wolf3D (shareware/Apogee/retail), Spear + demo, Blake Stone, Planet
   Strike, standalone games; demo sync accuracy.
6. **Modding without code** — pk3 layout, drag-drop/`--file`/mods folder, Options → Mods,
   YAML defs (actors, weapons, walls, floors, mapdefs), `!remove`/`!replace`, fonts, sounds
   and music (ogg/mp3/wav/imf), texts, palettes.
7. **Scripting with Lua** — actions, hooks (`onlevel`/`ontick`), `game.actors`, fireball demo.
8. **Make a standalone game** — editor New mod → standalone-demo → play.
9. **The PFWolf editor** — map editing tools (brushes, stamp, wand, rotate/flip), 3D view,
   checks, art/sound/palette/text/script browsers, Definitions window, map import.
10. **Gameplay systems** — player classes, factions, switches, multiple pushwalls, messages,
    save system (thumbnails, quick/auto saves), custom controls + controller.
11. **Dev log / behind the scenes** — accuracy work (demo sync), architecture refactors.

## Documentation outline

Ordered so a player can stop early and a modder keeps reading.

- **Getting started** — install, game data, game selection, command-line options
- **Playing** — controls + controller, options menus, saves, console commands
- **Multiplayer** — hosting/joining, modes, Prop Hunt, online play (`docs/online-play.md`)
- **Modding basics** — pk3 structure, loading mods, merge rules, `!remove`/`!replace`
- **Content reference** — game-info, gamepack-info, actordefs, weapons, player classes,
  walls/floors/mapdefs, factions, translations, animdefs, fonts, hud-messages, weather,
  sounds/music, textures/flats/sprites folders
- **Maps** — formats (.wad, GAMEMAPS pair), planes 0–5 meaning, tall walls, flats, light
  zones, tags/switches, diagonal and wall-sprite markers, patches
- **Scripting** — Lua API, hooks, sandbox
- **Editor manual** — one page per window/browser
- **Standalone games** — from template to release
- **Example mods** — what each one in `examples/mods/` demonstrates

## Check before recording

Memory notes flag these as untested in-game; verify first so a video doesn't catch a bug:
multiple pushwalls, floor codes, wall-sprite gates (not openable), mirrored textures,
controller look, fullscreen + Auto resolution, LAN multiplayer, Spear multiplayer, audio
by ear, editor browsers' dialogs.

Missing demo content (would need a small map or mod): diagonal walls, wall sprites, flats,
fog/night, animated assets, multiple pushwalls. A single "feature tour" mod with one room
per feature would serve every Short and the docs screenshots.
