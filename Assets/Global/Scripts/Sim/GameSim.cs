using System;
using System.Collections.Generic;

// The deterministic fight simulation.
//
// RULES (this is what keeps both players in sync):
//   - only ints. No float, no Time.deltaTime, no Unity physics, no UnityEngine.Random.
//   - the ONLY thing that changes the state is Step(input0, input1).
//   - positions are in thousandths of a unit ("milli-units"), so there is no float drift.
//
// The LD.Sim.asmdef next to this file has "No Engine References" ticked, so using UnityEngine
// in this folder is a compile error. That is on purpose.
public class GameSim
{
    public const int ArenaHalf = 8000;
    public const int BackThrowDistance = 900;   // where a back throw puts the victim, behind the grabber

    public static int ClampToArena(int x)
    {
        return Math.Max(-ArenaHalf, Math.Min(ArenaHalf, x));
    }

    public Fighter[] fighters = new Fighter[2];
    public List<HitboxInstance> hitboxes = new List<HitboxInstance>();
    public List<SimEvent> events = new List<SimEvent>();   // what happened THIS tick, for the client
    public int tick;
    public int seed;

    // SUPERFLASH: when an Ultimate starts, the whole match freezes for this many ticks (nobody
    // thinks or moves, hitboxes don't age, input is ignored) while the view plays a flash.
    public const int SuperFlashTicks = 30;
    public int freezeTicks;

    // ROUNDS: best of 3. A KO ends the round; after a short pause (the KO pose plays, nothing can
    // be hit) the next round starts with FRESH fighters built by the factories given to Reset.
    // Health, positions and hitboxes reset; the Ultimate meter carries over. A double KO gives
    // both players the round, and if that makes both reach RoundsToWin the match is a draw.
    public const int RoundsToWin = 2;
    public const int RoundEndDelay = 120;   // ticks between the KO and the next round
    public const int StartX = 3000;         // fighters start at -StartX and +StartX

    public int round;                       // 1, 2, 3
    public int[] roundsWon = new int[2];
    public int roundOverTicks;              // > 0 during the pause after a KO
    Func<Fighter>[] makers = new Func<Fighter>[2];

    List<HitboxInstance> hitsThisTick = new List<HitboxInstance>();

    // True once someone has won the match (not just a round).
    public bool IsOver
    {
        get { return roundsWon[0] >= RoundsToWin || roundsWon[1] >= RoundsToWin; }
    }

    // -1 = nobody yet, 0 = player 1, 1 = player 2, 2 = draw
    public int Winner
    {
        get
        {
            if (!IsOver) return -1;
            bool won0 = roundsWon[0] >= RoundsToWin;
            bool won1 = roundsWon[1] >= RoundsToWin;
            if (won0 && won1) return 2;
            return won0 ? 0 : 1;
        }
    }

    // The sim knows nothing about which characters exist; whoever starts the match passes a
    // recipe for each player (CharacterList.Factory in the Characters folder). It is called again
    // at the start of every round, so it must return a NEW fighter each time.
    public void Reset(int newSeed, Func<Fighter> make0, Func<Fighter> make1)
    {
        seed = newSeed;
        tick = 0;
        makers[0] = make0;
        makers[1] = make1;
        roundsWon[0] = 0;
        roundsWon[1] = 0;
        events.Clear();
        StartRound(1, false);
    }

    void StartRound(int number, bool keepMeter)
    {
        int meter0 = keepMeter ? fighters[0].meter : 0;
        int meter1 = keepMeter ? fighters[1].meter : 0;

        round = number;
        roundOverTicks = 0;
        freezeTicks = 0;
        hitboxes.Clear();
        fighters[0] = makers[0]();
        fighters[1] = makers[1]();
        fighters[0].Init(0, -StartX);
        fighters[1].Init(1, StartX);
        fighters[0].meter = meter0;
        fighters[1].meter = meter1;

        if (number > 1) AddEvent(SimEventType.RoundStart, -1, -1, 0, 0, number);
    }

    // Called at the end of a tick in which somebody reached 0 HP.
    void EndRound()
    {
        bool dead0 = fighters[0].hp <= 0;
        bool dead1 = fighters[1].hp <= 0;
        if (dead1) roundsWon[0]++;
        if (dead0) roundsWon[1]++;

        int roundWinner = dead0 && dead1 ? 2 : (dead1 ? 0 : 1);
        AddEvent(SimEventType.RoundEnd, roundWinner, -1, 0, 0, round);
        if (!IsOver) roundOverTicks = RoundEndDelay;
    }

