# K3N (Powerup)

**Id:** 16 (new, the first free roster id) · **Class:** `K3N : BaseFighter` in `Assets/Characters/Scripts/Fighters/K3N.cs` (not created yet) · **Era:** TBD (arcade-style character, confirmed) · **Art style:** TBD (the tracker shows a pixel-art sprite with purple hair and a cape)

> **Status: BUILT (2026-10-17)** in `K3N.cs`, with its tests in `Assets/Tests/EditMode/RosterTests.cs`. The numbers here match the code on that date; from now on **the code is the source of truth**. He is the new 7th roster character (id 16).
>
> **How to read this doc:** the **Sheet says** column is copied word for word from the tracker. Rules marked **(confirmed)** were answered by the team on 2026-10-17. **Frame numbers started as planner proposals** (the sheet gave none): tune in playtest; once in code, the code wins. "(std X)" = standard value. **CD** = per-move cooldown (D-43).

## From the Moveset Tracker
| Archetype | HP | Movement Speed | Light | Heavy | Grab | Forward Dash | Backward Dash | Block | Special | Ultimate | Perk 1 | Perk 2 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Powerup | 100 | 100 | Cape whip around | Charged Punch | Grab with hand | Quick leap back | Quick hop forward | Block - (flavored as throwing Zeep Zorp in front of the enemy to absorb the damage) | Rapid fire short range punches | Install | Install - will enhance all of his moves and spend his super meter | More damage taken = less attack recharge time |

**Zeep Zorp** is K3N's "pet" entity (confirmed). It's only seen in his block for now.

## Game plan
A brawler who saves up meter and then spends it for a burst of power. Outside Install he's an average close-range fighter with a charged punch and a hold-to-mash rapid punch. With Install on he hits 30% harder with no cooldowns, for as long as his meter lasts. He's weak while his meter is empty, and to opponents who run away and wait out his Install.

## Stats
| HP | Walk speed | Body (hurtbox) |
|---|---|---|
| 100 (confirmed) | 60 (100% of base, confirmed) | `Box(-500, 0, 1000, 2000)` (std) |

## Generic moves
| Move | Name / look | Sheet says | Startup | Active | Total | Dmg | Hitstun | KB | Box | CD | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Light (J) | Cape whip | Cape whip around | 5 (std 4) | 3 | 16 (std 15) | 5 | 12 | 40 | `Box(400, 900, 1200, 400)` (std w 900) | 5 | **Front only, a bit longer than a jab (confirmed).** 0.3 more reach, 1 tick slower |
| Heavy (K) | Charged Punch | Charged Punch | 10 after release | 4 | 28 + charge | 12 → 20 | 20 → 26 | 80 → 120 | `Box(300, 700, 1300, 700)` (std) | 10 | **Hold K to charge, release to punch (confirmed, D-42).** 60 ticks to full (confirmed) |
| Grab (L) | Grab with hand | Grab with hand | 5 | 2 | 40 | 12 | 40 | 90 | `Box(300, 600, 700, 1000)` | 15 | Standard |
| Forward dash | Quick hop forward | "Quick leap back" (swapped) | - | - | 10 | - | - | - | - | 20 | **The labels were swapped in the sheet (confirmed).** Stays on the ground. 10 ticks / 150 = 1.5 units (std 14/130) |
| Back dash | Quick leap back | "Quick hop forward" (swapped) | - | - | 12 | - | - | - | - | 20 | On the ground. 12 ticks / 130 ≈ 1.6 units (std 16/110) |
| Block | Zeep Zorp shield | Throws Zeep Zorp in front to absorb the damage | - | - | - | 0 | 60% | 100% | - | - | **Standard hold-back block; Zeep Zorp is only the art (confirmed)** |

## Special (I): Rapid Punches
"Rapid fire short range punches". **Hold I to keep punching** (confirmed), up to a cap.

| Startup | Hit every | Hits | Dmg per hit | Hitstun | KB | Box | Total |
|---|---|---|---|---|---|---|---|
| 6 | 5 ticks | 3 minimum (tap), up to 10 (held) | 2 | 10 | 3 (was 8, D-55) | `Box(300, 800, 900, 600)` | 6 + 5 × hits + 14 recovery |

