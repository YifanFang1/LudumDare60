# LudumDare60: "Old Tech" fighting game


A side-on 1v1 fighting game in the style of Street Fighter, made by a team for the **Ludum Dare 60 Jam (72h)**. Theme: **OLD TECH**.
Players fight **online** (room codes through a Cloudflare relay) or **solo vs an AI**. The target build is **WebGL on itch.io**.


## The concept
- **Fighters from different eras.** Each character comes from a different time period (caveman, medieval, Victorian, steampunk, dieselpunk, 20th century...). Their attacks and weapons fit that era's technology.
- **Each fighter also has an "Old Tech(nique)".** This is a signature glitch or arcade trick used as a game mechanic, for example: clipping through walls or the floor, the Konami code, a Pac-Man kill-screen effect, a Mario BLJ, bhopping, rocket jumping, black-and-white western, PS1 low-poly.
- **Art styles differ per character.** There are many artists, and each one draws in their own style (pixel art, low-poly, photo cutouts, hand-drawn...). Code and tools must not assume one shared look. Art always faces RIGHT.


### Roster
| Character | Status | Notes |
|---|---|---|
| Samurai | In code, placeholder (id 10) | Shoto. Placeholder: standard moveset, design TBD by the team. |
| Painter | In code, placeholder (id 11) | Rushdown. Placeholder: standard moveset, design TBD by the team. |
| Black and White Cowboy | In code, placeholder (id 12) | Zoner. Placeholder: standard moveset, design TBD by the team. |
| Elephant | In code, placeholder (id 13) | Grappler ("Overpowered Elephant"). Placeholder: standard moveset, design TBD by the team. |
| Executioner | In code, placeholder (id 14) | Trapper. Placeholder: standard moveset, design TBD by the team. |
| Plague Doctor | In code, placeholder (id 15) | Mixup. Placeholder: standard moveset, design TBD by the team. |
| Brawler | In code | Close-range test character (id 0) and the fallback for unknown ids. Not a real roster character. |
| Shooter | In code | Long-range test character (id 1): jab and a one-on-screen fireball. Not a real roster character. |
| Sandbag | In code | Training dummy (id 2): ignores all input, refills health instead of being KO'd. |


The plan is 6 characters at launch, with more later. **The plan, the shared moveset and every design decision are in [Design/](Design/README.md)** (start with [DECISIONS.md](Design/DECISIONS.md) and [IMPLEMENTATION_PLAN.md](Design/IMPLEMENTATION_PLAN.md)). For now all six share one standard moveset; creative design (moves, looks, Old Tech) comes from the team.


**Idea pool (not committed):** steampunk British zoner whose ultimate runs the opponent over with a train · Salem witch (fireball, familiar) · plague doctor · Dr. Frankenstein (+ monster) · Vietnam soldier (rocket jump with self-stun, tree-disguise or bear-trap counters, matchstick projectile) · caveman (rock bash, spear, bow, fire) · medieval knight/executioner · torture-museum character (iron maiden, brazen bull) · samurai · Wall Street guy / oil tycoon / Henry Ford · printing press goons / typewriter "ink" guy (art is PNGs of a real guy posing) · philosopher whose words hurt · Sisyphus and his boulder · Little Boy vs Fat Man · Nokia-phone weapon · radio-possessing horrors · coal-mine canary · washing-machine weapon · puppet who briefly possesses the opponent.


**Archetypes to cover:** shoto, rushdown, zoner, grappler, knight, puppet, and an arcade character with an elephant "stand".


## Tech stack
- Unity **6000.6.3f1**, URP 2D renderer, the new Input System, plus the 2D Animation, Aseprite and PSD importers.
- Online play uses `NativeWebSocket` to connect to a **Cloudflare Worker + Durable Object** relay in [relay/](relay/) (JavaScript, `wrangler`).
- Main scene: [Assets/Scenes/Fight.unity](Assets/Scenes/Fight.unity).


**Read [Assets/Scripts/ARCHITECTURE.md](Assets/Scripts/ARCHITECTURE.md) before you change code.** It covers the assembly layout, the no-code character workflow, hitbox units and how characters get published. Keep it up to date when the architecture changes.


**To make a character, read [Assets/Scripts/Characters/CHARACTERS.md](Assets/Scripts/Characters/CHARACTERS.md).** Code characters extend the `BaseFighter` template.


