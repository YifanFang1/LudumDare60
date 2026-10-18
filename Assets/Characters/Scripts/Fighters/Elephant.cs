// ROOM: roster character, id 13. Grappler. Design: Design/Characters/Room.md
// (The class is still called Elephant, D-40. The in-game name is "Room": the elephant in the room.)
//
// A huge, slow tank who CAN'T BLOCK. 200 HP, walk 30, a bigger body (1.4 x 2.3 units).
// His boxes reach further forward than the standard ones because his body is wider.
//   Light (J)      Trunk whip: short reach, 9 damage.
//   Heavy (K)      Trunk slam: slow (18 ticks), big (18 damage).
//   Grab (L)       Trunk Grab: HOLDS the victim for 60 ticks. During the hold only Vine Boom (I)
//                  works, and it can't miss; without it the hold ends in a normal throw.
//   Dashes         slow and heavy, 60-tick cooldown, and a short air dash. A forward dash (ground
//                  or air) is a hit (perk 1).
//   Special (I)    Vine Boom: hits all around him and destroys enemy projectiles. Booms that land
//                  on the same tick (his and his copies') stack: x2, x4, capped at x4.
//   Ultimate (O)   Address Me: 4 copies at fixed spots copy his walking, Light, Heavy and Vine Boom
//                  for 8 s. Any enemy hit destroys a copy. Their hits give no meter.
//   Perk 1         Stampede: the forward dash hits (8 damage) and goes up to 2x further when hurt.
//   Perk 2         Unstoppable: ignores slows, scramble and bleed's damage penalty.
public class Elephant : BaseFighter
{
    public const int Id = 13;

    public const int HoldTicks = 60;
    public const int CopyTicks = 480;
    public const int MaxStackMultiplier = 4;
    public static readonly int[] CopySpots = { -6000, -2000, 2000, 6000 };
    public const int CopyClearance = 1000;   // no copy closer than this to either fighter

    public static readonly MoveDef TrunkWhip = Melee("Light",
        totalTicks: 20, spawnTick: 6, activeTicks: 3,
        box: new Box(500, 800, 800, 500),
        damage: 9, hitstun: 14, knockback: 50, cooldown: LightCooldown);

    public static readonly MoveDef TrunkSlam = Melee("Heavy",
        totalTicks: 40, spawnTick: 18, activeTicks: 4,
        box: new Box(600, 0, 1300, 1200),
        damage: 18, hitstun: 24, knockback: 90, cooldown: HeavyCooldown);

    public static readonly MoveDef TrunkGrab = MakeTrunkGrab();
    public static readonly MoveDef VineBoom = MakeVineBoom();
    public static readonly MoveDef AddressMe = MakeAddressMe();

    public static readonly HitboxDef StampedeHit = new HitboxDef
    {
        box = new Box(600, 300, 700, 1400),
        damage = 8, hitstun = 14, knockback = 60, followOwner = true,
    };

    static MoveDef MakeTrunkGrab()
    {
        MoveDef m = Grab("Grab",
            totalTicks: 40, spawnTick: 5, activeTicks: 2,
            box: new Box(500, 600, 800, 1000),
            damage: 12, hitstun: 40, knockback: 90, cooldown: GrabCooldown);
        m.hitboxes[0].holdTicks = HoldTicks;
        m.hitboxes[0].pullTo = 1200;   // just in front of his wide body
        return m;
    }

    static MoveDef MakeVineBoom()
    {
        MoveDef m = Melee("Special",
            totalTicks: 40, spawnTick: 14, activeTicks: 4,
            box: new Box(-1500, 0, 3000, 2000),
            damage: 8, hitstun: 20, knockback: 100, cooldown: SpecialCooldown);
        m.hitboxes[0].destroysProjectiles = true;
        m.hitboxes[0].stacks = true;
        return m;
    }

    static MoveDef MakeAddressMe()
    {
        MoveDef m = Pose("Ultimate", totalTicks: 20);
        m.ultimate = true;
        return m;
    }

    public override int MaxHp { get { return 200; } }
    public override int WalkSpeed { get { return 30; } }
    public override Box Hurtbox { get { return new Box(-700, 0, 1400, 2300); } }
    public override bool CanBlock { get { return false; } }

    protected override MoveDef LightAttack { get { return TrunkWhip; } }
    protected override MoveDef HeavyAttack { get { return TrunkSlam; } }
    protected override MoveDef GrabMove { get { return TrunkGrab; } }
    protected override MoveDef SpecialMove { get { return VineBoom; } }
    protected override MoveDef UltimateMove { get { return AddressMe; } }

    // Stampede (perk 1): longer the more he's hurt.
    public override DashDef ForwardDash { get { return new DashDef(24 * (100 + MissingHpPercent) / 100, 80); } }
    public override DashDef BackDash { get { return new DashDef(24, 70); } }
    public override int DashCooldown { get { return 60; } }
    // Heavy in the air too: a short, slow air dash (0.84 units; the standard is 1.3).
    public override DashDef AirDash { get { return new DashDef(12, 70); } }
    public override HitboxDef DashHitbox(bool forward) { return forward ? StampedeHit : null; }

    public override MoveDef ChooseHoldMove(GameSim sim, int input)
    {
        if ((input & Buttons.Special) != 0 && IsUsable(sim, VineBoom)) return VineBoom;
        return null;
    }

    public override int StackMultiplier(int count)
    {
        return System.Math.Min(1 << (count - 1), MaxStackMultiplier);
    }

    public override void OnMoveTick(GameSim sim)
    {
        if (move != AddressMe || stateTick != 0) return;
        for (int i = 0; i < CopySpots.Length; i++)
        {
            int spot = CopySpots[i];
            bool tooClose = System.Math.Abs(spot - sim.fighters[0].x) < CopyClearance
                         || System.Math.Abs(spot - sim.fighters[1].x) < CopyClearance;
            if (!tooClose) sim.SpawnSummon(index, spot, CopyTicks);
        }
    }

    public override bool SummonCopiesMove(MoveDef m)
    {
        return m == TrunkWhip || m == TrunkSlam || m == VineBoom;
    }

    // Unstoppable (perk 2).
    public override bool IgnoresStatus(StatusKind kind)
    {
        return kind == StatusKind.Slow || kind == StatusKind.Scramble || kind == StatusKind.BleedWeakness;
    }
}
