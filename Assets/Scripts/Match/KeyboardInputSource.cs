using System;
using System.Collections.Generic;

// The local player's input for each tick. What the keys are is decided by FightControls
// (new Input System, rebindable); this class only records one sample per tick.
public class KeyboardInputSource : IInputSource
{
    Dictionary<int, int> inputs = new Dictionary<int, int>();

    // Set this to send each sampled input to the other player.
    public Action<int, int> OnSampled;

    public void Sample(int tick)
    {
        int bits = FightControls.Read();
        inputs[tick] = bits;
        if (OnSampled != null) OnSampled(tick, bits);
    }

    public bool TryGet(int tick, out int bits)
    {
        return inputs.TryGetValue(tick, out bits);
    }
}
