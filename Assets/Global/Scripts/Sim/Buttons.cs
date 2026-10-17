// One player's input for one tick is a single int. Each button is one bit.
public static class Buttons
{
    public const int Left = 1;
    public const int Right = 2;
    public const int Up = 4;      // unused for now (no jumping), bit kept so other bits keep their values
    public const int Down = 8;
    public const int Light = 16;
    public const int Heavy = 32;
    public const int Block = 64;
}
