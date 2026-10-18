// SUMMONS: simple copies of a fighter that mimic what it does (Room's "Address Me" Ultimate).
//
// A copy is NOT a third fighter: it has no input, no health bar and no state machine of its own.
// Every tick GameSim copies the owner's walking and the moves the owner allows
// (Fighter.SummonCopiesMove) onto it, from the copy's own spot. Its hits are placed hitboxes
// owned by the owner (so the opponent can't tell them apart from the owner's own hits), and any
// enemy hit that touches a copy destroys it. Copies don't push anyone.
//
// Plain data, ints only, part of the desync hash.
public class Summon
{
    public int owner;          // fighter index (0 or 1)
    public int x;
    public int facing;
    public int ticksLeft;
    public MoveDef move;       // the move it's copying right now, or null
    public int moveTick;
}
