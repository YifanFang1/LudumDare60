using System;
using System.Collections.Generic;

// The roster: every character the game can create, looked up by id.
//
//   ids 0-999   characters written in code (BaseFighter subclasses), listed in the static
//               constructor below. Take the next free number and add ONE line there.
//                 0  Brawler   (also the fallback for unknown ids, never remove it)
//                 1  Shooter
//                 2  Sandbag   (training dummy)
//                 3-9          free, for more test characters
//                10  Samurai          "Ruy"              the launch roster (Design/README.md),
//                11  Painter          "Paintress"        designed in Design/Characters/. The
//                12  Cowboy           "Noir"             class names are older than the
//                13  Elephant         "Room"             in-game names (D-40) and are kept
//                14  Executioner      "The Executioner"  to avoid churn.
//                15  PlagueDoctor     "Dr. Remy"
//                16  Nek              "Nek" (temp name)
//                17+          free, for new roster characters
//   other ids   characters made in the Unity inspector (CharacterData assets). They get a big
//               random id and register themselves at startup, so nobody edits this file for them.
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

    public const int FirstDataId = 1000;   // ids below this are reserved for code characters

    static readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
    static readonly List<int> sortedIds = new List<int>();

    static CharacterList()
    {
        Add(Brawler.Id, "Brawler", delegate { return new Brawler(); }, null);
        Add(Shooter.Id, "Shooter", delegate { return new Shooter(); }, null);
        Add(Sandbag.Id, "Sandbag", delegate { return new Sandbag(); }, null);

        Add(Samurai.Id, "Ruy", delegate { return new Samurai(); }, null);
        Add(Painter.Id, "Paintress", delegate { return new Painter(); }, null);
        Add(Cowboy.Id, "Noir", delegate { return new Cowboy(); }, null);
        Add(Elephant.Id, "Room", delegate { return new Elephant(); }, null);
        Add(Executioner.Id, "The Executioner", delegate { return new Executioner(); }, null);
        Add(PlagueDoctor.Id, "Dr. Remy", delegate { return new PlagueDoctor(); }, null);
        Add(Nek.Id, "Nek", delegate { return new Nek(); }, null);
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

    // True for characters written in code (BaseFighter subclasses), false for inspector ones.
    public static bool IsCodeCharacter(int id)
    {
        Entry e;
        return entries.TryGetValue(id, out e) && e.def == null;
    }

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

    // A recipe for building this character, for GameSim.Reset: the sim builds a FRESH fighter
    // from it at the start of every round, so no per-round state can leak into the next one.
    public static Func<Fighter> Factory(int id)
    {
        return delegate { return Create(id); };
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
