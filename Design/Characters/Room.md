# Room (Grappler)

**Id:** 13 · **Class:** `Elephant : BaseFighter` in `Assets/Characters/Scripts/Fighters/Elephant.cs` (class name kept, display name becomes "Room", D-40) · **Era:** TBD · **Art style:** TBD (the tracker shows a grey low-detail elephant head)

> **Status: BUILT (2026-10-17)** in `Elephant.cs`, with its tests in `Assets/Tests/EditMode/RosterTests.cs`. The numbers here match the code on that date; from now on **the code is the source of truth**. His forward boxes reach further than first written, because his body is wider (D-54).
>
> **How to read this doc:** the **Sheet says** column is copied word for word from the tracker. Rules marked **(confirmed)** were answered by the team on 2026-10-17. **Frame numbers started as planner proposals** (the sheet gave none): tune in playtest; once in code, the code wins. "(std X)" = standard value. **CD** = per-move cooldown (D-43).

## From the Moveset Tracker
| Archetype | HP | Movement Speed | Light | Heavy | Grab | Forward Dash | Backward Dash | Block | Special | Ultimate | Perk 1 | Perk 2 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Grappler | 200 | 50 | trunk whip: high damage low range | trunk slam: slow windup with big medium attack | Trunk Grab: Locks them in place, can use vine boom | Really slow and heavy, longer cooldown | Really slow and heavy, longer cooldown | No Block | Vine Boom: destroys nearby projectiles and also does damage: mulipliciatively stacks with other vine booms in area | "Address Me" (summons low-health copies of the elephant that replicate their moveset) | Dash deals damage to the enemy: the more damage taken the further the elephant slides | immune to non-hitstun CC |

The elephant in the room: "Address Me" means "address the elephant in the room", and Vine Boom is the meme sound effect.

## Game plan
A huge, slow tank who **can't block** and wins by soaking damage. He walks in through projectiles, deleting them with Vine Boom. Up close, his Trunk Grab locks the opponent in place for a guaranteed Vine Boom. His Ultimate fills the screen with copies that boom along with him. He's weak to fast characters who hit and leave before his slow moves come out, and to anyone who baits his long dash cooldown.

## Stats
| HP | Walk speed | Body (hurtbox) |
|---|---|---|
| 200 (confirmed, 2x std) | 30 (50% of 60, confirmed) | `Box(-700, 0, 1400, 2300)`: 1.4 x 2.3 units (confirmed). Bigger is also easier to hit |

## Generic moves
| Move | Name / look | Sheet says | Startup | Active | Total | Dmg | Hitstun | KB | Box | CD | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Light (J) | Trunk whip | high damage low range | 6 (std 4) | 3 | 20 (std 15) | 9 (std 5) | 14 | 50 | `Box(500, 800, 800, 500)` | 5 | Just reaches a touching opponent past his wide body (D-54); almost double a jab's damage |
| Heavy (K) | Trunk slam | slow windup with big medium attack | 18 (std 10) | 4 | 40 (std 28) | 18 (std 12) | 24 | 90 | `Box(600, 0, 1300, 1200)` | 10 | Slow but big. Medium reach (1.9 units from his centre, D-54) |
| Grab (L) | Trunk Grab | Locks them in place, can use vine boom | 5 | 2 | 40 | see below | - | - | `Box(500, 600, 800, 1000)` (standard range past his body, confirmed; D-54) | 15 | **A held grab (confirmed):** see below |
| Forward dash | Heavy charge | Really slow and heavy, longer cooldown | - | - | 24 | 8 (perk 1) | 14 | 60 | `Box(600, 300, 700, 1400)` during the dash (D-54) | **60** | 24 ticks / 80 ≈ 1.9 units at full HP, up to 2x farther when hurt (perk 1). **It's a hit** |
| Back dash | Heavy step back | Really slow and heavy, longer cooldown | - | - | 24 | - | - | - | - | **60** | 24 ticks / 70 ≈ 1.7 units. No damage (confirmed) |
| Block | **None** | No Block | - | - | - | - | - | - | - | - | **He can't block, ever (confirmed).** Holding back just walks back |

**Trunk Grab (confirmed):** when it connects, the victim is **held 1.2 units in front of Room for 60 ticks** (`FighterStateId.Held`) and can't act. During the hold, the **only thing Room can do is Vine Boom (I), which is guaranteed to hit** the held victim. If he doesn't boom, the hold ends with a standard throw (12 damage, 40 hitstun, 90 knockback, back throw allowed). A booming release does Vine Boom's damage only, not the throw's too.

## Special (I): Vine Boom
"Destroys nearby projectiles and also does damage: multiplicatively stacks with other vine booms in area".

| Startup | Active | Total | Dmg | Hitstun | KB | Box | Visual key |
|---|---|---|---|---|---|---|---|
| 14 | 4 | 40 | 8 | 20 | 100 | `Box(-2200, 0, 4400, 2400)` (around him, both sides) | (melee, no projectile art) |

- **Destroys projectiles:** any enemy projectile touching the boom's box while it's active is removed. Traps and the Guillotine aren't projectiles and stay.
- **Stacking (confirmed):** it stacks with **his Ultimate copies' booms**. If N booms hit the victim on the same tick, the damage is multiplied: 2 booms = ×2, 3 = ×4, so `8 × 2^(N-1)`. **Capped at ×4** (confirmed), so the worst case is 32 damage.
- Standard Special cooldown (30).

