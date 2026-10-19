# Folder layout

Assets are grouped by feature, not by file type. Each feature folder holds its own scripts, art,
prefabs and scenes, so people working on different features stay out of each other's files.

    Assets/
      Global/          shared game core
        Scripts/Sim        LD.Sim         deterministic fight rules
        Scripts/Match      LD.Match       sessions, input, tick runner
        Scripts/Net        LD.Net.Relay   online play through the relay
        Scripts/View       (no asmdef)    WorldView, FightView
        Audio/             AudioManager + music
        Rendering/         SpriteOutline (fighter outlines) + OutlineProfile, shader and material in Resources/
      Characters/      everything about fighters (guide: CHARACTERS.md)
        Scripts/Fighters   LD.Characters  BaseFighter, CharacterList, test fighters
        Scripts/Data       LD.CharacterData  CharacterData, CodeCharacterArt, loader
        Scripts/Editor     inspector tools, animator generator, export menu
        <Name>/            one folder per character: art, animations, view prefab
          Resources/       its CharacterData / CodeCharacterArt asset
      Juice/           screen shake + particles (Scripts/, Resources/ for effect prefabs)
      UI/              LobbyUI, ControlsUI, GuiDraw, CRT filter (Scripts/)
      MenuScreen/      Menu.unity, the main scene (lobby + fight)
      Stages/          stage backgrounds, one folder per stage
      Tests/EditMode   LD.Tests.EditMode

**Resources folders:** the game finds characters and effects at runtime without anyone wiring
them up, by loading them from *any* folder named `Resources` (filtered by type). So a
`CharacterData` must sit in a `Resources` folder (normally `Assets/Characters/<Name>/Resources/`)
and a juice effect prefab in `Assets/Juice/Resources/`. Everything else can live anywhere.

**Moving files:** always move files inside the Unity Project window. Moving them in Explorer
without their `.meta` file gives them a new GUID, and every scene or asset that pointed at them
loses the link.

# Code layout

Each code folder is its own assembly (asmdef). The arrows are the ONLY allowed dependencies; the compiler enforces them.

    Sim  <-  Characters  <-  Match  <-  Net (relay)
               ^
          CharacterData (Unity assets)
               ^
          View (talks only to IMatchSession + CharacterData)

- **Sim** (LD.Sim): deterministic fight rules. Ints only, no UnityEngine. `Fighter` is the engine-level character class: walking, blocking (hold back), dashing (double-tap), the Ultimate meter, hitstun, KO, move cooldowns, hold input (charge / repeat / tap), and the status effects that live on the victim (slow, bleed, scramble, leeches). `GameSim` runs the match: hits (with per-hit modifiers), grabs (pull and hold), pushboxes, projectile reflect/destroy, ground zones (`Zone`), Ultimate copies (`Summon`), the Ultimate freeze and best-of-3 rounds. The order of one tick is at the top of `GameSim.cs`.
- **Characters** (LD.Characters): `BaseFighter` (the template every code character extends; its defaults are the **standard moveset**), the seven launch-roster characters (`Samurai` = Ruy, `Painter` = Paintress, `Cowboy` = Noir, `Elephant` = Room, `Executioner`, `PlagueDoctor` = Dr. Remy, `K3N`; designs in [Design/Characters/](../Design/Characters/)), `Sandbag` (training dummy and fallback; the old `Brawler` and `Shooter` test fighters live in `Tests/EditMode`), `CharacterDef` (a character as plain data), `DataFighter`, `CharacterList` (the roster). Sees only Sim. **Guide: [CHARACTERS.md](Characters/CHARACTERS.md).** The design behind the shared moveset is in [Design/SHARED_KIT.md](../Design/SHARED_KIT.md).
- **CharacterData** (LD.CharacterData): `CharacterLook` (art fields), the `CharacterData` ScriptableObject you edit in the inspector, `CodeCharacterArt` (art for code characters), and the loader that adds every `CharacterData` in a `Resources` folder to the roster.
- **Match** (LD.Match): `IMatchSession`, `MatchSessionBase`, `LocalMatchSession`, input sources, tick runner. `FightControls` holds the key bindings (new Input System, rebindable, saved in PlayerPrefs).
- **Net** (LD.Net.Relay): the Cloudflare relay session; also downloads the published character file. Nothing outside this folder references it. Delete the folder and swap `RelayLockstepSession` for `LocalMatchSession` on the GameObject to ship without online play.
- **View** (no asmdef, so in Assembly-CSharp): `WorldView` (fighters and projectile sprites as GameObjects) and `FightView` (the OnGUI HUD: health, meter, round pips, banners, the Ultimate flash), `PlaceholderArt` (the code-drawn placeholder look of every character, projectile, trap and zone until real art exists) and `FighterStatusText` (the ammo / potion / SLOW line under each health bar) in `Global/Scripts/View`; `LobbyUI`, `ControlsUI` in `UI/Scripts`; `CharacterPreview` in `Characters/Scripts`; `FightCamera` (follows and zooms on the fighters) and `ParallaxBackground` (layered stage art), with `StagePicker` switching on one random stage per match (from the shared match seed online). The Canvas UI (`UI/Scripts/Canvas`, see [UI/CANVAS_UI.md](UI/CANVAS_UI.md)) turns `FightView` off and shows `HudScreen` instead when it is in the scene; `HudScreen` shows the same meter, round pips, banners and Ultimate flash (**Fight > Add Meter And Round HUD** adds them to an existing canvas).
- **Tests** (`Assets/Tests/EditMode`, LD.Tests.EditMode): checks that run the real sim with no scene, in milliseconds. Window > General > Test Runner > EditMode > Run All.
  - `CharacterTests`: the characters and the roster. `KitTests`: every shared-kit rule (grab, dash, jump, meter, Ultimate, rounds, pushboxes). `KitExtensionTests`: the rules added for the roster (cooldowns, hold input, hit modifiers, statuses, zones, held grabs, traps, reflect, copies...), each on a `KitFighter` with only that rule switched on. `RosterTests`: each launch character's special rules. `AITests`: AI vs AI matches finish and use the whole kit, with every roster character.
  - Outside Unity, the sim and these tests also compile and run with plain .NET (they only need NUnit), which is how they were developed; Unity's Test Runner is still the reference.
  - `SoakTests`: random matches for **every pair** of code characters, each played twice, checking the hash is identical every tick (the desync check) and that matches can finish. It picks up new code characters by itself.
  - `SimHarness`: shared helpers (`NewMatch`, `Run`, `TestFighter`). The tests also reference LD.Match, for the AI.
