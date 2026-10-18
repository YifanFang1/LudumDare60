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
    Telegraph,    // a warning before an attack lands: a: owner, b: ticks until it lands, x: centre, amount: width
    Reflect,      // a projectile was sent back: a: the reflector (new owner), b: the old owner, x/y: where
    ProjectileDestroyed, // a: who destroyed it, b: the projectile's owner, x/y: where
    StatusDamage, // damage that wasn't a hit (bleed, poison, leeches, a cost): a: who lost HP, amount: HP lost
    Heal,         // a: who healed, amount: HP gained
    SummonSpawn,  // a: the owner, x: where the copy appeared
    SummonDestroyed, // a: the owner, b: who destroyed it (-1 = it ran out), x: where
    Hold,         // a held grab caught someone: a: grabber, b: victim, amount: hold ticks
    OutOfAmmo,    // a fresh Special press with no ammo left (Noir's empty click): a: who, x/y: where
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
