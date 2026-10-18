using System;
using System.Collections.Generic;
using NUnit.Framework;

// SOAK TESTS: long matches with random inputs for EVERY pair of code characters. They catch the
// bugs that break online play without failing any single small test:
//   - nondeterminism: the same inputs must give the same hash on every tick. Each match is played
//     TWICE in a row in the same process, so a move definition that gets changed during a match
//     (shared static data, see BaseFighter) makes the second run differ.
//   - crashes and impossible states (HP or meter out of range, fighters outside the arena).
//   - matches that can never end.
// They can NOT see state that is missing from the hash; that's what the review checklist in
// Design/IMPLEMENTATION_PLAN.md is for.
//
// Tests may use System.Random; the sim may not.
public class SoakTests
{
    const int RandomMatchTicks = 3000;
    const int FinishWithinTicks = 30000;

    // A random "player": picks an intent and holds it for 1-8 ticks, so it walks, blocks (holds
    // away), double-taps into dashes and uses every move now and then. Movement is TOWARD or AWAY
    // from the opponent rather than a fixed left/right, so fights stay close enough for grabs.
    class RandomPlayer
    {
        const int Toward = 1 << 20;   // placeholders, turned into Left/Right each tick
        const int Away = 1 << 21;

        static readonly int[] Choices =
        {
            0, Toward, Toward, Toward, Away, Away,
            Buttons.Light, Buttons.Heavy, Buttons.Grab, Buttons.Grab, Buttons.Special, Buttons.Ultimate,
            Toward | Buttons.Light, Toward | Buttons.Grab, Away | Buttons.Grab, Buttons.Down,
            Buttons.Up, Toward | Buttons.Up, Away | Buttons.Up,
        };

        readonly Random rng;
        readonly int me;
        int current;
        int ticksLeft;

        public RandomPlayer(int seed, int myIndex)
        {
            rng = new Random(seed);
            me = myIndex;
        }

        public int Next(GameSim sim)
        {
            if (ticksLeft <= 0)
            {
                // Far away, it usually closes in first; otherwise fights drift apart and up-close
                // rules (grabs, pushboxes, trades) would hardly ever be tested.
                bool far = Math.Abs(sim.fighters[1 - me].x - sim.fighters[me].x) > 2500;
                current = far && rng.Next(4) != 0 ? Toward : Choices[rng.Next(Choices.Length)];
                ticksLeft = rng.Next(1, 9);
            }
            ticksLeft--;

            bool opponentOnRight = sim.fighters[1 - me].x >= sim.fighters[me].x;
            int toward = opponentOnRight ? Buttons.Right : Buttons.Left;
            int away = opponentOnRight ? Buttons.Left : Buttons.Right;
            int bits = current & ~(Toward | Away);
            if ((current & Toward) != 0) bits |= toward;
            if ((current & Away) != 0) bits |= away;
            return bits;
        }
    }

    static List<int> CodeCharacterIds()
    {
        List<int> ids = new List<int>();
        for (int i = 0; i < CharacterList.Ids.Count; i++)
        {
            int id = CharacterList.Ids[i];
            if (CharacterList.IsCodeCharacter(id)) ids.Add(id);
        }
        return ids;
    }

    static GameSim NewMatch(int p0, int p1)
    {
        GameSim sim = new GameSim();
        sim.Reset(7, CharacterList.Factory(p0), CharacterList.Factory(p1));
        return sim;
    }

    // Plays a random match and returns the hash after every tick. Checks the invariants as it goes
    // and counts which events happened, so the test can prove the random play reached every feature.
    static List<int> PlayRandom(int p0, int p1, int seed, HashSet<SimEventType> seen)
    {
        GameSim sim = NewMatch(p0, p1);
        RandomPlayer a = new RandomPlayer(seed, 0);
        RandomPlayer b = new RandomPlayer(seed * 7919 + 1, 1);
        List<int> hashes = new List<int>(RandomMatchTicks);

        for (int t = 0; t < RandomMatchTicks && !sim.IsOver; t++)
        {
            int inA = a.Next(sim);   // both read the sim BEFORE the step, like real players
            int inB = b.Next(sim);
            sim.Step(inA, inB);
            hashes.Add(sim.Hash());
            if (!InvariantsHold(sim))
            {
                Assert.Fail(CharacterList.NameOf(p0) + " vs " + CharacterList.NameOf(p1) + ": impossible state on tick " + t);
            }
            for (int e = 0; e < sim.events.Count; e++) seen.Add(sim.events[e].type);
        }
        return hashes;
    }