    public void Step(int in0, int in1)
    {
        events.Clear();
        if (IsOver) return;

        if (roundOverTicks > 0)
        {
            StepRoundOver();
            tick++;
            return;
        }

        if (freezeTicks > 0)
        {
            freezeTicks--;
            tick++;
            return;
        }

        // 1. both fighters decide (nobody has moved yet, so neither has an advantage)
        fighters[0].Think(this, in0);
        fighters[1].Think(this, in1);

        // 2. both fighters move, then bodies that overlap are pushed apart
        fighters[0].Move(this);
        fighters[1].Move(this);
        SeparateBodies();

        // 3. hitboxes vs bodies, then hitboxes age
        ResolveHits();
        AgeHitboxes();

        // 4. a KO ends the round
        if (fighters[0].hp <= 0 || fighters[1].hp <= 0) EndRound();

        tick++;
    }

    // The pause after a KO: fighters finish what they were doing (knockback slides, the KO pose
    // plays) with no input, and nothing can be hit, so a projectile still in the air can't hurt
    // the winner after the round is decided. Then the next round starts.
    void StepRoundOver()
    {
        fighters[0].Think(this, 0);
        fighters[1].Think(this, 0);
        fighters[0].Move(this);
        fighters[1].Move(this);
        SeparateBodies();
        AgeHitboxes();

        roundOverTicks--;
        if (roundOverTicks == 0) StartRound(round + 1, true);
    }

    // Summon a hitbox. Fighters call this from their moves.
    public void SpawnHitbox(int owner, HitboxDef def)
    {
        Fighter o = fighters[owner];
        HitboxInstance h = new HitboxInstance();
        h.owner = owner;
        h.def = def;
        h.originX = o.x;
        h.originY = o.y;
        h.facing = o.facing;
        h.lifetime = def.lifetime;
        h.fromUltimate = o.move != null && o.move.ultimate;
        hitboxes.Add(h);
    }

    // Freeze the match for SuperFlashTicks (see freezeTicks). Fighter calls this when an
    // Ultimate starts; `owner` is who started it.
    public void StartSuperFlash(int owner)
    {
        freezeTicks = SuperFlashTicks;
        Fighter o = fighters[owner];
        AddEvent(SimEventType.SuperFlash, owner, -1, o.x, o.y, 0);
    }

    public void RemoveFollowHitboxes(int owner)
    {
        for (int i = hitboxes.Count - 1; i >= 0; i--)
        {
            if (hitboxes[i].owner == owner && hitboxes[i].def.followOwner)
            {
                hitboxes.RemoveAt(i);
            }
        }
    }

    public void AddEvent(SimEventType type, int a, int b, int x, int y, int amount)
    {
        SimEvent e = new SimEvent();
        e.type = type;
        e.a = a;
        e.b = b;
        e.x = x;
        e.y = y;
        e.amount = amount;
        events.Add(e);
    }

    // PUSHBOXES: fighters can't stand inside each other. If their bodies (hurtboxes) overlap along
    // x, both are pushed apart by half the overlap; a fighter against the arena edge can't move,
    // so the other one takes the rest. When both stand on exactly the same spot, fighter 0 goes
    // left: the tie-break must never depend on anything but the state, or the two machines differ.
    void SeparateBodies()
    {
        Box a = fighters[0].HurtboxWorld();
        Box b = fighters[1].HurtboxWorld();

        // Doubled centres, so there is no rounding from dividing by 2.
        bool zeroIsLeft = a.x * 2 + a.w <= b.x * 2 + b.w;
        Fighter left = zeroIsLeft ? fighters[0] : fighters[1];
        Fighter right = zeroIsLeft ? fighters[1] : fighters[0];
        Box leftBox = zeroIsLeft ? a : b;
        Box rightBox = zeroIsLeft ? b : a;

        int push = (leftBox.x + leftBox.w) - rightBox.x;   // how far they must move apart in total
        if (push <= 0) return;

        int leftMove = push / 2;
        int rightMove = push - leftMove;
        int leftRoom = left.x + ArenaHalf;     // how far the left fighter can still go left
        int rightRoom = ArenaHalf - right.x;   // how far the right fighter can still go right
        if (leftMove > leftRoom)
        {
            rightMove += leftMove - leftRoom;
            leftMove = leftRoom;
        }
        if (rightMove > rightRoom)
        {
            leftMove = Math.Min(leftRoom, leftMove + rightMove - rightRoom);
            rightMove = rightRoom;
        }
        left.x -= leftMove;
        right.x += rightMove;
    }

