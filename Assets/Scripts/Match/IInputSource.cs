// Where a fighter's input comes from: keyboard, the other player over the network, or an AI.
// The simulation never knows which one it is talking to.
public interface IInputSource
{
    // Called once per tick number, in order. `tick` can be a few ticks AHEAD of the sim
    // (that is the input delay). Local sources read the keyboard here and remember it.
    void Sample(int tick);

    // Returns false if the input for that tick has not arrived yet (the sim then waits).
    bool TryGet(int tick, out int bits);
}
