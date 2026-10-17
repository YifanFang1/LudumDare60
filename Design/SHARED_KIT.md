# The shared kit

Every character gets these actions. They are written **once** in the sim (`Fighter`, `BaseFighter`, `GameSim`). For now all six roster characters use the kit unchanged (D-29). A later design overrides only what it changes. The decisions behind each rule are in [DECISIONS.md](DECISIONS.md) (`D-xx`).

Contents: [Controls](#controls) · [The standard moveset](#the-standard-moveset) · [Block](#block) · [Grab](#grab) · [Dash](#dash) · [Meter](#meter) · [Special and Ultimate](#special-and-ultimate) · [Rounds](#rounds) · [Pushboxes](#pushboxes) · [The new API at a glance](#the-new-api-at-a-glance) · [View, audio and AI](#view-audio-and-ai) · [What gets hashed](#what-gets-hashed) · [Extensions](#extensions-build-when-a-design-needs-them)

## Controls

| Key | Action | Bit in `Sim/Buttons.cs` |
|---|---|---|
| A / D | Walk. Hold away from the opponent to block. Double-tap to dash. | `Left = 1`, `Right = 2` (existing) |
| S | Down (unused by the sim for now, D-22) | `Down = 8` (existing) |
| J | Light | `Light = 16` (existing) |
| K | Heavy | `Heavy = 32` (existing) |
| L | Grab | `Grab = 128` (**new**) |
| I | Special | `Special = 256` (**new**) |
| O | Ultimate | `Ultimate = 512` (**new**) |

`Block = 64` and `Up = 4` stay reserved and unused. Add one `Make(...)` line per new button in `FightControls` and remove the `Block` line (D-02).

**If several attack buttons are pressed on the same tick**, the priority is Ultimate > Special > Grab > Heavy > Light. Attacks are checked before dashes, and dashes before walking. The **Ultimate needs a fresh press** (pressed this tick, not held on the previous one), so holding O can't fire it by accident the moment the bar fills.

## The standard moveset

These are `BaseFighter`'s defaults, stored as `static readonly` moves (`StandardLight`, `StandardHeavy`, ...). The six placeholder characters use all of them as they are.

| Stat | Value |
|---|---|
| HP | 100 |
| Walk speed | 60 |
| Body (hurtbox) | `Box(-500, 0, 1000, 2000)` (1 x 2 units) |

| Move | Startup (spawnTick) | Active / lifetime | Total | Damage | Hitstun | Knockback | Box | Notes |
|---|---|---|---|---|---|---|---|---|
| Light (J) | 4 | 3 | 15 | 5 | 12 | 40 | `Box(400, 900, 900, 400)` | Fast and safe |
| Heavy (K) | 10 | 4 | 28 | 12 | 20 | 80 | `Box(300, 700, 1300, 700)` | Long reach, punishable |
| Grab (L) | 5 | 2 | 40 | 12 | 40 | 90 | `Box(300, 600, 700, 1000)` | `grab = true`, see [Grab](#grab) |
| Special (I) | 12 | lifetime 150, speed 100 | 30 | 10 | 18 | 60 | `Box(400, 900, 500, 400)` | A projectile. **One on screen at a time** |
| Ultimate (O) | 6 (after the freeze) | 4 | 45 | 30 | 40 | 120 | `Box(300, 0, 2000, 2200)` | A big blockable strike (D-11). Gives no meter |
| Forward dash | - | - | 14 | - | - | speed 130 | - | Travels ~1.8 units |
| Back dash | - | - | 16 | - | - | speed 110 | - | Travels ~1.8 units |
| Perks | - | - | - | - | - | - | - | None for now (D-12, D-29) |

These match the scale of the test characters (Brawler's Jab and Haymaker, Shooter's Fireball), so everything can be tested against Brawler, Shooter and Sandbag.

**Brawler and Shooter** keep their own Light and Heavy and get the standard Grab, Dash, Special and Ultimate for free. Shooter overrides `CanUseMove`, so its override must call `base.CanUseMove(sim, move)` for moves that aren't its fireball. **Sandbag** ignores all input, so nothing changes for it.

## Block

No change to the existing rule (D-03): hold away from the opponent while in Idle, Walk or Blockstun. A blocked hit does 0 damage, pushes back by `BlockPushPercent` of the knockback, and causes blockstun of `BlockstunPercent` (60%) of the hitstun.

New exceptions:
- Hitboxes flagged `unblockable` or `grab` ignore blocking.
- You can't block during a dash (it's a commitment).

## Grab

A grab is a normal `MoveDef` whose hitbox has `grab = true`.

**Grab rules** (D-05, D-06), which apply to every hitbox with `grab = true`:
1. Ignores blocking.
2. **Can't catch a victim in Hitstun, Blockstun or KO.** The hitbox stays alive for its remaining active ticks, so it can still catch them if they recover in time.
3. *Can* catch a victim who is attacking or dashing. Faster strikes still beat a grab, because a hit on the grabber cancels its hitbox (`RemoveFollowHitboxes`, which already exists).

**Back throw** (D-07): if the grabber is holding back when the grab connects, `GameSim` moves the victim to the grabber's other side (`victim.x = grabber.x - grabber.facing * 900`, clamped to the arena) **before** `TakeHit`, so the knockback then sends them the other way. Raise a `Grab` event instead of `Hit`.

A whiffed grab has 33 ticks of recovery. A landed grab leaves the grabber about +7 (33 ticks of their recovery vs 40 hitstun), with the victim thrown about 3.6 units away.

## Dash

New state `FighterStateId.Dash` (appended **at the end** of the enum so the old numbers don't change).

- **Input:** a fresh press (not held on the previous tick) of Left or Right, when the previous fresh press of the same direction was at most `DashTapWindow = 12` ticks earlier. Taps are recorded every tick, but a dash only starts from Idle or Walk.
- **Direction:** toward the opponent = forward dash, away = back dash. It's decided when the dash starts.
- **During a dash:** `vx = direction * speed` for the dash's `ticks`, then Idle. You can't block or attack. You *can* be hit and grabbed.
- **Per character later:** override `ForwardDash` / `BackDash`, which return a `DashDef { ticks, speed }`.

`Fighter` keeps `prevInput`, `lastTapDir` and `lastTapTick` for this. They change behaviour, so they're hashed.

## Meter

`meter` is an int on `Fighter`, `0..MeterMax` (`MeterMax = 1000`). Gains (D-08) are constants in `Fighter`:

| Event | Attacker gets | Victim gets |
|---|---|---|
| Hit lands | `damage * 7` | `damage * 4` |
| Hit blocked | `damage * 2` | `damage * 2` |
| Hit from an Ultimate | 0 | 0 |

Use `AddMeter(amount)`, which clamps. Hits spawned during the Ultimate move are flagged on the `HitboxInstance` (`fromUltimate`, set at spawn) so they give no meter. **The meter carries between rounds** (D-18).

## Special and Ultimate

`BaseFighter` gets two new slots, `SpecialMove` and `UltimateMove`, defaulting to the standard moves above (`null` would mean "this character has none").

- **Special (I):** usable whenever `CanUseMove` allows it. The default `CanUseMove` allows the standard Special only when `ProjectilesOnScreen(sim) == 0`.
- **Ultimate (O):** a move is an Ultimate when its `MoveDef.ultimate` flag is set (D-31). `BaseFighter` only picks it on a fresh press with a full bar, and `Fighter` refuses any Ultimate without a full bar whatever `ChooseMove` returns, so the rule holds for every character, inspector ones included. It starts from Idle or Walk. When it starts:
  1. `meter = 0`.
  2. `sim.StartSuperFlash(index)` sets `sim.freezeTicks = SuperFlashTicks` (30) and raises a `SuperFlash` event.
  3. While `freezeTicks > 0`, `GameSim.Step` only counts it down and advances `tick`. Nobody thinks, moves or ages hitboxes, and inputs are ignored.
  4. Then the Ultimate move plays like any other move from its tick 0.

## Rounds

Best of 3, and the meter carries over (D-18, D-19, D-20).

- `GameSim.Reset(seed, Func<Fighter> make0, Func<Fighter> make1)` keeps the two factories. `MatchSessionBase` passes `delegate { return CharacterList.Create(id); }`.
- When someone is KO'd: raise a `RoundEnd` event, give the round to the survivor (to both on a double KO), and wait `RoundEndDelay = 120` ticks (the KO pose plays).
- Then, unless someone has `RoundsToWin = 2`: build **fresh fighters** from the factories, copy over their `meter`, clear hitboxes and the freeze, put the fighters at the start positions, and raise `RoundStart`.
- `IsOver` now means "the match is over". `Winner` works as before (2 = draw).
- New state: `round`, `roundsWon[2]`, `roundOverTicks`.

## Pushboxes

After both fighters move (D-21): if their bodies overlap along x (using each hurtbox's x range), push each one apart by half the overlap. If one is against a wall, the other takes all of it. **Tie-break when x is exactly equal:** fighter 0 goes left. It must be deterministic, never "whichever comes first".

## The new API at a glance

```csharp
// ----- Sim/Buttons -----
public const int Grab = 128;
public const int Special = 256;
public const int Ultimate = 512;

// ----- Sim/HitboxDef: new fields -----
public bool unblockable;     // ignores blocking
public bool grab;            // grab rules: ignores blocking, can't catch hitstun/blockstun
public string visual;        // COSMETIC: art key for projectiles. Not part of any hash.

// ----- Sim/MoveDef: new field -----
public bool ultimate;        // needs a full meter, spends it, starts the superflash, its hits give no meter

// ----- Sim/HitboxInstance -----
public bool fromUltimate;    // set at spawn from the owner's move; gives no meter

// ----- Sim/Fighter -----
public int meter;
public void AddMeter(int amount);
public virtual DashDef ForwardDash { get { return new DashDef(14, 130); } }
public virtual DashDef BackDash    { get { return new DashDef(16, 110); } }

// ----- Characters/BaseFighter: defaults are the standard moveset -----
protected virtual MoveDef LightAttack  { get { return StandardLight; } }
protected virtual MoveDef HeavyAttack  { get { return StandardHeavy; } }
protected virtual MoveDef GrabMove     { get { return StandardGrab; } }
protected virtual MoveDef SpecialMove  { get { return StandardSpecial; } }
protected virtual MoveDef UltimateMove { get { return StandardUltimate; } }

// ----- Sim/GameSim -----
public int freezeTicks;
public void StartSuperFlash(int owner);
public void Reset(int seed, System.Func<Fighter> make0, System.Func<Fighter> make1);
```

New `SimEventType` values, **added at the end**: `Grab`, `Dash`, `SuperFlash`, `RoundStart`, `RoundEnd`.

`HitboxDef` and `MoveDef` are also used by inspector characters, so `unblockable`, `grab` and `ultimate` are in `CharacterDef.Hash()`, and `JsonUtility` exports them to the server file automatically. `visual` is left out of the hash because it's cosmetic. `AttackButton` (the inspector's button dropdown) gained `Grab`, `Special` and `Ultimate` at the end (D-32), so inspector characters can bind every button.

## View, audio and AI

None of these touch the sim. They react to `SimEvent`s and read state.

- **HUD:** an Ultimate meter for each player (glowing when full), round pips, and "ROUND 1" / "FINAL ROUND" / "K.O." banners. Both HUDs have them: the Canvas `HudScreen` used by `Menu.unity` (**Fight > Add Meter And Round HUD** adds them to an existing canvas), and the OnGUI `FightView` fallback.
- **Superflash:** a generic screen flash plus the character's name on `SuperFlash`.
- **Projectile art:** `CharacterLook` gets a list of `(visual key, Sprite)` pairs. `WorldView` draws every non-follow hitbox whose `visual` matches, **always** (not only when debug boxes are on). Without art it's a yellow box, as today. The standard Special uses the key `special`.
- **Animator states:** add `DashForward` and `DashBack` to the generic state list (`WorldView` and `CharacterAnimatorTool`). `Grab`, `Special` and `Ultimate` come from `ListMoves()` once `BaseFighter.ListMoves()` includes the new slots.
- **Audio:** optional clip slots in `AudioManager` for the new events.
- **AI (`AIInputSource`, tested by `AITests`):** a small upgrade. Ultimate when the meter is full and it's in range. Grab sometimes when close and the player is blocking. Special sometimes at range. Hold back sometimes when the player starts an attack. Dash in sometimes from far away. It may read the sim and use `System.Random`; it only outputs bits.

## What gets hashed

Everything that changes behaviour must be in `GameSim.Hash()`, or desyncs go unnoticed:
- **Fighter:** `meter`, `dashTicks`, `prevInput`, `lastTapDir`, `lastTapTick`, plus `facing` and which `move` is playing, which the original hash missed (D-34). Also the existing fields and `HashExtra()`.
- **GameSim:** `freezeTicks`, `round`, `roundsWon[0..1]`, `roundOverTicks`, and each hitbox's `owner`.

**Bump `Version` in `RelayLockstepSession.cs`** when the kit lands. It changes the sim rules and the input bits.

## Extensions (build when a design needs them)

Not scheduled (D-30). When a real character design needs one of these, build it **as a generic kit feature** (a virtual hook with a do-nothing default, plus a test) in its own commit, then use it from the character. Never special-case a character inside `Fighter` or `GameSim`.

| Extension | What it adds | Notes |
|---|---|---|
| Move hooks | `OnMoveStart(sim)`, `OnMoveTick(sim)`: code that runs when a move starts and on every tick of it | For lunges, teleports, spending ammo. Moves may set `vx` or `x` |
| Cancels | `moveContact` / `moveHit` flags, `CanCancel(sim, next)`, `CanCancelDash(sim, next)` | Cancels need a fresh press. Add a 4-tick input buffer only if players can't hit the timing |
| Hit modifiers | Per-hit copies of `damage`/`hitstun`/`knockback` on `HitboxInstance`; `ModifyOutgoingHit` (attacker) and `ReactToHit` (victim) returning `Normal`/`Armor`/`Absorb` | For armor, parries and damage bonuses. Edit the instance, never the def (D-25). The hooks run in pass 1 of `ResolveHits`, before anything is applied, so trades stay fair |
| Status effects | `ApplyPoison`, `ApplyScramble` (swapped left/right), `TakeDamage(sim, amount, canKO)` | They live on the victim (D-24); damage over time can't KO (D-23) |
| Intangibility | `IsIntangible`: skipped by hits and pushboxes | The view hides the fighter or shows it see-through |
| Placed hitboxes | `sim.SpawnHitboxAt(owner, def, worldX)`, `sim.CountHitboxes(owner, def)` | For traps and hitboxes at a chosen spot |
| Telegraphs | A `Telegraph` event (x, width, duration) that the view draws as a warning outline | For attacks that need to warn before they hit |

Each one added means more fields in the hash and a `Version` bump.
