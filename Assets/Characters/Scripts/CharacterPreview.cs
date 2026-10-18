using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// BLOCKOUT TOOL, works without pressing Play.
//   1. Add this component to an empty GameObject in the scene.
//   2. Pick a character from the list (the same CharacterList the game uses: code characters like
//      the Executioner and inspector ones alike).
//   3. Pick a move with Move Index and drag the Tick slider.
// The Scene view draws the character's sprite (if it has art), its body (green) and the move's
// hitboxes at that tick: red while active, faint yellow when not. Change a character's numbers
// (in its .cs file, or the sliders on its CharacterData asset) and this updates. Tick N here is
// exactly tick N of the move in the real fight.
// Press Play in the inspector to step through the move's ticks automatically (60 per second).
// Only the sprite and boxes are previewed; the Animator of a View Prefab plays in matches.
// Outline shows the sprite with the Player or Enemy outline (colours: Fight > Outline Colors).
[ExecuteAlways]
public class CharacterPreview : MonoBehaviour
{
    [Tooltip("CharacterList id of the character to preview (the inspector shows a dropdown).")]
    public int characterId = -1;
    // Old scenes picked a CharacterData asset here. It is converted to an id the first time it is seen.
    [HideInInspector] public CharacterData character;

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

    Fighter fighter;                   // built from CharacterList for characterId
    int fighterId = int.MinValue;

    // Makes sure the roster is loaded and the fighter for characterId exists. False = nothing to show.
    bool Resolve()
    {
        if (character != null && character.def != null)
        {
            characterId = character.def.id;   // upgrade from the old asset slot
            character = null;
        }

        if (!Application.isPlaying) CharacterRegistry.EnsureLoaded();
        if (!CharacterList.Contains(characterId))
        {
            System.Collections.Generic.IList<int> ids = CharacterList.Ids;
            if (ids.Count == 0) return false;
            if (characterId >= 0) return false;   // an id that no longer exists: show nothing instead of switching silently
            characterId = ids[0];
        }

        if (fighter == null || fighterId != characterId)
        {
            fighter = CharacterList.Create(characterId);
            fighterId = characterId;
        }
        return fighter != null;
    }

    public MoveDef[] Moves()
    {
        return Resolve() ? fighter.ListMoves() : new MoveDef[0];
    }

    public Box Hurtbox { get { return Resolve() ? fighter.Hurtbox : new Box(0, 0, 0, 0); } }

    public MoveDef CurrentMove()
    {
        MoveDef[] moves = Moves();
        if (moves.Length == 0) return null;
        return moves[Mathf.Clamp(moveIndex, 0, moves.Length - 1)];
    }

    public int TotalTicks()
    {
        MoveDef move = CurrentMove();
        return move == null ? 0 : move.totalTicks;
    }

#if UNITY_EDITOR
    double lastTime;
    float tickBank;

    // Steps `tick` forward 60 times a second while "playing" (edit mode only).
    void EditorStep()
    {
        double now = EditorApplication.timeSinceStartup;
        float dt = (float)(now - lastTime);
        lastTime = now;

        if (!playing || Application.isPlaying) { tickBank = 0f; return; }
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

    // Forget the cached fighter, so the next draw builds it again (after "Reload characters").
    public void Refresh()
    {
        fighter = null;
        fighterId = int.MinValue;
    }
#endif

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

        CharacterLook look = Resolve() ? CharacterRegistry.GetLook(characterId) : null;
        bool have = look != null && look.sprite != null && showSprite;
        art.enabled = have;
        if (!have) return;

        int facing = faceRight ? 1 : -1;
        art.sprite = look.sprite;
        art.transform.localPosition = new Vector3(facing * look.spriteOffsetX, look.spriteOffsetY, 0f);
        art.transform.localScale = new Vector3(facing * look.SpriteFlip * look.spriteScale, look.spriteScale, 1f);

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
        if (!Resolve()) return;

        int facing = faceRight ? 1 : -1;
        Vector3 origin = transform.position;

        if (showHurtbox) WorldView.DrawBox(fighter.Hurtbox.ToWorld(0, 0, facing), origin, Color.green, filled);

        // ground line under the character
        Gizmos.color = Color.gray;
        Gizmos.DrawLine(origin + new Vector3(-3f, 0f, 0f), origin + new Vector3(3f, 0f, 0f));

        MoveDef move = CurrentMove();
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
        string label = CharacterList.NameOf(characterId) + ": " + move.name + "  tick " + tick + " / " + move.totalTicks + (anyActive ? "   HITBOX ACTIVE" : "");
        Handles.Label(origin + new Vector3(-1f, 2.4f, 0f), label);
#endif
    }
}
