using NUnit.Framework;

public class MatchupDialogueTests
{
    [Test]
    public void SelectedPairControlsExchangeAndSpeakerOrderEvenWhenSlotsSwap()
    {
        var a = MatchupDialogue.Select(10, 12, 0);
        var b = MatchupDialogue.Select(12, 10, 0);
        Assert.AreEqual(a.firstText, b.firstText);
        Assert.AreEqual(a.secondText, b.secondText);
        Assert.AreEqual(10, a.firstId);
        Assert.AreEqual(12, a.secondId);
        Assert.AreEqual("Wet Urn? What are you on about Buckaroo?", a.secondText);
        GameSim sim = new GameSim();
        a.Configure(sim, 10, 0);
        Assert.AreEqual(0, sim.introFirstSlot);
        b.Configure(sim, 12, 0);
        Assert.AreEqual(1, sim.introFirstSlot);
        Assert.AreNotEqual(a.firstText, MatchupDialogue.Select(10, 16, 0).firstText);
        Assert.AreEqual(12, MatchupDialogue.Select(10, 12, 1).firstId);
    }

    [Test]
    public void SingleSuppliedLineKeepsItsSpeakerAndDoesNotInventAReply()
    {
        var entry = MatchupDialogue.Select(15, 10, 0);
        Assert.AreEqual("Uh, what's with the rat?", entry.firstText);
        Assert.IsNull(entry.secondText);
        GameSim sim = new GameSim { introductionsOn = true, countdownOn = true };
        entry.Configure(sim, 15, 0);
        sim.Reset(1, CharacterList.Factory(15), CharacterList.Factory(10));
        Assert.AreEqual(0, sim.introSecondTicks);
        for (int i = 0; i < sim.introFirstTicks; i++)
        {
            Assert.AreEqual(1, sim.IntroSlot);
            sim.Step(Buttons.Light, Buttons.Heavy);
        }
        Assert.AreEqual(-1, sim.IntroSlot);
        Assert.AreEqual(GameSim.CountdownTicks, sim.countdownTicks);
    }

    [Test]
    public void LongExchangesUseDeterministicReadingTimeBeforeCountdown()
    {
        var entry = MatchupDialogue.Select(10, 16, 1);
        GameSim a = NewExchange(entry), b = NewExchange(entry);
        Assert.Greater(a.introSecondTicks, GameSim.IntroLineTicks);
        Assert.AreEqual(1, a.IntroSlot);
        int total = a.introTicks;
        for (int i = 0; i < total; i++)
        {
            Assert.AreEqual(i < a.introFirstTicks ? 1 : 0, a.IntroSlot);
            if (i == a.introFirstTicks) Assert.AreEqual(0, a.IntroLineElapsedTicks);
            a.Step(Buttons.Right | Buttons.Light, Buttons.Left | Buttons.Heavy);
            b.Step(Buttons.Right | Buttons.Light, Buttons.Left | Buttons.Heavy);
            Assert.AreEqual(a.Hash(), b.Hash());
            Assert.AreEqual(-GameSim.StartX, a.fighters[0].x);
            Assert.AreEqual(GameSim.StartX, a.fighters[1].x);
            Assert.AreEqual(GameSim.RoundTimeTicks, a.roundTimeTicks);
        }
        Assert.AreEqual(GameSim.CountdownTicks, a.countdownTicks);
        a.Step(0, 0);
        Assert.AreEqual(GameSim.CountdownTicks - 1, a.countdownTicks);
    }

    static GameSim NewExchange(MatchupDialogue.Exchange entry)
    {
        var sim = new GameSim { introductionsOn = true, countdownOn = true, roundTimerOn = true };
        entry.Configure(sim, 10, 1);
        sim.Reset(1, CharacterList.Factory(10), CharacterList.Factory(16));
        return sim;
    }

    [Test]
    public void EmptyMatchupsKeepExplicitPlaceholderAndNegativeVariantsAreSafe()
    {
        Assert.AreEqual("To be implemented.", MatchupDialogue.Select(13, 15, 0).firstText);
        Assert.AreEqual("To be implemented.", MatchupDialogue.Select(10, 10, 0).firstText);
        Assert.IsTrue(MatchupDialogue.Select(10, 16, int.MinValue).firstText != null);
    }
}
