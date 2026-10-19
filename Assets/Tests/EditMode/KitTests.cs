using NUnit.Framework;
using static SimHarness;

// Checks for the shared kit every character gets (Design/SHARED_KIT.md): buttons, grabs,
// pushboxes, dashes, meter, Special/Ultimate and rounds. Each rule has at least one test, so a
// change that breaks a rule for everyone shows up here first.
public class KitTests
{
    // ================= buttons =================

    [Test]
    public void EveryButtonIsItsOwnBit()
    {
        int[] all = { Buttons.Left, Buttons.Right, Buttons.Up, Buttons.Down, Buttons.Light, Buttons.Heavy,
                      Buttons.Block, Buttons.Grab, Buttons.Special, Buttons.Ultimate, Buttons.Dash };
        int seen = 0;
        for (int i = 0; i < all.Length; i++)
        {
            Assert.AreEqual(1, CountBits(all[i]), "button " + all[i] + " should be exactly one bit");
            Assert.AreEqual(0, seen & all[i], "button " + all[i] + " reuses a bit");
            seen |= all[i];
        }
    }

    static int CountBits(int v)
    {
        int n = 0;
        for (; v != 0; v &= v - 1) n++;
        return n;
    }

    // ================= hitbox flags: grab and unblockable =================

    [Test]
    public void GrabIgnoresBlocking()
    {
        MoveDef grab = TestFighter.MeleeMove(damage: 10);
        grab.hitboxes[0].grab = true;
        GameSim sim = NewMatch(new TestFighter { light = grab }, new TestFighter(), 1100);
        bool sawBlockstun = false;

        sim.Step(Buttons.Light, BackP1);
        for (int i = 0; i < 20; i++)
        {
            sim.Step(None, BackP1);
            if (sim.fighters[1].state == FighterStateId.Blockstun) sawBlockstun = true;
        }

        Assert.AreEqual(90, sim.fighters[1].hp, "a grab should hurt a blocking opponent");
        Assert.IsFalse(sawBlockstun, "a grab is never blocked");
    }

    [Test]
    public void GrabCannotCatchSomeoneInBlockstun()
    {
        MoveDef grab = TestFighter.MeleeMove(damage: 10);
        grab.hitboxes[0].grab = true;
        GameSim sim = NewMatch(new TestFighter { light = grab }, new TestFighter(), 1100);
        Fighter victim = sim.fighters[1];
        victim.state = FighterStateId.Blockstun;   // as if they had just blocked something
        victim.stateTick = 0;
        victim.stunTicks = 60;                      // longer than the grab is active

        Run(sim, Buttons.Light, BackP1, 1);
        Run(sim, None, BackP1, 20);

        Assert.AreEqual(100, victim.hp, "grabs can't catch a fighter in blockstun");
    }

    [Test]
    public void GrabCatchesThemWhenStunEndsInTime()
    {
        MoveDef grab = TestFighter.MeleeMove(damage: 10);
        grab.hitboxes[0].grab = true;
        grab.hitboxes[0].lifetime = 10;
        GameSim sim = NewMatch(new TestFighter { light = grab }, new TestFighter(), 1100);
        Fighter victim = sim.fighters[1];
        victim.state = FighterStateId.Hitstun;
        victim.stateTick = 0;
        victim.stunTicks = 8;                       // still stunned when the grab appears (tick 4), free on tick 7

        Run(sim, Buttons.Light, None, 1);
        Run(sim, None, None, 20);

        Assert.AreEqual(90, victim.hp, "a grab that is still active catches them once their stun ends");
    }

    [Test]
    public void UnblockableHitIgnoresBlocking()
    {
        MoveDef strike = TestFighter.MeleeMove(damage: 10);
        strike.hitboxes[0].unblockable = true;
        GameSim sim = NewMatch(new TestFighter { light = strike }, new TestFighter(), 1100);

        Run(sim, Buttons.Light, BackP1, 1);
        Run(sim, None, BackP1, 20);

        Assert.AreEqual(90, sim.fighters[1].hp);
    }

    // ================= the standard grab =================

    [Test]
    public void StandardGrabBeatsBlockingOpponent()
    {
        // Holding back also walks backwards, so a blocker in open space walks out of grab range.
        // Corner them: against the wall, holding back only blocks. Brawler bodies touch at 1200.
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 1200);
        sim.fighters[1].x = GameSim.ArenaHalf;
        sim.fighters[0].x = GameSim.ArenaHalf - 1200;
        Fighter victim = sim.fighters[1];
        bool sawGrabEvent = false;
        bool sawBlockstun = false;

        sim.Step(Buttons.Grab, BackP1);
        for (int i = 0; i < 20; i++)
        {
            sim.Step(None, BackP1);
            for (int e = 0; e < sim.events.Count; e++)
            {
                if (sim.events[e].type == SimEventType.Grab) sawGrabEvent = true;
            }
            if (victim.state == FighterStateId.Blockstun) sawBlockstun = true;
        }

