# <Name> (<Type>)

> Copy this file to `Design/Characters/<Name>.md` when the team designs a character. Keep every heading, even if the answer is "standard" (the [standard moveset](../SHARED_KIT.md#the-standard-moveset)). Roster characters keep their id from [../README.md](../README.md) (10-15); a brand-new character takes the next free id (16 and up) and adds a row to the roster table there. For a filled-in example, see [_EXAMPLE.md](_EXAMPLE.md).

**Id:** <...> · **Class:** `<Name> : BaseFighter` in `Assets/Characters/Scripts/Fighters/<Name>.cs` · **Era:** <...> · **Art style:** <who draws it, and how>

## Game plan
Two or three sentences: how this character wins, and what it's weak to. Every move below should serve this.

## Stats
| HP | Walk speed | Body (hurtbox) |
|---|---|---|
| 100 | 60 | `Box(-500, 0, 1000, 2000)` (1.0 x 2.0) |

## Generic moves
Write "standard" for anything that uses the [shared kit](../SHARED_KIT.md#generic-moves-and-their-defaults) defaults.

| Move | Name / look | Startup | Active | Total | Dmg | Hitstun | KB | Box | CD | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| Light (J) | Jab | | | | | | | | 5 | |
| Heavy (K) | | | | | | | | | 10 | |
| Grab (L) | | | | | | | | | 15 | |
| Forward dash | | | | | | | | | 20 | ticks / speed |
| Back dash | | | | | | | | | 20 | ticks / speed |
| Block | standard | | | | | | | | - | |

**CD** is the per-move cooldown in ticks after the move ends (D-43). The values shown are the standard ones; the Special's is 30.

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
