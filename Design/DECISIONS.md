# Design decisions

A log of the decisions behind the launch roster and the shared moveset. Each one says what we chose, what else we considered and why, so nobody has to rediscover the reasoning in the middle of the jam.

- **Status:** `Decided` = agreed with the project lead. `Default` = picked by the planner as a sensible default and not asked about yet. Challenge a Default freely, but write the change here.
- **Changing a decision:** don't delete the old entry. Set its status to `Replaced by D-xx` and add a new entry at the bottom.

Date for all entries below: 2026-10-16.

## Scope

### D-29 All six characters are identical placeholders for now (Decided; replaced by D-40 on 2026-10-17)
ArcadeGuy, Painter, Black and White Cowboy, Elephant, Executioner and Plague Doctor get the **same stats and the same standard kit**, including the same Special and Ultimate, and no perks. The goal right now is the bare implementation: all six exist, can be selected, and play a full match with the basic attacks.
- *Why:* the creative side (move names, looks, eras, what each Special, Ultimate and perk does, which Old Tech(nique) each one gets) belongs to the artists and designers on the team, not to the plan. Each character gets its own design later, from [Characters/_TEMPLATE.md](Characters/_TEMPLATE.md).
- Each type (Shoto, Rushdown, Zoner, Grappler, Trapper, Mixup) is recorded as a design target only.

