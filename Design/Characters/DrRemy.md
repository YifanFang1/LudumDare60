# Dr. Remy + Remy's Rat (Mixup)

**Id:** 15 · **Class:** `PlagueDoctor : BaseFighter` in `Assets/Characters/Scripts/Fighters/PlagueDoctor.cs` (class name kept, display name becomes "Dr. Remy", D-40) · **Era:** TBD (plague doctor) · **Art style:** TBD (the tracker shows a plague doctor in a long black coat with a cane)

> **Status: BUILT (2026-10-17)** in `PlagueDoctor.cs`, with its tests in `Assets/Tests/EditMode/RosterTests.cs`. The numbers here match the code on that date; from now on **the code is the source of truth**. Each potion's frames are counted from the end of the 15-tick hold, so each total is 15 + the number below.
>
> **How to read this doc:** the **Sheet says** column is copied word for word from the tracker. Rules marked **(confirmed)** were answered by the team on 2026-10-17. **Frame numbers started as planner proposals** (the sheet gave none): tune in playtest; once in code, the code wins. "(std X)" = standard value. **CD** = per-move cooldown (D-43).

**Names (confirmed):** the doctor is **Dr. Remy** (the in-game name), and his rat is **Remy**.

## From the Moveset Tracker
| Archetype | HP | Movement Speed | Light | Heavy | Grab | Forward Dash | Backward Dash | Block | Special | Ultimate | Perk 1 | Perk 2 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Mixup | 60 | 125 | Jab - hit with the stick medium range | Throws remy at the enemy | Grab: sticks leeches on the guy | Short Dash | Short Dash | Block - ? (flavored as spinning his cane to make a shield) | Potion Throw (press to switch, hold to throw) | "Unleash the Rats" (rats run across the screen and you have to jump to minimize damage) | potion options: poison cloud around the player (forces enemy to back up), self healing, slow potion | Places leeches on his enemy that do life steal |

## Game plan
A frail, quick doctor who wins by choosing the right potion for the moment. Poison keeps rushdown off him, slow stops runners, and heal plus leeches let him outlast the opponent. He pokes with a medium-range cane and throws his rat from mid range. He has the lowest HP in the roster (60), so one good combo wrecks him.

## Stats
| HP | Walk speed | Body (hurtbox) |
|---|---|---|
| 60 (confirmed, lowest) | 75 (125% of 60, confirmed) | `Box(-500, 0, 1000, 2000)` (std) |

## Generic moves
| Move | Name / look | Sheet says | Startup | Active | Total | Dmg | Hitstun | KB | Box | CD | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Light (J) | Cane jab | Jab - hit with the stick medium range | 5 | 3 | 16 | 5 | 12 | 40 | `Box(400, 900, 1200, 300)` (std w 900) | 5 | 0.3 more reach than a jab |
| Heavy (K) | Throw Remy | Throws remy at the enemy | 12 | lifetime 60 | 32 | 8 | 18 | 50 | `Box(300, 600, 500, 400)` | 10 | **A projectile; he can throw again after, with no wait for the rat to come back (confirmed).** Speed 90, so about 5.4 units. Visual key `rat` |
| Grab (L) | Leech grab | Grab: sticks leeches on the guy | 5 | 2 | 40 | 6 (std 12) | 40 | 90 | `Box(300, 600, 700, 1000)` | 15 | Standard range. Less damage, but it applies **leeches** (perk 2) |
| Forward dash | Short dash | Short Dash | - | - | 10 | - | - | - | - | 20 | 10 ticks / 120 = 1.2 units (std 1.8) |
| Back dash | Short dash | Short Dash | - | - | 10 | - | - | - | - | 20 | 10 ticks / 110 = 1.1 units |
| Block | Cane spin | ? (spinning his cane to make a shield) | - | - | - | 0 | 60% | 100% | - | - | **Standard; the cane spin is only the art (confirmed)** |

## Special (I): Potion Throw
"Press to switch, hold to throw". **Confirmed:** **tap I** cycles **Poison → Heal → Slow** (then back to Poison). **Hold I for 15 ticks** uses the selected potion. Starts each round on Poison (confirmed).

| Potion | What it does | Startup (after the hold) | Total (after the hold) | Numbers |
|---|---|---|---|---|
| **Poison** | A cloud **around Remy** that forces the enemy to back off by threat only, no push (confirmed) | 10 | 30 | A zone 3 units wide on Remy's spot for 180 ticks. 1 dmg every 20 ticks to an enemy inside (can't KO, D-23). One cloud at a time (D-53) |
| **Heal** | He **drinks** it (confirmed) | 20 | 40 | **+10 HP**, never above 60. **Its own 300-tick cooldown** (confirmed: stops heal stalling) |
| **Slow** | **Thrown** at the enemy (confirmed) | 12 | 30 | Projectile speed 90, lifetime 80, 2 dmg, 10 hitstun. On hit: the shared **slow** status for 180 ticks (D-46) |

