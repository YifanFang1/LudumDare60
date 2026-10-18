using System.Collections.Generic;
using NUnit.Framework;
using static SimHarness;

// Checks for the kit extensions the roster designs need (Design/ROSTER_PLAN.md, Step 1 and the
// extensions of Passes 1-3): cooldowns, hold input, hit modifiers, move hooks, intangibility,
// grab pull, held grabs, placed hitboxes, status effects, ground zones, projectile reflect and
// destroy, stacking hits, Ultimate copies, telegraphs and the seeded roll.
//
// Every test uses KitFighter (SimHarness.cs) and switches on only the rule it checks, so a failure
// points at one rule. The characters that use these rules are tested in RosterTests.cs.
public class KitExtensionTests
{
    // Holds `button` for player `player` for `ticks` ticks; returns the ticks on which a move started.
    static List<int> MoveStarts(GameSim sim, int player, int button, int ticks)
    {
        List<int> starts = new List<int>();
        for (int t = 0; t < ticks; t++)
        {
            sim.Step(player == 0 ? button : None, player == 1 ? button : None);
            for (int e = 0; e < sim.events.Count; e++)
            {
                if (sim.events[e].type == SimEventType.AttackStart && sim.events[e].a == player) starts.Add(t);
            }
        }
        return starts;
    }

    static bool Saw(GameSim sim, SimEventType type)
    {
        for (int e = 0; e < sim.events.Count; e++) if (sim.events[e].type == type) return true;
        return false;
    }

    // A long-lived projectile-only reflector (or destroyer), like Paintress's canvas.
    static MoveDef Shield(bool reflect)
    {
        MoveDef m = TestFighter.MeleeMove(0);
        m.totalTicks = 210;
        HitboxDef h = m.hitboxes[0];
        h.lifetime = 200;
        h.box = new Box(300, 0, 600, 2200);
        h.ignoresFighters = true;
        h.reflectsProjectiles = reflect;
        h.destroysProjectiles = !reflect;
        return m;
    }

    // ================= cooldowns (D-43) =================

    [Test]
    public void CooldownMakesTheSameMoveWaitExactlyThatMuchLonger()
    {
        MoveDef plain = TestFighter.MeleeMove(1);   // 20 ticks long
        MoveDef cooled = TestFighter.MeleeMove(1);
        cooled.cooldown = 10;
        GameSim a = NewMatch(new KitFighter { light = plain }, new KitFighter(), 8000);
        GameSim b = NewMatch(new KitFighter { light = cooled }, new KitFighter(), 8000);

        List<int> sa = MoveStarts(a, 0, Buttons.Light, 80);
        List<int> sb = MoveStarts(b, 0, Buttons.Light, 80);

        Assert.AreEqual(21, sa[1] - sa[0], "with no cooldown, a held button restarts the move the tick after it ends");
        Assert.AreEqual(31, sb[1] - sb[0], "a 10-tick cooldown waits exactly 10 ticks longer");
    }

    [Test]
    public void CooldownOnlyBlocksThatSameMove()
    {
        MoveDef jab = TestFighter.MeleeMove(1);
        jab.cooldown = 100;
        MoveDef kick = TestFighter.MeleeMove(1);
        kick.name = "Heavy";
        GameSim sim = NewMatch(new KitFighter { light = jab, heavy = kick }, new KitFighter(), 8000);
        Fighter f = sim.fighters[0];

        Run(sim, Buttons.Light, None, 1);
        Run(sim, None, None, 25);
        Assert.IsTrue(f.IsOnCooldown(jab));
        Assert.Greater(f.CooldownLeft(jab), 0);

        sim.Step(Buttons.Light, None);
        Assert.AreNotEqual(FighterStateId.Attack, f.state, "the jab is cooling down");

        sim.Step(Buttons.Heavy, None);
        Assert.AreEqual(FighterStateId.Attack, f.state, "the kick has its own cooldown");
        Assert.AreSame(kick, f.move);
    }

    [Test]
    public void CooldownStartsWhenAHitCutsTheMoveShort()
    {
        MoveDef slow = TestFighter.MeleeMove(1);
        slow.totalTicks = 30;
        slow.hitboxes[0].spawnTick = 20;
        slow.cooldown = 50;
        GameSim sim = NewMatch(new KitFighter { light = slow }, new KitFighter { light = BaseFighter.StandardLight }, 1200);

        Run(sim, Buttons.Light, Buttons.Light, 1);
        Run(sim, None, None, 8);

        Assert.AreEqual(FighterStateId.Hitstun, sim.fighters[0].state, "the faster jab should interrupt");
        Assert.IsTrue(sim.fighters[0].IsOnCooldown(slow), "an interrupted move still starts its cooldown");
    }

