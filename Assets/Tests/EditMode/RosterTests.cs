using NUnit.Framework;
using static SimHarness;

// One test per special rule of each launch-roster character (the "Tests" list in each
// Design/Characters/<Name>.md). They play the real characters through the real sim with button
// input, so they also prove every move can be done from the keyboard.
//
// Sandbag is the usual victim: it never moves, blocks or gets KO'd, so damage is easy to read.
// The rules these characters are built from are tested on their own in KitExtensionTests.cs.
public class RosterTests
{
    // Runs until `f` is on the ground and able to act again (or `max` ticks pass).
    static void UntilFree(GameSim sim, Fighter f, int max = 400)
    {
        for (int i = 0; i < max; i++)
        {
            bool free = (f.state == FighterStateId.Idle || f.state == FighterStateId.Walk) && !f.IsAirborne;
            if (free) return;
            sim.Step(None, None);
        }
    }

    static bool Saw(GameSim sim, SimEventType type)
    {
        for (int e = 0; e < sim.events.Count; e++) if (sim.events[e].type == type) return true;
        return false;
    }

    static int Count(GameSim sim, SimEventType type)
    {
        int n = 0;
        for (int e = 0; e < sim.events.Count; e++) if (sim.events[e].type == type) n++;
        return n;
    }

    // Presses `button` for player 0, holds it `extraHeld` more ticks, then lets go and waits.
    static void Press(GameSim sim, int button, int extraHeld = 0, int thenWait = 0)
    {
        Run(sim, button, None, 1 + extraHeld);
        Run(sim, None, None, thenWait);
    }

    // ================= Ruy =================

    static int HadokenDamage(int extraHeld)
    {
        GameSim sim = NewMatch(Samurai.Id, Sandbag.Id, 3000);
        Press(sim, Buttons.Special, extraHeld, 200);
        return 100 - sim.fighters[1].hp;
    }

    [Test]
    public void RuyHadokenDamageScalesWithCharge()
    {
        Assert.AreEqual(10, HadokenDamage(0), "a tap");
        Assert.AreEqual(15, HadokenDamage(30), "half charged");
        Assert.AreEqual(20, HadokenDamage(60), "fully charged");
        Assert.AreEqual(20, HadokenDamage(100), "the charge is capped, and it throws by itself");
    }

    [Test]
    public void RuyHasOneHadokenOnScreen()
    {
        GameSim sim = NewMatch(Samurai.Id, Brawler.Id, 15000);
        int most = 0;
        for (int i = 0; i < 300; i++)
        {
            sim.Step(i % 2 == 0 ? Buttons.Special : None, None);   // tap, tap, tap...
            int onScreen = 0;
            for (int h = 0; h < sim.hitboxes.Count; h++) if (sim.hitboxes[h].owner == 0 && sim.hitboxes[h].IsProjectile) onScreen++;
            most = System.Math.Max(most, onScreen);
        }
        Assert.AreEqual(1, most);
    }

    [Test]
    public void RuyRageAddsDamageWhenHurt()
    {
        GameSim sim = NewMatch(Samurai.Id, Sandbag.Id, 1200);
        sim.fighters[0].hp = 50;

        Press(sim, Buttons.Light, 0, 20);

        Assert.AreEqual(100 - 5 * 125 / 100, sim.fighters[1].hp, "+25% at half health");
    }

    // Fills the meter, fires the Ultimate and runs through the superflash.
    static GameSim AfterSuperflash(int p0, int p1, int gap)
    {
        GameSim sim = NewMatch(p0, p1, gap);
        sim.fighters[0].meter = Fighter.MeterMax;
        sim.Step(Buttons.Ultimate, None);
        Run(sim, None, None, GameSim.SuperFlashTicks);
        return sim;
    }

    [Test]
    public void RuyShoryukenIsInvincibleOnStartupOnly()
    {
        GameSim sim = AfterSuperflash(Samurai.Id, Sandbag.Id, 6000);
        Fighter ruy = sim.fighters[0];

        for (int t = 0; t < 12; t++)
        {
            sim.Step(None, None);
            Assert.AreEqual(t < Samurai.ShoryukenInvincibleTicks, ruy.IsIntangible, "tick " + ruy.stateTick);
        }
    }

    [Test]
    public void RuyShoryukenIgnoresAHitDuringStartup()
    {
        GameSim sim = AfterSuperflash(Samurai.Id, Sandbag.Id, 6000);
        Fighter ruy = sim.fighters[0];
        HitboxDef everywhere = new HitboxDef { lifetime = 5, box = new Box(-2000, 0, 4000, 5000), damage = 10, followOwner = false };

        sim.Step(None, None);
        sim.SpawnHitboxAt(1, everywhere, ruy.x, -1);
        Run(sim, None, None, 6);

        Assert.AreEqual(100, ruy.hp);
    }

