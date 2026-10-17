// Things that happened during a tick, for the CLIENT to react to (screen shake, sparks, sounds).
// The sim only writes these; it never reads them back, so they cannot cause desyncs.
public enum SimEventType { AttackStart, Hit, KO, Block }

public struct SimEvent
{
    public SimEventType type;
    public int a;        // main fighter: the attacker for Hit/Block, the one who KO'd for KO, else the actor
    public int b;        // other fighter (the victim for Hit, the blocker for Block), or -1
    public int x;        // world position in milli-units
    public int y;
    public int amount;   // damage for Hit
}