    [Test]
    public void DashCooldownDelaysTheNextDash()
    {
        GameSim sim = NewMatch(new KitFighter(), new KitFighter(), 8000);
        Fighter f = sim.fighters[0];

        // Double-tap left (back): tap, release, tap.
        sim.Step(Buttons.Left, None);
        sim.Step(None, None);
        sim.Step(Buttons.Left, None);
        Assert.AreEqual(FighterStateId.Dash, f.state);

        while (f.state == FighterStateId.Dash) sim.Step(None, None);
        Assert.AreEqual(Fighter.StandardDashCooldown + 1, f.dashCooldown, "ticks until the next dash, counted from the end");

        sim.Step(Buttons.Left, None);
        sim.Step(None, None);
        sim.Step(Buttons.Left, None);
        Assert.AreNotEqual(FighterStateId.Dash, f.state, "too soon to dash again");

        Run(sim, None, None, Fighter.StandardDashCooldown);
        sim.Step(Buttons.Left, None);
        sim.Step(None, None);
        sim.Step(Buttons.Left, None);
        Assert.AreEqual(FighterStateId.Dash, f.state, "the cooldown is over");
    }

    [Test]
    public void StandardMovesHaveTheStandardCooldowns()
    {
        Assert.AreEqual(5, BaseFighter.StandardLight.cooldown);
        Assert.AreEqual(10, BaseFighter.StandardHeavy.cooldown);
        Assert.AreEqual(15, BaseFighter.StandardGrab.cooldown);
        Assert.AreEqual(30, BaseFighter.StandardSpecial.cooldown);
        Assert.AreEqual(0, BaseFighter.StandardUltimate.cooldown, "an Ultimate's limit is the meter");
        Assert.AreEqual(20, Fighter.StandardDashCooldown);
    }

    [Test]
    public void InspectorCharacterMovesRespectTheirCooldown()
    {
        CharacterDef def = CharacterDef.CreateDefault();
        def.id = 424242;
        GameSim sim = NewMatch(new DataFighter(def), new KitFighter(), 8000);

        List<int> starts = MoveStarts(sim, 0, Buttons.Light, 60);

        Assert.AreEqual(5, def.moves[1].move.cooldown, "a new inspector character starts with the standard Light cooldown");
        Assert.AreEqual(15 + 1 + 5, starts[1] - starts[0]);
    }

    [Test]
    public void CharacterDataHashIncludesTheNewFields()
    {
        CharacterDef def = CharacterDef.CreateDefault();
        int before = def.Hash();
        def.moves[0].move.cooldown = 99;
        Assert.AreNotEqual(before, def.Hash(), "two builds with different cooldowns must refuse to play each other");

        before = def.Hash();
        def.moves[0].move.hitboxes[0].holdTicks = 30;
        Assert.AreNotEqual(before, def.Hash());
    }

    // ================= blocking switch =================

    [Test]
    public void FighterThatCannotBlockTakesFullDamage()
    {
        GameSim sim = NewMatch(new KitFighter { light = TestFighter.MeleeMove(10) }, new KitFighter { canBlock = false }, 1100);
        bool sawBlockstun = false;

        sim.Step(Buttons.Light, BackP1);
        for (int i = 0; i < 20; i++)
        {
            sim.Step(None, BackP1);
            if (sim.fighters[1].state == FighterStateId.Blockstun) sawBlockstun = true;
        }

        Assert.AreEqual(90, sim.fighters[1].hp);
        Assert.IsFalse(sawBlockstun);
    }

    // ================= hit modifiers (D-25) =================

    [Test]
    public void OutgoingModifierChangesTheHitButNeverTheSharedDef()
    {
        MoveDef jab = TestFighter.MeleeMove(10);
        KitFighter attacker = new KitFighter { light = jab };
        attacker.modifyOutgoing = delegate (GameSim s, Fighter v, HitboxInstance h) { h.damage *= 2; };
        GameSim sim = NewMatch(attacker, new KitFighter(), 1100);
        int eventDamage = 0;

        sim.Step(Buttons.Light, None);
        for (int i = 0; i < 10; i++)
        {
            sim.Step(None, None);
            for (int e = 0; e < sim.events.Count; e++) if (sim.events[e].type == SimEventType.Hit) eventDamage = sim.events[e].amount;
        }

        Assert.AreEqual(80, sim.fighters[1].hp);
        Assert.AreEqual(20, eventDamage, "the Hit event reports the real damage");
        Assert.AreEqual(10, jab.hitboxes[0].damage, "the shared HitboxDef must not change");
    }

    [Test]
    public void SpawnHookCanStampTheHitInstance()
    {
        KitFighter attacker = new KitFighter { light = TestFighter.MeleeMove(10) };
        attacker.onSpawned = delegate (GameSim s, HitboxInstance h) { h.damage = 33; };
        GameSim sim = NewMatch(attacker, new KitFighter(), 1100);

        Run(sim, Buttons.Light, None, 1);
        Run(sim, None, None, 10);

        Assert.AreEqual(67, sim.fighters[1].hp);
    }

