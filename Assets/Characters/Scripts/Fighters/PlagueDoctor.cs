// DR. REMY (+ Remy's Rat): roster character, id 15. Mixup. Design: Design/Characters/DrRemy.md
// (The class is still called PlagueDoctor, D-40. The in-game name is "Dr. Remy"; the rat is Remy.)
//
// A frail, quick doctor who picks the right potion for the moment. 60 HP, walk 75.
//   Light (J)      Cane jab: a little more reach than a jab.
//   Heavy (K)      Throw Remy: a rat projectile. No on-screen limit, only the cooldown.
//   Grab (L)       Leech grab: 6 damage, and leeches drain 1 HP a second for 5 s, healing him.
//   Dashes         short.
//   Special (I)    Potions: TAP I to switch Poison -> Heal -> Slow. HOLD I for 15 ticks to use it:
//                    Poison  a cloud around him for 3 s: 1 damage every 20 ticks to the opponent
//                            inside (can't KO). One cloud at a time.
//                    Heal    he drinks it: +10 HP (never above 60). Its own 5 s cooldown.
//                    Slow    thrown: on hit, a 3 s slow.
//                  Using any potion puts the others on a 30-tick cooldown too.
//   Ultimate (O)   Unleash the Rats: 3 low waves of rats cross the screen, 30 ticks apart. Each
//                  can't be blocked, but a jump clears it. 10 damage each.
//   Perk 1         Apothecary: the potion choice (above).
//   Perk 2         Leeches (above).
public class PlagueDoctor : BaseFighter
{
    public const int Id = 15;

    public const int PotionPoison = 0;
    public const int PotionHeal = 1;
    public const int PotionSlow = 2;
    public const int TapWindow = 15;          // hold I this long to use the potion
    public const int SharedPotionCooldown = 30;
    public const int HealCooldown = 300;
    public const int HealAmount = 10;
    public const int PoisonTag = 1;
    public const int LeechTicks = 300;
    public const int SlowPercent = 40;
    public const int SlowTicks = 180;

    public static readonly MoveDef CaneJab = Melee("Light",
        totalTicks: 16, spawnTick: 5, activeTicks: 3,
        box: new Box(400, 900, 1200, 300),
        damage: 5, hitstun: 12, knockback: 40, cooldown: LightCooldown);

    // An overhead lob: Remy throws the rat up and over, and it comes down on the opponent. It
    // leaves his hand about head high, rises, then falls (RatLobUp / RatLobGravity); it is gone
    // when it reaches the floor, about 46 ticks and 2.7 units out. Close up it sails past the head.
    public const int RatLobUp = 45;
    public const int RatLobGravity = 3;
    public static readonly MoveDef ThrowRemy = MakeLob(Projectile("Heavy",
        totalTicks: 32, spawnTick: 12, lifetime: 60,
        box: new Box(200, 1100, 450, 450),
        speed: 60, damage: 8, hitstun: 18, knockback: 50,
        visual: "rat", cooldown: HeavyCooldown), RatLobUp, RatLobGravity);

    public static readonly MoveDef LeechGrab = HoldGrab(Grab("Grab",
        totalTicks: 40, spawnTick: 5, activeTicks: 2,
        box: new Box(300, 600, 700, 1000),
        damage: 6, hitstun: 40, knockback: 90, cooldown: GrabCooldown));

    // The three potions. Each waits TapWindow ticks first: let go before then = a tap (switch).
    public static readonly MoveDef PoisonPotion = MakePotion(Pose("Special", totalTicks: TapWindow + 30, cooldown: SharedPotionCooldown));
    public static readonly MoveDef HealPotion = MakePotion(Pose("Special", totalTicks: TapWindow + 40, cooldown: HealCooldown));
    public static readonly MoveDef SlowPotion = MakePotion(Projectile("Special",
        totalTicks: TapWindow + 30, spawnTick: TapWindow + 12, lifetime: 80,
        box: new Box(400, 900, 400, 400),
        speed: 90, damage: 2, hitstun: 10, knockback: 20,
        visual: "potion_slow", cooldown: SharedPotionCooldown));

    public static readonly MoveDef UnleashTheRats = MakeRats();

    static MoveDef MakeLob(MoveDef m, int up, int gravity)
    {
        m.hitboxes[0].vy = up;
        m.hitboxes[0].gravity = gravity;
        return m;
    }

    static MoveDef MakePotion(MoveDef m)
    {
        m.holdButton = Buttons.Special;
        m.tapWindow = TapWindow;
        return m;
    }

    static MoveDef MakeRats()
    {
        MoveDef m = new MoveDef { name = "Ultimate", totalTicks = 80, ultimate = true };
        m.hitboxes = new HitboxDef[3];
        for (int i = 0; i < 3; i++)
        {
            m.hitboxes[i] = new HitboxDef
            {
                spawnTick = 6 + 30 * i, lifetime = 110, box = new Box(0, 0, 800, 500),
                damage = 10, hitstun = 12, knockback = 0,
                followOwner = false, vx = 160, unblockable = true, visual = "rats",
            };
        }
        return m;
    }

