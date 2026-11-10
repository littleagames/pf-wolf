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

**Phase 2 done 2026-11-10** (MIDI import):
- `MidiFile` (Audio/Opl): type 0/1/2, running status, tempo map (and SMPTE timing), program,
  CC 7 volume, CC 11 expression, pitch bend with its RPN 0 range kept as a note starts (bends
  during a note are counted, not played), notes never let go end with the file, optional
  snapping to 1/8, 1/16, 1/32 notes. Length = where the last track ends.
- `OplBank`: PFWolf's built-in bank (16 family voices + 7 drums, made here, rough, to be tuned
  by ear) and DMX GENMIDI `.op2` (first voice of each instrument; fixed-note flag; note
  offset). `.wopl` not done: no file on hand to check the format against.
- `MidiImport.ToSong`: notes to channels 1-8 (0 only when allowed and the rest are busy),
  a free channel already set to the instrument first, else the one free longest, else the
  oldest note is cut short; volume = carrier level + 40·log10 of velocity, CC 7 and CC 11 in
  0.75 dB steps (carrier only, so an additive voice's modulator stays as loud); drums on the
  same channels with the bank's fixed notes; song starts Muse-style; loops where the file ends.
  `MidiImportReport` says what was cut short, out of range, missing drums, skipped bends, and
  warns past 16383 commands.
- F8 **Import MIDI…** → MidiImportWindow: report, bank (+ Load GENMIDI bank…), transpose,
  snap, OPL channels, MIDI channels; Play; Save to mod… (music/NAME.imf through the same
  save as Add song to mod, named after the song picked in the browser if any). Tests in
  MidiImportTests (MIDI bytes built in the test).
- Checked in the editor with a generated 3-track MIDI file: the dialog, report, toggling drums
  off and channel 0 on. Not tried: Play (by ear), Save to mod, a real-world MIDI, a real
  GENMIDI file (the .op2 layout is from Chocolate Doom's structs; whether DMX's notes come out
  an octave off needs an ear).

**Phase 3 done 2026-11-10** (song editor):
- Model: `ImfPitchChange` — A/B written while a note read as one sounds (no re-strike) is the
  note's pitch change, so it moves and transposes with the note. Kept writes fell to Wolf3D
  1032, Spear 1327, Blake 379 (what's left: Muse's pitch-set-ahead writes while a channel is
  silent, and the start block); pitch changes: Wolf3D 402, Spear 847, Blake 2995. A note's
  key-off is now at the pitch it's sounding at. Round trip still exact for every game.
- `ImfSongEdits`: add (cuts short / takes out what it lands on, one note a channel), remove,
  move (time, semitones, channel; pitch changes go along), resize (not into the next note),
  volume, per-note instrument (changes back for the next note), `GuessTicksPerRow`
  (fractional: Muse's songs aren't on whole ticks — Dungeon's gaps 58-62, Blake's 87/88 —
  most common gap ≥ 10 ticks, its half/third/quarter if common too, least-squares fitted).
- `ImfRangeSource` (play from a tick, loop a region; earlier commands written first so notes
  already keyed sound), `OplAudition` (a note of an instrument).
- SongEditorWindow (F8 **Edit song…**, **New song…**, and Import MIDI's **Edit in the song
  editor…**): PianoRollView (channel colors, dim muted, pitch-change lines, ruler cursor,
  loop region, song end; Draw/Select tools, Shift-box, Ctrl-click, drag to move, right
  edge to resize, arrows, Delete, Ctrl+A, Ctrl+wheel zoom), side tabs Song (channels:
  current, mute, solo; length), Notes (volume 0-63, instrument, to channel, delete),
  Instrument (list, New, copy from any game song/AdLib sound/built-in voice, Try, feedback,
  additive, each operator by name with an envelope graph), Raw (kept writes, every command).
  Undo/redo of whole-song snapshots; instrument edits within 2 s are one step; "*" tracks the
  saved version (undo back to it clears). Save to mod (music/NAME.imf), Export .imf / WAV.
- Tests: SongEditorTests (edits, grid, playback, VM undo/dirty/save/mute).
- Checked by screenshots: GETTHEM open, ruler cursor, drawing a note (took out 10 channel-1
  notes it landed on, now said in the status), undo, box select, Instrument tab. Not tried:
  Play / audition by ear, dragging notes by mouse, resize by mouse, Save to mod, Export
  dialogs, a Blake song's pitch-change lines, the close prompt.

**Phase 4 done 2026-11-10** (AdLib sound editor):
- Views/InstrumentPanel (UserControl): the instrument settings, now shared by the song
  editor and this one; `InstrumentEditorViewModel.IgnoresFeedback` greys feedback/additive
  with a note (Wolf3D writes 0xC0 = 0 for sound effects; the byte is still kept in the file).
- `AdLibDraft` (priority, instrument, block, tones) and AdLibEditorViewModel: strokes (a drag
  is one undo step, the sound grows when drawn past its end), snap to semitones, Up/Down a
  semitone, Longer/Shorter (×2, ½, same pitches), Silence, Take out, on the ticks picked or
  all; Block ± keeping the pitch (tones doubled/halved); length, block, priority; instrument
  edits within 2 s one step; copy instrument from the library; Play as the game does
  (AdLibSound.ToPcm16) with a playhead; Save to mod (sounds/NAME.adl, the browser's name
  prompt), Export .adl / WAV; "*" against the saved version.
- ToneStripView: a column a tick at its pitch (rows a semitone, C's labelled, tones 8-255
  shown), left draws (interpolating between mouse points), right erases, Shift-drag or the
  ruler picks ticks, Ctrl+wheel zooms; hover line says tick, tone, Hz, note.
- F8: **Edit AdLib sound…** (the AdLib sound picked, or the one a sound-seq entry's picked
  variant plays) and **New AdLib sound…** (a second of silence, block 4, priority 50, the
  game's first AdLib sound's instrument).
- Tests: AdLibEditorTests. Checked by screenshots: ALHITWAL open (8 ticks), New AdLib sound,
  a drawn sweep (one undo step, snapped). Not tried: Play by ear, erasing/picking by mouse,
  Save to mod, the export dialogs, the close prompt.

**Phase 5 done 2026-11-10** (PC speaker editor):
- `ToneDraft` / `ToneSoundEditorViewModel`: what the AdLib and PC speaker editors share (strokes,
  snap, Up/Down, Longer/Shorter, Silence, Take out, length, priority, undo/redo with versions,
  Play, Save to mod), pitches through each draft's `Frequency` / `ToneOfFrequency`, so Up on a PC
  sound is a smaller value. `AdLibDraft` and AdLibEditorViewModel now sit on it (block, instrument).
- `PcDraft` (priority, a value a tick; 1193180 / (value × 60) Hz; played through `PcSound.ToMono8`)
  and PcSpeakerEditorViewModel: **Make it from** the AdLib version, tick by tick, each tone as the
  nearest value, **octaves higher** (default 1). The AdLib version is found through the sound-seq
  entries (`SoundCatalog.PartnerOf`: PCHITWAL goes with ALHITWAL), else the same name. The games'
  own PC versions weren't made from the AdLib ones: Wolf3D's sit a median 13 semitones above,
  Blake's 9 (hence the octave default); every game PC sound uses values 15-160, so the strip shows
  8-255 (2486 Hz down to 78 Hz).
- ToneStripView takes either editor (PC values drawn green); a drag now goes between mouse
  points in pitch, not in tone numbers.
- PcSpeakerEditorWindow; F8 **Edit PC speaker sound…** (the PC sound picked, or a sound-seq
  entry's picked PC variant) and **New PC speaker sound…** (a second of silence; with an AdLib
  sound shown, named as its PC version and able to be made from it).
- Tests: PcSpeakerEditorTests, GameData `Pc_Sounds_Open_In_The_Editor_As_They_Are_And_AdLib_Ones_Make_Pc_Ones`.
  Checked by screenshots: PCHITWAL open (8 values, paired with ALHITWAL), Make it from ALHITWAL
  (an octave up, "*", undo). Not tried: Play by ear, drawing/erasing by mouse, New PC speaker sound
  from an AdLib sound in the window, Save to mod, the export dialogs, the close prompt.

Numbers for phase 3 (before pitch changes): Wolf3D's songs read as 23382 notes with 1451 kept writes; Spear 2180,
Blake 3440 kept writes (worth a look at what they are before the raw view is designed).
