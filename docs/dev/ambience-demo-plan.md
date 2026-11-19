# Ambience Demo plan

**Status (2026-11-19, branch `feature/ambience-demo`): phases 1 and 2 done.** The mod is
`examples/mods/ambience-demo` (README section "Ambience Demo"); the engine gained:

- `game.playsound(sound[, x, y])` (2a);
- ambient fades: zone `ambient-fade: <tics>`, `game.ambient(sound, tics)`, two ambient voices
  (`Managers/AmbientMixer.cs`) (2b); the editor's Map Properties has a Fade column for zones;
- sound-seq `volume`, `volume-variance`, `pitch`, `pitch-variance` (2c), picked with
  System.Random so demos and network games stay in step;
- a script's `game.ambient` now wins over the zones until it asks for nil; `"none"` is silence (2d);
- the `ambient` console command (what's playing, levels, fades);
- a fix: a map with `music: ""` stops the menu's song and resumes the ambient loop after the menu.

Decisions taken: the mod is `ambience-demo`; for 2d, Lua wins (no layers). The generator room's
generators are switched off with Use (not shot: no shootable-prop pattern to build on). Phase 3
is planned in detail below: **3a is done** (2026-11-19: `Gameplay/LoopSounds.cs`, the `loops`
command, the generators' drone and the sewer's outflow in the demo); 3b and 3c are not started.
Phase 4 is still open. The mod-facing changes (new sound-seq and zone fields, `game.ambient`'s
second argument and its win over zones, `game.playsound`) belong in the release notes of whichever
version ships this branch.

An example mod (`examples/mods/ambience-demo`, built by `examples/tools/make-ambience-demo.ps1`)
that shows what the engine can do with sound beyond weapons and monsters: area ambience, sounds
placed in the world, random one-shots, and sound that reacts to the game.

## What's there today

| Feature | Where | Used by an example mod? |
|---|---|---|
| Zone ambience: `zones: <id>: ambient:` on the zone plane (plane 5), `ambient: none`, carry-over through zones with no ambient | `Gameplay/ZoneAmbience.cs`, game-info `default-map` / mapdefs `zones:` | **No.** Only Midnight Assault (outside the repo) and unit tests (`ZoneAmbienceTests`) |
| `ambient-disguised:` / `ambient-undetected:` (stealth variants) | same | **No** |
| Editor: zone ambient fields | `MapPropertiesWindow` | n/a |
| `game.ambient(sound)` (one looping ambient channel, at the listener) | `GameScripts.cs` | music-demo (the generator hum, on tag 1) |
| Positional one-shots: `actor:playsound(s)`, `A_PlaySound(s, "positional")` (OpenAL distance falloff + panning, ref 1.5 / max 16 tiles) | `PositionalSounds.cs`, `AudioManager.PlayAt` | various |
| `A_ActiveSound`: heard only from an area connected to the player's | `ActorSounds.cs` | Mecha Hitler only |
| sound-seq `random:` picks, `channel:` | `SoundSequenceAsset` | footsteps-demo, others |
| `sounds/NAME.wav/ogg/mp3`, `music/` files | AssetManager | many |
| `game.onmusic` / `game.music` (music that follows the game) | `GameScripts.cs` | music-demo |
| Footsteps by floor flat | footsteps-demo (Lua) | footsteps-demo |

What the engine **can't** do yet:

1. **No looping sound placed in the world.** The only loop is the single ambient channel, and it
   isn't placed. A waterfall or generator you hear more as you walk up to it has to be faked with a
   repeating one-shot (gaps or overlaps at the seam, and the source doesn't follow a moving actor).
2. **No `game.playsound(sound, x, y)`.** Lua can place a sound only on an actor.
3. **The ambient channel cuts hard.** Changing zones swaps the loop with no fade.
4. **Walls and doors don't block sound.** `PlayAt` uses distance only. A sound behind a shut door
   is as loud as one in the open. Only `A_ActiveSound` checks areas, and it plays or doesn't.
5. **No per-sound volume or pitch** in sound-seq, so random picks can't vary in pitch or loudness.
6. **No reverb.** A cave sounds the same as an office.
7. **One ambient layer.** You can't have a base loop (wind) plus a second loop (rain on a roof).

## The demo map (MAP01, about 6 areas, one walk-through)

A hub corridor leads to rooms that each show one thing, with a sign or HUD message at each door
(as other demos do):

1. **Courtyard** (zone 1, `ambient: wind`, sky flat): wind loop, plus Lua one-shots at random
   intervals (birds now and then, a distant dog bark), unplaced.
2. **Sewer** (zone 2, `ambient: drips`): drip loop, and placed drips from invisible emitter things
   (`AmbientDrip` in actordefs, a state loop of `A_PlaySound("ambience/drip", "positional")` with
   random tics) so drips come from left and right.
3. **Generator room** (zone 3, `ambient: hum`, as music-demo but through zones, not Lua). A
   placed `Generator` thing whose clank is heard louder up close. Shoot it and the hum stops
   (zone ambient switched off by Lua, `game.ambient(nil)` only works where zones leave the
   channel alone, see phase 2 note).
4. **Quiet corridor** (zone 4, `ambient: none`): silence on purpose, between two loud rooms, to
   show that a doorway with no zone keeps the sound going but a `none` zone stops it.
5. **Guard post** (zone 5, `ambient: calm`, `ambient-undetected: tense`): a tense loop while the
   guards haven't seen you, which changes the moment they do. Ties in with stealth-demo's ideas.
6. **Radio room**: a radio thing on a named `channel:` that loops a short tune with
   `A_PlaySound` in its states. It shows that a new sound on the same channel cuts off the old one.
   Use it to switch stations, which is a nice use of channels.

Sounds are synthesized by the PS script, as footsteps-demo's are: wind (filtered noise with a
slow swell), drips (short sine pings), hum (50 Hz plus harmonics), birds (chirp sweeps), bark
(noise burst), radio (a few square-wave notes). They're placeholders that sound fine, and nobody
has listened to them yet.

## Phases

### Phase 1: the demo with what exists (no engine changes) (done)

Rooms 1, 2, 4, 5 and 6 above, with zones in the mod's mapdefs, actordefs emitter things, a small
Lua script for random one-shots, and synthesized WAVs. This alone gives zone ambience its first
example and its first in-game test. Add "(never tried)" lines to `docs/dev/manual-testing.md`.

### Phase 2: small engine gaps (each one separate and optional) (done)

- **2a `game.playsound(sound, x, y)`** (and `game.playsound(sound)` unplaced). A one-liner over
  `AudioManager.PlayAt`. It lets the courtyard birds come from the trees, not from your head.
- **2b Ambient fade.** Zone field `ambient-fade: <tics>` (and an optional `game.ambient(sound,
  fadetics)`): fade the old loop out and the new one in on the ambient source (two sources, so
  the two can overlap).
- **2c sound-seq `volume:` and `pitch:`** (a number, or `[min, max]` picked at random each play).
  These map to AL Gain / Pitch on the source when it starts. The demo uses them for drips and
  footsteps-like variety.
- **2d Mixing Lua ambience and zone ambience.** Today, once a map's zones have ambient sounds,
  `ZoneAmbience` sets the channel each tic, so `game.ambient` loses. Choose one: Lua wins while
  it asked for something, or a second layer (see 3b).

### Phase 3: placed loops, layers and muffling (planned 2026-11-19)

Three pieces, each its own commit, in this order: 3c muffles 3a's loops, and 3b is small and
stands alone. None of them needs a save change. Loops come from the actor's class and state,
layers from the zones (or a script that asks again each tic, as today), and muffling from the
doors.

#### 3a Looping emitters (`loop-sound`)

The model is particles. They have a class property, `particles:`, plus a per-frame
`particles:` override and a `none` value (`Rendering/Particles.cs` `CurrentEmitters`, and
`StateDataNodeDeserializer.EmitterNames`). Loop sounds work the same way:

- **Actordefs.**
  - Class property `loopsound: <sound>`, written `loop-sound` in YAML like the other
    hyphenated keys. It's a sound-seq name or a `sounds/` file, so it gets sound-seq volume
    and pitch, with any variance (and a `random:` pick) taken once as the voice starts, using
    System.Random, never the game's numbers.
  - Class property `loopradius: <tiles>`: how far away it can be heard. The default is 16
    tiles, the effect voices' MaxDistance.
  - Frame field `loop-sound:` overrides the class's while the frame shows. `none` (or `off`)
    means silence, so a generator's `Off` state is just `loop-sound: none`.
  - Read in `StateDataNodeDeserializer`, held as `ActorStatesData.LoopSound` and
    `ActorStateFrame.LoopSound` (a string, or "" for none).
  - Documented in `ActorSchema` (frame field) and `ActorProperties` (class properties).
  - Editor: `StateFrames.cs` must carry it through cloning, as it does `light`. Add it to
    `YamlKeyTests`' frame-key list.
- **Engine: `Gameplay/LoopSounds.cs` (`LoopEmitters`, with hooks like AmbientMixer so it's
  testable without AL, and `LoopSounds`, which gathers the actors).**
  - Each tic, after the listener moves, it works out the actors that loop now: the frame's
    `loop-sound`, else the class's.
  - Classes are read once, as `Particles.ClassEmitters` does. The candidate list is rebuilt
    only when `MapManager.ActorsVersion` changes, so Swarm Demo's 20k actors cost nothing
    when no class loops.
  - From that list it picks the nearest **8** within each one's radius. 8 is a pool of its
    own (`AudioManager` gets `LoopVoices = 8` sources), so it never takes the 16 effect
    voices or the 4 channels.
  - Picking is sticky. An emitter that has a voice keeps it until another one is clearly
    nearer, say by 2 tiles, so two generators at the edge of the pool don't swap every tic.
  - The pick is a pure static helper, `PickLoopVoices(candidates, current, count, hysteresis)`,
    with unit tests, as `PickSharedSource` has.
  - A voice that's taken or given up fades in or out over about 8 tics, not with a click. Its
    position follows the actor each tic, so a looping projectile or a patrolling robot carries
    its sound.
  - A changed sound on the same actor (it enters a state with another `loop-sound`)
    restarts the voice. The same sound goes on where it is.
  - Each start begins at a random sample offset, using System.Random as 2c does, so two of
    the same machine side by side don't sound in phase.
- **AudioManager:**
  - `StartLoopVoice(voice, name, x, y, radius, level)`, `MoveLoopVoice`, `StopLoopVoice` and
    `LoopVoicePlaying`. `LoopSounds.Describe()` serves the `loops` command.
  - The loop sources aren't relative and they loop, with the same distance model as the
    effects.
  - Their gain is the fade × sound-seq volume × the sound volume setting (and the muffling,
    3c).
  - `SetPaused` pauses them and `StopAll` stops them. The emitters start them again next tic
    if they're still wanted, which covers a level's end, a load and the menus.
  - `WaitSoundDone` and `IsAnySoundPlaying` leave them out, as they do the ambient.
- **Lua:** nothing new is needed, since a state change turns a loop on or off.
  `game.actors(...)` and `actor:jump(state)` already do this (the generator's Use).
- **Editor:** the actor window's frame fields have a "Loop sound" box beside Particles.
- **Console:** `loops` lists the voices: actor, sound, distance, level, muffling.
- **Multiplayer and demos:** it's local and only for show (the listener is the camera, as it
  spectates), uses no game random numbers, and touches nothing in the TicCmds. Demos stay
  trace-identical.

#### 3b Ambient layers (`ambient-2`)

- **Zones:** `ambient-2:` plus `ambient-2-disguised:` and `ambient-2-undetected:`. The second
  loop plays over the first with the same carry-over and `none` rules, and the same
  `ambient-fade`.
- **AudioManager:** a second AmbientMixer with two more sources, so `AmbientMixer.Voices`
  stays 2 per layer. `SetAmbient(name, fade, layer = 0)`; `DescribeAmbient` shows both
  layers.
- **ZoneAmbience:** resolves each layer separately. `ZoneSounds` gets `Sound2`, `Disguised2`
  and `Undetected2`. A script holds a layer separately too: `game.ambient(sound, tics, layer)`,
  where layer is 1 (default) or 2.
- **Editor:** Map Properties zones get an "Ambient 2" column. The stealth variants of layer 2
  are YAML only, since the grid is wide enough already.
- `ScriptApi` updated.

#### 3c Door muffling (Game Options, off by default)

- **What's muffled:** every placed sound (`PlayAt`: actors, tiles, `game.playsound(x, y)`)
  and 3a's loops. Unplaced sounds (weapons, pickups, the ambient) are not.
- **How much:** by the fewest **shut doors** between the listener's area and the sound's.
  - 0 means it's heard as now. Each shut door multiplies the gain by `muffle` (default
    0.35), so two doors give 0.12.
  - An area that no door path reaches at all (only through a pushwall, say) gets `muffle`
    squared.
  - This beats the plan's first idea, AreaByPlayer's yes or no. A room two doors off is
    quieter than one next door, and a generator through one door is still heard.
