// SHOOTER: a long-range test character (a "zoner"). Shows how to add a projectile and a rule
// on top of BaseFighter.
// Tall, thin and slow, with less health. Wants to keep you away with fireballs.
//
//   Light (J)  Poke       weak, short; so the Shooter isn't helpless up close
//   Heavy (K)  Fireball   slow projectile; only ONE can be on screen at a time
//
// Id 1.
public class Shooter : BaseFighter
{
    public const int Id = 1;

    static readonly MoveDef Poke = Melee("Light",
        totalTicks: 13, spawnTick: 3, activeTicks: 3,
        box: new Box(300, 1100, 700, 300),
        damage: 4, hitstun: 11, knockback: 30);

    // Crosses the arena (16 units) in about 3 seconds. The Shooter recovers after 30 ticks, a lot
    // earlier than the fireball lands, so it can follow up or walk behind it.
    static readonly MoveDef Fireball = Projectile("Heavy",
        totalTicks: 30, spawnTick: 12, lifetime: 180,
        box: new Box(400, 900, 500, 400),
        speed: 90, damage: 10, hitstun: 18, knockback: 60);

    public override int MaxHp { get { return 90; } }
    public override int WalkSpeed { get { return 50; } }
    public override Box Hurtbox { get { return new Box(-400, 0, 800, 2100); } }

    protected override MoveDef LightAttack { get { return Poke; } }
    protected override MoveDef HeavyAttack { get { return Fireball; } }

    // No fireball spam: wait until the last one hits something or flies off.
    protected override bool CanUseMove(GameSim sim, MoveDef move)
    {
        if (move == Fireball) return ProjectilesOnScreen(sim) == 0;
        return true;
    }
}
