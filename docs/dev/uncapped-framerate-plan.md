# Uncapped frame rate

Drawing the view as often as the display can show it, not once per 70 Hz tic, with
everything that moves placed between where it was on the last two tics. The game itself
still runs at exactly 70 tics a second, so how it plays, demos and multiplayer don't change:
only how many pictures of it there are.

## Why

The game draws at most 70 frames a second. A display at 60, 144 or 165 Hz can't show 70
evenly: some frames stay up a refresh longer than others, which shows as judder, most of all
when turning. Drawing at the display's rate, with motion smoothed between tics, makes each
refresh a new picture.

## How the loop works today

`LevelLoop.PlayLoop`, once a frame: `PollControls` (input, then `CalcTics`, which sleeps until
at least one tic is due) → everyone thinks for those tics → `ThreeDRefresh` draws → keys,
console. A frame and a batch of tics are the same thing.

## What changes

- **Render-only frames.** When no tic is due yet, the loop doesn't sleep: it draws again and
  presents. Input events are still read (the window stays live), but nothing thinks, and
  the controls aren't consumed: mouse movement waits in SDL for the next tic as now.
- **Interpolation.** Before each batch of tics, the loop keeps where the camera and every
  actor were (x, y, angle, pitch, eye height). A frame draws them at
  `previous + (current - previous) * fraction`, where fraction is how far real time has
  got through the current tic. The view is at most one tic (14 ms) behind, as in other
  ports that do this. Anything that moved more than a tile in a tic (a teleport, a respawn,
  a new level, a load) is drawn where it is, not slid there.
- **Smooth angles.** The view angle is whole degrees on each tic. The ray tracer already
  works in tenths of a degree (`MidAngle`), so an in-between angle is drawn without
  changing it; the view's sin/cos come from the angle itself, not the whole-degree tables.
- **Pacing.** With vsync on, presenting waits for the display, so frames come at its rate.
  With vsync off, frames are capped at `vid_maxfps` (default: the display's refresh rate,
  read from SDL), sleeping until the next frame or tic is due, whichever comes first.
- **What the renderer does to the game.** Drawing the view marks what's seen: the automap's
  floor, enemies' Visible flag and single-player wake-ups. On render-only frames those come
  from the smoothed view, up to a tic behind; the game sees them within the same 14 ms as
  now. Demo playback, demo recording, demotest and multiplayer keep today's one-frame-a-tic
  loop, so they stay identical to vanilla.

## Settings

- `vid_uncapped` (Video menu "Uncapped frame rate"): on by default; off is today's loop.
- `vid_maxfps <n|0>`: the cap with vsync off; 0 is the display's refresh rate.
- VSync: with uncapped on, it applies in a window too. There's no tic sleep left for it to
  fight (the stalls `PresentsInVSync` worked around came from that mix). With uncapped off,
  it stays fullscreen-only.

## Phases

1. **Loop and view.** Render-only frames, the fraction, camera and actor positions smoothed,
   smooth view angle, pacing, `vid_uncapped`/`vid_maxfps`, config.cfg. Check: `perf` shows
   frames at the display's rate in a window and fullscreen; demotest unchanged; turning looks
   smooth. *Done 2026-10-18:* `FrameInterpolation` (Rendering/), `LevelLoop.DrawBetweenTics`/
   `WaitForFrameSlot`/`DrawFrame`, a render-only frame draws with `session.Tics` 0 so nothing
   drawn moves on; frame-rate block appended to config.cfg (classic flag + max fps). Measured
   windowed at 165 Hz: ~160 fps with vsync, 159 capped at the refresh rate without, exactly
   100 at `vid_maxfps 100`, classic exactly 70. Fullscreen not run; smoothness not judged by eye.
2. **Everything else that moves.** Doors and pushwalls sliding, weapon bob and the weapon
   raising/lowering, the death cam's turn, a spectated actor's view, the chase cam,
   projectiles' smoke. Mouse turning applied to the view on every frame (the tic still turns
   the player as now), so it feels as quick as the display.
3. **The rest.** Uncapped drawing in multiplayer and demo playback, with render-only frames
   that leave the game untouched (no seen-marking) so lockstep and demos stay in sync; menus'
   fades at the display's rate; Video menu entries.

## Decisions (2026-10-18)

- Uncapped is on by default.
- With uncapped on, VSync applies in a window too.
