# Design docs

This folder holds the **plan** for the launch roster: the shared moveset every character gets, the decisions behind it, and the build order. The code docs live next to the code ([Assets/ARCHITECTURE.md](../Assets/ARCHITECTURE.md), [CHARACTERS.md](../Assets/Characters/CHARACTERS.md)) and describe what is *built*. This folder describes what is *planned* and *why*.

It sits outside `Assets/` on purpose, so Unity doesn't make `.meta` files for it.

| File | What's in it |
|---|---|
| [DECISIONS.md](DECISIONS.md) | Every design decision, the options we turned down, and why. Read it before changing a rule. |
| [SHARED_KIT.md](SHARED_KIT.md) | The standard moveset every character gets (Light, Heavy, Block, Grab, Dash, Special, Ultimate, meter, rounds) and the engine changes it needs. |
| [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) | Build order for the shared kit and the placeholders (done), tests and coding practices. |
| [ROSTER_PLAN.md](ROSTER_PLAN.md) | Build order for the real character designs: kit-wide changes, then three passes (moves, Ultimates, perks), plus the cut list. **Done.** |
| [Characters/_TEMPLATE.md](Characters/_TEMPLATE.md) | The form for designing a character's own moveset later. |
| [Characters/_EXAMPLE.md](Characters/_EXAMPLE.md) | The template filled in for a made-up Caveman, to show what a finished design looks like. |

## The launch roster

**The team designed all seven characters** in the "Character Moveset - Moveset Tracker" sheet (2026-10-17, D-40), and **all seven are built** ([ROSTER_PLAN.md](ROSTER_PLAN.md)). Each one's design, with the questions that were answered and those still open, is in [Characters/](Characters/). Names follow the sheet (in-game display names too); the C# class names stay. Until artists deliver, each is drawn as a code-made placeholder with its own colour, pattern and letter (D-58).

| Id | Character | Type | Class | Design | Status |
|---|---|---|---|---|---|
| 10 | Ruy | Shoto | `Samurai` | [Ruy.md](Characters/Ruy.md) | Built; placeholder art (was ArcadeGuy, then Samurai) |
| 11 | Paintress | Rushdown | `Painter` | [Paintress.md](Characters/Paintress.md) | Built; placeholder art |
| 12 | Noir | Zoner | `Cowboy` | [Noir.md](Characters/Noir.md) | Built; placeholder art (was Black and White Cowboy) |
| 13 | Room | Grappler | `Elephant` | [Room.md](Characters/Room.md) | Built; placeholder art |
| 14 | The Executioner | Trapper | `Executioner` | [Executioner.md](Characters/Executioner.md) | Built; placeholder art |
| 15 | Dr. Remy | Mixup | `PlagueDoctor` | [DrRemy.md](Characters/DrRemy.md) | Built; placeholder art (was Plague Doctor) |
| 16 | Nek (temp name) | Powerup | `Nek` | [Nek.md](Characters/Nek.md) | Built; placeholder art |

**Old Tech(niques) and eras are still TBD for everyone** (D-45). The sheet didn't include them.

The test characters Brawler (0), Shooter (1) and Sandbag (2) stay. Ids 3-9 are free for more test characters, and **17 and up are free for new roster characters**.

## Status

| Part | Status |
|---|---|
| Shared kit (sim) | Done, with tests (K1-K7, K-end) |
| HUD (meter, rounds), projectile art, sounds, AI update | Done (V1, A1) |
| Six placeholder characters | Done in code (R1); art slots are Editor steps (R2) |
| Real character designs | Designed (D-40 to D-49) and built (D-50 to D-61, [ROSTER_PLAN.md](ROSTER_PLAN.md)); 170 EditMode tests pass. Online two-build match: to do by the team |
| Real art | Waiting on the artists (placeholders drawn in code until then) |

Update these tables when you finish a step.

## Numbers in these docs

Units are the same as in the code: **ticks** (60 = 1 second) and **milli-units** (1000 = 1 Unity unit; the arena runs from -8000 to 8000). Boxes are `Box(x, y, w, h)` as in [Box.cs](../Assets/Global/Scripts/Sim/Box.cs). The numbers are starting values; once something is in code, **the code is the source of truth**.