- A tap gives 3 punches (6 damage). Holding gives up to 10 (20 damage). He stops when you let go or at 10 hits.
- **Hitstun must be longer than the gap between punches**, or the victim escapes mid-string. With hit stop (`HitStopBase` 5 + damage/3) the gap is about 10 ticks, so 10 hitstun keeps them locked. Check this in a test.
- Tiny knockback (3), so all ten punches stay in range: at 8 the victim was pushed out of reach by the 7th hit (D-55). On block he's pushed out quickly, so blocked strings end early.
- Standard Special cooldown (30) after the move. No projectile, so the default one-projectile limit doesn't matter.

## Ultimate (O): Install (via perk 1). Old Tech: TBD
K3N has **no normal Ultimate**. His perk 1, Install, turns the O button into a **meter-drain toggle** (confirmed):
- **Starts with any meter above 0** (no full bar needed) and **no superflash** (confirmed).
- While it's on, his meter **drains until it's empty, or until he presses O again** to turn it off and keep what's left (confirmed). Drain: 2 meter per tick (120 per second), so a full bar lasts about 8.3 s (confirmed).
- **While on (confirmed):** **+30% damage** on all his hits, and **no move cooldowns**.
- **No meter gain while it's on** (confirmed). Turning it on is **instant, with no pose** (confirmed), and allowed whenever he could act (not while stunned, held or KO'd, D-56); it also clears his running move cooldowns.
- It ends at once if he's KO'd or the round ends. The meter still carries over to the next round (D-18), but Install itself doesn't.

## Perks
1. **Install** (confirmed): see the Ultimate above. This **replaces** the standard full-bar Ultimate, so D-09/D-11 don't apply to him. Hooks: a fresh press of O toggles a `installOn` flag (in `ChooseMove`/`OnTick`, not a `MoveDef` with `ultimate`), `OnTick` drains the meter, `ModifyOutgoingHit` adds +30%, and the cooldown hook returns 0.
2. **Second Wind** (confirmed): "more damage taken = less attack recharge time". Recharge means the new per-move cooldowns (D-43). His cooldowns shrink by `missingHp% / 2` (up to about -50% near 0 HP). Hook: a `ModifyCooldown(move, ticks)` virtual on the cooldown system. It has no effect while Install is on, because cooldowns are 0 then.

## Implementation notes
- **New class + registration:** `K3N.cs`, `Id = 16`, added to `CharacterList` and to the soak test pool.
- **Extra state (in `HashExtra()`):** `installOn` (bool). The charge and hold state for the punch and Rapid Punches live in the generic charge fields (hashed by `GameSim`).
- **Kit changes and extensions used:** per-move cooldowns + `ModifyCooldown` (D-43), *Charge input* (Heavy charge, Special hold-to-repeat), *Hit modifiers* (+30% and the charge damage).
- **The Install button is not a `MoveDef`:** pressing O shouldn't play a move or start a superflash. `Fighter.ThinkGrounded` only starts a superflash for moves with the `ultimate` flag, so returning no move is enough. Should turning it on play a short pose (like a 10-tick move)? See open questions.
- **Animator states beyond the generic ones:** `HeavyCharge` (holding K), and an Install aura or overlay (view only, driven by `installOn`).
- **Projectile visual keys:** none.

## View polish (optional)
- An Install aura and a draining meter bar that pulses while it's on.
- Zeep Zorp pops in front of him in the `Blockstun` animation.

## AI hints
- Turn Install on when the meter is high and the player is close; turn it off when the player runs away.
- Hold I for the full string only when the first punch hits.

## Tests
- `K3NInstallStartsWithPartialMeterAndDrains`: at 300 meter, Install turns on and the meter falls by 2 a tick until it's 0, then turns off.
- `K3NInstallToggleOffKeepsMeter`: pressing O again stops the drain.
- `K3NInstallAddsDamageAndRemovesCooldowns`.
- `K3NRapidPunchesKeepVictimInHitstun`: a held string of 10 hits lands every hit on Sandbag.
- `K3NCooldownsShrinkWhenHurt`.

## Open questions
**Answered (2026-10-17):** no meter gain during Install; 2 meter per tick drain; instant, no pose; Charged Punch 12 -> 20 damage, hitstun 20 -> 26, knockback 80 -> 120 over 60 ticks (all confirmed).

Still open:
- **Old Tech, era, art style:** TBD (D-45). "Install" is a fighting-game term (Guilty Gear), not an Old Tech.
- **Name:** "K3N" is a temp name.
- He's an arcade character like Ruy, and both have a "more damage taken" perk. How will they look and play differently?
