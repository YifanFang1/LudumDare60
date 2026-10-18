// How a jump moves: it leaves the ground at `up` milli-units per tick (Fighter.Gravity takes some
// of that away every tick) and drifts `forward` milli-units per tick in the direction held when
// it started. A character changes its jump by overriding Fighter.JumpArc. Plain data, like DashDef.
public struct JumpDef
{
    public int up;
    public int forward;

    public JumpDef(int up, int forward)
    {
        this.up = up;
        this.forward = forward;
    }
}
