using System.Collections.Generic;
using UnityEngine;

// Draws the fight as real GameObjects (so you can see and select them in the Scene view).
// It only READS the sim; it never changes it.
//
//   Fighters:  a character's view prefab if it has one, else its sprite, else a coloured box sized
//              to its hurtbox (blockout mode, no art needed). The art comes from the character's
//              CharacterData, or its CodeCharacterArt for characters written in code.
//   Hitboxes:  translucent yellow boxes while they exist (switch off with showHitboxes).
//   Projectiles: the owner's projectile sprite for the move's `visual` name (CharacterLook.
//              projectileSprites), drawn ALWAYS, not only with the debug toggle.
//   Gizmos:    green hurtbox and red hitbox outlines, in the Scene view while the game runs.
//   Fighter info: text over each fighter's head (state, move, keys held, blocking or not) and a
//              log of the last hits, blocks and grabs, bottom right. Debug UI only.
//
// Scale: 1000 sim milli-units = 1 Unity unit, so a 1000 x 2000 body is 1 x 2 units.
public class WorldView : MonoBehaviour
{
    public const float Scale = 0.001f;

    public bool frameCamera = true;        // set the main camera to see the whole arena
    public bool showHurtboxes = true;      // translucent green body boxes (the coloured box itself when there is no art)
    public bool showHitboxes = true;       // translucent yellow attack boxes
    public bool showFighterInfo = true;    // text over each fighter + a log of what hit whom
    public bool showToggles = true;        // the "Debug boxes" panel (bottom left) with checkboxes for the above
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
    List<SpriteRenderer> projectileArt = new List<SpriteRenderer>();

