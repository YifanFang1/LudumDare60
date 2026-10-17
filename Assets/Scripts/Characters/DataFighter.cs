// A character built from a CharacterDef (edited in the Unity inspector). Behaves exactly like a
// hand-written Fighter subclass such as Brawler. For special mechanics, write a subclass in code
// instead (see Brawler.cs); for normal characters the inspector is enough.
public class DataFighter : Fighter
{
    readonly CharacterDef def;

    public DataFighter(CharacterDef def)
    {
        this.def = def;
    }

    public override int MaxHp { get { return def.maxHp; } }
    public override int WalkSpeed { get { return def.walkSpeed; } }
    public override Box Hurtbox { get { return def.hurtbox; } }

    public override MoveDef ChooseMove(int input)
    {
        if (def.moves == null) return null;
        for (int i = 0; i < def.moves.Length; i++)
        {
            MoveBinding b = def.moves[i];
            if (b.move != null && (input & CharacterDef.ButtonMask(b.button)) != 0) return b.move;
        }
        return null;
    }
}
