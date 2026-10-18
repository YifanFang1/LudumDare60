# Making characters


This guide is for anyone adding a fighter. Read [ARCHITECTURE.md](../ARCHITECTURE.md) first for the big picture and the "no-code" inspector workflow.


## Two ways to make a character


| | Inspector character (no code) | Code character |
|---|---|---|
| What it is | A `CharacterData` asset in `Assets/Characters/<Name>/Resources/` | A C# class that extends `BaseFighter` |
| Use it when | The character is only stats, a body and moves | It needs a rule data can't express: a meter, a cooldown, a projectile limit, any Old Tech(nique) glitch |
| Tuning | Sliders in the inspector, live preview | Edit numbers in the `.cs` file |
| Balance updates through the server | Yes (`Fight > Export Characters to Server File`) | No, it needs a new build |
| Id | Random, generated automatically | A small fixed number you pick (0-999) |


**Rule of thumb:** start with an inspector character. Switch to code when you hit something the inspector can't do.


## How the classes fit together


```
Fighter                  (Sim/Fighter.cs) the engine. Walking, blocking, dashing, jumping, meter, hitstun, KO, the state machine.
 ├─ BaseFighter          (Characters/BaseFighter.cs) THE TEMPLATE. The standard moveset, controls, move helpers.
 │   ├─ Samurai ... PlagueDoctor   ids 10-15, the launch roster. Placeholders: the standard moveset, unchanged
 │   ├─ Brawler          id 0, close range test character
 │   ├─ Shooter          id 1, long range test character
 │   ├─ Sandbag          id 2, training dummy (does nothing, never dies)
 │   └─ <your character>
 └─ DataFighter          plays inspector characters (CharacterData assets)
```


- **Fighter** is shared by everyone. Change it only for rules that should apply to every character, such as a new state like jumping. Changing it changes the sim, so bump `Version` in [RelayLockstepSession.cs](../Global/Scripts/Net/RelayLockstepSession.cs).
- **BaseFighter** is the template. It gives a character defaults for every trait, and its moves default to the **standard moveset** ([Design/SHARED_KIT.md](../../Design/SHARED_KIT.md)), so a subclass only writes what's different. A subclass that overrides nothing is already fully playable: that's what the six roster placeholders (`Samurai.cs` etc.) are.
- **Your character** overrides a few properties. Brawler and Shooter are about 30 lines each.


### What every character gets for free (from `Fighter` and `GameSim`)
- Walking left and right (A/D), always turning to face the opponent. Fighters can't walk through each other (pushboxes).
- **Blocking:** hold the direction *away* from the opponent while standing or walking. A blocked hit does 0 damage and still pushes you back. It also puts you in **Blockstun** for 60% of the move's hitstun: you can't act, but you keep blocking while you hold back. You can't block during your own attack, a dash or hitstun. The numbers are `BlockstunPercent` and `BlockPushPercent` in `Fighter.cs`.
- **Dashing:** double-tap a direction (two fresh presses within 12 ticks). Toward the opponent is a forward dash. Override `ForwardDash` / `BackDash` to change them.
- **Jumping:** hold W (alone for straight up, with A/D to jump that way). Holding A/D in the air steers a little (`AirControl`), you can't block in the air, and you get one air attack per jump. Bodies don't push while someone is airborne, so you can jump over the opponent. Grabs can't catch an airborne fighter. Override `JumpArc` to change the jump.
- **Grab rules and back throws:** any hitbox with `grab` set ignores blocking but can't catch a stunned fighter; hold back when it lands to throw the victim behind you.
- **The Ultimate meter:** fills from hits (dealt, taken and blocked). A move with `ultimate` set needs a full bar, spends it and freezes the match for a moment.
- Hitstun, knockback, KO, best-of-3 rounds, and hitbox/hurtbox collisions.


### The template's traits (from `BaseFighter`)


