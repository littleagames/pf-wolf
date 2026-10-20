# Title sequence plan: every game's front end from game-info

## Status (2026-10-20): done

- Phase 1 (8b45ac2) and phase 2 (3c4778d) are done. Phase 3's docs are the comment block above
  `title-sequence` in gamepacks/wolf3d/game-info.yaml.
- **Checked in the game:**
  - Wolf3D: notice until a key, PG-13, title 15 s, credits 10 s, high scores 10 s, demo, a key
    in the demo opens the menu, and Tab on a drawn screen opens the demo-level prompt.
  - Spear: PG-13, then the title in TITLEPAL.
  - Planet Strike: the first key finishes the fizzle and the second opens the menu.
- **Not checked:** wolf3d-shareware and spear-demo (no data files here), Blake AOG's intro
  (only its YAML moved), and an unskippable movie (`on-key: ignore`).
- **Differences from the plan below:**
  - `music-after` was dropped. The music playing before a demo always comes back after it,
    unless a key ended the demo.
  - A screen whose `pic` is missing is left out with a one-time warning. A `demo` screen in a
    game with no demos is left out too. A loop with nothing it can show goes to the menu.
  - `stripes: N` became `menu-background: true` plus `stripe: Y`, since the backdrop and the
    stripe are separate.
  - Every drawn screen fades out what's on screen first, which the notice needs after the
    signon.
  - Tab-to-record now works for every pack. Before, Blake's loop cleared the keys first.
  - On a demo, `ignore` and `finish` act as the list's default, because a demo always ends on
    a key.
  - The old top-level `intro:` / `title-loop:` keys print a startup warning.
- **Not built:** a list editor in the editor's Definitions window. The screens are edited in
  the raw YAML tab.

Goal: the screens from the signon to the menu (the intro shown once, then the title loop that
goes round until a key) come from a game-info `title-sequence:` block for every game pack
(Wolf3D and its variants, Spear and the Spear demo, Blake Stone, Planet Strike). A key press can
do something other than "go to the menu": skip to the next screen, finish an effect (Planet
Strike's title fizzle), end the intro, or be ignored.

## Decisions (2026-10-20)

- **One `title-sequence:` block** holds `intro:` and `loop:`, replacing top-level `intro:` /
  `title-loop:`. Breaking is fine (alpha). We fix the packs and example mods here. The user fixes
  their own mods (Midnight Assault, End of Destiny) in a separate session afterwards.
- **`non-shareware-notice` is removed.** The notice becomes an ordinary intro screen in Wolf3D's
  `title-sequence`.
- **Planet Strike's title:** the first key finishes the fizzle (TITLE2 shown) and a key after
  that goes to the menu.
- **No `goto` / labels.** Lists play in order. This can be added later without breaking anything.
- **`on-key` per screen only, with no named-key mappings.** Any key does the screen's action.
  Tab held as the loop ends still records a demo, as an engine rule as it is today (DemoLoop
  checks it after the loop for every pack).

## Where things stand

- `GameInfoAsset.Intro` / `TitleLoop` (`List<TitleScreenInfo>`, PFWolf.Common/Assets/GameInfoAsset.cs)
  are read from game-info `intro:` and `title-loop:`. A screen is a movie, a demo, or a drawn page
  (background, high scores, title-pics, a pic at x/y, presenter text), with palette, music,
  fade-from/fade-to colours, `fizzle-pic`, `seconds` / `until-music-ends`.
- `TitleScreens.RunTitleScreens` (PFWolf/Ui/TitleScreens.cs) plays them. A key in the intro skips
  the screen it's pressed on; a key in the title loop goes to the menu.
- Blake Stone, Planet Strike and examples/mods/standalone-demo use them. NewMod.cs writes a
  `title-loop:` into new mods.
- Wolf3D, Spear and their variants have none, so `TitleLoop.DemoLoop` (PFWolf/Ui/TitleLoop.cs)
  runs its hard-coded path: the non-shareware notice (drawn in code), PG-13, then title →
  credits → high scores → demo.
- The editor's PaletteCatalog and SoundCatalog read `Intro` / `TitleLoop` for "used by".

## YAML

```yaml
title-sequence:
  # Shown once as the game starts. A key goes to the next screen unless the screen says otherwise.
  intro:
    - stripes: 10                  # Wolf3D's non-shareware notice
      lines:
        - { text: "Attention", x: 110, y: 15, style: READHCOLOR }
        - { text: "This game is NOT shareware.\nPlease do not distribute it.\nThanks.\n\n        Id Software", x: 40, y: 60, style: HIGHLIGHT }
      wait-for-key: true           # no timeout
    - background: Light Blue       # PG-13
      pic: pg13
      x: 216
      y: 110
      seconds: 7
  # Over and over until a key. A key goes to the menu unless the screen says otherwise.
  loop:
    - title: true
      seconds: 15
    - pic: credits
      seconds: 10
    - high-scores: true
      seconds: 10
    - demo: true
      music-after: true            # put the title music back after the demo's
```

Planet Strike's title:

```yaml
    - pic: TITLE1
      palette: Extra3PAL
      fizzle-pic: TITLE2
      on-key: finish               # first key ends the fizzle; the next key does the list's default (menu)
      seconds: 6
```

### `on-key` actions

| Action | Does |
|---|---|
| `menu` | Leaves for the control panel (default in `loop`) |
| `next` | Goes on to the next screen (default in `intro`) |
| `end-intro` | Skips the rest of the intro into the loop (intro only) |
| `finish` | Completes the screen's effect (the fizzle) and keeps holding for the rest of `seconds`; a second key does the list's default |
| `ignore` | Keys do nothing: the screen runs its full time |

A screen with no effect left to finish treats `finish` as its list's default.

### Other new screen fields

- `stripes: N`: MenuDrawing.ClearMScreen + DrawStripes(N), the menu look.
- `lines:`: text at x/y in a menu style (or `font` + `color`). Mods can use it for a page of
  text without a VGAGRAPH presenter script.
- `wait-for-key: true`: held until a key (input.Ack), with no timeout.
- `music-after: true` on a demo screen: restarts `intro-music` once the demo ends.
- Pictures that don't exist skip their screen with no warning (replaces the
  `assets.Exists("pg13")` check), so a standalone game with no PG-13 isn't shown a black screen.