    // Two stacking hitboxes that land on the same tick: one hit, damage x the owner's multiplier.
    static MoveDef DoubleHit(bool stacks)
    {
        MoveDef m = TestFighter.MeleeMove(5);
        HitboxDef second = new HitboxDef
        {
            spawnTick = 2, lifetime = 3, box = new Box(300, 0, 1200, 2000),
            damage = 5, hitstun = 12, followOwner = true,
        };
        m.hitboxes = new HitboxDef[] { m.hitboxes[0], second };
        m.hitboxes[0].stacks = stacks;
        m.hitboxes[1].stacks = stacks;
        return m;
    }

    [Test]
    public void StackingHitsMergeIntoOneBiggerHit()
    {
        GameSim stacked = NewMatch(new KitFighter { light = DoubleHit(true), stackMultiplier = 4 }, new KitFighter(), 1100);
        GameSim plain = NewMatch(new KitFighter { light = DoubleHit(false), stackMultiplier = 4 }, new KitFighter(), 1100);
        int stackedHits = 0;

        stacked.Step(Buttons.Light, None);
        plain.Step(Buttons.Light, None);
        for (int i = 0; i < 10; i++)
        {
            stacked.Step(None, None);
            plain.Step(None, None);
            for (int e = 0; e < stacked.events.Count; e++) if (stacked.events[e].type == SimEventType.Hit) stackedHits++;
        }

        Assert.AreEqual(1, stackedHits, "stacking hits merge into one");
        Assert.AreEqual(100 - 5 * 4, stacked.fighters[1].hp);
        Assert.AreEqual(100 - 5 - 5, plain.fighters[1].hp, "without the flag both hits land normally");
    }

    // ================= hold input (D-42) =================

    static MoveDef Chargeable(int chargeMax)
    {
        MoveDef m = TestFighter.MeleeMove(1);
        m.holdButton = Buttons.Light;
        m.chargeTick = 0;
        m.chargeMax = chargeMax;
        return m;
    }

    // Presses Light once, holds it `extraHeld` more ticks, then lets go. Returns the tick the
    // hitbox spawned on and the fighter's chargeTicks at that moment.
    static int SpawnTickAfterHolding(int extraHeld, int chargeMax, out int charge)
    {
        KitFighter f = new KitFighter { light = Chargeable(chargeMax) };
        int spawnedAt = -1;
        int seenCharge = -1;
        f.onSpawned = delegate (GameSim s, HitboxInstance h) { if (spawnedAt < 0) { spawnedAt = s.tick; seenCharge = f.chargeTicks; } };
        GameSim sim = NewMatch(f, new KitFighter(), 8000);

        Run(sim, Buttons.Light, None, 1 + extraHeld);
        Run(sim, None, None, 100);
        charge = seenCharge;
        return spawnedAt;
    }

    [Test]
    public void ChargeHoldsTheMoveOnItsChargeTick()
    {
        int tapCharge, heldCharge;
        int tapped = SpawnTickAfterHolding(0, 60, out tapCharge);
        int held = SpawnTickAfterHolding(10, 60, out heldCharge);

        Assert.AreEqual(0, tapCharge, "a tap doesn't charge");
        Assert.AreEqual(10, heldCharge);
        Assert.AreEqual(10, held - tapped, "the move waited exactly as long as it was held");
    }

    [Test]
    public void ChargeReleasesByItselfAtTheCap()
    {
        int charge;
        int capped = SpawnTickAfterHolding(500, 30, out charge);   // hold far longer than the cap

        Assert.AreEqual(30, charge);
        Assert.Greater(capped, 0, "the move came out without letting go");
    }

    static int SpawnsWhileHolding(int heldTicks)
    {
        MoveDef m = TestFighter.MeleeMove(1);
        m.totalTicks = 25;
        m.hitboxes[0].spawnTick = 6;
        m.holdButton = Buttons.Light;
        m.loopStart = 6;
        m.loopEnd = 10;
        m.loopMin = 3;
        m.loopMax = 6;
        KitFighter f = new KitFighter { light = m };
        int spawns = 0;
        f.onSpawned = delegate (GameSim s, HitboxInstance h) { spawns++; };
        GameSim sim = NewMatch(f, new KitFighter(), 8000);

        Run(sim, Buttons.Light, None, heldTicks);
        Run(sim, None, None, 100 - heldTicks);
        return spawns;
    }

    [Test]
    public void LoopRepeatsWhileHeldBetweenItsMinAndMax()
    {
        Assert.AreEqual(3, SpawnsWhileHolding(1), "a tap plays the loop loopMin times");
        // 40 ticks covers all 6 passes (ticks 6-36) but lets go before the move ends, so it
        // doesn't start again.
        Assert.AreEqual(6, SpawnsWhileHolding(40), "holding plays it loopMax times");
    }