    [Test]
    public void RuyShoryukenKeepsItsRecoveryAfterLanding()
    {
        GameSim sim = AfterSuperflash(Samurai.Id, Sandbag.Id, 6000);
        Fighter ruy = sim.fighters[0];

        Run(sim, None, None, 5);
        Assert.Greater(ruy.y, 0, "it leaves the ground");
        while (ruy.IsAirborne) sim.Step(None, None);

        Assert.AreEqual(FighterStateId.Attack, ruy.state, "still in the move after landing");
        Assert.Less(ruy.stateTick, Samurai.Shoryuken.totalTicks - 1);
        UntilFree(sim, ruy);
        Assert.AreEqual(FighterStateId.Idle, ruy.state);
    }

    [Test]
    public void RuyShoryukenHitsForThirty()
    {
        GameSim sim = AfterSuperflash(Samurai.Id, Sandbag.Id, 1200);
        Run(sim, None, None, 60);
        Assert.AreEqual(70, sim.fighters[1].hp);
    }

    [Test]
    public void RuyShoryukenLaunchesTheVictim()
    {
        GameSim sim = AfterSuperflash(Samurai.Id, Sandbag.Id, 1200);
        int highest = 0;
        for (int i = 0; i < 60; i++)
        {
            sim.Step(None, None);
            highest = System.Math.Max(highest, sim.fighters[1].y);
        }
        Assert.Greater(highest, 3000, "launched well above the ground");
    }

    // ================= K3N =================

    [Test]
    public void K3NInstallStartsWithAnyMeterAndDrainsIt()
    {
        GameSim sim = NewMatch(K3N.Id, Sandbag.Id, 8000);
        K3N k3n = (K3N)sim.fighters[0];
        k3n.meter = 300;

        sim.Step(Buttons.Ultimate, None);
        Assert.IsTrue(k3n.installOn);
        Assert.AreEqual(0, sim.freezeTicks, "no superflash");
        Assert.AreEqual(300 - K3N.InstallDrainPerTick, k3n.meter);

        Run(sim, None, None, 200);
        Assert.AreEqual(0, k3n.meter);
        Assert.IsFalse(k3n.installOn, "it switches off when the meter is empty");
    }

    [Test]
    public void K3NInstallNeedsSomeMeter()
    {
        GameSim sim = NewMatch(K3N.Id, Sandbag.Id, 8000);
        sim.Step(Buttons.Ultimate, None);
        Assert.IsFalse(((K3N)sim.fighters[0]).installOn);
    }

    [Test]
    public void K3NInstallToggleOffKeepsTheRest()
    {
        GameSim sim = NewMatch(K3N.Id, Sandbag.Id, 8000);
        K3N k3n = (K3N)sim.fighters[0];
        k3n.meter = 500;

        sim.Step(Buttons.Ultimate, None);
        Run(sim, None, None, 9);
        sim.Step(Buttons.Ultimate, None);   // a fresh press
        int left = k3n.meter;
        Run(sim, None, None, 30);

        Assert.IsFalse(k3n.installOn);
        Assert.AreEqual(left, k3n.meter, "no more drain once it's off");
        Assert.AreEqual(500 - 10 * K3N.InstallDrainPerTick, left);
    }

    [Test]
    public void K3NInstallAddsDamageAndBlocksMeterGain()
    {
        GameSim sim = NewMatch(K3N.Id, Sandbag.Id, 1200);
        K3N k3n = (K3N)sim.fighters[0];
        k3n.meter = Fighter.MeterMax;
        sim.Step(Buttons.Ultimate, None);

        int before = k3n.meter;
        Press(sim, Buttons.Light, 0, 20);

        Assert.AreEqual(100 - 4 * 115 / 100, sim.fighters[1].hp);
        Assert.AreEqual(before - 21 * K3N.InstallDrainPerTick, k3n.meter, "only the drain: no meter from the hit");
    }

    [Test]
    public void K3NInstallRemovesCooldowns()
    {
        GameSim sim = NewMatch(K3N.Id, Sandbag.Id, 8000);
        K3N k3n = (K3N)sim.fighters[0];
        Press(sim, Buttons.Light, 0, 20);
        Assert.IsTrue(k3n.IsOnCooldown(K3N.CapeWhip));

        k3n.meter = Fighter.MeterMax;
        sim.Step(Buttons.Ultimate, None);
        Assert.IsFalse(k3n.IsOnCooldown(K3N.CapeWhip), "Install clears running cooldowns");

        Press(sim, Buttons.Light, 0, 20);
        Assert.IsFalse(k3n.IsOnCooldown(K3N.CapeWhip), "and starts none");
    }

