// THE EXECUTIONER: roster character, id 14. Trapper. Design: Design/Characters/Executioner.md
//
// A slow, tough torturer who wins by attrition. 150 HP, walk 42.
//   Light (J)      Whip: 1 unit of reach. Adds a bleed stack on hit.
//   Heavy (K)      Charged whip: hold K to charge, let go to lash. Charging adds REACH
//                  (1.8 -> 2.8 units over 60 ticks), not damage. Adds a bleed stack.
//   Grab (L)       Iron Maiden: a whip grab from about 2.5 units that pulls them in. 15 damage,
//                  adds a bleed stack.
//   Dashes         slow and heavy, 60-tick cooldown, and a short air dash.
//   Special (I)    Bear Trap: dropped at his feet, max 2 (a 3rd replaces the oldest). It arms after
//                  20 ticks, waits until stepped on (jumping avoids it), and can't be blocked.
//                  3 damage, 10 hitstun, a 3-second slow and a bleed stack.
//   Ultimate (O)   Guillotine: a warning outline on the opponent's spot, then 30 ticks later an
//                  unblockable blade drops there for 40 damage. Move out of the outline.
//   Perk 1         Bleed: stacks to 5, 1 damage per stack per second (can't KO), one stack fades
//                  every 2 s without a new one. At 5 stacks the victim's hits do 25% less.
//   Perk 2         Dismember: every 3rd landed hit slows the victim for 3 s.
public class Executioner : BaseFighter
{
    public const int Id = 14;

    public const int MaxTraps = 2;
    public const int SlowPercent = 40;
    public const int SlowTicks = 180;
    public const int DismemberEvery = 3;
    public const int WhipFullCharge = 60;
    public const int WhipExtraReach = 1000;
    public const int GuillotineDelay = 30;
    public const int GuillotineWidth = 1200;

    public static readonly MoveDef Whip = Melee("Light",
        totalTicks: 17, spawnTick: 5, activeTicks: 3,
        box: new Box(400, 900, 1000, 300),
        damage: 5, hitstun: 12, knockback: 40, cooldown: LightCooldown);

    public static readonly MoveDef ChargedWhip = MakeChargedWhip();
    public static readonly MoveDef IronMaiden = MakeIronMaiden();
    public static readonly MoveDef BearTrap = Pose("Special", totalTicks: 30, cooldown: SpecialCooldown);
    public static readonly MoveDef Guillotine = MakeGuillotine();

    public static readonly HitboxDef TrapHit = new HitboxDef
    {
        lifetime = 1, box = new Box(-400, 0, 800, 200),
        damage = 3, hitstun = 10, knockback = 0,
        followOwner = false, vx = 0, unblockable = true,
        untilHit = true, groundOnly = true, armTicks = 20, visual = "trap",
    };

    public static readonly HitboxDef BladeHit = new HitboxDef
    {
        lifetime = 4, box = new Box(-600, 0, GuillotineWidth, 2400),
        damage = 40, hitstun = 40, knockback = 0,
        followOwner = false, vx = 0, unblockable = true, visual = "guillotine",
    };

    static MoveDef MakeChargedWhip()
    {
        MoveDef m = Melee("Heavy",
            totalTicks: 32, spawnTick: 12, activeTicks: 4,
            box: new Box(400, 900, 1800, 300),
            damage: 10, hitstun: 20, knockback: 70, cooldown: HeavyCooldown);
        m.holdButton = Buttons.Heavy;
        m.chargeTick = 0;
        m.chargeMax = WhipFullCharge;
        return m;
    }

    static MoveDef MakeIronMaiden()
    {
        MoveDef m = Grab("Grab",
            totalTicks: 48, spawnTick: 10, activeTicks: 3,
            box: new Box(300, 600, 2200, 1000),
            damage: 15, hitstun: 40, knockback: 60, cooldown: GrabCooldown);
        m.hitboxes[0].pullTo = 900;
        return m;
    }

    static MoveDef MakeGuillotine()
    {
        MoveDef m = Pose("Ultimate", totalTicks: 45);
        m.ultimate = true;
        return m;
    }

    public int hitCounter;     // landed hits, for Dismember
    public int guillotineX;    // where the blade will drop

    public override int MaxHp { get { return 150; } }
    public override int WalkSpeed { get { return 42; } }

    protected override MoveDef LightAttack { get { return Whip; } }
    protected override MoveDef HeavyAttack { get { return ChargedWhip; } }
    protected override MoveDef GrabMove { get { return IronMaiden; } }
    protected override MoveDef SpecialMove { get { return BearTrap; } }
    protected override MoveDef UltimateMove { get { return Guillotine; } }

    public override DashDef ForwardDash { get { return new DashDef(22, 80); } }
    public override DashDef BackDash { get { return new DashDef(24, 70); } }
    public override int DashCooldown { get { return 60; } }
    // Heavy in the air too: a short, slow air dash (0.84 units; the standard is 1.3).
    public override DashDef AirDash { get { return new DashDef(12, 70); } }

    public override void OnHitboxSpawned(GameSim sim, HitboxInstance hit)
    {
        if (hit.def == ChargedWhip.hitboxes[0])
        {
            hit.box.w = hit.def.box.w + WhipExtraReach * chargeTicks / WhipFullCharge;
        }
    }

    public override void OnMoveTick(GameSim sim)
    {
        if (move == BearTrap && stateTick == 15)
        {
            if (sim.CountHitboxes(index, TrapHit) >= MaxTraps) sim.RemoveOldestHitbox(index, TrapHit);
            sim.SpawnHitboxAt(index, TrapHit, x, facing);
        }
        else if (move == Guillotine)
        {
            if (stateTick == 0)
            {
                guillotineX = Opponent(sim).x;   // locked in now; it doesn't follow them
                sim.Telegraph(index, guillotineX, GuillotineWidth, GuillotineDelay);
            }
            else if (stateTick == GuillotineDelay)
            {
                sim.SpawnHitboxAt(index, BladeHit, guillotineX, facing);
            }
        }
    }

    public override void OnHitDealt(GameSim sim, Fighter victim, HitboxInstance hit)
    {
        if (hit.blocked) return;

        bool whipOrTrap = hit.def == Whip.hitboxes[0] || hit.def == ChargedWhip.hitboxes[0]
                       || hit.def == IronMaiden.hitboxes[0] || hit.def == TrapHit;
        if (whipOrTrap) victim.AddBleed();
        if (hit.def == TrapHit) victim.ApplySlow(SlowPercent, SlowTicks);

        hitCounter++;
        if (hitCounter % DismemberEvery == 0) victim.ApplySlow(SlowPercent, SlowTicks);
    }

    public override string HudInfo
    {
        get { return "Dismember in " + (DismemberEvery - hitCounter % DismemberEvery); }
    }

    public override int HashExtra() { return hitCounter * 31 + guillotineX; }
}