    static bool InvariantsHold(GameSim sim)
    {
        for (int i = 0; i < 2; i++)
        {
            Fighter f = sim.fighters[i];
            if (f.hp < 0 || f.hp > f.MaxHp) return false;
            if (f.meter < 0 || f.meter > Fighter.MeterMax) return false;
            if (f.x < -GameSim.ArenaHalf || f.x > GameSim.ArenaHalf) return false;
            if (f.state == FighterStateId.Attack && f.move == null) return false;
            if (f.y < 0) return false;
            // Only these states can be in the air (Idle, Walk, Dash, Blockstun are ground-only).
            if (f.IsAirborne && f.state != FighterStateId.Jump && f.state != FighterStateId.Attack
                && f.state != FighterStateId.Hitstun && f.state != FighterStateId.KO) return false;
            if (sim.roundsWon[i] > GameSim.RoundsToWin) return false;
        }
        return sim.freezeTicks >= 0 && sim.freezeTicks <= GameSim.SuperFlashTicks;
    }

    static void AssertSaw(HashSet<SimEventType> seen, params SimEventType[] wanted)
    {
        for (int i = 0; i < wanted.Length; i++)
        {
            Assert.IsTrue(seen.Contains(wanted[i]), "the random matches never produced a " + wanted[i]
                          + " event, so this test isn't exercising it any more");
        }
    }

    [Test]
    public void EveryPairIsDeterministic()
    {
        List<int> ids = CodeCharacterIds();
        Assert.GreaterOrEqual(ids.Count, 3, "the test characters should be registered");
        HashSet<SimEventType> seen = new HashSet<SimEventType>();

        for (int i = 0; i < ids.Count; i++)
        {
            for (int j = 0; j < ids.Count; j++)
            {
                int seed = 1 + i * 101 + j;
                List<int> first = PlayRandom(ids[i], ids[j], seed, seen);
                List<int> second = PlayRandom(ids[i], ids[j], seed, seen);
                Assert.AreEqual(first.Count, second.Count);
                for (int t = 0; t < first.Count; t++)
                {
                    if (first[t] != second[t])
                    {
                        Assert.Fail(CharacterList.NameOf(ids[i]) + " vs " + CharacterList.NameOf(ids[j])
                                    + ": the same inputs gave different states on tick " + t);
                    }
                }
            }
        }

        AssertSaw(seen, SimEventType.Hit, SimEventType.Block, SimEventType.Grab, SimEventType.Dash,
                  SimEventType.SuperFlash, SimEventType.KO, SimEventType.RoundEnd);
    }

    // An aggressive bot: walk in, jab when close. Against a random opponent the match must reach
    // a winner, so rounds really do end and the next one really starts.
    [Test]
    public void EveryPairCanFinishAMatch()
    {
        List<int> ids = CodeCharacterIds();
        for (int i = 0; i < ids.Count; i++)
        {
            for (int j = 0; j < ids.Count; j++)
            {
                if (ids[i] == Sandbag.Id || ids[j] == Sandbag.Id) continue;   // Sandbag can't attack or be KO'd

                GameSim sim = NewMatch(ids[i], ids[j]);
                RandomPlayer opponent = new RandomPlayer(1000 + i * 101 + j, 1);
                bool sawRoundStart = false;
                int t = 0;
                for (; t < FinishWithinTicks && !sim.IsOver; t++)
                {
                    Fighter me = sim.fighters[0];
                    Fighter them = sim.fighters[1];
                    int toward = them.x > me.x ? Buttons.Right : Buttons.Left;
                    int input = Math.Abs(them.x - me.x) > 1400 ? toward : Buttons.Light;
                    sim.Step(input, opponent.Next(sim));
                    for (int e = 0; e < sim.events.Count; e++)
                    {
                        if (sim.events[e].type == SimEventType.RoundStart) sawRoundStart = true;
                    }
                }
                string who = CharacterList.NameOf(ids[i]) + " vs " + CharacterList.NameOf(ids[j]);
                Assert.IsTrue(sim.IsOver, who + " didn't finish in " + FinishWithinTicks + " ticks");
                Assert.AreNotEqual(-1, sim.Winner);
                Assert.IsTrue(sawRoundStart, who + " ended without ever starting round 2");
                Assert.Greater(t, 2 * GameSim.RoundEndDelay, who + " ended too fast to be a real best of 3");
            }
        }
    }
}
