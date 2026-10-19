using System;

// New states go at the END so the numbers of the old ones (used in the desync hash) stay the same.
// Held: pinned by a held grab (HitboxDef.holdTicks) until the grabber throws or hits them.
public enum FighterStateId { Idle, Walk, Attack, Hitstun, KO, Blockstun, Dash, Jump, Held }

// Status effects a fighter can resist (Fighter.IgnoresStatus). New kinds go at the END.
//   Slow:           any slow (traps, Dismember, Remy's slow potion, an enemy paint puddle)
//   Scramble:       shuffled controls (Paintress's Ultimate)
//   BleedWeakness:  the -25% damage at 5 bleed stacks (bleed's damage over time still applies)
public enum StatusKind { Slow, Scramble, BleedWeakness }

// The engine-level base class for every character. It owns the state machine, movement,
// blocking and getting hit. You normally do NOT subclass this directly:
//   - code characters extend BaseFighter (Characters/BaseFighter.cs), the character template
//   - inspector characters are played by DataFighter
// See Characters/CHARACTERS.md for the full guide.
//
// BLOCKING: hold the direction AWAY from the opponent while standing or walking. A blocked hit
// does no damage, still pushes you back, and puts you in Blockstun (shorter than hitstun) where
// you can't act but keep blocking. Blocking works the same for every character, so it lives here.
// A character that can't block at all says so with CanBlock.
//
// DASHING: tap a direction twice within DashTapWindow ticks (a "fresh" press each time: the key
// was up on the tick before), or press Dash (Shift) together with a direction. Toward the opponent
// = forward dash, away = back dash. A dash can't block or attack until it ends, and can't be
// repeated until DashCooldown ticks after it ends. Characters change their dashes with
// ForwardDash / BackDash.
//
// AIR DASH: the same inputs during a jump, once per jump. It flies straight sideways (no gravity)
// for AirDash.ticks, then you fall again, still able to steer and to use your air attack. Once per
// jump is its limit, so it ignores the dash cooldown, but it starts it (no ground dash right after
// landing from one). Slows and dash perks (OnDashStart, DashHitbox) apply to it like any dash.
//
// JUMPING: hold Up while standing or walking. Up alone goes straight up, Up + a direction jumps that
// way. In the air, holding left or right nudges you that way a little (AirControl per tick, never
// faster than the jump's forward speed), so you can adjust but not stop dead or turn on a dime.
// You can't block or turn around in the air. Once per jump you can do an air attack (ChooseAirMove: Light or Heavy by default).
// Landing ends the jump and cancels an unfinished air attack. Bodies only push each other when
// both fighters are on the ground, so you can jump over the opponent and land on their other side.
// Grabs can't catch an airborne fighter. A fighter hit in the air falls, and its hitstun lasts at
// least until it lands. Characters change their jump with JumpArc.
//
// COOLDOWNS (D-43): when a move ends (finished, cancelled by a hit or by landing), that same move
// can't start again for MoveDef.cooldown ticks (changed per character by ModifyCooldown).
//
// HOLD INPUT (D-42): a move can charge while its button is held, repeat while held, or be a
// "tap vs hold" move. The rules are data on the MoveDef (holdButton, chargeTick...).
//
// STATUS EFFECTS (D-46): slow, bleed, scramble and leeches live HERE, on the victim, and are only
// changed through the Apply... methods below, so a character never writes another fighter's
// fields (D-24). GameSim ticks them once per tick (TickStatuses). Damage over time can't KO (D-23).
//
// TICKS: everything is counted in ticks (1 tick = 1/60 s). `stateTick` is how many ticks the
// fighter has been in the current state; it is 0 on the first tick of a state.
//
// DETERMINISM RULES: ints only, no UnityEngine, no Time, no random.
public abstract class Fighter
{
    public int index;               // 0 or 1
    public int characterId;         // which CharacterList entry this is (set by CharacterList.Create)
    public int x;                   // milli-units
    public int y;                   // feet height in milli-units; 0 = on the ground
    public int vx;                  // milli-units per tick
    public int vy;                  // milli-units per tick, up is positive (only while airborne)
    public int hp;
    public int facing = 1;          // 1 = facing right, -1 = facing left
    public FighterStateId state;
    public int stateTick;
    public int stunTicks;           // how long the current Hitstun or Blockstun lasts
    public bool holdingBack;        // this tick's input holds away from the opponent (see IsBlocking)
    public MoveDef move;            // the move being performed while state == Attack
    public int dashTicks;           // how long the current Dash lasts
    public bool usedAirDash;        // this jump already had its air dash (reset on landing)
    public int meter;               // 0..MeterMax. A full bar allows the Ultimate. Carries over between rounds.
    public bool usedAirMove;        // this jump already had its air attack (reset on landing)

    // Input history, so the fighter can tell a fresh press from a held key (dashes, the Ultimate).
    public int prevInput;           // last tick's input, after FilterInput
    public int lastTapDir;          // Buttons.Left or Buttons.Right: the last fresh direction press (0 = none)
    public int lastTapTick;         // GameSim.tick of that press
    protected int pressedInput;     // bits pressed THIS tick that were up last tick. Worked out from
                                    // prevInput at the start of Think, so it needs no hashing.

