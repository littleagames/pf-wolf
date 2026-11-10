# OPL editor: IMF songs, AdLib and PC speaker sounds (editor)

Tools in the editor for making the AdLib-era audio that has no modern stand-in: IMF songs
(`music/NAME.imf`), AdLib sound effects (`sounds/NAME.adl`) and PC speaker sounds
(`sounds/NAME.pcs`). The engine already loads all three from pk3s and mods; today the editor
only plays them (F8). Branch: feature/imf-editor.

Decided (2026-11-10): songs are edited as notes on a **piano roll**, with a raw command view
as the fallback; **MIDI import comes early** (phase 2); **PC speaker sounds** are the last phase.

## The formats

**IMF song.** A list of commands, 4 bytes each: OPL register, value, delay in ticks of
700 a second. An AUDIOT chunk (and a type 1 `.imf`) starts with a 16-bit byte length, so a
song is at most 16383 commands; a type 0 `.imf` is all commands. The song loops from the top.
`Wolf3dImfAudio` reads both, `ImfMusicSource` plays them on Nuked-OPL3 (buffered writes, so a
key-off and key-on in one tick strikes the note again).

What Wolf3D's songs look like (checked on all 27 WL6 songs, 2026-11-10):
- Muse output. An init block with no delay: `00=00`, `BD=00`, `08=00`, then each channel's
  instrument (operator registers 20/40/60/80/E0 for both operators, and C0).
- Notes are A0+n (F-number low) and B0+n (key on, block, F-number high) on **channels 1-8
  only**. Channel 0 is written once in the init and never keyed: DOS Wolf3D plays AdLib
  sound effects on channel 0. Rhythm mode (BD) is never turned on. OPL2 registers only.
- Instruments change mid-song (C0 writes: 13-48 a song).
- Each AUDIOT chunk has an 88-byte Muse tag after the commands: the file name
  (`CORNER.IMF`), the source path (`\sound\CORNER.IMF`) and data the engine ignores.

PFWolf renders AdLib sound effects on their own chip, so a song could use channel 0 here,
but not in DOS Wolf3D or Wolf4SDL.

**AdLib sound.** A 32-bit length, a 16-bit priority, a 16-byte instrument (mChar, cChar,
mScale, cScale, mAttack, cAttack, mSus, cSus, mWave, cWave, nConn, voice, mode, 3 unused),
an octave block, then one byte a tick at 140 a second: the F-number low byte, 0 for silence.
F-number high bits are always 0, so a tick's frequency is `tone * 49716 / 2^(20 - block)`.
Wolf3D (and `AdLibSound.ToPcm16`) ignores nConn and writes feedback/connection 0, and plays the
sound on channel 0.

**PC speaker sound.** A 32-bit length, a 16-bit priority, then one byte a tick at 140 a
second: the PIT reload value / 60, 0 for silence. Frequency = `1193180 / (value * 60)`.

## Where things go

- **PFWolf.Common, Assets/Sounds:** `OplInstrument` (the 11 OPL2 bytes of a two-operator
  voice, named fields, read/write to registers and to the AdLib sound layout), writers next to
  the readers: `Wolf3dImfAudio.ToFile` (type 1 with a short tag; type 0 past 16383 commands),
  `AdLibSound.Create`, `PcSound.Create`.
- **PFWolf.Editor, Audio/Opl:** the song model and everything that edits it (below), the
  OPL preview source, MIDI and bank readers.
- Saving is mods only, as the art and demo browsers do: `music/NAME.imf`, `sounds/NAME.adl`,
  `sounds/NAME.pcs` through `ModFiles`, in place of a mod's own file, or into a mod picked
  (`PickMod`) for one of the game's own; then the game is loaded again and the browser shows
  the new one. A new name plays by name (no sound-seq entry needed); game-info / mapdefs name
  a song to use it.

## The song model

```
ImfSong
  Ticks per row, rows per beat     (display grid only; 700 ticks a second underneath)
  Instruments: List<OplInstrument>
  Channels[9]: enabled, name, mute/solo (editor only)
  Notes: channel, start tick, length, F-number + block (exact), volume (carrier TL), instrument
  Extra: register writes the model doesn't understand, at their tick (kept so nothing is lost)
```

A note's pitch is kept as F-number + block, so a song read in is written out exactly; the
piano roll shows the nearest MIDI note and cents. Writing a note picks the F-number for its
MIDI note (A4 = 440 Hz).

**Reading (decompiling) IMF.** Walk the commands keeping each channel's registers: key bit
0→1, or off+on in one tick, starts a note; 1→0 ends it; an operator/C0 write on a channel
between notes is an instrument change (instruments deduplicated by their bytes); a carrier
TL change alone is a volume. Anything else (01, 08, BD, a write on a sounding note) goes to
Extra. A song that can't be read sensibly opens in the raw command view.

**Writing (compiling) IMF.** Init block (as Muse writes it), then at each tick: key-offs,
instrument changes, volumes, A0/B0 key-ons, Extra writes, in that order; delays fill the gap
to the next tick that has writes. Warns past 16383 commands and when channel 0 is used.

**Round-trip test (GameData):** every song in WL6, SOD, WL1 and Blake (AOG, PS) read and
written again gives the same register state at every tick (compared state by state, not
byte by byte, so command order inside a tick may differ). Plus synthetic tests.

