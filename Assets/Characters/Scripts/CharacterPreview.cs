using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// BLOCKOUT TOOL, works without pressing Play.
//   1. Add this component to an empty GameObject in the scene.
//   2. Drag a CharacterData asset into "Character".
//   3. Pick a move with Move Index and drag the Tick slider.
// The Scene view draws the character's sprite (if it has one), its body (green) and the move's
// hitboxes at that tick: red while active, faint yellow when not. Select the CharacterData asset
// and drag its sliders (hurtbox, hitbox boxes, sprite offset and scale): everything here updates
// live. Tick N here is exactly tick N of the move in the real fight.
// Press Play in the inspector to step through the move's ticks automatically (60 per second).
// Only the sprite and boxes are previewed; the Animator of a View Prefab plays in matches.
// Outline shows the sprite with the Player or Enemy outline (colours: Fight > Outline Colors).
[ExecuteAlways]
public class CharacterPreview : MonoBehaviour
{
    public CharacterData character;
    public int moveIndex;               // slider in the inspector (see Editor/CharacterPreviewEditor)
    public int tick;                    // slider in the inspector, 0 .. totalTicks - 1
    public bool playing;                // steps through the move's ticks in the editor
    [Range(0.1f, 2f)] public float playSpeed = 1f;
    public bool faceRight = true;

    [Header("What to show")]
    public bool showSprite = true;
    public bool showHurtbox = true;
    public bool showHitboxes = true;
    public bool showInactiveHitboxes = true;
    public bool filled = true;         // translucent fill as well as the outline
    public bool showOutline = true;
    [Tooltip("Which outline colours to preview. Custom is plain white here.")]
    public SpriteOutline.Style outline = SpriteOutline.Style.Player;

    SpriteRenderer art;                // lives on a hidden child object, never saved in the scene
    SpriteOutline artOutline;          // on the same hidden object

#if UNITY_EDITOR
    double lastTime;
    float tickBank;

    // Steps `tick` forward 60 times a second while "playing" (edit mode only).
    void EditorStep()
    {
        double now = EditorApplication.timeSinceStartup;
        float dt = (float)(now - lastTime);
        lastTime = now;

        if (!playing || Application.isPlaying || character == null) { tickBank = 0f; return; }
        int total = TotalTicks();
        if (total <= 0) return;

        tickBank += dt * playSpeed;
        while (tickBank >= 1f / 60f)
        {
            tickBank -= 1f / 60f;
            tick = (tick + 1) % total;
        }
        SceneView.RepaintAll();
    }
#endif

    public int TotalTicks()
    {
        if (character == null || character.def == null || character.def.moves == null || character.def.moves.Length == 0) return 0;
        MoveDef move = character.def.moves[Mathf.Clamp(moveIndex, 0, character.def.moves.Length - 1)].move;
        return move == null ? 0 : move.totalTicks;
    }

    void OnEnable()
    {
#if UNITY_EDITOR
        lastTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += EditorStep;
#endif
        if (art == null)
        {
            GameObject go = new GameObject("PreviewSprite");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(transform, false);
            art = go.AddComponent<SpriteRenderer>();
            art.sortingOrder = -5;
            artOutline = go.AddComponent<SpriteOutline>();
        }
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorStep;
#endif
        if (art == null) return;
        if (Application.isPlaying) Destroy(art.gameObject);
        else DestroyImmediate(art.gameObject);
        art = null;
        artOutline = null;
    }

    void SyncSprite()
    {
        if (art == null) return;

        bool have = character != null && character.sprite != null && showSprite;
        art.enabled = have;
        if (!have) return;

        int facing = faceRight ? 1 : -1;
        art.sprite = character.sprite;
        art.transform.localPosition = new Vector3(facing * character.spriteOffsetX, character.spriteOffsetY, 0f);
        art.transform.localScale = new Vector3(facing * character.SpriteFlip * character.spriteScale, character.spriteScale, 1f);

        if (artOutline != null)
        {
            artOutline.enabled = showOutline;
            artOutline.style = outline;
            if (showOutline) artOutline.Apply();
        }
    }

    void OnDrawGizmos()
    {
        SyncSprite();
        if (character == null || character.def == null) return;

        CharacterDef def = character.def;
        int facing = faceRight ? 1 : -1;
        Vector3 origin = transform.position;

        if (showHurtbox) WorldView.DrawBox(def.hurtbox.ToWorld(0, 0, facing), origin, Color.green, filled);

        // ground line under the character
        Gizmos.color = Color.gray;
        Gizmos.DrawLine(origin + new Vector3(-3f, 0f, 0f), origin + new Vector3(3f, 0f, 0f));

        if (def.moves == null || def.moves.Length == 0) return;
        MoveDef move = def.moves[Mathf.Clamp(moveIndex, 0, def.moves.Length - 1)].move;
        if (move == null || move.hitboxes == null) return;

        bool anyActive = false;
        if (showHitboxes)
        {
            for (int i = 0; i < move.hitboxes.Length; i++)
            {
                HitboxDef hb = move.hitboxes[i];
                bool active = tick >= hb.spawnTick && tick < hb.spawnTick + hb.lifetime;
                anyActive |= active;

                // a projectile has moved vx * (ticks since it spawned) forward by now
                int travelled = hb.followOwner ? 0 : facing * hb.vx * Mathf.Max(0, tick - hb.spawnTick);
                if (active)
                {
                    WorldView.DrawBox(hb.box.ToWorld(travelled, 0, facing), origin, Color.red, filled);
                }
                else if (showInactiveHitboxes)
                {
                    WorldView.DrawBox(hb.box.ToWorld(0, 0, facing), origin, new Color(1f, 0.9f, 0.2f, 0.35f));
                }
            }
        }

#if UNITY_EDITOR
        string label = move.name + "  tick " + tick + " / " + move.totalTicks + (anyActive ? "   HITBOX ACTIVE" : "");
        Handles.Label(origin + new Vector3(-1f, 2.4f, 0f), label);
#endif
    }
}
