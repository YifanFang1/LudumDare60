// BRAWLER: a close-range test character, and the simplest example of BaseFighter.
// Tough and stocky, walks in fast and hits hard up close.
//
//   Light (J)  Jab        fast, short, safe
//   Heavy (K)  Haymaker   slow start, long reach, big damage and knockback
//   Grab, Special, Ultimate and dashes: the template's standard ones (see BaseFighter).
//
// Id 0. It is also the fallback when a character id is unknown, so it must always exist.
// To make your own character, copy this file (or Shooter.cs for a ranged one) and follow
// Characters/CHARACTERS.md.
public class Brawler : BaseFighter
{
    public const int Id = 0;

    static readonly MoveDef Jab = Melee("Light",
        totalTicks: 15, spawnTick: 4, activeTicks: 3,
        box: new Box(400, 900, 900, 400),
        damage: 6, hitstun: 14, knockback: 40);

    static readonly MoveDef Haymaker = Melee("Heavy",
        totalTicks: 28, spawnTick: 10, activeTicks: 4,
        box: new Box(400, 700, 1300, 700),
        damage: 14, hitstun: 22, knockback: 90);

    // A bit more health and speed than the template, and a wider, shorter body.
    public override int MaxHp { get { return 110; } }
    public override int WalkSpeed { get { return 70; } }
    public override Box Hurtbox { get { return new Box(-600, 0, 1200, 1900); } }

    protected override MoveDef LightAttack { get { return Jab; } }
    protected override MoveDef HeavyAttack { get { return Haymaker; } }
}
