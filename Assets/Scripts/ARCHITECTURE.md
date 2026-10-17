# Code layout

Each folder is its own assembly (asmdef). The arrows are the ONLY allowed dependencies; the compiler enforces them.

    Sim  <-  Characters  <-  Match  <-  Net (relay)
               ^
          CharacterData (Unity assets)
               ^
          View (talks only to IMatchSession + CharacterData)

- **Sim** (LD.Sim): deterministic fight rules. Ints only, no UnityEngine. `Fighter` is the engine-level character class: walking, blocking (hold back), hitstun, KO.
- **Characters** (LD.Characters): `BaseFighter` (the template every code character extends), `Brawler`, `Shooter` and `Sandbag` (test characters), `CharacterDef` (a character as plain data), `DataFighter`, `CharacterList` (the roster). Sees only Sim. **Guide: [Characters/CHARACTERS.md](Characters/CHARACTERS.md).**
- **CharacterData** (LD.CharacterData): `CharacterLook` (art fields), the `CharacterData` ScriptableObject you edit in the inspector, `CodeCharacterArt` (art for code characters), and the loader that adds every asset in `Resources/Characters` to the roster.
- **Match** (LD.Match): `IMatchSession`, `MatchSessionBase`, `LocalMatchSession`, input sources, tick runner. `FightControls` holds the key bindings (new Input System, rebindable, saved in PlayerPrefs).
- **Net** (LD.Net.Relay): the Cloudflare relay session; also downloads the published character file. Nothing outside this folder references it. Delete the folder and swap `RelayLockstepSession` for `LocalMatchSession` on the GameObject to ship without online play.
- **View**: `WorldView` (fighters as GameObjects), `FightView` (HUD), `LobbyUI`, `ControlsUI`, `CharacterPreview`, `FightCamera` (follows and zooms on the fighters), `ParallaxBackground` (layered stage art).
- **Tests** (`Assets/Tests/EditMode`, LD.Tests.EditMode): sim-only checks of the character framework. Window > General > Test Runner > EditMode > Run All.
- Juice (screen shake, sparks) and audio are removed for now. They only listen to `IMatchSession` events, so they can be added back later without touching the sim.

## Making a character (no code)
1. Project window: right click `Assets/Resources/Characters` > Create > Fight > Character. Name the file.
2. Click it once so it gets its permanent random id. Edit stats, hurtbox, moves and hitboxes in the inspector.
3. It appears in the lobby's character buttons automatically. Nobody edits a shared list, so there are no merge conflicts.

**Hitbox numbers** are in milli-units (1000 = 1 Unity unit). A `Box` is: x = how far forward its near edge is from the fighter's origin, y = height above the feet, w, h = size. A move is `totalTicks` long (60 ticks = 1 s); each hitbox appears on `spawnTick` (0 = first tick of the move) for `lifetime` ticks. Untick `followOwner` and set `vx` for a projectile. Moves are checked top to bottom; the first whose button is held wins.

**Blockout without pressing Play:** add `CharacterPreview` to an empty GameObject, drag in your character, pick the move and drag the Tick slider. The Scene view shows the body (green) and the hitboxes at that tick (red = active). Edit numbers in the asset and the outlines update live.

**Sliders and toggles:** every box in the inspector (hurtbox and hitboxes) is four sliders. Select the character asset next to a `CharacterPreview` and drag: the Scene view updates live (you can also drag while the game runs). `CharacterPreview` has checkboxes for sprite, hurtbox, hitboxes and fill. During a match, the "Debug boxes" panel (bottom left) has checkboxes for hurtboxes and hitboxes (a hitbox only shows during the few ticks it is active); with no art, "hurtboxes" off hides the placeholder body.

**Simple sprite:** assign `Sprite` on the character (art facing right) and tune Sprite Offset X/Y and Sprite Scale with the sliders until it lines up with the green hurtbox. It shows in the preview and in matches.

**Art and animation (optional):** assign a `viewPrefab` (art facing right, with an Animator). The Animator needs states named `Idle`, `Walk`, `Hitstun`, `Blockstun`, `KO`, and one per move, named exactly like the move. Select the asset and run **Fight > Create Animator for Selected Character** to generate the controller, empty clips and prefab with the right names. The sim picks the pose: an attack clip is scrubbed across the move's length, so if the hitbox spawns on tick 4 of a 15-tick move, the clip's frame at 4/15 of its length is the hit frame. Without a prefab you get a coloured box.

**Art for code characters:** code characters have no `CharacterData`, so their art goes in a **Code Character Art** asset (Create > Fight > Code Character Art, saved in `Resources/Characters`). It has the same look fields, plus a dropdown to pick the character. `CharacterData` and `CodeCharacterArt` both extend `CharacterLook`, and `WorldView` draws from whichever one matches the fighter's id (`CharacterRegistry.GetLook`). Full steps: [Characters/CHARACTERS.md](Characters/CHARACTERS.md#adding-art-and-animation).

**Special mechanics** (combo counters, charge meters, projectile limits): write a `BaseFighter` subclass in code like `Brawler.cs` or `Shooter.cs`, register it in `CharacterList` with the next free id (0-999), and override `HashExtra()` for any extra state. Step by step: [Characters/CHARACTERS.md](Characters/CHARACTERS.md).

## Publishing characters to the server
Author natively in Unity (inspector, sliders, preview, local Play vs AI). When you want everyone online to get the changes:
1. Menu **Fight > Export Characters to Server File** writes `relay/characters.json` (stats, hurtbox, moves, hitboxes of every character asset).
2. Commit it, then run `npm run deploy` in the `relay` folder. The relay hosts it at `/characters.json`.
3. Every game connects to that relay at startup, downloads the file and turns it into the real characters. Players on any build get the same data, so balance changes need no new build or itch upload. Art (sprites, prefabs) is still part of the build and is matched by character id; a character without art in the build is a coloured box.

An empty server file means "nothing published yet" and the characters inside the build are used. If the download fails the game falls back to the build's characters and says so. In the Editor the download is off by default so you test your own assets; tick `Fetch In Editor` on the relay session to test the pipeline. Code characters (Brawler, Shooter, Sandbag) are never exported.

## Sim rules
ints only, no float/Time/Random/UnityEngine, state changes only inside `GameSim.Step`. Visuals react to the sim; the sim never reads them.

**Blocking** is in `Fighter` and works the same for every character: hold away from the opponent while standing or walking. A blocked hit does no damage, pushes back, and causes `Blockstun` (60% of the hitstun). `GameSim` decides `hit.blocked` before applying any hit, and sends a `Block` event instead of `Hit`.

## Online
Character select happens after both players are in the room: each player picks their own character and presses Ready, which sends `c,<characterId>` through the relay (it forwards any message untouched, so the relay needs no change). When both picks have arrived, both machines start the match with the picks ordered by slot. Vs AI uses the same screen, and you pick the AI's character too. The network version includes a hash of all character data, so two builds with different characters refuse to match instead of desyncing.