    const int LogLines = 8;
    List<string> eventLog = new List<string>();   // newest last
    GUIStyle infoStyle;

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
        match.SimEventRaised += LogEvent;
    }

    void OnDisable()
    {
        if (match == null) return;
        match.MatchStarted -= Build;
        match.MatchEnded -= Clear;
        match.SimEventRaised -= LogEvent;
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
            if (outlineFighters && v.placeholder == null) AddOutlines(v.root, i == match.LocalSlot);
            views[i] = v;
        }
    }

    // Outlines every sprite of a fighter's art. The fighter this machine controls gets the Player
    // colours and the other one the Enemy colours, so online each player sees themselves as "Player".
    static void AddOutlines(GameObject root, bool isLocalPlayer)
    {
        foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            SpriteOutline outline = sr.GetComponent<SpriteOutline>();
            if (outline == null) outline = sr.gameObject.AddComponent<SpriteOutline>();
            outline.style = isLocalPlayer ? SpriteOutline.Style.Player : SpriteOutline.Style.Enemy;
            outline.Apply();
        }
    }

    /// <summary>The GameObject holding fighter i's art, or null between matches. JuiceManager uses it for impact frames.</summary>
    public GameObject FighterRoot(int i)
    {
        return views[i] != null ? views[i].root : null;
    }

    void Clear()
    {
        if (worldRoot != null) Destroy(worldRoot);
        worldRoot = null;
        views[0] = null;
        views[1] = null;
        hitboxQuads.Clear();
        projectileArt.Clear();
        eventLog.Clear();
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
        UpdateProjectileArt(sim);
    }

    void UpdateFighter(FighterView v, Fighter f, int index)
    {
        if (v.placeholder != null)
        {
            Color c = v.data != null ? v.data.placeholderColor
                    : (index == 0 ? new Color(0.3f, 0.5f, 1f) : new Color(1f, 0.4f, 0.4f));
            if (f.state == FighterStateId.Attack) c = Color.Lerp(c, Color.white, 0.5f);
            if (f.state == FighterStateId.Dash) c = Color.Lerp(c, Color.white, 0.25f);
            if (f.state == FighterStateId.Hitstun) c = Color.Lerp(c, Color.black, 0.35f);
            if (f.state == FighterStateId.Blockstun) c = Color.Lerp(c, Color.cyan, 0.5f);
            if (f.state == FighterStateId.KO) c = Color.gray;
            v.placeholder.enabled = showHurtboxes || !MatchScreens.DebugUI;   // with no art the box IS the body, so debug-off never hides it
            Place(v.placeholder, f.HurtboxWorld(), c);
            return;
        }

        // Sprite or prefab: both use the look's offset / scale sliders, so art can be lined up with
        // the hurtbox the same way in either mode.
        CharacterLook d = v.data;
        v.root.transform.position = new Vector3(f.x * Scale + f.facing * d.spriteOffsetX, f.y * Scale + d.spriteOffsetY, 0f);
        v.root.transform.localScale = new Vector3(f.facing * d.SpriteFlip * d.spriteScale, d.spriteScale, 1f);   // art faces right (or is flipped to)
        if (v.art == null) Pose(v, f);

        if (v.hurt != null)
        {
            bool hurtOn = showHurtboxes && MatchScreens.DebugUI;
            v.hurt.enabled = hurtOn;
            if (hurtOn) Place(v.hurt, f.HurtboxWorld(), new Color(0.2f, 1f, 0.3f, 0.35f));
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
            case FighterStateId.Dash:
                stateName = f.IsDashingForward ? "DashForward" : "DashBack";
                t = tick / (float)Mathf.Max(1, f.dashTicks);
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
        int count = showHitboxes && MatchScreens.DebugUI ? sim.hitboxes.Count : 0;

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

    // Projectile sprites, centred on the projectile's hitbox and flipped to its direction. The size
    // comes from the sprite's import settings (Pixels Per Unit), like the fighters' art.
    void UpdateProjectileArt(GameSim sim)
    {
        int used = 0;
        for (int i = 0; i < sim.hitboxes.Count; i++)
        {
            HitboxInstance h = sim.hitboxes[i];
            if (h.def.followOwner || string.IsNullOrEmpty(h.def.visual)) continue;
            FighterView owner = views[h.owner];
            Sprite sprite = owner != null && owner.data != null ? owner.data.FindProjectileSprite(h.def.visual) : null;
            if (sprite == null) continue;

            if (used == projectileArt.Count)
            {
                GameObject go = new GameObject("Projectile");
                go.transform.SetParent(worldRoot.transform, false);
                SpriteRenderer created = go.AddComponent<SpriteRenderer>();
                created.sortingOrder = 8;
                projectileArt.Add(created);
            }
            SpriteRenderer sr = projectileArt[used++];
            sr.enabled = true;
            sr.sprite = sprite;
            Box b = h.World(sim);
            sr.transform.position = new Vector3((b.x + b.w / 2f) * Scale, (b.y + b.h / 2f) * Scale, 0f);
            sr.transform.localScale = new Vector3(h.facing, 1f, 1f);   // art faces right
        }
        for (int i = used; i < projectileArt.Count; i++) projectileArt[i].enabled = false;
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
        if (!showToggles || !MatchScreens.DebugUI || match == null || match.Phase != MatchPhase.Playing) return;

        float width, height;
        GuiDraw.BeginVirtualScreen(out width, out height);

        if (showFighterInfo && views[0] != null)
        {
            if (infoStyle == null)
            {
                infoStyle = new GUIStyle(GUI.skin.label);
                infoStyle.fontSize = 11;
                infoStyle.alignment = TextAnchor.UpperCenter;
                infoStyle.normal.textColor = Color.white;
            }
            for (int i = 0; i < 2; i++) DrawFighterInfo(match.Sim.fighters[i], height);
            DrawEventLog(width, height);
        }

        // A titled panel so it's easy to spot. Hitboxes only exist for the few ticks an attack is
        // active, so with "Hitboxes" on you see a yellow flash exactly when a move can hit.
        // Offline only, the panel also gets a cheat button, so it is taller.
        float panelHeight = match.IsOnline ? 106 : 134;
        GUILayout.BeginArea(new Rect(12, height - panelHeight - 12, 230, panelHeight), "Debug boxes", GUI.skin.window);
        showHurtboxes = GUILayout.Toggle(showHurtboxes, " Hurtboxes (green: bodies)");
        showHitboxes = GUILayout.Toggle(showHitboxes, " Hitboxes (yellow: attacks)");
        showFighterInfo = GUILayout.Toggle(showFighterInfo, " Fighter info + hit log");

        // Testing cheat: fill both Ultimate meters. This changes the sim from outside Step(), which
        // would desync an online match, so the button only exists vs the AI.
        if (!match.IsOnline && GUILayout.Button("Full Ultimate meter (both)"))
        {
            match.Sim.fighters[0].meter = Fighter.MeterMax;
            match.Sim.fighters[1].meter = Fighter.MeterMax;
        }
        GUILayout.EndArea();
    }

    // ================= fighter info + hit log =================

    // A small box above the fighter's head: what they're doing, which keys they hold this tick,
    // and whether a hit landing right now would be blocked (Fighter.IsBlocking).
    void DrawFighterInfo(Fighter f, float height)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Box body = f.HurtboxWorld();
        Vector3 head = cam.WorldToScreenPoint(new Vector3((body.x + body.w / 2f) * Scale, (body.y + body.h) * Scale, 0f));
        // Screen y counts up from the bottom in real pixels; OnGUI y counts down from the top in
        // virtual pixels (see GuiDraw.BeginVirtualScreen).
        float s = Screen.height / height;
        float gx = head.x / s;
        float gy = (Screen.height - head.y) / s;

        string text = "P" + (f.index + 1) + "  " + StateText(f)
                    + "\nkeys: " + KeysText(f.prevInput)
                    + "\nHP " + f.hp + "   meter " + (f.meter * 100 / Fighter.MeterMax) + "%";
        Color back = new Color(0f, 0f, 0f, 0.6f);
        if (f.IsBlocking)
        {
            text += "\nBLOCKING";
            back = new Color(0f, 0.35f, 0.45f, 0.75f);
        }
        else if (f.holdingBack)
        {
            text += "\nholding back, but can't block now";
        }

        int lines = text.Split('\n').Length;
        Rect r = new Rect(gx - 110, gy - lines * 14 - 12, 220, lines * 14 + 6);
        GuiDraw.Fill(r.x, r.y, r.width, r.height, back);
        GUI.Label(r, text, infoStyle);
    }

    static string StateText(Fighter f)
    {
        int t = Mathf.Max(0, f.stateTick) + 1;   // 1-based, so "3/12" means the 3rd tick of 12
        switch (f.state)
        {
            case FighterStateId.Attack:
                return f.move != null ? f.move.name + " " + t + "/" + f.move.totalTicks : "Attack";
            case FighterStateId.Hitstun:
            case FighterStateId.Blockstun:
                return f.state + " " + t + "/" + f.stunTicks;
            case FighterStateId.Dash:
                return f.IsDashingForward ? "Dash forward" : "Dash back";
            default:
                return f.state.ToString();
        }
    }

    static string KeysText(int input)
    {
        string s = "";
        if ((input & Buttons.Left) != 0) s += "Left ";
        if ((input & Buttons.Right) != 0) s += "Right ";
        if ((input & Buttons.Down) != 0) s += "Down ";
        if ((input & Buttons.Light) != 0) s += "Light ";
        if ((input & Buttons.Heavy) != 0) s += "Heavy ";
        if ((input & Buttons.Grab) != 0) s += "Grab ";
        if ((input & Buttons.Special) != 0) s += "Special ";
        if ((input & Buttons.Ultimate) != 0) s += "Ult ";
        return s.Length > 0 ? s.TrimEnd() : "-";
    }

    // Turns sim events into one readable line each. Several ticks can run in one frame, so this
    // collects them as they come instead of looking at the sim once per frame.
    void LogEvent(SimEvent e)
    {
        GameSim sim = match.Sim;
        string line;
        switch (e.type)
        {
            case SimEventType.Hit:
                line = P(e.a) + " " + MoveName(sim, e.a) + " HIT " + P(e.b) + " for " + e.amount;
                break;
            case SimEventType.Block:
                line = P(e.b) + " BLOCKED " + P(e.a) + "'s " + MoveName(sim, e.a);
                break;
            case SimEventType.Grab:
                line = P(e.a) + " GRABBED " + P(e.b) + " for " + e.amount + " (grabs can't be blocked)";
                break;
            case SimEventType.Dash:
                line = P(e.a) + (e.amount == 1 ? " dashed forward" : " dashed back");
                break;
            case SimEventType.SuperFlash:
                line = P(e.a) + " started their ULTIMATE";
                break;
            case SimEventType.RoundEnd:
                line = e.a == 2 ? "Round " + e.amount + ": double KO" : "Round " + e.amount + " won by " + P(e.a);
                break;
            default:
                return;   // AttackStart is shown over the fighter's head instead
        }
        eventLog.Add("[tick " + sim.tick + "] " + line);
        if (eventLog.Count > LogLines) eventLog.RemoveAt(0);
    }

    static string P(int index)
    {
        return "P" + (index + 1);
    }

    // The attacker's current move. A projectile can land after its move ended, so that case
    // just says "projectile".
    static string MoveName(GameSim sim, int attacker)
    {
        Fighter f = sim.fighters[attacker];
        return f.state == FighterStateId.Attack && f.move != null ? f.move.name : "projectile";
    }

    void DrawEventLog(float width, float height)
    {
        float boxHeight = LogLines * 15 + 30;
        GUILayout.BeginArea(new Rect(width - 342, height - boxHeight - 12, 330, boxHeight), "Hit log", GUI.skin.window);
        for (int i = 0; i < eventLog.Count; i++) GUILayout.Label(eventLog[i], GUILayout.Height(15));
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
