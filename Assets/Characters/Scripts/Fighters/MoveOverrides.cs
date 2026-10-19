using System;
using System.Collections.Generic;

// Saved frame-data edits for code-written characters. The Character Preview's Save button writes
// Resources/MoveOverrides/<characterId>.json; CharacterList.Create applies it on top of the .cs
// values (moves are matched by their position in ListMoves, hitboxes by their position in the move).
// Delete the json to go back to the .cs numbers. Plain ints, same file in every build, so the sim
// stays deterministic as long as both players run the same build (bump the relay Version after a save).
public static class MoveOverrides
{
    [Serializable]
    public class HitboxData
    {
        public int spawnTick, lifetime, damage, hitstun, knockback, x, y, w, h, vx;
    }

    [Serializable]
    public class MoveData
    {
        public string name;   // for reading the file; the index is what matches
        public int totalTicks, cooldown;
        public HitboxData[] hitboxes;
    }

    [Serializable]
    public class FileData
    {
        public MoveData[] moves;
    }

    static readonly HashSet<int> applied = new HashSet<int>();

    // Set by the Unity side (CharacterRegistry): reads Resources/MoveOverrides/<id>.json. This assembly
    // has no engine references, so it only sees the parsed data.
    public static Func<int, FileData> Loader;

    public static void Reset() { applied.Clear(); }

    public static void Apply(Fighter f, int id)
    {
        if (f == null || Loader == null || applied.Contains(id)) return;
        applied.Add(id);   // the move defs are shared statics, so once is enough
        FileData data = Loader(id);
        if (data == null || data.moves == null) return;

        MoveDef[] moves = f.ListMoves();
        for (int i = 0; i < moves.Length && i < data.moves.Length; i++)
        {
            MoveDef m = moves[i];
            MoveData d = data.moves[i];
            if (m == null || d == null) continue;
            m.totalTicks = Math.Max(1, d.totalTicks);
            m.cooldown = Math.Max(0, d.cooldown);
            if (m.hitboxes == null || d.hitboxes == null) continue;
            for (int h = 0; h < m.hitboxes.Length && h < d.hitboxes.Length; h++)
            {
                HitboxDef hb = m.hitboxes[h];
                HitboxData hd = d.hitboxes[h];
                hb.spawnTick = hd.spawnTick;
                hb.lifetime = hd.lifetime;
                hb.damage = hd.damage;
                hb.hitstun = hd.hitstun;
                hb.knockback = hd.knockback;
                hb.box.x = hd.x; hb.box.y = hd.y; hb.box.w = hd.w; hb.box.h = hd.h;
                if (!hb.followOwner) hb.vx = hd.vx;
            }
        }
    }

    public static FileData Capture(MoveDef[] moves)
    {
        FileData data = new FileData();
        data.moves = new MoveData[moves.Length];
        for (int i = 0; i < moves.Length; i++)
        {
            MoveDef m = moves[i];
            MoveData d = new MoveData { name = m.name, totalTicks = m.totalTicks, cooldown = m.cooldown };
            int n = m.hitboxes == null ? 0 : m.hitboxes.Length;
            d.hitboxes = new HitboxData[n];
            for (int h = 0; h < n; h++)
            {
                HitboxDef hb = m.hitboxes[h];
                d.hitboxes[h] = new HitboxData
                {
                    spawnTick = hb.spawnTick, lifetime = hb.lifetime, damage = hb.damage, hitstun = hb.hitstun,
                    knockback = hb.knockback, x = hb.box.x, y = hb.box.y, w = hb.box.w, h = hb.box.h, vx = hb.vx
                };
            }
            data.moves[i] = d;
        }
        return data;
    }
}
