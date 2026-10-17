// Things that happened during a tick, for the CLIENT to react to (screen shake, sparks, sounds).
// The sim only writes these; it never reads them back, so they cannot cause desyncs.
// New types go at the END so the old numbers stay the same.
public enum SimEventType
{
    AttackStart,
    Hit,
    KO,
    Block,
    Grab,         // a grab connected (a: grabber, b: victim, amount: damage). Sent INSTEAD of Hit.
    Dash,         // a: the dasher, amount: 1 = forward dash, 0 = back dash
    SuperFlash,   // an Ultimate started; the sim freezes for GameSim.SuperFlashTicks (a: who)
    RoundStart,   // amount: the round number (1, 2, 3). Not sent for round 1 (the match start is that)
    RoundEnd,     // a: round winner (0, 1, or 2 = double KO), amount: the round number
}

public struct SimEvent
{
    public SimEventType type;
    public int a;        // main fighter: the attacker for Hit/Block/Grab, the one who KO'd for KO, else the actor
    public int b;        // other fighter (the victim for Hit/Grab, the blocker for Block), or -1
    public int x;        // world position in milli-units
    public int y;
    public int amount;   // damage for Hit/Grab, see the enum for the others
}