- **Where it's worked out:** `AreaConnections.DoorsBetween(from)`, a 0-1 breadth-first search
  over the door graph (each door joins its two areas; open costs 0, shut costs 1).
  - It's a pure static helper with unit tests.
  - It's cached, and redone only when a door opens or closes (`ConnectAreas` is already
    called then) or the listener's area changes.
  - It must be the **listener's** area, from the camera's actor. `AreaByPlayer` is every
    player's in multiplayer, so it would be wrong here.
- **Applying it:**
  - The sound's area is `MapManager.AreaAt` of its tile. A door or wall tile (a door's own
    sound) isn't muffled, and doors already check AreaByPlayer.
  - Each effect source keeps the area it was started from, so a door opening mid-sound
    swells it.
  - Gain moves toward the new muffle over about 8 tics on both effect and loop sources.
- **Settings:**
  - A Game Options switch, `GameOption.SoundMuffling`, console `s_muffle`, setting-defaults
    `sound-muffling`. Off by default, as stock Wolf3D sounds the same through doors.
  - Ambience Demo turns it on in its setting-defaults, and its sign says where to switch it
    off.
  - The strength is game-info `sound-muffle: 0.35`, so a pack can change it.
- **No EFX low-pass here.** Phase 4 has to load EFX through OpenTK anyway, so the filter goes
  in with it, applied on the same muffle path. Until then muffling is gain only.

