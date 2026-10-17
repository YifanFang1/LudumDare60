// How a dash moves: `speed` milli-units per tick for `ticks` ticks. A character changes its dashes
// by overriding Fighter.ForwardDash / Fighter.BackDash. Plain data, like MoveDef.
public struct DashDef
{
    public int ticks;
    public int speed;

    public DashDef(int ticks, int speed)
    {
        this.ticks = ticks;
        this.speed = speed;
    }
}
