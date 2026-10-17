# Implementation plan

> **Status (2026-10-17):** K1-K7, K-end, V1, A1, R1 and R3 are implemented, and all 47 EditMode tests pass. **R2** (one art asset per character, plus the generated Animator) is made in the Unity Editor; the steps are in the R2 row below. Decisions made while building are D-31 to D-35 in [DECISIONS.md](DECISIONS.md). The plan below is kept as written, so it shows what was intended.

How to build the [shared kit](SHARED_KIT.md) and the six placeholder characters with **one programmer** (D-27). The goal (D-29) is the bare implementation: all six characters selectable and playing full best-of-3 matches with the standard moveset. Every step below is a small commit that compiles, passes the EditMode tests and leaves the game playable.

**Rough total:** ~15 hours (kit ~11h, view and AI ~3h, roster ~1h). The estimates are guesses to help plan; they aren't promises.

## Overview

```
Phase 1  Kit (sim)          K1 ─ K2 ─ K3 ─ K4 ─ K5 ─ K6 ─ K7 ─ K-end
Phase 2  View + AI          V1 (meter bar, rounds HUD, superflash, new Animator states, projectile art)   A1 (AI)
Phase 3  Roster             R1 (six placeholder classes) ─ R2 (art slots) ─ R3 (docs)
Later    Real designs       each character designed by the team from Characters/_TEMPLATE.md
```

## Phase 1: the kit (sim only)

Do these in order: each one depends on the previous. File paths are under `Assets/Scripts/`.