- A tap (released before 15 ticks) only switches: no move plays and no cooldown starts. Holding to 15 starts the move.
- After **using** a potion, all three are on (at least) the 30-tick Special cooldown, so he can't chain them (D-53). If the selected potion is cooling down, a press of I still switches to the next one.
- Visual keys: `potion_slow` (projectile). The poison cloud is a zone (drawn by kind).

## Ultimate (O): Unleash the Rats. Old Tech: TBD
"Rats run across the screen and you have to jump to minimize damage". **Confirmed:** after the 30-tick superflash, **3 low waves of rats cross the whole screen, about 0.5 s apart, 10 damage each, unblockable**. **Each wave you jump is avoided**; mistiming lets some through, which is the "minimize".

| Wave | Spawns on tick | Box | Speed | Lifetime | Dmg | Hitstun | KB |
|---|---|---|---|---|---|---|---|
| 1 | 6 | `Box(0, 0, 800, 500)` from Remy | 160 | 110 (crosses 16 units+) | 10 | 12 | 0 |
| 2 | 36 | same | 160 | 110 | 10 | 12 | 0 |
| 3 | 66 | same | 160 | 110 | 10 | 12 | 0 |

- `unblockable`, low (500 tall). A standard jump (about 2 units high) clears it.
- **Hitstun is kept short (12) on purpose**, so a victim hit by wave 1 recovers in time to try jumping wave 2. Otherwise one hit would make all three guaranteed.
- Total: 80 ticks (he's helpless while the rats run, so a jump-in can punish him).
- **Damage:** 30 if all three hit (= the standard Ultimate), 0 if all are jumped.

## Perks
1. **Apothecary** (confirmed): the three-potion choice of his Special *is* his perk 1 (the sheet's Perk 1 cell lists the potions).
2. **Leeches** (confirmed): his Grab puts leeches on the victim. They **drain 1 HP per second for 300 ticks** (5 HP total) and **heal Remy by the same amount**, never above his max HP. They **can't KO** (D-23), and a new grab **refreshes** the timer. Hooks: `OnHitDealt` (grab landed) applies a *leech* status to the victim; the victim's status tick calls `TakeDamage(canKO: false)` and heals the owner.

## Implementation notes
- **Extra state (in `HashExtra()`):** `selectedPotion` (0-2). Leeches live on the victim as a status (hashed by `GameSim`). The poison cloud is a zone in `GameSim`.
- **Kit changes and extensions used:**
  - per-move cooldowns (D-43)
  - *Charge input* in its **tap-vs-hold** form (D-42): a release before 15 ticks fires a "tap" callback, and reaching 15 starts the move
  - *Status effects*: slow, leech (DoT that heals its owner), `TakeDamage(canKO: false)`, and a `Heal` method
  - *Ground zones* (poison cloud)
  - *Move hooks* (`OnMoveStart` drinks the heal, places the cloud)
- **D-14 (one Special) still holds:** it's one button with three modes, chosen beforehand.
- **Animator states beyond the generic ones:** the generic `Special` clip for all three, or optionally `SpecialPoison`, `SpecialHeal`, `SpecialSlow` (they'd need to go in `ListMoves()`).
- **Projectile visual keys:** `rat`, `potion_slow`, `rats` (Ultimate waves).

## View polish (optional)
- HUD: the selected potion's icon next to his health bar. This is the only way to see what a tap did, so it isn't really optional.
- A green poison cloud, a drinking animation, and leech icons on the victim.

## AI hints
- The generic AI doesn't know about potion cycling. It needs: poison when the opponent is close, slow at range, heal below 50% HP when safe.
- Grab more often (leeches).

## Tests
- `RemyTapCyclesPotionsHoldUsesThem`.
- `RemyHealCapsAtMaxHp`.
- `RemyPoisonCloudCannotKO`.
- `RemyLeechesDrainAndHealFor300Ticks`, and a second grab refreshes them.
- `RemyRatWavesAreUnblockableAndJumpable`.
- `RemyRatCanBeThrownAgainBeforeTheLastLands`.

## Open questions
**Answered (2026-10-17, all confirmed):** poison only threatens (no push); the rat throw keeps the standard Heavy cooldown (a rat every ~42 ticks, no on-screen limit); the potion resets to Poison each round; Paintress can reflect his slow potion; **heal stalling is stopped by a 300-tick heal cooldown** (the team's choice over a heal limit or a round timer).

Still open:
- **Old Tech, era, art style:** TBD (D-45).
- **"Mixup" archetype:** mixups usually mean high/low or left/right guessing, and the game has no lows or overheads (D-22). His kit plays more like a setup/sustain character.
