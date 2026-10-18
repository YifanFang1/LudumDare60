using UnityEditor;
using UnityEngine;

// Makes the two assets the outlines need, the first time the project opens with this code:
//   Assets/Global/Rendering/Resources/SpriteOutline.mat    the material every outlined sprite shares
//   Assets/Global/Rendering/Resources/OutlineProfile.asset the Player / Enemy colours
// Commit them (with their .meta files) so everyone uses the same ones.
// Menu Fight > Outline Colors (Player and Enemy) selects the profile so you can edit it.
[InitializeOnLoad]
static class OutlineAssets
{
    const string Folder = "Assets/Global/Rendering/Resources";
    const string MaterialPath = Folder + "/SpriteOutline.mat";
    const string ProfilePath = Folder + "/OutlineProfile.asset";

    static OutlineAssets()
    {
        // Not right now: creating assets while scripts are still loading is not allowed.
        EditorApplication.delayCall += Ensure;
    }

    [MenuItem("Fight/Outline Colors (Player and Enemy)")]
    static void SelectProfile()
    {
        Ensure();
        OutlineProfile profile = AssetDatabase.LoadAssetAtPath<OutlineProfile>(ProfilePath);
        Selection.activeObject = profile;
        EditorGUIUtility.PingObject(profile);
    }

    static void Ensure()
    {
        bool created = false;

        if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) == null)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "/SpriteOutline.shader");
            if (shader == null) return;   // not imported yet; this runs again after the next compile
            AssetDatabase.CreateAsset(new Material(shader), MaterialPath);
            created = true;
        }

        if (AssetDatabase.LoadAssetAtPath<OutlineProfile>(ProfilePath) == null)
        {
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<OutlineProfile>(), ProfilePath);
            created = true;
        }

        if (created)
        {
            AssetDatabase.SaveAssets();
            OutlineProfile.Forget();   // anything that already fell back to the defaults picks up the new assets
            Debug.Log("Created the outline material and OutlineProfile in " + Folder + ". Commit them with their .meta files.");
        }
    }
}