    // Cooldowns (D-43). A few slots are plenty: only moves that ended recently are in here.
    public const int CooldownSlots = 8;
    readonly MoveDef[] cooldownMoves = new MoveDef[CooldownSlots];
    readonly int[] cooldownTicks = new int[CooldownSlots];
    public int dashCooldown;        // ticks until the next dash may start

    // Hold input (D-42), for the move being performed.
    public int chargeTicks;         // how long the move was held on its charge tick
    public bool charging;           // still waiting on the charge tick
    public int loopCount;           // how many extra times the loop has played

    // Held grab: on the GRABBER, how long the hold has left and the throw that ends it.
    public int holdTicks;
    public HitboxDef holdThrow;     // null once the grabber has used its follow-up instead

    // Status effects (D-46). See the Apply... methods.
    public int slowTicks;
    public int slowPercent;
    public int bleedStacks;
    public int bleedDamageTimer;
    public int bleedFadeTimer;
    public int scrambleTicks;
    public int scrambleCode;        // which shuffle (1..23), rolled when the scramble starts
    public int leechTicks;
    public int leechTimer;
    public int leechOwner = -1;     // who the leeches heal

    // ---------- fill these in per character ----------
    public abstract int MaxHp { get; }
    public abstract int WalkSpeed { get; }               // milli-units per tick
    // Which move does this input start? null = none. `sim` is there so a character can check the
    // match (for example "is my fireball still on screen?"). Read it, never change it here.
    public abstract MoveDef ChooseMove(GameSim sim, int input);
    // The same in the air (once per jump). null = no air attack for this input.
    public virtual MoveDef ChooseAirMove(GameSim sim, int input) { return null; }
    // The same while holding someone with a held grab (HitboxDef.holdTicks). null = keep holding.
    public virtual MoveDef ChooseHoldMove(GameSim sim, int input) { return null; }

    // Every move this character can do. NOT used by the fight; editor tools read it, e.g. to make
    // one Animator state per move. BaseFighter lists its slots (Light, Heavy, Grab, Special,
    // Ultimate); if you add moves outside those, list them too.
    public virtual MoveDef[] ListMoves() { return new MoveDef[0]; }

    // The body. Override to make a character taller / shorter, or change it while crouching.
    public virtual Box Hurtbox
    {
        get { return new Box(-500, 0, 1000, 2000); }
    }

    // Dashes: about 1.8 units each by default. Override to make a character's dashes faster,
    // longer or shorter.
    public virtual DashDef ForwardDash { get { return new DashDef(14, 130); } }
    public virtual DashDef BackDash { get { return new DashDef(16, 110); } }
    // Ticks after a dash ends before the next one may start (D-43).
    public virtual int DashCooldown { get { return StandardDashCooldown; } }
    public const int StandardDashCooldown = 20;
    // The air dash, either direction: about 1.3 units in a straight line. Override to change it.
    public virtual DashDef AirDash { get { return new DashDef(12, 110); } }

    // The jump: about 40 ticks in the air, peaking at about 2.1 units (high enough to clear a
    // Special projectile), travelling about 2 units forward or back. Override to change it.
    // Peak height is roughly up * up / (2 * Gravity); time in the air roughly 2 * up / Gravity.
    public virtual JumpDef JumpArc { get { return new JumpDef(210, 50); } }

    // How much holding left/right changes your sideways speed each tick in the air (milli-units
    // per tick, per tick). 0 = a fixed arc. At 3 it takes about 17 ticks to go from no drift to
    // full speed, and most of a jump (about 33 ticks) to turn a forward jump into a backward one.
    public virtual int AirControl { get { return 3; } }

    // false = holding back never blocks (Room, D-44).
    public virtual bool CanBlock { get { return true; } }

    // ---------- optional hooks for theme mechanics / modifiers ----------
    // Change this tick's input before the fighter uses it. Return 0 to ignore the player
    // completely (Sandbag), or swap bits for a "reversed controls" effect.
    protected virtual int FilterInput(int input) { return input; }

    // false = a hit that would KO refills health instead (training dummies).
    public virtual bool CanBeKnockedOut { get { return true; } }

    public virtual void OnTick(GameSim sim) { }
    // Called for blocked hits too; check hit.blocked if your mechanic should only count real hits.
    public virtual void OnHitDealt(GameSim sim, Fighter victim, HitboxInstance hit) { }
    public virtual void OnHitReceived(GameSim sim, Fighter attacker, HitboxInstance hit) { }

    // Move hooks: when a move starts (the tick it's chosen, before any superflash), on every tick
    // it plays (after its hitboxes for that tick have spawned), and when it ends (`finished` is
    // false if a hit or a landing cut it short).
    public virtual void OnMoveStart(GameSim sim) { }
    public virtual void OnMoveTick(GameSim sim) { }
    public virtual void OnMoveEnd(GameSim sim, MoveDef ended, bool finished) { }
    // A tap-vs-hold move (MoveDef.tapWindow) was let go early: it was a tap, not a hold.
    public virtual void OnTap(GameSim sim, MoveDef tapped) { }

