using NUnit.Framework;

public class IntroductionTests
{
    static GameSim NewIntro()
    {
        var sim = new GameSim { introductionsOn = true, countdownOn = true, roundTimerOn = true, hitStopOn = false };
        sim.Reset(1, CharacterList.Factory(10), CharacterList.Factory(14));
        return sim;
    }

    [Test]
    public void BothIntroductionsBlockCombatBeforeCountdownAndPreserveClock()
    {
        GameSim a = NewIntro(), b = NewIntro();
        for (int i = 0; i < GameSim.IntroLineTicks * 2; i++)
        {
            Assert.AreEqual(i < GameSim.IntroLineTicks ? 0 : 1, a.IntroSlot);
            a.Step(Buttons.Right | Buttons.Light, Buttons.Left | Buttons.Heavy);
            b.Step(Buttons.Right | Buttons.Light, Buttons.Left | Buttons.Heavy);
            Assert.AreEqual(a.Hash(), b.Hash());
            Assert.AreEqual(-GameSim.StartX, a.fighters[0].x);
            Assert.AreEqual(GameSim.StartX, a.fighters[1].x);
            Assert.IsNull(a.fighters[0].move);
            Assert.IsNull(a.fighters[1].move);
            Assert.AreEqual(GameSim.CountdownTicks, a.countdownTicks);
            Assert.AreEqual(GameSim.RoundTimeTicks, a.roundTimeTicks);
        }
        Assert.AreEqual(-1, a.IntroSlot);
        a.Step(0, 0);
        Assert.AreEqual(GameSim.CountdownTicks - 1, a.countdownTicks);
    }

    [Test]
    public void IntrosAreHashedReplayOnRematchAndDoNotRepeatEveryRound()
    {
        GameSim sim = NewIntro(), other = NewIntro();
        other.introTicks--;
        Assert.AreNotEqual(sim.Hash(), other.Hash());
        sim.Step(0, 0);
        sim.Reset(1, CharacterList.Factory(10), CharacterList.Factory(14));
        Assert.AreEqual(GameSim.IntroLineTicks * 2, sim.introTicks);
        for (int i = 0; i < GameSim.IntroLineTicks * 2 + GameSim.CountdownTicks; i++) sim.Step(0, 0);
        sim.fighters[1].hp = 0;
        sim.Step(0, 0);
        for (int i = 0; i < GameSim.RoundEndDelay; i++) sim.Step(0, 0);
        Assert.AreEqual(2, sim.round);
        Assert.AreEqual(0, sim.introTicks);
        Assert.AreEqual(GameSim.CountdownTicks, sim.countdownTicks);
    }
}
