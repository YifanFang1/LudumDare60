// THE CHARACTER TEMPLATE. Every character written in code extends this class.
//
// It gives a new character sensible defaults for everything, so a subclass only writes what
// makes it different (see Tests/EditMode/Brawler.cs and Shooter.cs, about 30 lines each). The defaults are the
// STANDARD MOVESET from Design/SHARED_KIT.md, so a character that overrides nothing is already
// fully playable (the six roster placeholders, e.g. Samurai.cs, are exactly that):
//
//   Trait          Default                              Override to change it
//   ------------   ----------------------------------   ---------------------------------------
//   Health         100                                  MaxHp
//   Walk speed     60 milli-units per tick              WalkSpeed
//   Body           1 unit wide, 2 units tall            Hurtbox
//   Light (J)      StandardLight: fast jab              LightAttack  (return a MoveDef, or null)
//   Heavy (K)      StandardHeavy: slow, long reach      HeavyAttack
//   Grab (L)       StandardGrab: unblockable, short     GrabMove
//   Special (I)    StandardSpecial: a projectile        SpecialMove
//   Ultimate (O)   StandardUltimate: big strike         UltimateMove (needs a full meter)
//   Jump + J       StandardJumpLight: quick air kick    JumpLightAttack (one air attack per jump)
//   Jump + K       StandardJumpHeavy: big jump-in       JumpHeavyAttack
//   Dashes         about 1.8 units                      ForwardDash / BackDash (in Fighter)
//   Jump (W)       about 2.1 units high, a little       JumpArc / AirControl (in Fighter)
//                  steering in the air
//   Move rules     one Special projectile at a time     CanUseMove   (e.g. "one fireball at a time")
//   Cooldowns      Light 5, Heavy 10, Grab 15,          the move's `cooldown` (D-43), or
//                  Special 30 ticks after the move      ModifyCooldown for a rule
//                  ends; dashes 20 (Fighter)
//
// Everything every fighter shares (walking, blocking by holding back, dashing, hitstun, KO, the
// meter) is already done by Fighter in the Sim folder, so characters get it for free.
//
// Building moves: use the Melee(...), Grab(...), Projectile(...) and Ultimate(...) helpers below
// with NAMED arguments, so every number says what it is. For the extra rules (charging, holding,
// traps...) set the MoveDef / HitboxDef fields after building the move (see Sim/MoveDef.cs). Store moves in `static readonly` fields
// and never change them at runtime: both players share the same MoveDef objects.
//
// RULES: this folder is part of the deterministic sim. Ints only, no UnityEngine, no Random,
// no Time. Any extra state you add (meters, cooldowns) must go into HashExtra().
//
// Full guide: Characters/CHARACTERS.md
public abstract class BaseFighter : Fighter
{
    // ---------- traits (override the ones your character changes) ----------
    public override int MaxHp { get { return 100; } }
    public override int WalkSpeed { get { return 60; } }
    public override Box Hurtbox { get { return new Box(-500, 0, 1000, 2000); } }

    // The move each button starts. null = that button does nothing for this character.
    protected virtual MoveDef LightAttack { get { return StandardLight; } }
    protected virtual MoveDef HeavyAttack { get { return StandardHeavy; } }
    protected virtual MoveDef GrabMove { get { return StandardGrab; } }
    protected virtual MoveDef SpecialMove { get { return StandardSpecial; } }
    protected virtual MoveDef UltimateMove { get { return StandardUltimate; } }
    protected virtual MoveDef JumpLightAttack { get { return StandardJumpLight; } }
    protected virtual MoveDef JumpHeavyAttack { get { return StandardJumpHeavy; } }

    // ---------- the standard moveset (Design/SHARED_KIT.md) ----------
    // Every character gets these unless it overrides the slot. Public so tests can check them.
    // The names are the slot names, because a move's name is also its Animator state.

    // Standard cooldowns (D-43): ticks after a move ends before the SAME move can start again.
    // Air attacks have none (they're once per jump anyway), and an Ultimate's limit is the meter.
    public const int LightCooldown = 5;
    public const int HeavyCooldown = 10;
    public const int GrabCooldown = 15;
    public const int SpecialCooldown = 30;

    // Fast and safe.
    public static readonly MoveDef StandardLight = Melee("Light",
        totalTicks: 15, spawnTick: 4, activeTicks: 3,
        box: new Box(400, 900, 900, 400),
        damage: 5, hitstun: 12, knockback: 40, cooldown: LightCooldown);

    // Long reach, big damage, punishable if it whiffs or is blocked.
    public static readonly MoveDef StandardHeavy = Melee("Heavy",
        totalTicks: 28, spawnTick: 10, activeTicks: 4,
        box: new Box(300, 700, 1300, 700),
        damage: 12, hitstun: 20, knockback: 80, cooldown: HeavyCooldown);

