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
//
// ONE TICK (Step), in this order:
//   1. both fighters decide (Think)            5. hits: projectile vs projectile, Ultimate copies, fighters
//   2. Ultimate copies mimic their owners      6. status effects and ground zones tick
//   3. both fighters move, bodies push apart   7. hitboxes and zones age
//   4. held grabs count down (and throw)       8. a KO ends the round
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
    public List<Zone> zones = new List<Zone>();          // ground zones (Zone.cs)
    public List<Summon> summons = new List<Summon>();    // Ultimate copies (Summon.cs)
    public List<SimEvent> events = new List<SimEvent>();   // what happened THIS tick, for the client
    public int tick;
    public int seed;

    // Sessions enable this; isolated combat tests can start immediately.
    public bool countdownOn;
    // Only the first round introduces each fighter, before the normal countdown.
    public bool introductionsOn;
    public const int IntroLineTicks = 240;
    public int introTicks;
    public int introFirstSlot;
    public int introFirstTicks = IntroLineTicks;
    public int introSecondTicks = IntroLineTicks;
    public int introVariant;
    public int IntroSlot { get { return introTicks > introSecondTicks ? introFirstSlot : introTicks > 0 ? 1 - introFirstSlot : -1; } }
    public int IntroLineElapsedTicks
    {
        get { return introTicks > introSecondTicks ? introFirstTicks + introSecondTicks - introTicks : introSecondTicks - introTicks; }
    }
    public const int CountdownTicks = 180;
    public const int FightBannerTicks = 60;
    // ROUND CALL: "ROUND 1" / "FINAL ROUND" on its own, after the intros and before the 3-2-1, so the
    // announcer never talks over the dialogue. Sessions switch it on; tests that only want the
    // countdown leave it off. Nobody can act during it, like the countdown.
    public bool roundCallOn;
    public const int RoundCallTicks = 126;   // 2.1 seconds (Announcer_Round1 is 3.1 s, so it runs a little into the 3-2-1)
    public int roundCallTicks;
    public int countdownTicks;
    public int fightBannerTicks;
    public string CountdownText
    {
        get { return countdownTicks > 0 ? ((countdownTicks + 59) / 60).ToString()
            : fightBannerTicks > 0 ? "FIGHT!" : ""; }
    }

    // SUPERFLASH: when an Ultimate starts, the whole match freezes for this many ticks (nobody
    // thinks or moves, hitboxes don't age, input is ignored) while the view plays a flash.
    public const int SuperFlashTicks = 30;
    public int freezeTicks;

    // HIT STOP: when a hit connects, the whole match pauses for a few ticks so the hit feels heavy
    // (same as the superflash: nobody moves, hitboxes don't age, input is ignored). Bigger hits
    // pause longer; a KO pauses longest. Kept apart from freezeTicks so the view can tell them apart.
    public const int HitStopBase = 5;        // ticks for a 0-damage hit...
    public const int HitStopPerDamage = 3;   // ...plus 1 tick per this much damage
    public const int HitStopMax = 16;
    public const int BlockStop = 4;          // blocked hits pause briefly too
    public const int KOStop = 30;            // the hit that ends a round
    public int hitStopTicks;
    public bool hitStopOn = true;            // a rule, not state: tests switch it off to keep their tick counts simple

    // ROUNDS: best of 3. A KO ends the round; after a short pause (the KO pose plays, nothing can
    // be hit) the next round starts with FRESH fighters built by the factories given to Reset.
    // Health, positions and hitboxes reset; the Ultimate meter carries over. A double KO gives
    // both players the round, and if that makes both reach RoundsToWin the match is a draw.
    public const int RoundsToWin = 2;
    public const int RoundEndDelay = 120;   // ticks between the KO and the next round
    public const int StartX = 3000;         // fighters start at -StartX and +StartX

    // ROUND TIMER: a round lasts at most 80 seconds of live fighting (the countdown, hit stop, superflash
    // and the pause after a KO do not count). When it runs out, whoever has more health wins the round;
    // equal health is a draw for the round, scored like a double KO. Sessions switch it on like the
    // countdown; isolated combat tests leave it off.
    public const int RoundTimeTicks = 80 * 60;
    public bool roundTimerOn;
    public int roundTimeTicks;              // counts down from RoundTimeTicks
    public bool timeUp;                     // the round just ended on the clock, not a KO
    public int roundWinner = -1;            // who took the round that just ended: 0, 1, or 2 = draw; -1 while it is on

    public int round;                       // 1, 2, 3
    public int[] roundsWon = new int[2];
    public int roundOverTicks;              // > 0 during the pause after a KO
    Func<Fighter>[] makers = new Func<Fighter>[2];

    // Reused every tick so the sim allocates nothing per tick (WebGL's garbage collector is slow).
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
        introTicks = introductionsOn && number == 1 ? introFirstTicks + introSecondTicks : 0;
        roundTimeTicks = RoundTimeTicks;
        timeUp = false;
        roundWinner = -1;
        roundCallTicks = roundCallOn ? RoundCallTicks : 0;
        countdownTicks = countdownOn ? CountdownTicks : 0;
        fightBannerTicks = 0;
        freezeTicks = 0;
        hitStopTicks = 0;
        hitboxes.Clear();
        zones.Clear();
        summons.Clear();
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

        roundWinner = dead0 && dead1 ? 2 : (dead1 ? 0 : 1);
        AddEvent(SimEventType.RoundEnd, roundWinner, -1, 0, 0, round);
        if (!IsOver) roundOverTicks = RoundEndDelay;
    }

    // The clock ran out: more health wins the round, equal health is a draw (both are scored).
    void EndRoundOnTime()
    {
        timeUp = true;
        int hp0 = fighters[0].hp;
        int hp1 = fighters[1].hp;
        roundWinner = hp0 > hp1 ? 0 : (hp1 > hp0 ? 1 : 2);
        if (roundWinner != 1) roundsWon[0]++;
        if (roundWinner != 0) roundsWon[1]++;
        AddEvent(SimEventType.RoundEnd, roundWinner, -1, 0, 0, round);
        if (!IsOver) roundOverTicks = RoundEndDelay;
    }

    public void Step(int in0, int in1)
    {
        events.Clear();
        if (IsOver) return;

        // The intro consumes synchronized ticks too: neither player can act or run the clock.
        if (introTicks > 0)
        {
            introTicks--;
            tick++;
            return;
        }

        // Then the round call, then the countdown: input ticks are consumed without being applied.
        if (roundCallTicks > 0)
        {
            roundCallTicks--;
            tick++;
            return;
        }
        if (countdownTicks > 0)
        {
            countdownTicks--;
            if (countdownTicks == 0) fightBannerTicks = FightBannerTicks;
            tick++;
            return;
        }
        if (fightBannerTicks > 0) fightBannerTicks--;

        // Before the round-over pause, so the KO hit gets its hit stop too.
        if (hitStopTicks > 0)
        {
            hitStopTicks--;
            tick++;
            return;
        }

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

        if (roundTimerOn && roundTimeTicks > 0) roundTimeTicks--;

        // 1. both fighters decide (nobody has moved yet, so neither has an advantage)
        fighters[0].Think(this, in0);
        fighters[1].Think(this, in1);

        // 2. Ultimate copies do what their owners just chose
        StepSummons();

        // 3. both fighters move, then bodies that overlap are pushed apart
        fighters[0].Move(this);
        fighters[1].Move(this);
        SeparateBodies();

        // 4. held grabs count down; one that runs out throws
        StepHolds();

        // 5. hitboxes vs projectiles, copies and bodies
        ResolveHits();

        // 6. status effects (bleed, leeches, poison...), 7. everything ages
        fighters[0].TickStatuses(this);
        fighters[1].TickStatuses(this);
        AgeZones();
        AgeHitboxes();

        // 8. a KO ends the round
        if (fighters[0].hp <= 0 || fighters[1].hp <= 0) EndRound();
        else if (roundTimerOn && roundTimeTicks <= 0) EndRoundOnTime();

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

    // ================= hitboxes =================

    // Summon a hitbox. Fighters call this from their moves. `lifetime` overrides the def's
    // (a dash's hitbox lasts as long as the dash).
    public HitboxInstance SpawnHitbox(int owner, HitboxDef def)
    {
        return SpawnHitbox(owner, def, def.lifetime);
    }

    public HitboxInstance SpawnHitbox(int owner, HitboxDef def, int lifetime)
    {
        Fighter o = fighters[owner];
        bool fromUltimate = o.move != null && o.move.ultimate;
        return AddHitbox(owner, def, o.x, o.y, o.facing, lifetime, false, fromUltimate);
    }

    // A hitbox placed at a spot in the world (a trap, a falling blade) instead of following its
    // owner. `facing` flips its box like a fighter's would.
    public HitboxInstance SpawnHitboxAt(int owner, HitboxDef def, int worldX, int facing)
    {
        Fighter o = fighters[owner];
        bool fromUltimate = o.move != null && o.move.ultimate;
        return AddHitbox(owner, def, worldX, 0, facing, def.lifetime, true, fromUltimate);
    }

    HitboxInstance AddHitbox(int owner, HitboxDef def, int x, int y, int facing, int lifetime, bool placed, bool fromUltimate)
    {
        HitboxInstance h = new HitboxInstance();
        h.owner = owner;
        h.def = def;
        h.originX = x;
        h.originY = y;
        h.vy = def.vy;
        h.facing = facing;
        h.lifetime = lifetime;
        h.placed = placed;
        h.fromUltimate = fromUltimate;
        h.damage = def.damage;
        h.hitstun = def.hitstun;
        h.knockback = def.knockback;
        h.box = def.box;
        hitboxes.Add(h);
        fighters[owner].OnHitboxSpawned(this, h);
        return h;
    }

    // How many hitboxes made from `def` this fighter has out right now.
    public int CountHitboxes(int owner, HitboxDef def)
    {
        int count = 0;
        for (int i = 0; i < hitboxes.Count; i++)
        {
            if (hitboxes[i].owner == owner && hitboxes[i].def == def && !hitboxes[i].hasHit) count++;
        }
        return count;
    }

    // Removes the oldest hitbox made from `def` (e.g. the 3rd trap replaces the 1st).
    public void RemoveOldestHitbox(int owner, HitboxDef def)
    {
        for (int i = 0; i < hitboxes.Count; i++)
        {
            if (hitboxes[i].owner == owner && hitboxes[i].def == def)
            {
                hitboxes.RemoveAt(i);
                return;
            }
        }
    }

    // Freeze the match for SuperFlashTicks (see freezeTicks). Fighter calls this when an
    // Ultimate starts; `owner` is who started it.
    public void StartSuperFlash(int owner)
    {
        freezeTicks = SuperFlashTicks;
        Fighter o = fighters[owner];
        AddEvent(SimEventType.SuperFlash, owner, -1, o.x, o.y, 0);
    }

    // Being hit cancels a fighter's melee: removes the hitboxes that follow it. Projectiles,
    // traps and its Ultimate copies' hits stay.
    public void RemoveFollowHitboxes(int owner)
    {
        for (int i = hitboxes.Count - 1; i >= 0; i--)
        {
            if (hitboxes[i].owner == owner && hitboxes[i].FollowsOwner)
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

    // A warning outline the view draws `ticks` before an attack lands at x (see SimEventType.Telegraph).
    public void Telegraph(int owner, int x, int width, int ticks)
    {
        AddEvent(SimEventType.Telegraph, owner, ticks, x, 0, width);
    }

    // A pseudo-random number for the rare rule that must look random (D-47). It's worked out from
    // the match seed, the tick and `salt`, so both machines get the same one, and it keeps no state.
    public uint Roll(int salt)
    {
        unchecked
        {
            uint h = (uint)seed * 0x9E3779B1u;
            h ^= (uint)tick * 0x85EBCA77u;
            h ^= (uint)salt * 0xC2B2AE3Du;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h;
        }
    }

    // ================= ground zones =================

    public void AddZone(Zone z)
    {
        zones.Add(z);
    }

    public int CountZones(int owner, int tag)
    {
        int count = 0;
        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i].owner == owner && zones[i].tag == tag) count++;
        }
        return count;
    }

    public void RemoveOldestZone(int owner, int tag)
    {
        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i].owner == owner && zones[i].tag == tag)
            {
                zones.RemoveAt(i);
                return;
            }
        }
    }

    void AgeZones()
    {
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            zones[i].age++;
            zones[i].ticksLeft--;
            if (zones[i].ticksLeft <= 0) zones.RemoveAt(i);
        }
    }

    // ================= Ultimate copies (summons) =================

    // A copy of fighter `owner` at x that lasts `ticks` (see Summon.cs).
    public void SpawnSummon(int owner, int x, int ticks)
    {
        Summon s = new Summon();
        s.owner = owner;
        s.x = ClampToArena(x);
        s.facing = s.x <= fighters[1 - owner].x ? 1 : -1;
        s.ticksLeft = ticks;
        summons.Add(s);
        AddEvent(SimEventType.SummonSpawn, owner, -1, s.x, 0, 0);
    }

    public int CountSummons(int owner)
    {
        int count = 0;
        for (int i = 0; i < summons.Count; i++)
        {
            if (summons[i].owner == owner) count++;
        }
        return count;
    }

    // A copy's body: its owner's hurtbox, standing at the copy's spot.
    public Box SummonBody(Summon s)
    {
        return fighters[s.owner].Hurtbox.ToWorld(s.x, 0, s.facing);
    }

    void StepSummons()
    {
        for (int i = summons.Count - 1; i >= 0; i--)
        {
            Summon s = summons[i];
            s.ticksLeft--;
            if (s.ticksLeft <= 0)
            {
                summons.RemoveAt(i);
                AddEvent(SimEventType.SummonDestroyed, s.owner, -1, s.x, 0, 0);
                continue;
            }

            Fighter o = fighters[s.owner];
            if (s.move == null)
            {
                s.facing = s.x <= fighters[1 - s.owner].x ? 1 : -1;
                if (o.state == FighterStateId.Walk) s.x = ClampToArena(s.x + o.vx);
                if (o.state == FighterStateId.Attack && o.stateTick == 0 && o.move != null && o.SummonCopiesMove(o.move))
                {
                    s.move = o.move;
                    s.moveTick = -1;
                }
            }

            if (s.move != null)
            {
                s.moveTick++;
                for (int h = 0; h < s.move.hitboxes.Length; h++)
                {
                    HitboxDef def = s.move.hitboxes[h];
                    if (def.spawnTick == s.moveTick) AddHitbox(s.owner, def, s.x, 0, s.facing, def.lifetime, true, true);
                }
                if (s.moveTick >= s.move.totalTicks - 1) s.move = null;
            }
        }
    }

    // ================= held grabs =================

    // Distance in front of the grabber a held victim is pinned at, if the grab doesn't say (pullTo).
    public const int HoldDistance = 1000;

    void StepHolds()
    {
        for (int i = 0; i < 2; i++)
        {
            Fighter grabber = fighters[i];
            Fighter victim = fighters[1 - i];

            // Something else knocked the victim out of the hold: the grabber has nobody to throw.
            if (grabber.holdTicks > 0 && victim.state != FighterStateId.Held)
            {
                grabber.holdTicks = 0;
                grabber.holdThrow = null;
            }

            if (grabber.holdTicks > 0)
            {
                grabber.holdTicks--;
                if (grabber.holdTicks == 0 && grabber.holdThrow != null)
                {
                    HitboxDef throwDef = grabber.holdThrow;
                    grabber.holdThrow = null;
                    ThrowHeldVictim(i, throwDef);
                }
            }

            // Nobody holding them and no follow-up playing: let go.
            if (victim.state == FighterStateId.Held && grabber.holdTicks == 0 && grabber.state != FighterStateId.Attack)
            {
                victim.ReleaseHold();
            }
        }
    }

    // The hold ran out without a follow-up: the grab's own numbers are applied as a normal throw.
    void ThrowHeldVictim(int owner, HitboxDef def)
    {
        Fighter grabber = fighters[owner];
        Fighter victim = fighters[1 - owner];
        HitboxInstance h = new HitboxInstance();
        h.owner = owner;
        h.def = def;
        h.facing = grabber.facing;
        h.damage = def.damage;
        h.hitstun = def.hitstun;
        h.knockback = def.knockback;
        h.box = def.box;
        h.hasHit = true;
        h.hitX = victim.x;
        h.hitY = 1000;
        grabber.ModifyOutgoingHit(this, victim, h);
        if (grabber.HasBleedWeakness) h.damage = h.damage * (100 - Fighter.BleedWeaknessPercent) / 100;
        RaiseHitEvent(h);
        ApplyHit(h, false);
    }

    // ================= hits =================

    void ResolveHits()
    {
        ProjectileInteractions();
        HitSummons();

        // Pass 1: find every hit without changing anything, so both players can trade hits.
        hitsThisTick.Clear();
        for (int i = 0; i < hitboxes.Count; i++)
        {
            HitboxInstance h = hitboxes[i];
            if (h.hasHit || h.def.ignoresFighters || h.age < h.def.armTicks) continue;

            Fighter attacker = fighters[h.owner];
            Fighter victim = fighters[1 - h.owner];
            if (victim.state == FighterStateId.KO || victim.IsIntangible) continue;
            if (h.def.groundOnly && victim.IsAirborne) continue;
            // Grab rule: a fighter in hitstun or blockstun, in the air, or already held can't be
            // grabbed. The grab hitbox stays alive, so it still catches them if they recover in time.
            if (h.def.grab && (victim.state == FighterStateId.Hitstun || victim.state == FighterStateId.Blockstun
                               || victim.state == FighterStateId.Held || victim.IsAirborne)) continue;

            Box hitWorld = h.World(this);
            Box hurtWorld = victim.HurtboxWorld();
            if (hitWorld.Overlaps(hurtWorld))
            {
                h.hasHit = true;
                h.blocked = !h.def.unblockable && !h.def.grab && victim.IsBlocking;
                hitWorld.OverlapCenter(hurtWorld, out h.hitX, out h.hitY);
                attacker.ModifyOutgoingHit(this, victim, h);
                if (attacker.HasBleedWeakness) h.damage = h.damage * (100 - Fighter.BleedWeaknessPercent) / 100;
                hitsThisTick.Add(h);
            }
        }
        MergeStackingHits(0);
        MergeStackingHits(1);
        for (int i = 0; i < hitsThisTick.Count; i++) RaiseHitEvent(hitsThisTick[i]);

        // Pass 2: apply them.
        for (int i = 0; i < hitsThisTick.Count; i++) ApplyHit(hitsThisTick[i], true);
    }

    // Stacking hits (HitboxDef.stacks) from one owner on the same tick become one bigger hit.
    void MergeStackingHits(int owner)
    {
        int count = 0;
        HitboxInstance first = null;
        for (int i = 0; i < hitsThisTick.Count; i++)
        {
            HitboxInstance h = hitsThisTick[i];
            if (h.owner != owner || !h.def.stacks) continue;
            count++;
            if (first == null) first = h;
        }
        if (count < 2) return;

        for (int i = hitsThisTick.Count - 1; i >= 0; i--)
        {
            HitboxInstance h = hitsThisTick[i];
            if (h.owner == owner && h.def.stacks && h != first) hitsThisTick.RemoveAt(i);
        }
        first.damage *= fighters[owner].StackMultiplier(count);
    }

    void RaiseHitEvent(HitboxInstance h)
    {
        if (h.blocked) AddEvent(SimEventType.Block, h.owner, 1 - h.owner, h.hitX, h.hitY, 0);
        else if (h.def.grab) AddEvent(SimEventType.Grab, h.owner, 1 - h.owner, h.hitX, h.hitY, h.damage);
        else AddEvent(SimEventType.Hit, h.owner, 1 - h.owner, h.hitX, h.hitY, h.damage);

        // Tell the view whether this was an Ultimate's hitbox (impact frames). Cosmetic: no state changes.
        SimEvent e = events[events.Count - 1];
        e.ultimate = h.fromUltimate;
        events[events.Count - 1] = e;
    }

    void ApplyHit(HitboxInstance h, bool allowHold)
    {
        Fighter attacker = fighters[h.owner];
        Fighter victim = fighters[1 - h.owner];

        // A held grab pins the victim instead of throwing at once (the throw comes later).
        if (allowHold && h.def.grab && h.def.holdTicks > 0)
        {
            int pinAt = attacker.x + attacker.facing * (h.def.pullTo > 0 ? h.def.pullTo : HoldDistance);
            victim.EnterHeld(this, pinAt);
            attacker.BeginHold(this, h.def);
            AddEvent(SimEventType.Hold, h.owner, 1 - h.owner, victim.x, 1000, h.def.holdTicks);
            return;
        }

        // Back throw: a grab that lands while the grabber holds back puts the victim behind
        // the grabber first, so TakeHit's knockback then sends them the other way. Otherwise a
        // pulling grab (a lasso) drags them in front of the grabber.
        if (h.def.grab && attacker.holdingBack)
        {
            victim.x = ClampToArena(attacker.x - attacker.facing * BackThrowDistance);
        }
        else if (h.def.grab && h.def.pullTo > 0)
        {
            victim.x = ClampToArena(attacker.x + attacker.facing * h.def.pullTo);
        }
        victim.TakeHit(this, h);
        GiveMeter(attacker, victim, h);
        attacker.OnHitDealt(this, victim, h);
        victim.OnHitReceived(this, attacker, h);

        if (hitStopOn) hitStopTicks = Math.Max(hitStopTicks, HitStopFor(h, victim));
    }

    // Reflectors send enemy projectiles back; destroyers remove them. Ultimate projectiles are
    // immune to both (D-50).
    void ProjectileInteractions()
    {
        for (int i = 0; i < hitboxes.Count; i++)
        {
            HitboxInstance a = hitboxes[i];
            if (a.hasHit || !(a.def.reflectsProjectiles || a.def.destroysProjectiles)) continue;
            Box aw = a.World(this);

            for (int j = 0; j < hitboxes.Count; j++)
            {
                HitboxInstance p = hitboxes[j];
                if (p == a || p.owner == a.owner || p.hasHit || !p.IsProjectile || p.fromUltimate) continue;
                Box pw = p.World(this);
                if (!aw.Overlaps(pw)) continue;

                int cx, cy;
                aw.OverlapCenter(pw, out cx, out cy);
                if (a.def.reflectsProjectiles)
                {
                    AddEvent(SimEventType.Reflect, a.owner, p.owner, cx, cy, 0);
                    p.owner = a.owner;
                    p.facing = -p.facing;
                }
                else
                {
                    p.hasHit = true;   // gone at the end of the tick, and it can't hit anyone now
                    AddEvent(SimEventType.ProjectileDestroyed, a.owner, p.owner, cx, cy, 0);
                }
            }
        }
    }

    // Any enemy hit that touches an Ultimate copy destroys the copy and is used up (so a copy
    // shields its owner from a projectile). Grabs ignore copies.
    void HitSummons()
    {
        for (int s = summons.Count - 1; s >= 0; s--)
        {
            Summon copy = summons[s];
            Box body = SummonBody(copy);
            for (int i = 0; i < hitboxes.Count; i++)
            {
                HitboxInstance h = hitboxes[i];
                if (h.owner == copy.owner || h.hasHit || h.def.grab || h.def.ignoresFighters || h.age < h.def.armTicks) continue;
                if (!h.World(this).Overlaps(body)) continue;

                h.hasHit = true;
                summons.RemoveAt(s);
                AddEvent(SimEventType.SummonDestroyed, copy.owner, h.owner, copy.x, 1000, 0);
                break;
            }
        }
    }

    // How long a hit that just landed pauses the match.
    static int HitStopFor(HitboxInstance h, Fighter victim)
    {
        if (victim.hp <= 0) return KOStop;
        if (h.blocked) return BlockStop;
        return Math.Min(HitStopMax, HitStopBase + h.damage / HitStopPerDamage);
    }

    // Ultimate meter for a hit that connected (D-08 in Design/DECISIONS.md).
    static void GiveMeter(Fighter attacker, Fighter victim, HitboxInstance h)
    {
        if (h.fromUltimate) return;
        int damage = h.damage;
        int forAttacker = h.blocked ? damage * Fighter.MeterPerDamageBlocked : damage * Fighter.MeterPerDamageDealt;
        int forVictim = h.blocked ? damage * Fighter.MeterPerDamageBlocked : damage * Fighter.MeterPerDamageTaken;
        if (attacker.CanGainMeter) attacker.AddMeter(forAttacker);
        if (victim.CanGainMeter) victim.AddMeter(forVictim);
    }

    void AgeHitboxes()
    {
        for (int i = hitboxes.Count - 1; i >= 0; i--)
        {
            HitboxInstance h = hitboxes[i];
            bool landed = false;
            if (!h.FollowsOwner)
            {
                h.originX += h.facing * h.def.vx;
                if (h.def.gravity != 0)
                {
                    h.originY += h.vy;
                    h.vy -= h.def.gravity;
                    landed = h.originY + h.box.y <= 0;   // a lob ends when its box reaches the ground
                }
            }
            h.age++;
            if (!h.def.untilHit) h.lifetime--;
            if ((h.lifetime <= 0 && !h.def.untilHit) || h.hasHit || landed) hitboxes.RemoveAt(i);
        }
    }

    // PUSHBOXES: fighters can't stand inside each other. If their bodies (hurtboxes) overlap along
    // x, both are pushed apart by half the overlap; a fighter against the arena edge can't move,
    // so the other one takes the rest. When both stand on exactly the same spot, fighter 0 goes
    // left: the tie-break must never depend on anything but the state, or the two machines differ.
    // Bodies only push while BOTH fighters are on the ground, so a jump can pass over the opponent;
    // if it lands overlapping them, this pushes them apart from the landing tick on.
    // Ultimate copies never push.
    void SeparateBodies()
    {
        if (fighters[0].IsAirborne || fighters[1].IsAirborne) return;

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

    // ================= desync hash =================

    // A number that summarises the whole state. Both players compare these to catch desyncs.
    // (Events are NOT included on purpose: they are output, not state.)
    public int Hash()
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + tick;
            h = h * 31 + freezeTicks;
            h = h * 31 + hitStopTicks;
            h = h * 31 + round;
            h = h * 31 + roundsWon[0];
            h = h * 31 + roundsWon[1];
            h = h * 31 + roundOverTicks;
            h = h * 31 + introTicks;
            h = h * 31 + introFirstSlot;
            h = h * 31 + introFirstTicks;
            h = h * 31 + introSecondTicks;
            h = h * 31 + introVariant;
            h = h * 31 + roundCallTicks;
            h = h * 31 + countdownTicks;
            h = h * 31 + fightBannerTicks;
            for (int i = 0; i < 2; i++)
            {
                Fighter f = fighters[i];
                h = h * 31 + f.x;
                h = h * 31 + f.y;
                h = h * 31 + f.vx;
                h = h * 31 + f.vy;
                h = h * 31 + (f.usedAirMove ? 1 : 0);
                h = h * 31 + (f.usedAirDash ? 1 : 0);
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
                h = h * 31 + f.HashCooldowns();
                h = h * 31 + f.chargeTicks;
                h = h * 31 + (f.charging ? 1 : 0);
                h = h * 31 + f.loopCount;
                h = h * 31 + f.holdTicks;
                h = h * 31 + (f.holdThrow != null ? f.holdThrow.damage + 1 : 0);
                h = h * 31 + f.slowTicks;
                h = h * 31 + f.slowPercent;
                h = h * 31 + f.bleedStacks;
                h = h * 31 + f.bleedDamageTimer;
                h = h * 31 + f.bleedFadeTimer;
                h = h * 31 + f.scrambleTicks;
                h = h * 31 + f.scrambleCode;
                h = h * 31 + f.leechTicks;
                h = h * 31 + f.leechTimer;
                h = h * 31 + f.leechOwner;
                h = h * 31 + f.HashExtra();
            }
            h = h * 31 + hitboxes.Count;
            for (int i = 0; i < hitboxes.Count; i++)
            {
                HitboxInstance hb = hitboxes[i];
                h = h * 31 + hb.owner;
                h = h * 31 + hb.lifetime;
                h = h * 31 + hb.originX;
                h = h * 31 + hb.originY;
                h = h * 31 + hb.vy;
                h = h * 31 + hb.facing;
                h = h * 31 + hb.age;
                h = h * 31 + hb.damage;
                h = h * 31 + hb.box.w;
                h = h * 31 + (hb.placed ? 1 : 0);
            }
            h = h * 31 + zones.Count;
            for (int i = 0; i < zones.Count; i++)
            {
                Zone z = zones[i];
                h = h * 31 + z.owner;
                h = h * 31 + z.tag;
                h = h * 31 + z.x;
                h = h * 31 + z.ticksLeft;
            }
            h = h * 31 + summons.Count;
            for (int i = 0; i < summons.Count; i++)
            {
                Summon s = summons[i];
                h = h * 31 + s.owner;
                h = h * 31 + s.x;
                h = h * 31 + s.facing;
                h = h * 31 + s.ticksLeft;
                h = h * 31 + s.moveTick;
                h = h * 31 + MoveFingerprint(s.move);
            }
            return h;
        }
    }

    // Which move this is, as a number for the hash. Built from the move's numbers, not from
    // name.GetHashCode(): string hashes are not guaranteed to match between the Editor (Mono)
    // and a WebGL build (IL2CPP), and the two players may be on different ones.
    public static int MoveFingerprint(MoveDef move)
    {
        if (move == null) return 0;
        unchecked
        {
            int h = move.totalTicks * 31 + (move.ultimate ? 1 : 0);
            h = h * 31 + move.cooldown;
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
