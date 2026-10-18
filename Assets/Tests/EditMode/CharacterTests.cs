using NUnit.Framework;
using static SimHarness;

// Quick checks that the character framework still works. They run the real sim with no scene,
// so they finish in milliseconds.
//   Run them: Window > General > Test Runner > EditMode > Run All
//
// When you add a character with a special rule, add a test here that proves the rule works.
// The shared kit (grab, dash, meter, rounds...) is tested in KitTests.cs. Helpers: SimHarness.cs.
public class CharacterTests
{
    [Test]
    public void CodeCharactersAreRegistered()
    {
        Assert.IsInstanceOf<Brawler>(CharacterList.Create(Brawler.Id));
        Assert.IsInstanceOf<Shooter>(CharacterList.Create(Shooter.Id));
        Assert.IsInstanceOf<Sandbag>(CharacterList.Create(Sandbag.Id));
        Assert.AreEqual(Shooter.Id, CharacterList.Create(Shooter.Id).characterId);
    }

    [Test]
    public void RosterCharactersAreRegisteredWithTheirSheetNames()
    {
        // D-40: the in-game names come from the Moveset Tracker; the class names stay.
        int[] ids = { Samurai.Id, Painter.Id, Cowboy.Id, Elephant.Id, Executioner.Id, PlagueDoctor.Id, Nek.Id };
        string[] names = { "Ruy", "Paintress", "Noir", "Room", "The Executioner", "Dr. Remy", "Nek" };
        System.Type[] types = { typeof(Samurai), typeof(Painter), typeof(Cowboy), typeof(Elephant),
                                typeof(Executioner), typeof(PlagueDoctor), typeof(Nek) };
        string[] slots = { "Light", "Heavy", "Grab", "Special", "Ultimate", "JumpLight", "JumpHeavy" };

        for (int i = 0; i < ids.Length; i++)
        {
            Fighter f = CharacterList.Create(ids[i]);
            Assert.AreEqual(ids[i], f.characterId);
            Assert.AreEqual(types[i], f.GetType());
            Assert.IsTrue(CharacterList.IsCodeCharacter(ids[i]));
            Assert.AreEqual(names[i], CharacterList.NameOf(ids[i]));

            // Move names are Animator state names (D-17), so every move must use a slot name.
            MoveDef[] moves = f.ListMoves();
            Assert.GreaterOrEqual(moves.Length, 6, names[i] + " should list its moves");
            for (int m = 0; m < moves.Length; m++)
            {
                Assert.Contains(moves[m].name, slots, names[i] + "'s move names must be slot names");
            }
        }
        Assert.AreEqual(16, Nek.Id, "Nek takes the first free roster id (D-40)");
    }

    [Test]
    public void UnknownIdFallsBackToBrawler()
    {
        Assert.IsInstanceOf<Brawler>(CharacterList.Create(-12345));
    }

    [Test]
    public void JabHitsStandingOpponent()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 1200);
        int startHp = sim.fighters[1].hp;

        Run(sim, Buttons.Light, None, 1);   // tap J
        Run(sim, None, None, 30);

        Assert.Less(sim.fighters[1].hp, startHp);
    }

    [Test]
    public void HoldingBackBlocks()
    {
        GameSim sim = NewMatch(Brawler.Id, Brawler.Id, 1200);
        int startHp = sim.fighters[1].hp;
        int back = Buttons.Right;           // player 2 stands on the right, so back is Right
        bool sawBlockstun = false;

        sim.Step(Buttons.Light, back);
        for (int i = 0; i < 30; i++)
        {
            sim.Step(None, back);
            if (sim.fighters[1].state == FighterStateId.Blockstun) sawBlockstun = true;
        }

        Assert.AreEqual(startHp, sim.fighters[1].hp, "a blocked hit should do no damage");
        Assert.IsTrue(sawBlockstun, "a blocked hit should cause blockstun");
    }

    [Test]
    public void ShooterHasOnlyOneFireballOnScreen()
    {
        GameSim sim = NewMatch(Shooter.Id, Brawler.Id, 14000);
        int mostOnScreen = 0;
        int fireballsThrown = 0;

        // Hold K for a long time: the Shooter should throw again only after the last one is gone.
        for (int i = 0; i < 400; i++)
        {
            sim.Step(Buttons.Heavy, None);
            if (sim.fighters[0].state == FighterStateId.Attack && sim.fighters[0].stateTick == 0) fireballsThrown++;

            int onScreen = 0;
            for (int h = 0; h < sim.hitboxes.Count; h++)
            {
                if (sim.hitboxes[h].owner == 0 && !sim.hitboxes[h].def.followOwner) onScreen++;
            }
            if (onScreen > mostOnScreen) mostOnScreen = onScreen;
        }

        Assert.AreEqual(1, mostOnScreen);
        Assert.GreaterOrEqual(fireballsThrown, 2, "should be able to throw again once the first one lands");
    }

    [Test]
    public void SandbagIgnoresAllInput()
    {
        GameSim sim = NewMatch(Brawler.Id, Sandbag.Id, 6000);
        int startX = sim.fighters[1].x;

        Run(sim, None, Buttons.Left | Buttons.Light | Buttons.Heavy, 60);

        Assert.AreEqual(startX, sim.fighters[1].x, "Sandbag should not walk");
        Assert.AreEqual(FighterStateId.Idle, sim.fighters[1].state, "Sandbag should not attack");
        Assert.AreEqual(0, sim.hitboxes.Count);
    }

    [Test]
    public void SandbagNeverGetsKnockedOut()
    {
        GameSim sim = NewMatch(Brawler.Id, Sandbag.Id, 1200);
        int lowestHp = sim.fighters[1].hp;

        // Haymaker it over and over (walking back in after each knockback) for well over its health.
        // Sandbag holds Right (= back) the whole time: it must NOT block.
        for (int i = 0; i < 3000; i++)
        {
            bool inRange = sim.fighters[1].x - sim.fighters[0].x < 1500;
            sim.Step(inRange ? Buttons.Heavy : Buttons.Right, Buttons.Right);
            if (sim.fighters[1].hp < lowestHp) lowestHp = sim.fighters[1].hp;
        }

        Assert.IsFalse(sim.IsOver, "the match should never end against Sandbag");
        Assert.Less(lowestHp, sim.fighters[1].MaxHp / 2, "it should have taken real damage");
        Assert.Greater(sim.fighters[1].hp, 0);
    }

    [Test]
    public void SameInputsGiveSameResult()
    {
        // Lockstep online play depends on this: both machines must end up identical.
        GameSim a = NewMatch(Shooter.Id, Brawler.Id, 6000);
        GameSim b = NewMatch(Shooter.Id, Brawler.Id, 6000);
        int[] script = { Buttons.Heavy, Buttons.Right, Buttons.Light, None, Buttons.Left, Buttons.Heavy };

        for (int i = 0; i < 600; i++)
        {
            int in0 = script[(i / 7) % script.Length];
            int in1 = script[(i / 11) % script.Length];
            a.Step(in0, in1);
            b.Step(in0, in1);
        }

        Assert.AreEqual(a.Hash(), b.Hash());
    }
}
