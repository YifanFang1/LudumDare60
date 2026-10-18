# Canvas UI

The menus and HUD are normal Unity UI (Canvas, Button, TextMeshPro, Image). You edit them in the
editor like any other UI. The scripts only connect them to the match.

## First time

1. Window > TextMeshPro > Import TMP Essential Resources (once per project).
2. Open the Fight scene (it needs a GameManager with LocalMatchSession or RelayLockstepSession).
3. Menu: **Fight > Rebuild Default Canvas**. This creates `FightUI` in the scene and
   `Assets/UI/Prefabs/CharacterButton.prefab`.
4. Press Play. The canvas replaces the old OnGUI lobby and HUD automatically.

## Designing a screen

Select `FightUI`. On **Match Screens**, change **Editor Preview** to Lobby / Waiting / CharacterSelect /
Hud. Only that screen is shown while the game is stopped, so you can move, resize and restyle it.

- Fonts: make a TMP font asset (Window > TextMeshPro > Font Asset Creator), then set it on any text
  or as the default in Project Settings > TextMeshPro.
- Buttons and panels: swap the Image sprite, colours and Button colour tints as usual.
- Character buttons: edit the `CharacterButton` prefab once and every button changes.
- Health bars: the `Fill` Image under LeftHealth / RightHealth. Keep Image Type = Filled.
- Delete anything you do not want. Empty references are skipped.
- Extra `OnClick` events added in the Inspector still run next to the built-in behaviour.

## How it fits together

```
MatchScreens            shows the screen for the current phase (Lobby / Waiting / CharacterSelect / Playing)
  LobbyScreen           name, server, quick match, rooms, play vs AI
  WaitingScreen         room code + cancel
  CharacterSelectScreen tabs (vs AI), character grid, ready / fight
  HudScreen             health bars, names, info, result, names above fighters,
                        Ultimate meters, round pips, ROUND / K.O. banners, the Ultimate flash
```

Every screen talks only to `IMatchSession`, never to networking, so the UI works with any session
(offline, relay, or a future one). Nothing here touches the sim.

`ControlsUI` (key rebinding) is still the old OnGUI panel and keeps working on top of the canvas.
The hurtbox / hitbox toggles in `WorldView` are also still OnGUI (they are debug tools).

To go back to the old OnGUI screens: untick **Replace Imgui UI** on Match Screens or delete `FightUI`.

## Start menu and controls page

- `MainMenuScreen` (Play / Controls / Quit) is the first thing shown. Play opens the lobby; the lobby has a Back button.
- `ControlsScreen` lists every action from `FightControls`; click a row, press a key (Esc cancels). Reset and Back buttons included. Row look = `Assets/UI/Prefabs/ControlRow.prefab`.
- `Fight > Rebuild Default Canvas` deletes the FightUI in the open scene (if any) and builds the whole default UI again, with no prompt. Hand edits to the canvas are replaced; the Match Screens options (debug UI, start page, CRT) are kept.
- `Match Screens > Start On Main Menu` turns the start screen off if you want to land in the lobby.
- Quit is hidden in WebGL builds.

## Meters, rounds and the Ultimate flash

The HUD also shows the shared moveset's state (see `Design/SHARED_KIT.md`): an Ultimate meter for each player (bottom corners, with "ULTIMATE READY" above it when full), one pip per round won beside each health bar, a ROUND 1 / FINAL ROUND / K.O. banner, and a white flash with "<name>: ULTIMATE!" while the match freezes for an Ultimate.

- A new canvas from **Fight > Rebuild Default Canvas** has them already.
- A FightUI you already built and edited: run **Fight > Add Meter And Round HUD**. It adds only the missing parts and leaves your edits alone. Save the scene afterwards.
- Restyle or move them like anything else. On `HudScreen`: the meter colours, pip colours, banner time and flash strength. The meter `Fill` Image must stay Type = Filled. Delete a part you don't want; an empty slot is skipped.

## Play flow

Main menu > Play > **Mode** (name, Online, Offline vs AI, Back). Online opens the lobby page (quick match, create/join room); Offline starts a match vs the AI. After a match you land back on the lobby (online) or the mode page (offline). `Fight > Rebuild Default Canvas` also adds the mode page to an existing FightUI.

## Main menu art and the metal door

- Art lives in `Assets/UI/Assets`: `Main Menu BACKGROUND`, `Techtonic Logo`, the `PLAY` / `OPTIONS` / `CREDITS` / `QUIT` pictures (and their `...HOVER` versions), `Options and Credits Menu Background`, and `Metal Door Background for transitions`. `MainMenuMockup.png` shows the layout.
- **Fight > Rebuild Default Canvas** swaps an existing FightUI's start menu for that layout, adds the Credits page (Options opens the Controls page), and adds the `DoorOverlay`.
- Every change of screen goes through `DoorTransition`: a metal shutter slides down, `MatchScreens` switches the page while it is closed, then it slides back up. Leave `Match Screens > Door` empty to switch screens instantly.
- `QUIT.png` (the non-hover picture) is missing from the art folder, so Quit uses `QUITHOVER` dimmed until hovered. Add `QUIT.png` and re-run the rebuild to use it.
- The old glass-shatter scripts (`ShatterEffect`, `Fracture`, ...) are no longer used by anything.