        Assert.AreEqual(victim.MaxHp - BaseFighter.StandardGrab.hitboxes[0].damage, victim.hp);
        Assert.IsTrue(sawGrabEvent, "a landed grab sends a Grab event");
        Assert.IsFalse(sawBlockstun);
    }

    [Test]
    public void BackThrowPutsVictimOnTheOtherSide()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 1200);

        Run(sim, Buttons.Grab | BackP0, None, 1);
        Run(sim, BackP0, None, 10);

        Assert.Less(sim.fighters[1].x, sim.fighters[0].x, "the victim should end up behind the grabber");
        Assert.Less(sim.fighters[1].hp, sim.fighters[1].MaxHp);
    }

    [Test]
    public void ForwardThrowKeepsSides()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 1200);

        Run(sim, Buttons.Grab, None, 1);
        Run(sim, None, None, 10);

        Assert.Greater(sim.fighters[1].x, sim.fighters[0].x);
        Assert.Less(sim.fighters[1].hp, sim.fighters[1].MaxHp);
    }

    [Test]
    public void WhiffedGrabHasLongRecovery()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 6000);   // far out of reach
        Fighter grabber = sim.fighters[0];

        Run(sim, Buttons.Grab, None, 1);
        Run(sim, None, None, BaseFighter.StandardGrab.totalTicks - 1);
        Assert.AreEqual(FighterStateId.Attack, grabber.state, "still recovering");
        Run(sim, None, None, 1);
        Assert.AreEqual(FighterStateId.Idle, grabber.state);
    }

    [Test]
    public void GrabWinsOverHeavyWhenBothArePressed()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 6000);
        sim.Step(Buttons.Grab | Buttons.Heavy, None);
        Assert.AreSame(BaseFighter.StandardGrab, sim.fighters[0].move);
    }

    // ================= dashes =================

    // Tap `dir`, wait `gapTicks` ticks with nothing pressed, tap `dir` again.
    static void DoubleTap(GameSim sim, int dir, int gapTicks)
    {
        sim.Step(dir, None);
        Run(sim, None, None, gapTicks);
        sim.Step(dir, None);
    }

    [Test]
    public void DoubleTapForwardDashesTheFullDistance()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 8000);
        Fighter f = sim.fighters[0];
        DashDef dash = f.ForwardDash;
        bool sawDashEvent = false;

        sim.Step(Buttons.Right, None);
        Run(sim, None, None, 3);
        int before = f.x;
        sim.Step(Buttons.Right, None);
        for (int e = 0; e < sim.events.Count; e++)
        {
            if (sim.events[e].type == SimEventType.Dash && sim.events[e].amount == 1) sawDashEvent = true;
        }
        Assert.AreEqual(FighterStateId.Dash, f.state);
        Assert.IsTrue(f.IsDashingForward);
        Run(sim, None, None, dash.ticks + 5);

        Assert.AreEqual(before + dash.ticks * dash.speed, f.x, "a forward dash covers ticks x speed");
        Assert.AreEqual(FighterStateId.Idle, f.state, "and then it ends");
        Assert.IsTrue(sawDashEvent);
    }

    [Test]
    public void DoubleTapBackDashes()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 4000);
        Fighter f = sim.fighters[0];
        DoubleTap(sim, BackP0, 3);

        Assert.AreEqual(FighterStateId.Dash, f.state);
        Assert.IsFalse(f.IsDashingForward);
        Assert.Less(f.vx, 0, "player 1 dashes back to the left");
    }

    [Test]
    public void SecondTapOnTheLastTickOfTheWindowStillDashes()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 8000);
        DoubleTap(sim, Buttons.Right, Fighter.DashTapWindow - 1);   // taps on ticks 0 and 12
        Assert.AreEqual(FighterStateId.Dash, sim.fighters[0].state);
    }

    [Test]
    public void SecondTapOneTickTooLateDoesNotDash()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 8000);
        DoubleTap(sim, Buttons.Right, Fighter.DashTapWindow);       // taps on ticks 0 and 13
        Assert.AreEqual(FighterStateId.Walk, sim.fighters[0].state);
    }

    [Test]
    public void HoldingADirectionNeverDashes()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 8000);
        for (int i = 0; i < 60; i++)
        {
            sim.Step(Buttons.Right, None);
            Assert.AreNotEqual(FighterStateId.Dash, sim.fighters[0].state);
        }
    }

    [Test]
    public void CannotBlockWhileDashing()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 4000);
        Fighter f = sim.fighters[0];
        sim.Step(BackP0, None);
        Run(sim, None, None, 2);
        sim.Step(BackP0, None);   // back dash, and keep holding back
        sim.Step(BackP0, None);

        Assert.AreEqual(FighterStateId.Dash, f.state);
        Assert.IsTrue(f.holdingBack);
        Assert.IsFalse(f.IsBlocking, "a dash is a commitment: no blocking until it ends");
    }

    // ================= jumping =================

    // Runs with nothing pressed until fighter `i` is back on the ground. Returns the ticks it took.
    static int RunUntilLanded(GameSim sim, int i)
    {
        int ticks = 0;
        while (sim.fighters[i].IsAirborne && ticks < 200)
        {
            sim.Step(None, None);
            ticks++;
        }
        Assert.IsFalse(sim.fighters[i].IsAirborne, "should have landed");
        return ticks;
    }

    [Test]
    public void JumpGoesUpAndComesBackDown()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 8000);
        Fighter f = sim.fighters[0];
        int startX = f.x;

        sim.Step(Buttons.Up, None);
        Assert.AreEqual(FighterStateId.Jump, f.state);
        Assert.Greater(f.y, 0);

        int peak = 0;
        int ticks = 1;
        while (f.IsAirborne && ticks < 200)
        {
            sim.Step(None, None);
            ticks++;
            if (f.y > peak) peak = f.y;
        }
        Assert.AreEqual(0, f.y);
        Assert.AreEqual(FighterStateId.Idle, f.state);
        Assert.AreEqual(startX, f.x, "Up alone jumps straight up");
        Assert.Greater(peak, 1300, "high enough to clear a Special projectile");
        Assert.That(ticks, Is.InRange(25, 50), "about half a second in the air");
    }

    [Test]
    public void JumpOverTheOpponentSwitchesSides()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 1400);
        sim.Step(Buttons.Up | Buttons.Right, None);
        RunUntilLanded(sim, 0);
        Run(sim, None, None, 2);   // landing on top of them: pushboxes separate the bodies

        Assert.Greater(sim.fighters[0].x, sim.fighters[1].x, "player 1 should land on the far side");
        Assert.AreEqual(-1, sim.fighters[0].facing, "and turn around once on the ground");
        Assert.GreaterOrEqual(BodyGap(sim), 0);
    }

    // Jumps once with `takeOff` and holds `inAir` for the whole jump. Returns how far player 1 moved.
    static int JumpDistance(int takeOff, int inAir)
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 12000);
        Fighter f = sim.fighters[0];
        int startX = f.x;
        sim.Step(takeOff, None);
        while (f.IsAirborne) sim.Step(inAir, None);
        return f.x - startX;
    }

    [Test]
    public void AirControlNudgesAStraightUpJump()
    {
        int drift = JumpDistance(Buttons.Up, Buttons.Right);
        int fullForward = JumpDistance(Buttons.Up | Buttons.Right, None);
        Assert.Greater(drift, 0, "holding right in the air drifts right");
        Assert.Less(drift, fullForward, "but less than jumping forward in the first place");
    }

    [Test]
    public void AirControlSlowsButDoesNotInstantlyReverseAJump()
    {
        int steeredBack = JumpDistance(Buttons.Up | Buttons.Right, Buttons.Left);
        int fullForward = JumpDistance(Buttons.Up | Buttons.Right, None);
        Assert.Less(steeredBack, fullForward, "holding back shortens a forward jump");
        Assert.Greater(steeredBack, -fullForward, "but can't turn it into a full backward jump");
    }

    [Test]
    public void AirControlNeverExceedsJumpSpeed()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 12000);
        Fighter f = sim.fighters[0];
        sim.Step(Buttons.Up | Buttons.Right, None);
        while (f.IsAirborne)
        {
            sim.Step(Buttons.Right, None);
            Assert.LessOrEqual(f.vx, f.JumpArc.forward);
        }
    }

    // ================= Shift dash and air dash =================

    static int DashEvents(GameSim sim)
    {
        int n = 0;
        for (int e = 0; e < sim.events.Count; e++)
        {
            if (sim.events[e].type == SimEventType.Dash && sim.events[e].a == 0) n++;
        }
        return n;
    }

    [Test]
    public void ShiftPlusDirectionDashes()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 8000);
        sim.Step(Buttons.Dash | Buttons.Right, None);
        Assert.AreEqual(FighterStateId.Dash, sim.fighters[0].state);
        Assert.IsTrue(sim.fighters[0].IsDashingForward);
    }

    [Test]
    public void ShiftFirstThenDirectionAlsoDashes()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 8000);
        sim.Step(Buttons.Dash, None);
        Assert.AreNotEqual(FighterStateId.Dash, sim.fighters[0].state, "Shift alone does nothing");
        sim.Step(Buttons.Dash | BackP0, None);
        Assert.AreEqual(FighterStateId.Dash, sim.fighters[0].state);
        Assert.IsFalse(sim.fighters[0].IsDashingForward, "a back dash");
    }

    [Test]
    public void HoldingShiftAndADirectionDashesOnlyOnce()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 12000);
        int dashes = 0;
        for (int i = 0; i < 90; i++)
        {
            sim.Step(Buttons.Dash | Buttons.Right, None);
            dashes += DashEvents(sim);
        }
        Assert.AreEqual(1, dashes, "then it walks");
    }

    [Test]
    public void AirDashFliesStraightThenFalls()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 12000);
        Fighter f = sim.fighters[0];
        sim.Step(Buttons.Up, None);
        Run(sim, None, None, 8);
        sim.Step(Buttons.Dash | Buttons.Right, None);
        Assert.AreEqual(FighterStateId.Dash, f.state);
        Assert.IsTrue(f.IsAirborne);

        int height = f.y;
        int startX = f.x;
        for (int i = 1; i < f.AirDash.ticks; i++)
        {
            sim.Step(None, None);
            Assert.AreEqual(height, f.y, "no gravity during an air dash");
        }
        Assert.Greater(f.x, startX);
        sim.Step(None, None);
        Assert.AreEqual(FighterStateId.Jump, f.state, "back to falling");
        RunUntilLanded(sim, 0);
        Assert.AreEqual(FighterStateId.Idle, f.state);
    }

    [Test]
    public void DoubleTapAirDashes()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 12000);
        sim.Step(Buttons.Up, None);
        Run(sim, None, None, 5);
        sim.Step(Buttons.Right, None);
        sim.Step(None, None);
        sim.Step(Buttons.Right, None);
        Assert.AreEqual(FighterStateId.Dash, sim.fighters[0].state);
        Assert.IsTrue(sim.fighters[0].IsAirborne);
    }

    [Test]
    public void OnlyOneAirDashPerJump()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 12000);
        Fighter f = sim.fighters[0];
        sim.Step(Buttons.Up, None);
        Run(sim, None, None, 3);
        int dashes = 0;
        while (f.IsAirborne)
        {
            sim.Step(Buttons.Dash | Buttons.Right, None);   // re-press every other tick
            dashes += DashEvents(sim);
            if (!f.IsAirborne) break;
            sim.Step(None, None);
        }
        Assert.AreEqual(1, dashes);
    }

    [Test]
    public void CanStillAirAttackAfterAnAirDash()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 12000);
        Fighter f = sim.fighters[0];
        sim.Step(Buttons.Up, None);
        Run(sim, None, None, 3);
        sim.Step(Buttons.Dash | Buttons.Right, None);
        Run(sim, None, None, f.AirDash.ticks);
        Assert.AreEqual(FighterStateId.Jump, f.state);
        sim.Step(Buttons.Light, None);
        Assert.AreSame(BaseFighter.StandardJumpLight, f.move);
    }

    [Test]
    public void CannotBlockInTheAir()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 4000);
        Fighter f = sim.fighters[0];
        Run(sim, Buttons.Up | BackP0, None, 3);

        Assert.IsTrue(f.IsAirborne);
        Assert.IsTrue(f.holdingBack);
        Assert.IsFalse(f.IsBlocking, "a jump is a commitment: no blocking until you land");
    }

    [Test]
    public void GrabCannotCatchAnAirborneFighter()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 1200);
        sim.Step(Buttons.Grab, Buttons.Up);   // player 2 jumps as player 1 grabs
        Run(sim, None, None, 20);

        Assert.AreEqual(sim.fighters[1].MaxHp, sim.fighters[1].hp);
    }

    [Test]
    public void JumpInHeavyHitsAStandingOpponent()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 2400);
        Fighter jumper = sim.fighters[0];
        sim.Step(Buttons.Up | Buttons.Right, None);
        while (jumper.vy >= 0) sim.Step(None, None);   // wait for the way down
        sim.Step(Buttons.Heavy, None);
        Assert.AreSame(BaseFighter.StandardJumpHeavy, jumper.move);
        RunUntilLanded(sim, 0);

        Fighter victim = sim.fighters[1];
        Assert.AreEqual(victim.MaxHp - BaseFighter.StandardJumpHeavy.hitboxes[0].damage, victim.hp);
    }

    [Test]
    public void OnlyOneAirAttackPerJump()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 8000);
        Fighter f = sim.fighters[0];
        sim.Step(Buttons.Up, None);   // Up + Light on the ground would jab instead of jumping
        int attacks = 0;
        while (f.IsAirborne)
        {
            sim.Step(Buttons.Light, None);   // keep holding Light the whole jump
            for (int e = 0; e < sim.events.Count; e++)
            {
                if (sim.events[e].type == SimEventType.AttackStart && sim.events[e].a == 0) attacks++;
            }
        }
        Assert.AreEqual(1, attacks);
    }

    [Test]
    public void LandingCancelsAnUnfinishedAirAttack()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 8000);
        Fighter f = sim.fighters[0];
        sim.Step(Buttons.Up, None);
        while (f.y > 400 || f.vy > 0) sim.Step(None, None);   // almost back down
        sim.Step(Buttons.Heavy, None);
        Assert.AreEqual(FighterStateId.Attack, f.state);

        RunUntilLanded(sim, 0);
        Assert.AreEqual(FighterStateId.Idle, f.state);
        Assert.IsNull(f.move);
        Assert.AreEqual(0, sim.hitboxes.Count, "the air attack's hitbox is gone too");
    }

    [Test]
    public void HitstunLastsUntilAnAirborneFighterLands()
    {
        MoveDef poke = TestFighter.MeleeMove(damage: 5);
        poke.hitboxes[0].hitstun = 2;   // much shorter than the fall
        GameSim sim = NewMatch(new TestFighter { light = poke }, new TestFighter(), 1100);
        Fighter victim = sim.fighters[1];

        sim.Step(Buttons.Light, Buttons.Up);   // player 2 jumps into player 1's poke
        int guard = 0;
        while (victim.state != FighterStateId.Hitstun && guard++ < 10) sim.Step(None, None);
        Assert.AreEqual(FighterStateId.Hitstun, victim.state, "the poke should catch the rising jump");
        Assert.IsTrue(victim.IsAirborne);

        while (victim.IsAirborne)
        {
            Assert.AreEqual(FighterStateId.Hitstun, victim.state, "no recovering in mid-air");
            sim.Step(None, None);
        }
        Run(sim, None, None, 3);
        Assert.AreEqual(FighterStateId.Idle, victim.state, "back to normal after landing");
    }

    [Test]
    public void InspectorCharacterWithoutAirMovesGetsTheStandardOnes()
    {
        GameSim sim = NewMatch(new DataFighter(CharacterDef.CreateDefault()), new TestFighter(), 8000);
        sim.Step(Buttons.Up, None);
        sim.Step(Buttons.Heavy, None);
        Assert.AreSame(BaseFighter.StandardJumpHeavy, sim.fighters[0].move);
    }

    // ================= meter, Special, Ultimate =================

    [Test]
    public void LandedHitGivesMeterToBoth()
    {
        GameSim sim = NewMatch(new TestFighter { light = TestFighter.MeleeMove(damage: 10) }, new TestFighter(), 1100);
        Run(sim, Buttons.Light, None, 1);
        Run(sim, None, None, 20);

        Assert.AreEqual(10 * Fighter.MeterPerDamageDealt, sim.fighters[0].meter);
        Assert.AreEqual(10 * Fighter.MeterPerDamageTaken, sim.fighters[1].meter);
    }

    [Test]
    public void BlockedHitGivesMeterToBoth()
    {
        GameSim sim = NewMatch(new TestFighter { light = TestFighter.MeleeMove(damage: 10) }, new TestFighter(), 1100);
        Run(sim, Buttons.Light, BackP1, 1);
        Run(sim, None, BackP1, 20);

        Assert.AreEqual(100, sim.fighters[1].hp, "blocked");
        Assert.AreEqual(10 * Fighter.MeterPerDamageBlocked, sim.fighters[0].meter);
        Assert.AreEqual(10 * Fighter.MeterPerDamageBlocked, sim.fighters[1].meter);
    }

    [Test]
    public void MeterNeverGoesAboveMax()
    {
        Fighter f = new TestFighter();
        f.AddMeter(Fighter.MeterMax * 3);
        Assert.AreEqual(Fighter.MeterMax, f.meter);
    }

    [Test]
    public void UltimateNeedsAFullBar()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 6000);
        sim.fighters[0].meter = Fighter.MeterMax - 1;
        sim.Step(Buttons.Ultimate, None);

        Assert.AreNotEqual(FighterStateId.Attack, sim.fighters[0].state);
        Assert.AreEqual(0, sim.freezeTicks);
    }

    [Test]
    public void UltimateSpendsTheBarAndFreezesTheMatch()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 6000);
        Fighter user = sim.fighters[0];
        Fighter other = sim.fighters[1];
        user.meter = Fighter.MeterMax;

        sim.Step(Buttons.Ultimate, Buttons.Left);
        bool sawFlash = false;
        for (int e = 0; e < sim.events.Count; e++)
        {
            if (sim.events[e].type == SimEventType.SuperFlash && sim.events[e].a == 0) sawFlash = true;
        }
        Assert.IsTrue(sawFlash);
        Assert.AreSame(BaseFighter.StandardUltimate, user.move);
        Assert.AreEqual(0, user.meter, "the whole bar is spent");
        Assert.AreEqual(GameSim.SuperFlashTicks, sim.freezeTicks);

        // During the freeze nobody moves or thinks, whatever they press.
        int otherX = other.x;
        int userTick = user.stateTick;
        for (int i = 0; i < GameSim.SuperFlashTicks; i++)
        {
            sim.Step(Buttons.Light, Buttons.Left);
            Assert.AreEqual(otherX, other.x, "frozen on tick " + i);
            Assert.AreEqual(userTick, user.stateTick);
        }
        Assert.AreEqual(0, sim.freezeTicks);

        sim.Step(None, Buttons.Left);
        Assert.AreNotEqual(otherX, other.x, "the match runs again after the freeze");
    }

    [Test]
    public void UltimateNeedsAFreshPress()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 6000);
        Fighter f = sim.fighters[0];
        sim.Step(Buttons.Ultimate, None);   // holding O with an empty bar does nothing...
        f.meter = Fighter.MeterMax;
        sim.Step(Buttons.Ultimate, None);   // ...and keeping it held when the bar fills doesn't fire
        Assert.AreNotEqual(FighterStateId.Attack, f.state);

        sim.Step(None, None);
        sim.Step(Buttons.Ultimate, None);   // a new press does
        Assert.AreSame(BaseFighter.StandardUltimate, f.move);
    }

    [Test]
    public void UltimateHitsGiveNoMeter()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 1200);
        sim.fighters[0].meter = Fighter.MeterMax;
        Run(sim, Buttons.Ultimate, None, 1);
        Run(sim, None, None, GameSim.SuperFlashTicks + 20);

        Fighter victim = sim.fighters[1];
        Assert.AreEqual(victim.MaxHp - BaseFighter.StandardUltimate.hitboxes[0].damage, victim.hp, "the ultimate landed");
        Assert.AreEqual(0, sim.fighters[0].meter);
        Assert.AreEqual(0, victim.meter);
    }

    [Test]
    public void SpecialIsOneProjectileAtATime()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 14000);
        int mostOnScreen = 0;
        int thrown = 0;
        for (int i = 0; i < 600; i++)
        {
            sim.Step(Buttons.Special, None);
            if (sim.fighters[0].state == FighterStateId.Attack && sim.fighters[0].stateTick == 0) thrown++;
            int onScreen = 0;
            for (int h = 0; h < sim.hitboxes.Count; h++)
            {
                if (sim.hitboxes[h].owner == 0 && !sim.hitboxes[h].def.followOwner) onScreen++;
            }
            if (onScreen > mostOnScreen) mostOnScreen = onScreen;
        }

        Assert.AreEqual(1, mostOnScreen);
        Assert.GreaterOrEqual(thrown, 2, "a new one can be thrown once the last is gone");
    }

    [Test]
    public void InspectorCharacterUltimateAlsoNeedsAFullBar()
    {
        MoveDef ult = TestFighter.MeleeMove(damage: 20);
        ult.ultimate = true;
        CharacterDef def = new CharacterDef();
        def.moves = new MoveBinding[] { new MoveBinding { button = AttackButton.Ultimate, move = ult } };
        GameSim sim = NewMatch(new DataFighter(def), new TestFighter(), 1100);

        sim.Step(Buttons.Ultimate, None);
        Assert.AreNotEqual(FighterStateId.Attack, sim.fighters[0].state, "empty bar");

        sim.Step(None, None);
        sim.fighters[0].meter = Fighter.MeterMax;
        sim.Step(Buttons.Ultimate, None);
        Assert.AreSame(ult, sim.fighters[0].move);
        Assert.AreEqual(GameSim.SuperFlashTicks, sim.freezeTicks);
    }

    // ================= rounds =================

    // Puts the two fighters next to each other and lets `winner` KO the other with a Light on
    // 1 HP, the real way (a hit), then runs until the round has ended.
    static void WinRound(GameSim sim, int winner)
    {
        int loser = 1 - winner;
        sim.fighters[0].x = -600;
        sim.fighters[1].x = 600;
        sim.fighters[loser].hp = 1;
        int roundBefore = sim.round;
        int[] input = new int[2];
        input[winner] = Buttons.Light;
        sim.Step(input[0], input[1]);
        for (int i = 0; i < 60 && sim.roundOverTicks == 0 && !sim.IsOver && sim.round == roundBefore; i++)
        {
            sim.Step(None, None);
        }
        Assert.IsTrue(sim.roundOverTicks > 0 || sim.IsOver, "the round should have ended with a KO");
    }

    static void RunUntilNextRound(GameSim sim)
    {
        int roundBefore = sim.round;
        for (int i = 0; i < GameSim.RoundEndDelay + 5 && sim.round == roundBefore; i++) sim.Step(None, None);
        Assert.AreEqual(roundBefore + 1, sim.round);
    }

    [Test]
    public void KoStartsTheNextRoundAfterThePause()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 1200);
        Fighter oldP1 = sim.fighters[1];
        WinRound(sim, 0);

        Assert.AreEqual(1, sim.roundsWon[0]);
        Assert.AreEqual(0, sim.roundsWon[1]);
        Assert.IsFalse(sim.IsOver, "one round is not the match");
        Assert.AreEqual(GameSim.RoundEndDelay, sim.roundOverTicks);

        bool sawRoundStart = false;
        int ticks = 0;
        while (sim.round == 1 && ticks < 500)
        {
            sim.Step(None, None);
            ticks++;
            for (int e = 0; e < sim.events.Count; e++)
            {
                if (sim.events[e].type == SimEventType.RoundStart && sim.events[e].amount == 2) sawRoundStart = true;
            }
        }
        Assert.AreEqual(GameSim.RoundEndDelay, ticks, "the pause lasts RoundEndDelay ticks");
        Assert.IsTrue(sawRoundStart);
        Assert.AreNotSame(oldP1, sim.fighters[1], "a new round builds fresh fighters");
        Assert.AreEqual(sim.fighters[1].MaxHp, sim.fighters[1].hp);
        Assert.AreEqual(-GameSim.StartX, sim.fighters[0].x);
        Assert.AreEqual(GameSim.StartX, sim.fighters[1].x);
        Assert.AreEqual(0, sim.hitboxes.Count);
    }

    [Test]
    public void MeterCarriesIntoTheNextRound()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 1200);
        sim.fighters[0].meter = 500;
        WinRound(sim, 0);
        int meter0 = sim.fighters[0].meter;
        int meter1 = sim.fighters[1].meter;
        Assert.Greater(meter0, 500, "the KO hit gave meter");
        Assert.Greater(meter1, 0, "getting hit gave meter");

        RunUntilNextRound(sim);

        Assert.AreEqual(meter0, sim.fighters[0].meter);
        Assert.AreEqual(meter1, sim.fighters[1].meter);
    }

    [Test]
    public void NothingCanBeHitDuringTheKoPause()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 6000);
        Run(sim, None, Buttons.Special, 1);    // player 2 throws a projectile at player 1...
        Run(sim, None, None, 14);
        Assert.AreEqual(1, sim.hitboxes.Count, "the projectile is in the air");
        sim.fighters[1].hp = 0;               // ...and is KO'd before it arrives
        sim.Step(None, None);
        Assert.Greater(sim.roundOverTicks, 0);

        Run(sim, None, None, GameSim.RoundEndDelay - 5);
        Assert.AreEqual(sim.fighters[0].MaxHp, sim.fighters[0].hp, "the winner can't be hit after the KO");
    }

    [Test]
    public void TwoRoundsWinTheMatch()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 1200);
        WinRound(sim, 1);
        RunUntilNextRound(sim);
        WinRound(sim, 0);
        RunUntilNextRound(sim);
        Assert.AreEqual(3, sim.round);
        Assert.IsFalse(sim.IsOver, "1-1 goes to a third round");

        WinRound(sim, 1);
        Assert.IsTrue(sim.IsOver);
        Assert.AreEqual(1, sim.Winner);

        int tickAtEnd = sim.tick;
        sim.Step(Buttons.Light, Buttons.Light);
        Assert.AreEqual(tickAtEnd, sim.tick, "a finished match doesn't run any more");
    }

    [Test]
    public void DoubleKoGivesBothPlayersTheRound()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 1200);
        sim.fighters[0].hp = 1;
        sim.fighters[1].hp = 1;
        Run(sim, Buttons.Light, Buttons.Light, 1);   // both jab on the same tick: a trade
        Run(sim, None, None, 10);

        Assert.AreEqual(1, sim.roundsWon[0]);
        Assert.AreEqual(1, sim.roundsWon[1]);
        Assert.IsFalse(sim.IsOver);
    }

    [Test]
    public void DoubleKoOnMatchPointIsADraw()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 1200);
        sim.roundsWon[0] = 1;
        sim.roundsWon[1] = 1;
        sim.fighters[0].hp = 1;
        sim.fighters[1].hp = 1;
        Run(sim, Buttons.Light, Buttons.Light, 1);
        Run(sim, None, None, 10);

        Assert.IsTrue(sim.IsOver);
        Assert.AreEqual(2, sim.Winner);
    }

    // ================= pushboxes =================

    static int BodyGap(GameSim sim)
    {
        Box a = sim.fighters[0].HurtboxWorld();
        Box b = sim.fighters[1].HurtboxWorld();
        return a.x < b.x ? b.x - (a.x + a.w) : a.x - (b.x + b.w);   // negative = overlapping
    }

    [Test]
    public void WalkingIntoEachOtherNeverOverlaps()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 4000);
        for (int i = 0; i < 200; i++)
        {
            sim.Step(Buttons.Right, Buttons.Left);   // both walk forward
            Assert.GreaterOrEqual(BodyGap(sim), 0, "bodies overlap on tick " + i);
        }
        Assert.Less(sim.fighters[0].x, sim.fighters[1].x, "they must not pass through each other");
        Assert.AreEqual(-sim.fighters[0].x, sim.fighters[1].x, "equal pushing should meet in the middle");
    }

    [Test]
    public void FighterAgainstTheWallStaysAndTheOtherIsPushed()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 2000);
        sim.fighters[1].x = GameSim.ArenaHalf;
        sim.fighters[0].x = GameSim.ArenaHalf - 1500;
        for (int i = 0; i < 60; i++) sim.Step(Buttons.Right, None);

        Assert.AreEqual(GameSim.ArenaHalf, sim.fighters[1].x);
        Assert.GreaterOrEqual(BodyGap(sim), 0);
    }

    [Test]
    public void SameSpotTieBreakPutsPlayerOneLeft()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 0);
        sim.Step(None, None);

        Assert.Less(sim.fighters[0].x, sim.fighters[1].x);
        Assert.GreaterOrEqual(BodyGap(sim), 0);
    }

    [Test]
    public void NormalHitIsStillBlocked()
    {
        GameSim sim = NewMatch(new TestFighter { light = TestFighter.MeleeMove(damage: 10) }, new TestFighter(), 1100);

        Run(sim, Buttons.Light, BackP1, 1);
        Run(sim, None, BackP1, 20);

        Assert.AreEqual(100, sim.fighters[1].hp);
    }
}