    // Unblockable, short reach, long whiff recovery (33 ticks). Hold back when it lands to throw
    // the opponent behind you.
    public static readonly MoveDef StandardGrab = HoldGrab(Grab("Grab",
        totalTicks: 40, spawnTick: 5, activeTicks: 2,
        box: new Box(300, 600, 700, 1000),
        damage: 12, hitstun: 40, knockback: 90, cooldown: GrabCooldown));

    // A projectile that crosses the arena in about 2.5 s. Only one on screen at a time (CanUseMove).
    public static readonly MoveDef StandardSpecial = Projectile("Special",
        totalTicks: 30, spawnTick: 12, lifetime: 150,
        box: new Box(400, 900, 500, 400),
        speed: 100, damage: 10, hitstun: 18, knockback: 60,
        visual: "special", cooldown: SpecialCooldown);

    // A big blockable strike after the superflash. Needs a full meter; gives no meter back.
    // If it's blocked, its long recovery is the risk.
    public static readonly MoveDef StandardUltimate = Ultimate("Ultimate",
        totalTicks: 45, spawnTick: 6, activeTicks: 4,
        box: new Box(300, 0, 2000, 2200),
        damage: 30, hitstun: 40, knockback: 120);

    // Air attacks. The boxes sit low and forward (box y = 0 is the jumper's feet), so they reach
    // down onto a standing opponent's head and body. Landing cancels them, so they're short.
    public static readonly MoveDef StandardJumpLight = Melee("JumpLight",
        totalTicks: 14, spawnTick: 3, activeTicks: 8,
        box: new Box(200, 0, 800, 700),
        damage: 5, hitstun: 14, knockback: 30);

    public static readonly MoveDef StandardJumpHeavy = Melee("JumpHeavy",
        totalTicks: 24, spawnTick: 6, activeTicks: 6,
        box: new Box(100, 0, 1000, 900),
        damage: 10, hitstun: 20, knockback: 60);

    // Extra rules for when a move is allowed (resources, cooldowns, projectile limits).
    // `sim` is read-only here: look, don't change. If you override this, call
    // base.CanUseMove(sim, move) for the moves you don't handle yourself.
    protected virtual bool CanUseMove(GameSim sim, MoveDef move)
    {
        if (move == StandardSpecial) return ProjectilesOnScreen(sim) == 0;
        return true;
    }

    // ---------- the standard control scheme ----------
    // If several buttons are pressed on the same tick: Ultimate > Special > Grab > Heavy > Light.
    // Holding a button repeats its move, except the Ultimate, which needs a FRESH press so holding
    // O can't fire it by accident the moment the bar fills. Override ChooseMove itself only if
    // your character needs different controls (e.g. Down + Light for a low attack).
    public override MoveDef ChooseMove(GameSim sim, int input)
    {
        if ((pressedInput & Buttons.Ultimate) != 0 && meter >= MeterMax && IsUsable(sim, UltimateMove)) return UltimateMove;
        if ((input & Buttons.Special) != 0 && IsUsable(sim, SpecialMove)) return SpecialMove;
        if ((input & Buttons.Grab) != 0 && IsUsable(sim, GrabMove)) return GrabMove;
        if ((input & Buttons.Heavy) != 0 && IsUsable(sim, HeavyAttack)) return HeavyAttack;
        if ((input & Buttons.Light) != 0 && IsUsable(sim, LightAttack)) return LightAttack;
        return null;
    }

    // In the air: Heavy > Light, once per jump (Fighter takes care of the "once").
    public override MoveDef ChooseAirMove(GameSim sim, int input)
    {
        if ((input & Buttons.Heavy) != 0 && IsUsable(sim, JumpHeavyAttack)) return JumpHeavyAttack;
        if ((input & Buttons.Light) != 0 && IsUsable(sim, JumpLightAttack)) return JumpLightAttack;
        return null;
    }

    // Every move this character can do, for editor tools (one Animator state per move).
    public override MoveDef[] ListMoves()
    {
        System.Collections.Generic.List<MoveDef> moves = new System.Collections.Generic.List<MoveDef>();
        if (LightAttack != null) moves.Add(LightAttack);
        if (HeavyAttack != null) moves.Add(HeavyAttack);
        if (GrabMove != null) moves.Add(GrabMove);
        if (SpecialMove != null) moves.Add(SpecialMove);
        if (UltimateMove != null) moves.Add(UltimateMove);
        if (JumpLightAttack != null) moves.Add(JumpLightAttack);
        if (JumpHeavyAttack != null) moves.Add(JumpHeavyAttack);
        return moves.ToArray();
    }

    // Exists, isn't cooling down, and the character's own rules allow it.
    protected bool IsUsable(GameSim sim, MoveDef move)
    {
        return move != null && !IsOnCooldown(move) && CanUseMove(sim, move);
    }

    // ---------- helpers for writing moves ----------
    // Positions and sizes are milli-units (1000 = 1 Unity unit); times are ticks (60 = 1 second).
    // `box` is relative to the fighter, see Sim/Box.cs (x = forward from the fighter's centre).

