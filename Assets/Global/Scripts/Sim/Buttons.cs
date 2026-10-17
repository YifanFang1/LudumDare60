// One player's input for one tick is a single int. Each button is one bit.
// New buttons go at the END with the next free bit, so the old values never change.
public static class Buttons
{
    public const int Left = 1;
    public const int Right = 2;
    public const int Up = 4;      // unused for now (no jumping), bit kept so other bits keep their values
    public const int Down = 8;
    public const int Light = 16;
    public const int Heavy = 32;
    public const int Block = 64;      // unused: blocking is "hold away from the opponent" (Design/DECISIONS.md D-03)
    public const int Grab = 128;
    public const int Special = 256;
    public const int Ultimate = 512;  // only works with a full meter, and needs a fresh press (not held)
}