    // A hitbox of yours was just created. Change the instance's copies (damage, box...) here,
    // e.g. a charged fireball. Never change hit.def.
    public virtual void OnHitboxSpawned(GameSim sim, HitboxInstance hit) { }
    // One of your hits is about to land on `victim` (nothing has been applied yet, so trades stay
    // fair). Change hit.damage / hitstun / knockback here, e.g. a damage bonus. Never change hit.def.
    public virtual void ModifyOutgoingHit(GameSim sim, Fighter victim, HitboxInstance hit) { }
    // How long `move`'s cooldown really is for this fighter right now (it just ended).
    public virtual int ModifyCooldown(MoveDef ended, int ticks) { return ticks; }
    // When `count` of your stacking hits (HitboxDef.stacks) land on the same tick, they merge into
    // one hit with its damage multiplied by this. 1 = no bonus.
    public virtual int StackMultiplier(int count) { return 1; }

    // A dash started. `forward` = toward the opponent.
    public virtual void OnDashStart(GameSim sim, bool forward) { }
    // A hitbox the dash carries along for its whole length (null = the dash doesn't hit).
    public virtual HitboxDef DashHitbox(bool forward) { return null; }

    // true = ignore this kind of status effect (Room's perk).
    public virtual bool IgnoresStatus(StatusKind kind) { return false; }
    // false = hits don't give this fighter meter right now (K3N's Install).
    public virtual bool CanGainMeter { get { return true; } }
    // true = hits pass through this fighter right now (invincible frames). Bodies still push.
    public virtual bool IsIntangible { get { return false; } }
    // Which moves an Ultimate copy (Summon) of this fighter repeats. None by default.
    public virtual bool SummonCopiesMove(MoveDef m) { return false; }

    // COSMETIC, never affects the fight: a short line for the HUD ("Ammo 4/6", "Potion: Heal"),
    // and whether to draw this fighter powered up (a glow). Not hashed.
    public virtual string HudInfo { get { return null; } }
    public virtual bool IsPoweredUp { get { return false; } }

    // HINTS FOR THE AI (AIInputSource). They only READ the sim and never affect the fight, so they
    // aren't hashed. The AI stays generic: it asks these instead of knowing characters by name.
    //   UltimateIsToggle      the Ultimate button switches a mode on and off (K3N's Install)
    //   WantsSpecialSwitch    a TAP of Special changes what Special does, and a tap is wanted now
    //   SpecialHoldTicks      hold Special at least this long to use it (0 = any press works)
    public virtual bool UltimateIsToggle { get { return false; } }
    public virtual bool WantsSpecialSwitch(GameSim sim) { return false; }
    public virtual int SpecialHoldTicks { get { return 0; } }

    // If your character has extra state that affects the fight (a combo counter, a charge meter,
    // a cooldown), mix it in here so the desync check can see it:  return meter * 31 + cooldown;
    public virtual int HashExtra() { return 0; }

    // Blocking tuning, shared by every character.
    public const int BackBlockWalkPercent = 50;   // walking back while blocking moves at this % of walk speed
    public const int BlockstunPercent = 60;    // blockstun = this % of the move's hitstun
    public const int BlockPushPercent = 100;   // pushback on block = this % of the move's knockback

    // The second tap of a double-tap must come at most this many ticks after the first.
    public const int DashTapWindow = 12;

    // Taken off vy every tick while airborne, shared by every character.
    public const int Gravity = 11;

    // Ultimate meter, shared by every character (Design/DECISIONS.md D-08). With 100 HP that is
    // roughly one full bar per round. Hits from an Ultimate give nothing.
    public const int MeterMax = 1000;
    public const int MeterPerDamageDealt = 7;     // landing a hit
    public const int MeterPerDamageTaken = 4;     // getting hit (a comeback for the losing player)
    public const int MeterPerDamageBlocked = 2;   // a blocked hit, for BOTH players

    // Status tuning (D-46), shared by every character.
    public const int MaxSlowPercent = 50;          // all slows together never cut speed more than this
    public const int MaxBleedStacks = 5;
    public const int BleedTickInterval = 60;       // each stack does 1 damage this often
    public const int BleedFadeInterval = 120;      // one stack fades this long after the last new one
    public const int BleedWeaknessPercent = 25;    // at MaxBleedStacks, your hits do this much less
    public const int LeechInterval = 60;           // leeches drain 1 HP this often

    public void AddMeter(int amount)
    {
        meter = Math.Max(0, Math.Min(MeterMax, meter + amount));
    }

    // ---------- helpers ----------
    // True if a hit landing right now would be blocked.
    public bool IsBlocking
    {
        get
        {
            bool canBlockInState = state == FighterStateId.Idle || state == FighterStateId.Walk
                                || state == FighterStateId.Blockstun;
            return CanBlock && canBlockInState && holdingBack && holdTicks == 0;
        }
    }

    // True while off the ground (jumping, or knocked out of the air and still falling).
    public bool IsAirborne
    {
        get { return y > 0 || vy > 0; }
    }

    public Box HurtboxWorld()
    {
        return Hurtbox.ToWorld(x, y, facing);
    }

