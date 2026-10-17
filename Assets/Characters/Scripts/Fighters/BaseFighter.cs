// THE CHARACTER TEMPLATE. Every character written in code extends this class.
//
// It gives a new character sensible defaults for everything, so a subclass only writes what
// makes it different (see Brawler.cs and Shooter.cs, about 30 lines each):
//
//   Trait          Default                         Override to change it
//   ------------   -----------------------------   ---------------------------------------
//   Health         100                             MaxHp
//   Walk speed     60 milli-units per tick         WalkSpeed
//   Body           1 unit wide, 2 units tall       Hurtbox
//   Light (J)      none                            LightAttack  (return a MoveDef)
//   Heavy (K)      none                            HeavyAttack
//   Move rules     any move can always be used     CanUseMove   (e.g. "one fireball at a time")
//
// Everything every fighter shares (walking, blocking by holding back, hitstun, KO) is already
// done by Fighter in the Sim folder, so characters get it for free.
//
// Building moves: use the Melee(...) and Projectile(...) helpers below with NAMED arguments, so
// every number says what it is. Store moves in `static readonly` fields and never change them at
// runtime: both players share the same MoveDef objects.
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

    // The move each attack button starts. null = that button does nothing for this character.
    protected virtual MoveDef LightAttack { get { return null; } }
    protected virtual MoveDef HeavyAttack { get { return null; } }

    // Extra rules for when a move is allowed (resources, cooldowns, projectile limits).
    // `sim` is read-only here: look, don't change.
    protected virtual bool CanUseMove(GameSim sim, MoveDef move)
    {
        return true;
    }

    // ---------- the standard control scheme ----------
    // Heavy wins if both buttons are pressed on the same tick. Override ChooseMove itself only if
    // your character needs different controls (e.g. Down + Light for a low attack).
    public override MoveDef ChooseMove(GameSim sim, int input)
    {
        if ((input & Buttons.Heavy) != 0 && IsUsable(sim, HeavyAttack)) return HeavyAttack;
        if ((input & Buttons.Light) != 0 && IsUsable(sim, LightAttack)) return LightAttack;
        return null;
    }

    public override MoveDef[] ListMoves()
    {
        System.Collections.Generic.List<MoveDef> moves = new System.Collections.Generic.List<MoveDef>();
        if (LightAttack != null) moves.Add(LightAttack);
        if (HeavyAttack != null) moves.Add(HeavyAttack);
        return moves.ToArray();
    }

    bool IsUsable(GameSim sim, MoveDef move)
    {
        return move != null && CanUseMove(sim, move);
    }

    // ---------- helpers for writing moves ----------
    // Positions and sizes are milli-units (1000 = 1 Unity unit); times are ticks (60 = 1 second).
    // `box` is relative to the fighter, see Sim/Box.cs (x = forward from the fighter's centre).

    // A punch, kick or swing: one hitbox that moves with the fighter.
    protected static MoveDef Melee(string name, int totalTicks, int spawnTick, int activeTicks, Box box,
                                   int damage, int hitstun, int knockback)
    {
        return new MoveDef
        {
            name = name,
            totalTicks = totalTicks,
            hitboxes = new HitboxDef[]
            {
                new HitboxDef { spawnTick = spawnTick, lifetime = activeTicks, box = box,
                                damage = damage, hitstun = hitstun, knockback = knockback,
                                followOwner = true }
            }
        };
    }

    // A shot that leaves the fighter and flies forward at `speed` milli-units per tick until it
    // hits something or its `lifetime` runs out. The fighter can act again after `totalTicks`.
    protected static MoveDef Projectile(string name, int totalTicks, int spawnTick, int lifetime, Box box,
                                        int speed, int damage, int hitstun, int knockback)
    {
        return new MoveDef
        {
            name = name,
            totalTicks = totalTicks,
            hitboxes = new HitboxDef[]
            {
                new HitboxDef { spawnTick = spawnTick, lifetime = lifetime, box = box,
                                damage = damage, hitstun = hitstun, knockback = knockback,
                                followOwner = false, vx = speed }
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
            if (h.owner == index && !h.def.followOwner) count++;
        }
        return count;
    }
}
