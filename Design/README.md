# Design docs

This folder holds the **plan** for the launch roster: the shared moveset every character gets, the decisions behind it, and the build order. The code docs live next to the code ([Assets/ARCHITECTURE.md](../Assets/ARCHITECTURE.md), [CHARACTERS.md](../Assets/Characters/CHARACTERS.md)) and describe what is *built*. This folder describes what is *planned* and *why*.

It sits outside `Assets/` on purpose, so Unity doesn't make `.meta` files for it.

| File | What's in it |
|---|---|
| [DECISIONS.md](DECISIONS.md) | Every design decision, the options we turned down, and why. Read it before changing a rule. |
| [SHARED_KIT.md](SHARED_KIT.md) | The standard moveset every character gets (Light, Heavy, Block, Grab, Dash, Special, Ultimate, meter, rounds) and the engine changes it needs. |
| [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) | Build order, tasks, tests and coding practices. |
| [Characters/_TEMPLATE.md](Characters/_TEMPLATE.md) | The form for designing a character's own moveset later. |
| [Characters/_EXAMPLE.md](Characters/_EXAMPLE.md) | The template filled in for a made-up Caveman, to show what a finished design looks like. |

## The launch roster

**For now every character is the same placeholder** (D-29): the same stats and the [standard kit](SHARED_KIT.md), including the same Special, Ultimate and (empty) perks. The goal is that all six exist, are selectable and play a full match. Their real movesets, looks and Old Tech(niques) will be designed later by the team and artists, using the [template](Characters/_TEMPLATE.md).

| Id | Character | Type (design target) | Class | Status |
|---|---|---|---|---|
| 10 | ArcadeGuy | Shoto | `ArcadeGuy` | Placeholder, in code |
| 11 | Painter | Rushdown | `Painter` | Placeholder, in code |
| 12 | Black and White Cowboy | Zoner | `Cowboy` | Placeholder, in code |
| 13 | Elephant | Grappler | `Elephant` | Placeholder, in code |
| 14 | Executioner | Trapper | `Executioner` | Placeholder, in code |
| 15 | Plague Doctor | Mixup | `PlagueDoctor` | Placeholder, in code |

The type is only a target for the later design. It doesn't change anything in the placeholder.

The test characters Brawler (0), Shooter (1) and Sandbag (2) stay. Ids 3-9 are free for more test characters, and **16 and up are free for new roster characters**.

## Status

| Part | Status |
|---|---|
| Shared kit (sim) | Done, with tests (K1-K7, K-end) |
| HUD (meter, rounds), projectile art, sounds, AI update | Done (V1, A1) |
| Six placeholder characters | Done in code (R1); art slots are Editor steps (R2) |
| Real character designs | Waiting on the team |

Update these tables when you finish a step.

## Numbers in these docs

Units are the same as in the code: **ticks** (60 = 1 second) and **milli-units** (1000 = 1 Unity unit; the arena runs from -8000 to 8000). Boxes are `Box(x, y, w, h)` as in [Box.cs](../Assets/Global/Scripts/Sim/Box.cs). The numbers are starting values; once something is in code, **the code is the source of truth**.