    // Ticks until `m` may be used again (0 = ready).
    public int CooldownLeft(MoveDef m)
    {
        if (m == null) return 0;
        for (int i = 0; i < CooldownSlots; i++)
        {
            if (cooldownMoves[i] == m) return cooldownTicks[i];
        }
        return 0;
    }

    public bool IsOnCooldown(MoveDef m)
    {
        return CooldownLeft(m) > 0;
    }

    // Puts `m` on cooldown for `ticks` (replacing any cooldown it already has). Characters can call
    // this to share a cooldown between moves (Dr. Remy's potions).
    // "Cooldown N" means the move waits N ticks LONGER than it would with none. Cooldowns count
    // down at the start of each Think, so the first count happens on the very next tick: N + 1
    // is stored, which is exactly "ticks until it may be used" (CooldownLeft).
    protected void StartCooldown(MoveDef m, int ticks)
    {
        if (m == null || ticks <= 0) return;
        ticks++;
        int free = -1;
        int shortest = 0;
        for (int i = 0; i < CooldownSlots; i++)
        {
            if (cooldownMoves[i] == m) { cooldownTicks[i] = ticks; return; }
            if (free < 0 && cooldownMoves[i] == null) free = i;
            if (cooldownTicks[i] < cooldownTicks[shortest]) shortest = i;
        }
        int slot = free >= 0 ? free : shortest;   // all slots busy: drop the one closest to ready
        cooldownMoves[slot] = m;
        cooldownTicks[slot] = ticks;
    }

    // Every move is ready at once (not dashes). For "no cooldowns while powered up" rules.
    protected void ClearCooldowns()
    {
        for (int i = 0; i < CooldownSlots; i++)
        {
            cooldownMoves[i] = null;
            cooldownTicks[i] = 0;
        }
    }

    void TickCooldowns()
    {
        for (int i = 0; i < CooldownSlots; i++)
        {
            if (cooldownMoves[i] == null) continue;
            cooldownTicks[i]--;
            if (cooldownTicks[i] <= 0)
            {
                cooldownTicks[i] = 0;
                cooldownMoves[i] = null;
            }
        }
        if (dashCooldown > 0) dashCooldown--;
    }

    // Walk and dash speed in percent after slows and paint (100 = normal).
    public int SpeedPercent(GameSim sim)
    {
        int slow = slowTicks > 0 && !IgnoresStatus(StatusKind.Slow) ? slowPercent : 0;
        int zoneSlow = 0;
        int zoneBoost = 0;
        for (int i = 0; i < sim.zones.Count; i++)
        {
            Zone z = sim.zones[i];
            if (!z.Covers(this)) continue;
            if (z.owner == index) zoneBoost = Math.Max(zoneBoost, z.ownerSpeedPercent);
            else if (!IgnoresStatus(StatusKind.Slow)) zoneSlow = Math.Max(zoneSlow, z.enemySlowPercent);
        }
        return 100 + zoneBoost - Math.Min(MaxSlowPercent, slow + zoneSlow);
    }

    public void Init(int fighterIndex, int startX)
    {
        index = fighterIndex;
        x = startX;
        y = 0;
        vx = 0;
        vy = 0;
        usedAirMove = false;
        usedAirDash = false;
        hp = MaxHp;
        facing = fighterIndex == 0 ? 1 : -1;
        move = null;
        stunTicks = 0;
        dashTicks = 0;
        meter = 0;   // GameSim copies it back in when a new round starts (it carries over)
        holdingBack = false;
        prevInput = 0;
        lastTapDir = 0;
        lastTapTick = 0;
        for (int i = 0; i < CooldownSlots; i++) { cooldownMoves[i] = null; cooldownTicks[i] = 0; }
        dashCooldown = 0;
        chargeTicks = 0;
        charging = false;
        loopCount = 0;
        holdTicks = 0;
        holdThrow = null;
        slowTicks = 0;
        slowPercent = 0;
        bleedStacks = 0;
        bleedDamageTimer = 0;
        bleedFadeTimer = 0;
        scrambleTicks = 0;
        scrambleCode = 0;
        leechTicks = 0;
        leechTimer = 0;
        leechOwner = -1;
        EnterState(FighterStateId.Idle);
    }

    protected void EnterState(FighterStateId next)
    {
        state = next;
        stateTick = -1;   // Think() adds 1 at the start of every tick, so the first tick in a state is 0
    }

