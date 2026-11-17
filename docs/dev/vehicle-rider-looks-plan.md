# Vehicle riders by class (and 8-direction vehicles) — plan

Goal (Vehicle Arenas, the MP vehicle pack outside the repo): someone riding a jeep or tank shows
**who** is riding — B.J., a Guard, an Officer, an SS, a Mutant or a Dog (multiplayer.pk3's
"Play as" classes) — by their head in the jeep's driver seat or the tank's hatch. Front view
(A0) first; 8 directions later, once there is art for them.

## Can it be done without engine work? No

How a rider is drawn today (`PFWolf/Gameplay/PlayerBodies.cs`):

- While riding, the player's body **becomes a new actor of the vehicle's class**
  (`looks = p.Vehicle`), run through its Ride / RideSee / RideMissile / RidePain states. That
  actor has no link back to its rider, so nothing in its states can ask "who's in me?".
- The weapon trick (`weapon.bodysprite`) only applies when the body's Spawn sprite is Player's
  own (`BodyHoldsWeapon`). A vehicle's never is, so it is switched off for riders. It is also
  keyed on the **weapon**, not the class: everyone in the tank holds `ArmyTankCannon`, and in the
  jeep every class except the Dog holds the same guns.
- `A_Board` boards the used thing's own class (`thing.Name`); there is no action that picks a
  class per user, so per-class vehicle subclasses (`ArmyJeepGuard`) can't be reached from one
  map thing.
- Putting the look on the classes instead (`GuardPlayer` saying "in a jeep I look like this")
  can't be done from the vehicle mod either: a mod's actordefs entry **replaces the whole
  class**, so Vehicle Arenas would have to copy multiplayer.pk3's classes.
- Lua can read `actor.vehicle` but has no way to change what a body draws.

The engine change is small, though: one property and one branch in `DrawnSprite`.

## Phase 1 — engine: `vehicle.ridersprites` (done 2026-11-16)

Built as below: `PlayerBodies.RiderSprite`/`RiderShows`, `InventoryManager.ParentOf`, the
property in `Definitions/ActorProperties.cs`, 6 tests in `PlayerBodiesTests`, documented in
docs/manual/player-classes.md and the 0.9.3 release notes. Not yet seen in the game: it needs
phase 2's art.

A vehicle property mapping rider classes to sprites, read like `player.maxamount`'s map:

```yaml
ArmyJeep:
  properties:
    vehicle.ridersprites:
      Player: VJBJ
      GuardPlayer: VJGD
      OfficerPlayer: VJOF
      SSPlayer: VJSS
      MutantPlayer: VJMU
      DogPlayer: VJDG
```

Rule, the same as `weapon.bodysprite`'s so mod authors learn one idea:

- `PlayerBodies.DrawnSprite`: when the body's owner is riding (`p.Vehicle != null`, body is the
  vehicle), find the rider's class in the map — walking the class's parents, so a mod's
  `EliteGuardPlayer: parent GuardPlayer` gets the Guard's look — and draw that sprite in place of
  the **vehicle's Spawn sprite**, frame by frame, only where the swap has that frame's picture
  (VJGD with only B0–H0 is fine; a missing frame shows the plain vehicle).
- Only the rider's body is swapped; a parked vehicle and wrecks never are.
- View rule (matters for phase 3): if the vehicle's own sprite has 8 views for a frame and the
  swap has only the single (A0-style) picture, the swap is used **only for the front view**;
  the other views show the plain vehicle rather than a front picture from behind.
- Cache the per-(vehicle, class) lookup; DrawnSprite runs for every sprite drawn.

Nothing new to sync or save: the rider's class and vehicle are already in the net state
(NetPlayerState, mid-game join) and the look is derived each frame. Chase-cam (`Camera.Chasing`)
shows your own head for free.

Tests (`PlayerBodiesTests`): mapped class → swap; parent class inherits; unmapped class →
vehicle's own sprite; frame missing in the swap → falls back; parked vehicle untouched;
directional vehicle + single-view swap → swap on the front view only.

Docs: the vehicles section of the manual + actordefs property reference; the actor editor's
property list if it has one for vehicles.

## Phase 2 — art: riders on the jeep, tank and boat (A0, in the mod) (done 2026-11-16)

Built in the pack's `tools/riders.ps1` (called from make-vehicle-arenas.ps1, new `-Wolf3D`
parameter for the base game's VSWAP.WL6), and the three vehicles' `vehicle.ridersprites` in
`actordefs/vehicle-arenas.yaml`. As it turned out, no hand touch-up was needed: the class
heads are the same size as the jeep driver's (about 9 px wide), so they're pasted 1:1. The
tunic color is the torso's commonest saturated color if colors cover a third of it (else its
greys, for the officer), plus up to 3 more of that hue. Seen in the game headless: Guard, Dog
and Mutant in jeeps, SS in the tank, Officer in the boat.

In `tools/make-vehicle-arenas.ps1`, generated like the rest of the pack's art. Six classes each
(B.J., Guard, Officer, SS, Mutant, Dog):