    // A punch, kick or swing: one hitbox that moves with the fighter.
    protected static MoveDef Melee(string name, int totalTicks, int spawnTick, int activeTicks, Box box,
                                   int damage, int hitstun, int knockback, int cooldown = 0)
    {
        return new MoveDef
        {
            name = name,
            totalTicks = totalTicks,
            cooldown = cooldown,
            hitboxes = new HitboxDef[]
            {
                new HitboxDef { spawnTick = spawnTick, lifetime = activeTicks, box = box,
                                damage = damage, hitstun = hitstun, knockback = knockback,
                                followOwner = true }
            }
        };
    }

    // A grab: like Melee, but it ignores blocking and can't catch someone in hitstun/blockstun
    // (see HitboxDef.grab). Keep the box short: grabs only work up close.
    protected static MoveDef Grab(string name, int totalTicks, int spawnTick, int activeTicks, Box box,
                                  int damage, int hitstun, int knockback, int cooldown = 0)
    {
        MoveDef move = Melee(name, totalTicks, spawnTick, activeTicks, box, damage, hitstun, knockback, cooldown);
        move.hitboxes[0].grab = true;
        return move;
    }

    // Every grab except the Elephant's: the grab pins the victim body to body in front of the
    // grabber (GrabHoldDistance = the two bodies just touching), holds them GrabHoldTicks, then
    // the grab's damage lands and the victim is knocked back only a little (GrabThrowKnockback).
    // It uses the held-grab machinery (HitboxDef.holdTicks), so a hit on the grabber breaks it.
    public const int GrabHoldTicks = 20;
    public const int GrabHoldDistance = 1000;
    public const int GrabThrowKnockback = 30;

    protected static MoveDef HoldGrab(MoveDef move)
    {
        HitboxDef h = move.hitboxes[0];
        h.holdTicks = GrabHoldTicks;
        h.pullTo = GrabHoldDistance;
        h.knockback = System.Math.Min(h.knockback, GrabThrowKnockback);
        return move;
    }

    // An Ultimate: like Melee, but it needs a full meter, spends it, and starts with the
    // superflash freeze (see MoveDef.ultimate). The startup counts from the end of the freeze.
    protected static MoveDef Ultimate(string name, int totalTicks, int spawnTick, int activeTicks, Box box,
                                      int damage, int hitstun, int knockback)
    {
        MoveDef move = Melee(name, totalTicks, spawnTick, activeTicks, box, damage, hitstun, knockback);
        move.ultimate = true;
        return move;
    }

    // A shot that leaves the fighter and flies forward at `speed` milli-units per tick until it
    // hits something or its `lifetime` runs out. The fighter can act again after `totalTicks`.
    // `visual` names the sprite the view draws for it (set in the character's art asset).
    protected static MoveDef Projectile(string name, int totalTicks, int spawnTick, int lifetime, Box box,
                                        int speed, int damage, int hitstun, int knockback, string visual = null,
                                        int cooldown = 0)
    {
        return new MoveDef
        {
            name = name,
            totalTicks = totalTicks,
            cooldown = cooldown,
            hitboxes = new HitboxDef[]
            {
                new HitboxDef { spawnTick = spawnTick, lifetime = lifetime, box = box,
                                damage = damage, hitstun = hitstun, knockback = knockback,
                                followOwner = false, vx = speed, visual = visual }
            }
        };
    }

    // How many of this fighter's projectiles are flying right now. The hitboxes are part of the
    // sim state, so using this in CanUseMove needs no extra HashExtra().
    protected int ProjectilesOnScreen(GameSim sim)
    {
        int count = 0;
        for (int i = 0; i < sim.hitboxes.Count; i++)
        {
            HitboxInstance h = sim.hitboxes[i];
            if (h.owner == index && h.IsProjectile) count++;
        }
        return count;
    }

    // A move with no hitboxes (a pose, a buff, a potion you drink). Its effect comes from the move
    // hooks (OnMoveStart / OnMoveTick).
    protected static MoveDef Pose(string name, int totalTicks, int cooldown = 0)
    {
        return new MoveDef { name = name, totalTicks = totalTicks, cooldown = cooldown, hitboxes = new HitboxDef[0] };
    }

    // The opponent (read only, like the rest of the sim in ChooseMove / CanUseMove).
    protected Fighter Opponent(GameSim sim)
    {
        return sim.fighters[1 - index];
    }

    // Missing health as a percent of max (0 at full health, near 100 near death). Several perks
    // ("more damage taken = ...") scale with it.
    protected int MissingHpPercent
    {
        get { return (MaxHp - hp) * 100 / MaxHp; }
    }

    // True while this fighter is doing exactly `m`.
    protected bool IsDoing(MoveDef m)
    {
        return state == FighterStateId.Attack && move == m;
    }
}