    // ---------- phase 1 of a tick: decide what to do ----------
    public void Think(GameSim sim, int input)
    {
        input = Scramble(FilterInput(input));
        pressedInput = input & ~prevInput;
        int doubleTap = TrackTaps(sim);   // recorded in every state, but only used from Idle/Walk/Jump
        stateTick++;
        TickCooldowns();
        OnTick(sim);

        switch (state)
        {
            case FighterStateId.Idle:
            case FighterStateId.Walk:
                if (holdTicks > 0) ThinkHolding(sim, input);
                else ThinkGrounded(sim, input, doubleTap);
                break;

            case FighterStateId.Attack:
                ThinkAttack(sim, input);
                break;

            case FighterStateId.Jump:
                ThinkAir(sim, input, doubleTap);
                break;

            case FighterStateId.Dash:
                if (stateTick >= dashTicks - 1)
                {
                    if (IsAirborne)
                    {
                        // An air dash ends in a normal fall, keeping a jump's worth of momentum.
                        vx = vx > 0 ? JumpArc.forward : -JumpArc.forward;
                        EnterState(FighterStateId.Jump);
                    }
                    else
                    {
                        vx = 0;
                        EnterState(FighterStateId.Idle);
                    }
                }
                break;

            case FighterStateId.Hitstun:
            case FighterStateId.Blockstun:
                // Stun never ends in the air: a fighter hit out of a jump stays in hitstun until it lands.
                if (stateTick >= stunTicks - 1 && !IsAirborne)
                {
                    vx = 0;
                    EnterState(FighterStateId.Idle);
                }
                break;

            case FighterStateId.Held:
            case FighterStateId.KO:
                vx = 0;   // a held fighter is released by GameSim (StepHolds), never by itself
                break;
        }

        // Air control works while jumping and during an air attack, not while knocked out of the air,
        // and not during a ground move that leaves the ground (an uppercut has a fixed arc).
        if (IsAirborne && (state == FighterStateId.Jump || (state == FighterStateId.Attack && move != null && !move.keepOnLanding)))
        {
            SteerInAir(input);
        }

        // Done after the switch so it uses this tick's facing (ThinkGrounded may have turned us).
        int back = facing > 0 ? Buttons.Left : Buttons.Right;
        holdingBack = (input & back) != 0;
        prevInput = input;
    }

    // Remembers fresh Left/Right presses. Returns the direction bit (Buttons.Left or Right) if this
    // press completes a double-tap, else 0. A completed double-tap is forgotten, so a third tap
    // needs a fourth before it dashes again.
    int TrackTaps(GameSim sim)
    {
        int tapped = 0;
        if ((pressedInput & Buttons.Left) != 0) tapped = Buttons.Left;
        else if ((pressedInput & Buttons.Right) != 0) tapped = Buttons.Right;
        if (tapped == 0) return 0;

        bool isDoubleTap = tapped == lastTapDir && sim.tick - lastTapTick <= DashTapWindow;
        lastTapDir = isDoubleTap ? 0 : tapped;
        lastTapTick = sim.tick;
        return isDoubleTap ? tapped : 0;
    }

    void ThinkGrounded(GameSim sim, int input, int doubleTap)
    {
        Fighter other = sim.fighters[1 - index];
        facing = x <= other.x ? 1 : -1;

        MoveDef chosen = ChooseMove(sim, input);
        // Safety nets for every character, whatever ChooseMove says: an Ultimate needs a full bar,
        // and a move on cooldown can't start.
        if (chosen != null && chosen.ultimate && meter < MeterMax) chosen = null;
        if (chosen != null && IsOnCooldown(chosen)) chosen = null;
        if (chosen != null)
        {
            StartMove(sim, chosen);
            return;
        }

        int dashDirection = dashCooldown == 0 ? DashRequest(input, doubleTap) : 0;
        if (dashDirection != 0)
        {
            StartDash(sim, dashDirection);
            return;
        }

        int direction = DirectionFrom(input);

        // Held, not a fresh press: holding Up keeps jumping each time you land.
        if ((input & Buttons.Up) != 0)
        {
            JumpDef jump = JumpArc;
            vx = direction * jump.forward;
            vy = jump.up;
            usedAirMove = false;
            usedAirDash = false;
            EnterState(FighterStateId.Jump);
            return;
        }

        int walk = WalkSpeed * SpeedPercent(sim) / 100;
        // Walking backwards while blocking (holding back, the same test IsBlocking uses) is half speed.
        // holdingBack is only updated at the end of the tick, so test this tick's input here.
        int backButton = facing > 0 ? Buttons.Left : Buttons.Right;
        if ((input & backButton) != 0 && direction != 0 && CanBlock && holdTicks == 0) walk = walk * BackBlockWalkPercent / 100;
        vx = direction * walk;
        FighterStateId wanted = vx != 0 ? FighterStateId.Walk : FighterStateId.Idle;
        if (state != wanted) EnterState(wanted);
    }

    // While holding someone (a held grab): no walking, jumping or dashing. Only the hold follow-up.
    void ThinkHolding(GameSim sim, int input)
    {
        vx = 0;
        if (state != FighterStateId.Idle) EnterState(FighterStateId.Idle);
        MoveDef chosen = ChooseHoldMove(sim, input);
        if (chosen == null || IsOnCooldown(chosen) || chosen.ultimate) return;

        // The follow-up replaces the throw. GameSim keeps the victim held while this move plays.
        holdTicks = 0;
        holdThrow = null;
        StartMove(sim, chosen);
    }

    // Starts `chosen` from the ground. Every ground move goes through here.
    void StartMove(GameSim sim, MoveDef chosen)
    {
        move = chosen;
        vx = 0;
        chargeTicks = 0;
        charging = chosen.chargeMax > 0;
        loopCount = 0;
        EnterState(FighterStateId.Attack);
        sim.AddEvent(SimEventType.AttackStart, index, -1, x, y, 0);
        if (chosen.ultimate)
        {
            meter = 0;
            sim.StartSuperFlash(index);
        }
        OnMoveStart(sim);
    }

