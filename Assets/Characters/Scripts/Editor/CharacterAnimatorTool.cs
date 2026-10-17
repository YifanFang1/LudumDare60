using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Menu: Fight > Create Animator for Selected Character
//
// Select a character's art asset (a CodeCharacterArt, or an inspector CharacterData) and run this.
// It makes everything an animated character needs, with the exact names the game looks for:
//
//   Assets/Characters/<Name>/<Name>.controller        Animator with states Idle, Walk, DashForward,
//                                              DashBack, Hitstun, Blockstun, KO and one per move
//                                              (Light, Heavy, Grab, Special, Ultimate, ...)
//   Assets/Characters/<Name>/<Name>_<State>.anim      one empty clip per state, ready for frames
//   Assets/Characters/<Name>/<Name>_View.prefab       Animator + a child "Art" SpriteRenderer
//
// and plugs the prefab into the asset's View Prefab slot. After that an artist only fills in the
// clips (Window > Animation > Animation, select the prefab, pick a clip, drag sprites in).
//
// Safe to run again, e.g. after adding a move: it only adds what's missing and never deletes
// or overwrites existing clips or states.
public static class CharacterAnimatorTool
{
    const string MenuPath = "Fight/Create Animator for Selected Character";
    const string ArtRoot = "Assets/Characters";

    // Every fighter state the view can ask for (see WorldView.Pose). Moves are added per character.
    static readonly string[] StateNames = { "Idle", "Walk", "DashForward", "DashBack", "Hitstun", "Blockstun", "KO" };
    static readonly string[] LoopingStates = { "Idle", "Walk" };

    [MenuItem(MenuPath, true)]
    static bool CanRun()
    {
        return Selection.activeObject is CharacterLook;
    }

    [MenuItem(MenuPath)]
    static void Run()
    {
        CharacterLook look = (CharacterLook)Selection.activeObject;

        string displayName;
        MoveDef[] moves;
        if (!Describe(look, out displayName, out moves)) return;

        string folder = EnsureFolder(ArtRoot, MakeSafe(displayName));
        string baseName = folder + "/" + MakeSafe(displayName);

        List<string> states = new List<string>(StateNames);
        for (int i = 0; i < moves.Length; i++)
        {
            if (!string.IsNullOrEmpty(moves[i].name) && !states.Contains(moves[i].name)) states.Add(moves[i].name);
        }

        AnimatorController controller = LoadOrCreateController(baseName + ".controller");
        int added = AddMissingStates(controller, states, baseName);

        GameObject prefab = EnsurePrefab(look, controller, baseName + "_View.prefab");

        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(prefab != null ? (Object)prefab : controller);
        Debug.Log("Animator for " + displayName + ": " + states.Count + " states (" + added + " new) in " + folder
                  + ". States: " + string.Join(", ", states.ToArray()));
    }

    // Name and move list of the character this look belongs to.
    static bool Describe(CharacterLook look, out string displayName, out MoveDef[] moves)
    {
        CharacterData data = look as CharacterData;
        if (data != null)
        {
            displayName = string.IsNullOrEmpty(data.def.displayName) ? data.name : data.def.displayName;
            moves = new DataFighter(data.def).ListMoves();
            return true;
        }

        if (!CharacterList.IsCodeCharacter(look.CharacterId))
        {
            EditorUtility.DisplayDialog("Pick a character first",
                "'" + look.name + "' isn't set to a code character yet. Choose one in its 'Character' dropdown, then try again.", "OK");
            displayName = null;
            moves = null;
            return false;
        }
        displayName = CharacterList.NameOf(look.CharacterId);
        moves = CharacterList.Create(look.CharacterId).ListMoves();
        return true;
    }

    static AnimatorController LoadOrCreateController(string path)
    {
        AnimatorController c = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        return c != null ? c : AnimatorController.CreateAnimatorControllerAtPath(path);
    }

    static int AddMissingStates(AnimatorController controller, List<string> states, string baseName)
    {
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        HashSet<string> existing = new HashSet<string>();
        foreach (ChildAnimatorState s in machine.states) existing.Add(s.state.name);

        int added = 0;
        for (int i = 0; i < states.Count; i++)
        {
            string name = states[i];
            if (existing.Contains(name)) continue;

            AnimatorState state = machine.AddState(name);
            state.motion = LoadOrCreateClip(baseName + "_" + MakeSafe(name) + ".anim", System.Array.IndexOf(LoopingStates, name) >= 0);
            if (name == "Idle") machine.defaultState = state;
            added++;
        }
        EditorUtility.SetDirty(controller);
        return added;
    }

    static AnimationClip LoadOrCreateClip(string path, bool loop)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip != null) return clip;

        clip = new AnimationClip();
        clip.frameRate = 60;   // one frame per sim tick
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    // Make sure the look has a View Prefab whose Animator uses the controller.
    static GameObject EnsurePrefab(CharacterLook look, AnimatorController controller, string path)
    {
        if (look.viewPrefab == null)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing == null)
            {
                GameObject root = new GameObject(Path.GetFileNameWithoutExtension(path));
                root.AddComponent<Animator>().runtimeAnimatorController = controller;
                GameObject art = new GameObject("Art");
                art.transform.SetParent(root.transform, false);
                art.AddComponent<SpriteRenderer>().sprite = look.sprite;   // the single pose, if there is one
                existing = PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
            }
            look.viewPrefab = existing;
            EditorUtility.SetDirty(look);
            return existing;
        }

        // The look already has a prefab: give it an Animator / the controller if it lacks one.
        string prefabPath = AssetDatabase.GetAssetPath(look.viewPrefab);
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        Animator animator = contents.GetComponentInChildren<Animator>();
        if (animator == null) animator = contents.AddComponent<Animator>();
        if (animator.runtimeAnimatorController == null)
        {
            animator.runtimeAnimatorController = controller;
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        }
        else if (animator.runtimeAnimatorController != controller)
        {
            Debug.LogWarning("'" + look.viewPrefab.name + "' already uses the Animator '" + animator.runtimeAnimatorController.name
                             + "', so it was left alone. The new states were added to '" + controller.name + "'.");
        }
        PrefabUtility.UnloadPrefabContents(contents);
        return look.viewPrefab;
    }

    static string EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent))
        {
            AssetDatabase.CreateFolder(Path.GetDirectoryName(parent).Replace('\\', '/'), Path.GetFileName(parent));
        }
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        return path;
    }

    static string MakeSafe(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Replace(' ', '_');
    }
}
