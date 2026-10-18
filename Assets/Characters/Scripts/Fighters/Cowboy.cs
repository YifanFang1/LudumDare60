// NOIR: roster character, id 12. Zoner. Design: Design/Characters/Noir.md
// (The class is still called Cowboy, D-40. The in-game name is "Noir".)
//
// A fragile gunslinger who keeps opponents out. 80 HP, slow walk (45).
//   Light (J)      Fire: an instant 4-unit shot (a long melee box, not a projectile). 3 damage.
//   Heavy (K)      Throw gun: a slow, short projectile (he keeps his gun).
//   Grab (L)       Lasso: grabs from about 2.5 units away and pulls them in.
//   Dashes         Horse: long and fast, about 3.6 units each way.
//   Special (I)    Bullet: fast, full screen, big knockback. Only 6 per round (perk 1).
//   Ultimate (O)   Rapid Fire: for 5 s, unlimited bullets and almost no Special cooldown.
//   Perk 1         Six-Shooter: 6 bullets per round, no reload.
//   Perk 2         Point Blank: all his hits knock back up to x2 harder the closer the victim
//                  (x1 at 4+ units, x2 when touching).
public class Cowboy : BaseFighter
{
    public const int Id = 12;

    public const int BulletsPerRound = 6;
    public const int RapidFireTicks = 300;
    public const int RapidFireCooldown = 4;
    public const int PointBlankRange = 4000;

    public static readonly MoveDef Fire = Melee("Light",
        totalTicks: 20, spawnTick: 6, activeTicks: 2,
        box: new Box(400, 1000, 4000, 200),
        damage: 3, hitstun: 8, knockback: 30, cooldown: LightCooldown);

    public static readonly MoveDef ThrowGun = Projectile("Heavy",
        totalTicks: 30, spawnTick: 12, lifetime: 50,
        box: new Box(300, 800, 500, 400),
        speed: 70, damage: 10, hitstun: 20, knockback: 70,
        visual: "gun", cooldown: HeavyCooldown);

    public static readonly MoveDef Lasso = MakeLasso();

    public static readonly MoveDef Bullet = Projectile("Special",
        totalTicks: 26, spawnTick: 10, lifetime: 80,
        box: new Box(400, 1000, 300, 200),
        speed: 200, damage: 8, hitstun: 18, knockback: 150,
        visual: "bullet", cooldown: SpecialCooldown);

    public static readonly MoveDef RapidFire = MakeRapidFire();

    static MoveDef MakeLasso()
    {
        MoveDef m = Grab("Grab",
            totalTicks: 48, spawnTick: 10, activeTicks: 3,
            box: new Box(300, 600, 2200, 1000),
            damage: 12, hitstun: 40, knockback: 90, cooldown: GrabCooldown);
        m.hitboxes[0].pullTo = 900;   // drags them in front of him before the throw
        return m;
    }

    // A 20-tick twirl with no hitbox; the buff starts when the superflash ends.
    static MoveDef MakeRapidFire()
    {
        MoveDef m = Pose("Ultimate", totalTicks: 20);
        m.ultimate = true;
        return m;
    }

    public int ammo = BulletsPerRound;   // a fresh fighter each round (D-19), so this resets
    public int rapidFireTicks;

    public override int MaxHp { get { return 80; } }
    public override int WalkSpeed { get { return 45; } }

    protected override MoveDef LightAttack { get { return Fire; } }
    protected override MoveDef HeavyAttack { get { return ThrowGun; } }
    protected override MoveDef GrabMove { get { return Lasso; } }
    protected override MoveDef SpecialMove { get { return Bullet; } }
    protected override MoveDef UltimateMove { get { return RapidFire; } }

    public override DashDef ForwardDash { get { return new DashDef(20, 180); } }
    public override DashDef BackDash { get { return new DashDef(22, 165); } }

    // No on-screen limit for bullets: ammo and the cooldown are the limit.
    protected override bool CanUseMove(GameSim sim, MoveDef m)
    {
        if (m == Bullet) return ammo > 0 || rapidFireTicks > 0;
        return base.CanUseMove(sim, m);
    }

    // Standard controls, plus the empty click: a FRESH Special press with no bullets left.
    // (The event only feeds sound; it never changes the fight.)
    public override MoveDef ChooseMove(GameSim sim, int input)
    {
        if ((pressedInput & Buttons.Special) != 0 && ammo <= 0 && rapidFireTicks <= 0)
            sim.AddEvent(SimEventType.OutOfAmmo, index, -1, x, y, 0);
        return base.ChooseMove(sim, input);
    }

    public override void OnTick(GameSim sim)
    {
        if (rapidFireTicks > 0) rapidFireTicks--;
    }

    public override void OnMoveTick(GameSim sim)
    {
        if (move == RapidFire && stateTick == 0) rapidFireTicks = RapidFireTicks;
    }

    public override void OnHitboxSpawned(GameSim sim, HitboxInstance hit)
    {
        if (hit.def == Bullet.hitboxes[0] && rapidFireTicks == 0) ammo--;
    }

    public override int ModifyCooldown(MoveDef ended, int ticks)
    {
        if (ended == Bullet && rapidFireTicks > 0) return RapidFireCooldown;
        return ticks;
    }

    // Point Blank (perk 2), worked out when the hit lands.
    public override void ModifyOutgoingHit(GameSim sim, Fighter victim, HitboxInstance hit)
    {
        int dist = System.Math.Abs(x - victim.x);
        if (dist < PointBlankRange)
        {
            hit.knockback = hit.knockback * (100 + 100 * (PointBlankRange - dist) / PointBlankRange) / 100;
        }
    }

    public override string HudInfo
    {
        get { return rapidFireTicks > 0 ? "RAPID FIRE" : "Ammo " + ammo + "/" + BulletsPerRound; }
    }

    public override bool IsPoweredUp { get { return rapidFireTicks > 0; } }

    public override int HashExtra() { return ammo * 1000 + rapidFireTicks; }
}
