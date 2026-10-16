// A hitbox that currently exists in the match. Fighters summon these with GameSim.SpawnHitbox.
public class HitboxInstance
{
    public int owner;          // fighter index (0 or 1) that summoned it
    public HitboxDef def;
    public int originX;        // only used when def.followOwner is false
    public int originY;
    public int facing;
    public int lifetime;       // ticks left
    public bool hasHit;

    public Box World(GameSim sim)
    {
        if (def.followOwner)
        {
            Fighter o = sim.fighters[owner];
            return def.box.ToWorld(o.x, o.y, o.facing);
        }
        return def.box.ToWorld(originX, originY, facing);
    }
}
