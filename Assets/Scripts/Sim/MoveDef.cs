// A move is plain data: how long it lasts and which hitboxes it "summons" on which tick.
// These classes are [Serializable] so a ScriptableObject outside this folder (CharacterData) can
// show them in the Unity inspector. The Sim folder itself stays free of UnityEngine.

[System.Serializable]
public class HitboxDef
{
    public int spawnTick;            // tick of the move (0 = first tick of the move) when it appears
    public int lifetime;             // how many ticks it stays alive
    public Box box;                  // local box, see Box.cs
    public int damage;
    public int hitstun;              // ticks the victim is stunned
    public int knockback;            // milli-units per tick the victim is pushed away
    public bool followOwner = true;  // true = melee, moves with the fighter. false = projectile
    public int vx;                   // forward speed in milli-units per tick (projectiles only)
}

[System.Serializable]
public class MoveDef
{
    public string name;              // also the Animator state name the view plays for this move
    public int totalTicks;           // the move ends (back to idle) after this many ticks
    public HitboxDef[] hitboxes;
}