    [Test]
    public void K3NRapidPunchesKeepTheVictimInHitstun()
    {
        GameSim sim = NewMatch(K3N.Id, Sandbag.Id, 1200);
        int hits = 0;
        for (int i = 0; i < 120; i++)
        {
            sim.Step(i < 60 ? Buttons.Special : None, None);
            hits += Count(sim, SimEventType.Hit);
        }

        Assert.AreEqual(30, hits, "a held string lands every jab: 10 passes of 3");
        Assert.AreEqual(100 - 30 * 1, sim.fighters[1].hp);
    }

    [Test]
    public void K3NRapidPunchesTappedThrowThree()
    {
        GameSim sim = NewMatch(K3N.Id, Sandbag.Id, 1200);
        Press(sim, Buttons.Special, 0, 60);
        Assert.AreEqual(100 - 3 * 3 * 1, sim.fighters[1].hp);   // 3 passes of a 3-hit jab, 1 damage each
    }

    [Test]
    public void K3NChargedPunchScalesWithCharge()
    {
        GameSim tapped = NewMatch(K3N.Id, Sandbag.Id, 1200);
        Press(tapped, Buttons.Heavy, 0, 40);
        GameSim full = NewMatch(K3N.Id, Sandbag.Id, 1200);
        Press(full, Buttons.Heavy, 60, 40);

        Assert.AreEqual(90, tapped.fighters[1].hp);
        Assert.AreEqual(82, full.fighters[1].hp);
    }

    [Test]
    public void K3NCooldownsShrinkWhenHurt()
    {
        GameSim sim = NewMatch(K3N.Id, Sandbag.Id, 8000);
        K3N k3n = (K3N)sim.fighters[0];
        k3n.hp = 50;

        Press(sim, Buttons.Special, 0, 0);
        UntilFree(sim, k3n);

        // Special cooldown 30, -25% at half health = 22 (+1: "ticks until usable").
        Assert.AreEqual(30 * 75 / 100 + 1, k3n.CooldownLeft(K3N.RapidPunches));
    }

    // ================= Noir =================

    [Test]
    public void NoirHasSixBulletsPerRound()
    {
        GameSim sim = NewMatch(Cowboy.Id, Sandbag.Id, 15000);
        Cowboy noir = (Cowboy)sim.fighters[0];
        int shots = 0;
        for (int i = 0; i < 1000; i++)
        {
            sim.Step(Buttons.Special, None);
            if (noir.state == FighterStateId.Attack && noir.stateTick == -1) shots++;
        }

        Assert.AreEqual(6, shots);
        Assert.AreEqual(0, noir.ammo);
        Assert.AreEqual(6, ((Cowboy)CharacterList.Create(Cowboy.Id)).ammo, "every round builds a fresh Noir with 6");
    }

    [Test]
    public void NoirRapidFireGivesUnlimitedAmmoFor300Ticks()
    {
        GameSim sim = AfterSuperflash(Cowboy.Id, Sandbag.Id, 15000);
        Cowboy noir = (Cowboy)sim.fighters[0];
        noir.ammo = 0;
        UntilFree(sim, noir);
        Assert.Greater(noir.rapidFireTicks, 0);

        int shots = 0;
        for (int i = 0; i < 280; i++)
        {
            sim.Step(Buttons.Special, None);
            if (noir.state == FighterStateId.Attack && noir.stateTick == -1) shots++;
        }
        Assert.Greater(shots, 6, "more than a full gun's worth, with no ammo");

        Run(sim, None, None, 60);
        sim.Step(Buttons.Special, None);
        Assert.AreNotEqual(FighterStateId.Attack, noir.state, "it ran out, and the gun is still empty");
    }

    static int KnockbackAt(int gap)
    {
        GameSim sim = NewMatch(Cowboy.Id, Sandbag.Id, gap);
        sim.Step(Buttons.Light, None);
        for (int i = 0; i < 20; i++)
        {
            sim.Step(None, None);
            if (Saw(sim, SimEventType.Hit)) return System.Math.Abs(sim.fighters[1].vx);
        }
        return -1;
    }

    [Test]
    public void NoirPointBlankKnocksBackHarderUpClose()
    {
        int close = KnockbackAt(1000);
        int far = KnockbackAt(3900);

        Assert.AreEqual(30 * (100 + 100 * 3000 / 4000) / 100, close);
        Assert.AreEqual(30 * (100 + 100 * 100 / 4000) / 100, far);
    }

    [Test]
    public void NoirLassoPullsTheVictimIn()
    {
        GameSim sim = NewMatch(Cowboy.Id, Sandbag.Id, 2400);
        sim.Step(Buttons.Grab, None);
        for (int i = 0; i < 20 && !Saw(sim, SimEventType.Grab); i++) sim.Step(None, None);

        Assert.AreEqual(sim.fighters[0].x + 900, sim.fighters[1].x);
        Assert.AreEqual(88, sim.fighters[1].hp);
    }

