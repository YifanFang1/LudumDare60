// GROUND ZONES: an area of the floor that does something to whoever stands in it, for a while.
// Paintress's paint puddles (faster for her, slower for the opponent) and Dr. Remy's poison cloud
// (damage over time) are zones. They live in GameSim.zones, are part of the desync hash, and never
// move. Characters add them with GameSim.AddZone and the kit applies their effects every tick.
//
// Plain data, ints only. `visual` is cosmetic (the view draws the zone by it) and never affects
// the fight; `tag` lets the owner count and replace its own zones (e.g. "max 3 puddles").
public class Zone
{
    public int owner;          // fighter index (0 or 1)
    public int tag;            // owner-defined kind, for GameSim.CountZones / RemoveOldestZone
    public int x;              // centre, milli-units
    public int halfWidth;      // it covers x - halfWidth .. x + halfWidth, on the ground
    public int ticksLeft;
    public int age;

    public int ownerSpeedPercent;   // + this much walk/dash speed for the owner standing in it
    public int enemySlowPercent;    // - this much walk/dash speed for the opponent (a Slow status)
    public int enemyDamage;         // damage to the opponent every damageInterval ticks (can't KO)
    public int damageInterval;

    public string visual;           // COSMETIC

    // True if `f` is standing (on the ground) inside it.
    public bool Covers(Fighter f)
    {
        return !f.IsAirborne && f.x >= x - halfWidth && f.x <= x + halfWidth;
    }
}
