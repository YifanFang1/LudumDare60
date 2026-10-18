// A hitbox that currently exists in the match. Fighters summon these with GameSim.SpawnHitbox.
//
// The def (HitboxDef) is SHARED by both players and every round, so it is never changed. Anything
// a character wants to change about ONE hit (a charged fireball's damage, a longer whip, a damage
// bonus) is changed on the copies below instead (D-25): `damage`, `hitstun`, `knockback`, `box`.
public class HitboxInstance
{
    public int owner;          // fighter index (0 or 1) that summoned it
    public HitboxDef def;
    public int originX;        // where it is, when it doesn't follow its owner (projectiles, placed hitboxes)
    public int originY;
    public int facing;
    public int lifetime;       // ticks left
    public int age;            // ticks it has existed (for HitboxDef.armTicks)
    public bool hasHit;
    public bool blocked;       // set when it connects: true if the victim was blocking
    public bool fromUltimate;  // spawned by an Ultimate move: its hits give no meter
    // Placed at originX instead of following its owner, even if def.followOwner is true (an
    // Ultimate copy's punch, see Summon). Being hit doesn't remove placed hitboxes.
    public bool placed;
    public int hitX;           // COSMETIC: where it connected (for sparks), set when it hits
    public int hitY;

    // Per-hit copies of the def's numbers, filled in at spawn. Change these, never the def.
    public int damage;
    public int hitstun;
    public int knockback;
    public Box box;

    // Follows its owner (melee) unless it's a projectile or was placed somewhere.
    public bool FollowsOwner { get { return def.followOwner && !placed; } }

    // A projectile: something flying across the screen. Traps and other placed hitboxes don't move,
    // so they are not projectiles (they can't be reflected or destroyed).
    public bool IsProjectile { get { return !def.followOwner && def.vx != 0; } }

    public Box World(GameSim sim)
    {
        if (FollowsOwner)
        {
            Fighter o = sim.fighters[owner];
            return box.ToWorld(o.x, o.y, o.facing);
        }
        return box.ToWorld(originX, originY, facing);
    }
}
