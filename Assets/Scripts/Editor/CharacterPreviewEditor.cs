using UnityEditor;
using UnityEngine;

// Inspector for CharacterPreview: Move and Tick are sliders that follow the character's data
// (Move runs over its list of moves, Tick over the chosen move's length), plus a Play button.
[CustomEditor(typeof(CharacterPreview))]
public class CharacterPreviewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        CharacterPreview p = (CharacterPreview)target;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("character"));

        bool ready = p.character != null && p.character.def != null
                     && p.character.def.moves != null && p.character.def.moves.Length > 0;

        if (!ready)
        {
            EditorGUILayout.HelpBox("Drag a Character asset (with at least one move) into the slot above.", MessageType.Info);
        }
        else
        {
            CharacterDef def = p.character.def;

            SerializedProperty moveProp = serializedObject.FindProperty("moveIndex");
            moveProp.intValue = EditorGUILayout.IntSlider("Move", moveProp.intValue, 0, def.moves.Length - 1);

            MoveBinding binding = def.moves[Mathf.Clamp(moveProp.intValue, 0, def.moves.Length - 1)];
            string moveName = binding.move != null && !string.IsNullOrEmpty(binding.move.name) ? binding.move.name : "(unnamed)";
            int total = binding.move != null ? binding.move.totalTicks : 0;
            EditorGUILayout.LabelField(" ", moveName + "   button: " + binding.button + "   " + total + " ticks");

            SerializedProperty tickProp = serializedObject.FindProperty("tick");
            tickProp.intValue = EditorGUILayout.IntSlider("Tick", tickProp.intValue, 0, Mathf.Max(0, total - 1));

            SerializedProperty playProp = serializedObject.FindProperty("playing");
            if (GUILayout.Button(playProp.boolValue ? "Stop" : "Play move"))
            {
                playProp.boolValue = !playProp.boolValue;
            }
        }

        DrawPropertiesExcluding(serializedObject, "m_Script", "character", "moveIndex", "tick", "playing");
        serializedObject.ApplyModifiedProperties();
    }
}
