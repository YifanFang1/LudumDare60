# Making characters

This guide is for anyone adding a fighter. Read [ARCHITECTURE.md](../ARCHITECTURE.md) first for the big picture and the "no-code" inspector workflow.

## Two ways to make a character

| | Inspector character (no code) | Code character |
|---|---|---|
| What it is | A `CharacterData` asset in `Assets/Resources/Characters/` | A C# class that extends `BaseFighter` |
| Use it when | The character is only stats, a body and moves | It needs a rule data can't express: a meter, a cooldown, a projectile limit, any Old Tech(nique) glitch |
| Tuning | Sliders in the inspector, live preview | Edit numbers in the `.cs` file |
| Balance updates through the server | Yes (`Fight > Export Characters to Server File`) | No, it needs a new build |
| Id | Random, generated automatically | A small fixed number you pick (0-999) |

**Rule of thumb:** start with an inspector character. Switch to code when you hit something the inspector can't do.

## How the classes fit together

```
Fighter                  (Sim/Fighter.cs) the engine. Walking, blocking, hitstun, KO, the state machine.
 ├─ BaseFighter          (Characters/BaseFighter.cs) THE TEMPLATE. Default traits, J/K controls, move helpers.
 │   ├─ Brawler          id 0, close range test character
 │   ├─ Shooter          id 1, long range test character
 │   ├─ Sandbag          id 2, training dummy (does nothing, never dies)
 │   └─ <your character>
 └─ DataFighter          plays inspector characters (CharacterData assets)
```

- **Fighter** is shared by everyone. Change it only for rules that should apply to every character, such as a new state like jumping. Changing it changes the sim, so bump `Version` in [RelayLockstepSession.cs](../Net/RelayLockstepSession.cs).
- **BaseFighter** is the template. It gives a character defaults for every trait, so a subclass only writes what's different.
- **Your character** overrides a few properties. Brawler and Shooter are about 30 lines each.

### What every character gets for free (from `Fighter`)
- Walking left and right (A/D), always turning to face the opponent.
- **Blocking:** hold the direction *away* from the opponent while standing or walking. A blocked hit does 0 damage and still pushes you back. It also puts you in **Blockstun** for 60% of the move's hitstun: you can't act, but you keep blocking while you hold back. You can't block during your own attack or while in hitstun. The numbers are `BlockstunPercent` and `BlockPushPercent` in `Fighter.cs`.
- Hitstun, knockback, KO, and hitbox/hurtbox collisions.

### The template's traits (from `BaseFighter`)

| Trait | Default | Override |
|---|---|---|
| Health | 100 | `MaxHp` |
| Walk speed | 60 milli-units per tick (3.6 units per second) | `WalkSpeed` |
| Body (hurtbox) | 1 unit wide, 2 units tall | `Hurtbox` |
| Light attack (J) | none | `LightAttack` |
| Heavy attack (K) | none | `HeavyAttack` |
| When a move is allowed | always | `CanUseMove(sim, move)` |
| Controls | K beats J if both are pressed | `ChooseMove(sim, input)` (rarely needed) |

## Make a new code character, step by step

1. **Copy a character.** Duplicate `Brawler.cs` for melee or `Shooter.cs` for ranged, inside `Assets/Scripts/Characters/`. Rename the file and the class, e.g. `Knight.cs` / `class Knight : BaseFighter`. The class name must match the file name.
2. **Pick an id.** Set `public const int Id = 2;` using the next free number in the list at the top of [CharacterList.cs](CharacterList.cs). Never reuse or change an id once it has been pushed: it is what gets saved and sent over the network.
3. **Register it.** Add one line to the static constructor in `CharacterList.cs`, then add your id to the comment list above it:
   ```csharp
   Add(Knight.Id, "Knight", delegate { return new Knight(); }, null);
   ```
