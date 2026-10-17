using System;

// New states go at the END so the numbers of the old ones (used in the desync hash) stay the same.
public enum FighterStateId { Idle, Walk, Attack, Hitstun, KO, Blockstun, Dash }

// The engine-level base class for every character. It owns the state machine, movement,
// blocking and getting hit. You normally do NOT subclass this directly:
//   - code characters extend BaseFighter (Characters/BaseFighter.cs), the character template
//   - inspector characters are played by DataFighter
// See Characters/CHARACTERS.md for the full guide.
//
// BLOCKING: hold the direction AWAY from the opponent while standing or walking. A blocked hit
// does no damage, still pushes you back, and puts you in Blockstun (shorter than hitstun) where
// you can't act but keep blocking. Blocking works the same for every character, so it lives here.
//
// DASHING: tap a direction twice within DashTapWindow ticks (a "fresh" press each time: the key
// was up on the tick before). Toward the opponent = forward dash, away = back dash. A dash can't
// block or attack until it ends. Characters change their dashes with ForwardDash / BackDash.
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
    public int y;                   // always 0 (no jumping); kept so boxes and events still have a height
    public int vx;                  // milli-units per tick
    public int hp;
    public int facing = 1;          // 1 = facing right, -1 = facing left
    public FighterStateId state;
    public int stateTick;
    public int stunTicks;           // how long the current Hitstun or Blockstun lasts
    public bool holdingBack;        // this tick's input holds away from the opponent (see IsBlocking)
    public MoveDef move;            // the move being performed while state == Attack
    public int dashTicks;           // how long the current Dash lasts
    public int meter;               // 0..MeterMax. A full bar allows the Ultimate. Carries over between rounds.

    // Input history, so the fighter can tell a fresh press from a held key (dashes, the Ultimate).
    public int prevInput;           // last tick's input, after FilterInput
    public int lastTapDir;          // Buttons.Left or Buttons.Right: the last fresh direction press (0 = none)
    public int lastTapTick;         // GameSim.tick of that press
    protected int pressedInput;     // bits pressed THIS tick that were up last tick. Worked out from
                                    // prevInput at the start of Think, so it needs no hashing.

    // ---------- fill these in per character ----------
    public abstract int MaxHp { get; }
    public abstract int WalkSpeed { get; }               // milli-units per tick
    // Which move does this input start? null = none. `sim` is there so a character can check the
    // match (for example "is my fireball still on screen?"). Read it, never change it here.
    public abstract MoveDef ChooseMove(GameSim sim, int input);

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

    // If your character has extra state that affects the fight (a combo counter, a charge meter,
    // a cooldown), mix it in here so the desync check can see it:  return meter * 31 + cooldown;
    public virtual int HashExtra() { return 0; }

    // Blocking tuning, shared by every character.
    public const int BlockstunPercent = 60;    // blockstun = this % of the move's hitstun
    public const int BlockPushPercent = 100;   // pushback on block = this % of the move's knockback

    // The second tap of a double-tap must come at most this many ticks after the first.
    public const int DashTapWindow = 12;

    // Ultimate meter, shared by every character (Design/DECISIONS.md D-08). With 100 HP that is
    // roughly one full bar per round. Hits from an Ultimate give nothing.
    public const int MeterMax = 1000;
    public const int MeterPerDamageDealt = 7;     // landing a hit
    public const int MeterPerDamageTaken = 4;     // getting hit (a comeback for the losing player)
    public const int MeterPerDamageBlocked = 2;   // a blocked hit, for BOTH players

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
            return canBlockInState && holdingBack;
        }
    }

    public Box HurtboxWorld()
    {
        return Hurtbox.ToWorld(x, y, facing);
    }

    public void Init(int fighterIndex, int startX)
    {
        index = fighterIndex;
        x = startX;
        y = 0;
        vx = 0;
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
        input = FilterInput(input);
        pressedInput = input & ~prevInput;
        int doubleTap = TrackTaps(sim);   // recorded in every state, but only used from Idle/Walk
        stateTick++;
        OnTick(sim);

        switch (state)
        {
            case FighterStateId.Idle:
            case FighterStateId.Walk:
                ThinkGrounded(sim, input, doubleTap);
                break;

            case FighterStateId.Attack:
                ThinkAttack(sim);
                break;

            case FighterStateId.Dash:
                if (stateTick >= dashTicks - 1)
                {
                    vx = 0;
                    EnterState(FighterStateId.Idle);
                }
                break;

            case FighterStateId.Hitstun:
            case FighterStateId.Blockstun:
                if (stateTick >= stunTicks - 1)
                {
                    vx = 0;
                    EnterState(FighterStateId.Idle);
                }
                break;

            case FighterStateId.KO:
                vx = 0;
                break;
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
        // Safety net for every character: an Ultimate needs a full bar, whatever ChooseMove says.
        if (chosen != null && chosen.ultimate && meter < MeterMax) chosen = null;
        if (chosen != null)
        {
            move = chosen;
            vx = 0;
            EnterState(FighterStateId.Attack);
            sim.AddEvent(SimEventType.AttackStart, index, -1, x, y, 0);
            if (chosen.ultimate)
            {
                meter = 0;
                sim.StartSuperFlash(index);
            }
            return;
        }

        if (doubleTap != 0)
        {
            StartDash(sim, doubleTap == Buttons.Right ? 1 : -1);
            return;
        }

        int direction = DirectionFrom(input);

        vx = direction * WalkSpeed;
        FighterStateId wanted = vx != 0 ? FighterStateId.Walk : FighterStateId.Idle;
        if (state != wanted) EnterState(wanted);
    }

    // `worldDirection` is +1 (right) or -1 (left); toward the opponent is a forward dash.
    void StartDash(GameSim sim, int worldDirection)
    {
        bool forward = worldDirection == facing;
        DashDef dash = forward ? ForwardDash : BackDash;
        dashTicks = Math.Max(1, dash.ticks);
        vx = worldDirection * dash.speed;
        EnterState(FighterStateId.Dash);
        sim.AddEvent(SimEventType.Dash, index, -1, x, y, forward ? 1 : 0);
    }

    // True while dashing toward the opponent (for the view: DashForward vs DashBack pose).
    public bool IsDashingForward
    {
        get { return state == FighterStateId.Dash && (vx > 0) == (facing > 0); }
    }

    void ThinkAttack(GameSim sim)
    {
        // Summon every hitbox that is due on this tick of the move.
        for (int i = 0; i < move.hitboxes.Length; i++)
        {
            if (move.hitboxes[i].spawnTick == stateTick)
            {
                sim.SpawnHitbox(index, move.hitboxes[i]);
            }
        }

        if (stateTick >= move.totalTicks - 1)
        {
            move = null;
            EnterState(FighterStateId.Idle);
        }
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
    }

    // ---------- getting hit ----------
    // GameSim decides whether the hit was blocked (hit.blocked) before anything changes, so two
    // hits on the same tick are treated the same way.
    public void TakeHit(GameSim sim, HitboxInstance hit)
    {
        int away = sim.fighters[hit.owner].x <= x ? 1 : -1;

        if (hit.blocked)
        {
            vx = away * hit.def.knockback * BlockPushPercent / 100;
            stunTicks = Math.Max(1, hit.def.hitstun * BlockstunPercent / 100);
            EnterState(FighterStateId.Blockstun);
            return;
        }

        hp -= hit.def.damage;
        if (hp <= 0 && !CanBeKnockedOut) hp = MaxHp;
        move = null;
        sim.RemoveFollowHitboxes(index);   // being hit cancels your own melee hitboxes
        vx = away * hit.def.knockback;

        if (hp <= 0)
        {
            hp = 0;
            EnterState(FighterStateId.KO);
            sim.AddEvent(SimEventType.KO, index, hit.owner, x, y, 0);
        }
        else
        {
            stunTicks = hit.def.hitstun;
            EnterState(FighterStateId.Hitstun);
        }
    }
}