    [Test]
    public void NoirFireIsALongInstantPoke()
    {
        GameSim sim = NewMatch(Cowboy.Id, Sandbag.Id, 4000);
        Press(sim, Buttons.Light, 0, 10);
        Assert.AreEqual(97, sim.fighters[1].hp, "4 units away and still hit");
    }

    // ================= Room =================

    [Test]
    public void RoomCannotBlock()
    {
        GameSim sim = NewMatch(Brawler.Id, Elephant.Id, 1200);
        sim.Step(Buttons.Light, BackP1);
        Run(sim, None, BackP1, 20);
        Assert.AreEqual(200 - 6, sim.fighters[1].hp);
    }

    static GameSim RoomHolding()
    {
        GameSim sim = NewMatch(Elephant.Id, Sandbag.Id, 1700);
        sim.Step(Buttons.Grab, None);
        for (int i = 0; i < 20 && sim.fighters[1].state != FighterStateId.Held; i++) sim.Step(None, None);
        return sim;
    }

    [Test]
    public void RoomTrunkGrabHoldsThenVineBoomCannotMiss()
    {
        GameSim sim = RoomHolding();
        Assert.AreEqual(FighterStateId.Held, sim.fighters[1].state);

        Press(sim, Buttons.Special, 0, 80);

        Assert.AreEqual(100 - 8, sim.fighters[1].hp, "the boom hit, and replaced the throw");
    }

    [Test]
    public void RoomTrunkGrabThrowsWhenTheHoldRunsOut()
    {
        GameSim sim = RoomHolding();
        Run(sim, None, None, Elephant.HoldTicks + 5);
        Assert.AreEqual(100 - 12, sim.fighters[1].hp);
    }

    [Test]
    public void RoomVineBoomDestroysProjectiles()
    {
        GameSim sim = NewMatch(Elephant.Id, Sandbag.Id, 8000);
        Fighter room = sim.fighters[0];
        HitboxDef slowShot = new HitboxDef { lifetime = 200, box = new Box(400, 900, 500, 400), damage = 10, hitstun = 10, followOwner = false, vx = 10 };
        sim.SpawnHitboxAt(1, slowShot, room.x + 1800, -1);   // drifting toward him, inside the boom's reach

        Press(sim, Buttons.Special, 0, 60);

        Assert.AreEqual(200, room.hp);
        Assert.AreEqual(0, sim.CountHitboxes(1, slowShot));
    }

    [Test]
    public void RoomVineBoomsStackWithHisCopies()
    {
        GameSim sim = NewMatch(Elephant.Id, Sandbag.Id, 1200);   // Room -600, Sandbag 600
        sim.SpawnSummon(0, 1800, 300);                           // a copy on the far side of the victim

        Press(sim, Buttons.Special, 0, 40);

        Assert.AreEqual(100 - 8 * 2, sim.fighters[1].hp, "two booms on one tick = one hit at x2");
    }

    [Test]
    public void RoomBoomStackingIsCappedAtFour()
    {
        Elephant room = (Elephant)CharacterList.Create(Elephant.Id);
        Assert.AreEqual(1, room.StackMultiplier(1));
        Assert.AreEqual(2, room.StackMultiplier(2));
        Assert.AreEqual(4, room.StackMultiplier(3));
        Assert.AreEqual(4, room.StackMultiplier(5));
    }

    [Test]
    public void RoomAddressMeGrowsHimForEightSecondsWithHyperArmor()
    {
        GameSim sim = AfterSuperflash(Elephant.Id, Sandbag.Id, 1200);
        sim.Step(None, None);
        Elephant room = (Elephant)sim.fighters[0];
        Assert.IsTrue(room.Grown);
        Assert.AreEqual(0, sim.CountSummons(0), "no more copies");
        Assert.AreEqual(1050, -room.Hurtbox.x, "body is x1.5");

        Run(sim, None, None, Elephant.GrowTicks);
        Assert.IsFalse(room.Grown);
        Assert.AreEqual(700, -room.Hurtbox.x);
    }

    [Test]
    public void RoomForwardDashHitsAndGoesFurtherWhenHurt()
    {
        Elephant full = (Elephant)CharacterList.Create(Elephant.Id);
        full.Init(0, 0);
        Elephant hurt = (Elephant)CharacterList.Create(Elephant.Id);
        hurt.Init(0, 0);
        hurt.hp = 100;
        Assert.AreEqual(24, full.ForwardDash.ticks);
        Assert.AreEqual(36, hurt.ForwardDash.ticks, "+50% at half health");

        GameSim sim = NewMatch(Elephant.Id, Sandbag.Id, 2500);
        sim.Step(Buttons.Right, None);
        sim.Step(None, None);
        sim.Step(Buttons.Right, None);
        Run(sim, None, None, 30);
        Assert.AreEqual(100 - 8, sim.fighters[1].hp, "Stampede");
    }