- **Audio**: `AudioManager` on the GameManager object. It plays fight sounds and character voice shouts from `IMatchSession.SimEventRaised`, speaks the intro and win/lose dialogue it reads from `MatchCinematics` (`CharacterLook.voice`), and picks music from the match phase; menus call `AudioManager.Click()` / `Confirm()`, which do nothing if there is no AudioManager. Every clip slot is optional.
- **Juice** (`Assets/Juice`): screen shake and particles. It only listens to `IMatchSession` events, so it never touches the sim.
- **Outlines** (`Global/Rendering`): `SpriteOutline` draws any number of coloured outline layers around a sprite's shape (each one around the last), worked out from the current frame every frame. `WorldView` adds it to both fighters: the fighter this machine controls gets the **Player** layers and the other one the **Enemy** layers, so online each player sees themselves in the Player colours. Edit the colours and widths in **Fight > Outline Colors (Player and Enemy)** (the `OutlineProfile` asset). Preview without pressing Play: add `SpriteOutline` to any sprite in the scene, or use the Outline option on `CharacterPreview`. Sprites need **Mesh Type: Full Rect** in their import settings.

## Making a character (no code)
1. Project window: make a folder `Assets/Characters/<Name>/Resources`, then right click it > Create > Fight > Character. Name the file.
2. Click it once so it gets its permanent random id. Edit stats, hurtbox, moves and hitboxes in the inspector.
3. It appears in the lobby's character buttons automatically. Nobody edits a shared list, so there are no merge conflicts.

**Hitbox numbers** are in milli-units (1000 = 1 Unity unit). A `Box` is: x = how far forward its near edge is from the fighter's origin, y = height above the feet, w, h = size. A move is `totalTicks` long (60 ticks = 1 s); each hitbox appears on `spawnTick` (0 = first tick of the move) for `lifetime` ticks. Untick `followOwner` and set `vx` for a projectile. Moves are checked top to bottom; the first whose button is held wins. A move can be bound to Light, Heavy, Grab, Special or Ultimate. Tick `grab` on a hitbox for a grab (ignores blocking, can't catch a stunned fighter), `unblockable` for a hit that ignores blocking, and `ultimate` on a move to make it need a full meter and start the freeze. `visual` names the projectile's sprite (cosmetic).

