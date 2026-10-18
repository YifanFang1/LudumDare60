# Ruy (Shoto)

**Id:** 10 · **Class:** `Samurai : BaseFighter` in `Assets/Characters/Scripts/Fighters/Samurai.cs` (class name kept, display name becomes "Ruy", D-40) · **Era:** TBD · **Art style:** TBD (the tracker shows a Street Fighter-style sprite)

> **Status: BUILT (2026-10-17)** in `Samurai.cs`, with its tests in `Assets/Tests/EditMode/RosterTests.cs`. The numbers here match the code on that date; from now on **the code is the source of truth**.
>
> **How to read this doc:** the **Sheet says** column is copied word for word from the tracker. Rules marked **(confirmed)** were answered by the team on 2026-10-17. **Frame numbers started as planner proposals** (the sheet gave none): tune them in playtest. "(std X)" = the standard value is X ([shared kit](../SHARED_KIT.md#the-standard-moveset)). The **CD** column is the new per-move cooldown (D-43).

## From the Moveset Tracker
| Archetype | HP | Movement Speed | Light | Heavy | Grab | Forward Dash | Backward Dash | Block | Special | Ultimate | Perk 1 | Perk 2 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Shoto | 100 (base) | 100 (base) | Jab - Standard Range/Damage/Speed | Kick - Standard Range/Damage/Speed | Grapple - Standard Range/Damage/Speed | Lunge - Standard Range/Speed | Jump - Standard Range/Speed | Block - Standard | Hadoken (the blue ball being thrown) | Shoryuken | more damage taken = more powerful attacks | The legally distinct Hadoken does more damage the longer you charge it |

## Game plan
The all-rounder who gets stronger as the round goes badly. He controls the screen with a Hadoken he can charge for more damage, stops jumps with an invincible Shoryuken, and turns a losing round around with his rage perk. He's weak to players who stay out of fireball range and bait the Shoryuken, which is very punishable if it misses.

## Stats
| HP | Walk speed | Body (hurtbox) |
|---|---|---|
| 100 (std, confirmed) | 60 (std; 100% of base, confirmed) | `Box(-500, 0, 1000, 2000)` (std) |

## Generic moves
| Move | Name / look | Sheet says | Startup | Active | Total | Dmg | Hitstun | KB | Box | CD | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Light (J) | Jab | Standard | 4 | 3 | 15 | 5 | 12 | 40 | `Box(400, 900, 900, 400)` | 5 | All standard |
| Heavy (K) | Kick | Standard | 10 | 4 | 28 | 12 | 20 | 80 | `Box(300, 700, 1300, 700)` | 10 | All standard |
| Grab (L) | Grapple | Standard | 5 | 2 | 40 | 12 | 40 | 90 | `Box(300, 600, 700, 1000)` | 15 | All standard |
| Forward dash | Lunge | Standard | - | - | 14 | - | - | - | - | 20 | Standard 14 ticks / 130 |
| Back dash | "Jump" | Standard | - | - | 16 | - | - | - | - | 20 | Standard 16 ticks / 110. **A ground back dash; "Jump" is only its name (confirmed).** His real jump (W) is standard too |
| Block | Block | Standard | - | - | - | 0 | 60% | 100% | - | - | Standard |

## Special (I): Hadoken
A blue fireball flying straight forward. **Hold I to charge, release to throw** (confirmed, D-42). It fires by itself at full charge.

| Charge | Startup after release | Lifetime | Speed | Dmg | Hitstun | KB | Box | Visual key |
|---|---|---|---|---|---|---|---|---|
| Tap (0 ticks held) | 12 (std) | 150 (std) | 100 (std) | 10 (std) | 18 | 60 | `Box(400, 900, 500, 400)` (std) | `special` |
| Full (60 ticks held) | 12 | 150 | 100 | **20** | 18 | 60 | same | `special_charged` (optional) |

- **Charge (confirmed):** damage goes from 10 to 20 over 60 ticks held: `damage = 10 + 10 * held / 60`. Only damage scales, not speed or size. He can't move or block while charging (he's committed).
- **Limit:** one Hadoken on screen (the default `CanUseMove`), plus the standard Special cooldown of 30 ticks after the move ends.
- Total = charge time + 30 (std 30).

## Ultimate (O): Shoryuken. Old Tech: TBD
After the 30-tick superflash, a rising uppercut that's **invincible on startup** (confirmed).

| Tick | What happens |
|---|---|
| 0 | He jumps straight up (`vy = 230`, peaks about 2.4 units) with a little forward drift (`vx = facing * 20`) |
| 0-7 | Intangible: hits and projectiles pass through him |
| 2-13 | Uppercut hitbox `Box(100, 0, 900, 2400)`, following him up |
| ~42 | Lands. **The move must keep playing after landing** (see Implementation notes) |
| 52 | Move ends |

| Startup | Active | Total | Dmg | Hitstun | KB |
|---|---|---|---|---|---|
| 2 (std 6) | 12 (std 4) | 52 (std 45) | 30 (std 30) | 40 | 120 |

- **Type:** a strike. It's blockable (confirmed) and anti-air: the tall box catches jumpers.
- **Counterplay:** block it and punish while he falls (he's helpless for about 40 ticks), or stay out of range and bait it.
- **Damage:** 30, the standard. With full rage (perk 1) it does 45, the most in the roster. Watch this in playtest.

## Perks
1. **Rage** (confirmed): "more damage taken = more powerful attacks". All his hits (Light, Heavy, Grab, Hadoken, Shoryuken) get `+missingHp% / 2` damage: +25% at 50 HP, about +50% near 0 HP. Hook: `ModifyOutgoingHit` (*Hit modifiers* extension), which changes the hit instance, never the `HitboxDef` (D-25).
2. **Legally Distinct Hadoken** (confirmed): the Hadoken's charge (see Special). Hook: the *Charge input* extension (D-42).

## Implementation notes
- **Extra state (in `HashExtra()`):** none of his own. The charge time lives in the generic charge fields on `Fighter`, which `GameSim.Hash()` covers.
- **Kit changes and extensions used:** per-move cooldowns (D-43), *Charge input* (Hadoken), *Hit modifiers* (rage, charge damage), *Move hooks* (`OnMoveStart` sets `vy` for the Shoryuken), *Intangibility* (Shoryuken ticks 0-7).
- **Engine issue:** today `Fighter.Land()` **cancels any attack on landing** (it's built for air attacks). The Shoryuken starts on the ground and must keep its recovery after it lands. That needs a small generic flag, e.g. `MoveDef.keepOnLanding`.
- **Animator states beyond the generic ones:** `SpecialCharge` (holding I), optional. Rising and falling are frames of the `Ultimate` clip.
- **Projectile visual keys:** `special`, and optionally `special_charged` (a bigger or brighter ball for the art only; the hitbox stays the same).

## View polish (optional)
- The fireball glows brighter as the charge goes up.
- A rage aura (or a red tint) that gets stronger as his HP drops, so players can see the damage bonus.

## AI hints
- Charge the Hadoken only from full-screen distance.
- Use the Shoryuken when the opponent jumps in close, not at random.

## Tests
- `RuyHadokenDamageScalesWithCharge`: a tap does 10, holding 30 ticks does 15, and holding 60+ does 20 (it fires by itself at 60).
- `RuyRageAddsDamageWhenHurt`: at 50 HP his Jab does `5 * 125 / 100` = 6.
- `RuyShoryukenIsIntangibleOnStartup`: a projectile passes through him on ticks 0-7.
- `RuyShoryukenKeepsRecoveryAfterLanding`: he can't act until tick 52, even though he lands around 42.

## Open questions
**Answered (2026-10-17):** rage also boosts the Shoryuken (up to 45, confirmed). The charged Hadoken looks bigger in the view only; its hitbox stays the same (confirmed).

Still open:
- **Old Tech, era, art style:** TBD by the team (D-45).
- Two comeback systems stack here: taking damage already fills the meter (D-08), and rage adds damage on top. Playtest it.
- Nek's perk 2 and Room's perk 1 are also "more damage taken = ..." perks. Three of seven characters get stronger when hurt.
