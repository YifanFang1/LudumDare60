using System.Collections.Generic;
using UnityEngine;

// Draws the fight as real GameObjects (so you can see and select them in the Scene view).
// It only READS the sim; it never changes it.
//
//   Fighters:  a character's view prefab if it has one, else its sprite, else a coloured box sized
//              to its hurtbox (blockout mode, no art needed). The art comes from the character's
//              CharacterData, or its CodeCharacterArt for characters written in code.
//   Hitboxes:  translucent yellow boxes while they exist (switch off with showHitboxes).
//   Gizmos:    green hurtbox and red hitbox outlines, in the Scene view while the game runs.
//
// Scale: 1000 sim milli-units = 1 Unity unit, so a 1000 x 2000 body is 1 x 2 units.
public class WorldView : MonoBehaviour
{
    public const float Scale = 0.001f;

    public bool frameCamera = true;        // set the main camera to see the whole arena
    public bool showHurtboxes = true;      // translucent green body boxes (the coloured box itself when there is no art)
    public bool showHitboxes = true;       // translucent yellow attack boxes
    public bool showToggles = true;        // the "Debug boxes" panel (bottom left) with checkboxes for the two above
    public Material placeholderMaterial;   // optional. If boxes look black, use an Unlit sprite material

    class FighterView
    {
        public GameObject root;
        public Animator animator;
        public SpriteRenderer placeholder;   // blockout mode: coloured box, no art
        public SpriteRenderer art;           // sprite mode: CharacterLook.sprite
        public SpriteRenderer hurt;          // translucent hurtbox overlay (prefab and sprite modes)
        public CharacterLook data;           // null = no art for this character in this build
    }

    static Sprite whiteSprite;   // 1x1 white, pivot bottom-left, so scale = size

    IMatchSession match;
    GameObject worldRoot;
    FighterView[] views = new FighterView[2];
    List<SpriteRenderer> hitboxQuads = new List<SpriteRenderer>();

    void Awake()
    {
        match = GetComponent<IMatchSession>();
        if (match == null)
        {
            Debug.LogError("WorldView needs a component that implements IMatchSession (LocalMatchSession or RelayLockstepSession) on the same object.");
            enabled = false;
        }
    }

    void OnEnable()
    {
        if (match == null) return;
        match.MatchStarted += Build;
        match.MatchEnded += Clear;
    }

    void OnDisable()
    {
        if (match == null) return;
        match.MatchStarted -= Build;
        match.MatchEnded -= Clear;
        Clear();
    }

    // ================= setup / teardown =================

