# Prop Hunt plan

A new versus match mode on `feature/multiplayer/prop-hunt`. Two teams. **Props** disguise
themselves as decorations on the map (lamps, barrels, plants, tables) and hide. **Hunters**
start blind, then have a time limit to find and shoot every prop. Props win the round if any
of them are still alive when time runs out. Hunters win if every prop dies first. Teams swap
each round, and the team that wins more rounds wins the match.

## Why it fits

Most of it is already in place from the other match modes:

| Need | Already there |
|---|---|
| Mode, rules, scoreboard, time limit | `GameMode` enum + `GameModes.cs`, `Deathmatch` match core, `NetRules`, lobby Game row |
| Two teams, no friendly fire | `PlayerState.Team`, `SmallerTeam`, `session.AreRivals` |
| Per-mode logic with its own state | `IMatchObjective` / `MatchObjective` (`LevelSetup`, `Tick`, `PlayerDied`, `MayRespawn`, `StatusLine`, `Write`/`Read`) |
| Looking like something else | `PlayerBodies`: a body is any actordefs class (`CreateActor`). `CarrySprite` already swaps the drawn sprite |
| Dead players watching others | `NetOverlay.WatchNextPlayer` (turn left/right while dead) |
| Staying dead until later | `MayRespawn` (Horde already uses it between waves) |
| Same result on every machine | Lockstep `TicCmd`s and the session's random numbers. Mid-game join sends `Objective.Write` |
| Headless tests | `--exec "wait; addplayer; gamemode ...; match; quit"` |

### What Wolf3D makes harder

- **Decorations sit in tile centres.** A prop body drawn off-centre gives the prop away.
  Fix: a still prop's body is drawn at the centre of its tile (see decision 3).
- **Shots auto-aim in a cone (`ShootCandidatesFrom`).** A hunter only needs to face the
  prop, not aim at it. That makes it easy to "spray" every decoration in the room. The usual
  Prop Hunt counter is a penalty for shooting the wrong thing (decision 2). With real
  decorations counted as targets, a prop standing behind a real lamp is also covered by it.
- **Decoration sprites don't rotate.** Good: a prop looks the same from every side.
  The body's `Angle` doesn't matter while disguised.
- **No chase cam yet** (deferred in camera-spectate). Props see in first person unless we
  build one (decision 1).

## Phases

### Phase 1: the mode and disguises (done 2026-10-17, uncommitted)

As built: `GameMode.PropHunt`, `MatchModes/PropHunt.cs` (red hunts for now), `PlayerState.Disguise`
and `PlayerState.RoleClass` (a part's loadout and weapon rules, read by `GiveStartingInventory`
and `PlayerClassUsesItem`), the `IMatchObjective.PlayerUse` / `PlayerUseHooks.ModeUse` hook,
`MatchModeHooks.Rearm`, and `pfwolf-pk3/actordefs/prophunt.yaml` (PropPlayer, PropHands).
`Puddle` and `TeamFlag` are `NODISGUISE`. NetProtocol 9, NetStateVersion 6. Checked: unit
tests and a headless DM02 run (props come in disguised, holding PropHands; a screenshot from
the hunter shows the prop as the green ceiling light). Not yet checked: Use to change disguise
in real play, a loopback network game, the lobby row, Spear/Blake decorations.

Found along the way: a prop disguised as a lamp gives off no light (actor lights come from map
actors, not bodies). Under shading that's a tell. Handle in phase 3.

- `GameMode.PropHunt` (appended, byte on the wire). Add it to `NetModes`, `IsVersus`,
  `IsTeamGame`, `Id` `prop-hunt`, `Parse` (`ph`, `prophunt`), `LabelKey` `$STR_MP_PROPHUNT`,
  `ScoreName` "Rounds", score limits (rounds to win: 0, 2, 3, 4, 5, 7). Default: monsters off,
  and items kept.
- `PlayerState.Disguise` (class name or null). `PlayerBodies.TickPlayerBody` builds the body
  from `Disguise ?? PlayerClass`, and keeps it on Spawn (no See/Missile/Pain/Death
  switching while disguised). On death, the body goes back to the player class so the
  death frames play.
- **Picking a disguise:** a prop presses Use while facing a decoration within reach and
  becomes it. What counts as a decoration: actordefs with a Spawn sprite that aren't
  Monster, Inventory or PlayerPawn and aren't flagged `NODISGUISE`. Puddles and other flat
  floor art get `NODISGUISE` in the base pk3 so props can't hide as them. Props start
  disguised as a random decoration on the level.
- Props carry no weapons (`player.weapons: []`, look-only, like the dog class's bite-only
  rule) and hold nothing in hand.
- Network: `NetPlayerState` + `NetJoin` carry `Disguise`. Bump `NETSTATEVERSION` and
  `NetProtocol`. The Use press is already in the `TicCmd`.