    [Test]
    public void TapWindowTurnsAQuickTapIntoOnTap()
    {
        MoveDef m = TestFighter.PoseMove(30);
        m.name = "Special";
        m.holdButton = Buttons.Special;
        m.tapWindow = 15;
        m.cooldown = 50;
        KitFighter tapper = new KitFighter { special = m };
        GameSim sim = NewMatch(tapper, new KitFighter(), 8000);

        Run(sim, Buttons.Special, None, 2);
        Run(sim, None, None, 3);

        Assert.AreEqual(1, tapper.taps);
        Assert.AreEqual(FighterStateId.Idle, tapper.state);
        Assert.IsFalse(tapper.IsOnCooldown(m), "a tap starts no cooldown");

        Run(sim, Buttons.Special, None, 20);
        Assert.AreEqual(1, tapper.taps, "holding past the window is not a tap");
        Assert.AreEqual(FighterStateId.Attack, tapper.state);
    }

    // ================= move hooks =================

    [Test]
    public void KeepOnLandingMovePlaysOutItsRecovery()
    {
        MoveDef uppercut = TestFighter.PoseMove(60);
        uppercut.keepOnLanding = true;
        MoveDef hop = TestFighter.PoseMove(60);
        KitFighter keeps = new KitFighter { light = uppercut };
        KitFighter cancels = new KitFighter { light = hop };
        keeps.onMoveTick = delegate (GameSim s, KitFighter f) { if (f.stateTick == 0) f.vy = 100; };
        cancels.onMoveTick = delegate (GameSim s, KitFighter f) { if (f.stateTick == 0) f.vy = 100; };
        GameSim a = NewMatch(keeps, new KitFighter(), 8000);
        GameSim b = NewMatch(cancels, new KitFighter(), 8000);

        Run(a, Buttons.Light, None, 1);
        Run(b, Buttons.Light, None, 1);
        Run(a, None, None, 40);   // ~20 ticks in the air at vy 100
        Run(b, None, None, 40);

        Assert.AreEqual(0, keeps.y);
        Assert.AreEqual(FighterStateId.Attack, keeps.state, "an uppercut keeps playing after it lands");
        Assert.AreEqual(FighterStateId.Idle, cancels.state, "a normal move is cancelled by landing, like an air attack");

        Run(a, None, None, 25);
        Assert.AreEqual(FighterStateId.Idle, keeps.state, "and it still ends on time");
    }

    [Test]
    public void MoveEndHookRunsForFinishedAndInterruptedMoves()
    {
        KitFighter f = new KitFighter { light = TestFighter.MeleeMove(1) };
        GameSim sim = NewMatch(f, new KitFighter(), 8000);

        Run(sim, Buttons.Light, None, 1);
        Run(sim, None, None, 25);

        Assert.AreEqual(1, f.moveEnds);
    }

    // ================= intangibility =================

    [Test]
    public void IntangibleFighterIsNotHit()
    {
        GameSim sim = NewMatch(new KitFighter { light = TestFighter.MeleeMove(10) }, new KitFighter { intangible = true }, 1100);
        bool sawHit = false;

        sim.Step(Buttons.Light, None);
        for (int i = 0; i < 10; i++)
        {
            sim.Step(None, None);
            if (Saw(sim, SimEventType.Hit)) sawHit = true;
        }

        Assert.AreEqual(100, sim.fighters[1].hp);
        Assert.IsFalse(sawHit);
    }

    // ================= grabs: pull and hold =================

    static MoveDef GrabMove(int damage, int reach)
    {
        MoveDef m = TestFighter.MeleeMove(damage);
        m.name = "Grab";
        m.hitboxes[0].grab = true;
        m.hitboxes[0].box = new Box(300, 0, reach, 2000);
        m.hitboxes[0].hitstun = 20;
        return m;
    }

    [Test]
    public void PullingGrabDragsTheVictimInFront()
    {
        MoveDef lasso = GrabMove(10, 2500);
        lasso.hitboxes[0].pullTo = 900;
        GameSim sim = NewMatch(new KitFighter { grab = lasso }, new KitFighter(), 2500);
        int pulledTo = int.MinValue;

        sim.Step(Buttons.Grab, None);
        for (int i = 0; i < 10 && pulledTo == int.MinValue; i++)
        {
            sim.Step(None, None);
            if (Saw(sim, SimEventType.Grab)) pulledTo = sim.fighters[1].x;
        }

        Assert.AreEqual(sim.fighters[0].x + 900, pulledTo);
        Assert.AreEqual(90, sim.fighters[1].hp);
    }

    static GameSim HeldGrabMatch(KitFighter grabber, int holdTicks)
    {
        MoveDef hold = GrabMove(10, 1500);
        hold.hitboxes[0].holdTicks = holdTicks;
        grabber.grab = hold;
        GameSim sim = NewMatch(grabber, new KitFighter(), 1100);
        sim.Step(Buttons.Grab, None);
        for (int i = 0; i < 10 && sim.fighters[1].state != FighterStateId.Held; i++) sim.Step(None, None);
        return sim;
    }

