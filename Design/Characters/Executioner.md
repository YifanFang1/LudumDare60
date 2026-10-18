# The Executioner (Trapper)

**Id:** 14 · **Class:** `Executioner : BaseFighter` in `Assets/Characters/Scripts/Fighters/Executioner.cs` · display name becomes "The Executioner" (D-40) · **Era:** TBD (medieval) · **Art style:** TBD (the tracker shows a hand-drawn line-art sketch)

> **Status: BUILT (2026-10-17)** in `Executioner.cs`, with its tests in `Assets/Tests/EditMode/RosterTests.cs`. The numbers here match the code on that date; from now on **the code is the source of truth**.
>
> **How to read this doc:** the **Sheet says** column is copied word for word from the tracker. Rules marked **(confirmed)** were answered by the team on 2026-10-17. **Frame numbers started as planner proposals** (the sheet gave none): tune in playtest; once in code, the code wins. "(std X)" = standard value. **CD** = per-move cooldown (D-43).

## From the Moveset Tracker
| Archetype | HP | Movement Speed | Light | Heavy | Grab | Forward Dash | Backward Dash | Block | Special | Ultimate | Perk 1 | Perk 2 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Trapper | 150 | 70 | Whip Attack/ small Range | Charge whip attack - longer range | Grab and pulls enemy into iron maiden form and does damage | Really slow and heavy | Really slow and heavy | Block- Standard | Places Traps on the ground, they inflict slow and bleed | Summons the Guillotine, if the enemy is in the path takes huge damage | Inflicts bleed damage/ which reduces enemies damage when fully inflicited | Percentage chance to inflict dismember status, slowing enemy |

## Game plan
A slow, tough torturer who wins by attrition. He lays traps where the opponent must walk, stacks bleed with his whip until they hit weakly, and slows them with dismember so they can't escape. He controls space with a charged long whip and a very short-range grab. He's weak to fast opponents who jump his traps and stay out of whip range, and his dashes are slow with a long cooldown.

## Stats
| HP | Walk speed | Body (hurtbox) |
|---|---|---|
| 150 (confirmed) | 42 (70% of 60, confirmed) | `Box(-500, 0, 1000, 2000)` (std) |

## Generic moves
| Move | Name / look | Sheet says | Startup | Active | Total | Dmg | Hitstun | KB | Box | CD | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Light (J) | Whip | Whip Attack / small Range | 5 | 3 | 17 | 5 | 12 | 40 | `Box(400, 900, 1000, 300)` | 5 | +1 bleed stack on hit (perk 1) |
| Heavy (K) | Charged whip | Charge whip attack - longer range | 12 after release | 4 | 32 + charge | 10 | 20 | 70 | `Box(400, 900, 1800 → 2800, 300)` | 10 | **Hold K to charge, release (confirmed, D-42).** The charge adds **reach** (1.8 → 2.8 units over 60 ticks), not damage (confirmed). +1 bleed stack |
| Grab (L) | Iron Maiden | Grab and pulls enemy into iron maiden form and does damage | 10 (std 5) | 3 | 48 (std 40) | 15 | 40 | 60 | `Box(300, 600, 400, 1000)` | 15 | **Very short range grab (reach about 0.7 units, almost touching; changed from 2.5); it pulls the victim in, and an iron maiden prop closes on them.** |
| Forward dash | Heavy step | Really slow and heavy | - | - | 22 | - | - | - | - | **60** | 22 ticks / 80 ≈ 1.8 units, slow. Dash cooldown 60, like Room (confirmed: "longer" for heavy characters) |
| Back dash | Heavy step back | Really slow and heavy | - | - | 24 | - | - | - | - | **60** | 24 ticks / 70 ≈ 1.7 units |
| Block | Block | Standard | - | - | - | 0 | 60% | 100% | - | - | Standard |

## Special (I): Bear Trap
"Places Traps on the ground, they inflict slow and bleed". **Confirmed:** dropped **at his feet**, **max 2** at once (a 3rd replaces the oldest), **each lasts until stepped on** (it triggers once), it's **visible**, and **jumping avoids it** (only a grounded opponent triggers it).

| Startup (trap appears) | Total | Trap box | On trigger |
|---|---|---|---|
| 15 | 30 | `Box(-400, 0, 800, 200)` at the spot | 3 dmg, 10 hitstun (confirmed), 0 KB, **slow** (40%, 180 ticks) + **1 bleed stack**. Can't be blocked (D-52) |

- The trap is a **placed hitbox** that lives until it triggers, so it doesn't follow him.
- He can't trigger his own traps.
- A trap only arms 20 ticks after it appears (confirmed), so dropping one on top of the opponent isn't a free hit.
- Standard Special cooldown (30).

