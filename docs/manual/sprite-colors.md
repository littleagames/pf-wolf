---
title: Sprite colors
order: 5
---

# Sprite colors

A color translation recolors a sprite's palette colors as it's drawn, so one set of sprites can
come in several colors: the player's allies in green, a player in their team's shirt, a ghostly
grey guard. Translations are named in `translations.yaml`, and the
[translations reference](../reference/translations) says how one is written.

## Which translation an actor is drawn through

An actor is drawn through one translation at most: the first of these that it has.

1. **Its own.** A mapdefs thing's `translation:` (`things: 400: { class: Guard, translation: x }`),
   else its actordefs class's `translation` property. A script can set `actor.translation`, and
   the `settranslation` console command tries one out.
2. **A player's.** A team game's flag carrier is in their carrier colors; otherwise a player is
   in the color they picked (game-info `player-colors`), else their class's `translation`. A
   disguised Prop Hunt prop is drawn as its decoration is.
3. **Its faction's.** An enemy with health on a faction is drawn through that faction's
   `class-translations` entry for its class (or the nearest class it descends from), else the
   faction's `translation`. See [Factions](./factions).

Projectiles, and anything else without health, aren't drawn through a faction's colors.

An actor that turns into another (Hitler out of Mecha Hitler's armor) is drawn in its new class's
own `translation` if it has one, else keeps the colors it had. A projectile an actor fires takes
only its own class's.
