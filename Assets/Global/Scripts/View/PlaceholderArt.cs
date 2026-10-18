using System.Collections.Generic;
using UnityEngine;

// PLACEHOLDER ("fake") ART, drawn in code, so every character can be told apart before an artist
// has touched it. COSMETIC ONLY: it never affects the fight.
//
//   Fighters:     a box sized to the hurtbox, in the character's own colour, with its own pattern
//                 and a big letter (table below). A small dark tab shows which way it faces.
//   Projectiles,  a simple shape per `visual` key (circle, bar, triangle...), in the owner's colour,
//   traps:        stretched to the hitbox.
//   Zones:        a coloured strip on the floor (paint) or a translucent cloud (poison).
//
// REPLACING IT WITH REAL ART needs no code: give the character a Code Character Art asset
// (Create > Fight > Code Character Art) with a Sprite or a View Prefab, and its projectile sprites
// under the same visual keys. WorldView always prefers real art and only falls back to this.
// To change a placeholder (colour, pattern, letter), edit the Styles table.
public static class PlaceholderArt
{
    public enum Pattern { Solid, Stripes, Bands, Checker, Dots, Diagonal, Frame, Chevron }

    public struct Style
    {
        public Color color;
        public Pattern pattern;
        public char letter;

        public Style(float r, float g, float b, Pattern pattern, char letter)
        {
            color = new Color(r, g, b);
            this.pattern = pattern;
            this.letter = letter;
        }
    }

    // One row per code character (CharacterList ids). Inspector characters get a style made up
    // from their id and name (StyleFor).
    static readonly Dictionary<int, Style> Styles = new Dictionary<int, Style>
    {
        { 0,  new Style(0.95f, 0.55f, 0.20f, Pattern.Bands,    'B') },   // Brawler
        { 1,  new Style(0.20f, 0.75f, 0.75f, Pattern.Dots,     'S') },   // Shooter
        { 2,  new Style(0.80f, 0.70f, 0.50f, Pattern.Checker,  'Z') },   // Sandbag
        { 10, new Style(0.25f, 0.45f, 0.95f, Pattern.Stripes,  'R') },   // Ruy
        { 11, new Style(0.95f, 0.30f, 0.75f, Pattern.Diagonal, 'P') },   // Paintress
        { 12, new Style(0.60f, 0.60f, 0.60f, Pattern.Checker,  'N') },   // Noir (black and white)
        { 13, new Style(0.45f, 0.52f, 0.62f, Pattern.Frame,    'E') },   // Room (the Elephant)
        { 14, new Style(0.65f, 0.12f, 0.15f, Pattern.Chevron,  'X') },   // The Executioner
        { 15, new Style(0.25f, 0.60f, 0.30f, Pattern.Dots,     'D') },   // Dr. Remy
        { 16, new Style(0.55f, 0.30f, 0.90f, Pattern.Bands,    'K') },   // Nek
    };

    public static Style StyleFor(int characterId)
    {
        Style s;
        if (Styles.TryGetValue(characterId, out s)) return s;

        // Made up but stable: the same character always gets the same look.
        uint h = (uint)characterId * 2654435761u;
        Color c = Color.HSVToRGB((h % 360) / 360f, 0.6f, 0.9f);
        string name = CharacterList.NameOf(characterId);
        char letter = name.Length > 0 ? char.ToUpperInvariant(name[0]) : '?';
        return new Style(c.r, c.g, c.b, (Pattern)((h >> 9) % 8), letter);
    }

    // ================= fighters =================

    const int FighterW = 24;   // pixels; the sprite is 1 x 2 units, stretched to the hurtbox
    const int FighterH = 48;

    static readonly Dictionary<int, Sprite> fighterSprites = new Dictionary<int, Sprite>();

