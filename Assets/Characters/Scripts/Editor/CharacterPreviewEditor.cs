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
            if (move == K3N.InstallWhip || move == K3N.InstallPunch) moveName += " (INSTALL)";
            EditorGUILayout.LabelField(" ", moveName + "   " + move.totalTicks + " ticks");

            SerializedProperty tickProp = serializedObject.FindProperty("tick");
            tickProp.intValue = EditorGUILayout.IntSlider("Tick", tickProp.intValue, 0, Mathf.Max(0, move.totalTicks - 1));

            SerializedProperty playProp = serializedObject.FindProperty("playing");
            if (GUILayout.Button(playProp.boolValue ? "Stop" : "Play move"))
            {
                playProp.boolValue = !playProp.boolValue;
            }

            DrawFrameData(p, move, tickProp);
        }

        DrawPropertiesExcluding(serializedObject, "m_Script", "characterId", "character", "moveIndex", "tick", "playing");
        serializedObject.ApplyModifiedProperties();
    }

    // ---------- frame data: a timeline plus the numbers, editable right here ----------

    static readonly Color StartupColor = new Color(0.35f, 0.45f, 0.6f);
    static readonly Color ActiveColor = new Color(0.9f, 0.25f, 0.2f);
    static readonly Color RecoveryColor = new Color(0.3f, 0.3f, 0.3f);

    bool frameFoldout = true;
    readonly System.Collections.Generic.Dictionary<int, bool> hitboxFoldouts = new System.Collections.Generic.Dictionary<int, bool>();

    void DrawFrameData(CharacterPreview p, MoveDef move, SerializedProperty tickProp)
    {
        EditorGUILayout.Space();
        frameFoldout = EditorGUILayout.Foldout(frameFoldout, "Frame data: " + move.name, true);
        if (!frameFoldout) return;

        CharacterLook look = CharacterRegistry.GetLook(p.characterId);
        CharacterData asset = look as CharacterData;   // an inspector character: edits are saved in its asset
        if (asset == null)
        {
            EditorGUILayout.HelpBox("This character is written in code, so edits here are live but are lost when scripts reload. Press 'Copy values' and paste them into its .cs file to keep them.", MessageType.Info);
        }

        DrawTimeline(move, tickProp);

        EditorGUI.BeginChangeCheck();
        if (asset != null) Undo.RecordObject(asset, "Edit frame data");

        move.totalTicks = Mathf.Max(1, EditorGUILayout.IntField("Total ticks", move.totalTicks));
        move.cooldown = Mathf.Max(0, EditorGUILayout.IntField("Cooldown", move.cooldown));

        if (move.hitboxes != null)
        {
            for (int i = 0; i < move.hitboxes.Length; i++)
            {
                HitboxDef hb = move.hitboxes[i];
                bool open;
                if (!hitboxFoldouts.TryGetValue(i, out open)) open = true;
                int startup = hb.spawnTick;
                int active = hb.untilHit ? 0 : hb.lifetime;
                int recovery = Mathf.Max(0, move.totalTicks - hb.spawnTick - hb.lifetime);
                open = EditorGUILayout.Foldout(open, "Hitbox " + i + "   startup " + startup + "   active " + active + "   recovery " + recovery, true);
                hitboxFoldouts[i] = open;
                if (!open) continue;

                EditorGUI.indentLevel++;
                hb.spawnTick = Mathf.Max(0, EditorGUILayout.IntField("Spawn tick (startup)", hb.spawnTick));
                hb.lifetime = Mathf.Max(1, EditorGUILayout.IntField("Lifetime (active ticks)", hb.lifetime));
                hb.damage = EditorGUILayout.IntField("Damage", hb.damage);
                hb.hitstun = EditorGUILayout.IntField("Hitstun", hb.hitstun);
                hb.knockback = EditorGUILayout.IntField("Knockback", hb.knockback);
                hb.box.x = EditorGUILayout.IntField("Box x", hb.box.x);
                hb.box.y = EditorGUILayout.IntField("Box y", hb.box.y);
                hb.box.w = EditorGUILayout.IntField("Box width", hb.box.w);
                hb.box.h = EditorGUILayout.IntField("Box height", hb.box.h);
                if (!hb.followOwner) hb.vx = EditorGUILayout.IntField("Projectile speed (vx)", hb.vx);
                EditorGUI.indentLevel--;
                move.hitboxes[i] = hb;
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            if (asset != null) EditorUtility.SetDirty(asset);
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Copy values")) GUIUtility.systemCopyBuffer = FrameText(move);
    }

    // Startup (blue), active (red) and recovery (grey) across the move's ticks. Click or drag to set the tick.
    static void DrawTimeline(MoveDef move, SerializedProperty tickProp)
    {
        Rect r = GUILayoutUtility.GetRect(10f, 26f, GUILayout.ExpandWidth(true));
        float total = Mathf.Max(1, move.totalTicks);

        EditorGUI.DrawRect(r, RecoveryColor);
        if (move.hitboxes != null && move.hitboxes.Length > 0)
        {
            int first = int.MaxValue;
            for (int i = 0; i < move.hitboxes.Length; i++) first = Mathf.Min(first, move.hitboxes[i].spawnTick);
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(first / total), r.height), StartupColor);
            for (int i = 0; i < move.hitboxes.Length; i++)
            {
                HitboxDef hb = move.hitboxes[i];
                float x0 = r.x + r.width * Mathf.Clamp01(hb.spawnTick / total);
                float x1 = r.x + r.width * Mathf.Clamp01((hb.spawnTick + hb.lifetime) / total);
                EditorGUI.DrawRect(new Rect(x0, r.y + 3f, Mathf.Max(2f, x1 - x0), r.height - 6f), ActiveColor);
            }
        }
        float tx = r.x + r.width * Mathf.Clamp01((tickProp.intValue + 0.5f) / total);
        EditorGUI.DrawRect(new Rect(tx - 1f, r.y, 2f, r.height), Color.white);

        Event e = Event.current;
        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && r.Contains(e.mousePosition))
        {
            tickProp.intValue = Mathf.Clamp(Mathf.FloorToInt((e.mousePosition.x - r.x) / r.width * total), 0, move.totalTicks - 1);
            e.Use();
        }
    }

    static string FrameText(MoveDef move)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("// " + move.name + ": totalTicks " + move.totalTicks + ", cooldown " + move.cooldown);
        if (move.hitboxes != null)
        {
            for (int i = 0; i < move.hitboxes.Length; i++)
            {
                HitboxDef hb = move.hitboxes[i];
                sb.AppendLine("// hitbox " + i + ": spawnTick " + hb.spawnTick + ", activeTicks/lifetime " + hb.lifetime
                    + ", box new Box(" + hb.box.x + ", " + hb.box.y + ", " + hb.box.w + ", " + hb.box.h + ")"
                    + ", damage " + hb.damage + ", hitstun " + hb.hitstun + ", knockback " + hb.knockback
                    + (hb.followOwner ? "" : ", speed " + hb.vx));
            }
        }
        return sb.ToString();
    }
}
