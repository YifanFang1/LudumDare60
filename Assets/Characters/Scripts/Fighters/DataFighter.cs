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
        if (!HasAirMoves())
        {
            moves.Add(BaseFighter.StandardJumpLight);
            moves.Add(BaseFighter.StandardJumpHeavy);
        }
        return moves.ToArray();
    }

    public override MoveDef ChooseMove(GameSim sim, int input)
    {
        return Pick(input, false);
    }

    // Uses the character's JumpLight / JumpHeavy bindings. A character with none at all gets the
    // standard air attacks, so older assets can attack in the air without being edited.
    public override MoveDef ChooseAirMove(GameSim sim, int input)
    {
        if (!HasAirMoves())
        {
            if ((input & Buttons.Heavy) != 0) return BaseFighter.StandardJumpHeavy;
            if ((input & Buttons.Light) != 0) return BaseFighter.StandardJumpLight;
            return null;
        }
        return Pick(input, true);
    }

    // First binding (in list order) for a held button, from the ground list or the air list.
    MoveDef Pick(int input, bool air)
    {
        if (def.moves == null) return null;
        for (int i = 0; i < def.moves.Length; i++)
        {
            MoveBinding b = def.moves[i];
            if (b.move == null || CharacterDef.IsAirButton(b.button) != air) continue;
            if ((input & CharacterDef.ButtonMask(b.button)) == 0) continue;
            if (b.move.ultimate && meter < MeterMax) continue;   // not ready: try the next binding
            return b.move;
        }
        return null;
    }

    bool HasAirMoves()
    {
        if (def.moves == null) return false;
        for (int i = 0; i < def.moves.Length; i++)
        {
            if (def.moves[i].move != null && CharacterDef.IsAirButton(def.moves[i].button)) return true;
        }
        return false;
    }
}