// Pre-round countdown uses simulation ticks so online peers unlock together.
public class CountdownTests
{
    [NUnit.Framework.Test]
    public void CountdownBlocksInputThenUnlocksAtFight()
    {
        GameSim sim = new GameSim { countdownOn = true };
        sim.Reset(1, CharacterList.Factory(0), CharacterList.Factory(0));
        for (int i = 0; i < 180; i++)
        {
            NUnit.Framework.Assert.AreEqual((3 - i / 60).ToString(), sim.CountdownText);
            sim.Step(Buttons.Right | Buttons.Light, Buttons.Left | Buttons.Heavy);
            NUnit.Framework.Assert.AreEqual(-GameSim.StartX, sim.fighters[0].x);
            NUnit.Framework.Assert.AreEqual(GameSim.StartX, sim.fighters[1].x);
            NUnit.Framework.Assert.IsNull(sim.fighters[0].move);
            NUnit.Framework.Assert.IsNull(sim.fighters[1].move);
        }
        NUnit.Framework.Assert.AreEqual("FIGHT!", sim.CountdownText);
        sim.Step(Buttons.Right, 0);
        NUnit.Framework.Assert.Greater(sim.fighters[0].x, -GameSim.StartX);
        for (int i = 1; i < 60; i++) sim.Step(0, 0);
        NUnit.Framework.Assert.AreEqual("", sim.CountdownText);
    }