**Blockout without pressing Play:** add `CharacterPreview` to an empty GameObject, drag in your character, pick the move and drag the Tick slider. The Scene view shows the body (green) and the hitboxes at that tick (red = active). Edit numbers in the asset and the outlines update live.

**Sliders and toggles:** every box in the inspector (hurtbox and hitboxes) is four sliders. Select the character asset next to a `CharacterPreview` and drag: the Scene view updates live (you can also drag while the game runs). `CharacterPreview` has checkboxes for sprite, hurtbox, hitboxes and fill. During a match, the "Debug boxes" panel (bottom left) has checkboxes for hurtboxes and hitboxes (a hitbox only shows during the few ticks it is active); with no art, "hurtboxes" off hides the placeholder body.

**Simple sprite:** assign `Sprite` on the character (art facing right) and tune Sprite Offset X/Y and Sprite Scale with the sliders until it lines up with the green hurtbox. It shows in the preview and in matches.

**Art and animation (optional):** assign a `viewPrefab` (art facing right, with an Animator). The Animator needs states named `Idle`, `Walk`, `DashForward`, `DashBack`, `Hitstun`, `Blockstun`, `KO`, and one per move, named exactly like the move (`Light`, `Heavy`, `Grab`, `Special`, `Ultimate` for the standard moveset). Select the asset and run **Fight > Create Animator for Selected Character** to generate the controller, empty clips and prefab with the right names. The sim picks the pose: an attack clip is scrubbed across the move's length, so if the hitbox spawns on tick 4 of a 15-tick move, the clip's frame at 4/15 of its length is the hit frame. Without a prefab you get a coloured box.

**Art for code characters:** code characters have no `CharacterData`, so their art goes in a **Code Character Art** asset (Create > Fight > Code Character Art, saved in `Assets/Characters/<Name>/Resources`). It has the same look fields, plus a dropdown to pick the character. `CharacterData` and `CodeCharacterArt` both extend `CharacterLook`, and `WorldView` draws from whichever one matches the fighter's id (`CharacterRegistry.GetLook`). Full steps: [CHARACTERS.md](Characters/CHARACTERS.md#adding-art-and-animation).

