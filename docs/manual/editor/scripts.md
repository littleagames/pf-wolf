---
title: Scripts
order: 7
---

# Scripts

**Resources → Scripts…** (F12) lists the Lua scripts the game runs (`scripts/*.lua` in pfwolf.pk3
and the mods), in the order it runs them. Find one by name, by mod, or by an action it gives
(`A_FireRing`).

Picking one shows its Lua, with:

- **Actions it gives**: its global functions named `A_...` or `T_...`, which actordefs states can
  call, each with the states that call it (a click shows that state's frame in the definitions
  window);
- **Hooks it adds**: its `game.on...` hooks (`game.onlevel`, `game.ontick`, `game.onkill`...);
- what's wrong with it, by line: an error keeps the game from loading the script.

Picking any of these goes to its line.

## Writing Lua

The editor highlights Lua and offers what can come next: after a `.`, a `:`, a quote or two
letters of a word, and on Ctrl+Space. It knows the [scripting API](../../reference/scripting) (the
`game` table, an actor's fields and methods) and the names the game has, so `game.actors("`
offers class names. Holding the pointer over a name says what it is.

| Key | What it does |
| --- | --- |
| Ctrl+Space | Offers what can go here |
| F12, or Ctrl+click | Goes to where the name under the caret is defined |
| Alt+Left | Goes back |
| Shift+F12 | Lists the actordefs states that call the action under the caret |
| Ctrl+F | Finds a script |

**Save** (Ctrl+S) writes it back into its mod; one of pfwolf.pk3's goes into a mod picked, in its
place. *Save to…* writes it into another mod's scripts folder, and *Revert* goes back to it as the
game has it. **New…** (Ctrl+N) starts a script, to save in a mod, and *Delete…* takes a mod's own
script out.

[Your first script](../tutorials/first-script) is a place to start.
