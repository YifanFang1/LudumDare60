using UnityEditor;
using UnityEngine;

// Shows every Box in the inspector (hurtbox, hitboxes) as four sliders instead of four number
// boxes. Units are milli-units: 1000 = 1 Unity unit. Hold and drag; you can still type a value
// by clicking the number next to the slider.
[CustomPropertyDrawer(typeof(Box))]
public class BoxDrawer : PropertyDrawer
{
    const int Rows = 5;   // title + four sliders

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * Rows;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float line = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        Rect r = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        EditorGUI.LabelField(r, label, EditorStyles.boldLabel);
        r.y += line;
        EditorGUI.indentLevel++;
        Slider(r, property, "x", "x  (forward from body)", -3000, 3000); r.y += line;
        Slider(r, property, "y", "y  (height off the ground)", -500, 4000); r.y += line;
        Slider(r, property, "w", "w  (width)", 0, 4000); r.y += line;
        Slider(r, property, "h", "h  (height)", 0, 4000);
        EditorGUI.indentLevel--;

        EditorGUI.EndProperty();
    }

    static void Slider(Rect r, SerializedProperty box, string field, string label, int min, int max)
    {
        EditorGUI.IntSlider(r, box.FindPropertyRelative(field), min, max, new GUIContent(label));
    }
}
