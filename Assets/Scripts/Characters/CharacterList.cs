using System;
using System.Collections.Generic;

// The roster: every character the game can create, looked up by id.
//
//   id 0        Brawler, written in code (the example for special mechanics)
//   other ids   characters made in the Unity inspector (CharacterData assets). They register
//               themselves at startup, so nobody edits this file to add a character.
//
// A character's id is what gets stored for character select and sent over the network.
public static class CharacterList
{
    class Entry
    {
        public int id;
        public string name;
        public Func<Fighter> make;
        public CharacterDef def;     // null for characters written in code
    }

    static readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
    static readonly List<int> sortedIds = new List<int>();

    static CharacterList()
    {
        Add(0, "Brawler", delegate { return new Brawler(); }, null);
    }

    // For characters written in code. Pick an id that is not used yet.
    public static bool Register(int id, string name, Func<Fighter> make)
    {
        return Add(id, name, make, null);
    }

    // For characters made in the inspector. Returns false if the id is already taken.
    public static bool RegisterData(CharacterDef def)
    {
        return Add(def.id, def.displayName, delegate { return new DataFighter(def); }, def);
    }

    // Forget every inspector-made character (called before they are loaded again).
    public static void ClearData()
    {
        List<int> remove = new List<int>();
        foreach (KeyValuePair<int, Entry> pair in entries)
        {
            if (pair.Value.def != null) remove.Add(pair.Key);
        }
        for (int i = 0; i < remove.Count; i++) entries.Remove(remove[i]);
        Resort();
    }

    static bool Add(int id, string name, Func<Fighter> make, CharacterDef def)
    {
        if (entries.ContainsKey(id)) return false;
        Entry e = new Entry();
        e.id = id;
        e.name = name;
        e.make = make;
        e.def = def;
        entries[id] = e;
        Resort();
        return true;
    }

    static void Resort()
    {
        sortedIds.Clear();
        foreach (int id in entries.Keys) sortedIds.Add(id);
        sortedIds.Sort(delegate (int a, int b)
        {
            int c = string.CompareOrdinal(entries[a].name, entries[b].name);
            return c != 0 ? c : a.CompareTo(b);
        });
    }

    // ----- lookups -----

    public static IList<int> Ids { get { return sortedIds; } }   // sorted by name, for menus
    public static int Count { get { return entries.Count; } }
    public static bool Contains(int id) { return entries.ContainsKey(id); }

    public static string NameOf(int id)
    {
        Entry e;
        return entries.TryGetValue(id, out e) ? e.name : "?";
    }

    // An unknown id falls back to Brawler instead of crashing, so a bad id (from the network or a
    // deleted asset) cannot take the game down.
    public static Fighter Create(int id)
    {
        Entry e;
        if (!entries.TryGetValue(id, out e)) e = entries[0];
        Fighter f = e.make();
        f.characterId = e.id;
        return f;
    }

    // Fingerprint of all inspector-made characters. Added to the network version, so two players
    // whose character data differs are refused instead of silently desyncing.
    public static string DataHash()
    {
        List<int> ids = new List<int>();
        foreach (KeyValuePair<int, Entry> pair in entries)
        {
            if (pair.Value.def != null) ids.Add(pair.Key);
        }
        ids.Sort();

        unchecked
        {
            int h = 17;
            for (int i = 0; i < ids.Count; i++)
            {
                h = h * 31 + entries[ids[i]].def.Hash();
            }
            return h.ToString("x");
        }
    }
}
