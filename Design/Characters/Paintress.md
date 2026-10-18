# Paintress (Rushdown)

**Id:** 11 · **Class:** `Painter : BaseFighter` in `Assets/Characters/Scripts/Fighters/Painter.cs` (class name kept, display name becomes "Paintress", D-40) · **Era:** TBD · **Art style:** TBD (the tracker shows a hand-drawn sketch)

> **Status: BUILT (2026-10-17)** in `Painter.cs`, with its tests in `Assets/Tests/EditMode/RosterTests.cs`. The numbers here match the code on that date; from now on **the code is the source of truth**.
>
> **How to read this doc:** the **Sheet says** column is copied word for word from the tracker. Rules marked **(confirmed)** were answered by the team on 2026-10-17. **Frame numbers started as planner proposals** (the sheet gave none): tune in playtest; once in code, the code wins. "(std X)" = standard value. **CD** = per-move cooldown (D-43).

## From the Moveset Tracker
| Archetype | HP | Movement Speed | Light | Heavy | Grab | Forward Dash | Backward Dash | Block | Special | Ultimate | Perk 1 | Perk 2 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Rushdown | 75 | 150 | 1 armed-jab | upward leg sweep | Short range grab | Really fast | backward moving dash that does damage to itself | Block - ? (flavored as using her arms to block damage) | - Some kind of reflect? Think Palutena in Smash but the painter draws some sort of reflective canvas | Covers the eyes of the opponent with paint, stunning them for 5 seconds | Dashing forward makes them immune to damage, but backwards dashing does big damage | Paint skating makes them fast on the ground |

## Game plan
A fragile speedster who never stops coming forward. She dashes through attacks (her forward dash is invincible), and every hit leaves paint on the floor that speeds her up and slows the opponent. A canvas reflects zoners' projectiles back at them. Her Ultimate scrambles the opponent's controls so she can open them up. She's weak if she's caught: only 75 HP, and running away costs her health (the back dash hurts her).

## Stats
| HP | Walk speed | Body (hurtbox) |
|---|---|---|
| 75 (confirmed) | 90 (150% of 60, confirmed) | `Box(-500, 0, 1000, 2000)` (std) |

## Generic moves
| Move | Name / look | Sheet says | Startup | Active | Total | Dmg | Hitstun | KB | Box | CD | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Light (J) | One-armed jab | 1 armed-jab | 4 | 3 | 14 (std 15) | 5 | 12 | 40 | `Box(400, 900, 900, 400)` | 5 | Standard, 1 tick faster to recover |
| Heavy (K) | Upward leg sweep | upward leg sweep | 9 | 4 | 26 | 11 | 20 | 70 | `Box(300, 300, 1100, 1800)` | 10 | A rising anti-air kick with a tall box that also catches jumpers, not a low (confirmed; no lows yet, D-22) |
| Grab (L) | Short grab | Short range grab | 4 (std 5) | 2 | 38 | 10 | 40 | 90 | `Box(300, 600, 500, 1000)` (std w 700) | 15 | Shorter reach, 1 tick faster |
| Forward dash | Paint rush | Really fast | - | - | 10 | - | - | - | - | 20 | 10 ticks / 200 = 2 units. **Invincible (perk 1, confirmed)**; bodies still bump, so she can't dash through the opponent (confirmed) |
| Back dash | Paint slide | backward moving dash that does damage to itself | - | - | 10 | 8 to herself | - | - | - | 20 | 10 ticks / 220 = 2.2 units. **Costs her 8 HP (confirmed: the damage is to herself)**, never her last one (confirmed) |
| Block | Arms up | ? (using her arms to block damage) | - | - | - | 0 | 60% | 100% | - | - | **Standard; arms are only the art (confirmed)** |

## Special (I): Reflective Canvas
"Some kind of reflect? Think Palutena in Smash but the painter draws some sort of reflective canvas". **Confirmed:** a canvas in front of her for **about 0.5 s**. Enemy projectiles that touch it **fly back as hers**. **Melee goes through it.**

| Startup | Canvas active | Total | Canvas box |
|---|---|---|---|
| 4 | ticks 4-34 (30 ticks) | 44 | `Box(300, 0, 600, 2200)` |

- A reflected projectile changes owner to her, reverses direction, and keeps its damage, hitstun and knockback (and its sprite, mirrored).
- Reflectable: Ruy's Hadoken, Noir's bullet and thrown gun, Remy's thrown rat and slow potion. **Not** reflectable: traps, the Guillotine, poison clouds, and any Ultimate projectile (confirmed, D-50).
- Standard Special cooldown (30).

## Ultimate (O): Paint Splat. Old Tech: TBD
"Covers the eyes of the opponent with paint, stunning them for 5 seconds". **Changed (confirmed):** no real stun (a 300-tick stun would be a guaranteed kill combo). Instead, **3 seconds of slight blindness (ink blots on their screen) and randomized controls** (confirmed).