**Special mechanics** (combo counters, charge meters, projectile limits): write a `BaseFighter` subclass in code like `Executioner.cs`, register it in `CharacterList` with the next free id (0-999), and override `HashExtra()` for any extra state. A subclass that overrides nothing already has the full standard moveset (the test characters' Grab, Special and Ultimate come from it). Step by step: [CHARACTERS.md](Characters/CHARACTERS.md).

## Publishing characters to the server
Author natively in Unity (inspector, sliders, preview, local Play vs AI). When you want everyone online to get the changes:
1. Menu **Fight > Export Characters to Server File** writes `relay/characters.json` (stats, hurtbox, moves, hitboxes of every character asset).
2. Commit it, then run `npm run deploy` in the `relay` folder. The relay hosts it at `/characters.json`.
3. Every game connects to that relay at startup, downloads the file and turns it into the real characters. Players on any build get the same data, so balance changes need no new build or itch upload. Art (sprites, prefabs) is still part of the build and is matched by character id; a character without art in the build is a coloured box.

An empty server file means "nothing published yet" and the characters inside the build are used. If the download fails the game falls back to the build's characters and says so. In the Editor the download is off by default so you test your own assets; tick `Fetch In Editor` on the relay session to test the pipeline. Code characters (the roster, Sandbag) are never exported.

## Sim rules
ints only, no float/Time/Random/UnityEngine, state changes only inside `GameSim.Step`. Visuals react to the sim; the sim never reads them. Every rule below is the same for every character and has a test in `KitTests` (or `KitExtensionTests` for the roster's rules). The reasons are in [Design/DECISIONS.md](../Design/DECISIONS.md).

**Controls:** A/D walk, Space jump, Left Shift + A/D dash, J Light, K Heavy, L Grab, I Special, O Ultimate (`Sim/Buttons.cs`, rebindable in `FightControls`). If several are pressed on one tick: Ultimate > Special > Grab > Heavy > Light.

**Blocking** is in `Fighter` and works the same for every character: hold away from the opponent while standing or walking. A blocked hit does no damage, pushes back, and causes `Blockstun` (60% of the hitstun). `GameSim` decides `hit.blocked` before applying any hit, and sends a `Block` event instead of `Hit`. You can't block during a dash.

**Grabs** are hitboxes with `grab` ticked: they ignore blocking but can't catch a fighter in hitstun or blockstun (the hitbox stays out and catches them if they recover in time). Holding back when a grab lands throws the victim behind you. A landed grab sends a `Grab` event instead of `Hit`.

**Dashes:** a fresh press of a direction twice within 12 ticks (`Fighter.DashTapWindow`), or `Buttons.Dash` (Left Shift) with a direction (`DashRequest`). In the air it is an air dash, once per jump (`usedAirDash`, `AirDash`): `Move` skips gravity during it, then it returns to `Jump`. Toward the opponent is a forward dash. Characters change them with `ForwardDash` / `BackDash`.

**Jumps:** holding Up (`Buttons.Up`) from Idle or Walk starts `FighterStateId.Jump` with `vy = JumpArc.up` and `vx = direction * JumpArc.forward`; holding left/right in the air nudges `vx` by `AirControl` per tick (`SteerInAir`), capped at the jump speed. `Fighter.Move` applies `Gravity` while `IsAirborne` (`y > 0 || vy > 0`) and lands at `y = 0`. In the air the only choice is one air attack (`ChooseAirMove`, `usedAirMove`); landing cancels it. You can't block in the air, grabs skip airborne victims, `SeparateBodies` only pushes when both fighters are grounded (so jumps can cross over), and Hitstun can't end while airborne. Inspector characters bind air attacks with `AttackButton.JumpLight` / `JumpHeavy`, and get the standard ones if they have none.

**Meter and Ultimate:** `Fighter.meter` (0-1000) fills by hitting (damage x 7), getting hit (x 4) and blocked hits (x 2 for both). A move with `ultimate` set needs a full bar and a **fresh** press, spends the whole bar, and freezes the match for `GameSim.SuperFlashTicks` (`freezeTicks`, a `SuperFlash` event). Its hits give no meter.

**Hit stop:** when a hit lands, the whole match pauses for `GameSim.hitStopTicks` (counted down first in `Step`, like the superflash). It is `HitStopBase + damage / HitStopPerDamage` ticks, capped at `HitStopMax`; blocked hits pause `BlockStop` and a KO pauses `KOStop` before the round-over pause. The constants are in `GameSim.cs`. `hitStopOn` is a rule switch that the EditMode test harness turns off so older tests can count ticks simply; `HitStopTests` covers it.

**Pushboxes:** after moving, overlapping bodies are pushed apart, so fighters can't walk through each other. The tie-break when two fighters stand on the same spot is fixed (player 1 goes left), never "whoever comes first".

**Cooldowns (D-43):** when a move ends (finished, or cut short by a hit or a landing), the same move can't start again for its `MoveDef.cooldown` ticks; other moves are free. `Fighter.CooldownLeft` says how long is left. Dashes wait `DashCooldown` ticks. Cooldowns pause during hit stop and the superflash.

**Hold input (D-42):** data on the `MoveDef`. `chargeTick`/`chargeMax`: the move pauses while its `holdButton` is held (`Fighter.chargeTicks`). `loopStart`..`loopEnd`: repeats while held (`loopMin`..`loopMax` times). `tapWindow`: letting go early is a tap (`OnTap`), not the move.

**Status effects (D-46):** on the victim, changed only through `ApplySlow`, `AddBleed`, `ApplyScramble`, `ApplyLeech`, `Heal` and `TakeDamage`, ticked by `GameSim` after hits. One slow at a time (the longest); all slows together are capped at -50%. Damage over time never KOs. `IgnoresStatus` resists them.

**Zones, copies, held grabs, reflectors:** `GameSim.zones` (floor areas: speed, slow, damage over time), `GameSim.summons` (copies that mimic their owner and die to any hit), `HitboxDef.holdTicks` (pins the victim in `FighterStateId.Held` until the throw or the grabber's follow-up), `reflectsProjectiles` / `destroysProjectiles` (never Ultimate projectiles, D-50). All of it is hashed.

**Rounds:** best of 3 (`GameSim.RoundsToWin`). A KO ends the round; after `RoundEndDelay` ticks (no hits allowed) the next round starts with **fresh fighter objects** from the factories given to `GameSim.Reset`, so per-round state can never leak. Only the meter carries over. A double KO gives both players the round, and the match can end in a draw. `IsOver` means the *match* is over.

## Online
Character select happens after both players are in the room: each player picks their own character and presses Ready, which sends `c,<characterId>` through the relay (it forwards any message untouched, so the relay needs no change). When both picks have arrived, both machines start the match with the picks ordered by slot. Vs AI uses the same screen, and you pick the AI's character too. The network version includes a hash of all character data, so two builds with different characters refuse to match instead of desyncing.
