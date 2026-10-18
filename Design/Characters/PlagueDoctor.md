# Plague Doctor (Mixup)

**Id:** 15 · **Class:** `PlagueDoctor : BaseFighter` in `Assets/Characters/Scripts/Fighters/PlagueDoctor.cs` · **Era:** TBD · **Art style:** TBD

## Game plan
Two or three sentences: how this character wins, and what it's weak to. Every move below should serve this.

## Stats
| HP | Walk speed | Body (hurtbox) |
|---|---|---|
| 100 | 60 | `Box(-500, 0, 1000, 2000)` (1.0 x 2.0) |

## Generic moves
Write "standard" for anything that uses the [shared kit](../SHARED_KIT.md#generic-moves-and-their-defaults) defaults.

| Move | Name / look | Startup | Active | Total | Dmg | Hitstun | KB | Box | Notes |
|---|---|---|---|---|---|---|---|---|---|
| Light (J) | | | | | | | | | |
| Heavy (K) | | | | | | | | | |
| Grab (L) | | | | | | | | | |
| Forward dash | | | | | | | | | ticks / speed |
| Back dash | | | | | | | | | ticks / speed |
| Block | standard | | | | | | | | |

## Special (I): <name>
What it does, its frame data, any limit (`CanUseMove`), and any held-direction variant.

## Ultimate (O): <name>. Old Tech: <the glitch or trick>
What happens after the 30-tick superflash. Strike (blockable), grab type (unblockable, grab rules) or interruptible? What's the opponent's counterplay? Its damage, compared with the [roster](../README.md).

## Perks
1. **<Name>:** the rule, and which hook implements it.
2. **<Name>:** ...

## Implementation notes
- **Extra state (in `HashExtra()`):** ...
- **Kit hooks used:** ...
- **Animator states beyond the generic ones:** ...
- **Projectile/trap visual keys:** ...

## View polish (optional)
Overlays, sounds and effects. These are never needed for the character to work.

## AI hints
What the generic AI does badly with this character, if anything.

## Tests
One EditMode test per special rule (see `Assets/Tests/EditMode/CharacterTests.cs`).

## Open questions
