using NUnit.Framework;
using static SimHarness;

// Checks for hit stop (GameSim.hitStopTicks): a landed hit pauses the whole match for a few
// ticks, longer for bigger hits and longest for a KO. SimHarness switches hit stop off for the
// other tests, so these switch it back on.
public class HitStopTests
{
    // Two TestFighters close enough for a light to land; P0's light does `damage`.
    static GameSim NewHitStopMatch(int damage)
    {
        GameSim sim = NewMatch(new TestFighter { light = TestFighter.MeleeMove(damage) }, new TestFighter(), 1100);
        sim.hitStopOn = true;
        return sim;
    }

    // Steps until P0's light connects (hit or block). Returns false if it never does.
    static bool StepUntilContact(GameSim sim, int in1)
    {
        sim.Step(Buttons.Light, in1);
        for (int i = 0; i < 20; i++)
        {
            for (int e = 0; e < sim.events.Count; e++)
            {
                SimEventType t = sim.events[e].type;
                if (t == SimEventType.Hit || t == SimEventType.Block) return true;
            }
            sim.Step(None, in1);
        }
        return false;
    }

    [Test]
    public void HitPausesTheMatch()
    {
        GameSim sim = NewHitStopMatch(10);
        Assert.IsTrue(StepUntilContact(sim, None), "the light should land");

        int expected = GameSim.HitStopBase + 10 / GameSim.HitStopPerDamage;
        Assert.AreEqual(expected, sim.hitStopTicks);

        // During the stop nobody moves or thinks, whatever they press.
        Fighter victim = sim.fighters[1];
        int victimX = victim.x;
        int victimTick = victim.stateTick;
        for (int i = 0; i < expected; i++)
        {
            sim.Step(Buttons.Heavy, Buttons.Right);
            Assert.AreEqual(victimX, victim.x, "frozen on tick " + i);
            Assert.AreEqual(victimTick, victim.stateTick);
        }
        Assert.AreEqual(0, sim.hitStopTicks);

        sim.Step(None, None);
        Assert.AreNotEqual(victimTick, victim.stateTick, "the match runs again after the stop");
    }

    [Test]
    public void BiggerHitsPauseLongerUpToTheMax()
    {
        GameSim small = NewHitStopMatch(3);
        GameSim big = NewHitStopMatch(90);
        Assert.IsTrue(StepUntilContact(small, None));
        Assert.IsTrue(StepUntilContact(big, None));

        Assert.Less(small.hitStopTicks, big.hitStopTicks);
        Assert.AreEqual(GameSim.HitStopMax, big.hitStopTicks);
    }

    [Test]
    public void BlockedHitPausesBriefly()
    {
        GameSim sim = NewHitStopMatch(10);
        Assert.IsTrue(StepUntilContact(sim, BackP1));

        Assert.AreEqual(100, sim.fighters[1].hp, "blocked");
        Assert.AreEqual(GameSim.BlockStop, sim.hitStopTicks);
    }

    [Test]
    public void KOHitPausesBeforeTheRoundOverPause()
    {
        GameSim sim = NewHitStopMatch(10);
        sim.fighters[1].hp = 10;
        Assert.IsTrue(StepUntilContact(sim, None));

        Assert.AreEqual(GameSim.KOStop, sim.hitStopTicks);
        Assert.AreEqual(GameSim.RoundEndDelay, sim.roundOverTicks);

        Run(sim, None, None, GameSim.KOStop);
        Assert.AreEqual(GameSim.RoundEndDelay, sim.roundOverTicks, "the round-over pause waits for the hit stop");
    }

    [Test]
    public void HitStopCanBeSwitchedOff()
    {
        GameSim sim = NewHitStopMatch(10);
        sim.hitStopOn = false;
        Assert.IsTrue(StepUntilContact(sim, None));

        Assert.AreEqual(0, sim.hitStopTicks);
    }
}
