// A sample character. Copy this file to make a new one: change the numbers, add moves,
// override Hurtbox, or use the hooks from Fighter for special mechanics. Then add one line to
// CharacterList.cs.
//
// This folder (LD.Characters) can only see LD.Sim. It cannot see networking, input, drawing or
// audio, so a character can never break when those change. Write moves as data and let the sim
// run them; put visuals/sounds in the view layer by listening to sim events.
public class Brawler : Fighter
{
    static readonly MoveDef Light = new MoveDef
    {
        name = "Light",
        totalTicks = 15,
        hitboxes = new HitboxDef[]
        {
            new HitboxDef { spawnTick = 4, lifetime = 3, box = new Box(400, 800, 1000, 400),
                            damage = 5, hitstun = 12, knockback = 40 }
        }
    };

    static readonly MoveDef Heavy = new MoveDef
    {
        name = "Heavy",
        totalTicks = 28,
        hitboxes = new HitboxDef[]
        {
            new HitboxDef { spawnTick = 10, lifetime = 4, box = new Box(300, 700, 1300, 700),
                            damage = 12, hitstun = 20, knockback = 80 }
        }
    };

    public override int MaxHp { get { return 100; } }
    public override int WalkSpeed { get { return 60; } }

    public override MoveDef ChooseMove(int input)
    {
        if ((input & Buttons.Heavy) != 0) return Heavy;
        if ((input & Buttons.Light) != 0) return Light;
        return null;
    }
}