    [Test]
    public void RoomAndTheExecutionerHaveAShortAirDash()
    {
        int[] heavy = { Elephant.Id, Executioner.Id };
        for (int i = 0; i < heavy.Length; i++)
        {
            DashDef air = CharacterList.Create(heavy[i]).AirDash;
            Assert.AreEqual(12, air.ticks);
            Assert.AreEqual(70, air.speed, CharacterList.NameOf(heavy[i]) + " has the short air dash (merge, D-62)");
        }
        Assert.AreEqual(110, CharacterList.Create(Samurai.Id).AirDash.speed, "everyone else has the standard one");
    }

    [Test]
    public void RoomDashCooldownIsLong()
    {
        Assert.AreEqual(60, CharacterList.Create(Elephant.Id).DashCooldown);
    }

    [Test]
    public void RoomIgnoresSlowsAndScramble()
    {
        GameSim sim = NewMatch(Elephant.Id, Sandbag.Id, 8000);
        Fighter room = sim.fighters[0];
        room.ApplySlow(40, 100);
        room.ApplyScramble(sim, 100);
        for (int i = 0; i < 5; i++) room.AddBleed();

        Assert.AreEqual(100, room.SpeedPercent(sim));
        Assert.AreEqual(0, room.scrambleTicks);
        Assert.IsFalse(room.HasBleedWeakness, "he still bleeds, but ignores the -25%");
        Assert.AreEqual(5, room.bleedStacks);
    }

    // ================= The Executioner =================

    [Test]
    public void ExecutionerHasAtMostTwoTrapsAndTheOldestGoes()
    {
        GameSim sim = NewMatch(Executioner.Id, Sandbag.Id, 12000);
        Fighter exe = sim.fighters[0];
        int[] placedAt = new int[3];
        for (int i = 0; i < 3; i++)
        {
            exe.x = -5000 + i * 1000;   // a different spot each time
            placedAt[i] = exe.x;
            Press(sim, Buttons.Special, 0, 0);
            UntilFree(sim, exe);
            Run(sim, None, None, BaseFighter.SpecialCooldown + 2);
        }

        Assert.AreEqual(Executioner.MaxTraps, sim.CountHitboxes(0, Executioner.TrapHit));
        for (int h = 0; h < sim.hitboxes.Count; h++)
        {
            Assert.AreNotEqual(placedAt[0], sim.hitboxes[h].originX, "the first trap was replaced");
        }
    }

    [Test]
    public void ExecutionerTrapSlowsBleedsAndIgnoresJumpers()
    {
        Assert.IsTrue(Executioner.TrapHit.groundOnly);
        Assert.IsTrue(Executioner.TrapHit.unblockable);
        Assert.IsTrue(Executioner.TrapHit.untilHit);

        GameSim sim = NewMatch(Executioner.Id, Sandbag.Id, 8000);
        Fighter victim = sim.fighters[1];
        HitboxInstance trap = sim.SpawnHitboxAt(0, Executioner.TrapHit, victim.x, 1);
        trap.age = Executioner.TrapHit.armTicks;   // already armed
        sim.Step(None, None);

        Assert.AreEqual(97, victim.hp);
        Assert.Greater(victim.slowTicks, 0);
        Assert.AreEqual(1, victim.bleedStacks);
    }

    // Lands `count` whips on a Sandbag, putting it back in reach before each one.
    static GameSim WhipHits(int count)
    {
        GameSim sim = NewMatch(Executioner.Id, Sandbag.Id, 1100);
        for (int i = 0; i < count; i++)
        {
            sim.fighters[1].x = sim.fighters[0].x + 1100;
            Press(sim, Buttons.Light, 0, 30);
        }
        return sim;
    }

    [Test]
    public void ExecutionerWhipHitsStackBleed()
    {
        GameSim sim = WhipHits(2);
        Assert.AreEqual(2, sim.fighters[1].bleedStacks);
    }

    [Test]
    public void ExecutionerEveryThirdHitDismembers()
    {
        Assert.AreEqual(0, WhipHits(2).fighters[1].slowTicks, "not yet");
        Assert.Greater(WhipHits(3).fighters[1].slowTicks, 0, "the third landed hit slows");
    }