    void ResolveHits()
    {
        // Pass 1: find every hit without changing anything, so both players can trade hits.
        hitsThisTick.Clear();
        for (int i = 0; i < hitboxes.Count; i++)
        {
            HitboxInstance h = hitboxes[i];
            if (h.hasHit) continue;

            Fighter victim = fighters[1 - h.owner];
            if (victim.state == FighterStateId.KO) continue;
            // Grab rule: a fighter in hitstun or blockstun can't be grabbed. The grab hitbox stays
            // alive, so it still catches them if they recover before it runs out.
            if (h.def.grab && (victim.state == FighterStateId.Hitstun || victim.state == FighterStateId.Blockstun)) continue;

            Box hitWorld = h.World(this);
            Box hurtWorld = victim.HurtboxWorld();
            if (hitWorld.Overlaps(hurtWorld))
            {
                h.hasHit = true;
                h.blocked = !h.def.unblockable && !h.def.grab && victim.IsBlocking;
                int cx, cy;
                hitWorld.OverlapCenter(hurtWorld, out cx, out cy);
                if (h.blocked) AddEvent(SimEventType.Block, h.owner, 1 - h.owner, cx, cy, 0);
                else if (h.def.grab) AddEvent(SimEventType.Grab, h.owner, 1 - h.owner, cx, cy, h.def.damage);
                else AddEvent(SimEventType.Hit, h.owner, 1 - h.owner, cx, cy, h.def.damage);
                hitsThisTick.Add(h);
            }
        }

        // Pass 2: apply them.
        for (int i = 0; i < hitsThisTick.Count; i++)
        {
            HitboxInstance h = hitsThisTick[i];
            Fighter attacker = fighters[h.owner];
            Fighter victim = fighters[1 - h.owner];
            // Back throw: a grab that lands while the grabber holds back puts the victim behind
            // the grabber first, so TakeHit's knockback then sends them the other way.
            if (h.def.grab && attacker.holdingBack)
            {
                victim.x = ClampToArena(attacker.x - attacker.facing * BackThrowDistance);
            }
            victim.TakeHit(this, h);
            GiveMeter(attacker, victim, h);
            attacker.OnHitDealt(this, victim, h);
            victim.OnHitReceived(this, attacker, h);
        }
    }

    // Ultimate meter for a hit that connected (D-08 in Design/DECISIONS.md).
    static void GiveMeter(Fighter attacker, Fighter victim, HitboxInstance h)
    {
        if (h.fromUltimate) return;
        int damage = h.def.damage;
        if (h.blocked)
        {
            attacker.AddMeter(damage * Fighter.MeterPerDamageBlocked);
            victim.AddMeter(damage * Fighter.MeterPerDamageBlocked);
        }
        else
        {
            attacker.AddMeter(damage * Fighter.MeterPerDamageDealt);
            victim.AddMeter(damage * Fighter.MeterPerDamageTaken);
        }
    }

    void AgeHitboxes()
    {
        for (int i = hitboxes.Count - 1; i >= 0; i--)
        {
            HitboxInstance h = hitboxes[i];
            if (!h.def.followOwner) h.originX += h.facing * h.def.vx;
            h.lifetime--;
            if (h.lifetime <= 0 || h.hasHit) hitboxes.RemoveAt(i);
        }
    }

    // A number that summarises the whole state. Both players compare these to catch desyncs.
    // (Events are NOT included on purpose: they are output, not state.)
    public int Hash()
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + tick;
            h = h * 31 + freezeTicks;
            h = h * 31 + round;
            h = h * 31 + roundsWon[0];
            h = h * 31 + roundsWon[1];
            h = h * 31 + roundOverTicks;
            for (int i = 0; i < 2; i++)
            {
                Fighter f = fighters[i];
                h = h * 31 + f.x;
                h = h * 31 + f.vx;
                h = h * 31 + f.hp;
                h = h * 31 + (int)f.state;
                h = h * 31 + f.stateTick;
                h = h * 31 + f.stunTicks;
                h = h * 31 + (f.holdingBack ? 1 : 0);
                h = h * 31 + f.facing;
                h = h * 31 + MoveFingerprint(f.move);
                h = h * 31 + f.dashTicks;
                h = h * 31 + f.meter;
                h = h * 31 + f.prevInput;
                h = h * 31 + f.lastTapDir;
                h = h * 31 + f.lastTapTick;
                h = h * 31 + f.HashExtra();
            }
            h = h * 31 + hitboxes.Count;
            for (int i = 0; i < hitboxes.Count; i++)
            {
                h = h * 31 + hitboxes[i].owner;
                h = h * 31 + hitboxes[i].lifetime;
                h = h * 31 + hitboxes[i].originX;
            }
            return h;
        }
    }

    // Which move a fighter is doing, as a number for the hash. Built from the move's numbers, not
    // from name.GetHashCode(): string hashes are not guaranteed to match between the Editor (Mono)
    // and a WebGL build (IL2CPP), and the two players may be on different ones.
    static int MoveFingerprint(MoveDef move)
    {
        if (move == null) return 0;
        unchecked
        {
            int h = move.totalTicks * 31 + (move.ultimate ? 1 : 0);
            for (int i = 0; i < move.hitboxes.Length; i++)
            {
                HitboxDef d = move.hitboxes[i];
                h = h * 31 + d.spawnTick;
                h = h * 31 + d.damage;
                h = h * 31 + d.box.w;
            }
            return h;
        }
    }
}
