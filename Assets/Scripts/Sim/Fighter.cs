using System;

// New states go at the END so the numbers of the old ones (used in the desync hash) stay the same.
public enum FighterStateId { Idle, Walk, Attack, Hitstun, KO, Blockstun }

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

    // ---------- fill these in per character ----------
    public abstract int MaxHp { get; }
    public abstract int WalkSpeed { get; }               // milli-units per tick
    // Which move does this input start? null = none. `sim` is there so a character can check the
    // match (for example "is my fireball still on screen?"). Read it, never change it here.
    public abstract MoveDef ChooseMove(GameSim sim, int input);

    // Every move this character can do. NOT used by the fight; editor tools read it, e.g. to make
    // one Animator state per move. If you add moves outside LightAttack/HeavyAttack, list them too.
    public virtual MoveDef[] ListMoves() { return new MoveDef[0]; }

    // The body. Override to make a character taller / shorter, or change it while crouching.
    public virtual Box Hurtbox
    {
        get { return new Box(-500, 0, 1000, 2000); }
    }

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
        holdingBack = false;
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
        stateTick++;
        OnTick(sim);

        switch (state)
        {
            case FighterStateId.Idle:
            case FighterStateId.Walk:
                ThinkGrounded(sim, input);
                break;

            case FighterStateId.Attack:
                ThinkAttack(sim);
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
    }

    void ThinkGrounded(GameSim sim, int input)
    {
        Fighter other = sim.fighters[1 - index];
        facing = x <= other.x ? 1 : -1;

        MoveDef chosen = ChooseMove(sim, input);
        if (chosen != null)
        {
            move = chosen;
            vx = 0;
            EnterState(FighterStateId.Attack);
            sim.AddEvent(SimEventType.AttackStart, index, -1, x, y, 0);
            return;
        }

        int direction = DirectionFrom(input);

        vx = direction * WalkSpeed;
        FighterStateId wanted = vx != 0 ? FighterStateId.Walk : FighterStateId.Idle;
        if (state != wanted) EnterState(wanted);
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
        x += vx;
        x = Math.Max(-GameSim.ArenaHalf, Math.Min(GameSim.ArenaHalf, x));
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
