using System.Collections.Generic;
using NUnit.Framework;

// Checks that the offline opponent (AIInputSource) can actually play: AI vs AI matches must finish,
// and across them the AI must use the whole shared kit (block, grab, dash, Special, Ultimate).
// The AI only produces button bits, so this also proves those moves can be done from plain input.
public class AITests
{
    const int MaxTicks = 60000;

    // Feeds both AIs to the sim the way LockstepRunner does with no input delay.
    static GameSim PlayAIMatch(int p0, int p1, HashSet<SimEventType> seen)
    {
        GameSim sim = new GameSim();
        sim.Reset(3, CharacterList.Factory(p0), CharacterList.Factory(p1));
        AIInputSource ai0 = new AIInputSource(sim, 0);
        AIInputSource ai1 = new AIInputSource(sim, 1);

        for (int t = 0; t < MaxTicks && !sim.IsOver; t++)
        {
            int tick = sim.tick;
            ai0.Sample(tick);
            ai1.Sample(tick);
            int in0, in1;
            Assert.IsTrue(ai0.TryGet(tick, out in0));
            Assert.IsTrue(ai1.TryGet(tick, out in1));
            sim.Step(in0, in1);
            for (int e = 0; e < sim.events.Count; e++) seen.Add(sim.events[e].type);
        }
        return sim;
    }

    [Test]
    public void AIMatchesFinishAndUseTheWholeKit()
    {
        int[] ids = { Brawler.Id, Shooter.Id, Samurai.Id };
        HashSet<SimEventType> seen = new HashSet<SimEventType>();

        for (int i = 0; i < ids.Length; i++)
        {
            for (int j = 0; j < ids.Length; j++)
            {
                GameSim sim = PlayAIMatch(ids[i], ids[j], seen);
                Assert.IsTrue(sim.IsOver, CharacterList.NameOf(ids[i]) + " vs " + CharacterList.NameOf(ids[j])
                                          + ": AI vs AI didn't finish in " + MaxTicks + " ticks");
            }
        }

        // Every launch-roster character, as each side, against a plain opponent: the AI has to be
        // able to finish a match with all of them (Room can't block, K3N has no normal Ultimate...).
        int[] roster = { Samurai.Id, Painter.Id, Cowboy.Id, Elephant.Id, Executioner.Id, PlagueDoctor.Id, K3N.Id };
        for (int i = 0; i < roster.Length; i++)
        {
            GameSim asP1 = PlayAIMatch(roster[i], Brawler.Id, seen);
            GameSim asP2 = PlayAIMatch(Brawler.Id, roster[i], seen);
            Assert.IsTrue(asP1.IsOver && asP2.IsOver, CharacterList.NameOf(roster[i]) + ": AI match didn't finish in " + MaxTicks + " ticks");
        }

        AssertKitUsed(seen);
    }

    [Test]
    public void AIUsesTheHoldFollowUpWhileHolding()
    {
        GameSim sim = new GameSim();
        sim.hitStopOn = false;
        sim.Reset(3, CharacterList.Factory(Elephant.Id), CharacterList.Factory(Sandbag.Id));
        sim.fighters[0].x = -850;
        sim.fighters[1].x = 850;
        sim.Step(Buttons.Grab, 0);
        for (int i = 0; i < 20 && sim.fighters[1].state != FighterStateId.Held; i++) sim.Step(0, 0);
        Assert.AreEqual(FighterStateId.Held, sim.fighters[1].state, "set-up: Room is holding the Sandbag");

        AIInputSource ai = new AIInputSource(sim, 0);
        ai.Sample(sim.tick);
        int bits;
        Assert.IsTrue(ai.TryGet(sim.tick, out bits));
        Assert.AreNotEqual(0, bits & Buttons.Special, "the AI should Vine Boom the held victim");
    }

    // Runs the AI as player 0 for `ticks` ticks against an idle opponent; `check` runs every tick.
    static void RunAI(GameSim sim, int ticks, System.Action check)
    {
        AIInputSource ai = new AIInputSource(sim, 0);
        for (int t = 0; t < ticks && !sim.IsOver; t++)
        {
            ai.Sample(sim.tick);
            int bits;
            ai.TryGet(sim.tick, out bits);
            sim.Step(bits, 0);
            check();
        }
    }

    static GameSim NewMatch(int p0, int p1)
    {
        GameSim sim = new GameSim();
        sim.Reset(3, CharacterList.Factory(p0), CharacterList.Factory(p1));
        return sim;
    }

    [Test]
    public void AITogglesInstallOnUpCloseAndOffWhenFar()
    {
        GameSim sim = NewMatch(K3N.Id, Sandbag.Id);
        K3N k3n = (K3N)sim.fighters[0];
        k3n.meter = 800;
        bool turnedOn = false;
        RunAI(sim, 1200, delegate { if (k3n.installOn) turnedOn = true; });
        Assert.IsTrue(turnedOn, "the AI should switch Install on up close with meter");

        // Far away with Install on: it switches it off to save meter.
        GameSim far = NewMatch(K3N.Id, Sandbag.Id);
        K3N farK3N = (K3N)far.fighters[0];
        farK3N.meter = Fighter.MeterMax;
        farK3N.installOn = true;
        far.fighters[0].x = -7000;
        far.fighters[1].x = 7000;
        bool turnedOff = false;
        RunAI(far, 120, delegate { if (!farK3N.installOn && farK3N.meter > 0) turnedOff = true; });
        Assert.IsTrue(turnedOff, "the AI should switch Install off when far away");
    }

    [Test]
    public void AISwitchesRemysPotionToHealWhenHurtAndDrinksIt()
    {
        GameSim sim = NewMatch(PlagueDoctor.Id, Sandbag.Id);
        PlagueDoctor remy = (PlagueDoctor)sim.fighters[0];
        remy.hp = 20;
        bool choseHeal = false;
        RunAI(sim, 2400, delegate { if (remy.selectedPotion == PlagueDoctor.PotionHeal) choseHeal = true; });

        Assert.IsTrue(choseHeal, "the AI should switch to the heal potion when hurt");
        Assert.Greater(remy.hp, 20, "and drink it (holding past the tap window)");
    }

    static void AssertKitUsed(HashSet<SimEventType> seen)
    {
        SimEventType[] wanted = { SimEventType.Hit, SimEventType.Block, SimEventType.Grab, SimEventType.Dash,
                                  SimEventType.SuperFlash, SimEventType.RoundStart };
        for (int i = 0; i < wanted.Length; i++)
        {
            Assert.IsTrue(seen.Contains(wanted[i]), "the AI never caused a " + wanted[i] + " event");
        }
    }
}