    [Test]
    public void ExecutionerGuillotineLocksTheSpotAndCannotBeBlocked()
    {
        // The Brawler stands against the right wall, so holding back blocks without walking away.
        GameSim sim = NewMatch(Executioner.Id, Brawler.Id, 2 * GameSim.ArenaHalf);
        sim.fighters[0].meter = Fighter.MeterMax;
        sim.Step(Buttons.Ultimate, None);
        Run(sim, None, None, GameSim.SuperFlashTicks);

        int victimX = sim.fighters[1].x;
        sim.Step(None, BackP1);
        Assert.IsTrue(Saw(sim, SimEventType.Telegraph));
        Assert.AreEqual(victimX, ((Executioner)sim.fighters[0]).guillotineX, "aimed where they stood");

        Run(sim, None, BackP1, 40);   // holding back (blocking) doesn't help
        Assert.AreEqual(110 - 40, sim.fighters[1].hp);
    }

    [Test]
    public void ExecutionerGuillotineMissesSomeoneWhoMovesAway()
    {
        GameSim sim = NewMatch(Executioner.Id, Brawler.Id, 4000);
        sim.fighters[0].meter = Fighter.MeterMax;
        sim.Step(Buttons.Ultimate, None);
        Run(sim, None, None, GameSim.SuperFlashTicks);

        Run(sim, None, Buttons.Right, 40);   // walk out of the outline
        Assert.AreEqual(110, sim.fighters[1].hp);
    }

    [Test]
    public void ExecutionerGrabPullsFromVeryCloseRange()
    {
        GameSim sim = NewMatch(Executioner.Id, Sandbag.Id, 1100);
        sim.Step(Buttons.Grab, None);
        for (int i = 0; i < 20 && !Saw(sim, SimEventType.Grab); i++) sim.Step(None, None);

        Assert.AreEqual(sim.fighters[0].x + 900, sim.fighters[1].x);
        Assert.AreEqual(85, sim.fighters[1].hp);
        Assert.AreEqual(1, sim.fighters[1].bleedStacks);
    }

    static int WhipReach(int extraHeld)
    {
        GameSim sim = NewMatch(Executioner.Id, Sandbag.Id, 12000);
        Run(sim, Buttons.Heavy, None, 1 + extraHeld);
        for (int i = 0; i < 40; i++)
        {
            sim.Step(None, None);
            for (int h = 0; h < sim.hitboxes.Count; h++) if (sim.hitboxes[h].owner == 0) return sim.hitboxes[h].box.w;
        }
        return -1;
    }

    [Test]
    public void ExecutionerChargedWhipReachesFurther()
    {
        Assert.AreEqual(1800, WhipReach(0));
        Assert.AreEqual(2800, WhipReach(60));
    }

    // ================= Paintress =================

    [Test]
    public void PaintressForwardDashIsInvincibleAndTheBackDashCostsHp()
    {
        GameSim sim = NewMatch(Painter.Id, Sandbag.Id, 8000);
        Fighter p = sim.fighters[0];

        sim.Step(Buttons.Right, None);
        sim.Step(None, None);
        sim.Step(Buttons.Right, None);
        Assert.IsTrue(p.IsDashingForward);
        Assert.IsTrue(p.IsIntangible);
        Assert.AreEqual(75, p.hp);

        UntilFree(sim, p);
        Run(sim, None, None, Fighter.StandardDashCooldown + 2);
        sim.Step(Buttons.Left, None);
        sim.Step(None, None);
        sim.Step(Buttons.Left, None);
        Assert.AreEqual(FighterStateId.Dash, p.state);
        Assert.IsFalse(p.IsIntangible);
        Assert.AreEqual(75 - Painter.BackDashCost, p.hp);
    }

    [Test]
    public void PaintressDashPerksApplyToAirDashesToo()
    {
        GameSim sim = NewMatch(Painter.Id, Sandbag.Id, 8000);
        Fighter p = sim.fighters[0];

        sim.Step(Buttons.Up, None);
        Run(sim, None, None, 6);
        sim.Step(Buttons.Dash | Buttons.Right, None);   // toward the Sandbag
        Assert.IsTrue(p.IsAirborne && p.state == FighterStateId.Dash);
        Assert.IsTrue(p.IsIntangible, "a forward air dash is invincible");
        Assert.AreEqual(75, p.hp);

        while (p.IsAirborne) sim.Step(None, None);
        Run(sim, None, None, Fighter.StandardDashCooldown + 40);
        sim.Step(Buttons.Up, None);
        Run(sim, None, None, 6);
        sim.Step(Buttons.Dash | Buttons.Left, None);    // away
        Assert.IsTrue(p.IsAirborne && p.state == FighterStateId.Dash);
        Assert.AreEqual(75 - Painter.BackDashCost, p.hp, "a back air dash costs HP");
    }

