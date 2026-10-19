using System.Collections.Generic;
using UnityEngine;

// Draws the fight as real GameObjects (so you can see and select them in the Scene view).
// It only READS the sim; it never changes it.
//
//   Fighters:  a character's view prefab if it has one, else its sprite, else a placeholder box
//              sized to its hurtbox, in the character's own colour, pattern and letter
//              (PlaceholderArt). The art comes from the character's CharacterData, or its
//              CodeCharacterArt for characters written in code. A single sprite (no animation) is
//              tinted by state when the look's Tint By State is ticked.
//   Hitboxes:  translucent yellow boxes while they exist (switch off with showHitboxes).
//   Projectiles, traps, the canvas...: the owner's sprite for the hitbox's `visual` name
//              (CharacterLook.projectileSprites), else a placeholder shape. Drawn ALWAYS.
//   Zones:     paint puddles and poison clouds (GameSim.zones), as floor strips / clouds.
//   Copies:    Ultimate copies (GameSim.summons), see-through.
//   Warnings:  a red outline wherever a Telegraph event says an attack is about to land.
//   Ink:       blots over the screen while the fighter THIS machine controls is scrambled.
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
    public bool outlineFighters = true;    // Player / Enemy outlines around the art (colours: Fight > Outline Colors)
    public Material placeholderMaterial;   // optional. If boxes look black, use an Unlit sprite material
    [Range(0f, 1f)]
    [Tooltip("How see-through the grey floor line is (the sim's floor, where feet touch). 0 = hidden, 1 = solid grey.")]
    public float groundLineAlpha = 0.15f;

    class FighterView
    {
        public GameObject root;
        public Animator animator;
        public SpriteRenderer placeholder;   // blockout mode: placeholder box, no art
        public SpriteRenderer facingTab;     // blockout mode: shows which way the box faces
        public SpriteRenderer art;           // sprite mode: CharacterLook.sprite
        public SpriteRenderer hurt;          // translucent hurtbox overlay (prefab and sprite modes)
        public CharacterLook data;           // null = no art for this character in this build
        public SpriteRenderer halo;          // Noir's glowing yellow halo while Rapid Fire is on (view only)
    }

    static Sprite haloSprite;   // soft round glow, pivot in the middle

    static Sprite HaloSprite()
    {
        if (haloSprite != null) return haloSprite;
        const int n = 128;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = d >= 1f ? 0f : Mathf.Pow(1f - d, 1.6f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        haloSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return haloSprite;
    }

    static Sprite whiteSprite;   // 1x1 white, pivot bottom-left, so scale = size

    IMatchSession match;
    GameObject worldRoot;
    FighterView[] views = new FighterView[2];
    List<SpriteRenderer> hitboxQuads = new List<SpriteRenderer>();
    List<SpriteRenderer> projectileArt = new List<SpriteRenderer>();

    // Placed hitboxes whose art outlasts them (ProjectileSprite.lingerSeconds): view only.
    class ProjectileGhost
    {
        public ProjectileSprite entry;
        public int facing;
        public Vector3 position;
        public float startTime;
    }
    List<ProjectileGhost> ghosts = new List<ProjectileGhost>();
    HashSet<long> ghostSeen = new HashSet<long>();      // hitboxes that were alive last frame
    HashSet<long> ghostSeenNow = new HashSet<long>();
    List<SpriteRenderer> zoneQuads = new List<SpriteRenderer>();
    List<SpriteRenderer> summonQuads = new List<SpriteRenderer>();
    List<SpriteRenderer> telegraphQuads = new List<SpriteRenderer>();

    // Move flashes (CharacterLook.moveFlashes): an image over a fighter's hurtbox that fades out. View only.
    class ActiveFlash
    {
        public int owner;
        public MoveFlash flash;
        public MoveDef move;
        public float startTime;
        public SpriteRenderer sr;
    }
    List<ActiveFlash> flashes = new List<ActiveFlash>();

    // A Telegraph event, remembered until its attack lands.
    struct Warning
    {
        public int x;
        public int width;
        public int untilTick;
    }
    List<Warning> warnings = new List<Warning>();

    const int LogLines = 8;
    List<string> eventLog = new List<string>();   // newest last
    GUIStyle infoStyle;

    void Awake()
    {
        if (GetComponent<MatchCinematics>() == null) gameObject.AddComponent<MatchCinematics>();
        if (GetComponent<DefeatSequence>() == null) gameObject.AddComponent<DefeatSequence>();
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
        ground.color = new Color(0.5f, 0.5f, 0.5f, groundLineAlpha);   // faint, so the stage art's own floor shows
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
                Debug.LogWarning("Couldn't find sprite or prefab for characterId: " + sim.fighters[i].characterId);
                v.placeholder = NewQuad("Fighter" + i + " (placeholder)", 0);
                v.placeholder.sprite = PlaceholderArt.FighterSprite(sim.fighters[i].characterId);
                v.facingTab = NewQuad("Facing" + i, 1);
                v.root = v.placeholder.gameObject;
            }
            if (v.placeholder == null) v.hurt = NewQuad("Hurtbox" + i, 5);
            if (sim.fighters[i].characterId == Cowboy.Id)
            {
                v.halo = NewQuad("Halo" + i, -1);   // behind the fighter
                v.halo.sprite = HaloSprite();
                v.halo.enabled = false;
            }
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
        ghosts.Clear();
        ghostSeen.Clear();
        ghostSeenNow.Clear();
        zoneQuads.Clear();
        summonQuads.Clear();
        telegraphQuads.Clear();
        flashes.Clear();
        warnings.Clear();
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
        UpdateMoveFlashes(sim);
        UpdateZones(sim);
        UpdateSummons(sim);
        UpdateWarnings(sim);
    }

    void UpdateFighter(FighterView v, Fighter f, int index)
    {
        UpdateHalo(v, f);
        if (v.placeholder != null)
        {
            Color c = StateTint(BaseColor(v, f), f);
            bool show = showHurtboxes || !MatchScreens.DebugUI;   // with no art the box IS the body, so debug-off never hides it
            v.placeholder.enabled = show;
            Box body = f.HurtboxWorld();
            PlaceSprite(v.placeholder, body, c);

            // A dark tab near the top of the box, on the side it faces.
            v.facingTab.enabled = show;
            int tabW = body.w / 6;
            int tabX = f.facing > 0 ? body.x + body.w - tabW : body.x;
            Place(v.facingTab, new Box(tabX, body.y + body.h * 3 / 4, tabW, body.h / 8), new Color(0f, 0f, 0f, 0.65f));
            return;
        }

        // Sprite or prefab: both use the look's offset / scale sliders, so art can be lined up with
        // the hurtbox the same way in either mode.
        CharacterLook d = v.data;
        v.root.transform.position = new Vector3(f.x * Scale + f.facing * d.spriteOffsetX, f.y * Scale + d.spriteOffsetY, 0f);
        float grow = f.ScalePercent / 100f;   // a grown fighter (Room's Ultimate) draws bigger, from the feet
        v.root.transform.localScale = new Vector3(f.facing * d.SpriteFlip * d.spriteScale * grow, d.spriteScale * grow, 1f);   // art faces right (or is flipped to)
        if (v.art == null) Pose(v, f);
        else v.art.color = d.tintByState ? StateTint(Color.white, f) : Color.white;

        if (v.hurt != null)
        {
            bool hurtOn = showHurtboxes && MatchScreens.DebugUI;
            v.hurt.enabled = hurtOn;
            if (hurtOn) Place(v.hurt, f.HurtboxWorld(), new Color(0.2f, 1f, 0.3f, 0.35f));
        }
    }

    // Noir's Rapid Fire: a pulsing yellow glow around the hurtbox for as long as the buff lasts.
    void UpdateHalo(FighterView v, Fighter f)
    {
        if (v.halo == null) return;
        bool on = f.IsPoweredUp;
        v.halo.enabled = on;
        if (!on) return;
        Box b = f.HurtboxWorld();
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
        float diameter = Mathf.Max(b.w, b.h) * Scale * (1.7f + 0.15f * pulse);
        float unit = v.halo.sprite.bounds.size.x;
        v.halo.transform.position = new Vector3((b.x + b.w * 0.5f) * Scale, (b.y + b.h * 0.5f) * Scale, 0f);
        v.halo.transform.localScale = new Vector3(diameter / unit, diameter / unit, 1f);
        v.halo.color = new Color(1f, 0.85f, 0.15f, 0.65f + 0.3f * pulse);
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
            case FighterStateId.Jump:
                // Scrubbed by height: rising plays the first half of the clip, falling the second.
                // (The jump state starts again after an air attack, so its tick isn't useful here.)
                stateName = "Jump";
                float up = Mathf.Max(1, f.JumpArc.up);
                t = 0.5f - Mathf.Clamp(f.vy / up, -1f, 1f) * 0.5f;
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
            case FighterStateId.Held:
                // Pinned by a held grab. An Animator without a Held state shows Hitstun instead.
                stateName = v.animator.HasState(0, Animator.StringToHash("Held")) ? "Held" : "Hitstun";
                t = 0f;
                break;
        }

        MatchCinematics cinematic = GetComponent<MatchCinematics>();
        if (cinematic != null && (cinematic.IntroActive ||
            (cinematic.OutroActive && (f.hp > 0 || !cinematic.LoserLineFinished))))
        {
            stateName = "Idle";
            t = Mathf.Repeat(Time.unscaledTime, 1f);
        }

        // A half-finished Animator still works: a missing state shows Idle instead.
        if (!v.animator.HasState(0, Animator.StringToHash(stateName)))
        {
            stateName = "Idle";
            t = Loop(tick, v.data.idleLoopTicks);
        }
        // Powered up (K3N's Install, Noir's Rapid Fire): use the "Install_<State>" Animator state
        // when the character has one (Install_Idle, Install_Light...), else the normal state.
        if (f.IsPoweredUp)
        {
            string powered = PoweredPrefix + stateName;
            if (v.animator.HasState(0, Animator.StringToHash(powered))) stateName = powered;
        }
        if (v.animator.HasState(0, Animator.StringToHash(stateName)))
        {
            v.animator.Play(stateName, 0, Mathf.Clamp01(t));
        }
    }

    // Name prefix of the Animator states used while a fighter IsPoweredUp.
    const string PoweredPrefix = "Install_";

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

    // Art for every hitbox with a `visual` name (projectiles, traps, the canvas...), centred on the
    // hitbox and flipped to its direction. A real sprite keeps the size from its import settings
    // (Pixels Per Unit), like the fighters' art; a placeholder shape is stretched to the hitbox.
    void UpdateProjectileArt(GameSim sim)
    {
        int used = 0;
        for (int i = 0; i < sim.hitboxes.Count; i++)
        {
            HitboxInstance h = sim.hitboxes[i];
            if (string.IsNullOrEmpty(h.def.visual)) continue;
            FighterView owner = views[h.owner];
            ProjectileSprite entry = owner != null && owner.data != null ? owner.data.FindProjectileEntry(h.def.visual) : null;
            Sprite sprite = entry != null ? entry.sprite : null;

            // A placed hitbox whose art lingers (or animates): remember it once, draw it from the ghost list below.
            if (entry != null && entry.lingerSeconds > 0f && !h.FollowsOwner)
            {
                long key = ((long)h.owner * 73856093L) ^ ((long)h.def.GetHashCode() * 19349663L) ^ ((long)h.originX * 83492791L) ^ ((long)h.originY * 2654435761L);
                ghostSeenNow.Add(key);
                if (!ghostSeen.Contains(key))
                {
                    Box gb = h.World(sim);
                    ghosts.Add(new ProjectileGhost
                    {
                        entry = entry, facing = h.facing, startTime = Time.time,
                        position = new Vector3((gb.x + gb.w / 2f) * Scale, (gb.y + gb.h / 2f) * Scale, 0f),
                    });
                }
                continue;
            }

            bool placeholder = sprite == null;
            if (placeholder) sprite = PlaceholderArt.ShapeSprite(h.def.visual);

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
            if (placeholder)
            {
                // Charged shots (more damage than the move's own) look a bit bigger. View only.
                float grow = h.def.damage > 0 && h.damage > h.def.damage ? 1f + 0.5f * (h.damage - h.def.damage) / h.def.damage : 1f;
                sr.transform.localScale = new Vector3(h.facing * b.w * Scale * grow, b.h * Scale * grow, 1f);
                Color c = OwnerColor(sim, h.owner);
                sr.color = h.age < h.def.armTicks ? new Color(c.r, c.g, c.b, 0.35f) : Color.Lerp(c, Color.white, 0.35f);
            }
            else
            {
                float size = entry != null && entry.scale > 0f ? entry.scale : 1f;
                float dir = entry != null && entry.flip ? -h.facing : h.facing;   // art faces right unless `flip`
                sr.transform.localScale = new Vector3(dir * size, size, 1f);
                sr.color = Color.white;
            }
        }

        // the animated / lingering ones
        HashSet<long> swap = ghostSeen; ghostSeen = ghostSeenNow; ghostSeenNow = swap; ghostSeenNow.Clear();
        for (int g = ghosts.Count - 1; g >= 0; g--)
        {
            ProjectileGhost gh = ghosts[g];
            float elapsed = Time.time - gh.startTime;
            if (elapsed > gh.entry.lingerSeconds) { ghosts.RemoveAt(g); continue; }
            Sprite frame = gh.entry.sprite;
            if (gh.entry.frames != null && gh.entry.frames.Length > 0)
            {
                int n = gh.entry.frames.Length;
                int idx = Mathf.FloorToInt(elapsed * Mathf.Max(0.01f, gh.entry.framesPerSecond));
                idx = gh.entry.loop ? idx % n : Mathf.Min(idx, n - 1);
                frame = gh.entry.frames[idx];
            }
            if (frame == null) continue;
            if (used == projectileArt.Count)
            {
                GameObject go = new GameObject("Projectile");
                go.transform.SetParent(worldRoot.transform, false);
                SpriteRenderer created = go.AddComponent<SpriteRenderer>();
                created.sortingOrder = 8;
                projectileArt.Add(created);
            }
            SpriteRenderer gsr = projectileArt[used++];
            gsr.enabled = true;
            gsr.sprite = frame;
            gsr.transform.position = gh.position;
            float gsize = gh.entry.scale > 0f ? gh.entry.scale : 1f;
            float gdir = gh.entry.flip ? -gh.facing : gh.facing;
            gsr.transform.localScale = new Vector3(gdir * gsize, gsize, 1f);
            gsr.color = Color.white;
        }
        for (int i = used; i < projectileArt.Count; i++) projectileArt[i].enabled = false;
    }

    void StartMoveFlash(int fighter)
    {
        if (worldRoot == null || fighter < 0 || fighter > 1) return;
        FighterView v = views[fighter];
        Fighter f = match.Sim.fighters[fighter];
        if (v == null || v.data == null || f.move == null) return;
        MoveFlash mf = v.data.FindMoveFlash(f.move.name);
        if (mf == null) return;
        ActiveFlash a = new ActiveFlash();
        a.owner = fighter;
        a.flash = mf;
        a.move = f.move;
        a.startTime = Time.time;
        GameObject go = new GameObject("MoveFlash");
        go.transform.SetParent(worldRoot.transform, false);
        a.sr = go.AddComponent<SpriteRenderer>();
        a.sr.sprite = mf.sprite;
        a.sr.sortingOrder = 9;
        flashes.Add(a);
    }

    // Each flash is stretched to the move's attack area (the box that hurts enemies; all of the move's
    // hitboxes combined, scaled up if the fighter is grown), follows the fighter, and fades to nothing.
    void UpdateMoveFlashes(GameSim sim)
    {
        for (int i = flashes.Count - 1; i >= 0; i--)
        {
            ActiveFlash a = flashes[i];
            float fade = Mathf.Max(0.01f, a.flash.fadeSeconds);
            float t = (Time.time - a.startTime) / fade;
            if (t >= 1f || a.sr == null)
            {
                if (a.sr != null) Destroy(a.sr.gameObject);
                flashes.RemoveAt(i);
                continue;
            }
            Fighter fo = sim.fighters[a.owner];
            Box b = MoveAttackArea(a.move, fo);
            Vector3 size = a.sr.sprite.bounds.size;
            a.sr.transform.position = new Vector3((b.x + b.w / 2f) * Scale, (b.y + b.h / 2f) * Scale, 0f);
            a.sr.transform.localScale = new Vector3(b.w * Scale / size.x, b.h * Scale / size.y, 1f);
            a.sr.color = new Color(1f, 1f, 1f, 1f - t);
        }
    }

    // The world box covering all of `m`'s hitboxes (milli-units), grown with the fighter's ScalePercent.
    // Falls back to the body hurtbox for a move with no hitboxes.
    static Box MoveAttackArea(MoveDef m, Fighter f)
    {
        if (m == null || m.hitboxes == null || m.hitboxes.Length == 0) return f.HurtboxWorld();
        int grow = f.ScalePercent;
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
        for (int i = 0; i < m.hitboxes.Length; i++)
        {
            Box b = m.hitboxes[i].box;
            Box w = new Box(b.x * grow / 100, b.y * grow / 100, b.w * grow / 100, b.h * grow / 100).ToWorld(f.x, f.y, f.facing);
            if (w.x < x0) x0 = w.x;
            if (w.y < y0) y0 = w.y;
            if (w.x + w.w > x1) x1 = w.x + w.w;
            if (w.y + w.h > y1) y1 = w.y + w.h;
        }
        return new Box(x0, y0, x1 - x0, y1 - y0);
    }

    // ================= zones, Ultimate copies, warnings =================

    void UpdateZones(GameSim sim)
    {
        while (zoneQuads.Count < sim.zones.Count) zoneQuads.Add(NewQuad("Zone", -1));
        for (int i = 0; i < zoneQuads.Count; i++)
        {
            bool on = i < sim.zones.Count;
            zoneQuads[i].enabled = on;
            if (!on) continue;
            Zone z = sim.zones[i];
            Color c;
            float height;
            PlaceholderArt.ZoneLook(z.visual, OwnerColor(sim, z.owner), out c, out height);
            if (z.ticksLeft < 30) c.a *= z.ticksLeft / 30f;   // fade out at the end
            Place(zoneQuads[i], new Box(z.x - z.halfWidth, 0, z.halfWidth * 2, (int)(height / Scale)), c);
        }
    }

    // Copies are drawn like their owner, see-through: the owner's sprite if it has one, else its
    // placeholder box.
    void UpdateSummons(GameSim sim)
    {
        while (summonQuads.Count < sim.summons.Count) summonQuads.Add(NewQuad("Copy", -1));
        for (int i = 0; i < summonQuads.Count; i++)
        {
            bool on = i < sim.summons.Count;
            SpriteRenderer sr = summonQuads[i];
            sr.enabled = on;
            if (!on) continue;

            Summon s = sim.summons[i];
            Fighter owner = sim.fighters[s.owner];
            CharacterLook look = views[s.owner] != null ? views[s.owner].data : null;
            Box body = sim.SummonBody(s);
            float alpha = s.move != null ? 0.7f : 0.45f;   // a bit more solid while attacking
            if (look != null && look.sprite != null)
            {
                sr.sprite = look.sprite;
                sr.transform.position = new Vector3(s.x * Scale + s.facing * look.spriteOffsetX, look.spriteOffsetY, 0f);
                sr.transform.localScale = new Vector3(s.facing * look.SpriteFlip * look.spriteScale, look.spriteScale, 1f);
                sr.color = new Color(1f, 1f, 1f, alpha);
            }
            else
            {
                sr.sprite = PlaceholderArt.FighterSprite(owner.characterId);
                Color c = OwnerColor(sim, s.owner);
                PlaceSprite(sr, body, new Color(c.r, c.g, c.b, alpha));
            }
        }
    }

    void UpdateWarnings(GameSim sim)
    {
        for (int i = warnings.Count - 1; i >= 0; i--)
        {
            if (sim.tick >= warnings[i].untilTick) warnings.RemoveAt(i);
        }
        while (telegraphQuads.Count < warnings.Count) telegraphQuads.Add(NewQuad("Warning", 9));
        float pulse = 0.25f + 0.25f * Mathf.PingPong(Time.unscaledTime * 6f, 1f);
        for (int i = 0; i < telegraphQuads.Count; i++)
        {
            bool on = i < warnings.Count;
            telegraphQuads[i].enabled = on;
            if (!on) continue;
            Warning w = warnings[i];
            Place(telegraphQuads[i], new Box(w.x - w.width / 2, 0, w.width, 2400), new Color(1f, 0.1f, 0.1f, pulse));
        }
    }

    // ================= colours =================

    // The fighter's own colour: its art asset's placeholder colour if it has an asset, else the
    // PlaceholderArt style for its character.
    static Color BaseColor(FighterView v, Fighter f)
    {
        return v.data != null ? v.data.placeholderColor : PlaceholderArt.StyleFor(f.characterId).color;
    }

    Color OwnerColor(GameSim sim, int owner)
    {
        FighterView v = views[owner];
        return v != null ? BaseColor(v, sim.fighters[owner]) : Color.white;
    }

    // State shows as a tint on boxes and single sprites (no animation to show it otherwise).
    static Color StateTint(Color c, Fighter f)
    {
        if (f.state == FighterStateId.Attack) c = Color.Lerp(c, Color.white, f.charging ? 0.2f + 0.6f * Mathf.Min(1f, f.chargeTicks / 60f) : 0.5f);
        if (f.state == FighterStateId.Dash || f.state == FighterStateId.Jump) c = Color.Lerp(c, Color.white, 0.25f);
        if (f.state == FighterStateId.Hitstun || f.state == FighterStateId.Held) c = Color.Lerp(c, Color.black, 0.35f);
        if (f.state == FighterStateId.Blockstun) c = Color.Lerp(c, Color.cyan, 0.5f);
        if (f.IsPoweredUp) c = Color.Lerp(c, new Color(1f, 0.85f, 0.2f), 0.3f + 0.2f * Mathf.PingPong(Time.unscaledTime * 4f, 1f));
        if (f.IsIntangible) c.a = 0.55f;
        if (f.state == FighterStateId.KO) c = Color.gray;
        return c;
    }

    static void Place(SpriteRenderer sr, Box b, Color c)
    {
        sr.transform.position = new Vector3(b.x * Scale, b.y * Scale, 0f);
        sr.transform.localScale = new Vector3(b.w * Scale, b.h * Scale, 1f);
        sr.color = c;
    }

    // Like Place, for a sprite of any size with a bottom-left pivot (the placeholder boxes).
    static void PlaceSprite(SpriteRenderer sr, Box b, Color c)
    {
        Vector2 size = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        sr.transform.position = new Vector3(b.x * Scale, b.y * Scale, 0f);
        sr.transform.localScale = new Vector3(b.w * Scale / size.x, b.h * Scale / size.y, 1f);
        sr.color = c;
    }

    // ================= on-screen toggles =================

    void OnGUI()
    {
        if (match == null || match.Phase != MatchPhase.Playing) return;
        DrawInk();
        if (!showToggles || !MatchScreens.DebugUI) return;

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

    // Paintress's Ultimate: ink over the screen of the player who was splatted, and only theirs
    // (online each machine draws its own player). It doesn't touch the sim, so it does nothing to
    // the AI. The blots are placed from the scramble's code, so they stay still while it lasts.
    void DrawInk()
    {
        if (views[0] == null) return;
        Fighter me = match.Sim.fighters[match.LocalSlot];
        if (me.scrambleTicks <= 0) return;

        float width, height;
        GuiDraw.BeginVirtualScreen(out width, out height);
        float alpha = Mathf.Min(1f, me.scrambleTicks / 30f) * 0.5f;   // "slightly" blind: you can still see through it
        Color ink = new Color(0.12f, 0.02f, 0.18f, alpha);
        uint seed = (uint)me.scrambleCode * 2654435761u + 1u;
        for (int i = 0; i < 5; i++)
        {
            seed = seed * 1664525u + 1013904223u;
            float x = (seed % 1000) / 1000f * (width - 220);
            seed = seed * 1664525u + 1013904223u;
            float y = (seed % 1000) / 1000f * (height - 160);
            seed = seed * 1664525u + 1013904223u;
            float size = 90 + (seed % 120);
            // A rough blot: a few overlapping rectangles.
            GuiDraw.Fill(x, y + size * 0.2f, size, size * 0.6f, ink);
            GuiDraw.Fill(x + size * 0.2f, y, size * 0.6f, size, ink);
        }
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
        string status = FighterStatusText.For(f);
        if (status.Length > 0) text += "\n" + status;
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
                if (f.move != null && f.charging) return f.move.name + " charging " + f.chargeTicks;
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
        if ((input & Buttons.Up) != 0) s += "Up ";
        if ((input & Buttons.Down) != 0) s += "Down ";
        if ((input & Buttons.Light) != 0) s += "Light ";
        if ((input & Buttons.Heavy) != 0) s += "Heavy ";
        if ((input & Buttons.Grab) != 0) s += "Grab ";
        if ((input & Buttons.Special) != 0) s += "Special ";
        if ((input & Buttons.Ultimate) != 0) s += "Ult ";
        if ((input & Buttons.Dash) != 0) s += "Dash ";
        return s.Length > 0 ? s.TrimEnd() : "-";
    }

    // Turns sim events into one readable line each. Several ticks can run in one frame, so this
    // collects them as they come instead of looking at the sim once per frame.
    void LogEvent(SimEvent e)
    {
        GameSim sim = match.Sim;
        if (e.type == SimEventType.AttackStart) StartMoveFlash(e.a);
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
            case SimEventType.Telegraph:
                Warning w = new Warning();
                w.x = e.x;
                w.width = e.amount;
                w.untilTick = sim.tick + e.b;
                warnings.Add(w);
                line = P(e.a) + "'s attack lands here in " + e.b + " ticks";
                break;
            case SimEventType.Reflect:
                line = P(e.a) + " REFLECTED " + P(e.b) + "'s projectile";
                break;
            case SimEventType.ProjectileDestroyed:
                line = P(e.a) + " destroyed " + P(e.b) + "'s projectile";
                break;
            case SimEventType.Hold:
                line = P(e.a) + " is HOLDING " + P(e.b);
                break;
            case SimEventType.SummonSpawn:
                line = P(e.a) + " summoned a copy";
                break;
            case SimEventType.SummonDestroyed:
                line = e.b >= 0 ? P(e.b) + " destroyed a copy" : P(e.a) + "'s copy faded";
                break;
            case SimEventType.Heal:
                line = P(e.a) + " healed " + e.amount;
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
