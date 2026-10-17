using System.Collections.Generic;
using UnityEngine;

// Converts between CharacterDef lists and the JSON text stored on the server.
public static class CharacterJson
{
    public static string ToJson(CharacterDef[] defs)
    {
        CharacterBundle bundle = new CharacterBundle();
        bundle.characters = defs;
        return JsonUtility.ToJson(bundle, true);
    }

    // Parses and checks a bundle. On failure `error` says why and nothing should be used.
    public static bool TryParse(string json, out CharacterDef[] defs, out string error)
    {
        defs = null;
        error = null;

        CharacterBundle bundle;
        try { bundle = JsonUtility.FromJson<CharacterBundle>(json); }
        catch (System.Exception e) { error = "not valid JSON (" + e.Message + ")"; return false; }

        if (bundle == null || bundle.characters == null) { error = "no character list"; return false; }
        if (bundle.format != CharacterBundle.CurrentFormat)
        {
            error = "format " + bundle.format + " (this build understands " + CharacterBundle.CurrentFormat + ")";
            return false;
        }

        HashSet<int> seen = new HashSet<int>();
        for (int i = 0; i < bundle.characters.Length; i++)
        {
            CharacterDef d = bundle.characters[i];
            if (d == null || d.id == 0) { error = "character #" + i + " has no id"; return false; }
            if (!seen.Add(d.id)) { error = "duplicate id " + d.id; return false; }
            if (d.maxHp <= 0) { error = d.displayName + ": maxHp must be above 0"; return false; }
            if (d.moves == null) { error = d.displayName + ": no moves list"; return false; }
            for (int m = 0; m < d.moves.Length; m++)
            {
                if (d.moves[m] == null || d.moves[m].move == null || d.moves[m].move.hitboxes == null)
                {
                    error = d.displayName + ": move #" + m + " is incomplete";
                    return false;
                }
            }
        }

        defs = bundle.characters;
        return true;
    }
}