- Tests: `GameModeTests` (Parse/Id/flags), a disguise test that Use on a decoration sets
  `Disguise` and the body's sprite, and a headless `gamemode ph` run.

### Phase 2: rounds (done 2026-10-17)

As built: `PropHunt.Phase` Hide (30 s) → Hunt (3 min) → RoundOver (5 s), constants
`HideTics`/`HuntTics`/`RoundOverTics` for now (lobby rows in phase 4). Sides swap by
`HuntingTeam` (players keep their team and color). A new round sets each player's
`RoleClass` and then respawns everyone through `MatchModeHooks.Respawn` (`Players.Respawn`).
Hunters wait through `IMatchObjective.IsWaiting`: `PlayerThinkHooks.Waiting` drops their
`TicCmd` (`PlayerInput.Drop`), and `NetOverlay.DrawWaitingView` blacks out their view
under the automap, messages and match line. `MayRespawn` is always false, and the dead hint
says "back later". `TNT1` weapon frames now draw nothing instead of warning. Checked: unit
tests for the round flow, a time-out, the score limit and join state; headless DM02 run
through round 1 → round 2 (screenshot of the blackout). Not yet checked: a loopback network
game, a hunter's view after the hide in a real window, 3-4 players.

- `MatchModes/PropHunt.cs : MatchObjective`. Phases: **Hide** (hunters frozen and blacked
  out, props move) → **Hunt** (round timer) → **RoundOver** (a few seconds to show who won)
  → next round.
- Round start: teams swap, every player is respawned at once at a spot for their team
  (a forced respawn, not waiting for fire), props are given a random disguise, hunters get
  their weapons back.
- `MayRespawn` is false during a round. Dead props and dead hunters watch the living
  (decision 5).
- Round win: hunters when no prop is alive. Props when the timer runs out with any prop
  alive. `AddTeamScore(winner, 1)` and the score limit ends the match. With no limit, the
  match's time limit ends it.
- `StatusLine`: "Hiding 0:24" / "Props left 3   1:52". `FinalTitle`: "Red wins 4 rounds to 2".
- `Write`/`Read`: phase, phase end tic, which team is hunting, round number.
- Hunter blindness: the view is drawn black with a "Props are hiding" message, and the
  hunter's `TicCmd` movement and fire are ignored during Hide (done in the sim, so it stays
  in sync).
- Tests: headless round runs with `wait` + `addplayer`, round win both ways, team swap,
  mid-game join state round-trip.

### Phase 3: hunting rules and prop tells (done 2026-10-17)

As built: `IMatchObjective.IsDecoy` → `PlayerAttackHooks.IsDecoy`: during the hunt a hunter's
`ShootCandidatesFrom` also counts the level's disguise decorations, and hitting one costs the
hunter a quarter of the shot's damage (`PlayerAttacks.DecoyShare`, at least 1). Prop health:
`PropHunt.HealthAs` (50 + radius × 50 / 32, 25-100, or `prophunt.health`), kept as the same share
when the look changes (`PropHunt.Disguise`), and capped there each tic. `MatchModeHooks.ShowHealth`
redraws it. Snapping: `PlayerBodies.SettleTics` (1 s still → body drawn at the tile's middle;
`PlayerState.StillTics`, display only). Taunts: `A_Taunt(...)` on PropHands' Fire (one of
seven Wolf3D sounds, 2 s cooldown), played as the prop's own sound (positional for others).
Lights: `ActorLights.LitActors` includes players' bodies, so a lamp disguise lights up. Left
out: the forced taunt, last-prop pistol, projectiles hitting decoys. Checked: unit tests
(health shares, decoy rules) and a headless run (health 50/50 as a ceiling light). Not yet
checked by eye or ear: the penalty, the snap, taunts, lamp light under shading, Blake/Spear
taunt sounds (Wolf3D names).

- **Wrong-prop penalty** (decision 2): in Prop Hunt, `ShootCandidatesFrom` also counts
  decorations that could be disguises (same test as phase 1). When the nearest target is a
  real decoration, the hunter takes the damage instead, and nothing else is hit.
- **Prop health:** disguise health scales with the decoration's `radius` (small lamp less,
  table more), capped at 100. Optional property `prophunt.health` on the decoration overrides it.
- **Snapping** (decision 3): a prop that hasn't moved for 1 second is drawn at its tile's centre.
- **Taunts:** a prop's fire button plays a taunt sound everyone hears from the prop's
  spot (`sounds/` or digi sounds; list in game-info `prop-hunt: taunts:`). Short cooldown.
  An optional forced taunt every N seconds so hiding forever doesn't work.
- **Last prop:** when one prop is left, everyone is told, and that prop can be armed with a
  pistol (optional, off by default).