    public int selectedPotion = PotionPoison;   // resets to Poison each round (a fresh fighter)
    public bool cycledThisPress;                // this press of I already switched the potion

    public override int MaxHp { get { return 60; } }
    public override int WalkSpeed { get { return 75; } }

    protected override MoveDef LightAttack { get { return CaneJab; } }
    protected override MoveDef HeavyAttack { get { return ThrowRemy; } }
    protected override MoveDef GrabMove { get { return LeechGrab; } }
    protected override MoveDef SpecialMove { get { return SelectedPotionMove; } }
    protected override MoveDef UltimateMove { get { return UnleashTheRats; } }

    public override DashDef ForwardDash { get { return new DashDef(10, 120); } }
    public override DashDef BackDash { get { return new DashDef(10, 110); } }

    public MoveDef SelectedPotionMove
    {
        get
        {
            if (selectedPotion == PotionHeal) return HealPotion;
            if (selectedPotion == PotionSlow) return SlowPotion;
            return PoisonPotion;
        }
    }

    public override MoveDef[] ListMoves()
    {
        // One "Special" Animator state covers all three potions, so list just one of them.
        return new MoveDef[] { CaneJab, ThrowRemy, LeechGrab, PoisonPotion, UnleashTheRats, StandardJumpLight, StandardJumpHeavy };
    }

    // While the press of I that switched the potion is still held, it can't also start one.
    public override MoveDef ChooseMove(GameSim sim, int input)
    {
        if (cycledThisPress) input &= ~Buttons.Special;
        return base.ChooseMove(sim, input);
    }

    public override void OnTick(GameSim sim)
    {
        if ((prevInput & Buttons.Special) == 0) cycledThisPress = false;   // I was let go

        // A tap normally switches through OnTap. If the selected potion can't start (cooldown),
        // there's no move to tap, so a fresh press switches straight away.
        bool idle = state == FighterStateId.Idle || state == FighterStateId.Walk;
        if (idle && (pressedInput & Buttons.Special) != 0 && !IsUsable(sim, SelectedPotionMove))
        {
            NextPotion();
            cycledThisPress = true;
        }
    }

    public override void OnTap(GameSim sim, MoveDef tapped)
    {
        NextPotion();
    }

    void NextPotion()
    {
        selectedPotion = (selectedPotion + 1) % 3;
    }

    public override void OnMoveTick(GameSim sim)
    {
        if (move == PoisonPotion && stateTick == TapWindow + 10)
        {
            if (sim.CountZones(index, PoisonTag) > 0) sim.RemoveOldestZone(index, PoisonTag);
            Zone cloud = new Zone();
            cloud.owner = index;
            cloud.tag = PoisonTag;
            cloud.x = x;
            cloud.halfWidth = 1500;
            cloud.ticksLeft = 180;
            cloud.enemyDamage = 1;
            cloud.damageInterval = 20;
            cloud.visual = "poison";
            sim.AddZone(cloud);
        }
        else if (move == HealPotion && stateTick == TapWindow + 20)
        {
            Heal(sim, HealAmount);
        }
    }

    // Using one potion puts the other two on (at least) the shared cooldown.
    public override void OnMoveEnd(GameSim sim, MoveDef ended, bool finished)
    {
        if (ended != PoisonPotion && ended != HealPotion && ended != SlowPotion) return;
        ShareCooldown(PoisonPotion, ended);
        ShareCooldown(HealPotion, ended);
        ShareCooldown(SlowPotion, ended);
    }

    void ShareCooldown(MoveDef potion, MoveDef used)
    {
        if (potion != used && CooldownLeft(potion) < SharedPotionCooldown) StartCooldown(potion, SharedPotionCooldown);
    }

    public override void OnHitDealt(GameSim sim, Fighter victim, HitboxInstance hit)
    {
        if (hit.blocked) return;
        if (hit.def == LeechGrab.hitboxes[0]) victim.ApplyLeech(index, LeechTicks);
        if (hit.def == SlowPotion.hitboxes[0]) victim.ApplySlow(SlowPercent, SlowTicks);
    }

    // AI hints: hold I past the tap window to use a potion, and tap to switch to the one that suits
    // the moment: Heal when hurt and it's ready, Poison up close, Slow at range.
    public override int SpecialHoldTicks { get { return TapWindow + 2; } }

    public override bool WantsSpecialSwitch(GameSim sim)
    {
        int wanted;
        if (hp * 2 < MaxHp && !IsOnCooldown(HealPotion)) wanted = PotionHeal;
        else if (System.Math.Abs(Opponent(sim).x - x) < 2000) wanted = PotionPoison;
        else wanted = PotionSlow;
        return wanted != selectedPotion;
    }

    public override string HudInfo
    {
        get
        {
            string name = selectedPotion == PotionHeal ? "Heal" : selectedPotion == PotionSlow ? "Slow" : "Poison";
            int wait = CooldownLeft(SelectedPotionMove);
            return wait > 0 ? "Potion: " + name + " (" + (wait + 59) / 60 + "s)" : "Potion: " + name;
        }
    }

    public override int HashExtra() { return selectedPotion * 2 + (cycledThisPress ? 1 : 0); }
}
