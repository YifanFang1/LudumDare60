using System;

// Which button starts a move. (Add more here when you add buttons in Sim/Buttons.cs.)
public enum AttackButton { Light, Heavy }

[Serializable]
public class MoveBinding
{
    public AttackButton button;
    public MoveDef move;
}

// A whole character as plain data. This is what the Unity inspector edits (inside CharacterData)
// and what DataFighter plays. No UnityEngine here, so it is safe for the deterministic sim.
[Serializable]
public class CharacterDef
{
    public int id;                           // unique and permanent, generated automatically
    public string displayName = "New Character";
    public int maxHp = 100;
    public int walkSpeed = 60;               // milli-units per tick
    public Box hurtbox = new Box(-500, 0, 1000, 2000);   // body, see Box.cs
    public MoveBinding[] moves;              // checked in order: first one whose button is held wins

    public static int ButtonMask(AttackButton b)
    {
        return b == AttackButton.Heavy ? Buttons.Heavy : Buttons.Light;
    }

    // A new character starts with a basic Light and Heavy punch so it is playable immediately.
    public static CharacterDef CreateDefault()
    {
        CharacterDef d = new CharacterDef();
        d.moves = new MoveBinding[]
        {
            new MoveBinding
            {
                button = AttackButton.Heavy,
                move = new MoveDef
                {
                    name = "Heavy", totalTicks = 28,
                    hitboxes = new HitboxDef[]
                    {
                        new HitboxDef { spawnTick = 10, lifetime = 4, box = new Box(300, 700, 1300, 700),
                                        damage = 12, hitstun = 20, knockback = 80 }
                    }
                }
            },
            new MoveBinding
            {
                button = AttackButton.Light,
                move = new MoveDef
                {
                    name = "Light", totalTicks = 15,
                    hitboxes = new HitboxDef[]
                    {
                        new HitboxDef { spawnTick = 4, lifetime = 3, box = new Box(400, 800, 1000, 400),
                                        damage = 5, hitstun = 12, knockback = 40 }
                    }
                }
            },
        };
        return d;
    }

    // Fingerprint of everything that affects the simulation (names are cosmetic and left out).
    // Two players whose characters differ get different values.
    public int Hash()
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + id;
            h = h * 31 + maxHp;
            h = h * 31 + walkSpeed;
            h = MixBox(h, hurtbox);
            if (moves == null) return h;
            for (int i = 0; i < moves.Length; i++)
            {
                MoveBinding b = moves[i];
                h = h * 31 + (int)b.button;
                if (b.move == null) continue;
                h = h * 31 + b.move.totalTicks;
                if (b.move.hitboxes == null) continue;
                for (int j = 0; j < b.move.hitboxes.Length; j++)
                {
                    HitboxDef hb = b.move.hitboxes[j];
                    h = h * 31 + hb.spawnTick;
                    h = h * 31 + hb.lifetime;
                    h = MixBox(h, hb.box);
                    h = h * 31 + hb.damage;
                    h = h * 31 + hb.hitstun;
                    h = h * 31 + hb.knockback;
                    h = h * 31 + (hb.followOwner ? 1 : 0);
                    h = h * 31 + hb.vx;
                }
            }
            return h;
        }
    }

    static int MixBox(int h, Box b)
    {
        unchecked
        {
            h = h * 31 + b.x;
            h = h * 31 + b.y;
            h = h * 31 + b.w;
            h = h * 31 + b.h;
            return h;
        }
    }
}
