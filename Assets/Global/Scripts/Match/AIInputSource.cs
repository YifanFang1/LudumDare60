using System;
using System.Collections.Generic;

// A simple opponent so the game is playable offline. It knows nothing about specific characters:
// it uses the shared kit (Design/SHARED_KIT.md) the same way for everyone.
//
//   far away     walk in, sometimes dash in, sometimes throw the Special
//   close        jab, heavy or grab (grabs more often when you're blocking), sometimes back dash
//   you attack   about half the time it holds back to block until your move ends
//   full meter   fires the Ultimate when you're in range
//
// It only runs on one machine, so it is allowed to read the sim and use System.Random.
// It only ever outputs button bits, exactly like a keyboard, so it can't cheat the sim.
public class AIInputSource : IInputSource
{
    const int CloseRange = 1500;    // centre to centre: Light and Grab reach about this far
    const int FarRange = 4000;
    const int UltimateRange = 2600;

    GameSim sim;
    int me;
    Random rng = new Random(1);
    Dictionary<int, int> inputs = new Dictionary<int, int>();
    Queue<int> planned = new Queue<int>();   // inputs already decided for the next ticks (dash taps)
    int blockUntilTick = -1;                 // keep holding back until this sim tick
    int lastOutput;

    public AIInputSource(GameSim sim, int myIndex)
    {
        this.sim = sim;
        this.me = myIndex;
    }

    public void Sample(int tick)
    {
        int bits = planned.Count > 0 ? planned.Dequeue() : Decide();
        inputs[tick] = bits;
        lastOutput = bits;
    }

    public bool TryGet(int tick, out int bits)
    {
        return inputs.TryGetValue(tick, out bits);
    }

    int Decide()
    {
        Fighter self = sim.fighters[me];
        Fighter foe = sim.fighters[1 - me];
        int distance = foe.x - self.x;
        int gap = Math.Abs(distance);
        int toward = distance > 0 ? Buttons.Right : Buttons.Left;
        int away = distance > 0 ? Buttons.Left : Buttons.Right;

        // Block: when the player starts a move, about half the time hold back until it's over.
        if (foe.state == FighterStateId.Attack && foe.stateTick == 0 && foe.move != null && rng.Next(2) == 0)
        {
            blockUntilTick = sim.tick + foe.move.totalTicks;
        }
        if (sim.tick < blockUntilTick) return away;

        // Ultimate: needs a full bar and a fresh press (so not if O was held last tick).
        if (self.meter >= Fighter.MeterMax && gap < UltimateRange && (lastOutput & Buttons.Ultimate) == 0 && rng.Next(10) == 0)
        {
            return Buttons.Ultimate;
        }

        if (gap > FarRange)
        {
            int roll = rng.Next(100);
            if (roll < 2) return Buttons.Special;
            if (roll < 3) return PlanDash(toward);
            return toward;
        }

        if (gap > CloseRange)
        {
            if (rng.Next(80) == 0) return Buttons.Special;
            return toward;   // walk in
        }

        // Close: mostly wait a beat between attacks, so the player can act too.
        int r = rng.Next(40);
        if (r < 2) return Buttons.Light;
        if (r == 2) return Buttons.Heavy;
        if (r == 3 || (foe.holdingBack && r < 6)) return Buttons.Grab;
        if (r == 4) return PlanDash(away);
        return 0;
    }

    // A dash is a double-tap: release, tap, release, tap. Each tap must be a FRESH press, so the
    // key is let go first in case it was being held to walk. Returns this tick's input; the rest
    // is queued for the next ticks.
    int PlanDash(int direction)
    {
        planned.Enqueue(direction);
        planned.Enqueue(0);
        planned.Enqueue(direction);
        return 0;
    }
}