    [Test]
    public void HeldGrabPinsTheVictimThenThrowsWhenTheHoldEnds()
    {
        KitFighter grabber = new KitFighter();
        GameSim sim = HeldGrabMatch(grabber, 30);
        Fighter victim = sim.fighters[1];
        Assert.AreEqual(FighterStateId.Held, victim.state);
        Assert.AreEqual(100, victim.hp, "no damage until the throw");

        int grabberX = grabber.x;
        Run(sim, Buttons.Right, Buttons.Light | BackP1, 10);
        Assert.AreEqual(grabberX, grabber.x, "the grabber can't walk while holding");
        Assert.AreEqual(FighterStateId.Held, victim.state, "the victim can't act or block");

        Run(sim, None, None, 25);
        Assert.AreEqual(90, victim.hp, "the hold ended in a throw");
        Assert.AreEqual(FighterStateId.Hitstun, victim.state);
    }

    [Test]
    public void HoldFollowUpReplacesTheThrow()
    {
        MoveDef followUp = TestFighter.MeleeMove(7);
        followUp.name = "Special";
        KitFighter grabber = new KitFighter { holdMove = followUp };
        GameSim sim = HeldGrabMatch(grabber, 30);

        Run(sim, Buttons.Special, None, 1);
        Run(sim, None, None, 60);

        Assert.AreEqual(93, sim.fighters[1].hp, "only the follow-up hit; the throw never happened");
    }

    [Test]
    public void GrabCannotCatchSomeoneAlreadyHeld()
    {
        KitFighter grabber = new KitFighter();
        GameSim sim = HeldGrabMatch(grabber, 30);
        Assert.AreEqual(FighterStateId.Held, sim.fighters[1].state);

        HitboxInstance second = sim.SpawnHitbox(0, grabber.grab.hitboxes[0]);
        sim.Step(None, None);
        Assert.IsFalse(second.hasHit);
    }

    // ================= placed hitboxes =================

    static HitboxDef Trap(int armTicks)
    {
        return new HitboxDef
        {
            lifetime = 1, box = new Box(-400, 0, 800, 200), damage = 5, hitstun = 10,
            followOwner = false, untilHit = true, groundOnly = true, armTicks = armTicks,
        };
    }

    [Test]
    public void PlacedTrapWaitsUntilSomeoneStepsOnIt()
    {
        GameSim sim = NewMatch(new KitFighter(), new KitFighter(), 8000);
        HitboxDef trap = Trap(0);
        sim.SpawnHitboxAt(0, trap, 0, 1);

        Run(sim, None, None, 300);
        Assert.AreEqual(1, sim.CountHitboxes(0, trap), "it doesn't time out");

        for (int i = 0; i < 300 && sim.fighters[1].hp == 100; i++) sim.Step(None, Buttons.Left);
        Assert.AreEqual(95, sim.fighters[1].hp, "walking onto it set it off");
        Assert.AreEqual(0, sim.CountHitboxes(0, trap), "and it's used up");
    }

    [Test]
    public void GroundOnlyHitboxMissesAnAirborneFighter()
    {
        GameSim sim = NewMatch(new KitFighter(), new KitFighter(), 8000);
        Fighter victim = sim.fighters[1];
        victim.y = 1500;   // in the air above the trap
        sim.SpawnHitboxAt(0, Trap(0), victim.x, 1);

        sim.Step(None, None);
        Assert.AreEqual(100, victim.hp);
    }

    [Test]
    public void TrapCannotHitBeforeItArms()
    {
        GameSim sim = NewMatch(new KitFighter(), new KitFighter(), 8000);
        sim.SpawnHitboxAt(0, Trap(20), sim.fighters[1].x, 1);

        Run(sim, None, None, 20);
        Assert.AreEqual(100, sim.fighters[1].hp, "still arming");
        Run(sim, None, None, 2);
        Assert.AreEqual(95, sim.fighters[1].hp);
    }

    // ================= status effects (D-46) =================

    [Test]
    public void LongestSlowWinsAndCutsWalkSpeed()
    {
        GameSim sim = NewMatch(new KitFighter(), new KitFighter(), 8000);
        Fighter f = sim.fighters[0];

        f.ApplySlow(40, 100);
        Assert.AreEqual(60, f.SpeedPercent(sim));
        f.ApplySlow(20, 50);
        Assert.AreEqual(60, f.SpeedPercent(sim), "a shorter slow doesn't replace a longer one");
        f.ApplySlow(20, 200);
        Assert.AreEqual(80, f.SpeedPercent(sim), "a longer one does");

        f.ApplySlow(40, 300);
        int startX = f.x;
        Run(sim, Buttons.Right, None, 10);   // a fighter moves on the same tick it decides to walk
        Assert.AreEqual(10 * (60 * 60 / 100), f.x - startX, "walk speed 60 at 60%");
    }