| Vehicle | Frames per class | Sprites | What's pasted, and where |
|---|---|---|---|
| Jeep (`VAJP`) | B (Ride), C D E (RideSee), F G H (RideMissile) — 7 | `VJBJ VJGD VJOF VJSS VJMU VJDG` | head over the driver's head (top right of the windshield), tunic recolored |
| Tank (`VATK`) | A B C D (Ride/RideSee), E (RideMissile) — 5 | `VTBJ VTGD VTOF VTSS VTMU VTDG` | head in the turret hatch (no crew in the art today) |
| Boat (`VABT`) | A B C D (Ride and RideSee both rock through them) — 4 | `VBBJ VBGD VBOF VBSS VBMU VBDG` | head **and shoulders** sitting in the dinghy (the art is an empty boat), bobbing with the hull's offset in each frame |

That's 96 pictures (16 a class), all generated.

- Heads (and the boat's shoulders) cut from each class's own front standing sprite (B.J. from
  PLMP, Guard, Officer, SS, Mutant, Dog), with a per-frame anchor table of x/y offsets in the
  script (the jeep's driver leans out in F–H; the boat's hull rises and falls A–D).
- **Main risk: scale.** The jeep's driver is drawn at vehicle scale; his head is about half the
  height of a Wolf3D soldier's head. A nearest-neighbour shrink of an enemy head will look rough;
  expect to touch each class's head up once by hand (editor's pixel editor) and keep those as
  source pictures, then let the script paste them into every frame.
- Jeep tunic: the driver's green recolored per class by a palette-index remap (Guard brown,
  Officer and SS blue-grey/blue, Mutant pale green-grey, B.J.'s own colors). For the Dog the
  driver's body is replaced by the dog's head, neck and front shoulders, recolored brown.
- The Dog gets the same treatment as the others: its head in the jeep's driver seat, in the
  tank's hatch and in the boat.
- Gun nest: unchanged. It is a mount, not a vehicle, and its gunner is already drawn as their
  own class's body.

With phase 1 not yet built, the new sprites are harmless: nothing refers to them.

## Phase 3 — 8 directions (engine and script done 2026-11-16; art waits on the user)

The user draws the 7 other views (decision 5); everything else is ready for them:

- **Parked things turn.** A thing that stands still was always drawn with `GetShapeName(NoDir)`,
  its front, so a parked jeep with `VAJPA1`–`A8` still showed `A0` from every side.
  `SpriteDrawing.DrawScaleds` now gives a static thing with views and a `Dir` the view
  `ActorShapeName` picks (CalcRotate by `Dir`, its `ViewX` from `TransformTile`). Seen headless
  with numbered stand-in views: VA01's jeep (angles 0, east) shows 1 from the east, 7 from the
  south, 5 from the west and 3 from the north.
- **Facing kept.** Getting out and wrecking put the vehicle (or wreck) down facing the nearest of
  the eight ways to the rider's angle (`Vehicles.ParkedAngles`, through `hooks.Respawn`'s new
  angles). A deathmatch respawn faces its map spot's angles (`MapManager.ThingSpots` returns
  them). Getting into a vehicle drawn from its sides (`hooks.DrawnTurning`) turns the rider to
  its `Dir`; a front-only one keeps the old 4-way snap to the rider's own facing. Seen headless:
  boarding VA01's jeep from the south turned the player from 90 to 0.
- **Riders on any view.** `RiderShows` lets any view of the rider's sprite show. A rider's
  picture is the whole vehicle, so its own views are right even where the plain vehicle lacks
  that view. Its single picture (B0) shows from the front, or where the vehicle falls back to its
  own single picture, never as a front seen from behind. Seen headless: B.J. at the wheel from
  the front, the plain view 7 from the side.
- **The pack's script.** `art\sprites\*.png` (with a README of names, views and frames) are
  copied into the mod before the riders are made. `riders.ps1` then puts each class into every
  drawn view of a riding frame, with the class's head from the same side: VSWAP sprite + view - 1
  (B.J.'s `PLMPA1`–`A8`), the head's rows taken from the front, its middle column found per view.
  Where it goes is in `tools\rider-places.psd1`: per view (`'3'`) or frame and view (`'C3'`):
  `Chin`, `Body` rows, `Behind` (a row, or `'rim'` for the boat), `Erase` (the jeep's own
  driver, from the parked frame A of that view), `Tunic`. A drawn view with no place gets no
  rider, and a warning. Tried on stand-in views (fronts copied as views 2, 3, 5): 96 pictures,
  side and back heads right; the front riders came out byte-identical to phase 2's.
- Tests: `VehiclesTests` (facing on boarding, getting out, wrecks, ParkedAngles),
  `DeathmatchTests` (respawn faces the spot), `PlayerBodiesTests` (a rider drawn for some views,
  a single picture where the vehicle shows its own single picture).

The pack's mapdefs also have the vehicles facing the other ways (2026-11-16): 980-982 the jeep,
983-985 the tank, 986-988 the boat, each north, west, south (970, 971, 973 face east). Seen
headless with a scratch VA01 whose jeeps were 980: the front from the north, 7 from the east, 5
from the south, and boarding from the east turned the player north. No map uses them yet.

Left: the art itself; then a `rider-places.psd1` entry per drawn view (check each with a contact
sheet); the maps using the new facings where they want them; and the manual-testing entry's
8-direction items.

## Decisions (2026-11-16, the user's)

1. B.J. gets his own pasted head, like every other class (the jeep art's green driver isn't him).
2. Head **and** tunic: the jeep driver's tunic is recolored per class.
3. Dog: its head in the seat / hatch / boat, the same as the others (no dog-only pose).
4. Jeep, tank **and boat**; not the gun nest.
5. Phase 3's 7 other views: the user supplies the art (drawn or found), rather than models
   rendered by the script.