| Trait | Default | Override |
|---|---|---|
| Health | 100 | `MaxHp` |
| Walk speed | 60 milli-units per tick (3.6 units per second) | `WalkSpeed` |
| Body (hurtbox) | 1 unit wide, 2 units tall | `Hurtbox` |
| Light (J) | `StandardLight`: startup 4, 5 dmg | `LightAttack` |
| Heavy (K) | `StandardHeavy`: startup 10, 12 dmg, long reach | `HeavyAttack` |
| Grab (L) | `StandardGrab`: startup 5, 12 dmg, short reach, 40 ticks if it whiffs | `GrabMove` |
| Special (I) | `StandardSpecial`: a projectile, 10 dmg, one on screen | `SpecialMove` |
| Ultimate (O) | `StandardUltimate`: 30 dmg strike, needs a full meter | `UltimateMove` |
| Jump + J | `StandardJumpLight`: quick air kick, 5 dmg | `JumpLightAttack` |
| Jump + K | `StandardJumpHeavy`: big jump-in, 10 dmg | `JumpHeavyAttack` |
| Dashes | about 1.8 units forward and back | `ForwardDash` / `BackDash` |
| Jump | about 2.1 units high, 2 units across, 40 ticks | `JumpArc` |
| Air control | a little steering with A/D in the air | `AirControl` (0 = fixed arc) |
| When a move is allowed | always, except one Special projectile at a time | `CanUseMove(sim, move)`: call `base.CanUseMove` for moves you don't handle |
| Controls | Ultimate > Special > Grab > Heavy > Light if pressed together | `ChooseMove(sim, input)` (rarely needed) |

Return `null` from a move slot to take that move away (e.g. a character with no projectile).


## Make a new code character, step by step


1. **Copy a character.** Duplicate `Brawler.cs` for melee or `Shooter.cs` for ranged, inside `Assets/Characters/Scripts/Fighters/`. Rename the file and the class, e.g. `Knight.cs` / `class Knight : BaseFighter`. The class name must match the file name.
2. **Pick an id.** Set `public const int Id = 16;` using the next free number in the list at the top of [CharacterList.cs](Scripts/Fighters/CharacterList.cs) (3-9 are for test characters, 16 and up for new roster characters). Never reuse or change an id once it has been pushed: it is what gets saved and sent over the network.
3. **Register it.** Add one line to the static constructor in `CharacterList.cs`, then add your id to the comment list above it:
   ```csharp
   Add(Knight.Id, "Knight", delegate { return new Knight(); }, null);
   ```