#### The demo, after phase 3

- **Generator room:** the hum moves from the zone onto the generators (`loop-sound:
  ambience/generator`, `loop-sound: none` in `Off`). Walk round them and it pans and swells.
  - Zone 3 keeps a faint electrical buzz, which the script still holds off with
    `game.ambient("none", tics)` while they're off, so that example stays.
  - The room gets a door to the hub, to show muffling: shut it and the machines drop away.
- **Sewer:** an outflow at the far end (object 993, `AmbienceWater`, invisible TNT1 or a grate
  sprite) loops `ambience/water` with a 10-tile radius, so you hear it come nearer.
- **Courtyard:** `ambient-2: ambience/rain`, a light rain over the wind. The quiet corridor's
  `none` stops both layers.
- **Sounds:** the script synthesizes `ambience/water` (filtered noise with slow burbles) and
  `ambience/rain` (hiss with random ticks). Both are closed into seamless loops with the
  script's Close-Loop helper.
- `AmbienceDemoGameTests`:
  - Reads `loops` at the generators.
  - Reads it again with the door shut and `s_muffle 1`, expecting the muffle to be 0.35.
  - Reads `ambient` in the courtyard, expecting both layers.

#### Testing

- **Unit tests:**
  - `PickLoopVoices`: nearest N, radius, stickiness.
  - The LoopEmitters fade and restart rules, through hooks.
  - `DoorsBetween`: open, shut, two doors, unreachable.
  - Layer resolution in `ZoneAmbienceTests`.
  - `loop-sound` parsing, including `none` and the class versus frame override.
