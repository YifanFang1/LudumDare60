// RUY: roster character, id 10. Shoto. Design: Design/Characters/Ruy.md
// (The class is still called Samurai: main renamed ArcadeGuy to Samurai, and D-40 keeps class
// names to avoid churn. The in-game name is "Ruy".)
//
// The all-rounder who gets stronger as the round goes badly.
//   Light, Heavy, Grab, dashes   standard
//   Special (I)    Hadoken: hold I to charge, let go to throw. 10 damage tapped, up to 20 after
//                  60 ticks of holding (it throws by itself at full charge). One on screen.
//   Ultimate (O)   Shoryuken: a rising uppercut, invincible on its first 8 ticks, 30 damage.
//                  On hit it launches the victim high into the air. It keeps playing after it
//                  lands, so it's very punishable if blocked.
//   Perk 1         Rage: all his hits do +missingHp%/2 damage (up to about +50%).
//   Perk 2         The Hadoken's charge (above).
public class Samurai : BaseFighter
{
    public const int Id = 10;

    public const int HadokenFullCharge = 60;   // ticks of holding for the full +10 damage
    public const int HadokenBonusDamage = 10;
    public const int ShoryukenInvincibleTicks = 8;   // ticks 0-7 of the move
    // The launch: a Shoryuken that connects (not blocked) throws the victim high into the air.
    // Peak height is about up * up / (2 * Gravity): 280 -> about 3.6 units, above Ruy's own 2.4.
    public const int ShoryukenLaunchUp = 280;        // milli-units per tick, upward
    public const int ShoryukenLaunchSideways = 40;   // milli-units per tick, away from Ruy (the normal knockback is 120)

    public static readonly MoveDef Hadoken = MakeHadoken();
    public static readonly MoveDef Shoryuken = MakeShoryuken();

    static MoveDef MakeHadoken()
    {
        MoveDef m = Projectile("Special",
            totalTicks: 30, spawnTick: 12, lifetime: 150,
            box: new Box(400, 900, 500, 400),
            speed: 100, damage: 10, hitstun: 18, knockback: 60,
            visual: "special", cooldown: SpecialCooldown);
        m.holdButton = Buttons.Special;
        m.chargeTick = 0;                 // he holds the wind-up pose while charging
        m.chargeMax = HadokenFullCharge;
        return m;
    }

    static MoveDef MakeShoryuken()
    {
        MoveDef m = Ultimate("Ultimate",
            totalTicks: 52, spawnTick: 2, activeTicks: 12,
            box: new Box(100, 0, 900, 2400),
            damage: 30, hitstun: 40, knockback: 120);
        m.keepOnLanding = true;           // lands around tick 42, then 10 more helpless ticks
        return m;
    }

    protected override MoveDef SpecialMove { get { return Hadoken; } }
    protected override MoveDef UltimateMove { get { return Shoryuken; } }

    protected override bool CanUseMove(GameSim sim, MoveDef m)
    {
        if (m == Hadoken) return ProjectilesOnScreen(sim) == 0;
        return base.CanUseMove(sim, m);
    }

    // The charge is stamped on the fireball when it's thrown, so it keeps its damage in flight.
    public override void OnHitboxSpawned(GameSim sim, HitboxInstance hit)
    {
        if (hit.def == Hadoken.hitboxes[0])
        {
            hit.damage = hit.def.damage + HadokenBonusDamage * chargeTicks / HadokenFullCharge;
        }
    }

    // The jump of the uppercut. Ticks count from the end of the superflash.
    public override void OnMoveTick(GameSim sim)
    {
        if (move == Shoryuken && stateTick == 0)
        {
            vy = 230;              // peaks about 2.4 units up
            vx = facing * 20;      // a little forward drift
        }
    }

    // The launch. Runs after TakeHit, so it replaces the normal knockback. The victim stays in
    // hitstun (or KO) until they land, so a launched fighter can't act until they're back down.
    public override void OnHitDealt(GameSim sim, Fighter victim, HitboxInstance hit)
    {
        if (hit.blocked || hit.def != Shoryuken.hitboxes[0]) return;
        int away = x <= victim.x ? 1 : -1;
        victim.vy = ShoryukenLaunchUp;
        victim.vx = away * ShoryukenLaunchSideways;
    }

    public override bool IsIntangible
    {
        get { return IsDoing(Shoryuken) && stateTick < ShoryukenInvincibleTicks; }
    }

    // Rage (perk 1): +25% at half health, about +50% near 0.
    public override void ModifyOutgoingHit(GameSim sim, Fighter victim, HitboxInstance hit)
    {
        hit.damage = hit.damage * (100 + MissingHpPercent / 2) / 100;
    }
}