    [Test]
    public void PaintressBackDashCannotKOHer()
    {
        GameSim sim = NewMatch(Painter.Id, Sandbag.Id, 8000);
        sim.fighters[0].hp = 3;
        sim.Step(Buttons.Left, None);
        sim.Step(None, None);
        sim.Step(Buttons.Left, None);
        Assert.AreEqual(1, sim.fighters[0].hp);
    }

    [Test]
    public void PaintressCanvasReflectsAFireball()
    {
        GameSim sim = NewMatch(Shooter.Id, Painter.Id, 6000);
        bool reflected = false;
        for (int t = 0; t < 200; t++)
        {
            int shooter = t == 0 ? Buttons.Heavy : None;    // Shooter's Heavy is a fireball
            int paintress = t == 30 ? Buttons.Special : None;
            sim.Step(shooter, paintress);
            if (Saw(sim, SimEventType.Reflect)) reflected = true;
        }

        Assert.IsTrue(reflected);
        Assert.AreEqual(90 - 10, sim.fighters[0].hp, "the Shooter got its own fireball back");
        Assert.AreEqual(75, sim.fighters[1].hp);
    }

    [Test]
    public void PaintressSplatCannotBeBlockedAndScramblesControls()
    {
        GameSim sim = NewMatch(Painter.Id, Brawler.Id, 3000);
        sim.fighters[0].meter = Fighter.MeterMax;
        sim.Step(Buttons.Ultimate, BackP1);
        Run(sim, None, BackP1, GameSim.SuperFlashTicks + 40);

        Assert.Greater(sim.fighters[1].scrambleTicks, 0);
        Assert.Less(sim.fighters[1].hp, 110, "blocking didn't stop it");
    }

    [Test]
    public void PaintressSplatIsLowEnoughToJump()
    {
        HitboxDef splat = Painter.PaintSplat.hitboxes[0];
        Assert.Less(splat.box.y + splat.box.h, 1000, "a jump (about 2 units high) clears it");
    }

    [Test]
    public void PaintressHitsLeaveUpToThreePuddles()
    {
        GameSim sim = NewMatch(Painter.Id, Sandbag.Id, 1100);
        for (int i = 0; i < 4; i++)
        {
            sim.fighters[1].x = sim.fighters[0].x + 1100;
            Press(sim, Buttons.Light, 0, 25);
        }

        Assert.AreEqual(Painter.MaxPuddles, sim.CountZones(0, Painter.PaintTag));

        Zone puddle = sim.zones[sim.zones.Count - 1];
        sim.fighters[0].x = puddle.x;
        sim.fighters[1].x = puddle.x + 100;
        Assert.AreEqual(115, sim.fighters[0].SpeedPercent(sim));
        Assert.AreEqual(85, sim.fighters[1].SpeedPercent(sim));
    }

    // ================= Dr. Remy =================

    static void Tap(GameSim sim)
    {
        sim.Step(Buttons.Special, None);
        Run(sim, None, None, 3);
    }

    [Test]
    public void RemyTapSwitchesPotions()
    {
        GameSim sim = NewMatch(PlagueDoctor.Id, Sandbag.Id, 8000);
        PlagueDoctor remy = (PlagueDoctor)sim.fighters[0];
        Assert.AreEqual(PlagueDoctor.PotionPoison, remy.selectedPotion);

        Tap(sim);
        Assert.AreEqual(PlagueDoctor.PotionHeal, remy.selectedPotion);
        Tap(sim);
        Assert.AreEqual(PlagueDoctor.PotionSlow, remy.selectedPotion);
        Tap(sim);
        Assert.AreEqual(PlagueDoctor.PotionPoison, remy.selectedPotion);
        Assert.AreEqual(FighterStateId.Idle, remy.state, "taps don't play a move");
    }

    [Test]
    public void RemyHoldDrinksTheHealPotionCappedAtMax()
    {
        GameSim sim = NewMatch(PlagueDoctor.Id, Sandbag.Id, 8000);
        PlagueDoctor remy = (PlagueDoctor)sim.fighters[0];
        remy.hp = 40;
        Tap(sim);   // Poison -> Heal

        Press(sim, Buttons.Special, 30, 40);
        Assert.AreEqual(50, remy.hp);
        Assert.Greater(remy.CooldownLeft(PlagueDoctor.HealPotion), 200, "heal has its own long cooldown");
        Assert.LessOrEqual(remy.CooldownLeft(PlagueDoctor.PoisonPotion), PlagueDoctor.SharedPotionCooldown + 1);
        Assert.Greater(remy.CooldownLeft(PlagueDoctor.PoisonPotion), 0, "the other potions share 30 ticks");

        remy.hp = 58;
        Run(sim, None, None, PlagueDoctor.HealCooldown);
        Press(sim, Buttons.Special, 30, 40);
        Assert.AreEqual(remy.MaxHp, remy.hp, "never above max");
    }