    // White and greys, so SpriteRenderer.color tints it: the base takes the full colour, the
    // pattern a darker shade, the letter and border stay dark.
    public static Sprite FighterSprite(int characterId)
    {
        Sprite cached;
        if (fighterSprites.TryGetValue(characterId, out cached) && cached != null) return cached;

        Style style = StyleFor(characterId);
        Texture2D tex = NewTexture(FighterW, FighterH);
        Color32[] px = new Color32[FighterW * FighterH];
        for (int y = 0; y < FighterH; y++)
        {
            for (int x = 0; x < FighterW; x++)
            {
                byte v = PatternOn(style.pattern, x, y) ? (byte)170 : (byte)255;
                if (x < 2 || y < 2 || x >= FighterW - 2 || y >= FighterH - 2) v = 70;   // border
                px[y * FighterW + x] = new Color32(v, v, v, 255);
            }
        }
        DrawLetter(px, FighterW, style.letter, 3, (FighterW - 15) / 2, FighterH - 8 - 21);
        tex.SetPixels32(px);
        tex.Apply();

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, FighterW, FighterH), Vector2.zero, FighterW);
        sprite.name = "Placeholder " + CharacterList.NameOf(characterId);
        fighterSprites[characterId] = sprite;
        return sprite;
    }

    static bool PatternOn(Pattern p, int x, int y)
    {
        switch (p)
        {
            case Pattern.Stripes: return (x / 3) % 2 == 0;
            case Pattern.Bands: return (y / 4) % 2 == 0;
            case Pattern.Checker: return ((x / 4) + (y / 4)) % 2 == 0;
            case Pattern.Dots: return x % 6 >= 2 && x % 6 <= 3 && y % 6 >= 2 && y % 6 <= 3;
            case Pattern.Diagonal: return ((x + y) / 3) % 2 == 0;
            case Pattern.Frame: return x < 6 || x >= FighterW - 6 || y < 6;
            case Pattern.Chevron: return ((Mathf.Abs(x - FighterW / 2) + y) / 4) % 2 == 0;
            default: return false;
        }
    }

    // ================= projectiles, traps, zones =================

    public enum Shape { Circle, Bar, Square, Triangle, Diamond, Spikes, Frame }

    // Which shape a projectile or trap gets when the character has no sprite for its visual key.
    static Shape ShapeFor(string visual)
    {
        switch (visual)
        {
            case "special":
            case "paint_splat": return Shape.Circle;
            case "bullet": return Shape.Bar;
            case "gun":
            case "guillotine": return Shape.Square;
            case "rat":
            case "rats": return Shape.Triangle;
            case "trap": return Shape.Spikes;
            case "canvas": return Shape.Frame;
            default: return Shape.Diamond;
        }
    }

    const int ShapeSize = 32;
    static readonly Dictionary<int, Sprite> shapeSprites = new Dictionary<int, Sprite>();

    // A 1 x 1 unit sprite (centre pivot) to stretch over a hitbox, facing right.
    public static Sprite ShapeSprite(string visual)
    {
        Shape shape = ShapeFor(visual);
        Sprite cached;
        if (shapeSprites.TryGetValue((int)shape, out cached) && cached != null) return cached;

        Texture2D tex = NewTexture(ShapeSize, ShapeSize);
        Color32[] px = new Color32[ShapeSize * ShapeSize];
        float c = (ShapeSize - 1) / 2f;
        for (int y = 0; y < ShapeSize; y++)
        {
            for (int x = 0; x < ShapeSize; x++)
            {
                bool on;
                switch (shape)
                {
                    case Shape.Circle: on = (x - c) * (x - c) + (y - c) * (y - c) <= c * c; break;
                    case Shape.Bar: on = y > ShapeSize / 3 && y < 2 * ShapeSize / 3; break;
                    case Shape.Triangle: on = Mathf.Abs(y - c) <= (x + 1) / 2f; break;   // points right
                    case Shape.Diamond: on = Mathf.Abs(x - c) + Mathf.Abs(y - c) <= c; break;
                    case Shape.Spikes: on = y <= (x % 8 < 4 ? x % 8 : 8 - x % 8) * 3; break;
                    case Shape.Frame: on = x < 4 || y < 4 || x >= ShapeSize - 4 || y >= ShapeSize - 4; break;
                    default: on = true; break;
                }
                bool edge = on && (x == 0 || y == 0 || x == ShapeSize - 1 || y == ShapeSize - 1);
                byte v = edge ? (byte)120 : (byte)255;
                px[y * ShapeSize + x] = on ? new Color32(v, v, v, 255) : new Color32(0, 0, 0, 0);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, ShapeSize, ShapeSize), new Vector2(0.5f, 0.5f), ShapeSize);
        sprite.name = "Placeholder " + shape;
        shapeSprites[(int)shape] = sprite;
        return sprite;
    }

    // How a ground zone looks: colour and height in Unity units. Zones are drawn even with real
    // art, because they're areas, not objects (an artist can still replace the look here).
    public static void ZoneLook(string visual, Color ownerColor, out Color color, out float height)
    {
        if (visual == "poison")
        {
            color = new Color(0.35f, 0.85f, 0.25f, 0.28f);
            height = 2.0f;
            return;
        }
        color = new Color(ownerColor.r, ownerColor.g, ownerColor.b, 0.55f);
        height = 0.12f;
    }

    // ================= helpers =================

    static Texture2D NewTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    // A 5 x 7 pixel font, scaled up, for the letter on a fighter. Rows top to bottom.
    static readonly Dictionary<char, string[]> Font = new Dictionary<char, string[]>
    {
        { 'A', new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" } },
        { 'B', new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" } },
        { 'C', new[] { "01110", "10001", "10000", "10000", "10000", "10001", "01110" } },
        { 'D', new[] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" } },
        { 'E', new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" } },
        { 'F', new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" } },
        { 'G', new[] { "01110", "10001", "10000", "10111", "10001", "10001", "01111" } },
        { 'H', new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" } },
        { 'I', new[] { "01110", "00100", "00100", "00100", "00100", "00100", "01110" } },
        { 'J', new[] { "00111", "00010", "00010", "00010", "00010", "10010", "01100" } },
        { 'K', new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" } },
        { 'L', new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" } },
        { 'M', new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" } },
        { 'N', new[] { "10001", "10001", "11001", "10101", "10011", "10001", "10001" } },
        { 'O', new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" } },
        { 'P', new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" } },
        { 'Q', new[] { "01110", "10001", "10001", "10001", "10101", "10010", "01101" } },
        { 'R', new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" } },
        { 'S', new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" } },
        { 'T', new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" } },
        { 'U', new[] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" } },
        { 'V', new[] { "10001", "10001", "10001", "10001", "10001", "01010", "00100" } },
        { 'W', new[] { "10001", "10001", "10001", "10101", "10101", "10101", "01010" } },
        { 'X', new[] { "10001", "10001", "01010", "00100", "01010", "10001", "10001" } },
        { 'Y', new[] { "10001", "10001", "01010", "00100", "00100", "00100", "00100" } },
        { 'Z', new[] { "11111", "00001", "00010", "00100", "01000", "10000", "11111" } },
        { '?', new[] { "01110", "10001", "00001", "00010", "00100", "00000", "00100" } },
    };

    static void DrawLetter(Color32[] px, int texWidth, char letter, int scale, int left, int bottom)
    {
        string[] rows;
        if (!Font.TryGetValue(letter, out rows)) rows = Font['?'];
        Color32 ink = new Color32(25, 25, 25, 255);
        for (int r = 0; r < 7; r++)
        {
            for (int col = 0; col < 5; col++)
            {
                if (rows[r][col] != '1') continue;
                for (int dy = 0; dy < scale; dy++)
                {
                    for (int dx = 0; dx < scale; dx++)
                    {
                        int x = left + col * scale + dx;
                        int y = bottom + (6 - r) * scale + dy;   // texture rows go bottom to top
                        if (x >= 0 && x < texWidth && y >= 0 && y * texWidth + x < px.Length) px[y * texWidth + x] = ink;
                    }
                }
            }
        }
    }
}