### D-30 Hooks only a specific design would need are not built yet (Default)
Cancels, hit modifiers (armor, parry), status effects, intangibility and placing hitboxes at a spot are specified in [SHARED_KIT.md](SHARED_KIT.md#extensions-build-when-a-design-needs-them) but **not scheduled**. Each is built, as a generic kit feature, when the first real character design needs it.
- *Why:* jam rule: build the simplest thing that ships. The existing hooks (`OnTick`, `OnHitDealt`, `OnHitReceived`, `FilterInput`, `CanUseMove`, `ChooseMove`) already cover a lot.

## Controls and generic actions

### D-01 Grab, Special and Ultimate use dedicated buttons (Decided)
J Light, K Heavy, **L Grab**, **I Special**, **O Ultimate**. A/D move, S down.
- *Considered:* Street Fighter button combos (J+K = grab...), and motion inputs (quarter-circle + button).
- *Why:* one key per action is easiest on a keyboard and for players new to fighting games, it's trivial for the AI, and it needs no input buffer in the sim.
- New bits in `Sim/Buttons.cs`: `Grab = 128`, `Special = 256`, `Ultimate = 512`. Existing bit values don't change.

### D-02 Grab defaults to the L key (Default)
The option we picked showed U for Grab, but D-03 keeps hold-back blocking, so L (currently bound to the unused Block bit) is free. J, K and L are the home row, which suits the three most used actions. The `Block` line is removed from `FightControls`, and bit 64 stays reserved. Players can rebind it either way.

### D-03 Blocking stays "hold away from the opponent" (Decided)
- *Considered:* a block button only, or both.
- *Why:* it's the genre standard and it's already built and tested in `Fighter`. Per-character variations can come from perks later.

### D-04 Dash = double-tap forward or back (Decided)
A second fresh press of the same direction within **12 ticks** starts a dash.
- *Considered:* a dash key + direction.
- *Why:* it's the genre standard and needs no extra key.

### D-05 Grabs are unblockable and can't be teched (Decided)
A grab beats blocking. Anything that hits the grabber during startup beats the grab. A whiffed grab has long recovery. There is no throw-break input.
- *Considered:* a tech window (press Grab within ~8 ticks to escape).
- *Why:* the simplest rule that still works: block beats strikes, grab beats block, strikes beat grab.

### D-06 Grabs can't catch a fighter in hitstun, blockstun or KO (Default)
- *Why:* otherwise a blocked string always ends in a guaranteed grab, and grabs become an infinite combo extender. This is the standard "throw invulnerability" rule.

### D-07 Hold back while grabbing = back throw (Default)
The victim is put on the other side of the grabber before the knockback is applied. Cheap, generic, and it makes corner escapes possible.

## Ultimate meter

### D-08 The meter fills by dealing and taking damage (Decided)
`MeterMax = 1000`. Starting values: **dealt hit = damage x 7**, **taken hit = damage x 4**, **blocked hit = damage x 2 for both players**. Hits from an ultimate give no meter. That works out to about one full bar per round.
- *Considered:* dealing damage only (the losing player rarely gets an ultimate), or a slow passive fill.
- *Why:* rewards aggression and gives the losing player a comeback.

### D-09 The Ultimate needs a full bar and spends all of it (Default; K3N is the exception, D-48)
One bar, no partial spending. Usable from Idle or Walk.

### D-10 The Ultimate starts with a short freeze (Decided)
A 30-tick "superflash": the whole sim stops while the view plays a flash, then the ultimate plays out.
- *Considered:* a full camera cut-in cinematic, or no freeze.
- *Why:* readable, deterministic, cheap.

### D-11 The placeholder Ultimate is a blockable strike (Default)
There's no chip damage in the game, so a blocked ultimate does nothing, and its long recovery is the risk. Real designs may make theirs unblockable or grab-type.

## Characters

### D-12 Each character will have 2 fixed perks (Decided; empty for now, D-29)
Always-on passives that can change any generic move. Not pickable at character select.
- *Considered:* pick 1 of 3 at character select (needs UI, network sync and 3x the balance work).

### D-13 Each character's Old Tech(nique) will live in its Ultimate (Decided)
The ultimate *is* the character's glitch or arcade trick. **Which** glitch each character gets is up to the team (D-29). The placeholder ultimate has none.

### D-14 One Special per character (Default)
One button, one move. A real design may let a held direction change it a little.

### D-15 Every roster character is a code character on a shared kit (Decided)
Each character is a `BaseFighter` subclass in its own file. Grab, dash, meter, the Special and Ultimate slots and rounds are written **once** in `Fighter` / `BaseFighter` / `GameSim`, and the placeholder moves are `BaseFighter`'s defaults. A placeholder character is then just its id; a real design overrides what it changes.
- *Considered:* extending the inspector (`CharacterDef`) system with the new slots.
- *Why:* real designs will need unique code anyway, and there's one programmer. Cost: tuning needs a recompile, and balance can't be pushed through the server for these characters.

### D-16 Ids 10-15 for the launch roster, 16+ for new characters (Default)
ArcadeGuy 10, Painter 11, Black and White Cowboy 12, Elephant 13, Executioner 14, Plague Doctor 15. 3-9 are left for test characters. Ids are permanent once pushed.

### D-17 Move names are slot names (Default)
Moves are named `Light`, `Heavy`, `Grab`, `Special`, `Ultimate`, because the move name is also the Animator state name. Artists get the same state list for every character.

## Match and engine

### D-18 Best of 3 rounds, meter carries over (Decided)
A KO ends the round; the first to 2 rounds wins. Health, position and hitboxes reset each round; **the meter carries over**.
- *Considered:* keeping single-round matches.

### D-19 A fresh fighter object every round (Default)
`GameSim` gets a factory for each player and builds new `Fighter` objects at the start of every round, copying only `meter` across. Per-character state can never leak between rounds, and nobody has to write a reset method. Factories are plain `System.Func<Fighter>`, so `Sim` still doesn't reference `Characters`.

### D-20 Double KO: both players get the round (Default)
If both players reach 2 rounds at the same time, the match is a draw (`Winner == 2`, which already exists).

### D-21 Add pushboxes (fighters can't walk through each other) (Default)
Right now `Fighter.Move` only adds `vx`, so fighters can overlap and pass through each other. Dashes make that obvious. After movement, `GameSim` pushes overlapping bodies apart equally.

### D-22 No crouching, lows or overheads for now (Decided)
`Buttons.Down` stays unused by the sim.

### D-23 Damage over time can't KO (Default; applies when statuses are built, D-30)
Poison-style damage leaves the victim at 1 HP at least, so a KO always comes from a real hit.

### D-24 Status effects live on the victim (Default; applies when built, D-30)
Status fields sit on the victim's `Fighter`, are applied through `Fighter` methods and are hashed by `GameSim`. A character never writes another fighter's fields directly.

### D-25 Hits are modified through hooks, never by changing shared move data (Default; applies when built, D-30)
`MoveDef`s are `static readonly` and shared by both players. Per-hit changes edit the **hit instance**, not the definition.

### D-26 No round timer for now (Default, open question)
A timer stops a player stalling with a lead. Revisit after the first playtest; if added, the player with the higher HP % wins the round when it runs out.

## Process

### D-27 One programmer: shared kit first, then the roster (Decided: one programmer. Default: the order)
See [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md).

### D-28 Design docs live in `/Design` (Decided)
Outside `Assets/`, so there are no `.meta` files. When characters get real designs, each one gets its own file in `Design/Characters/` so people don't edit the same file.

## Implementation (2026-10-17)

### D-31 "Ultimate" is a flag on the move, not a slot (Default)
`MoveDef.ultimate` marks a move as an Ultimate. `Fighter` enforces the full-bar rule, spends the meter and starts the superflash for any move with the flag, and `GameSim` gives no meter for its hits.
- *Considered:* having the sim recognise the Ultimate by the slot it came from (`BaseFighter.UltimateMove`).
- *Why:* `Fighter` (in Sim) can't see `BaseFighter`'s slots, and inspector characters have no slots at all. A data flag makes the rule hold for every kind of character, in one place.

### D-32 Inspector characters can bind Grab, Special and Ultimate (Default)
`AttackButton` gained `Grab`, `Special` and `Ultimate` (appended, so stored values don't change), as the comment in `CharacterDef.cs` asks when buttons are added. An inspector move bound to Ultimate with the `ultimate` flag ticked is skipped until the bar is full.

### D-33 Tests may reference LD.Match (Default)
The EditMode test assembly references LD.Match so `AITests` can play AI vs AI matches. That's a test-only dependency; the game's dependency rules (`Sim <- Characters <- Match <- Net`) are unchanged.

### D-34 `facing` and the current move are now in the desync hash (Default)
The original `GameSim.Hash()` left them out, although both change what happens next. The move is hashed by its numbers, not by `name.GetHashCode()`, because string hashes are not guaranteed to match between the Editor (Mono) and WebGL (IL2CPP).

### D-35 Held keys keep repeating moves; only the Ultimate and dashes need a fresh press (Default; partly replaced by D-42 and D-43)
As before, holding J repeats jabs. The Ultimate needs a fresh press so holding O can't fire it the moment the bar fills, and each dash tap must be a fresh press.

### D-36 Generate a character's Animator only when its art work starts (Default)
`WorldView` draws a character's View Prefab instead of its coloured placeholder box. A freshly generated prefab has no sprites, so running **Fight > Create Animator for Selected Character** too early makes the character invisible (only the debug hurtbox shows). For now the six roster characters get a Code Character Art asset with a distinct `Placeholder Color`, and the Animator is generated when an artist picks the character up.

### D-37 Merge with main (2026-10-17): network Version 7 (Default)
Main bumped `Version` to "6" for player names, and the shared kit also bumped it to "6" on its own branch. The merge is "7", so a build with only one of the two changes can never match a build with both and desync.

### D-38 The kit HUD is in the Canvas HUD too (Decided)
Main's Canvas UI (`HudScreen`, in `Menu.unity`) switches the OnGUI `FightView` off, which hid the meter, round pips, banners and Ultimate flash in the real game. `HudScreen` now shows them as well, through optional slots. `BuildFightCanvas` creates them, and **Fight > Add Meter And Round HUD** adds them to a FightUI that already exists, without touching the rest. The round banner is worked out from `sim.round` rather than events, because the HUD screen only switches on after the match has started.

### D-39 A landed grab gets the same juice as a hit (Decided)
`JuiceManager` handles the `Grab` event like `Hit` (sparks and screen shake). A grab-specific effect can replace it later.

## Real designs from the Moveset Tracker (2026-10-17)

These come from the team's "Character Moveset - Moveset Tracker" sheet, plus the answers to the planner's questions about it. The per-character results are in [Characters/](Characters/). The build order is in [ROSTER_PLAN.md](ROSTER_PLAN.md).

### D-40 The roster gets real designs, with the sheet's names (Decided)
Replaces D-29. Seven characters: **Ruy** (Shoto, id 10), **Paintress** (Rushdown, 11), **Noir** (Zoner, 12), **Room** (Grappler, 13), **The Executioner** (Trapper, 14), **Dr. Remy** (Mixup, 15) and the new **K3N** (Powerup, **16**).
- Ruy takes over id 10, which main had renamed from ArcadeGuy to `Samurai`. It's the same Shoto slot and was still a placeholder.
- The sheet's names are used in the design docs and as the **in-game display names** (`CharacterList`). The **C# class names stay** (`Samurai`, `Painter`, `Cowboy`, `Elephant`, `Executioner`, `PlagueDoctor`), to avoid churn and merge conflicts. K3N gets a new class `K3N`.
- *Considered:* renaming the classes too (more churn for nothing the player sees), or keeping Samurai and giving Ruy a new id.

### D-41 The sheet's numbers: HP as written, movement speed as a % of walk speed (Decided)
- HP is used as written (60 to 200), and tuned after playtests.
- "Movement Speed" is a **percentage of the standard walk speed (60)**: 75 → 45, 150 → 90. Dashes are set separately from the Dash columns, and jumps stay standard.
- The sheet gives no frame data, so every frame number in the character docs is a planner's proposal to tune.

### D-42 Charge moves: hold to charge, release to fire (Decided)
Ruy's Hadoken, K3N's Charged Punch and the Executioner's Charged Whip charge while their button is held, and the move comes out on release, or by itself at the cap (60 ticks, proposed). A tap gives the weakest version. The same generic extension covers:
- **hold to repeat** (K3N's Rapid Punches keep going while I is held, up to a cap)
- **tap vs hold** (Dr. Remy: a release before 15 ticks switches potion, and reaching 15 uses it)

The charge state lives on `Fighter` and is hashed. This replaces D-35 for these moves only: other moves still repeat while held.
- *Considered:* fixed slow windups with no holding (simplest), or two fixed versions (tap or full).

### D-43 Every move has a cooldown, for every character (Decided)
After a move ends, **that same move** can't be used again for its cooldown. Other moves are free. Standard values: **Light 5, Heavy 10, Grab 15, Special 30 ticks; dashes 20**. The heavy characters (Room, Executioner) have a 60-tick dash cooldown. Ultimates have none (the meter is their limit).
- Characters change cooldowns through a hook (`ModifyCooldown`), not by editing shared `MoveDef`s (D-25). K3N's perk shrinks them as he's hurt, his Install sets them to 0, and Noir's Ultimate cuts the Special's.
- This changes **every character, the test characters included**, so all existing tests and the soak test need checking, `Version` gets bumped, and the AI must not press buttons on cooldown.
- *Considered:* one global cooldown after any attack, or cooldowns per button group.
- Asked for by the team so "attack recharge time" (K3N's perk 2) means something.

### D-44 Flavoured blocks are the standard block (Decided)
Noir's lasso spin, Paintress's arms, Dr. Remy's cane spin and K3N's Zeep Zorp are only the `Blockstun` animation. **Room is the exception: he can't block at all** (a `CanBlock` hook).

### D-45 Old Tech and era are TBD for every character (Decided)
The sheet has neither. Each doc lists "Old Tech" as its first open question, and nothing is invented. D-13 (the Old Tech lives in the Ultimate) still stands, but two Ultimates are now buffs (Noir's Rapid Fire, K3N's Install), so the team may want the Old Tech elsewhere for them.

### D-46 Status effects, as used by the designs (Decided; built with the Status effects extension, D-24)
Every status lives on the victim:
- **Slow:** a single status. A new slow only replaces the current one if it lasts longer; slows don't stack. Sources: Executioner traps and Dismember, Dr. Remy's slow potion.
- **Bleed:** stacks up to 5, from Executioner whip and trap hits.
- **Scramble:** from Paintress's Ultimate.
- **Leech:** from Dr. Remy's Grab.

Damage over time can't KO (D-23). Room ignores statuses (slow, dismember, scramble, paint slow) but still takes hitstun and knockback.

### D-47 No random chance in perks; seeded rolls only where a design needs them (Decided)
The Executioner's "percentage chance to dismember" became **every 3rd landed hit**: players can see it coming, and there's nothing to keep in sync. Two things still need a pseudo-random pick, and both use a roll made from the match seed and the tick (ints only, same on both machines, hashed): Paintress's control shuffle, and the placement of Room's copies. Room's copies were simplified to **fixed spots** instead.

### D-48 K3N's Install replaces his Ultimate (Decided)
His perk 1 turns the O button into a toggle. It starts with **any meter above 0**, with **no superflash**, and drains the meter until it's empty or he presses O again. While it's on: +30% damage, no cooldowns. He has no full-bar Ultimate, so he's the exception to D-09.

### D-49 Build the designs in layers, cut from the top (Decided)
Pass 1: every character's stats, normals, dashes and Special. Pass 2: Ultimates. Pass 3: perks. Each engine feature is built as a generic kit extension the first time a pass needs it. The riskiest features (Room's copies, the reflect) come last in their pass, so they can be cut. See [ROSTER_PLAN.md](ROSTER_PLAN.md).

## Building the roster (2026-10-17)

Answers to the open questions in each character doc: the team accepted every planner proposal listed there (Ruy, K3N, Noir, Room, Executioner, Paintress, Dr. Remy), except Dr. Remy's heal, which got its own cooldown (D-53). The docs now say "(confirmed)" for each one. The decisions below were made while building.

### D-50 Ultimate projectiles can't be reflected or destroyed (Decided for reflect, Default for destroy)
Paintress's canvas can't reflect them (confirmed). Room's Vine Boom doesn't destroy them either, so one rule covers both (Remy's rat waves and Paintress's splat always arrive). Traps and the Guillotine aren't projectiles (they don't move), so neither can touch them.

### D-51 What "cooldown N" means (Default)
The move waits exactly N ticks longer than it would with no cooldown. `Fighter.CooldownLeft` is "ticks until usable", so it reads N + 1 the moment the move ends. Cooldowns pause during hit stop and the superflash, like everything else. Air attacks have no cooldown (they're once per jump), and Ultimates have none (the meter is their limit).

### D-52 Traps can't be blocked (Default)
A trap on the floor is stepped on, not swung at, so blocking makes no sense. The Guillotine is unblockable by design.

### D-53 Dr. Remy's potions share a cooldown; heal has its own long one (Decided: heal. Default: sharing)
Using any potion puts the other two on (at least) the 30-tick Special cooldown, so he can't chain poison into slow. The heal potion has its own 300-tick cooldown (the team's answer to heal stalling). Only one poison cloud at a time: a new one replaces the old.

### D-54 Room's boxes reach further forward (Default)
His body is wider (1.4 units, confirmed), so the docs' boxes, written for the standard 1-unit body, couldn't reach a touching opponent. Every forward box got +200 to +300 of reach (`Elephant.cs`); the damage and frame data are unchanged.

### D-55 K3N's Rapid Punches knockback is 3, not 8 (Default)
At 8, ten punches pushed the victim out of reach by the 7th hit, so the held string couldn't connect. Found by `K3NRapidPunchesKeepTheVictimInHitstun`.

### D-56 K3N can flip Install whenever he could act (Default)
Idle, walking, attacking, dashing or jumping, but not while stunned, held or knocked out. Turning it on clears his running move cooldowns ("no cooldowns while it's on").

### D-57 What Room's copies copy (Decided, with one Default)
They copy walking, Light, Heavy and Vine Boom (confirmed). They don't jump (Default: the design only says "mimic movement", and copies standing still in the air would look wrong).

### D-58 Placeholder art is drawn in code (Decided)
`PlaceholderArt` (View) gives each character its own colour, pattern and letter, and each projectile/trap visual key a shape. Real art needs no code: a Code Character Art asset with a sprite or prefab replaces it. Single sprites are tinted by state unless the look's **Tint By State** is unticked (Decided).

### D-59 Network Version 10 (Decided; replaced by D-62)
Everything in the roster plan changes the sim rules, so `RelayLockstepSession.Version` is "10" and older builds are refused.

### D-60 The AI reads read-only hints from the fighter (Default)
The AI stays generic (it never names a character), so a fighter tells it what it can't work out on its own: `UltimateIsToggle` (K3N: Install goes on up close with 400+ meter, off when far away to save the meter), `WantsSpecialSwitch` (Dr. Remy: tap to switch to Heal when below half health and it's ready, Poison up close, Slow at range) and `SpecialHoldTicks` (Dr. Remy: hold past the tap window to use a potion). They only read the sim and never change the fight, so they aren't hashed. Tested in `AITests`.

### D-61 The online two-build match is left to the team (Decided)
The roster plan asks for one online match between two builds after each pass. The team chose to run it themselves instead of adding a development-build-only command-line hook. The local relay passes its own smoke test (`npm test`), and the Editor-side lockstep is unchanged apart from the new sim rules, which the soak test covers for determinism.

## Merge with main's air dash (2026-10-18)

### D-62 Air dash meets the roster rules; network Version 11 (Decided)
Main added `Buttons.Dash` (Left Shift + a direction), an air dash once per jump (`AirDash`), and Space as the default jump key. Main also bumped `Version` to "10" on its own, the same number as D-59, so the merge is **"11"**: a build with only one of the two changes can never match a build with both (the same reasoning as D-37). How the two meet:
- The air dash **ignores the dash cooldown** (once per jump is its limit) but **starts it**, so you can't land and ground-dash straight away.
- **Slows** cut the air dash's speed like any dash.
- **Dash perks** fire on air dashes too: Paintress's forward air dash is invincible and her back air dash costs 8 HP; Room's forward air dash is a Stampede hit.
- **Room and The Executioner** get a short air dash: 12 ticks at speed 70 (0.84 units; the standard is 1.3).
- Air attacks still respect move cooldowns.

Tests: `AirDashIgnoresTheDashCooldownButStartsIt`, `AirDashIsSlowedLikeAnyDash` (KitExtensionTests), `RoomAndTheExecutionerHaveAShortAirDash`, `PaintressDashPerksApplyToAirDashesToo` (RosterTests), plus main's own air dash tests in KitTests.