**Art and hitboxes:** importing a sprite (cutout PNG, bottom pivot, Pixels Per Unit from the hurtbox height) and the rules for sizing hurtboxes and hitboxes are in [CHARACTERS.md](Assets/Scripts/Characters/CHARACTERS.md#adding-art-and-animation). Gameplay sets the boxes and the art is fitted to them. Hurtboxes sit slightly inside the art, and hitboxes cover only the striking part on the impact frame.


## Hard rules (break these and online play desyncs or the build breaks)
1. **The sim is deterministic.** In `Assets/Scripts/Sim` and `Assets/Scripts/Characters`:
   - Use ints only: no `float`, `Time`, `UnityEngine.Random`, Unity physics or `UnityEngine`.
   - Positions are in milli-units (1000 = 1 Unity unit). Time is in ticks (60 per second).
   - The game state changes only inside `GameSim.Step(in0, in1)`.
   - `LD.Sim` has "No Engine References" turned on, and that is on purpose. Don't turn it off.
2. **Only the dependencies allowed by the asmdefs**: `Sim <- Characters <- Match <- Net`, `CharacterData -> Characters`, `View -> IMatchSession + CharacterData`. Never add a reference that points the other way. Visuals and audio react to `SimEvent`s; the sim never reads them.
3. **Extra fighter state** (meters, cooldowns, combo counters, stances) must be mixed into `HashExtra()`. Otherwise desyncs go undetected.
4. **Bump `Version`** in [RelayLockstepSession.cs](Assets/Scripts/Net/RelayLockstepSession.cs) whenever the sim rules or the network message format change.
5. **Make characters as data whenever possible.** Use `CharacterData` assets in `Assets/Resources/Characters/`, created with right click > Create > Fight > Character. Ids are random and permanent; never reuse or hand-edit them. Use a `BaseFighter` subclass registered in `CharacterList` (fixed id 0-999) only when a mechanic can't be expressed as data. Many of the "Old Tech(nique)" gimmicks will need one.
6. **The AI** (`AIInputSource`) runs on only one machine. It may read the sim and use `System.Random`, but it must only produce input bits.
7. **WebGL limits:** browsers on https only allow `wss://` connections, there are no threads, and the game must keep ticking in a background tab (`runInBackground` is set).


## Current state vs plan
**Built:** walk, light and heavy attacks, blocking by holding back (with blockstun), hitstun, KO, the **shared kit** ([Design/SHARED_KIT.md](Design/SHARED_KIT.md): grab with back throw, double-tap dashes, a Special projectile, an Ultimate with a meter and a freeze, pushboxes, best-of-3 rounds with the meter carrying over), a `BaseFighter` template whose defaults are that standard moveset, the six launch-roster characters as placeholders (ids 10-15), Brawler, Shooter and Sandbag test characters, a "Debug boxes" hitbox/hurtbox toggle panel, art for code characters (`CodeCharacterArt`) with an Animator generator (Fight > Create Animator for Selected Character), EditMode tests (including a determinism soak test over every character pair), projectiles (hitboxes that don't follow their owner), lockstep online play through the relay with desync hashes, local play vs a simple AI that uses the whole kit, the inspector character editor with live hitbox preview, a basic lobby and controls UI, and publishing character data to the server.


**Not built yet:**
- **Each character's own design** (moves, Special, Ultimate, 2 perks, Old Tech). All six roster characters use the standard moveset until the team designs them ([Design/Characters/_TEMPLATE.md](Design/Characters/_TEMPLATE.md)).
- **Kit extensions** (cancels, armor, status effects, teleports...): specified in [Design/SHARED_KIT.md](Design/SHARED_KIT.md#extensions-build-when-a-design-needs-them), built when a design needs one.
- **Online character choice.** Both online players are currently Brawler.
- **A real AI.**


**MVP moveset:** walk, block, light, heavy, throw/grab, and specials per character.


**Roadmap (after the MVP):**
- Moveset additions: jump (the `Buttons.Up` bit is reserved), aerials, high/low attacks.
- A proper character select screen and online lobby, including sending each player's character choice over the network.


Default keys: A/D move (hold away from the opponent to block, tap twice to dash), S down, J light, K heavy, L grab, I special, O ultimate. They can be rebound in `FightControls`. `Buttons.Block` (64) is reserved and unused: blocking is holding back.


## Commands
- Relay, run in `relay/`:
  - `npm run dev`: local relay
  - `npm test`: two-client smoke test
  - `npm run deploy`: publish the relay and `characters.json`
- To publish character balance changes: in Unity, run the menu **Fight > Export Characters to Server File**, commit `relay/characters.json`, then `npm run deploy`.


## How to work in this repo
- **The team's experience varies**, and some people are new to Unity and C#. Match the existing comment style: plain English that explains *why*, with each file's purpose at the top. After a change, explain what you did and what the person needs to do in the Unity Editor.
- **This is a 72h jam.** Prefer the simplest thing that works and ships. Don't refactor or add abstractions unless asked.
- **Avoid merge conflicts.** Don't hand-edit `.unity`, `.prefab` or `.asset` YAML unless you have to. Instead, tell the person which Inspector or scene changes to make. One asset per character keeps people out of each other's files.
- Commit `.meta` files together with their assets. Never touch `Library/`, `Temp/`, `Logs/`, `UserSettings/` or the generated `.csproj`/`.slnx` files.
- New buttons: add a bit in `Sim/Buttons.cs`, then add a `Make(...)` line in `FightControls`. Keep existing bit values.