    [Test]
    public void AllSlowsTogetherAreCappedAtHalfSpeed()
    {
        GameSim sim = NewMatch(new KitFighter(), new KitFighter(), 8000);
        Fighter f = sim.fighters[0];
        sim.AddZone(new Zone { owner = 1, x = f.x, halfWidth = 1000, ticksLeft = 100, enemySlowPercent = 30 });
        f.ApplySlow(40, 100);

        Assert.AreEqual(100 - Fighter.MaxSlowPercent, f.SpeedPercent(sim));
    }

    [Test]
    public void IgnoringStatusesSkipsSlowAndScramble()
    {
        GameSim sim = NewMatch(new KitFighter { ignoresStatuses = true }, new KitFighter(), 8000);
        Fighter f = sim.fighters[0];

        f.ApplySlow(40, 100);
        f.ApplyScramble(sim, 100);

        Assert.AreEqual(100, f.SpeedPercent(sim));
        Assert.AreEqual(0, f.scrambleTicks);
    }

    [Test]
    public void BleedHurtsOverTimeButNeverKOs()
    {
        GameSim sim = NewMatch(new KitFighter(), new KitFighter(), 8000);
        Fighter f = sim.fighters[0];
        for (int i = 0; i < 7; i++) f.AddBleed();
        Assert.AreEqual(Fighter.MaxBleedStacks, f.bleedStacks, "capped at 5");

        Run(sim, None, None, Fighter.BleedTickInterval);
        Assert.AreEqual(100 - 5, f.hp, "5 stacks = 5 damage a second");

        f.hp = 3;
        for (int i = 0; i < 5; i++) f.AddBleed();
        Run(sim, None, None, 600);
        Assert.AreEqual(1, f.hp, "damage over time stops at 1 HP (D-23)");
        Assert.AreNotEqual(FighterStateId.KO, f.state);
    }

    [Test]
    public void BleedStacksFadeWithoutNewOnes()
    {
        GameSim sim = NewMatch(new KitFighter(), new KitFighter(), 8000);
        Fighter f = sim.fighters[0];
        f.AddBleed();
        f.AddBleed();

        Run(sim, None, None, Fighter.BleedFadeInterval);
        Assert.AreEqual(1, f.bleedStacks);
        Run(sim, None, None, Fighter.BleedFadeInterval);
        Assert.AreEqual(0, f.bleedStacks);
    }

    [Test]
    public void FullBleedWeakensTheVictimsOwnHits()
    {
        GameSim sim = NewMatch(new KitFighter { light = TestFighter.MeleeMove(20) }, new KitFighter(), 1100);
        for (int i = 0; i < 5; i++) sim.fighters[0].AddBleed();

        Run(sim, Buttons.Light, None, 1);
        Run(sim, None, None, 10);

        Assert.AreEqual(100 - 20 * 75 / 100, sim.fighters[1].hp);
    }

    [Test]
    public void ScrambleSwapsLeftAndRightAndIsTheSameOnBothMachines()
    {
        GameSim a = NewMatch(new KitFighter(), new KitFighter(), 8000);
        GameSim b = NewMatch(new KitFighter(), new KitFighter(), 8000);
        a.fighters[0].ApplyScramble(a, 120);
        b.fighters[0].ApplyScramble(b, 120);

        Assert.AreEqual(a.fighters[0].scrambleCode, b.fighters[0].scrambleCode, "same seed and tick, same shuffle");
        Assert.That(a.fighters[0].scrambleCode, Is.InRange(1, 23), "never the unshuffled order");

        int startX = a.fighters[0].x;
        Run(a, Buttons.Right, None, 10);
        Assert.Less(a.fighters[0].x, startX, "holding Right walks left");

        Run(a, None, None, 120);
        Assert.AreEqual(0, a.fighters[0].scrambleTicks, "and it wears off");
    }

    [Test]
    public void LeechesDrainTheVictimAndHealTheirOwner()
    {
        GameSim sim = NewMatch(new KitFighter(), new KitFighter(), 8000);
        sim.fighters[0].hp = 50;
        sim.fighters[1].ApplyLeech(0, 300);

        Run(sim, None, None, 300);

        Assert.AreEqual(95, sim.fighters[1].hp, "1 HP a second for 5 seconds");
        Assert.AreEqual(55, sim.fighters[0].hp, "the owner gets it back");
        Assert.AreEqual(-1, sim.fighters[1].leechOwner, "and they fall off");
    }

    [Test]
    public void HealNeverGoesAboveMaxHp()
    {
        GameSim sim = NewMatch(new KitFighter(), new KitFighter(), 8000);
        Fighter f = sim.fighters[0];
        f.hp = 95;

        Assert.AreEqual(5, f.Heal(sim, 10));
        Assert.AreEqual(f.MaxHp, f.hp);
    }

