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
}
