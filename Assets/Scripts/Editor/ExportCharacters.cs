using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Menu: Fight > Export Characters to Server File
//
// Reads every CharacterData asset in the project and writes relay/characters.json. Deploying the
// relay (npm run deploy in the relay folder) publishes that file, and every game that connects
// downloads it at startup and turns it into the real characters.
public static class ExportCharacters
{
    [MenuItem("Fight/Export Characters to Server File")]
    static void Export()
    {
        List<CharacterDef> defs = new List<CharacterDef>();
        HashSet<int> ids = new HashSet<int>();
        int skipped = 0;

        string[] guids = AssetDatabase.FindAssets("t:CharacterData");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (data == null) continue;

            if (data.def.id == 0 || !ids.Add(data.def.id))
            {
                Debug.LogError("Skipped " + path + ": missing or duplicate id (right click it > Generate new id).");
                skipped++;
                continue;
            }
            defs.Add(data.def);
        }
        defs.Sort(delegate (CharacterDef a, CharacterDef b) { return a.id.CompareTo(b.id); });   // same file every time

        string root = Directory.GetParent(Application.dataPath).FullName;
        string file = Path.Combine(root, "relay", "characters.json");
        File.WriteAllText(file, CharacterJson.ToJson(defs.ToArray()) + "\n");

        EditorUtility.DisplayDialog("Characters exported",
            defs.Count + " character(s) written to relay/characters.json" + (skipped > 0 ? " (" + skipped + " skipped, see Console)" : "") +
            ".\n\nNext: commit that file, then run  npm run deploy  in the relay folder to publish it.", "OK");
    }
}
