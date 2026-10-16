using System.Collections.Generic;

// Input that arrives from the other player over the network.
public class RemoteInputSource : IInputSource
{
    Dictionary<int, int> inputs = new Dictionary<int, int>();

    // Called by NetClient when "i,<tick>,<bits>" comes in.
    public void Receive(int tick, int bits)
    {
        inputs[tick] = bits;
    }

    public void Sample(int tick) { }   // nothing to do, we just wait for the network

    public bool TryGet(int tick, out int bits)
    {
        return inputs.TryGetValue(tick, out bits);
    }
}