    // The move stopped (finished, cut short by a hit or a landing). Starts its cooldown.
    void EndMove(GameSim sim, bool finished)
    {
        MoveDef ended = move;
        move = null;
        if (ended == null) return;
        StartCooldown(ended, ModifyCooldown(ended, ended.cooldown));
        OnMoveEnd(sim, ended, finished);
    }

    // Which way to dash this tick: +1 (right), -1 (left) or 0 (no dash). Either a double-tap, or
    // Dash (Shift) + a direction on the tick one of them is pressed while the other is held, so
    // holding both doesn't dash again and again.
    int DashRequest(int input, int doubleTap)
    {
        if (doubleTap != 0) return doubleTap == Buttons.Right ? 1 : -1;
        if ((input & Buttons.Dash) == 0) return 0;
        if ((pressedInput & (Buttons.Dash | Buttons.Left | Buttons.Right)) == 0) return 0;
        return DirectionFrom(input);   // 0 if neither or both directions are held
    }

    // `worldDirection` is +1 (right) or -1 (left); toward the opponent is a forward dash.
    // In the air it's an air dash: it flies straight (Move skips gravity during it).
    void StartDash(GameSim sim, int worldDirection)
    {
        bool forward = worldDirection == facing;
        bool air = IsAirborne;
        DashDef dash = air ? AirDash : (forward ? ForwardDash : BackDash);
        dashTicks = Math.Max(1, dash.ticks);
        // Counts down from the start, so the next dash waits DashCooldown ticks longer than it
        // would with none (+1 for the same reason as StartCooldown). Air dashes start it too.
        dashCooldown = dashTicks + DashCooldown + 1;
        vx = worldDirection * (dash.speed * SpeedPercent(sim) / 100);
        if (air)
        {
            vy = 0;
            usedAirDash = true;
        }
        EnterState(FighterStateId.Dash);
        sim.AddEvent(SimEventType.Dash, index, -1, x, y, forward ? 1 : 0);
        HitboxDef carried = DashHitbox(forward);
        if (carried != null) sim.SpawnHitbox(index, carried, dashTicks);
        OnDashStart(sim, forward);
    }

    // True while dashing toward the opponent (for the view: DashForward vs DashBack pose).
    public bool IsDashingForward
    {
        get { return state == FighterStateId.Dash && (vx > 0) == (facing > 0); }
    }

    void ThinkAttack(GameSim sim, int input)
    {
        bool held = move.holdButton != 0 && (input & move.holdButton) != 0;

        // Tap vs hold: let go before the window ends = a tap. No move, no cooldown.
        if (move.tapWindow > 0 && stateTick < move.tapWindow && !held)
        {
            MoveDef tapped = move;
            move = null;
            sim.RemoveFollowHitboxes(index);
            EnterState(FighterStateId.Idle);
            OnTap(sim, tapped);
            return;
        }

        // Charge: wait on the charge tick while the button is held, up to the cap.
        if (charging && stateTick == move.chargeTick)
        {
            if (held && chargeTicks < move.chargeMax)
            {
                chargeTicks++;
                stateTick--;   // stay on this tick (the pose holds too)
                return;
            }
            charging = false;
        }

        // Summon every hitbox that is due on this tick of the move.
        for (int i = 0; i < move.hitboxes.Length; i++)
        {
            if (move.hitboxes[i].spawnTick == stateTick)
            {
                sim.SpawnHitbox(index, move.hitboxes[i]);
            }
        }
        OnMoveTick(sim);
        if (move == null) return;   // OnMoveTick may end it (it shouldn't, but be safe)

        // Repeat while held: jump back to the start of the loop.
        if (move.loopEnd > 0 && stateTick == move.loopEnd)
        {
            int played = loopCount + 1;
            if (played < move.loopMin || (held && played < move.loopMax))
            {
                loopCount++;
                stateTick = move.loopStart - 1;   // Think adds 1 next tick
                return;
            }
        }

        if (stateTick >= move.totalTicks - 1)
        {
            EndMove(sim, true);
            // An air attack that finishes before landing goes back to falling.
            EnterState(IsAirborne ? FighterStateId.Jump : FighterStateId.Idle);
        }
    }

    // Holding a direction moves vx toward that direction's full jump speed by AirControl per tick.
    // Holding nothing keeps your momentum, so a forward jump still carries you forward.
    void SteerInAir(int input)
    {
        int direction = DirectionFrom(input);
        if (direction == 0) return;
        int target = direction * JumpArc.forward;
        int step = AirControl;
        if (vx < target) vx = Math.Min(target, vx + step);
        else if (vx > target) vx = Math.Max(target, vx - step);
    }

    // In the air: the one air attack per jump, or the one air dash per jump (attack wins a tie,
    // like on the ground).
    void ThinkAir(GameSim sim, int input, int doubleTap)
    {
        MoveDef chosen = usedAirMove ? null : ChooseAirMove(sim, input);
        // Ultimates are ground-only (they spend meter there), and a move on cooldown can't start.
        if (chosen != null && (chosen.ultimate || IsOnCooldown(chosen))) chosen = null;
        if (chosen == null)
        {
            // Once per jump is the air dash's limit, so it doesn't wait for the dash cooldown.
            int dashDirection = usedAirDash ? 0 : DashRequest(input, doubleTap);
            if (dashDirection != 0) StartDash(sim, dashDirection);
            return;
        }

        move = chosen;
        usedAirMove = true;
        chargeTicks = 0;
        charging = false;
        loopCount = 0;
        EnterState(FighterStateId.Attack);   // vx and vy are kept, so the jump carries on
        sim.AddEvent(SimEventType.AttackStart, index, -1, x, y, 0);
        OnMoveStart(sim);
    }

