# Noir (Zoner)

**Id:** 12 · **Class:** `Cowboy : BaseFighter` in `Assets/Characters/Scripts/Fighters/Cowboy.cs` (class name kept, display name becomes "Noir", D-40; was "Black and White Cowboy") · **Era:** TBD (Western) · **Art style:** TBD (the tracker shows a black-and-white cowboy with a plague-doctor-style mask)

> **Status: BUILT (2026-10-17)** in `Cowboy.cs`, with its tests in `Assets/Tests/EditMode/RosterTests.cs`. The numbers here match the code on that date; from now on **the code is the source of truth**.
>
> **How to read this doc:** the **Sheet says** column is copied word for word from the tracker. Rules marked **(confirmed)** were answered by the team on 2026-10-17. **Frame numbers started as planner proposals** (the sheet gave none): tune in playtest; once in code, the code wins. "(std X)" = standard value. **CD** = per-move cooldown (D-43).

## From the Moveset Tracker
| Archetype | HP | Movement Speed | Light | Heavy | Grab | Forward Dash | Backward Dash | Block | Special | Ultimate | Perk 1 | Perk 2 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Zoner | 80 | 75 | Fire: really long hitbox jabs for light damage (low stuns) | throw gun | cowboy lasso | hort (horse) | hort (horse) | Block - ? (flavored as spinning his lasso to make a shield) | Shoot the ranged bullet for big knockback | Rapid fire shooting | 6 bullets for the ranged attack | Deals more knockback the closer the enemy |

## Game plan
A fragile gunslinger who keeps opponents out. His long Light pokes from far away, his bullet knocks them back across the screen, and everything he does knocks harder up close. He escapes on horseback. He's weak once his 6 bullets for the round are gone, and to anyone who gets past his pokes: he has only 80 HP and walks slowly.

## Stats
| HP | Walk speed | Body (hurtbox) |
|---|---|---|
| 80 (confirmed) | 45 (75% of 60, confirmed) | `Box(-500, 0, 1000, 2000)` (std) |

## Generic moves
| Move | Name / look | Sheet says | Startup | Active | Total | Dmg | Hitstun | KB | Box | CD | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Light (J) | Fire | Really long hitbox jabs, light damage, low stuns | 6 | 2 | 20 | 3 | 8 | 30 | `Box(400, 1000, 4000, 200)` | 5 | **An instant long melee box, about 4 units, not a projectile (confirmed).** Doesn't use ammo. Minus even on hit up close (see open questions) |
| Heavy (K) | Throw gun | throw gun | 12 | lifetime 50 | 30 | 10 | 20 | 70 | `Box(300, 800, 500, 400)` | 10 | **Just a projectile with a gun sprite; he keeps his gun (confirmed).** Speed 70, so about 3.5 units of range. Visual key `gun` |
| Grab (L) | Cowboy lasso | cowboy lasso | 10 (std 5) | 3 | 48 | 12 | 40 | 90 | `Box(300, 600, 2200, 1000)` | 15 | **A mid-range lasso (about 2.5 units) that pulls the victim in (confirmed).** Same mechanic as the Executioner's whip grab (`HitboxDef.pullTo`) |
| Forward dash | Horse | hort (horse) | - | - | 20 | - | - | - | - | 20 | **Long, fast, no damage (confirmed).** 20 ticks / 180 = 3.6 units (about 2x std) |
| Back dash | Horse | hort (horse) | - | - | 22 | - | - | - | - | 20 | 22 ticks / 165 ≈ 3.6 units |
| Block | Lasso spin | ? (spinning his lasso to make a shield) | - | - | - | 0 | 60% | 100% | - | - | **Standard; the lasso spin is only the art (confirmed)** |

## Special (I): Bullet
"Shoot the ranged bullet for big knockback".

| Startup | Lifetime | Total | Speed | Dmg | Hitstun | KB | Box | Visual key |
|---|---|---|---|---|---|---|---|---|
| 10 | 80 | 26 | 200 (std 100) | 8 | 18 | 150 (std 60) | `Box(400, 1000, 300, 200)` | `bullet` |