    [Test]
    public void RemyPoisonCloudHurtsButCannotKO()
    {
        GameSim sim = NewMatch(PlagueDoctor.Id, Sandbag.Id, 1200);
        Press(sim, Buttons.Special, 30, 0);   // Poison is selected first
        Assert.AreEqual(1, sim.CountZones(0, PlagueDoctor.PoisonTag));

        Run(sim, None, None, 200);
        Assert.AreEqual(100 - 9, sim.fighters[1].hp, "1 damage every 20 ticks for 180 ticks");

        sim.fighters[1].hp = 2;
        Run(sim, None, None, PlagueDoctor.SharedPotionCooldown);
        Press(sim, Buttons.Special, 30, 200);
        Assert.AreEqual(1, sim.fighters[1].hp);
    }

    [Test]
    public void RemyLeechGrabDrainsAndHealsHim()
    {
        GameSim sim = NewMatch(PlagueDoctor.Id, Sandbag.Id, 1100);
        PlagueDoctor remy = (PlagueDoctor)sim.fighters[0];
        remy.hp = 30;

        Press(sim, Buttons.Grab, 0, PlagueDoctor.LeechTicks + 20);

        Assert.AreEqual(100 - 6 - 5, sim.fighters[1].hp, "the grab, then 5 leech ticks");
        Assert.AreEqual(35, remy.hp);
    }

    [Test]
    public void RemySlowPotionSlows()
    {
        GameSim sim = NewMatch(PlagueDoctor.Id, Sandbag.Id, 3000);
        Tap(sim);
        Tap(sim);   // -> Slow
        Press(sim, Buttons.Special, 30, 40);
        Assert.Greater(sim.fighters[1].slowTicks, 0);
    }

    [Test]
    public void RemyRatShowerWaitsAndCannotBeBlocked()
    {
        MoveDef m = PlagueDoctor.UnleashTheRats;
        for (int i = 0; i < m.hitboxes.Length; i++)
        {
            Assert.GreaterOrEqual(m.hitboxes[i].spawnTick, PlagueDoctor.UltDelay, "no rat before the delay");
            Assert.IsTrue(m.hitboxes[i].unblockable);
            StringAssert.StartsWith("rat_shower_", m.hitboxes[i].visual);
        }

        GameSim sim = NewMatch(PlagueDoctor.Id, Brawler.Id, 3000);
        sim.fighters[0].meter = Fighter.MeterMax;
        sim.Step(Buttons.Ultimate, BackP1);
        Run(sim, None, BackP1, GameSim.SuperFlashTicks + PlagueDoctor.UltDelay);
        Assert.AreEqual(110, sim.fighters[1].hp, "nothing hits during the delay");
        Run(sim, None, BackP1, 250);
        Assert.Less(sim.fighters[1].hp, 110, "the shower lands even through a block");
    }

    [Test]
    public void RemyUltimateCannotBeInterrupted()
    {
        GameSim sim = NewMatch(PlagueDoctor.Id, Brawler.Id, 800);
        sim.fighters[0].meter = Fighter.MeterMax;
        sim.Step(Buttons.Ultimate, None);
        Run(sim, None, None, GameSim.SuperFlashTicks + 5);
        Assert.AreEqual(FighterStateId.Attack, sim.fighters[0].state);
        int before = sim.fighters[0].hp;
        Run(sim, None, Buttons.Light, 30);
        Assert.AreEqual(before, sim.fighters[0].hp, "no damage during the Ultimate");
        Assert.AreEqual(FighterStateId.Attack, sim.fighters[0].state, "still in the move");
    }

    [Test]
    public void RemyRatShowerIsScatteredNotIdentical()
    {
        MoveDef m = PlagueDoctor.UnleashTheRats;
        bool speedVaries = false, artVaries = false;
        for (int i = 1; i < m.hitboxes.Length; i++)
        {
            if (m.hitboxes[i].vx != m.hitboxes[0].vx) speedVaries = true;
            if (m.hitboxes[i].visual != m.hitboxes[0].visual) artVaries = true;
        }
        Assert.IsTrue(speedVaries);
        Assert.IsTrue(artVaries);
    }

    [Test]
    public void RemyCanThrowAnotherRatBeforeTheLastLands()
    {
        GameSim sim = NewMatch(PlagueDoctor.Id, Brawler.Id, 15000);
        int most = 0;
        for (int i = 0; i < 200; i++)
        {
            sim.Step(Buttons.Heavy, None);
            int rats = 0;
            for (int h = 0; h < sim.hitboxes.Count; h++) if (sim.hitboxes[h].owner == 0 && sim.hitboxes[h].IsProjectile) rats++;
            most = System.Math.Max(most, rats);
        }
        Assert.GreaterOrEqual(most, 2);
    }
}
