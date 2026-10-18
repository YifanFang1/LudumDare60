# ArcadeGuy (Shoto)

**Id:** 10 · **Class:** `ArcadeGuy : BaseFighter` in `Assets/Characters/Scripts/Fighters/ArcadeGuy.cs` · **Era:** TBD · **Art style:** TBD

> **Status: placeholder, all values standard.** Every number below is the [standard moveset](../SHARED_KIT.md#the-standard-moveset) from `BaseFighter.cs`, which is what `ArcadeGuy.cs` plays right now (it overrides nothing). When the team designs him, change the numbers here first, then override only those in code. For what each number means, see the [worked example](_EXAMPLE.md#how-to-read-the-numbers).

## Game plan
For now, the standard all-rounder, which already suits a shoto. He controls the screen with the fireball (Special), stops opponents who walk in with a fast Light, and punishes mistakes with Heavy or Grab. He has no special tools, so he's weak to nothing in particular and strong at nothing in particular. The team will sharpen this when they design him.

## Stats
| HP | Walk speed | Body (hurtbox) |
|---|---|---|
| 100 (standard) | 60 (standard) | `Box(-500, 0, 1000, 2000)` (1.0 x 2.0, standard) |

## Generic moves
All standard (the [shared kit](../SHARED_KIT.md#generic-moves-and-their-defaults) defaults). The values are written out so the team can see what they'd be changing.

| Move | Name / look | Startup | Active | Total | Dmg | Hitstun | KB | Box | Notes |
|---|---|---|---|---|---|---|---|---|---|
| Light (J) | Jab | 4 | 3 | 15 | 5 | 12 | 40 | `Box(400, 900, 900, 400)` | Standard. Fast and safe. 8 ticks recovery, -4 on block |
| Heavy (K) | Kick | 10 | 4 | 28 | 12 | 20 | 80 | `Box(300, 700, 1300, 700)` | Standard. Long reach (1.6 units), 14 ticks recovery, -6 on block |
| Grab (L) | Grapple | 5 | 2 | 40 | 12 | 40 | 90 | `Box(300, 600, 700, 1000)` | Standard. Unblockable, can't catch someone in hitstun or blockstun. Hold back when it lands to throw them behind you. 33 ticks recovery on whiff, about +7 when it lands |
| Forward dash | Lunge | - | - | 14 | - | - | - | - | Standard: 14 ticks / speed 130 (about 1.8 units). Can't block or attack during it |
| Back dash | Jump | - | - | 16 | - | - | - | - | Standard: 16 ticks / speed 110 (about 1.8 units). It's a back dash along the ground: the sim has no jumping yet (`Buttons.Up` is reserved for it) |
| Block | standard | - | - | - | 0 | 60% of the hit's hitstun | 100% of the hit's KB | - | Standard. Hold away from the opponent. No damage, blockstun is 60% of the hitstun |

## Special (I): Fireball (standard)
The standard Special: a projectile flying straight forward.

| Startup | Lifetime | Total | Speed | Dmg | Hitstun | KB | Box | Visual key |
|---|---|---|---|---|---|---|---|---|
| 12 | 150 | 30 | 100 | 10 | 18 | 60 | `Box(400, 900, 500, 400)` | `special` |

At speed 100 it crosses the arena in about 2.5 s. Limit: **one on screen at a time** (the default `CanUseMove`, `ProjectilesOnScreen(sim) == 0`). No held-direction variant.

## Ultimate (O): standard Ultimate. Old Tech: TBD
The standard Ultimate: after the 30-tick superflash, a big strike in front of him.

| Startup | Active | Total | Dmg | Hitstun | KB | Box |
|---|---|---|---|---|---|---|
| 6 | 4 | 45 | 30 | 40 | 120 | `Box(300, 0, 2000, 2200)` |

- **Type:** a strike. It can be blocked (not `unblockable`, not a grab).
- **Meter:** needs a full bar (1000), spends all of it, and its hits give no meter to either player.
- **Counterplay:** block it. On block he's at -15 (blockstun 24 minus the 39 ticks he still needs), so almost anything punishes him.
- **Damage:** 30, the standard. Every roster character is the same for now.

## Perks
None (standard, D-12 and D-29).

## Implementation notes
- **Extra state (in `HashExtra()`):** none.
- **Kit hooks used:** none. `ArcadeGuy.cs` only sets `Id = 10` and inherits everything from `BaseFighter`.
- **Animator states beyond the generic ones:** none. The generic states are Idle, Walk, DashForward, DashBack, Hitstun, Blockstun, KO, Light, Heavy, Grab, Special and Ultimate.
- **Projectile/trap visual keys:** `special` (the fireball). Its sprite goes in the Projectile Sprites list of his Code Character Art asset.

## View polish (optional)
None yet.

## AI hints
None: the generic AI already uses the whole standard kit.

## Tests
None of his own. He's covered by `RosterCharactersAreRegisteredWithTheStandardMoveset` (`CharacterTests.cs`), `AIMatchesFinishAndUseTheWholeKit` (`AITests.cs`) and the determinism soak test.

## Open questions
- Era, art style and Old Tech: TBD by the team.
- "Jump" is the name for the back dash. Should he get a real jump once jumping is built (roadmap), or is the name only flavor?