    [Test]
    public void StatusDamageCannotTakeTheLastHp()
    {
        GameSim sim = NewMatch(new KitFighter(), new KitFighter(), 8000);
        Fighter f = sim.fighters[0];
        f.hp = 10;

        Assert.AreEqual(9, f.TakeDamage(sim, 50, false));
        Assert.AreEqual(1, f.hp);
        Assert.IsTrue(Saw(sim, SimEventType.StatusDamage));
    }

    // ================= ground zones =================

    [Test]
    public void ZoneSpeedsUpItsOwnerAndSlowsTheOpponent()
    {
        GameSim sim = NewMatch(new KitFighter(), new KitFighter(), 1500);
        sim.AddZone(new Zone { owner = 0, x = 0, halfWidth = 2000, ticksLeft = 100, ownerSpeedPercent = 15, enemySlowPercent = 15 });

        Assert.AreEqual(115, sim.fighters[0].SpeedPercent(sim));
        Assert.AreEqual(85, sim.fighters[1].SpeedPercent(sim));

        sim.fighters[1].y = 500;   // in the air: not standing in it
        Assert.AreEqual(100, sim.fighters[1].SpeedPercent(sim));
    }

    [Test]
    public void ZoneDamageTicksOnItsIntervalAndTheZoneExpires()
    {
        GameSim sim = NewMatch(new KitFighter(), new KitFighter(), 1500);
        sim.AddZone(new Zone { owner = 0, x = 0, halfWidth = 2000, ticksLeft = 60, enemyDamage = 1, damageInterval = 20 });

        Run(sim, None, None, 60);

        Assert.AreEqual(97, sim.fighters[1].hp, "ages 0, 20 and 40");
        Assert.AreEqual(100, sim.fighters[0].hp, "the owner is never hurt by its own zone");
        Assert.AreEqual(0, sim.zones.Count);
    }

    [Test]
    public void OldestZoneIsTheOneRemoved()
    {
        GameSim sim = NewMatch(new KitFighter(), new KitFighter(), 8000);
        sim.AddZone(new Zone { owner = 0, tag = 1, x = 100, ticksLeft = 100 });
        sim.AddZone(new Zone { owner = 0, tag = 1, x = 200, ticksLeft = 100 });
        sim.AddZone(new Zone { owner = 0, tag = 2, x = 300, ticksLeft = 100 });

        Assert.AreEqual(2, sim.CountZones(0, 1));
        sim.RemoveOldestZone(0, 1);
        Assert.AreEqual(1, sim.CountZones(0, 1));
        Assert.AreEqual(200, sim.zones[0].x);
    }

    // ================= projectiles vs reflectors and destroyers =================

    static GameSim ShieldMatch(bool reflect, MoveDef shot, out KitFighter thrower)
    {
        thrower = new KitFighter { special = shot, ultimate = shot.ultimate ? shot : null };
        GameSim sim = NewMatch(thrower, new KitFighter { special = Shield(reflect) }, 6000);
        return sim;
    }

    [Test]
    public void ReflectorSendsAProjectileBackAtItsThrower()
    {
        KitFighter thrower;
        GameSim sim = ShieldMatch(true, TestFighter.ProjectileMove(10, 100), out thrower);
        bool sawReflect = false;

        sim.Step(Buttons.Special, Buttons.Special);
        for (int i = 0; i < 200; i++)
        {
            sim.Step(None, None);
            if (Saw(sim, SimEventType.Reflect)) sawReflect = true;
        }

        Assert.IsTrue(sawReflect);
        Assert.AreEqual(90, thrower.hp, "it came back and hit the thrower");
        Assert.AreEqual(100, sim.fighters[1].hp);
    }

    [Test]
    public void DestroyerRemovesAProjectile()
    {
        KitFighter thrower;
        GameSim sim = ShieldMatch(false, TestFighter.ProjectileMove(10, 100), out thrower);
        bool sawDestroyed = false;

        sim.Step(Buttons.Special, Buttons.Special);
        for (int i = 0; i < 200; i++)
        {
            sim.Step(None, None);
            if (Saw(sim, SimEventType.ProjectileDestroyed)) sawDestroyed = true;
        }

        Assert.IsTrue(sawDestroyed);
        Assert.AreEqual(100, thrower.hp);
        Assert.AreEqual(100, sim.fighters[1].hp);
    }

    [Test]
    public void UltimateProjectilesGoThroughReflectors()
    {
        MoveDef ultShot = TestFighter.ProjectileMove(10, 100);
        ultShot.ultimate = true;
        KitFighter thrower;
        GameSim sim = ShieldMatch(true, ultShot, out thrower);
        thrower.meter = Fighter.MeterMax;

        sim.Step(Buttons.Ultimate, Buttons.Special);
        Run(sim, None, None, 250);

        Assert.AreEqual(90, sim.fighters[1].hp, "the Ultimate's projectile wasn't reflected (D-50)");
        Assert.AreEqual(100, thrower.hp);
    }

