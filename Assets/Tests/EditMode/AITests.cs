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
        int[] ids = { Brawler.Id, Shooter.Id, Samurai.Id };   // the six roster characters are identical for now
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

        SimEventType[] wanted = { SimEventType.Hit, SimEventType.Block, SimEventType.Grab, SimEventType.Dash,
                                  SimEventType.SuperFlash, SimEventType.RoundStart };
        for (int i = 0; i < wanted.Length; i++)
        {
            Assert.IsTrue(seen.Contains(wanted[i]), "the AI never caused a " + wanted[i] + " event");
        }
    }
}
