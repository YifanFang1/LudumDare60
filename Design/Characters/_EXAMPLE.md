# Caveman (Bruiser)

> **This is a worked example, not a real design.** It shows how to fill in [_TEMPLATE.md](_TEMPLATE.md). The Caveman is from the idea pool and isn't committed to the roster, and id 16 isn't reserved for it. Copy the blank template for a real character, not this file.
>
> Lines that start with *Tip:* explain why a section is filled the way it is, and **"(std X)"** means "the standard value is X". Leave both out of real designs.

**Id:** 16 (example only) · **Class:** `Caveman : BaseFighter` in `Assets/Characters/Scripts/Fighters/Caveman.cs` · **Era:** Stone Age · **Art style:** hand-drawn charcoal on paper, scanned and cut out as PNGs (one artist)

## How to read the numbers
*Tip:* this section is only in the example, so you know what each number means and where it ends up in the code. Standard values are what every character gets if you write "standard". They come from [BaseFighter.cs](../../Assets/Characters/Scripts/Fighters/BaseFighter.cs) and the [shared kit](../SHARED_KIT.md#the-standard-moveset).

**Units:** time is in **ticks**, and 60 ticks = 1 second. Distance is in **milli-units**: 1000 = 1 Unity unit, about half a character's height. The arena runs from -8000 to 8000, so it's 16 units wide.

| Number | Code field | Unit | What it means | Standard |
|---|---|---|---|---|
| HP | `MaxHp` | hit points | Health at the start of each round. | 100 |
| Walk speed | `WalkSpeed` | milli-units per tick | Distance moved each tick while walking. 60 = 3.6 units per second, so crossing the arena takes about 4.5 s. | 60 |
| Body (hurtbox) | `Hurtbox` | milli-units | Where the fighter can be hit, and how much room they take up when bodies push apart. A `Box(x, y, w, h)` (see "Box" below). | `Box(-500, 0, 1000, 2000)`: 1 unit wide and 2 units tall, centred on the feet |
| Startup | `spawnTick` | ticks | Ticks from the button press to the hitbox appearing. Lower is faster and harder to react to. | Light 4 · Heavy 10 · Grab 5 · Special 12 · Ultimate 6 |
| Active | `activeTicks` (melee) / `lifetime` (projectile) | ticks | How long the hitbox exists. For a projectile, how long it flies before it disappears. | Light 3 · Heavy 4 · Grab 2 · Special 150 · Ultimate 4 |
| Total | `totalTicks` | ticks | Length of the whole move. The fighter can't do anything else until it ends. **Recovery** = Total - Startup - Active: the helpless time after the hitbox is gone. | Light 15 · Heavy 28 · Grab 40 · Special 30 · Ultimate 45 |
| Dmg | `damage` | hit points | HP the victim loses on hit. A blocked hit does 0. Damage also builds meter: the attacker gets `damage × 7` and the victim `damage × 4`. | Light 5 · Heavy 12 · Grab 12 · Special 10 · Ultimate 30 |
| Hitstun | `hitstun` | ticks | How long the victim can't act after being hit. On block they get **blockstun** instead, 60% of this (`BlockstunPercent`). | Light 12 · Heavy 20 · Grab 40 · Special 18 · Ultimate 40 |
| KB (knockback) | `knockback` | milli-units per tick | Speed the victim slides away for the whole hitstun (or blockstun on block). Distance ≈ KB × hitstun: the standard Light pushes 40 × 12 = 480, about half a unit. | Light 40 · Heavy 80 · Grab 90 · Special 60 · Ultimate 120 |
| Box | `box` | milli-units | The hitbox, as `Box(x, y, w, h)`. **x** is how far in front of the feet its near edge starts (negative = behind), **y** is how high its bottom edge is above the floor, **w** is its width and **h** its height. It flips by itself when the fighter faces left. | Light `Box(400, 900, 900, 400)` · Heavy `Box(300, 700, 1300, 700)` · Grab `Box(300, 600, 700, 1000)` · Special `Box(400, 900, 500, 400)` · Ultimate `Box(300, 0, 2000, 2200)` |
| Projectile speed | `speed` | milli-units per tick | How fast a projectile flies. 100 = 6 units per second, so it crosses the arena in about 2.5 s. | Special 100 |
| Dash ticks / speed | `ForwardDash`, `BackDash` = `DashDef(ticks, speed)` | ticks / milli-units per tick | How long the dash lasts and how fast it goes. Distance ≈ ticks × speed. You can't block or attack during it. | Forward 14 / 130 (about 1.8 units) · Back 16 / 110 (about 1.8 units) |

**Frame advantage** (written like "-6 on block") isn't a field, but designers use it to talk about safety. It's the victim's stun minus the time the attacker still needs to finish the move after the hit. Negative means the attacker recovers later than the victim, so the victim can hit back with any move that has less startup than that number. For a hit on the first active tick: advantage = stun - (Total - Startup).

## Game plan
A slow, tough bruiser who wins by taking a hit to land a bigger one. His Heavy shrugs off one jab, so mashing against him doesn't work. His thrown rock makes opponents come to him. He's weak to players who stay patient at mid range and punish his slow recovery.

*Tip:* every move below should help this plan. If a move doesn't, cut it or change it.

## Stats
| HP | Walk speed | Body (hurtbox) |
|---|---|---|
| 110 (std 100) | 55 (std 60) | `Box(-550, 0, 1100, 1900)`: 1.1 wide, 1.9 tall (std `Box(-500, 0, 1000, 2000)`: 1.0 x 2.0) |

*Tip:* change stats only a little from standard. Here, +10 HP and a slower walk pay for the armor perk. A wider body is also easier to hit, which is a weakness too.

## Generic moves
Write "standard" for anything that uses the [shared kit](../SHARED_KIT.md#generic-moves-and-their-defaults) defaults.

| Move | Name / look | Startup | Active | Total | Dmg | Hitstun | KB | Box | Notes |
|---|---|---|---|---|---|---|---|---|---|
| Light (J) | Club poke | 5 (std 4) | 3 (std 3) | 17 (std 15) | 6 (std 5) | 12 (std 12) | 40 (std 40) | `Box(400, 800, 1000, 400)` (std `Box(400, 900, 900, 400)`) | Slower than standard, but hits harder and reaches 0.1 further |
| Heavy (K) | Overhead club smash | 13 (std 10) | 4 (std 4) | 32 (std 28) | 15 (std 12) | 22 (std 20) | 90 (std 80) | `Box(300, 600, 1400, 900)` (std `Box(300, 700, 1300, 700)`) | Armored during startup (perk 1). -6 on block, same as standard |
| Grab (L) | Headlock and shove | standard (5) | standard (2) | standard (40) | standard (12) | standard (40) | standard (90) | standard (`Box(300, 600, 700, 1000)`) | Landing it sets up the Fire Rock (perk 2) |
| Forward dash | Lumbering charge | - | - | 16 (std 14) | - | - | - | - | Speed 110 (std 130). 1.76 units, about as far as standard but slower |
| Back dash | standard | | | standard (16) | | | | | Speed standard (110) |
| Block | standard | | | | | | | | Blockstun 60% of hitstun, no damage |

*Tip:* writing the standard value next to yours shows the coders and the balance person exactly what changed. Startup + Active must be no more than Total. In code the moves stay named `Light`, `Heavy` and so on, because the Animator state names and the roster test depend on those names. "Name / look" is only for flavor and art.

## Special (I): Rock Throw
He throws a rock straight forward. It's a projectile:

| Startup | Lifetime | Total | Speed | Dmg | Hitstun | KB | Box | Visual key |
|---|---|---|---|---|---|---|---|---|
| 14 (std 12) | 120 (std 150) | 34 (std 30) | 70 (std 100) | 12 (std 10) | 18 (std 18) | 60 (std 60) | `Box(400, 900, 600, 500)` (std `Box(400, 900, 500, 400)`) | `special` (std) |

At speed 70 for 120 ticks the rock flies 8.4 units, about half the arena (the standard one crosses the whole arena).

Limit: one on screen at a time, using the default `CanUseMove` (`ProjectilesOnScreen(sim) == 0`). The rock is slow, so it controls space more than it deals damage. Opponents can walk up behind it.

No held-direction variant.

## Ultimate (O): Out of Bounds. Old Tech: clipping through the floor
After the 30-tick superflash, the Caveman sinks into the ground and comes up under the opponent with an uppercut:

| Tick | What happens |
|---|---|
| 0-19 | He sinks through the floor and is intangible (hits and pushboxes skip him) |
| 14 | A crack appears under the opponent (a `Telegraph` event, 800 wide, 10 ticks long) |
| 20 | He moves to the opponent's x (clamped to the arena) and is no longer intangible |
| 24 | The uppercut hitbox spawns: `Box(-400, 0, 800, 2400)` for 4 ticks (centred on him, so facing doesn't matter) |
| 55 | The move ends (27 ticks of recovery) |

| Startup | Active | Total | Dmg | Hitstun | KB |
|---|---|---|---|---|---|
| 24 (std 6) | 4 (std 4) | 55 (std 45) | 28 (std 30) | 40 (std 40) | 120 (std 120) |

- **Type:** a strike. It can be blocked but not grabbed (normal hitbox, not `unblockable`).
- **Counterplay:** hold back to block when the crack appears. The opponent has 10 ticks to react. On block he's at -7 (blockstun 24 minus the 31 ticks he still needs), so a Light (startup 4) punishes him.
- **Damage:** 28, a little below the standard Ultimate's 30, because it always reaches across the whole screen.

## Perks
1. **Thick Skull:** during ticks 0-12 of Heavy (its startup), the first hit he takes still does damage but doesn't put him in hitstun, so the smash still comes out. Only one hit is absorbed per Heavy. Hook: `ReactToHit` returns `Armor` (the *Hit modifiers* extension).
2. **Fire!:** landing a Grab lights his next Rock Throw. It does +4 damage (16 instead of 12) and uses the visual key `fire_rock`. The fire goes out when the rock is thrown, hit or not, or at the end of the round. Hook: `ModifyOutgoingHit` adds the damage to the hit instance, never to the `HitboxDef` (D-25).

*Tip:* a perk is one clear rule. Say which hook it uses. If the hook doesn't exist yet, it's an [extension](../SHARED_KIT.md#extensions-build-when-a-design-needs-them) that someone has to build first (see below). The standard kit has no perks.

## Implementation notes
- **Extra state (in `HashExtra()`):** `armorUsed` (bool, reset when a Heavy starts), `fireRockReady` (bool), and the fire rock's hitbox flag if it's tracked separately.
- **Kit hooks used:** overrides of `MaxHp`, `WalkSpeed`, `Hurtbox`, `LightAttack`, `HeavyAttack`, `SpecialMove`, `UltimateMove` and `ForwardDash`. `GrabMove`, `BackDash` and `CanUseMove` stay the default.
- **Extensions needed (none are built yet, and each one means a `Version` bump):**
  - *Move hooks* (`OnMoveTick`): the Ultimate's teleport on tick 20.
  - *Intangibility* (`IsIntangible`): the Ultimate's ticks 0-19.
  - *Hit modifiers* (`ReactToHit`, `ModifyOutgoingHit`): both perks.
  - *Telegraphs*: the crack on tick 14.
- **Animator states beyond the generic ones:** none. Light, Heavy, Grab, Special and Ultimate already have states. The sinking and rising are frames of the `Ultimate` clip. The clip is scrubbed over 55 ticks, so the uppercut frame belongs 24/55 of the way through it.
- **Projectile/trap visual keys:** `special` (rock) and `fire_rock` (burning rock). Both go in the Code Character Art's Projectile Sprites list.

## View polish (optional)
- Dirt particles where he sinks and where he comes up.
- Draw the crack telegraph as cracked-ground art, not as the default outline.
- A grunt sound on Heavy, and a "clunk" when Thick Skull absorbs a hit, so players can tell the armor worked.

## AI hints
- The generic AI throws the Special at any range. Rock Throw is slow, so it should only throw from far away.
- It should use Heavy against an opponent who keeps jabbing, because of the armor.
- It should Grab more often than usual, to set up the Fire Rock.

## Tests
One EditMode test per special rule, in `Assets/Tests/EditMode/CharacterTests.cs`:
- `CavemanHeavyArmorAbsorbsOneHitDuringStartup`: a Light during Heavy's startup does damage, but the Heavy still hits.
- `CavemanHeavyArmorOnlyAbsorbsOneHit`: a second hit in the same Heavy causes hitstun.
- `CavemanGrabLightsTheNextRock`: after a landed Grab, the next Rock Throw does 16 damage, and the one after does 12.
- `CavemanFireRockGoesOutAtRoundEnd`: a fire rock that's ready doesn't carry into the next round.
- `CavemanUltimateTeleportsUnderOpponent`: on tick 20 of the Ultimate his x equals the opponent's x (clamped at the walls).
- `CavemanIsIntangibleWhileUnderground`: a projectile passes through him during ticks 0-19 of the Ultimate.
- The determinism soak test picks him up automatically once he's in `CharacterList`.

## Open questions
- When he comes up on tick 20, the two bodies overlap and pushboxes shove them apart before the uppercut. Should the hitbox be wide enough to hit anyway (it is now, at 800), or should pushboxes be off until tick 24?
- Is 110 HP plus armor too strong? Try it against Brawler and Shooter before deciding.
- Should the Thick Skull armor also work against grabs? It doesn't for now: grabs ignore armor, which gives the opponent an answer.
