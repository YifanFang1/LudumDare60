using System;

public enum FighterStateId { Idle, Walk, Attack, Hitstun, KO }

// Base class for every character. It owns the state machine, movement and getting hit.
// A new character = a new subclass that fills in the abstract members (see Brawler.cs).
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
    public int hitstunTicks;
    public MoveDef move;            // the move being performed while state == Attack

    // ---------- fill these in per character ----------
    public abstract int MaxHp { get; }
    public abstract int WalkSpeed { get; }               // milli-units per tick
    public abstract MoveDef ChooseMove(int input);       // which move does this input start? null = none

    // The body. Override to make a character taller / shorter, or change it while crouching.
    public virtual Box Hurtbox
    {
        get { return new Box(-500, 0, 1000, 2000); }
    }

    // ---------- optional hooks for theme mechanics / modifiers ----------
    public virtual void OnTick(GameSim sim) { }
    public virtual void OnHitDealt(GameSim sim, Fighter victim, HitboxInstance hit) { }
    public virtual void OnHitReceived(GameSim sim, Fighter attacker, HitboxInstance hit) { }

    // If your character has extra state that affects the fight (a combo counter, a charge meter,
    // a cooldown), mix it in here so the desync check can see it:  return meter * 31 + cooldown;
    public virtual int HashExtra() { return 0; }

    // ---------- helpers ----------
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
        hitstunTicks = 0;
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
                if (stateTick >= hitstunTicks - 1)
                {
                    vx = 0;
                    EnterState(FighterStateId.Idle);
                }
                break;

            case FighterStateId.KO:
                vx = 0;
                break;
        }
    }

    void ThinkGrounded(GameSim sim, int input)
    {
        Fighter other = sim.fighters[1 - index];
        facing = x <= other.x ? 1 : -1;

        MoveDef chosen = ChooseMove(input);
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
    public void TakeHit(GameSim sim, HitboxInstance hit)
    {
        hp -= hit.def.damage;
        move = null;
        sim.RemoveFollowHitboxes(index);   // being hit cancels your own melee hitboxes

        int away = sim.fighters[hit.owner].x <= x ? 1 : -1;
        vx = away * hit.def.knockback;

        if (hp <= 0)
        {
            hp = 0;
            EnterState(FighterStateId.KO);
            sim.AddEvent(SimEventType.KO, index, hit.owner, x, y, 0);
        }
        else
        {
            hitstunTicks = hit.def.hitstun;
            EnterState(FighterStateId.Hitstun);
        }
    }
}