- **Game test:** the GameData test above, plus a Swarm Demo test that no class loops and the
  emitters cost nothing.
- **Demo sync:** all 8 Wolf3D/Spear demos must stay trace-identical.
- **By ear:** everything. Add "(never tried)" lines to manual-testing.md for: loops panning
  and following, a door swelling a sound, two layers, and muffling switched off.

Mod-facing additions for the release notes: `loopsound`/`loopradius`/`loop-sound`, zone
`ambient-2*`, `game.ambient`'s layer argument, the `sound-muffling` setting and the
`sound-muffle` strength.

### Phase 4 (optional): reverb zones

Zone field `reverb: cave | hall | room | none` using OpenAL Soft's EFX reverb presets on an
auxiliary send. The bundled OpenAL Soft has EFX on Windows and Linux. Apple's framework doesn't,
so there it's quietly ignored. The demo's sewer becomes a cave. This phase is the riskiest
(EFX through OpenTK, untested hardware paths), so it goes last.

## Decisions for the user

1. **Scope:** phase 1 only (no engine changes, in keeping with the 0.9.x beta's
   no-new-features policy), or phase 1 plus some of phases 2/3 on a feature branch for later?
2. **Name:** `ambience-demo` or `sound-demo`?
3. **Door muffling (3c):** an option that's off by default (recommended), or always on for
   placed loops only?
4. **Lua vs zone ambience (2d):** Lua wins, or layers? *(Taken: Lua wins.)*

Phase 3's, with the recommendation first:

5. **Loop voices:** a pool of 8 (recommended), or more, such as 16, for maps full of machinery?
6. **Turning a loop on and off:** by state only (`loop-sound:` per frame, no save change;
   recommended), or also an `A_LoopSound(sound)` / `actor:loopsound()` override, which has to be
   saved (a save version bump, as particles' `A_StartEmitter` override is saved in ActorSnapshot)?
   *(Taken 2026-11-19: by state only.)*
7. **How much muffling:** by the number of shut doors between (recommended), or AreaByPlayer's
   yes or no (simpler, but a room two doors off is as loud as one next door)?
8. **EFX low-pass:** with phase 4 (recommended, as both need EFX loaded), or in 3c already?

## Testing

- Unit tests: `ZoneAmbience.Resolve` already covered. Add GameData tests that load the mod and
  check its zones resolve, and (phase 2/3) tests for `game.playsound`, fade timing, emitter pool
  picking the nearest N (`PickSharedSource`-style static helper).
- Headless: `ALSOFT_DRIVERS=null` keeps OpenAL silent. A scripted demo walking the corridor can
  log `AudioManager.Ambient` per zone.
- By ear: everything. Add "(never tried)" lines to manual-testing.md.
