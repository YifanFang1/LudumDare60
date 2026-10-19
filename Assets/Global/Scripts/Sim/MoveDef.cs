// A move is plain data: how long it lasts and which hitboxes it "summons" on which tick.
// These classes are [Serializable] so a ScriptableObject outside this folder (CharacterData) can
// show them in the Unity inspector. The Sim folder itself stays free of UnityEngine.
//
// New fields go at the END of each class and default to "off" (0 / false), so inspector assets
// and the server file made before they existed keep playing exactly as they did.

[System.Serializable]
public class HitboxDef
{
    public int spawnTick;            // tick of the move (0 = first tick of the move) when it appears
    public int lifetime;             // how many ticks it stays alive
    public Box box;                  // local box, see Box.cs
    public int damage;
    public int hitstun;              // ticks the victim is stunned
    public int knockback;            // milli-units per tick the victim is pushed away
    public bool followOwner = true;  // true = melee, moves with the fighter. false = projectile
    public int vx;                   // forward speed in milli-units per tick (projectiles only)
    public int vy;                   // lob: starting upward speed in milli-units per tick (projectiles only, with gravity)
    public int gravity;              // lob: pulls vy down by this much every tick. 0 = flies straight. A lob ends when its box reaches the ground
    public bool unblockable;         // hits even if the victim is blocking
    // A grab: ignores blocking, but can't catch a victim in hitstun or blockstun (so grabs can't
    // end a blocked string for free or extend combos). Holding back when it lands = back throw.
    public bool grab;
    public string visual;            // COSMETIC: which sprite the view draws for this projectile. Never affects the fight.

    // ---------- kit extensions (Design/ROSTER_PLAN.md). All off by default. ----------
    // Grab pull: a grab that lands puts the victim this far in front of the grabber first (a lasso
    // or whip that drags them in). 0 = they stay where they are. A back throw still wins.
    public int pullTo;
    // Held grab: a grab that lands HOLDS the victim (FighterStateId.Held) for this many ticks
    // instead of throwing at once. The grabber may use its hold follow-up (ChooseHoldMove); if it
    // doesn't, the throw (this hitbox's damage / hitstun / knockback) happens when the hold ends.
    public int holdTicks;
    // Lives until it hits something: `lifetime` is ignored (traps).
    public bool untilHit;
    // Can't hit an airborne fighter, so a jump avoids it (traps).
    public bool groundOnly;
    // Can't hit anyone during its first armTicks ticks (a trap needs a moment to arm).
    public int armTicks;
    // Never hits fighters; only useful with the projectile flags below (a reflector).
    public bool ignoresFighters;
    // While active, removes enemy projectiles that touch it.
    public bool destroysProjectiles;
    // While active, enemy projectiles that touch it fly back and belong to this hitbox's owner.
    public bool reflectsProjectiles;
    // Hits with this flag from the same owner that land on the same victim on the same tick merge
    // into ONE hit whose damage is multiplied (Fighter.StackMultiplier decides by how much).
    public bool stacks;
}

[System.Serializable]
public class MoveDef
{
    public string name;              // also the Animator state name the view plays for this move
    public int totalTicks;           // the move ends (back to idle) after this many ticks
    public HitboxDef[] hitboxes;
    // An Ultimate: needs a full meter and spends all of it, freezes the match for a short
    // "superflash" when it starts, and its hits give no meter. Fighter enforces all of this.
    public bool ultimate;

    // ---------- kit extensions (Design/ROSTER_PLAN.md). All off by default. ----------
    // COOLDOWN (D-43): after this move ends, the SAME move can't start again for this many ticks.
    // Other moves are free. 0 = no cooldown.
    public int cooldown;
    // HOLD INPUT (D-42): which button (a Buttons bit) the hold rules below watch. 0 = none.
    public int holdButton;
    // Charge: while holdButton is held, the move pauses on chargeTick, for up to chargeMax ticks,
    // then carries on (on release, or by itself at the cap). Fighter.chargeTicks says how long.
    public int chargeTick;
    public int chargeMax;            // 0 = this move doesn't charge
    // Repeat while held: ticks loopStart..loopEnd play again while holdButton is held, until they
    // have played loopMax times in all. They always play at least loopMin times (a tap).
    public int loopStart;
    public int loopEnd;              // 0 = this move doesn't loop
    public int loopMin;
    public int loopMax;
    // Tap vs hold: if holdButton is let go before this tick, the move is cancelled with no
    // cooldown and Fighter.OnTap runs instead (e.g. "tap to switch, hold to use"). 0 = off.
    public int tapWindow;
    // A ground move that leaves the ground (an uppercut) keeps playing after it lands, instead of
    // being cancelled like an air attack. It also can't be steered in the air.
    public bool keepOnLanding;
}