`intro-music`, `title-pics` and `title-palette` stay top-level: the menu and other screens use
them too.

## Phases

### Phase 1: title-sequence block and on-key (small to medium)

- GameInfoAsset: `TitleSequence { Intro, Loop }` in place of `Intro` / `TitleLoop`.
  `TitleScreenInfo` gets `OnKey` (enum), `Stripes`, `Lines`, `WaitForKey`, `MusicAfter`.
- TitleScreens: `RunTitleScreen` returns an outcome (`Next`, `Menu`, `EndIntro`) instead of
  `bool pressed`. `RunTitleScreens(screens, defaultAction)` walks the list and stops on `Menu` /
  `EndIntro`. `ignore` means a key doesn't end the hold; movies need MoviePlayer to take a
  "skippable" flag.
- `finish`: the fizzle (`video.Transition(FadeStyle.Fizzle, …)`) becomes interruptible by an
  opt-in flag that polls input each step and jumps to the end. Death and level fades keep the
  current path. After the fizzle ends early, the screen holds for the rest of its time with the
  list's default action.
- TitleLoop.DemoLoop reads `TitleSequence`.
- Move blake, planetstrike, standalone-demo and NewMod.cs's template to `title-sequence:`.
  Planet Strike gets `on-key: finish`.
- Editor: PaletteCatalog / SoundCatalog read the new lists. Check the Definitions window's
  game-info field editor for `intro` / `title-loop` keys.
- Tests: deserialisation and each action with a fake input (TitleLoopTests / new
  TitleScreensTests).

### Phase 2: Wolf3D and Spear as data (medium)

- Draw `stripes` / `lines`, `wait-for-key`, skip-if-missing, `music-after`.
- gamepacks/wolf3d: `title-sequence` with intro (notice, PG-13) and loop (title, credits, high
  scores, demo).
- Variants (the merge replaces lists whole, so `!replace` isn't needed for them; check how a
  nested block under `title-sequence` merges):
  - wolf3d-shareware: intro without the notice (PG-13 only).
  - wolf3d-apogee: inherits.
  - spear: intro of PG-13 only (it removes the notice today), and the same loop shape; `title:
    true` uses title-pics + TITLEPAL.
  - spear-demo: inherits Spear.
- Remove `non-shareware-notice` (GameInfoAsset, wolf3d/shareware/blake/spear YAML, the notice
  docs in gamepack-info / wolf3d game-info) and the hard-coded branch in DemoLoop, plus
  `TitleScreens.NonShareware` / `Pg13`. Search multiplayer-pk3 and examples/mods for leftovers.
- `--nowait` still skips the intro and loop, and Tab after the loop still records.
- Check by eye against the current build: wolf3d, wolf3d-shareware, spear, spear-demo (screens,
  timings, Spear's palette, music after the demo, widescreen margins), and Planet Strike's
  fizzle key. Run `demotest` too.

### Phase 3: docs and editor (small, optional)

- Document `title-sequence`, the screen fields and `on-key` atop gamepacks/wolf3d/game-info.yaml.
- Optional: a list editor for the screens in the Definitions window (or leave them to the raw YAML
  tab).

## Risks

1. **The interruptible fizzle.** The transition code is shared with death and level fades, so the
   interruptible path is opt-in.
2. **Matching Wolf3D's front end exactly** once the hard-coded path goes: music restarts, fade
   order, `ClearMargins` on wide screens.
3. **Nested-block merges** for child packs: confirm `title-sequence.loop` replaces the parent's
   list as top-level lists do.

## Follow-up (not this work)

- The user moves the Midnight Assault and End of Destiny mods to `title-sequence` in a separate
  session.
- Later, if wanted: `goto`, and keys mapped to actions by name (`title-keys`).