    // Real matches: "ROUND 1" on its own, then 3-2-1, then FIGHT; nobody moves until FIGHT.
    [NUnit.Framework.Test]
    public void RoundCallComesBeforeTheCountdown()
    {
        GameSim sim = new GameSim { countdownOn = true, roundCallOn = true };
        sim.Reset(1, CharacterList.Factory(0), CharacterList.Factory(0));
        for (int i = 0; i < GameSim.RoundCallTicks + GameSim.CountdownTicks; i++)
        {
            if (i < GameSim.RoundCallTicks)
                NUnit.Framework.Assert.AreEqual(GameSim.CountdownTicks, sim.countdownTicks, "the 3-2-1 waits for the round call");
            if (i == GameSim.RoundCallTicks) NUnit.Framework.Assert.AreEqual("3", sim.CountdownText);
            sim.Step(Buttons.Right, Buttons.Left);
            NUnit.Framework.Assert.AreEqual(-GameSim.StartX, sim.fighters[0].x);
        }
        NUnit.Framework.Assert.AreEqual("FIGHT!", sim.CountdownText);
        sim.Step(Buttons.Right, 0);
        NUnit.Framework.Assert.Greater(sim.fighters[0].x, -GameSim.StartX);
    }

    [NUnit.Framework.Test]
    public void CountdownRestartsForNextRoundAndResetAndIsHashed()
    {
        GameSim a = new GameSim { countdownOn = true };
        GameSim b = new GameSim { countdownOn = true };
        a.Reset(1, CharacterList.Factory(0), CharacterList.Factory(0));
        b.Reset(1, CharacterList.Factory(0), CharacterList.Factory(0));
        for (int i = 0; i < 180; i++)
        {
            a.Step(Buttons.Light, Buttons.Heavy);
            b.Step(Buttons.Light, Buttons.Heavy);
            NUnit.Framework.Assert.AreEqual(a.Hash(), b.Hash());
        }
        b.countdownTicks = 1;
        NUnit.Framework.Assert.AreNotEqual(a.Hash(), b.Hash());
        a.fighters[1].hp = 0;
        a.Step(0, 0);
        for (int i = 0; i < GameSim.RoundEndDelay; i++) a.Step(0, 0);
        NUnit.Framework.Assert.AreEqual(2, a.round);
        NUnit.Framework.Assert.AreEqual("3", a.CountdownText);
        a.Step(0, 0);
        a.Reset(1, CharacterList.Factory(0), CharacterList.Factory(0));
        NUnit.Framework.Assert.AreEqual(180, a.countdownTicks);
        NUnit.Framework.Assert.AreEqual(0, a.fightBannerTicks);
    }
}
