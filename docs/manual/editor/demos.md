---
title: Demos
order: 8
---

# Demos

**Resources → Demos…** (Ctrl+Shift+D) lists the game's demos and the ones recorded with the
[`recorddemo`](../../reference/console#recorddemo) command, and *Open .dmo…* (Ctrl+O) adds a demo
file to look at.

Picking one shows its controls along a timeline. Then the game plays it through, unseen, with the
mods, and writes down where everyone went: the demo's level shows with it, and the timeline goes
through it frame by frame.

- **Path**: where the player went, walked so far, then dashed where they go next;
- **Replay**: every actor, door and pushwall as it was at the frame shown;
- **Events**: kills, pickups, hurts, doors and pushwalls, as markers, and listed under *What
  happened*.

Click or drag on the timeline to go to a moment. Home and End go to the start and end, Left and
Right a frame back or on (with Shift, a second), and Space plays it, at the speed picked. *Follow
the player* keeps the player in sight. On the level, drag to pan and use the wheel to zoom.

**▶ Watch in PFWolf** (F5) starts the game playing the demo in its window, back to the title when
it ends; *Trace again* plays it through once more, after a change to the mods. *Save as .dmo…* saves
it as a file (`DEMO0-9.dmo` in the game's demos folder plays in place of the game's own), and *Add
to mod…* puts it in a mod's `demos/` folder, to ship with it: `DEMO0` to `DEMO9` play in the title
loop.