- Automap: other players are already left off (`AutomapDrawing` skips `PlayerPawn`).
  Check that nothing else shows them (names, the `match` command, the watching HUD).

### Play-test (2026-10-17)

Two windowed instances over loopback (host Red, client Blue), The Courtyard with the
moody-lights mod. Checked: lobby Prop Hunt row, hide blackout, a prop seen as a lit ceiling
light (light pool on wall and floor), settling to the tile's middle a second after a step,
the hunter losing 8 health for three shots at a real column, taunts playing with no missing
sounds (not heard), finding the prop ending round 2 for the hunters, time-outs winning rounds
1 and 3 for the props, sides swapping, the match ending at 3 rounds, 402/402 checksums.
Fixed after: rounds won now count for each player (the scoreboard's Rounds column was 0).
Also fixed after: a weapon with no `inventory.icon` (PropHands, DogBite) now shows the bar's
own picture where the last weapon's was, a prop's face goes by its share of its look's most
health (`IMatchObjective.MostHealth`, `StatusBarHooks.MostHealth`), and the automap's stats
start under the match line (`AutomapDrawing.TopReserved`).

### Phase 4: polish (done 2026-10-17)

As built: chase cam: `Camera.Chasing` (`WantsChase` = the `chasecam` setting in controls.cfg, on
by default, and `IMatchObjective.ChaseCam`: a living disguised prop) puts the view
`Camera.ChaseDistance` (1.5 tiles) behind and `ChaseRise` (16) above, stepped back short of
walls and shut doors (`IsOpenTile`); the body draws and the weapon doesn't. Lobby: `NetRules`
and `LobbySettings` carry `HideSeconds`/`HuntMinutes` (rules rows Hide Time 10-60 s, Hunt
Time 1-10 min, Prop Hunt only; protocol 10, net state 7); `PropHunt.HideTics/HuntTics` read
them. Map: PH01 "The Storerooms" (make-deathmatch-maps.ps1, 14 kinds of decoration;
game-info `modes: [prop-hunt, deathmatch, team-deathmatch]`); every DM arena still plays.
Editor: the actor schema's flags and properties descriptions name NODISGUISE and
`prophunt.health` (the editor has no per-property fields; NODISGUISE shows in flag
suggestions because the base pk3 uses it). Checked: unit tests (chase spot, lobby round-trip),
headless chase-cam and PH01 screenshots, the rules rows in a real lobby. Not done: game-info
`prop-hunt:` defaults, Spear/Blake decoration checks, varied `prophunt.health` on Wolf3D's
decorations (nearly all are solid, so nearly all give 100).

### Phase 4 plan, as written

- **Chase cam for props** (decision 1): a camera behind and above the prop, pushed in where
  walls are in the way, with the prop's own body drawn. This is the deferred chase cam from
  camera-spectate and would help the spectator view too.
- Lobby: round time and hide time rows when Prop Hunt is picked. Game-info `prop-hunt:` for
  defaults (hide seconds, round minutes, taunts, penalty).
- Maps: `MapInfo.Modes` gets `prop-hunt`. Any deathmatch map plays. A dedicated map with lots
  of decorations would help (make-deathmatch-maps.ps1).
- Editor: the `NODISGUISE` flag and `prophunt.health` in the actor field editor.
- Spear and Blake Stone decorations: check the disguise list on each game.

## Decisions for you

Recommendations are first in each list.

**Decided 2026-10-17:** 1a (first person, chase cam in phase 4), 2a (the hunter takes the
damage), 3a (free movement, snap when still), 4a (several rounds on one level). 5-8 are
still open. Until they're answered, the plan uses the recommended option for each.

1. **What props see.** (a) First person, with the HUD saying what they're disguised as, in
   phases 1-3, and a chase cam in phase 4. (b) Build the chase cam first, in phase 1.
2. **Shooting the wrong thing.** (a) The hunter takes the damage (classic Prop Hunt).
   (b) No penalty. (c) Ammo only: limited ammo per round.
3. **Prop movement.** (a) Move freely, and the body is drawn at the tile centre after standing
   still for 1 second. (b) Free movement with no snapping. (c) Lock in place with a button
   (can't move while locked).
4. **Rounds.** (a) Several rounds on one level, teams swapping each round. (b) One round per
   level, swapping teams on the next level.
5. **Death.** (a) Dead players watch the living until the round ends. (b) Dead props join the
   hunters for the rest of the round (infection style).
6. **Team sizes.** (a) Keep the lobby's red/blue teams and swap them. (b) One hunter per three
   props (picked each round), which suits 2-4 players better.
7. **Timings.** Defaults of 30 s to hide and 3 min to hunt, changeable in the lobby?
8. **Where it lives.** (a) Engine mode, available in any game like the other modes.
   (b) Only in multiplayer.pk3.
