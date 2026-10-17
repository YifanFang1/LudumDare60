using System.Collections.Generic;
using UnityEngine;

// Loads every CharacterData in a Resources/Characters folder before the first scene starts and
// registers it in CharacterList. After that the game treats them like any other character.
// Also loads the CodeCharacterArt assets in the same folder: the art for characters written in code.
public static class CharacterRegistry
{
    static readonly Dictionary<int, CharacterData> byId = new Dictionary<int, CharacterData>();
    static readonly Dictionary<int, CodeCharacterArt> codeArt = new Dictionary<int, CodeCharacterArt>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Load()
    {
        byId.Clear();
        CharacterList.ClearData();

        CharacterData[] all = Resources.LoadAll<CharacterData>("Characters");
        System.Array.Sort(all, delegate (CharacterData a, CharacterData b) { return string.CompareOrdinal(a.name, b.name); });

        for (int i = 0; i < all.Length; i++)
        {
            CharacterData d = all[i];
            if (d.def.id == 0)
            {
                Debug.LogError("Character '" + d.name + "' has no id yet. Click it once in the Project window so it gets one.");
                continue;
            }
            if (!CharacterList.RegisterData(d.def))
            {
                Debug.LogError("Character '" + d.name + "' has the same id (" + d.def.id + ") as another character (probably a duplicated asset). Right click it > Generate new id.");
                continue;
            }
            byId[d.def.id] = d;
        }

        LoadCodeCharacterArt();
    }

    static void LoadCodeCharacterArt()
    {
        codeArt.Clear();
        CodeCharacterArt[] all = Resources.LoadAll<CodeCharacterArt>("Characters");
        for (int i = 0; i < all.Length; i++)
        {
            CodeCharacterArt a = all[i];
            if (!CharacterList.IsCodeCharacter(a.characterId))
            {
                Debug.LogError("Character art '" + a.name + "' is not set to a code character. Click it and pick one in 'Character'.");
                continue;
            }
            if (codeArt.ContainsKey(a.characterId))
            {
                Debug.LogError("Character art '" + a.name + "' and '" + codeArt[a.characterId].name + "' are both for " + CharacterList.NameOf(a.characterId) + ". Only one is used.");
                continue;
            }
            codeArt[a.characterId] = a;
        }
    }

    // Replace the gameplay data with what the server published (see Net/RelayLockstepSession).
    // The server file is the single source of truth for stats and hitboxes, so everyone who
    // connected to it plays identical characters. Art still comes from the assets in this build,
    // matched by id; a character with no asset here just gets a coloured box.
    public static void ApplyServerData(CharacterDef[] defs)
    {
        CharacterList.ClearData();
        for (int i = 0; i < defs.Length; i++)
        {
            if (!CharacterList.RegisterData(defs[i]))
            {
                Debug.LogError("Server character '" + defs[i].displayName + "' uses reserved id " + defs[i].id + ". Skipped.");
            }
        }
    }

    // The inspector asset behind an id, or null for characters written in code.
    public static CharacterData Get(int id)
    {
        CharacterData d;
        return byId.TryGetValue(id, out d) ? d : null;
    }

    // How a character looks: its CharacterData, or its CodeCharacterArt for code characters.
    // null = no art in this build, so it is drawn as a coloured box.
    public static CharacterLook GetLook(int id)
    {
        CharacterData d;
        if (byId.TryGetValue(id, out d)) return d;
        CodeCharacterArt a;
        return codeArt.TryGetValue(id, out a) ? a : null;
    }
}