4. **Set the traits.** Override only what differs from the template table above.
5. **Write the moves.** See [Writing moves](#writing-moves).
6. **Try it.** Press Play in `Fight.unity`, click **"You:"** in the lobby until it shows your character, then start a game vs the AI. Turn on the hitbox and hurtbox toggles (bottom left) to see the boxes.
7. **Add a test** if your character has a special rule. Copy one in [Assets/Tests/EditMode/CharacterTests.cs](../../Tests/EditMode/CharacterTests.cs). Run all tests from **Window > General > Test Runner > EditMode > Run All**.
8. **Update the roster table** in the root `CLAUDE.md`.

## Writing moves

Use the helpers from `BaseFighter` with **named arguments**, so every number says what it is. Store each move in a `static readonly` field and never change it while the game runs, because both players share the same object.

```csharp
static readonly MoveDef Jab = Melee("Light",
    totalTicks: 15, spawnTick: 4, activeTicks: 3,
    box: new Box(400, 900, 900, 400),
    damage: 6, hitstun: 14, knockback: 40);

static readonly MoveDef Fireball = Projectile("Heavy",
    totalTicks: 30, spawnTick: 12, lifetime: 180,
    box: new Box(400, 900, 500, 400),
    speed: 90, damage: 10, hitstun: 18, knockback: 60);
```

**Units:** distances are milli-units (1000 = 1 Unity unit; the arena runs from -8000 to 8000). Times are ticks (60 = 1 second).

| Argument | Meaning |
|---|---|
| `name` | The move's name. Also the Animator state the view plays, so keep `"Light"` / `"Heavy"` unless you have a reason not to. |
| `totalTicks` | How long the whole move lasts. You can't act until it's over. |
| `spawnTick` | The tick when the hitbox appears (the **startup**). Smaller is faster. |
| `activeTicks` (melee) | How long the hitbox stays out. |
| `lifetime` (projectile) | How long the projectile flies before it disappears. |
| `box` | `new Box(x, y, w, h)`: `x` = how far in front of your centre the near edge is, `y` = height above the feet, `w`/`h` = size. See [Box.cs](../Sim/Box.cs). |
| `speed` | Projectile speed in milli-units per tick. |
| `damage` | Health removed (0 if blocked). |
| `hitstun` | Ticks the victim can't act. Blockstun is 60% of this. |
| `knockback` | Push speed in milli-units per tick for the whole hitstun or blockstun. |

**Recovery** = `totalTicks - spawnTick - activeTicks`. A fast, short move has small numbers for both. A strong move should have long startup or recovery so it can be punished.

Need more than one hitbox (a two-hit combo, a sweep that grows)? Build the `MoveDef` by hand like the helpers do, with several `HitboxDef`s at different `spawnTick`s.

## Adding special rules

| You want... | Do this |
|---|---|
| A move that's only usable sometimes | Override `CanUseMove`. See the one-fireball rule in `Shooter.cs`. `ProjectilesOnScreen(sim)` is there for you. |
| Something that happens every tick (regen, a timer) | Override `OnTick(sim)`. |
| To react to landing or taking a hit | Override `OnHitDealt` / `OnHitReceived`. They also run for blocked hits; check `hit.blocked`. |
| Different controls (e.g. Down + J = low kick) | Override `ChooseMove(sim, input)` and test the bits from `Sim/Buttons.cs`. |
| To ignore or change the player's input (stun, reversed controls) | Override `FilterInput(input)`. Sandbag returns 0 to ignore everything. |
| A fighter that can't be KO'd | Override `CanBeKnockedOut` to return false; a lethal hit refills health instead. |
| A new button | Add a bit in `Sim/Buttons.cs` (keep the old values), then a `Make(...)` line in `FightControls`. |

**Extra state (meters, cooldowns, counters, stances) MUST be added to `HashExtra()`.** Otherwise online desyncs go unnoticed:

```csharp
int meter;      // 0-100
int cooldown;   // ticks until the special can be used again

public override int HashExtra() { return meter * 31 + cooldown; }
```

You don't need to reset this state between matches. A new `Fighter` object is created for every match, so field initialisers are enough.

## The rules (break these and online play desyncs)

The `Characters` folder is part of the deterministic simulation:
- **Ints only.** No `float`, `double`, `Time`, `UnityEngine.Random`, `System.Random` or Unity physics. The asmdef blocks `UnityEngine` entirely.
- Change state only inside your overrides (they are called from `GameSim.Step`). `ChooseMove` and `CanUseMove` must only *read* the sim.
- Sounds, particles and screen shake go in the View layer and react to `SimEvent`s (`AttackStart`, `Hit`, `Block`, `KO`).
- Changing `Fighter` or `GameSim` rules means bumping `Version` in `RelayLockstepSession.cs`. Changing only your own character's numbers or code doesn't, as long as everyone plays the same build.

## The test characters

These exist to test the framework. They are not final roster characters.

| | Brawler (id 0) | Shooter (id 1) | Sandbag (id 2) |
|---|---|---|---|
| Role | Close range | Long range (zoner) | Training dummy |
| Health | 110 | 90 | 100, **refills instead of being KO'd** |
| Walk speed | 70 | 50 | can't move |
| Body | 1.2 wide x 1.9 tall | 0.8 wide x 2.1 tall | 1 wide x 2 tall (template) |
| Light (J) | Jab: startup 4, 6 dmg, 15 ticks total | Poke: startup 3, 4 dmg, 13 ticks total | none |
| Heavy (K) | Haymaker: startup 10, 14 dmg, long reach, 28 ticks | Fireball: startup 12, 10 dmg, speed 90, 30 ticks; **one on screen at a time** | none |

**Testing reach and damage:** in the lobby set **"Opponent:"** to Sandbag. It ignores all input (it never blocks either), and the match never ends.

Brawler is also the fallback for unknown ids, so never delete it.

## What characters look like

### Seeing hitboxes and hurtboxes
During a match there's a **"Debug boxes"** panel in the **bottom-left corner** with two checkboxes:
- **Hurtboxes (green: bodies):** the area where a fighter can be hit. With no art, this is the coloured box itself.
- **Hitboxes (yellow: attacks):** a hitbox exists only while an attack is *active*, which is usually 3-4 ticks, so it shows up as a short yellow flash. Projectiles stay yellow while they fly.

Both start on. The defaults are `Show Hurtboxes` / `Show Hitboxes` on the `WorldView` component in `Fight.unity`. Untick `Show Toggles` there to hide the panel, for example in a release build. Outside Play mode, use `CharacterPreview` (see ARCHITECTURE.md) to scrub through a move tick by tick.

### Placeholder boxes
A character with no art is a **coloured box** the size of its hurtbox: blue for player 1 and red for player 2 (or the art asset's `Placeholder Color`). It turns lighter when attacking, darker in hitstun, cyan in blockstun and grey when KO'd. You can tell Brawler (wide) and Shooter (tall, thin) apart by shape.

## Adding art and animation

Art never changes the fight: the sim decides where a fighter is and what it's doing, and the art just shows it. So artists can work without touching any code. **Art always faces RIGHT**; the game flips it for the player on the right.

**Where the art goes:**
- **Code characters** (Brawler, Shooter, Sandbag, any `BaseFighter`): a **Code Character Art** asset. One per character.
- **Inspector characters:** the art fields on the `CharacterData` asset itself (same fields, same steps from step 2).

### 1. Make the art asset (code characters only)
Project window > right-click `Assets/Resources/Characters` > **Create > Fight > Code Character Art**. Name it e.g. `Brawler Art`, then pick the character in its **Character** dropdown. It has to be in `Resources/Characters` or the game won't find it.

### 2a. A single picture (quickest)
Drag a sprite (facing right) into **Sprite**. Press Play, pick the character, tick **Hurtboxes** in the Debug boxes panel, and drag **Sprite Offset X/Y** and **Sprite Scale** until the picture lines up with the green box. The sliders work while the game runs. (Your values stay after you stop, because they're saved on the asset.)

### 2b. Animated
1. Select the art asset (or `CharacterData`) and run the menu **Fight > Create Animator for Selected Character**. It creates, in `Assets/Art/<Name>/`:
   - `<Name>.controller`: an Animator with one state for every pose the game asks for.
   - `<Name>_<State>.anim`: one empty clip per state.
   - `<Name>_View.prefab`: an Animator plus a child **Art** sprite, already plugged into the asset's **View Prefab** slot.
2. Open the prefab, select it, open **Window > Animation > Animation**, pick a clip from the dropdown in the top-left and drag your sprite frames onto the timeline.
3. Line it up with **Sprite Offset / Scale** exactly as in 2a.

Run the menu again after adding a move: it only adds the missing states and clips, and never overwrites existing ones.

**The states the game plays** (the names must match exactly; the tool makes them for you):

| State | When | How it's played |
|---|---|---|
| `Idle` | Standing still | Loops every `Idle Loop Ticks` (60 = 1 s) |
| `Walk` | Walking (forward or back) | Loops every `Walk Loop Ticks` |
| `Light`, `Heavy`, ... | One per move, named like the move | Stretched over the move's length |
| `Hitstun` | Just got hit | Stretched over the hitstun |
| `Blockstun` | Just blocked | Stretched over the blockstun |
| `KO` | Knocked out | Plays over 1 second, then holds the last frame |

The **sim controls the frame**, not time: an attack clip is scrubbed over the move, so if the hitbox comes out on tick 4 of a 15-tick move, the frame 4/15 of the way through the clip is the one on screen when it hits. Make that your "impact" frame. The clip's length in seconds doesn't matter. A missing state shows `Idle` instead, so a half-finished Animator still works.

Your own prefab works too, as long as it has an Animator with these state names. Run the menu on it to add any states it's missing.

## Checklist before you push a new character
- [ ] Unique id, registered in `CharacterList.cs` and listed in its comment.
- [ ] Ints only; any extra state is in `HashExtra()`.
- [ ] Tested vs the AI in `Fight.unity`; EditMode tests pass.
- [ ] Roster table in `CLAUDE.md` updated.
- [ ] `.meta` files committed with any new files.
- [ ] (Optional) a Code Character Art asset in `Resources/Characters`, so artists have a slot to fill.
