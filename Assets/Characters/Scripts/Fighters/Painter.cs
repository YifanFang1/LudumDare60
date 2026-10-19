// PAINTRESS: roster character, id 11. Rushdown. Design: Design/Characters/Paintress.md
// (The class is still called Painter, D-40. The in-game name is "Paintress".)
//
// A fragile speedster who never stops coming forward. 75 HP, walk 90.
//   Light (J)      One-armed jab.
//   Heavy (K)      Upward leg sweep: a rising kick with a tall box that also catches jumpers.
//   Grab (L)       Short grab: less reach, 1 tick faster.
//   Dashes         very fast. Forward: invincible. Back: slows HER to half speed for 2 s. Dash cooldown 0.5 s.
//   Special (I)    Reflective Canvas: for 30 ticks, enemy projectiles that touch it fly back as
//                  hers. It doesn't hit or stop melee. Ultimate projectiles go through.
//   Ultimate (O)   Paint Splat: a fast projectile that can't be blocked but can be jumped. On hit:
//                  3 s of scrambled controls (and ink blots on the victim's screen, view only).
//   Perk 1         Fearless Rush: the dashes above.
//   Perk 2         Paint Skating: every hit she lands or gets blocked leaves a paint puddle at the
//                  victim's feet (max 3, 4 s each). On paint she's +15% faster, the opponent -15%.
public class Painter : BaseFighter
{
    public const int Id = 11;

    public const int BackDashSlowPercent = 50;   // the back dash slows Paintress herself by this much...
    public const int BackDashSlowTicks = 120;    // ...for 2 s
    public const int ScrambleTicks = 180;
    public const int PaintTag = 1;
    public const int MaxPuddles = 3;
    public const int PuddleTicks = 240;
    public const int PuddleHalfWidth = 750;
    public const int PaintSpeedPercent = 15;

    public static readonly MoveDef Jab = Melee("Light",
        totalTicks: 14, spawnTick: 4, activeTicks: 3,
        box: new Box(400, 900, 900, 400),
        damage: 6, hitstun: 12, knockback: 40, cooldown: LightCooldown);

    public static readonly MoveDef LegSweep = Melee("Heavy",
        totalTicks: 26, spawnTick: 9, activeTicks: 4,
        box: new Box(300, 300, 1100, 1800),
        damage: 13, hitstun: 20, knockback: 70, cooldown: HeavyCooldown);

    public static readonly MoveDef ShortGrab = HoldGrab(Grab("Grab",
        totalTicks: 38, spawnTick: 4, activeTicks: 2,
        box: new Box(300, 600, 500, 1000),
        damage: 12, hitstun: 40, knockback: 90, cooldown: GrabCooldown));

    public static readonly MoveDef Canvas = MakeCanvas();
    public static readonly MoveDef PaintSplat = MakePaintSplat();

    static MoveDef MakeCanvas()
    {
        MoveDef m = Melee("Special",
            totalTicks: 44, spawnTick: 4, activeTicks: 30,
            box: new Box(300, 0, 600, 2200),
            damage: 0, hitstun: 0, knockback: 0, cooldown: SpecialCooldown);
        m.hitboxes[0].ignoresFighters = true;
        m.hitboxes[0].reflectsProjectiles = true;
        m.hitboxes[0].visual = "canvas";
        return m;
    }

    // Low enough (0.2 to 0.7 units up) that a jump clears it.
    static MoveDef MakePaintSplat()
    {
        MoveDef m = Projectile("Ultimate",
            totalTicks: 36, spawnTick: 8, lifetime: 120,
            box: new Box(400, 200, 500, 500),
            speed: 180, damage: 6, hitstun: 12, knockback: 20,
            visual: "paint_splat");
        m.ultimate = true;
        m.hitboxes[0].unblockable = true;
        return m;
    }

    public override int MaxHp { get { return 75; } }
    public override int WalkSpeed { get { return 90; } }

    protected override MoveDef LightAttack { get { return Jab; } }
    protected override MoveDef HeavyAttack { get { return LegSweep; } }
    protected override MoveDef GrabMove { get { return ShortGrab; } }
    protected override MoveDef SpecialMove { get { return Canvas; } }
    protected override MoveDef UltimateMove { get { return PaintSplat; } }

    public override int DashCooldown { get { return 30; } }   // 0.5 s between dashes
    public override DashDef ForwardDash { get { return new DashDef(10, 200); } }
    public override DashDef BackDash { get { return new DashDef(10, 220); } }

    // Fearless Rush (perk 1). Bodies still bump: she can't dash through the opponent.
    public override bool IsIntangible { get { return IsDashingForward; } }

    public override void OnDashStart(GameSim sim, bool forward)
    {
        if (!forward) ApplySlow(BackDashSlowPercent, BackDashSlowTicks);
    }

    public override void OnHitDealt(GameSim sim, Fighter victim, HitboxInstance hit)
    {
        if (hit.def == PaintSplat.hitboxes[0] && !hit.blocked) victim.ApplyScramble(sim, ScrambleTicks);

        // Paint Skating (perk 2): landed or blocked.
        if (sim.CountZones(index, PaintTag) >= MaxPuddles) sim.RemoveOldestZone(index, PaintTag);
        Zone puddle = new Zone();
        puddle.owner = index;
        puddle.tag = PaintTag;
        puddle.x = victim.x;
        puddle.halfWidth = PuddleHalfWidth;
        puddle.ticksLeft = PuddleTicks;
        puddle.ownerSpeedPercent = PaintSpeedPercent;
        puddle.enemySlowPercent = PaintSpeedPercent;
        puddle.visual = "paint";
        sim.AddZone(puddle);
    }
}
