// Shared helpers for the EditMode tests: start a match, run ticks, build test-only fighters.
// Keeping these in one place means a change to how matches start (GameSim.Reset) only has to be
// fixed here, not in every test.
public static class SimHarness
{
    public const int None = 0;

    // A fresh match between two registered characters standing `gap` milli-units apart
    // (centre to centre), in the middle of the arena. Built exactly like the game builds it.
    public static GameSim NewMatch(int p0Character, int p1Character, int gap)
    {
        GameSim sim = new GameSim();
        sim.hitStopOn = false;   // the tests count ticks after hits; HitStopTests switches it back on
        sim.Reset(1, CharacterList.Factory(p0Character), CharacterList.Factory(p1Character));
        Place(sim, gap);
        return sim;
    }

    // The same with two fighter objects a test has set up (e.g. a TestFighter with its own moves).
    // Unlike the game, the SAME object is reused every round: a test's settings survive, and
    // Fighter.Init still resets its state. Pass two different objects.
    public static GameSim NewMatch(Fighter p0, Fighter p1, int gap)
    {
        GameSim sim = new GameSim();
        sim.hitStopOn = false;
        sim.Reset(1, delegate { return p0; }, delegate { return p1; });
        Place(sim, gap);
        return sim;
    }

    static void Place(GameSim sim, int gap)
    {
        sim.fighters[0].x = -gap / 2;
        sim.fighters[1].x = gap / 2;
    }

    public static void Run(GameSim sim, int in0, int in1, int ticks)
    {
        for (int i = 0; i < ticks; i++) sim.Step(in0, in1);
    }

    // "Back" for each player at the start of a match: player 1 (left) faces right, so back is Left.
    public const int BackP0 = Buttons.Left;
    public const int BackP1 = Buttons.Right;
}

// A fighter whose moves each test picks. Not registered in CharacterList, so it never shows up
// in the game. The defaults are the template's (BaseFighter), so only what a test sets changes.
public class TestFighter : BaseFighter
{
    public MoveDef light;
    public MoveDef heavy;

    protected override MoveDef LightAttack { get { return light != null ? light : base.LightAttack; } }
    protected override MoveDef HeavyAttack { get { return heavy != null ? heavy : base.HeavyAttack; } }

    // A one-hitbox melee move, built like BaseFighter.Melee, that tests can tweak (grab, unblockable).
    public static MoveDef MeleeMove(int damage)
    {
        return Melee("Light", totalTicks: 20, spawnTick: 2, activeTicks: 3,
                     box: new Box(300, 0, 1200, 2000),
                     damage: damage, hitstun: 12, knockback: 0);
    }

    // A one-hitbox projectile that tests can tweak: flies forward at `speed` from tick 2.
    public static MoveDef ProjectileMove(int damage, int speed)
    {
        return Projectile("Special", totalTicks: 20, spawnTick: 2, lifetime: 200,
                          box: new Box(400, 500, 400, 600),
                          speed: speed, damage: damage, hitstun: 12, knockback: 0);
    }

    // A move with no hitboxes.
    public static MoveDef PoseMove(int totalTicks)
    {
        return Pose("Light", totalTicks);
    }
}

// A fighter for the kit-extension tests: every slot and every hook can be set per test, so each
// test switches on exactly the rule it checks. Not registered, so it never shows up in the game.
public class KitFighter : BaseFighter
{
    public MoveDef light, heavy, grab, special, ultimate, holdMove;
    public bool canBlock = true;
    public bool intangible;
    public bool canGainMeter = true;
    public bool ignoresStatuses;
    public int stackMultiplier = 1;
    public int taps;
    public int moveEnds;
    public HitboxDef dashHitbox;
    public System.Action<GameSim, Fighter, HitboxInstance> modifyOutgoing;
    public System.Action<GameSim, HitboxInstance> onSpawned;
    public System.Action<GameSim, KitFighter> onMoveStart;
    public System.Action<GameSim, KitFighter> onMoveTick;
    public System.Func<MoveDef, bool> copies;

    protected override MoveDef LightAttack { get { return light; } }
    protected override MoveDef HeavyAttack { get { return heavy; } }
    protected override MoveDef GrabMove { get { return grab; } }
    protected override MoveDef SpecialMove { get { return special; } }
    protected override MoveDef UltimateMove { get { return ultimate; } }

    protected override bool CanUseMove(GameSim sim, MoveDef m) { return true; }   // no projectile limit
    public override bool CanBlock { get { return canBlock; } }
    public override bool IsIntangible { get { return intangible; } }
    public override bool CanGainMeter { get { return canGainMeter; } }
    public override bool IgnoresStatus(StatusKind kind) { return ignoresStatuses; }
    public override int StackMultiplier(int count) { return stackMultiplier; }
    public override HitboxDef DashHitbox(bool forward) { return forward ? dashHitbox : null; }
    public override void OnTap(GameSim sim, MoveDef tapped) { taps++; }
    public override void OnMoveEnd(GameSim sim, MoveDef ended, bool finished) { moveEnds++; }
    public override bool SummonCopiesMove(MoveDef m) { return copies != null && copies(m); }

    public override MoveDef ChooseHoldMove(GameSim sim, int input)
    {
        return (input & Buttons.Special) != 0 ? holdMove : null;
    }

    public override void ModifyOutgoingHit(GameSim sim, Fighter victim, HitboxInstance hit)
    {
        if (modifyOutgoing != null) modifyOutgoing(sim, victim, hit);
    }

    public override void OnHitboxSpawned(GameSim sim, HitboxInstance hit)
    {
        if (onSpawned != null) onSpawned(sim, hit);
    }

    public override void OnMoveStart(GameSim sim)
    {
        if (onMoveStart != null) onMoveStart(sim, this);
    }

    public override void OnMoveTick(GameSim sim)
    {
        if (onMoveTick != null) onMoveTick(sim, this);
    }

    // For tests that need to put a move on cooldown directly.
    public void PutOnCooldown(MoveDef m, int ticks) { StartCooldown(m, ticks); }
}