    // ================= Ultimate copies (summons) =================

    [Test]
    public void CopyRepeatsTheOwnersMoveFromItsOwnSpotWithoutMeter()
    {
        KitFighter owner = new KitFighter { light = TestFighter.MeleeMove(10) };
        owner.copies = delegate (MoveDef m) { return true; };
        GameSim sim = NewMatch(owner, new KitFighter(), 8000);   // owner at -4000, victim at 4000
        sim.SpawnSummon(0, 3000, 300);

        Run(sim, Buttons.Light, None, 1);
        Run(sim, None, None, 10);

        Assert.AreEqual(90, sim.fighters[1].hp, "the copy next to the victim hit them");
        Assert.AreEqual(0, owner.meter, "copies' hits give no meter");
        Assert.AreEqual(0, sim.fighters[1].meter);
    }

    [Test]
    public void CopyWalksWhenItsOwnerWalks()
    {
        KitFighter owner = new KitFighter();
        GameSim sim = NewMatch(owner, new KitFighter(), 8000);
        sim.SpawnSummon(0, 0, 300);

        Run(sim, Buttons.Right, None, 10);

        Assert.AreEqual(10 * owner.WalkSpeed, sim.summons[0].x);
    }

    [Test]
    public void EnemyHitDestroysACopyAndIsUsedUp()
    {
        KitFighter owner = new KitFighter();
        GameSim sim = NewMatch(owner, new KitFighter { special = TestFighter.ProjectileMove(10, 100) }, 8000);
        sim.SpawnSummon(0, 0, 1000);   // between the two fighters

        Run(sim, None, Buttons.Special, 1);
        Run(sim, None, None, 150);

        Assert.AreEqual(0, sim.summons.Count);
        Assert.AreEqual(100, owner.hp, "the copy took the projectile instead of its owner");
    }

    [Test]
    public void CopiesExpireAndNeverPush()
    {
        GameSim sim = NewMatch(new KitFighter(), new KitFighter(), 2000);
        sim.SpawnSummon(0, 0, 50);

        Run(sim, None, Buttons.Left, 40);
        Assert.Less(sim.fighters[1].x, -500, "the opponent walked straight through the copy");

        Run(sim, None, None, 20);
        Assert.AreEqual(0, sim.summons.Count);
    }

    // ================= telegraphs, the seeded roll, meter and dash hitboxes =================

    [Test]
    public void TelegraphRaisesAnEventTheViewCanDraw()
    {
        GameSim sim = NewMatch(new KitFighter(), new KitFighter(), 8000);
        sim.Telegraph(1, 1234, 1200, 30);

        SimEvent e = sim.events[sim.events.Count - 1];
        Assert.AreEqual(SimEventType.Telegraph, e.type);
        Assert.AreEqual(1, e.a);
        Assert.AreEqual(30, e.b);
        Assert.AreEqual(1234, e.x);
        Assert.AreEqual(1200, e.amount);
    }

    [Test]
    public void RollIsDeterministicAndDependsOnSaltAndTick()
    {
        GameSim a = NewMatch(new KitFighter(), new KitFighter(), 8000);
        GameSim b = NewMatch(new KitFighter(), new KitFighter(), 8000);

        Assert.AreEqual(a.Roll(5), b.Roll(5));
        Assert.AreNotEqual(a.Roll(5), a.Roll(6));
        uint before = a.Roll(5);
        a.Step(None, None);
        Assert.AreNotEqual(before, a.Roll(5));
    }

    [Test]
    public void FighterThatCannotGainMeterGetsNone()
    {
        GameSim sim = NewMatch(new KitFighter { light = TestFighter.MeleeMove(10), canGainMeter = false }, new KitFighter(), 1100);

        Run(sim, Buttons.Light, None, 1);
        Run(sim, None, None, 10);

        Assert.AreEqual(0, sim.fighters[0].meter);
        Assert.AreEqual(10 * Fighter.MeterPerDamageTaken, sim.fighters[1].meter, "the victim still gets theirs");
    }

    [Test]
    public void DashHitboxHitsDuringTheDash()
    {
        HitboxDef ram = new HitboxDef { box = new Box(300, 0, 900, 2000), damage = 8, hitstun = 10, followOwner = true };
        GameSim sim = NewMatch(new KitFighter { dashHitbox = ram }, new KitFighter(), 3000);

        sim.Step(Buttons.Right, None);
        sim.Step(None, None);
        sim.Step(Buttons.Right, None);
        Run(sim, None, None, 20);

        Assert.AreEqual(92, sim.fighters[1].hp);
    }
}