## Ultimate (O): Address Me. Old Tech: TBD
After the 30-tick superflash, **4 copies of Room appear at fixed spots** across the arena (confirmed), **clearly marked as copies** (see-through or tinted). Spots: x = -6000, -2000, 2000, 6000, skipping any spot within 1 unit of a fighter.
- **They mimic his movement** (confirmed): each tick a copy walks when Room walks and starts his Light, Heavy or Vine Boom when he does, from its own spot, facing the opponent. Not his Grab or dashes (confirmed), and they don't jump (D-57). Their attacks hit for real, which is what makes Vine Boom stack.
- They take enemy projectiles for him (any hit destroys a copy, confirmed) and don't push the opponent (confirmed).
- **Each copy dies to 1 hit** and fades after **about 8 s (480 ticks)** (confirmed).
- **Type:** a summon. The Ultimate move itself is a 20-tick pose with no hitbox.
- **Counterplay:** jump over or hit the copies (any hit kills one), and stay out of overlapping boom areas.
- **Damage:** depends on stacking (see Vine Boom). Hits from copies give no meter, like any Ultimate hit (confirmed).

## Perks
1. **Stampede** (confirmed): "Dash deals damage to the enemy: the more damage taken the further the elephant slides". His **forward dash is a hit** (8 damage, box in the table). Its distance grows with **his own missing HP**: `ticks × (100 + missingHp%) / 100`, so up to 2x at low HP. The back dash does no damage. Hook: a dash hitbox (the *Dash hitboxes* extension) and an override of `ForwardDash` that reads `hp`.
2. **Unstoppable** (confirmed): "immune to non-hitstun CC". He ignores **status effects**: slow, dismember, scramble, Remy's slow potion, and Paintress's paint slow. He still takes hitstun and knockback normally, and damage over time (bleed, poison, leeches) still hurts him, but he ignores bleed's -25% (confirmed). Hook: `IgnoresStatus(kind)`.

## Implementation notes
- **Extra state (in `HashExtra()`):** the hold timer and held flag for the Trunk Grab. The copies live in `GameSim` (see below), which hashes them.
- **Kit changes and extensions used:**
  - `CanBlock` (a new virtual, default true; Room returns false).
  - Per-move cooldowns, with his longer dash cooldown (D-43).
  - *Held grab:* a new appended `FighterStateId.Held`. The victim is frozen in front of the grabber for N ticks, and the grabber's `ChooseMove` gets a window (only the Special allowed).
  - *Hitbox vs projectile:* a hitbox flag `destroysProjectiles`, checked in `ResolveHits`. Paintress's reflect uses the same pass.
  - *Hit modifiers:* boom stacking (count the booms hitting the victim on the same tick in pass 1 of `ResolveHits`).
  - *Dash hitboxes:* a dash can spawn a follow hitbox (perk 1).
  - *Status effects* + `IgnoresStatus` (perk 2).
  - *Summons:* the copies. This is **the most expensive feature in the roster** and the first thing to cut (see [ROSTER_PLAN](../ROSTER_PLAN.md)). Proposal: a `Summon` list in `GameSim` (owner, x, facing, hp, ticksLeft, move, moveTick). Their hits are placed (non-follow) hitboxes spawned at the copy's x. Hashed, plain lists, index order.
- **Animator states beyond the generic ones:** `Held` (the victim's pose, generic for everyone), `GrabHold` (Room holding someone). The copies reuse Room's prefab with a tint.
- **Projectile visual keys:** none.

## View polish (optional)
- The Vine Boom sound effect on every boom, louder when they stack.
- A "×2" / "×4" pop-up when booms stack.

## AI hints
- Never try to block (the AI's "hold back" is useless for him).
- Grab, then Vine Boom during the hold, every time.
- Walk forward through projectiles and boom when one is close.

## Tests
- `RoomCannotBlock`: holding back takes full damage.
- `RoomTrunkGrabHoldsThenVineBoomIsGuaranteed`.
- `RoomVineBoomDestroysProjectiles`: a Hadoken touching an active boom disappears.
- `RoomVineBoomsStackMultiplicatively`: 2 booms on the same tick do 16.
- `RoomCopiesMimicMovesAndDieInOneHit`.
- `RoomDashHitsAndGoesFurtherWhenHurt`.
- `RoomIgnoresSlow`.

## Open questions
**Answered (2026-10-17, all confirmed):** boom stacking capped at ×4; bigger body `Box(-700, 0, 1400, 2300)`; copies copy walking, Light, Heavy and Vine Boom (not Grab or dashes); copies take projectiles; copies don't push; damage over time still hurts him, but he ignores bleed's -25%; Trunk Grab has standard range; copies' hits give no meter.

Still open:
- **Old Tech, era, art style:** TBD (D-45).
- **No block + 200 HP:** meter comes from damage (D-08), and he takes about twice the damage per round of anyone else, so both players fill meter faster in his matches. Playtest it.
- The old CLAUDE.md archetype note "an arcade character with an elephant 'stand'" doesn't match this design (the elephant is his own fighter now).