- Crosses the whole arena in 80 ticks. The big knockback (150 × 18 ticks ≈ 2.7 units, before perk 2) is the point of the move.
- **Ammo: 6 bullets per round** (perk 1, confirmed). When they're gone, I does nothing for the rest of the round (see open questions about a "click").
- Limits: the standard Special cooldown (30) plus ammo. Is there also a one-bullet-on-screen rule? See open questions.

## Ultimate (O): Rapid Fire. Old Tech: TBD
After the 30-tick superflash he twirls his gun (a short pose, total 20 ticks, no hitbox). Then **for 5 seconds (300 ticks): unlimited ammo, and the Special cooldown is removed or greatly cut** (confirmed). The cooldown goes from 30 to 4 ticks, so he can shoot about every 30 ticks (the move's own 26 ticks + 4).
- When it ends, his ammo is what it was before (bullets fired during it don't count). No refill (confirmed: "Timed, 5 s").
- **Type:** a buff, not a strike. The opponent's counterplay is to block or jump the bullets, or to rush him (he can still be hit during it).
- **Damage:** none by itself. In 5 s he can fire about 10 bullets (80 damage if all hit, which they won't, because of the knockback). Compare with the standard Ultimate's 30.
- Bullets fired during the buff are normal Special hits, so they give meter. (Only hits spawned by the Ultimate move itself are `fromUltimate`.)

## Perks
1. **Six-Shooter** (confirmed): "6 bullets for the ranged attack". The Special has **6 bullets per round**, with no reload. The Light and the thrown gun don't use ammo. Hook: `CanUseMove` returns false for the Special when `ammo == 0` (unless the Ultimate buff is on). A new round builds a fresh fighter (D-19), so the ammo resets to 6.
2. **Point Blank** (confirmed): "deals more knockback the closer the enemy". **All his hits**: knockback ×1 at 4 or more units apart, rising linearly to ×2 when touching: `kb * (100 + 100 * (4000 - dist) / 4000) / 100` for `dist < 4000`, where `dist = |attacker.x - victim.x|`. Hook: `ModifyOutgoingHit` (*Hit modifiers*).

## Implementation notes
- **Extra state (in `HashExtra()`):** `ammo` (0-6), `rapidFireTicks` (the Ultimate buff timer).
- **Kit changes and extensions used:** per-move cooldowns + `ModifyCooldown` (the Ultimate cuts the Special's), *Hit modifiers* (perk 2), *Grab pull* (the lasso: a grab that moves the victim next to him before the throw, built like the back throw), *Move hooks* (`OnMoveStart` of the Ultimate starts the buff).
- **Animator states beyond the generic ones:** none needed. The horse is in the `DashForward`/`DashBack` clips.
- **Projectile visual keys:** `bullet`, `gun`. The Light is a melee box and needs no projectile art (a muzzle flash in the `Light` clip is enough).

## View polish (optional)
- HUD: 6 bullet pips under his health bar.
- An empty-gun "click" sound when I is pressed with no ammo.
- A sepia or black-and-white screen tint during Rapid Fire (it could become his Old Tech, see open questions).

## AI hints
- Count bullets: don't waste them at full screen against a blocking player.
- Use the Light to poke at mid range and the horse back dash to escape.

## Tests
- `NoirHasSixBulletsPerRound`: the 7th press of I spawns nothing; the next round has 6 again.
- `NoirRapidFireGivesUnlimitedAmmoFor300Ticks`: after the Ultimate he can shoot with 0 ammo, then 301 ticks later he can't.
- `NoirKnockbackDoublesAtPointBlank`.
- `NoirLassoPullsTheVictimIn`.

## Open questions
**Answered (2026-10-17, all confirmed):** the lasso is a ~2.5-unit grab that pulls the victim in; no one-bullet-on-screen limit (ammo and cooldown are enough); the thrown gun flies straight; Point Blank applies to every hit, the lasso throw included; an empty gun does nothing (the view can play a "click").

Still open:
- **Old Tech, era, art style:** TBD (D-45). The idea pool's "black-and-white western" may have been meant for him. What would it do in play?
- **Light balance:** 8 hitstun with 12 ticks of recovery means he's -4 even when the Light hits. Fine for a far poke, but a free punish up close. Playtest it.
- **80 HP + 45 walk + 6 bullets a round** against Room (200 HP, can't block, Vine Boom destroys projectiles): first playtest matchup.
