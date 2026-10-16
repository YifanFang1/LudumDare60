// A character built from a CharacterDef (edited in the Unity inspector). Behaves exactly like a
// hand-written character such as Brawler. For special mechanics, write a BaseFighter subclass in
// code instead (see Characters/CHARACTERS.md); for normal characters the inspector is enough.
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

    public override MoveDef[] ListMoves()
    {
        if (def.moves == null) return new MoveDef[0];
        System.Collections.Generic.List<MoveDef> moves = new System.Collections.Generic.List<MoveDef>();
        for (int i = 0; i < def.moves.Length; i++)
        {
            if (def.moves[i].move != null) moves.Add(def.moves[i].move);
        }
        return moves.ToArray();
    }

    public override MoveDef ChooseMove(GameSim sim, int input)
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
