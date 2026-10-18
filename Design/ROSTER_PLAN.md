# Roster build plan (from the Moveset Tracker)

> **Status (2026-10-17): every step below is built, nothing was cut.** Step 0, Step 1 (K8-K11), Passes 1-3 for all seven characters, and Pass 4's code (AI, HUD status line, code-drawn placeholder art, zones, copies, telegraphs, ink overlay, sound slots). Network `Version` is "10". Decisions made while building are D-50 to D-59 in [DECISIONS.md](DECISIONS.md). Tests: `KitExtensionTests` (one per new rule), `RosterTests` (one per character rule), plus `AITests` and the soak test with all ten code characters: 170 EditMode tests, all passing in Unity. The AI also toggles Nek's Install and picks Dr. Remy's potions on purpose (D-60). **Still to do:** the **online match between two builds** (the team runs it, D-61: start the relay with `npm run dev` in `relay/`, point both clients' relay address at `ws://localhost:8787`, play a full best-of-3 and watch for desync warnings), real art (Unity Editor, see Pass 4), and a playtest pass on the numbers. The plan below is kept as written.

How to turn the seven designs in [Characters/](Characters/) into the game. The decisions behind it are D-40 to D-49 in [DECISIONS.md](DECISIONS.md). The earlier [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) (the shared kit and the placeholders) is done, and this plan starts from there.

