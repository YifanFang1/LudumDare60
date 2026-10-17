using UnityEditor;
using UnityEngine;

// Menu Fight > CRT Settings: selects the CRT profile asset so its sliders show
// in the Inspector. Creates the asset first if nobody has made it yet.
public static class CRTSettingsMenu
{
    const string Folder = "Assets/UI/Resources";
    const string Path = Folder + "/CRTProfile.asset";

    [MenuItem("Fight/CRT Settings")]
    static void Open()
    {
        var profile = AssetDatabase.LoadAssetAtPath<CRTProfile>(Path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<CRTProfile>();
            AssetDatabase.CreateAsset(profile, Path);
            AssetDatabase.SaveAssets();
        }

        Selection.activeObject = profile;
        EditorGUIUtility.PingObject(profile);
    }
}