## Ultimate (O): Guillotine. Old Tech: TBD
"Summons the Guillotine, if the enemy is in the path takes huge damage". **Confirmed:** after the 30-tick superflash, a **warning outline appears at the opponent's position**, and the **blade drops about 0.5 s later**. It's **unblockable**: dash or walk out of it. **40 damage.**

| Tick | What happens |
|---|---|
| 0 | `Telegraph` at the opponent's x (1200 wide, 30 ticks). The spot is locked in now and doesn't follow them |
| 30 | The blade: a placed hitbox `Box(-600, 0, 1200, 2400)` at that spot, 4 ticks, `unblockable` |
| 45 | The move ends. He stays in the pose until then, so he can't follow up on the blade |

| Startup | Active | Total | Dmg | Hitstun | KB |
|---|---|---|---|---|---|
| 30 (std 6) | 4 | 45 (std 45) | **40** (std 30) | 40 | 0 (they're pinned) |

- **Counterplay:** move out of the outline within 30 ticks: walk 0.6 units at 60 speed (10 ticks), or dash. Jumping doesn't help (the box is 2.4 units tall).
- **Damage:** 40, the roster's biggest flat Ultimate, because it's easy to avoid.

## Perks
1. **Bleed** (confirmed): "inflicts bleed damage, which reduces enemies' damage when fully inflicted". Each **whip hit (Light, Heavy), whip grab or trap** adds **1 stack, max 5** (the grab confirmed). Each stack does **1 damage per second** (it can't KO, D-23). At **5 stacks** the victim deals **-25% damage**. Stacks **fade 1 every 2 s** without a new stack. Hooks: the *Status effects* extension (on the victim, D-24), and `ModifyOutgoingHit` on the victim's own hits (the -25%).
2. **Dismember** (confirmed, changed from "percentage chance"): **every 3rd landed hit** dismembers, which **slows** the victim for 180 ticks. It's deterministic, not random (D-47). Counts all his landed hits, the Guillotine included (confirmed); blocked hits don't count. The count resets each round (a fresh fighter, D-19). Hook: `OnHitDealt`, and the shared *slow* status (one slow at a time, the longest wins, D-46).

## Implementation notes
- **Extra state (in `HashExtra()`):** `hitCounter` (for Dismember). Traps are placed hitboxes in `GameSim.hitboxes` (hashed there). Bleed stacks and slow live on the **victim** (hashed by `GameSim` as status fields).
- **Kit changes and extensions used:** per-move cooldowns (D-43), *Charge input* (Heavy), *Grab pull* (shared with Noir's lasso), *Placed hitboxes* + `CountHitboxes` (traps, Guillotine), a "lives until triggered" hitbox option (traps: lifetime 0 = forever, removed on hit) and a "ground only" flag (jumping avoids traps), *Telegraphs* (Guillotine), *Status effects*: bleed and slow, *Hit modifiers* (bleed's -25%).
- **Animator states beyond the generic ones:** `HeavyCharge` (holding K).
- **Projectile and trap visual keys:** `trap`, `guillotine`. The view also needs a telegraph outline (generic).

## View polish (optional)
- Blood-drop icons over the victim's health bar for bleed stacks (1-5), red at 5.
- A bandaged limb or limp effect while dismembered.
- An iron maiden prop that slams shut in the `Grab` clip.

## AI hints
- Drop traps between himself and the opponent, not under the opponent's feet.
- Use the Guillotine when the opponent is cornered or in hitstun when the superflash ends.
- Avoid dashing: the cooldown is long.

## Tests
- `ExecutionerTrapsMaxTwoOldestReplaced`.
- `ExecutionerTrapIgnoresAirborneOpponent`.
- `ExecutionerBleedStacksToFiveAndCutsDamage`: at 5 stacks the victim's Jab does `5 * 75 / 100` = 3.
- `ExecutionerBleedCannotKO`.
- `ExecutionerEveryThirdHitDismembers`.
- `ExecutionerGuillotineIsUnblockableAndLocksTheSpot`.
- `ExecutionerGrabPullsFromVeryCloseRange`.

## Open questions
**Answered (2026-10-17, all confirmed):** the charge adds reach, not damage; bleed stays as designed (up to ~30 damage over 10 s from 5 stacks); the whip grab adds a bleed stack; traps cause 10 hitstun; the Guillotine counts toward Dismember but adds no bleed; a trap arms after 20 ticks; the iron maiden is a prop.

Still open:
- **Old Tech, era, art style:** TBD (D-45). The idea pool's "torture museum" props (iron maiden, brazen bull) could fit.
- His **whip grab** is the same mechanic as Noir's **lasso**. Fine for the code; is it fine for the design?
