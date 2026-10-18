using UnityEditor;
using UnityEngine;

// Inspector for CharacterPreview: a dropdown of every character in CharacterList, then Move and
// Tick sliders that follow that character's moves, plus a Play button.
[CustomEditor(typeof(CharacterPreview))]
public class CharacterPreviewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        CharacterPreview p = (CharacterPreview)target;

        if (!Application.isPlaying) CharacterRegistry.EnsureLoaded();

        // character dropdown, built from the real roster
        System.Collections.Generic.IList<int> ids = CharacterList.Ids;
        string[] names = new string[ids.Count];
        int selected = -1;
        for (int i = 0; i < ids.Count; i++)
        {
            names[i] = CharacterList.NameOf(ids[i]);
            if (ids[i] == p.characterId) selected = i;
        }
        SerializedProperty idProp = serializedObject.FindProperty("characterId");
        EditorGUILayout.BeginHorizontal();
        int picked = EditorGUILayout.Popup("Character", selected, names);
        if (GUILayout.Button("Reload", GUILayout.Width(60)))
        {
            CharacterRegistry.Reload();
            p.Refresh();
        }
        EditorGUILayout.EndHorizontal();
        if (picked >= 0 && picked != selected) idProp.intValue = ids[picked];

        MoveDef[] moves = p.Moves();
        if (moves.Length == 0)
        {
            EditorGUILayout.HelpBox("Pick a character above. (New inspector characters: press Reload.)", MessageType.Info);
        }
        else
        {
            SerializedProperty moveProp = serializedObject.FindProperty("moveIndex");
            moveProp.intValue = EditorGUILayout.IntSlider("Move", moveProp.intValue, 0, moves.Length - 1);

            MoveDef move = moves[Mathf.Clamp(moveProp.intValue, 0, moves.Length - 1)];
            string moveName = !string.IsNullOrEmpty(move.name) ? move.name : "(unnamed)";
            EditorGUILayout.LabelField(" ", moveName + "   " + move.totalTicks + " ticks");

            SerializedProperty tickProp = serializedObject.FindProperty("tick");
            tickProp.intValue = EditorGUILayout.IntSlider("Tick", tickProp.intValue, 0, Mathf.Max(0, move.totalTicks - 1));

            SerializedProperty playProp = serializedObject.FindProperty("playing");
            if (GUILayout.Button(playProp.boolValue ? "Stop" : "Play move"))
            {
                playProp.boolValue = !playProp.boolValue;
            }
        }

        DrawPropertiesExcluding(serializedObject, "m_Script", "characterId", "character", "moveIndex", "tick", "playing");
        serializedObject.ApplyModifiedProperties();
    }
}