| Step | What | Files | Test it with | Est. |
|---|---|---|---|---|
| **K1** | New buttons (`Grab = 128`, `Special = 256`, `Ultimate = 512`), `Make(...)` lines in `FightControls` (L / I / O), remove the `Block` line | `Sim/Buttons.cs`, `Match/FightControls.cs` | The controls UI shows the new keys | 0.5h |
| **K2** | `HitboxDef.unblockable` / `grab` / `visual`. Grab rules in `ResolveHits`. Add the new fields to `CharacterDef.Hash()` and `CharacterJson` | `Sim/MoveDef.cs`, `Sim/GameSim.cs`, `Characters/CharacterDef.cs`, `CharacterData/CharacterJson.cs` | A grab hitbox ignores blocking; a grab can't hit blockstun | 1.5h |
| **K3** | Pushboxes after `Move` (index tie-break) | `Sim/GameSim.cs` | Two fighters walking into each other never overlap | 1h |
| **K4** | `GrabMove` slot + `StandardGrab` in `BaseFighter`, back throw before `TakeHit`, `Grab` event | `Characters/BaseFighter.cs`, `Sim/GameSim.cs`, `Sim/SimEvent.cs` | Grab beats a blocking Sandbag; a back throw swaps sides | 1h |
| **K5** | Dash: `FighterStateId.Dash` (appended), `prevInput` and tap tracking, `DashDef`, `ForwardDash`/`BackDash`, `Dash` event | `Sim/Fighter.cs`, new `Sim/DashDef.cs` | A double-tap within 12 ticks dashes; at 13 it doesn't; no blocking while dashing | 1.5h |
| **K6** | Meter (`AddMeter`, gain constants, `fromUltimate`). `SpecialMove`/`UltimateMove` slots, the standard moves as `BaseFighter` defaults, `ChooseMove` priority, fresh press for the Ultimate. `StartSuperFlash` and `freezeTicks` in `Step`. Fix `Shooter.CanUseMove` to call `base` | `Sim/Fighter.cs`, `Sim/GameSim.cs`, `Sim/HitboxInstance.cs`, `Characters/BaseFighter.cs`, `Characters/Shooter.cs` | Meter numbers; the ult needs a full bar and empties it; positions frozen for 30 ticks; one Special projectile at a time | 2h |
| **K7** | Rounds: factories in `Reset`, a fresh fighter per round with meter copied, `RoundStart`/`RoundEnd`, best of 3, double KO. Update `MatchSessionBase` and check that `LockstepRunner`/`FightView` still use `IsOver` correctly | `Sim/GameSim.cs`, `Match/MatchSessionBase.cs` | KO → next round; meter carries; 2 rounds end the match; double KO | 2h |
| **K-end** | Determinism soak test (see [Testing](#testing)), every new field in `GameSim.Hash()`, **bump `Version`** in `Net/RelayLockstepSession.cs`, update ARCHITECTURE.md and CHARACTERS.md (controls, the standard moveset, rounds) | | All tests green | 1h |

## Phase 2: view and AI

| Step | What | Files | Est. |
|---|---|---|---|
| **V1** | Meter bars, round pips and banner in `FightView`. A generic flash plus the name on `SuperFlash`. `DashForward`/`DashBack` in the state lists of `WorldView` and `CharacterAnimatorTool`. `BaseFighter.ListMoves()` includes Grab/Special/Ultimate. Projectile sprites by `visual` key on `CharacterLook`, drawn by `WorldView` | `View/FightView.cs`, `View/WorldView.cs`, `Editor/CharacterAnimatorTool.cs`, `Characters/BaseFighter.cs`, `CharacterData/CharacterLook.cs` | 2h |
| **A1** | The AI upgrade described in [SHARED_KIT](SHARED_KIT.md#view-audio-and-ai) | `Match/AIInputSource.cs` | 1h |

After Phase 2, Brawler and Shooter already have grabs, dashes, a Special, an Ultimate and the meter, so the whole kit can be played before any roster character exists.

## Phase 3: the roster

| Step | What | Est. |
|---|---|---|
| **R1** | Six classes, one file each, in `Assets/Scripts/Characters/`: `Samurai` (id 10), `Painter` (11), `Cowboy` (12), `Elephant` (13), `Executioner` (14), `PlagueDoctor` (15). Each one is only a header comment and its id, because `BaseFighter`'s defaults are the standard moveset (see below). Register all six in `CharacterList` (and its comment list), with the display name "Black and White Cowboy" for `Cowboy`. Add them to the soak test's pool. | 0.5h |
| **R2** | For each, a **Code Character Art** asset in `Resources/Characters` with a different `Placeholder Color` (otherwise six identical boxes can't be told apart). Run **Fight > Create Animator for Selected Character** only when an artist starts on that character: it assigns a View Prefab, which replaces the coloured box, and an empty prefab shows nothing (D-36). These are Unity Editor steps, not code. | 0.5h |
| **R3** | Update the roster table in the root `CLAUDE.md`, the status tables in [README.md](README.md), and CHARACTERS.md. | 0.25h |

What a placeholder character looks like. One file per character keeps people out of each other's files when the real designs arrive:

```csharp
// SAMURAI. Roster character, id 10. Design target: Shoto.
// PLACEHOLDER: uses BaseFighter's standard moveset unchanged (Design/SHARED_KIT.md).
// Its real moveset, Special, Ultimate and perks will be designed by the team
// (Design/Characters/_TEMPLATE.md). Override only what the design changes.
public class Samurai : BaseFighter
{
    public const int Id = 10;
}
```

## Later: real designs

When the team has designed a character:
1. Fill in a copy of [Characters/_TEMPLATE.md](Characters/_TEMPLATE.md) as `Design/Characters/<Name>.md`, and record any new rule in [DECISIONS.md](DECISIONS.md).
2. If the design needs an engine feature, build it first from the [extensions list](SHARED_KIT.md#extensions-build-when-a-design-needs-them), as a generic hook with its own test.
3. Override only what changes in the character's class. Extra state goes in `HashExtra()`.
4. Add tests for the character's special rules, and update the docs.

## Testing

**Unit tests (EditMode, `Assets/Tests/EditMode`):** drive `GameSim` directly with scripted inputs. Add a small helper in the test assembly first (e.g. `SimHarness.Run(sim, ticks, input0Func, input1Func)`), so each test is a few lines. Every kit step has at least one test (the "Test it with" column above).

**Determinism soak test (write it in K-end, keep it forever):** for every pair of registered characters, play a full best-of-3 with **seeded random inputs** (tests may use `System.Random`; the sim may not), **twice in a row in the same process**, and check that the hash is identical on every tick. This catches the two classic desync bugs: shared static data changed during a match (the second run differs) and accidental nondeterminism. It also catches exceptions and stuck matches (assert the match ends within a maximum number of ticks). It can't catch state that's missing from the hash; the review checklist below covers that.

**Manual:** each kit step vs Sandbag and vs the AI, and **one online match** between two builds (or a build and the editor) after K7, because rounds change the match flow.

## Coding practices

1. **The sim is the game, the view is a TV.** Anything that decides an outcome lives in `Sim`/`Characters`. Anything you can see or hear reacts to `SimEvent`s or reads state.
2. **Generic hooks, not special cases.** If a character needs an engine change, add a virtual with a do-nothing default. `Fighter` and `GameSim` must never say `if (this is Elephant)`, and a character must never check the opponent's type.
3. **Shared move data is read-only.** `MoveDef`/`HitboxDef` are `static readonly`, shared by both players and every round. Never change them at runtime.
4. **Every field that changes behaviour is hashed,** in the same commit that adds it: character fields in `HashExtra()`, kit fields in `GameSim.Hash()`.
5. **Fresh objects instead of reset methods** (D-19). Field initialisers are your reset.
6. **Append-only enums and bits.** New `FighterStateId`, `SimEventType` and `Buttons` values go at the end, and never reuse a value.
7. **Integer maths:** multiply before dividing (`damage * 150 / 100`). C# integer division rounds toward zero (`-5 / 2 == -2`), so apply `facing` **after** dividing: `facing * (speed * pct / 100)`.
8. **Nothing order-dependent in the sim:** no iterating over `Dictionary` or `HashSet`. Lists in index order only. Tie-breaks go by fighter index.
9. **No allocations per tick in the sim** (WebGL's garbage collector is slow): reuse lists like `hitsThisTick` does.
10. **Named arguments for moves** (`Melee(...)`, `Projectile(...)`), and tuning constants at the top of the file with a comment saying why.
11. **`ChooseMove` and `CanUseMove` only read.** State changes happen in the hooks that `Step` calls.
12. **One commit per step, always green.** Message format: `K3: pushboxes`. Bump `Version` in any commit that changes the shared sim rules.
13. **The test comes with the feature,** in the same commit.
14. **Docs travel with the code:** the status tables here, ARCHITECTURE.md and CHARACTERS.md.

### Review checklist (read it before every push)

- [ ] Ints only; no `float`, `Time`, `Random` or `UnityEngine` in `Sim`/`Characters`.
- [ ] New fields are in `HashExtra()` or `GameSim.Hash()`.
- [ ] No writes to a `MoveDef`/`HitboxDef` at runtime.
- [ ] New enum values and bits are appended.
- [ ] `Version` bumped if shared sim rules changed.
- [ ] EditMode tests pass, including the soak test.
- [ ] Docs updated.

## Done means

- All six characters appear in the lobby and in the AI's character choice, and each one plays a full best-of-3 against the AI without anything getting stuck.
- Every move in the standard moveset works for every character: Light, Heavy, block, grab and back throw, both dashes, Special, Ultimate with the superflash, and the meter carrying across rounds.
- All EditMode tests pass, including the soak test with all nine characters in the pool.
- One online match has been played after the kit landed.
- Each character has an art asset with a generated Animator (the art itself can still be a coloured box).

## Risks to watch

| Risk | What to do about it |
|---|---|
| Rounds break online | Rounds are inside `Step`, so both machines change round on the same tick. Run one online match after K7. |
| Shooter or Brawler change behaviour by surprise | They now get the standard Grab, Special and Ultimate. That's intended; check them in the soak test and by hand. |
| Six identical boxes are confusing in playtests | Different `Placeholder Color` per character (R2), and the names shown in the HUD. |
| The standard Ultimate is too strong or too weak | It's one set of numbers in `BaseFighter`, so tune it once for everyone. |
