using System;
using System.Collections.Generic;

// A very dumb opponent so the game is playable offline. Replace with something smarter later.
// It only runs on one machine, so it is allowed to read the sim and use System.Random.
public class AIInputSource : IInputSource
{
    GameSim sim;
    int me;
    Random rng = new Random(1);
    Dictionary<int, int> inputs = new Dictionary<int, int>();

    public AIInputSource(GameSim sim, int myIndex)
    {
        this.sim = sim;
        this.me = myIndex;
    }

    public void Sample(int tick)
    {
        int bits = 0;
        int distance = sim.fighters[1 - me].x - sim.fighters[me].x;

        if (Math.Abs(distance) > 1300)
        {
            bits |= distance > 0 ? Buttons.Right : Buttons.Left;   // walk toward the player
        }
        else
        {
            int roll = rng.Next(0, 40);
            if (roll < 2) bits |= Buttons.Light;                    // sometimes attack
            else if (roll == 2) bits |= Buttons.Heavy;
        }

        inputs[tick] = bits;
    }

    public bool TryGet(int tick, out int bits)
    {
        return inputs.TryGetValue(tick, out bits);
    }
}