    void Build(GameSim sim)
    {
        Clear();
        worldRoot = new GameObject("FightWorld");
        worldRoot.transform.SetParent(transform, false);

        if (frameCamera)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.transform.position = new Vector3(0f, 3f, -10f);
            }
        }

        SpriteRenderer ground = NewQuad("Ground", -1);
        ground.color = Color.gray;
        ground.transform.position = new Vector3(-10f, -0.06f, 0f);
        ground.transform.localScale = new Vector3(20f, 0.06f, 1f);

        for (int i = 0; i < 2; i++)
        {
            FighterView v = new FighterView();
            v.data = CharacterRegistry.GetLook(sim.fighters[i].characterId);

            if (v.data != null && v.data.viewPrefab != null)
            {
                v.root = Instantiate(v.data.viewPrefab, worldRoot.transform);
                v.root.name = "Fighter" + i + " (" + v.data.name + ")";
                v.animator = v.root.GetComponentInChildren<Animator>();
                if (v.animator != null) v.animator.speed = 0f;   // the sim decides the pose, not time
            }
            else if (v.data != null && v.data.sprite != null)
            {
                GameObject go = new GameObject("Fighter" + i + " (" + v.data.name + ")");
                go.transform.SetParent(worldRoot.transform, false);
                v.art = go.AddComponent<SpriteRenderer>();
                v.art.sprite = v.data.sprite;
                v.art.sortingOrder = 0;
                v.root = go;
            }
            else
            {
                v.placeholder = NewQuad("Fighter" + i + " (placeholder)", 0);
                v.root = v.placeholder.gameObject;
            }
            if (v.placeholder == null) v.hurt = NewQuad("Hurtbox" + i, 5);
            views[i] = v;
        }
    }

    void Clear()
    {
        if (worldRoot != null) Destroy(worldRoot);
        worldRoot = null;
        views[0] = null;
        views[1] = null;
        hitboxQuads.Clear();
    }

    SpriteRenderer NewQuad(string objectName, int sortingOrder)
    {
        if (whiteSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0f), 1f);
        }

        GameObject go = new GameObject(objectName);
        go.transform.SetParent(worldRoot.transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = whiteSprite;
        sr.sortingOrder = sortingOrder;
        if (placeholderMaterial != null) sr.sharedMaterial = placeholderMaterial;
        return sr;
    }

    // ================= every frame =================

    void LateUpdate()
    {
        if (match.Phase != MatchPhase.Playing || views[0] == null) return;

        GameSim sim = match.Sim;
        for (int i = 0; i < 2; i++)
        {
            UpdateFighter(views[i], sim.fighters[i], i);
        }
        UpdateHitboxes(sim);
    }

    void UpdateFighter(FighterView v, Fighter f, int index)
    {
        if (v.placeholder != null)
        {
            Color c = v.data != null ? v.data.placeholderColor
                    : (index == 0 ? new Color(0.3f, 0.5f, 1f) : new Color(1f, 0.4f, 0.4f));
            if (f.state == FighterStateId.Attack) c = Color.Lerp(c, Color.white, 0.5f);
            if (f.state == FighterStateId.Hitstun) c = Color.Lerp(c, Color.black, 0.35f);
            if (f.state == FighterStateId.Blockstun) c = Color.Lerp(c, Color.cyan, 0.5f);
            if (f.state == FighterStateId.KO) c = Color.gray;
            v.placeholder.enabled = showHurtboxes;   // with no art the box IS the body
            Place(v.placeholder, f.HurtboxWorld(), c);
            return;
        }

        // Sprite or prefab: both use the look's offset / scale sliders, so art can be lined up with
        // the hurtbox the same way in either mode.
        CharacterLook d = v.data;
        v.root.transform.position = new Vector3(f.x * Scale + f.facing * d.spriteOffsetX, f.y * Scale + d.spriteOffsetY, 0f);
        v.root.transform.localScale = new Vector3(f.facing * d.spriteScale, d.spriteScale, 1f);   // art faces right
        if (v.art == null) Pose(v, f);

        if (v.hurt != null)
        {
            v.hurt.enabled = showHurtboxes;
            if (showHurtboxes) Place(v.hurt, f.HurtboxWorld(), new Color(0.2f, 1f, 0.3f, 0.35f));
        }
    }

    // The animation pose comes from the sim: state + how many ticks we have been in it.
    // Attack clips are scrubbed across the move's length, so the frame where a hitbox appears
    // (spawnTick / totalTicks of the way through the clip) is always the frame you see.
    void Pose(FighterView v, Fighter f)
    {
        if (v.animator == null) return;

        int tick = Mathf.Max(0, f.stateTick);
        string stateName = "Idle";
        float t = 0f;

        switch (f.state)
        {
            case FighterStateId.Idle:
                stateName = "Idle";
                t = Loop(tick, v.data.idleLoopTicks);
                break;
            case FighterStateId.Walk:
                stateName = "Walk";
                t = Loop(tick, v.data.walkLoopTicks);
                break;
            case FighterStateId.Attack:
                if (f.move != null)
                {
                    stateName = f.move.name;
                    t = tick / (float)Mathf.Max(1, f.move.totalTicks);
                }
                break;
            case FighterStateId.Hitstun:
                stateName = "Hitstun";
                t = tick / (float)Mathf.Max(1, f.stunTicks);
                break;
            case FighterStateId.Blockstun:
                stateName = "Blockstun";
                t = tick / (float)Mathf.Max(1, f.stunTicks);
                break;
            case FighterStateId.KO:
                stateName = "KO";
                t = tick / 60f;
                break;
        }

        // A half-finished Animator still works: a missing state shows Idle instead.
        if (!v.animator.HasState(0, Animator.StringToHash(stateName)))
        {
            stateName = "Idle";
            t = Loop(tick, v.data.idleLoopTicks);
        }
        if (v.animator.HasState(0, Animator.StringToHash(stateName)))
        {
            v.animator.Play(stateName, 0, Mathf.Clamp01(t));
        }
    }

    static float Loop(int tick, int length)
    {
        if (length <= 0) return 0f;
        return (tick % length) / (float)length;
    }

    void UpdateHitboxes(GameSim sim)
    {
        int count = showHitboxes ? sim.hitboxes.Count : 0;

        while (hitboxQuads.Count < count)
        {
            hitboxQuads.Add(NewQuad("Hitbox", 10));
        }

        for (int i = 0; i < hitboxQuads.Count; i++)
        {
            if (i < count)
            {
                hitboxQuads[i].enabled = true;
                Place(hitboxQuads[i], sim.hitboxes[i].World(sim), new Color(1f, 0.9f, 0.1f, 0.55f));
            }
            else
            {
                hitboxQuads[i].enabled = false;
            }
        }
    }

    static void Place(SpriteRenderer sr, Box b, Color c)
    {
        sr.transform.position = new Vector3(b.x * Scale, b.y * Scale, 0f);
        sr.transform.localScale = new Vector3(b.w * Scale, b.h * Scale, 1f);
        sr.color = c;
    }

    // ================= on-screen toggles =================

    void OnGUI()
    {
        if (!showToggles || match == null || match.Phase != MatchPhase.Playing) return;

        float width, height;
        GuiDraw.BeginVirtualScreen(out width, out height);
        // A titled panel so it's easy to spot. Hitboxes only exist for the few ticks an attack is
        // active, so with "Hitboxes" on you see a yellow flash exactly when a move can hit.
        GUILayout.BeginArea(new Rect(12, height - 96, 230, 84), "Debug boxes", GUI.skin.window);
        showHurtboxes = GUILayout.Toggle(showHurtboxes, " Hurtboxes (green: bodies)");
        showHitboxes = GUILayout.Toggle(showHitboxes, " Hitboxes (yellow: attacks)");
        GUILayout.EndArea();
    }

    // ================= Scene view outlines =================

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || match == null || match.Phase != MatchPhase.Playing) return;

        GameSim sim = match.Sim;
        for (int i = 0; i < 2; i++)
        {
            DrawBox(sim.fighters[i].HurtboxWorld(), Vector3.zero, Color.green);
        }
        for (int i = 0; i < sim.hitboxes.Count; i++)
        {
            DrawBox(sim.hitboxes[i].World(sim), Vector3.zero, Color.red);
        }
    }

    // Draws a WORLD box (see Box.ToWorld) as a wire rectangle. `offset` is added in Unity units.
    // With `filled`, also paints a translucent fill so the box is easy to see.
    public static void DrawBox(Box b, Vector3 offset, Color color, bool filled = false)
    {
        Vector3 center = offset + new Vector3((b.x + b.w / 2f) * Scale, (b.y + b.h / 2f) * Scale, 0f);
        Vector3 size = new Vector3(b.w * Scale, b.h * Scale, 0f);
        if (filled)
        {
            Gizmos.color = new Color(color.r, color.g, color.b, 0.2f);
            Gizmos.DrawCube(center, size);
        }
        Gizmos.color = color;
        Gizmos.DrawWireCube(center, size);
    }
}
