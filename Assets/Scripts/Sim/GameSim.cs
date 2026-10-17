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

    public Fighter[] fighters = new Fighter[2];
    public List<HitboxInstance> hitboxes = new List<HitboxInstance>();
    public List<SimEvent> events = new List<SimEvent>();   // what happened THIS tick, for the client
    public int tick;
    public int seed;

    List<HitboxInstance> hitsThisTick = new List<HitboxInstance>();

    public bool IsOver
    {
        get { return fighters[0].hp <= 0 || fighters[1].hp <= 0; }
    }

    // -1 = nobody yet, 0 = player 1, 1 = player 2, 2 = draw
    public int Winner
    {
        get
        {
            bool dead0 = fighters[0].hp <= 0;
            bool dead1 = fighters[1].hp <= 0;
            if (dead0 && dead1) return 2;
            if (dead0) return 1;
            if (dead1) return 0;
            return -1;
        }
    }

    // The sim knows nothing about which characters exist; whoever starts the match builds them
    // (see CharacterList in the Characters folder).
    public void Reset(int newSeed, Fighter p0, Fighter p1)
    {
        seed = newSeed;
        tick = 0;
        hitboxes.Clear();
        events.Clear();
        fighters[0] = p0;
        fighters[1] = p1;
        p0.Init(0, -3000);
        p1.Init(1, 3000);
    }

    public void Step(int in0, int in1)
    {
        events.Clear();
        if (IsOver) return;

        // 1. both fighters decide (nobody has moved yet, so neither has an advantage)
        fighters[0].Think(this, in0);
        fighters[1].Think(this, in1);

        // 2. both fighters move
        fighters[0].Move(this);
        fighters[1].Move(this);

        // 3. hitboxes vs bodies, then hitboxes age
        ResolveHits();
        AgeHitboxes();

        tick++;
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
        hitboxes.Add(h);
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

            Box hitWorld = h.World(this);
            Box hurtWorld = victim.HurtboxWorld();
            if (hitWorld.Overlaps(hurtWorld))
            {
                h.hasHit = true;
                int cx, cy;
                hitWorld.OverlapCenter(hurtWorld, out cx, out cy);
                AddEvent(SimEventType.Hit, h.owner, 1 - h.owner, cx, cy, h.def.damage);
                hitsThisTick.Add(h);
            }
        }

        // Pass 2: apply them.
        for (int i = 0; i < hitsThisTick.Count; i++)
        {
            HitboxInstance h = hitsThisTick[i];
            Fighter attacker = fighters[h.owner];
            Fighter victim = fighters[1 - h.owner];
            victim.TakeHit(this, h);
            attacker.OnHitDealt(this, victim, h);
            victim.OnHitReceived(this, attacker, h);
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
            for (int i = 0; i < 2; i++)
            {
                Fighter f = fighters[i];
                h = h * 31 + f.x;
                h = h * 31 + f.vx;
                h = h * 31 + f.hp;
                h = h * 31 + (int)f.state;
                h = h * 31 + f.stateTick;
                h = h * 31 + f.hitstunTicks;
                h = h * 31 + f.HashExtra();
            }
            h = h * 31 + hitboxes.Count;
            for (int i = 0; i < hitboxes.Count; i++)
            {
                h = h * 31 + hitboxes[i].lifetime;
                h = h * 31 + hitboxes[i].originX;
            }
            return h;
        }
    }
}