4. **Set the traits.** Override only what differs from the template table above.
5. **Write the moves.** See [Writing moves](#writing-moves).
6. **Try it.** Press Play in `MenuScreen/Menu.unity`, click **"You:"** in the lobby until it shows your character, then start a game vs the AI. Turn on the hitbox and hurtbox toggles (bottom left) to see the boxes.
7. **Add a test** if your character has a special rule. Copy one in [Assets/Tests/EditMode/CharacterTests.cs](../Tests/EditMode/CharacterTests.cs). Run all tests from **Window > General > Test Runner > EditMode > Run All**.
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
| `name` | The move's name. Also the Animator state the view plays, so keep the slot names (`"Light"`, `"Heavy"`, `"Grab"`, `"Special"`, `"Ultimate"`) unless you have a reason not to. |
| `totalTicks` | How long the whole move lasts. You can't act until it's over. |
| `spawnTick` | The tick when the hitbox appears (the **startup**). Smaller is faster. |
| `activeTicks` (melee) | How long the hitbox stays out. |
| `lifetime` (projectile) | How long the projectile flies before it disappears. |
| `box` | `new Box(x, y, w, h)`: `x` = how far in front of your centre the near edge is, `y` = height above the feet, `w`/`h` = size. See [Box.cs](../Global/Scripts/Sim/Box.cs). |
| `speed` | Projectile speed in milli-units per tick. |
| `damage` | Health removed (0 if blocked). |
| `hitstun` | Ticks the victim can't act. Blockstun is 60% of this. |
| `knockback` | Push speed in milli-units per tick for the whole hitstun or blockstun. |


**More helpers:** `Grab(...)` takes the same arguments as `Melee` and makes a grab. `Ultimate(...)` does the same for an Ultimate (its startup counts from the end of the freeze). `Projectile(...)` takes an optional `visual:` name, which is the key for the projectile's sprite in the character's art asset (**Projectile Sprites**). The standard Special uses `"special"`.

**Recovery** = `totalTicks - spawnTick - activeTicks`. A fast, short move has small numbers for both. A strong move should have long startup or recovery so it can be punished.


Need more than one hitbox (a two-hit combo, a sweep that grows)? Build the `MoveDef` by hand like the helpers do, with several `HitboxDef`s at different `spawnTick`s.


## Adding special rules


| You want... | Do this |
|---|---|
| A move that's only usable sometimes | Override `CanUseMove`. See the one-fireball rule in `Shooter.cs`, and end with `return base.CanUseMove(sim, move);` so the template's rules still apply. `ProjectilesOnScreen(sim)` is there for you. |
| Something that happens every tick (regen, a timer) | Override `OnTick(sim)`. |
| To react to landing or taking a hit | Override `OnHitDealt` / `OnHitReceived`. They also run for blocked hits; check `hit.blocked`. |
| Different controls (e.g. Down + J = low kick) | Override `ChooseMove(sim, input)` and test the bits from `Sim/Buttons.cs`. |
| To ignore or change the player's input (stun, reversed controls) | Override `FilterInput(input)`. Sandbag returns 0 to ignore everything. |
| A fighter that can't be KO'd | Override `CanBeKnockedOut` to return false; a lethal hit refills health instead. |
| A new button | Add a bit in `Sim/Buttons.cs` (keep the old values), then a `Make(...)` line in `FightControls`. For inspector characters, also add it at the end of `AttackButton` in `CharacterDef.cs`. |
| An engine rule the kit doesn't have yet (cancels, armor, poison, teleports...) | Check the extensions list in [Design/SHARED_KIT.md](../../Design/SHARED_KIT.md#extensions-build-when-a-design-needs-them): build it as a generic hook with a do-nothing default, plus a test in `KitTests.cs`. Never write `if (this is MyCharacter)` in `Fighter` or `GameSim`. |


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


Brawler and Shooter also get the standard Grab, Special, Ultimate and dashes from the template. Shooter's Special is a second projectile, and it shares the one-on-screen limit with the Fireball.

**Testing reach and damage:** in the lobby set **"Opponent:"** to Sandbag. It ignores all input (it never blocks either), and the match never ends.


Brawler is also the fallback for unknown ids, so never delete it.


## What characters look like


### Seeing hitboxes and hurtboxes
During a match there's a **"Debug boxes"** panel in the **bottom-left corner** with two checkboxes:
- **Hurtboxes (green: bodies):** the area where a fighter can be hit. With no art, this is the coloured box itself.
- **Hitboxes (yellow: attacks):** a hitbox exists only while an attack is *active*, which is usually 3-4 ticks, so it shows up as a short yellow flash. Projectiles stay yellow while they fly.


Both start on. The defaults are `Show Hurtboxes` / `Show Hitboxes` on the `WorldView` component in `MenuScreen/Menu.unity`. Untick `Show Toggles` there to hide the panel, for example in a release build. Outside Play mode, use `CharacterPreview` (see [ARCHITECTURE.md](../ARCHITECTURE.md)) to scrub through a move tick by tick.


### Placeholder boxes
A character with no art is a **coloured box** the size of its hurtbox: blue for player 1 and red for player 2 (or the art asset's `Placeholder Color`). It turns lighter when attacking, darker in hitstun, cyan in blockstun and grey when KO'd. You can tell Brawler (wide) and Shooter (tall, thin) apart by shape.


## Adding art and animation


Art never changes the fight: the sim decides where a fighter is and what it's doing, and the art just shows it. So artists can work without touching any code. **Art always faces RIGHT**; the game flips it for the player on the right.


**Where the art goes:**
- **Code characters** (Brawler, Shooter, Sandbag, any `BaseFighter`): a **Code Character Art** asset. One per character.
- **Inspector characters:** the art fields on the `CharacterData` asset itself (same fields, same steps from step 2).


### 0. Prepare and import the picture
1. **Cut it out.** The game draws the whole image, so a JPG with a background shows up as a rectangle. Remove the background (remove.bg, Photopea, GIMP), crop tight around the character, and save as a **PNG** with transparency. Make sure it faces RIGHT.
2. **Put it in its own folder**, e.g. `Assets/Characters/<Name>/<name>_idle.png`. One folder per character keeps artists out of each other's files.
3. **Import settings.** Select the PNG and set these in the Inspector, then click Apply:
   - **Texture Type:** Sprite (2D and UI). **Sprite Mode:** Single, or Multiple for a sprite sheet (slice it in the Sprite Editor).
   - **Pivot:** Bottom, or a Custom pivot between the feet. The game puts the pivot on the fighter's feet, so this keeps Sprite Offset Y near 0.
   - **Pixels Per Unit:** the character's height in pixels ÷ the hurtbox height in units. For a 260 px tall character on a 1.9-unit body that's 260 / 1.9 ≈ 137. Then Sprite Scale stays near 1 and only fine-tunes.
   - **Filter Mode:** Point for pixel art, Bilinear for photos and painted art. **Compression:** None or High Quality (characters are small and on screen all the time).
4. Commit the PNG together with its `.meta` file.


### 1. Make the art asset (code characters only)
Project window > right-click `Assets/Characters/<Name>/Resources` (make the folders if needed) > **Create > Fight > Code Character Art**. Name it e.g. `Brawler Art`, then pick the character in its **Character** dropdown. It has to be in a folder called `Resources` or the game won't find it.


### 2a. A single picture (quickest)
Drag a sprite (facing right) into **Sprite**. Press Play, pick the character, tick **Hurtboxes** in the Debug boxes panel, and drag **Sprite Offset X/Y** and **Sprite Scale** until the picture lines up with the green box. The sliders work while the game runs. (Your values stay after you stop, because they're saved on the asset.)


### 2b. Animated
1. Select the art asset (or `CharacterData`) and run the menu **Fight > Create Animator for Selected Character**. It creates, in `Assets/Characters/<Name>/`:
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
| `DashForward`, `DashBack` | Dashing toward / away from the opponent | Stretched over the dash |
| `Jump` | In the air, not attacking | First half of the clip while rising, second half while falling |
| `Light`, `Heavy`, `Grab`, `Special`, `Ultimate`, `JumpLight`, `JumpHeavy`, ... | One per move, named like the move | Stretched over the move's length |
| `Hitstun` | Just got hit | Stretched over the hitstun |
| `Blockstun` | Just blocked | Stretched over the blockstun |
| `KO` | Knocked out | Plays over 1 second, then holds the last frame |


The **sim controls the frame**, not time: an attack clip is scrubbed over the move, so if the hitbox comes out on tick 4 of a 15-tick move, the frame 4/15 of the way through the clip is the one on screen when it hits. Make that your "impact" frame. The clip's length in seconds doesn't matter. A missing state shows `Idle` instead, so a half-finished Animator still works.


Your own prefab works too, as long as it has an Animator with these state names. Run the menu on it to add any states it's missing.


## Sizing hurtboxes and hitboxes


**The boxes are the design; the art sells them.** Decide reach and body size from the character's role (rushdown, zoner, grappler...) first, then make the art fit. When the art can't be redrawn (photo cutouts, a fixed sprite), move the boxes toward the art, but never so far that the role breaks.


### Hurtbox (the body)
- **Slightly smaller than the art,** about 5-10% inside the silhouette. Getting hit by something that visibly missed feels unfair; whiffing at the very edge of the art feels fine.
- **Body size is a stat.** The template (1 x 2 units) is the baseline, and changes are a choice: a big body is easier to hit, which is a cost. A huge character (the elephant) should keep a huge hurtbox as its drawback.
- **Ignore loose parts:** trunks, capes, hair, a weapon at rest. Cover the torso and legs.
- **Engine limit:** each fighter has one fixed hurtbox, so it doesn't stretch into an attacking arm. You can't hit the tip of a long move, so long reach has to be paid for with startup and recovery.


### Hitboxes (the attacks)
- **Cover the striking part** (fist, weapon, trunk) on the impact frame, not the whole sprite. The far edge of the hitbox is where the art's tip is on that frame.
- **Start the near edge inside your own body** so the move can't whiff at point blank. Brawler's body runs from -600 to +600, and his hitboxes start at x = 400.
- **Measure reach past your body's front edge**, not from the centre. Brawler's Jab reaches 0.7 units past his body and his Haymaker 1.1. Comparing these numbers across the roster shows who wins at which distance.
- **Bigger box = slower or riskier.** Long reach or a large box should come with long startup or recovery. Fast, safe moves get small boxes.
- **Make the art hit when the box does:** put the impact frame at `spawnTick / totalTicks` through the attack clip (see the Animator section above).
- **Plan heights now,** even before jumping and crouching exist: highs at head height, mids at the torso, lows near the ground. Then high and low attacks won't need every box redone later.


### Tuning workflow
1. **Blockout first, art later.** Play the character as a coloured box against **Sandbag** until the ranges feel right.
2. **Test four distances and shapes:** point blank, max range (the tip), punishing after a blocked hit, and against **both body shapes**. Brawler is wide and Shooter is tall and thin, so a box tuned on one can whiff on the other.
3. **Add the art** and adjust Sprite Offset/Scale to fit the boxes, not the other way round.
4. **Prefer inspector characters for tuning:** they have live sliders, the `CharacterPreview` tick scrubber, and server-published balance. Code characters need a code edit and recompile for every tweak.


## Checklist before you push a new character
- [ ] Unique id, registered in `CharacterList.cs` and listed in its comment.
- [ ] Ints only; any extra state is in `HashExtra()`.
- [ ] Tested vs the AI in `MenuScreen/Menu.unity`; EditMode tests pass, including `SoakTests` (it adds new code characters to its pool by itself).
- [ ] Roster table in `CLAUDE.md` updated.
- [ ] `.meta` files committed with any new files.
- [ ] (Optional) a Code Character Art asset in `Assets/Characters/<Name>/Resources`, so artists have a slot to fill.
- [ ] Boxes checked against Sandbag at point blank and max range, and against both a wide and a thin body.



