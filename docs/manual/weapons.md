---
title: Weapon states
order: 6
---

# Weapon states

A weapon is an actordefs class with `Weapon` as an ancestor (Wolf3D's are in pfwolf.pk3's
`actordefs/wolf3d/weapons.yaml`). Its `weapon.*` properties say what it costs and how good it is
(the [actor properties reference](../reference/actor-properties) lists them). Its states are the
weapon in hand: the engine plays them the way ZDoom plays a weapon, and draws the frame it's on
over the view. A weapon's states only ever run in the player's hand. The ones it has as a pickup
on the map (`Spawn`) are an ordinary actor's.

The state rules are an actor's ([Actor states](./actor-states) has them all). A frame's `action`
runs as the frame ends, a state without a
`next-state` loops back to its own first frame, and a frame with 0 tics ends the moment it's
entered. The frame is drawn from its sprite's view 0 (`PISGA0`). Sprite `TNT1` draws nothing.

## The states

| State | When it plays |
| --- | --- |
| `Ready` | The weapon is up and idle. It loops on [`A_WeaponReady`](../reference/actions#a-weaponready). |
| `Fire` | A fresh press of fire, from `Ready`. |
| `Hold` | Fire still held as the attack comes round ([`A_ReFire`](../reference/actions#a-refire)). |
| `ZoomFire` | In place of `Fire`, while zoomed in through a scope (`weapon.zoom`). |
| `Backstab` | In place of `Fire`, when an enemy in the knife's reach is unaware and has its back turned. |
| `Reload` | The magazine is empty, or reload is pressed while it isn't full (`weapon.magazine`). |
| `Deselect` | The weapon is being put away for another ([`A_Lower`](../reference/actions#a-lower)). |
| `Select` | The weapon is coming up after a change ([`A_Raise`](../reference/actions#a-raise)). |

Only `Ready` is needed. A state a weapon hasn't got is never played: without `ZoomFire` a zoomed
shot is `Fire`, without `Deselect` the weapon is gone the moment it's changed from.

## Ready

`Ready` is where everything starts. Its frame should be 1 tic long and loop, running
`A_WeaponReady` each time, as Wolf3D's pistol does:

```yaml
Pistol:
  states:
    Ready:
      - sprite: PISG
        frames: [A]
        tics-per-frame: 1
        action: A_WeaponReady
        next-state: Ready
```

A longer frame works, but the weapon answers that much more slowly. Each time it runs,
`A_WeaponReady` does the first of these that applies:

1. **Out of ammo**, it puts the best weapon that can still fire in hand (lowest
   `weapon.selectionorder`; the knife). The weapon picked stays picked: it's taken back up once
   there's ammo for it again.
2. **With a magazine** that's empty, or on a press of reload while it isn't full, it goes to
   `Reload`, if there's ammo to load.
3. **On a fresh press of fire** it goes to `ZoomFire`, `Backstab` or `Fire`, as above.

The change keys (a slot key, or next and previous weapon) are only looked at while the weapon in
hand is on `Ready`, not mid-attack or mid-reload.

## Fire and Hold

`Fire` plays the attack, and its action frames do the damage. [`A_GunAttack`](../reference/actions#a-gunattack)
is Wolf3D's hitscan shot, [`A_CustomPunch`](../reference/actions#a-custompunch) the knife,
[`A_FireMissile`](../reference/actions#a-firemissile) throws a projectile, and
[`A_FireBullet`](../reference/actions#a-firebullet) is a shot at whatever's in the sights. Each
takes one shot's ammo (`weapon.ammouse1` of `weapon.ammotype1`) and fires nothing without it.

Fire only starts on a fresh press. To keep firing while it's held down, end the attack with a
frame that runs `A_ReFire`. With fire still held and a round ready, that goes back to `Hold` (or
to `Fire`, for a weapon without `Hold`). Otherwise the attack carries on to its end and back to
`Ready`. Wolf3D's machine gun fires every 12 tics while fire is held: B leads in, and C and D loop.

```yaml
MachineGun:
  states:
    Fire:
      - { sprite: MCHG, frames: [B], tics-per-frame: 6, next-state: Hold }
    Hold:
      - { sprite: MCHG, frames: [C], tics-per-frame: 6, action: A_GunAttack }
      - { sprite: MCHG, frames: [D], tics-per-frame: 6, action: A_ReFire }
      - { sprite: MCHG, frames: [E], tics-per-frame: 6, next-state: Ready }
```

A weapon with `weapon.chargetics` (Blake Stone's auto charge pistol) only shoots when charged:
its attack frames still play, but the shot is skipped. Each shot takes that many tics to charge
again.

[`A_HoldCharge`](../reference/actions#a-holdcharge)`("State")` cooks a throw: while fire is held
it goes back to that state, its power growing, and once fire is let go the attack carries on.
`A_FireMissile` with `"power"` throws as hard as it was cooked.

If a better weapon is picked up mid-attack, it takes the attack over. It goes on from the same
frame of its own `Fire` (counting timed frames from the first), with the same tics left, as
vanilla Wolf3D's did. If the weapon in hand has `Deselect`, or the new one has `Select`, the
change plays those instead (see below).

## Reload

A weapon with `weapon.magazine` shoots from that item, not straight from its ammo. The magazine's
`inventory.maxamount` is how many rounds it holds. [`A_Reload`](../reference/actions#a-reload)
moves rounds from the weapon's ammo (`weapon.ammotype1`) into it, all that fit or as many as it's
told. A weapon picked up comes loaded: an empty magazine is filled from what it gives first.

```yaml
RifleMag:
  parent: Ammo
  properties:
    "inventory.maxamount": 8

Rifle:
  parent: WolfWeapon
  properties:
    "weapon.slot": 4
    "weapon.selectionorder": 1400
    "weapon.magazine": RifleMag
  states:
    # Ready and Fire as the pistol's, with RIFG frames
    Reload:
      - { sprite: RIFR, frames: [A, B], tics-per-frame: 8 }
      - { sprite: RIFR, frames: [C], tics-per-frame: 8, action: A_Reload, next-state: Ready }
```

Given a state, `A_Reload` goes there while the magazine still has room and there's ammo left.
This makes a shotgun that loads a shell at a time:

```yaml
    Reload:
      - { sprite: SHTR, frames: [A], tics-per-frame: 6, next-state: ReloadLoop }
    ReloadLoop:
      - { sprite: SHTR, frames: [B], tics-per-frame: 10, action: 'A_Reload(1, "ReloadLoop")' }
      - { sprite: SHTR, frames: [C], tics-per-frame: 6, next-state: Ready }
```

Any state whose name starts with `Reload` counts as reloading. While a weapon reloads, use still
works and weapons can't be changed. A fresh press of fire cuts the reload short and fires, once
there's a round in.

## Deselect and Select

Changing weapons is instant, as in Wolf3D, unless the weapons say otherwise. One with a `Deselect`
state plays it first, going down off the view. Then the weapon changed to comes in on its `Select`
state, if it has one, coming up into view and on to `Ready`. Each weapon decides its own half: a
weapon with only `Select` comes up after one that simply vanished.

The two states run `A_Lower` and `A_Raise` on a 1-tic frame that loops. Each takes a speed: how
far it moves the weapon a tic, out of 96 for the whole way. Left out, it's 6, which is 16 tics
each way.

```yaml
Pistol:
  states:
    Deselect:
      - { sprite: PISG, frames: [A], tics-per-frame: 1, action: A_Lower, next-state: Deselect }
    Select:
      - { sprite: PISG, frames: [A], tics-per-frame: 1, action: A_Raise, next-state: Select }
```

- **While a weapon goes down or comes up**, fire does nothing. Another change waits until the
  weapon coming up is all the way up.
- **Changed back while it's going down**, a weapon goes on down out of view, then comes back up
  on its `Select`.
- **Out of ammo mid-change** works the same way: the weapon goes down before the fallback comes
  up.
- **A first weapon** (a new level, a loaded game) is simply up, with no `Select`.
- **A `Deselect` that never runs `A_Lower`**, or a `Select` that never runs `A_Raise`, would hold
  the weapon there for good. It's left out instead, so the change happens at once, with a warning.

A weapon built on another (`parent: Pistol`) inherits these states, sprites and all. Give it its
own if its sprites differ, or it goes down as its parent.

The [Holster Demo](../../examples/mods/holster-demo) gives Wolf3D's weapons both states: the knife
quick at 12 a tic, the gatling gun heavy at 4.

## Bobbing

While the player has **Weapon Bob** on in Game Options, the weapon in hand bobs as they walk.
`weapon.bobstyle` says how, as ZDoom's `Weapon.BobStyle` does:

| Style | How it moves |
| --- | --- |
| `spring` | Planet Strike's: up and down only, while walking forwards or back |
| `normal` | Doom's: side to side in a round U, lowest in the middle of the swing |
| `inverse` | A round arch, lowest at the sides |
| `alpha` | A pointed arch, in straight lines |
| `inversealpha` | A V, in straight lines |
| `smooth` | A gentler U |
| `inversesmooth` | A gentler arch |
| `none` | Still |

Without `weapon.bobstyle`, the player's **Bob Side to Side** setting picks: `normal` when it's
on, `spring` when it's off. `weapon.bobrangex`, `weapon.bobrangey` and `weapon.bobspeed` scale
how far across, how far down and how fast it goes; 1 is the usual, a swing 16 of Doom's 168 view
lines each way, once round in 128 tics. A swing is as wide as the player is fast (full at a
walk), and holds still while the weapon fires.

```yaml
GatlingGun:
  properties:
    "weapon.bobstyle": inversesmooth
    "weapon.bobrangex": 0.5
    "weapon.bobrangey": 1.5
    "weapon.bobspeed": 0.75
```

The [Bob Demo](../../examples/mods/bob-demo) gives each of Wolf3D's weapons a different style.

## See also

- [Actions](../reference/actions) lists every weapon action and its arguments.
- [Actor properties](../reference/actor-properties) lists the `weapon.*` properties.
- [Player classes](./player-classes) covers `weapon.bodysprite`, how other players see the weapon
  in hand.
