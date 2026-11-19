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
command, the generators' drone and the sewer's outflow in the demo); **3b is done** (2026-11-19:
zone `ambient-2*`, `game.ambient`'s layer, the courtyard's rain); **3c is done** (2026-11-19:
`Gameplay/SoundMuffling.cs`, Game Options "Muffle Thru Doors", on in the demo). Phase 3 is done.
Phase 4 (reverb zones, and a low-pass on muffled sounds) is planned in detail below: **4a is
done** (2026-11-19: `Managers/AudioEffects.cs`, the `soundinfo` command, muffled sounds dulled;
EFX proven on the null driver and on this PC's device: OpenAL Soft 1.23.1, EFX 1.0, 4 sends a
source); **4b is done** (2026-11-19: `Managers/ReverbMixer.cs`, `Gameplay/ZoneReverb.cs`,
`ReverbNames` in Common, game-info `reverb`/`reverb-fade`, sound-seq `reverb`, `game.reverb`,
Options → Sound → Reverb, the `reverb` and `s_reverb` commands, the editor's fields, every demo
room's reverb). 4c (custom reverbs) is left for later, as recommended.
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
  loop plays over the first with the same `none` rule and the same `ambient-fade`. Carry-over
  goes by the zone as a whole (decided while building it): a zone with any ambient sound sets
  both loops, so one without an `ambient-2` stops the second as you step in (else the
  courtyard's rain would follow you into the hall); one with none at all, a door, leaves both.
  `DescribeAmbient` adds ` | layer 2: …` when the second loop plays.
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
  - At most three doors count (`SoundMuffling.MaxDoors`), and an area that no door path
    reaches at all (only through a pushwall, say) counts as three: as built, in place of the
    first idea of `muffle` squared, which would have been louder than three doors.
  - This beats the plan's first idea, AreaByPlayer's yes or no. A room two doors off is
    quieter than one next door, and a generator through one door is still heard.
- **Where it's worked out:** `AreaConnections.DoorsBetween(from)`, a 0-1 breadth-first search
  over the door graph (each door joins its two areas; open costs 0, shut costs 1).
  - It's a pure static helper with unit tests.
  - As built, it's worked out afresh each tic in `Gameplay/SoundMuffling.cs` (Wolf3D has 37
    areas, so it's cheap), not cached: AreaConnect is changed in several places (doors,
    destructible walls, a loaded game), and nothing can go stale this way. Each door's two
    areas go in `AreaConnections.DoorJoin` as the level's doors are spawned.
  - It must be the **listener's** area: the camera tile's, else (in a doorway) its actor's,
    else the last one. `AreaByPlayer` is every player's in multiplayer, so it would be wrong.
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
  - As built, the menu row is "Muffle Thru Doors" (Blake Stone's and Planet Strike's Game
    Switches too). Wolf3D's Game Options and Blake's Game Switches got one row too many, so
    they scroll now (`visible-rows` 9 and 11), and GameOptionsMenu redraws on the menu loop's
    scroll (-2), which it used to take as leaving the menu.
  - The `loops` command shows a voice's muffling ("muffled to 0.35").
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

### Phase 4: reverb zones and a muffled low-pass (planned 2026-11-19)

A sewer that sounds like a pipe, a hall that rings, the player's gun echoing in a cave; and a
sound behind a shut door that's duller as well as quieter. All of it through OpenAL's EFX
extension.

#### What's there to build on (checked 2026-11-19)

- **OpenTK.Audio.OpenAL 4.9.4 has the whole EFX API** (`ALC.EFX`):
  - `GenEffect`, `Effect(id, EffectInteger.EffectType, EffectType.EaxReverb)` and the reverb's
    `EffectFloat`/`EffectVector3` parameters.
  - `GenAuxiliaryEffectSlot` and `AuxiliaryEffectSlot(slot, EffectSlotInteger.Effect, effect)`,
    with `EffectSlotFloat.Gain`.
  - `GenFilter`, `Filter(id, FilterInteger.FilterType, FilterType.Lowpass)`, `FilterFloat`
    `LowpassGain` and `LowpassGainHF`.
  - Per source: `Source(src, EFXSourceInteger.DirectFilter, filter)` and
    `Source(src, EFXSourceInteger3.AuxiliarySendFilter, slot, send, filter)`.
  - `IsExtensionPresent(device)`.
- **It ships 113 named presets**, `ReverbPresets.*` as `ReverbProperties` (Creative's
  efx-presets.h): Cave, Sewerpipe, Hangar, Hallway, StoneCorridor, StoneRoom, Room,
  LivingRoom, Bathroom, Arena, Underwater, CastleCourtyard, FactoryHall, SpaceStationHall,
  Forest, Mountains, Drugged, Dizzy… so nobody has to tune reverb numbers.
- **OpenAL Soft 1.23.1 is bundled for every OS, macOS included** (the mac-linux work), and it
  has EFX. The first plan's note that Apple's framework has none only matters if the bundled
  library fails to load. Then `IsExtensionPresent` is false and everything stays dry, with a
  line in the `reverb` command.
- `ALSOFT_DRIVERS=null` still runs EFX, so headless tests can check the bookkeeping.
- Phase 3c already leaves the hook for the low-pass: each placed source's muffle level moves in
  `AudioManager.TickMuffling`.

#### 4a EFX plumbing, and muffled sounds lose their treble

- **`Managers/AudioEffects.cs`:** opened with the device (on AudioManager's open thread, the
  context current).
  - It finds out whether EFX is there.
  - It makes the two auxiliary slots (4b) and one scratch low-pass filter.
  - It deletes them all at Shutdown.
  - With no EFX, every call does nothing.
- **The low-pass:** with muffling on, a muffled source (`_sourceMuffles` / `_loopMuffles`
  below 1) gets a direct low-pass of gain 1 and GainHF = muffle^1.5, re-attached as its muffle
  moves. The level is still the gain's job, as now. OpenAL copies a filter's values as it's
  attached, so one scratch filter serves every source. Unmuffled sources get no filter.
- **No new setting:** it's part of Muffle Thru Doors, on whenever that is and EFX is there.
- **Tests:**
  - A pure helper `LowpassFor(muffle)`, unit tested.
  - The `loops` command adds " and dulled" after "muffled to …" when a voice is filtered.
  - The muffling game test checks it.
- **As built:** the slots wait for 4b; 4a makes only the filter. A `soundinfo` console command
  shows the device, OpenAL's version and the EFX line (or why there's none).