    static int DirectionFrom(int input)
    {
        int d = 0;
        if ((input & Buttons.Left) != 0) d -= 1;
        if ((input & Buttons.Right) != 0) d += 1;
        return d;
    }

    // ---------- phase 2 of a tick: physics ----------
    public void Move(GameSim sim)
    {
        x = GameSim.ClampToArena(x + vx);

        // No gravity during an air dash: it flies straight, then the fall starts again.
        if (IsAirborne && state != FighterStateId.Dash)
        {
            y += vy;
            vy -= Gravity;
            if (y <= 0) Land(sim);
        }
    }

    void Land(GameSim sim)
    {
        y = 0;
        vy = 0;
        usedAirMove = false;
        usedAirDash = false;

        if (state == FighterStateId.Attack && move != null && move.keepOnLanding)
        {
            vx = 0;   // an uppercut lands and plays out its recovery
            return;
        }
        if (state == FighterStateId.Jump || state == FighterStateId.Attack)
        {
            // Landing cancels an air attack that hasn't finished. (Ground attacks never get here:
            // they can only start on the ground.)
            if (state == FighterStateId.Attack)
            {
                sim.RemoveFollowHitboxes(index);
                EndMove(sim, false);
            }
            move = null;
            vx = 0;
            EnterState(FighterStateId.Idle);
        }
        // Hitstun keeps its knockback slide and ends on its own; KO just lies there.
    }

    // ---------- getting hit ----------
    // GameSim decides whether the hit was blocked (hit.blocked) before anything changes, so two
    // hits on the same tick are treated the same way. The numbers come from the hit instance
    // (hit.damage...), which the attacker may have changed (ModifyOutgoingHit).
    public void TakeHit(GameSim sim, HitboxInstance hit)
    {
        int away = sim.fighters[hit.owner].x <= x ? 1 : -1;

        if (hit.blocked)
        {
            vx = away * hit.knockback * BlockPushPercent / 100;
            stunTicks = Math.Max(1, hit.hitstun * BlockstunPercent / 100);
            EnterState(FighterStateId.Blockstun);
            return;
        }

        hp -= hit.damage;
        if (hp <= 0 && !CanBeKnockedOut) hp = MaxHp;
        if (state == FighterStateId.Attack) EndMove(sim, false);
        move = null;
        holdTicks = 0;                     // being hit drops anyone you were holding
        holdThrow = null;
        sim.RemoveFollowHitboxes(index);   // being hit cancels your own melee hitboxes
        vx = away * hit.knockback;
        if (vy > 0) vy = 0;                // knocked out of the air: stop rising and fall from here

        if (hp <= 0)
        {
            hp = 0;
            EnterState(FighterStateId.KO);
            sim.AddEvent(SimEventType.KO, index, hit.owner, x, y, 0);
        }
        else
        {
            stunTicks = hit.hitstun;
            EnterState(FighterStateId.Hitstun);
        }
    }

    // ---------- held grabs (GameSim calls these) ----------
    // This fighter is pinned in front of the grabber.
    public void EnterHeld(GameSim sim, int atX)
    {
        if (state == FighterStateId.Attack) EndMove(sim, false);
        move = null;
        sim.RemoveFollowHitboxes(index);
        x = GameSim.ClampToArena(atX);
        vx = 0;
        vy = 0;
        EnterState(FighterStateId.Held);
    }

    // This fighter's held grab caught someone: stop the grab move and hold them for def.holdTicks.
    public void BeginHold(GameSim sim, HitboxDef def)
    {
        if (state == FighterStateId.Attack) EndMove(sim, false);
        move = null;
        vx = 0;
        holdTicks = def.holdTicks;
        holdThrow = def;
        EnterState(FighterStateId.Idle);
    }

    // The grabber let go without a hit (it was itself hit, or its follow-up missed).
    public void ReleaseHold()
    {
        if (state == FighterStateId.Held) EnterState(FighterStateId.Idle);
    }

    // ---------- status effects (D-46). Always change them through these. ----------

    // Damage that isn't a hit: bleed, poison, leeches, a self-inflicted cost. With canKO false it
    // never takes the last HP (D-23). Returns the HP actually lost.
    public int TakeDamage(GameSim sim, int amount, bool canKO)
    {
        if (amount <= 0 || hp <= 0) return 0;
        int floor = canKO && CanBeKnockedOut ? 0 : 1;
        int lost = Math.Min(amount, hp - floor);
        if (lost <= 0) return 0;
        hp -= lost;
        sim.AddEvent(SimEventType.StatusDamage, index, -1, x, y, lost);
        if (hp <= 0)
        {
            hp = 0;
            move = null;
            EnterState(FighterStateId.KO);
            sim.AddEvent(SimEventType.KO, index, -1, x, y, 0);
        }
        return lost;
    }