| Startup | Lifetime | Total | Speed | Dmg | Hitstun | KB | Box | Visual key |
|---|---|---|---|---|---|---|---|---|
| 8 | 120 | 36 | 180 | 5 | 12 | 20 | `Box(400, 900, 500, 500)` | `paint_splat` |

- **Delivery (confirmed):** a **fast projectile that can't be blocked but can be jumped** (`unblockable`).
- **On hit, for 180 ticks (confirmed):**
  - **Scrambled controls:** a **seeded shuffle** rolled once when it lands, kept for the whole 3 s. **Left and right are swapped** (which also flips blocking), and **Light, Heavy, Grab and Special are shuffled among themselves**. Jump and Ultimate stay where they are. The shuffle comes from the match seed + the tick, so both machines get the same one (D-47).
  - **Slight blindness:** ink blots over the victim's view. This is **view only**: it doesn't change the sim, so it does nothing to the AI, and online only the victim's screen gets it.
- **Damage:** 5 (the effect is the reward).
- **Counterplay:** jump the splat. If it hits, play carefully for 3 s.

## Perks
1. **Fearless Rush** (confirmed): **forward dashes are invincible** (intangible: hits and projectiles pass through her). **Back dashes cost her 8 HP.** Hooks: *Intangibility* (`IsIntangible` while dashing forward), and `OnDashStart` (or `OnTick` on the dash's first tick) calls `TakeDamage(self, 8, canKO: false)`.
2. **Paint Skating** (confirmed, more detail than the sheet): **every hit she lands or gets blocked leaves a paint puddle** at the victim's feet, about **1.5 units wide**. **Max 3** (the oldest goes), each lasts **240 ticks**. On paint, **she's +15% faster** (walk and dash) and **the opponent is -15% slower**. The opponent's slow counts as a status, so Room ignores it, and it stacks with the other slow status, capped at -50% in all (confirmed). Hook: a *Ground zones* extension (a list of zones in `GameSim`: owner, x, width, ticksLeft, kind), and speed modifiers read in `WalkSpeed` / the dash getters.

## Implementation notes
- **Extra state (in `HashExtra()`):** none of her own. Paint puddles are zones in `GameSim` (hashed there). Scramble lives on the victim as a status (hashed by `GameSim`).
- **Kit changes and extensions used:**
  - per-move cooldowns (D-43)
  - *Intangibility* (forward dash)
  - `TakeDamage(canKO: false)` from the *Status effects* extension (back dash cost)
  - *Hitbox vs projectile* (the canvas reflects; Room's Vine Boom destroys, same pass)
  - *Status effects*: scramble (seeded)
  - a seeded roll from the sim (D-47)
  - *Ground zones* (paint), plus a speed modifier hook for both fighters (`SpeedPercent`)
- **Engine issue:** *Intangibility* as specified skips **pushboxes** too. Her invincible forward dash would then pass **through** the opponent: a free crossup tool. See open questions.
- **Animator states beyond the generic ones:** none needed. The canvas is part of the `Special` clip.
- **Projectile visual keys:** `paint_splat`. The canvas and puddles need view art (zones are drawn by a generic zone renderer, keyed by kind).

## View polish (optional)
- Colourful puddles. The opponent's ink-blot overlay is drawn only on the victim's screen.
- A brush-stroke trail on her forward dash.

## AI hints
- Use the canvas only when a projectile is close.
- Forward dash through projectiles instead of jumping them.
- Avoid back dashing at low HP. The generic AI back dashes at random, so the cost needs a check.
- **Scramble affects the AI too** (it's in the sim), so the AI plays badly for 3 s, as intended. The blindness does nothing to the AI.

## Tests
- `PaintressForwardDashIsInvincible`: a Hadoken passes through her.
- `PaintressBackDashCostsHpButCannotKO`.
- `PaintressCanvasReflectsProjectiles`: a Hadoken comes back and hits its thrower.
- `PaintressSplatScramblesControlsDeterministically`: same seed + tick = same shuffle, and it ends after 180 ticks.
- `PaintressSplatIsUnblockable`.
- `PaintressPuddlesMaxThreeAndChangeSpeed`.

## Open questions
**Answered (2026-10-17, all confirmed):** the leg sweep is a rising anti-air kick, not a low; her invincible dash still bumps into bodies; the back dash cost can't KO her; paint slow is a status Room ignores; paint slow stacks with the slow status, capped at -50%; the canvas can't reflect Ultimate projectiles.

Still open:
- **Old Tech, era, art style:** TBD (D-45).
- **Blindness online vs AI:** it's view-only, so it only affects a human opponent (and online, only their screen). The scramble affects everyone.
- Should the leg sweep become a low once crouch-blocking exists (roadmap)?