#### 4b Reverb zones

- **Where it's said:**
  - A map's `reverb: <preset>` is where the level starts.
  - A zone's `reverb: <preset>` takes over as the player steps in.
  - A zone with no `reverb` (a door, a doorway, a light zone) keeps whatever's playing, as
    ambient does.
  - `none` is dry.
  - `reverb-fade: <tics>` on the zone or map (default 35) crossfades the change.
  - `default-map` gives every level's default.
- **Preset names:** the OpenTK preset names in hyphenated lower case (`cave`, `sewer-pipe`,
  `stone-corridor`, `castle-courtyard`, `space-station-hall`…).
  - `PFWolf.Common` keeps the list of names (`ReverbNames`) for the docs, the editor and load
    warnings.
  - The engine maps a name to its `ReverbPresets` field.
  - A unit test checks every name resolves and every preset has a name.
  - An unknown name warns once and is dry.
- **ReverbMixer (`Managers/ReverbMixer.cs`):** pure and hooked, as AmbientMixer is. It has
  two auxiliary slots.
  - A change loads the new preset into the idle slot's EAX-reverb effect, moves every
    source's send to it, and fades the slot gains (old out, new in) over the fade.
  - The old slot's tail rings on while its gain falls, so a gunshot fired as you leave a cave
    still echoes behind you.
  - If the EAX reverb type isn't there, it falls back to the standard reverb type, using the
    preset's matching fields.
- **What's sent to it:**
  - Every game sound: effects, channels (the radio) and the 3a loops.
  - The player's own weapons, which is the point: a pistol in a cave.
  - Not the music, and not the ambient loops, which are recorded "in the space" already, and
    a cave's wind through a cave reverb turns to mush.
  - A sound-seq `reverb: 0..1` sets a sound's send level (1 by default; 0 keeps a sound
    dry: menu clicks, a narrator).
- **Menus and pausing:** pausing drops both slots' gains at once, so a menu click is dry.
  Resuming brings the zone's back.
- **Lua:** `game.reverb(preset[, tics])` holds the reverb over the zones until
  `game.reverb(nil)`, as `game.ambient` does: an underwater section, a dream with `drugged`.
  It isn't saved, as the script's own state isn't.
- **Setting:** Options → Sound gets a "Reverb" checkbox (`s_reverb`, config.cfg), on by
  default. A stock game has no reverb zones, so nothing changes until a mod uses them.
  - Wolf3D's Sound menu has room for the row (the window grows by one row).
  - Blake Stone's needs checking.
- **Console:** `reverb` shows the preset playing, each slot's level, and whether EFX is there.
- **Editor:** a map-level Reverb field in Map Properties and a "Reverb" column in the zones
  grid, as drop-downs of the preset names. The YAML round trip is tested as `ambient-2` is.
