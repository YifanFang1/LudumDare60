using System;

// An axis-aligned rectangle in milli-units. y points UP, y = 0 is the ground.
//
// As a LOCAL box on a fighter: x is how far FORWARD (the way the fighter faces) the box's
// near edge is from the fighter's origin (negative = behind), y is the height above the feet.
// ToWorld() turns it into a WORLD box where x is the left edge, flipping for facing left.
[Serializable]
public struct Box
{
    public int x;
    public int y;
    public int w;
    public int h;

    public Box(int x, int y, int w, int h)
    {
        this.x = x;
        this.y = y;
        this.w = w;
        this.h = h;
    }

    public Box ToWorld(int originX, int originY, int facing)
    {
        int left = facing >= 0 ? originX + x : originX - x - w;
        return new Box(left, originY + y, w, h);
    }

    public bool Overlaps(Box o)
    {
        return x < o.x + o.w && o.x < x + w && y < o.y + o.h && o.y < y + h;
    }

    // Middle of the area where two boxes overlap (used for hit spark position).
    public void OverlapCenter(Box o, out int cx, out int cy)
    {
        int left = Math.Max(x, o.x);
        int right = Math.Min(x + w, o.x + o.w);
        int bottom = Math.Max(y, o.y);
        int top = Math.Min(y + h, o.y + o.h);
        cx = (left + right) / 2;
        cy = (bottom + top) / 2;
    }
}