## Phases

1. **OPL core and IMF round trip.** `OplInstrument`, the writers (IMF type 0/1 + tag,
   AdLib, PC) checked against the readers, `ImfSong` with the decompiler and compiler, the
   round-trip GameData test. F8 gets **Add to mod…** for a song or sound file from disk
   (`.imf/.wlf/.ogg/.mp3/.wav/.adl/.pcs`, picked by extension as the loader does) and
   **Export .imf/.adl/.pcs** for the game's own ones. No editing UI yet.
2. **MIDI import.** A hand-written Standard MIDI File reader (type 0 and 1, tempo map,
   running status), turned into an `ImfSong`: ticks from the tempo map, GM program →
   instrument from a bank, notes given out to channels 1-8 (oldest note stolen when they're
   all busy; how many were dropped is reported), velocity → carrier TL, channel 10 drums onto
   melodic channels with short percussion patches (rhythm mode isn't used, as in Wolf3D).
   Banks: a small built-in one of PFWolf's own hand-made patches (piano, bass, strings,
   brass, organ, lead, a few drums) and any DMX GENMIDI `.op2` (or `.wopl`) loaded from disk
   for full GM; no bank files from elsewhere ship with PFWolf. In F8: **Import MIDI…** →
   options (bank, channels to use, transpose, quantize) → preview → save to a mod as
   `music/NAME.imf`. Tests with hand-built MIDI bytes.
3. **Song editor (piano roll).** Non-modal window opened from F8 (**Edit** on a song, **New
   song…**), like the pixel editor from F6. Channel lanes in colors, mute/solo, draw/move/
   resize/delete notes, velocity, instrument per note (picker), grid from ticks per row/rows
   per beat with a BPM readout, a loop region, play from the cursor with a playhead, note
   audition while drawing. Instrument editor panel shared with phase 4: both operators'
   fields named (attack/decay/sustain/release with an envelope graph, multiplier, level, KSL,
   waveform, vibrato/tremolo/sustain/KSR), feedback and connection, audition. Instruments can
   be copied from any game song or AdLib sound. Undo/redo. Command count against the 16383
   limit. Raw command view (register, value, delay, with the register named) for songs that
   don't read as notes. Save to mod, Export .imf / WAV, Import MIDI into the open song.
   Preview latency: the editor's winmm player runs 4×100 ms buffers; smaller ones for the
   editor's audition if 400 ms feels slow.
4. **AdLib sound editor.** Opened from F8 (**Edit** on an AdLib sound, **New AdLib
   sound…**). The shared instrument panel (nConn shown greyed: Wolf3D ignores it), block,
   priority, and a per-tick pitch strip at 140 a second: draw a curve, set ticks silent,
   stretch/shrink, transpose; frequency and nearest note shown. Play, save
   `sounds/NAME.adl`, Export .adl / WAV.
5. **PC speaker editor.** Same per-tick strip for PIT values (frequency shown), priority,
   play, save `sounds/NAME.pcs`. **From the AdLib version:** the AdLib sound of the same name
   turned into PC values tick by tick (nearest frequency).

## Open questions

- Should the engine honour an AdLib sound's nConn (feedback/connection)? It would make
  sounds made here sound different in DOS Wolf3D. Default: no.
- Allow channel 0 in songs (PFWolf only) behind a warning, or not at all? Default: off, with a
  per-song switch.
- Which patches go in the built-in bank, and how they sound, is to be tuned by ear.

## Status

**Phase 1 done 2026-11-10** (uncommitted, feature/imf-editor):
- PFWolf.Common: `OplInstrument` / `OplOperator` / `OplRegisters` / `OplPitch`
  (Assets/Sounds/OplInstrument.cs); `Wolf3dImfAudio.ToFile`, `AdLibSound.Create` + Priority,
  Instrument, Block, Tones; `PcSound.Create` + Priority, Values.
- PFWolf.Editor/Audio/Opl: `ImfSong` (notes, patch changes, volume changes, kept writes),
  `ImfSongReader`, `ImfSongWriter`. The reader's rules are in its summary; a key-off is only
  folded into a note when it's the channel's last B write in the tick.
- F8: **Export file…** (.imf / .adl / .pcs as the engine reads them; an AUDIOT song keeps its
  Muse tag), **Add song to mod…** and **Add sound to mod…** (Data/SoundFiles: checked by reading
  it as the engine will, named after the one picked when it's that kind, other files of the
  name and kind in that mod taken out; a sound's .wav, .adl and .pcs live side by side).
- Tests: ImfSongTests, SoundFilesTests, GameData ImfSongGameTests. Every song of Wolf3D (27),
  Spear (24), Wolf3D shareware (11), Blake (19), Planet Strike (21) and Blake shareware
  reads and writes back with the same strikes and registers at every tick; every Wolf3D and
  Blake AdLib/PC sound rewrites byte for byte. GameContent wants `blake` / `planetstrike`, not
  the release ids (an unknown id silently loads Wolf3D).
- Checked in the editor by screenshot: the new buttons, Export file… on for a song. Not tried
  by hand: the file pickers, an actual Add into a mod (folder or pk3), exported files elsewhere.

Numbers for phase 3: Wolf3D's songs read as 23382 notes with 1451 kept writes; Spear 2180,
Blake 3440 kept writes (worth a look at what they are before the raw view is designed).
