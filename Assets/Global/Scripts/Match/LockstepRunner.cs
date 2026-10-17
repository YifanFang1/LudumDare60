using System;

// Runs the simulation at a fixed 60 ticks per second.
// A tick only happens when BOTH players' inputs for that tick are known.
// If the other player's input has not arrived yet, we wait (Stalled = true).
//
// Input delay: when the sim is on tick T we have already sampled the local keyboard for
// tick T + delay and sent it. So by the time the sim reaches a tick, the other player's
// input for it has usually already arrived.
public class LockstepRunner
{
    public const float TickTime = 1f / 60f;

    GameSim sim;
    IInputSource p0;
    IInputSource p1;
    int delay;
    int sampledUpTo;
    float acc;

    public bool Running;
    public bool Stalled;
    public Action<int> OnTick;   // called after every tick with the new tick number

    public void Begin(GameSim sim, IInputSource p0, IInputSource p1, int delay)
    {
        this.sim = sim;
        this.p0 = p0;
        this.p1 = p1;
        this.delay = delay;
        sampledUpTo = -1;
        acc = 0f;
        Running = true;
        Stalled = false;
    }

    // Call this every frame with Time.deltaTime.
    public void Update(float dt)
    {
        if (!Running) return;

        acc += dt;
        if (acc > 0.25f) acc = 0.25f;   // never try to catch up more than a few ticks

        while (acc >= TickTime)
        {
            if (!TryStep())
            {
                Stalled = true;
                acc = Math.Min(acc, TickTime);
                return;
            }
            Stalled = false;
            acc -= TickTime;

            if (sim.IsOver)
            {
                Running = false;
                return;
            }
        }
    }

    bool TryStep()
    {
        int tick = sim.tick;

        // Make sure inputs up to tick + delay have been sampled (and sent, for the local player).
        while (sampledUpTo < tick + delay)
        {
            sampledUpTo++;
            p0.Sample(sampledUpTo);
            p1.Sample(sampledUpTo);
        }

        int in0 = 0;
        int in1 = 0;
        if (!p0.TryGet(tick, out in0)) return false;
        if (!p1.TryGet(tick, out in1)) return false;

        sim.Step(in0, in1);
        if (OnTick != null) OnTick(sim.tick);
        return true;
    }
}
