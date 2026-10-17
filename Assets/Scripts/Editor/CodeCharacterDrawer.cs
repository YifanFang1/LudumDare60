using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Shows a [CodeCharacter] int field (CodeCharacterArt.characterId) as a dropdown of the characters
// written in code, so nobody has to remember or type ids.
[CustomPropertyDrawer(typeof(CodeCharacterAttribute))]
public class CodeCharacterDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        List<int> ids = new List<int>();
        List<string> names = new List<string>();
        ids.Add(-1);
        names.Add("(pick a character)");

        IList<int> all = CharacterList.Ids;
        for (int i = 0; i < all.Count; i++)
        {
            if (!CharacterList.IsCodeCharacter(all[i])) continue;
            ids.Add(all[i]);
            names.Add(CharacterList.NameOf(all[i]) + "   (id " + all[i] + ")");
        }

        int current = ids.IndexOf(property.intValue);
        if (current < 0)
        {
            ids.Add(property.intValue);
            names.Add("unknown id " + property.intValue);
            current = ids.Count - 1;
        }

        EditorGUI.BeginProperty(position, label, property);
        int picked = EditorGUI.Popup(position, label.text, current, names.ToArray());
        property.intValue = ids[picked];
        EditorGUI.EndProperty();
    }
}