- **Multiplayer and demos:** only for show, local to the listener (the camera's zone, as
  ambience). Nothing in TicCmds or saves.

**As built (4b):**
- The reverb follows the **camera's** zone, as muffling does, not the player pawn's as
  ambience does.
- OpenTK spells the sewer preset `Sewerpipe`; the list writes `sewer-pipe`, and names match
  without hyphens, spaces or underscores and in any case.
- `s_reverb` is saved as a "reverb off" bit in config.cfg's audio-devices byte, so older
  configs come up with it on.
- The `reverb` command also takes a preset (held over the zones, as `game.reverb`), or `zones`
  to give it back, with Tab completing the names.
- The EAX reverb type was there on OpenAL Soft's null driver, so the standard-reverb fallback
  hasn't been run.
- StopAll (a level's end) silences both slots, so the intermission is dry.

#### 4c Custom reverbs (small, optional)

game-info `reverbs:` holds named reverbs, each a `base:` preset with any of its numbers changed
(`decay-time`, `gain`, `density`, `diffusion`, `reflections-gain`, `late-reverb-gain`,
`decay-hf-ratio`…). Zones and `game.reverb` name them like presets. It's for a pack that wants
"Hitler's bunker" tuned by ear, rather than the closest of Creative's 113.

#### The demo, after phase 4

| Room | Reverb |
|---|---|
| Hall | `hallway` |
| Courtyard | `castle-courtyard` |
| Sewer | `sewer-pipe` |
| Generator room | `factory-medium-room` |
| Guard post | `stone-room` |
| Radio room | `living-room` |
| Elevator room | no zone, so the hall's carries on |

- The README tells you to fire the pistol in each room.
- Shut a door on the generators and they go quieter **and** duller (4a).
- If 4c goes in, the script shows `game.reverb("underwater")` on a switch, or one custom reverb
  for the sewer's far end.

#### Risks

- EFX function pointers are loaded through `alGetProcAddress` with the context current.
  AudioManager opens the device on a task thread, but the context is current for the whole
  process, so it should work. 4a's first step is to prove it with the null driver in a test.
- The reverb's cost is OpenAL Soft's own. Two slots is the default limit
  (`EFXContextAttributes.MaxAuxiliarySends`); we need one send per source, so there's no need
  to raise it.
- Nobody can judge reverb but by ear. Every preset choice in the demo is a guess until it's
  listened to.

#### Testing

- **Unit tests:**
  - `ReverbMixer` (crossfade, the same preset asked again, `none`, a pause, StopAll).
  - Preset name mapping both ways.
  - `LowpassFor`.
  - Zone `reverb` resolution and carry-over in `ZoneAmbienceTests` (or a ZoneReverb class
    beside it).
  - The editor's YAML round trip.
- **Game tests:** AmbienceDemoGameTests reads `reverb` room by room, and checks that a
  `reverb` change while paused is dry.
- **Demo sync:** the 8 demos stay trace-identical.
- **By ear:** everything, as "(never tried)" lines in manual-testing.md.

Mod-facing additions for the release notes: zone and map `reverb` / `reverb-fade`, sound-seq
`reverb`, `game.reverb`, the `s_reverb` setting, and (4c) game-info `reverbs:`.

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
   *(Taken: with phase 4.)*

Phase 4's, with the recommendation first:

9. **Where reverb comes from:** carried over as ambient is (a zone with no `reverb` keeps the
   last one, and the map's `reverb` is where the level starts; recommended), or every zone
   without one going back to the map's (simpler to read, but a doorway between two reverb
   rooms would flicker through the map's)?
10. **Which sounds reverberate:** every game sound including the player's weapons, but not
    music or the ambient loops, with sound-seq `reverb: 0` to keep one dry (recommended); or
    placed sounds only?
11. **The switch:** Options → Sound "Reverb" (`s_reverb`, on by default, since stock games have
    no reverb zones; recommended), or Game Options (already scrolling), or no switch?
12. **Custom reverbs (4c):** with phase 4, or later if the presets turn out to be enough
    (recommended: later)?

*(9 to 12 taken 2026-11-19 as recommended: the user started 4a and 4b on the defaults.)*

## Testing

- Unit tests: `ZoneAmbience.Resolve` already covered. Add GameData tests that load the mod and
  check its zones resolve, and (phase 2/3) tests for `game.playsound`, fade timing, emitter pool
  picking the nearest N (`PickSharedSource`-style static helper).
- Headless: `ALSOFT_DRIVERS=null` keeps OpenAL silent. A scripted demo walking the corridor can
  log `AudioManager.Ambient` per zone.
- By ear: everything. Add "(never tried)" lines to manual-testing.md.