    // Never above MaxHp. Returns the HP actually gained.
    public int Heal(GameSim sim, int amount)
    {
        if (amount <= 0 || hp <= 0) return 0;
        int gained = Math.Min(amount, MaxHp - hp);
        if (gained <= 0) return 0;
        hp += gained;
        sim.AddEvent(SimEventType.Heal, index, -1, x, y, gained);
        return gained;
    }

    // One slow at a time: a new one only replaces the current one if it lasts longer (D-46).
    public void ApplySlow(int percent, int ticks)
    {
        if (IgnoresStatus(StatusKind.Slow)) return;
        if (ticks <= slowTicks) return;
        slowTicks = ticks;
        slowPercent = percent;
    }

    // One more bleed stack (max MaxBleedStacks). Each new stack restarts the fade timer.
    public void AddBleed()
    {
        if (bleedStacks < MaxBleedStacks) bleedStacks++;
        bleedFadeTimer = 0;
    }

    // At full bleed this fighter's hits are weaker (unless it ignores that, like Room).
    public bool HasBleedWeakness
    {
        get { return bleedStacks >= MaxBleedStacks && !IgnoresStatus(StatusKind.BleedWeakness); }
    }

    // Shuffled controls for `ticks`. The shuffle comes from the match's seeded roll, so both
    // machines get the same one (D-47).
    public void ApplyScramble(GameSim sim, int ticks)
    {
        if (IgnoresStatus(StatusKind.Scramble)) return;
        scrambleTicks = ticks;
        scrambleCode = 1 + (int)(sim.Roll(index * 7919 + 17) % 23u);   // 1..23: never the unshuffled order
    }

    // Leeches drain 1 HP every LeechInterval ticks and heal `owner` by what they took.
    public void ApplyLeech(int owner, int ticks)
    {
        leechOwner = owner;
        leechTicks = ticks;
        leechTimer = 0;
    }

    // Once per tick, from GameSim (after hits).
    public void TickStatuses(GameSim sim)
    {
        if (hp <= 0) return;
        if (slowTicks > 0) slowTicks--;
        if (scrambleTicks > 0) scrambleTicks--;

        if (bleedStacks > 0)
        {
            bleedDamageTimer++;
            if (bleedDamageTimer >= BleedTickInterval)
            {
                bleedDamageTimer = 0;
                TakeDamage(sim, bleedStacks, false);
            }
            bleedFadeTimer++;
            if (bleedFadeTimer >= BleedFadeInterval)
            {
                bleedFadeTimer = 0;
                bleedStacks--;
                if (bleedStacks == 0) bleedDamageTimer = 0;
            }
        }

        if (leechTicks > 0)
        {
            leechTicks--;
            leechTimer++;
            if (leechTimer >= LeechInterval)
            {
                leechTimer = 0;
                int taken = TakeDamage(sim, 1, false);
                if (leechOwner >= 0) sim.fighters[leechOwner].Heal(sim, taken);
            }
            if (leechTicks == 0) leechOwner = -1;
        }

        for (int i = 0; i < sim.zones.Count; i++)
        {
            Zone z = sim.zones[i];
            if (z.owner == index || z.enemyDamage <= 0 || z.damageInterval <= 0) continue;
            if (z.Covers(this) && z.age % z.damageInterval == 0) TakeDamage(sim, z.enemyDamage, false);
        }
    }

    // Left and right swap, and Light / Heavy / Grab / Special are shuffled among themselves.
    // Jump, Down and Ultimate stay where they are.
    static readonly int[] ShuffledButtons = { Buttons.Light, Buttons.Heavy, Buttons.Grab, Buttons.Special };
    readonly int[] scrambleOrder = new int[4];

    int Scramble(int input)
    {
        if (scrambleTicks <= 0) return input;

        int output = input & ~(Buttons.Left | Buttons.Right | Buttons.Light | Buttons.Heavy | Buttons.Grab | Buttons.Special);
        if ((input & Buttons.Left) != 0) output |= Buttons.Right;
        if ((input & Buttons.Right) != 0) output |= Buttons.Left;

        // scrambleCode picks one of the 24 orders of the four buttons (a factorial number).
        int[] order = scrambleOrder;   // reused, so a scramble allocates nothing per tick
        for (int i = 0; i < 4; i++) order[i] = i;
        int code = scrambleCode;
        for (int i = 0; i < 3; i++)
        {
            int radix = 4 - i;
            int pick = i + code % radix;
            code /= radix;
            int t = order[i]; order[i] = order[pick]; order[pick] = t;
        }
        for (int i = 0; i < 4; i++)
        {
            if ((input & ShuffledButtons[i]) != 0) output |= ShuffledButtons[order[i]];
        }
        return output;
    }

    // For GameSim.Hash: every cooldown slot.
    public int HashCooldowns()
    {
        unchecked
        {
            int h = dashCooldown;
            for (int i = 0; i < CooldownSlots; i++)
            {
                h = h * 31 + cooldownTicks[i];
                h = h * 31 + GameSim.MoveFingerprint(cooldownMoves[i]);
            }
            return h;
        }
    }
}
