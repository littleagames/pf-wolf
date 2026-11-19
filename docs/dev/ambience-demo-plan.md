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
generators are switched off with Use (not shot: no shootable-prop pattern to build on). Phases
3 and 4 are still open. The mod-facing changes (new sound-seq and zone fields, `game.ambient`'s
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

### Phase 3: placed loops and muffling (the bigger engine work)

- **3a Looping emitters.** Actordefs property `loopsound: <sound>` (plus `loopradius:`): the
  actor's own looping OpenAL source that follows it, playing only for the nearest N emitters
  (a fixed pool, say 8, so the 16 effect voices are left alone). It stops when the actor is
  removed or changes state to one with `loopsound: none`. Needs no save change, since it comes
  from the actor's class and state. This gives the generator, a waterfall, a fireplace, and
  machinery you walk up to.
- **3b Ambient layers.** `game.ambient(sound, layer)` / zone `ambient-2:` for a second loop over
  the first (rain on the roof over the courtyard wind).
- **3c Door muffling.** Placed sounds (one-shots and 3a loops) from an area that isn't connected
  to the player's play quieter (a gain factor, say 0.3). If EFX is there, they also go through a
  low-pass filter. Uses `Session.Areas.AreaByPlayer`, as `A_ActiveSound` does. Open the door and
  the generator gets loud. This one changes how stock Wolf3D sounds, so it should be a Game
  Options switch that's off by default.

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
4. **Lua vs zone ambience (2d):** Lua wins, or layers?

## Testing

- Unit tests: `ZoneAmbience.Resolve` already covered. Add GameData tests that load the mod and
  check its zones resolve, and (phase 2/3) tests for `game.playsound`, fade timing, emitter pool
  picking the nearest N (`PickSharedSource`-style static helper).
- Headless: `ALSOFT_DRIVERS=null` keeps OpenAL silent. A scripted demo walking the corridor can
  log `AudioManager.Ambient` per zone.
- By ear: everything. Add "(never tried)" lines to manual-testing.md.