**Rules that still apply:** every engine feature is a **generic kit hook with a do-nothing default and its own test**, built the first time a design needs it ([SHARED_KIT extensions](SHARED_KIT.md#extensions-build-when-a-design-needs-them)). `Fighter` and `GameSim` never check which character they're running. Every new field is hashed in the same commit, and `Version` in `RelayLockstepSession.cs` is bumped whenever sim rules change. See the [coding practices](IMPLEMENTATION_PLAN.md#coding-practices) and the review checklist.

## Overview

```
Step 0   Housekeeping       names, Nek placeholder (id 16)
Step 1   Kit-wide changes   K8 cooldowns + CanBlock · K9 hit modifiers · K10 charge input · K11 move hooks
Pass 1   Playable + distinct  each character: stats, normals, dashes, Special   (+ the extensions they need)
Pass 2   Ultimates            each character's Ultimate                         (+ extensions)
Pass 3   Perks                each character's 2 perks                          (+ extensions)
Pass 4   Readability          HUD, view effects, sounds, AI per character, art hookup
```

After each pass, every character is **complete at that layer** and the game is shippable. If time runs out, the remaining layers stay standard (the Ultimate stays the standard Ultimate, perks stay empty). Within a pass, characters go from cheapest to most expensive, so the risky ones are last and can be cut.

**Rough total: about 45 hours for one programmer.** That's more than a 72h jam leaves after the kit. Either get a second coder on Pass 1-3 (character files are separate, so two people won't clash once Step 1 is in), or plan to ship with the [cut list](#cut-list) applied. The estimates are guesses, not promises.

## Step 0: housekeeping (0.5h)

| What | Where |
|---|---|
| Display names: Samurai → "Ruy", Painter → "Paintress", Cowboy → "Noir", Elephant → "Room", Executioner → "The Executioner", PlagueDoctor → "Dr. Remy" (D-40). Class names stay | `CharacterList.cs` (only the strings) |
| New placeholder `Nek : BaseFighter`, `Id = 16`, header comment like the others. Register it in `CharacterList` and the soak test's pool | `Assets/Characters/Scripts/Fighters/Nek.cs`, `CharacterList.cs`, the soak test |
| Fix the comment in `Samurai.cs` (it says "SAMURAI"; it's Ruy now) | `Samurai.cs` |
| **Unity Editor:** a Code Character Art asset for Nek with its own Placeholder Color (as in R2 of the old plan) | `Assets/Characters/CharacterDataBase/Resources/` |

No `Version` bump: names and a new id don't change sim rules.

## Step 1: kit-wide changes (about 6h)

These change **every character**, including Brawler, Shooter and Sandbag, so do them first and re-run all tests.

| Step | What | Test it with | Est. |
|---|---|---|---|
| **K8** | **Per-move cooldowns** (D-43): `MoveDef.cooldown` (appended field, add it to `CharacterDef.Hash()` and the JSON export), per-fighter cooldown timers (a small fixed array in index order, hashed), the standard values (Light 5, Heavy 10, Grab 15, Special 30), a **dash cooldown** (`DashDef.cooldown`, std 20), and `virtual int ModifyCooldown(MoveDef, int)`. `ChooseMove` skips moves on cooldown. Also **`virtual bool CanBlock`** (default true). Update the AI to skip buttons on cooldown. **Bump `Version`** | A Light can't repeat within 5 ticks of ending; a Heavy right after a Light works; a dash can't repeat for 20 ticks; `CanBlock = false` takes full damage | 2h |
| **K9** | **Hit modifiers**: per-hit copies of damage, hitstun and knockback on `HitboxInstance`; `ModifyOutgoingHit` (attacker) and `ReactToHit` (victim) in pass 1 of `ResolveHits` (D-25) | A modifier doubles damage on the instance; the shared `HitboxDef` is unchanged afterwards (soak test, 2 runs) | 1.5h |
| **K10** | **Charge input** (D-42): a move can have a hold phase (`MoveDef.chargeMax`, `chargeHoldTick`). While its button is held, the move pauses at that tick and counts `chargeTicks` (on `Fighter`, hashed). On release or at the cap, it continues. Two variants: **repeat while held** (a loop range plus a hit cap) and **tap vs hold** (a release before N ticks calls `OnTap` and cancels the move). Needs "which button started this move", stored on `Fighter` | Tap vs 30 vs 60 ticks held; auto-fire at the cap; a tap below the threshold calls `OnTap` and plays no move | 2h |
| **K11** | **Move hooks**: `OnMoveStart(sim)` and `OnMoveTick(sim)`, plus `MoveDef.keepOnLanding` (a ground move that goes airborne keeps playing after landing instead of being cancelled by `Land`) | A move that sets `vy` in `OnMoveStart` rises and lands, and its recovery still plays | 0.5h |

## Pass 1: playable and distinct (about 13h)

Each character gets its stats, Light, Heavy, Grab, dashes, block rule and Special from its doc. **Ultimates and perks stay standard/empty in this pass.** Write the tests listed in each doc that belong to this pass.

| # | Character | Moves to build | New extensions first needed here | Est. |
|---|---|---|---|---|
| 1 | **Ruy** ([doc](Characters/Ruy.md)) | Standard normals; Hadoken with charge (10 → 20) | uses K9 + K10 | 0.5h |
| 2 | **Nek** ([doc](Characters/Nek.md)) | Cape whip, Charged Punch, quick dashes, Rapid Punches (hold to repeat) | uses K10's repeat variant | 1h |
| 3 | **Noir** ([doc](Characters/Noir.md)) | 4-unit Fire box, thrown gun, horse dashes, bullet Special (no ammo limit yet) | **Grab pull**: a grab hitbox that moves the victim next to the grabber before the throw (like the back throw). 1h | 2h |
| 4 | **Executioner** ([doc](Characters/Executioner.md)) | Whip, charged whip (reach), whip grab (reuses Grab pull), slow dashes (60 CD), traps (slow only; bleed comes in Pass 3) | **Placed hitboxes** (`SpawnHitboxAt`, `CountHitboxes`, lifetime 0 = until it triggers, a `groundOnly` flag). **Status effects** core (on the victim, D-24): `ApplySlow`, `TakeDamage(canKO)`, `Heal`, `IgnoresStatus`, plus a speed hook `SpeedPercent` read by walk and dashes. 3h | 3.5h |
| 5 | **Dr. Remy** ([doc](Characters/DrRemy.md)) | Cane jab, rat projectile, short dashes, leech grab (damage only for now), potions (tap to cycle, hold to use) | **Ground zones** (a list in `GameSim`: owner, x, width, ticksLeft, kind; queried in `OnTick`; hashed; no per-tick allocations). 1.5h | 2.5h |
| 6 | **Paintress** ([doc](Characters/Paintress.md)) | Jab, leg sweep, short grab, fast dashes, Reflective Canvas | **Hitbox vs projectile**: hitbox flags `destroysProjectiles` and `reflectsProjectiles`, checked before hits resolve (a reflected projectile changes owner and reverses `vx`). 1.5h | 2h |
| 7 | **Room** ([doc](Characters/Room.md)) | `CanBlock = false`, 200 HP, trunk whip, trunk slam, slow dashes (60 CD), Vine Boom (reuses `destroysProjectiles`), Trunk Grab hold | **Held grab**: appended `FighterStateId.Held`, a hold timer, the victim pinned in front, and the grabber's `ChooseMove` limited to the Special during the hold. The victim's `Held` animator state is generic. 1.5h | 2.5h |

**End of Pass 1:** bump `Version`, run the soak test with all 10 characters, and play one online match. This is the **minimum shippable roster**: seven characters that play differently.

## Pass 2: Ultimates (about 11h)

| # | Character | Ultimate | New extensions | Est. |
|---|---|---|---|---|
| 1 | Noir | Rapid Fire: 5 s of unlimited ammo + a ~0 Special cooldown | none (a buff timer in `HashExtra` + `ModifyCooldown`) | 0.5h |
| 2 | Dr. Remy | Unleash the Rats: 3 unblockable low waves | none (projectiles spawned on ticks 6/36/66) | 0.5h |
| 3 | Nek | Install toggle (D-48): any meter, no superflash, drains, +30% damage, no cooldowns | none (uses K8, K9). It's technically perk 1, but it's his O button, so it's built here | 1.5h |
| 4 | Ruy | Shoryuken: invincible startup, rises, keeps its recovery on landing | **Intangibility** (`IsIntangible`: skipped by hits; **not** by pushboxes, see Paintress's open question). 1h | 1.5h |
| 5 | Executioner | Guillotine: a telegraph on the opponent's spot, an unblockable drop 30 ticks later, 40 damage | **Telegraphs** (a `Telegraph` event + a generic view outline). 1h | 1.5h |
| 6 | Paintress | Paint Splat: an unblockable, jumpable projectile, then 3 s of seeded scramble (plus a view-only blind) | **Seeded roll** (an int hash of seed, tick and index; no state). **Scramble** status (the victim's input is remapped in `FilterInput`). 1.5h | 2h |
| 7 | Room | Address Me: 4 copies at fixed spots that mimic him, die in 1 hit, last 480 ticks | **Summons**: a `Summon` list in `GameSim` (owner, x, facing, hp, ticksLeft, move, moveTick), stepped from the owner's input, with hits spawned as placed hitboxes at the copy's x. Hashed, in index order, no allocations per tick. Plus the view drawing copies with a tint. **The riskiest item in the plan.** | 4h |

**End of Pass 2:** bump `Version`, run the soak test, play one online match.

## Pass 3: perks (about 9h)

| Character | Perks | New extensions | Est. |
|---|---|---|---|
| Ruy | Rage (+missingHp%/2 damage); the Hadoken charge is already done | none (K9) | 0.5h |
| Noir | 6 bullets per round; Point Blank knockback (×1 → ×2 inside 4 units) | none | 1h |
| Nek | Second Wind (`ModifyCooldown` shrinks as he's hurt); Install is already done | none | 0.5h |
| Executioner | Bleed (stacks to 5, DoT, -25% damage at 5, fades); Dismember every 3rd hit (slow). Traps now add bleed too | **Bleed** status on top of the Pass 1 status core | 2h |
| Dr. Remy | Potions are already done; Leeches (1 HP/s for 300 ticks, heals Remy) | **Leech** status (DoT that heals its owner) | 1h |
| Paintress | Invincible forward dash + 8 HP back dash cost; paint puddles (max 3, 240 ticks, +15% / -15% speed) | uses Intangibility (Pass 2) and Zones (Pass 1); a dash-start hook | 2h |
| Room | Stampede (the forward dash is a hit and goes further when hurt); status immunity; Vine Boom stacking with his copies | **Dash hitboxes** (a dash can spawn a follow hitbox); boom stacking in `ResolveHits` pass 1 via K9 | 2h |

**End of Pass 3:** bump `Version`, run the soak test, play one online match.

## Pass 4: readability, AI and art (about 6h, can overlap with art work)

- **HUD** (`HudScreen` and the `FightView` fallback): Noir's ammo pips, Dr. Remy's selected potion (needed: without it a tap is invisible), Nek's Install state, bleed stacks, and small status icons (slow, scramble, leech).
- **View:** the telegraph outline, the zone renderer (paint, poison cloud), copies drawn see-through, the ink-blot overlay **only on the victim's local screen**, and the charge glow.
- **Audio:** Vine Boom (it's the joke), the gun click, the guillotine.
- **AI** (`AIInputSource`): per-character hints from each doc. At least: respect cooldowns (K8), never block as Room, cycle potions as Remy, count Noir's ammo, toggle Install.
- **Art (Unity Editor):** one Code Character Art asset per character, then **Fight > Create Animator for Selected Character** when an artist starts (D-36). New states to animate: `SpecialCharge`/`HeavyCharge` where used, `Held` (everyone), `GrabHold` (Room).

## Cut list

If time runs short, cut from the top. Each cut has a cheaper fallback so the character still works.

| Cut | Fallback | Saves |
|---|---|---|
| Room's copies (Summons) | Room keeps the standard Ultimate, or a one-shot "stampede" placed hitbox across the screen | ~4h |
| Vine Boom stacking | A plain Vine Boom | ~0.5h |
| Paintress's reflect | The canvas **destroys** projectiles instead (reuses Room's `destroysProjectiles`) | ~1h |
| Paint puddles (zones for Paintress) | Perk 2 becomes a flat +10% speed | ~1h |
| Room's held grab | A normal grab with long hitstun, so Vine Boom combos after it | ~1h |
| The scramble shuffle | Left/right swap only (no seeded roll) | ~0.5h |
| Charge input | Fixed slow windups (charged = the full-charge numbers, always) | ~2h |

## Issues to settle before or during the build

These are collected from the docs' open questions. The ones that can change the plan are first.

1. ~~Online play is still Brawler vs Brawler.~~ Out of date when written: online character select was already built (ARCHITECTURE.md, "Online"), so all seven are playable online.
2. **Old Tech is missing for all seven** (D-45). It's the jam theme, so get answers before the art is locked.
3. **No round timer** (D-26) plus Dr. Remy's healing and leeches means a stalling risk. Decide on a timer or a heal limit before Pass 3.
4. **Comeback overload:** Ruy (rage), Nek (cooldowns) and Room (dash) all get stronger when hurt, on top of the meter already favouring the player taking damage (D-08).
5. **Balance extremes:** Room has 200 HP and no block. Vine Boom stacking can reach ×16 without a cap. Executioner bleed can reach 30 damage. Ruy's Shoryuken does 45 with full rage.
6. **Cooldowns change the feel of every character**, including the AI and test characters. Playtest K8 alone before building on it.
7. **Shared mechanics:** Noir's lasso and the Executioner's whip grab are the same thing. Fine for the code, but maybe not for the design.

## Testing

- Every extension gets a unit test in the same commit (the "Test it with" column and each doc's Tests list).
- The **determinism soak test** must include Nek and run green after every step. Per-tick status, zone and summon lists are exactly the kind of state it catches.
- After each pass: one online match between two builds (rounds, statuses and summons must stay in sync), and a full best-of-3 for every character vs the AI.
- Before every push: the [review checklist](IMPLEMENTATION_PLAN.md#review-checklist-read-it-before-every-push).
