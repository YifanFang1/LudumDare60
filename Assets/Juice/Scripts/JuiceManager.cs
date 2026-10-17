using UnityEngine;

/// <summary>
/// Game juice: screen shake, hit flash, particles, impact frames.
/// <para>
/// VIEW ONLY. It reacts to IMatchSession events and never writes to the sim, the fighters or
/// Time.timeScale. Both machines raise the same sim events on the same tick, so every effect
/// shows up for both players with no networking code.
/// </para>
/// <para>
/// Call from anywhere:   JuiceManager.Shake(0.5f);   JuiceManager.Burst("Sparks", pos);
///                       JuiceManager.Attach("Aura", fighter, 120, anchor: JuiceAnchor.Center);
/// Every call does nothing if there is no JuiceManager in the scene, so it can never crash
/// another scene or tool.
/// </para>
/// Hit stop is NOT here: it changes gameplay timing, so it has to live in the sim (see Sim/).
/// </summary>
[DisallowMultipleComponent]
public class JuiceManager : MonoBehaviour
{
    #region Settings
    [Tooltip("Tuning values. Leave empty to use the defaults.")]
    [SerializeField] JuiceProfile profile;

    [Tooltip("Log every juice call to the console. Handy while the effects are still stubs.")]
    [SerializeField] bool logCalls = true;

    [Tooltip("Camera to shake. Leave empty to use the main camera (the one WorldView frames).")]
    [SerializeField] Camera shakeCamera;

    [Header("Testing (while playing: right click this component's title)")]
    [Tooltip("Effect name used by the Test menu items.")]
    [SerializeField] string testEffect = "Sparks";
    #endregion

    #region State
    static JuiceManager instance;

    IMatchSession match;
    JuiceParticles particles;
    JuiceScreenShake shake;
    readonly JuiceHandle[] lowHealthAura = new JuiceHandle[2];

    /// <summary>Same as FightControls: "Enter Play Mode without domain reload" keeps statics alive.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
    }
    #endregion

    #region Lifecycle
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[Juice] More than one JuiceManager in the scene. Only the first one is used.", this);
            enabled = false;
            return;
        }
        instance = this;

        if (profile == null) profile = ScriptableObject.CreateInstance<JuiceProfile>();
        particles = new JuiceParticles(transform);
        shake = new JuiceScreenShake(profile, shakeCamera);

        // Found the same way FightView and LobbyUI find it. Without it the API still works,
        // there are just no automatic reactions to the fight.
        match = GetComponent<IMatchSession>();
        if (match == null)
        {
            Debug.LogWarning("[Juice] JuiceManager found no IMatchSession on this object, so it will not react to fights.", this);
        }
    }

    void OnEnable()
    {
        if (match == null) return;
        match.MatchStarted += OnMatchStarted;
        match.SimEventRaised += OnSimEvent;
        match.MatchEnded += OnMatchEnded;
    }

    void OnDisable()
    {
        if (shake != null) shake.Restore();   // never leave the camera mid-shake

        if (match == null) return;
        match.MatchStarted -= OnMatchStarted;
        match.SimEventRaised -= OnSimEvent;
        match.MatchEnded -= OnMatchEnded;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>
    /// Effects run on real time, not sim ticks: they are visual only, and they keep playing
    /// while the sim is stalled waiting for the other player or frozen in hit stop.
    /// </summary>
    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // TODO: count down active hit flashes
        // TODO: count down the impact frame
    }

    /// <summary>
    /// Runs after the sim has ticked (in Update), so attached effects sit exactly where WorldView
    /// draws the fighters, and the camera shake is applied on top of wherever the camera rests.
    /// </summary>
    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        GameSim sim = match != null && match.Phase == MatchPhase.Playing ? match.Sim : null;
        if (sim != null) UpdateCharacterAuras(sim);
        particles.Tick(dt, sim);
        shake.Tick(dt);
    }
    #endregion

    #region Public API
    /// <summary>
    /// Shakes the screen. Hits add trauma, which decays over time, so rapid hits stack instead of restarting.
    /// The shake strength is trauma squared (see JuiceProfile), so 0.2 is a nudge and 1 is a big kick.
    /// </summary>
    /// <param name="trauma">Intensity of the screen shake that decays over time, values range within 0-1.</param>
    public static void Shake(float trauma)
    {
        if (instance) instance.DoShake(trauma);
    }

    /// <summary>Current screen-shake trauma, 0..1 (0 when there is no JuiceManager).</summary>
    public static float ShakeTrauma
    {
        get { return instance ? instance.shake.Trauma : 0f; }
    }

    /// <summary>
    /// Flash a specific fighter.
    /// </summary>
    /// <param name="fighter">The character of either player, player by sim index (0 or 1).</param>
    /// <param name="color">The color to flash the fighter</param>
    /// <param name="ticks">Duration of the flash: 60 ticks = 1 second</param>
    public static void Flash(int fighter, Color color, int ticks)
    {
        if (instance) instance.DoFlash(fighter, color, ticks);
    }

    /// <summary>Plays a particle effect once at a point. It cleans itself up.</summary>
    /// <param name="effect">Effect name (a JuiceEffect prefab in Assets/Juice/Resources). Not case sensitive. Empty = nothing.</param>
    /// <param name="pos">Unity units (use SimToWorld for sim positions).</param>
    /// <param name="facing">1 = right, -1 = left. Mirrors effects with Flip With Facing ticked.</param>
    /// <param name="scale">Size multiplier.</param>
    /// <param name="offset">Extra nudge in Unity units (X = forward), added to the prefab's own offset.</param>
    public static void Burst(string effect, Vector2 pos, int facing = 1, float scale = 1f, Vector2 offset = default)
    {
        if (instance) instance.DoBurst(effect, pos, facing, scale, offset);
    }

    /// <summary>
    /// Plays a particle effect once at a point on a fighter (feet, centre or head), facing the way
    /// they face. It stays where it was played; use Attach to follow the fighter.
    /// <code>JuiceManager.BurstOnFighter("Dust", fighter);                          // prefab's anchor (feet for dust)
    /// JuiceManager.BurstOnFighter("Stars", fighter, JuiceAnchor.Head, new Vector2(0f, 0.3f));</code>
    /// </summary>
    /// <param name="effect">Effect name (a JuiceEffect prefab in Assets/Juice/Resources). Not case sensitive. Empty = nothing.</param>
    /// <param name="fighter">Sim index (0 or 1).</param>
    /// <param name="anchor">Where on the fighter. Default = the anchor set on the prefab.</param>
    /// <param name="offset">Extra nudge from the anchor in Unity units (X = forward), added to the prefab's own offset.</param>
    /// <param name="scale">Size multiplier.</param>
    public static void BurstOnFighter(string effect, int fighter, JuiceAnchor anchor = JuiceAnchor.Default, Vector2 offset = default, float scale = 1f)
    {
        if (instance) instance.DoBurstOnFighter(effect, fighter, anchor, offset, scale);
    }

    /// <summary>
    /// Plays a particle effect that follows a point on a fighter (feet, centre or head).
    /// <code>JuiceManager.Attach("Aura", fighter, 120, anchor: JuiceAnchor.Center);    // 2 seconds from the chest
    /// JuiceHandle h = JuiceManager.Attach("Embers", fighter);                    // until JuiceManager.Stop(h)</code>
    /// </summary>
    /// <param name="effect">Effect name (a JuiceEffect prefab in Assets/Juice/Resources). Not case sensitive. Empty = nothing.</param>
    /// <param name="fighter">Sim index (0 or 1).</param>
    /// <param name="ticks">How long it emits (60 = 1 second). 0 = until Stop is called.</param>
    /// <param name="scale">Size multiplier.</param>
    /// <param name="anchor">Where on the fighter. Default = the anchor set on the prefab.</param>
    /// <param name="offset">Extra nudge from the anchor in Unity units (X = forward), added to the prefab's own offset.</param>
    /// <returns>A handle for Stop / IsPlaying. Safe to keep after the effect has ended.</returns>
    public static JuiceHandle Attach(string effect, int fighter, int ticks = 0, float scale = 1f,
                                     JuiceAnchor anchor = JuiceAnchor.Default, Vector2 offset = default)
    {
        return instance ? instance.DoAttach(effect, fighter, ticks, scale, anchor, offset) : JuiceHandle.None;
    }

    /// <summary>Stops an attached effect. Particles already out fade normally.</summary>
    public static void Stop(JuiceHandle handle)
    {
        if (instance) instance.particles.Stop(handle);
    }

    /// <summary>True while the effect behind this handle is still emitting.</summary>
    public static bool IsPlaying(JuiceHandle handle)
    {
        return instance && instance.particles.IsPlaying(handle);
    }

    /// <summary>True if a particle effect with this name exists in Assets/Juice/Resources.</summary>
    public static bool HasEffect(string effect)
    {
        return instance && instance.particles.Has(effect);
    }

    /// <summary>Full-screen contrast flash for big hits.</summary>
    /// <param name="ticks">Duration: 60 ticks = 1 second.</param>
    public static void ImpactFrame(int ticks)
    {
        if (instance) instance.DoImpactFrame(ticks);
    }
    #endregion

    #region Helpers
    /// <summary>Converts a sim position (milli-units, 1000 = 1 Unity unit) to Unity units.</summary>
    public static Vector2 SimToWorld(int x, int y)
    {
        return new Vector2(x * WorldView.Scale, y * WorldView.Scale);
    }

    /// <summary>
    /// A point on a fighter in Unity units: Feet (on the ground), Center (middle of the body) or
    /// Head (top of the body). Handy for Burst at a custom spot. Returns zero with no match running.
    /// </summary>
    /// <param name="fighter">Sim index (0 or 1).</param>
    public static Vector2 FighterPoint(int fighter, JuiceAnchor anchor)
    {
        if (!instance || instance.match == null || fighter < 0 || fighter > 1) return Vector2.zero;
        return JuiceParticles.FighterPoint(instance.match.Sim.fighters[fighter], anchor);
    }

    /// <summary>Converts sim ticks (60 per second) to seconds.</summary>
    public static float TicksToSeconds(int ticks)
    {
        return ticks * LockstepRunner.TickTime;
    }
    #endregion

    #region Event Handling
    void OnMatchStarted(GameSim sim)
    {
        ResetAll();
    }

    void OnMatchEnded()
    {
        ResetAll();
    }

    /// <summary>
    /// Turns sim events into juice. Events arrive right after the tick that produced them.
    /// Several ticks can run in one frame, so several events can arrive at once.
    /// </summary>
    void OnSimEvent(SimEvent e)
    {
        GameSim sim = match.Sim;

        switch (e.type)
        {
            case SimEventType.Grab:   // a landed grab: same fields as a hit, so the same juice
            case SimEventType.Hit:
            {
                // a = attacker, b = victim, (x, y) = centre of the overlap, amount = damage
                bool heavy = e.amount >= profile.heavyThreshold;
                int facing = sim.fighters[e.b].x >= sim.fighters[e.a].x ? 1 : -1;   // away from the attacker
                CharacterJuiceProfile attacker = profile.ForCharacter(sim.fighters[e.a].characterId);
                string spark = attacker == null ? "Sparks"
                             : heavy ? attacker.heavyAttack : attacker.lightAttack;
                Burst(spark, SimToWorld(e.x, e.y), facing);
                Shake(heavy ? profile.heavyHitTrauma : profile.lightHitTrauma);
                break;
            }

            case SimEventType.Block:
            {
                // a = attacker, b = blocker, (x, y) = centre of the overlap
                Shake(profile.blockTrauma);
                CharacterJuiceProfile blocker = profile.ForCharacter(sim.fighters[e.b].characterId);
                if (blocker == null) break;
                int facing = sim.fighters[e.b].x >= sim.fighters[e.a].x ? 1 : -1;   // away from the attacker
                Burst(blocker.block, SimToWorld(e.x, e.y), facing);
                break;
            }

            case SimEventType.KO:
            {
                // a = the fighter who was KO'd (see Fighter.TakeHit), b = the attacker
                Shake(profile.koTrauma);
                ImpactFrame(profile.koImpactTicks);
                break;
            }

            case SimEventType.AttackStart:
            {
                // a = attacker, (x, y) = attacker's feet
                Burst("Spawn", SimToWorld(e.x, e.y), sim.fighters[e.a].facing);
                break;
            }
        }
    }
    #endregion

    #region Implementation
    void DoShake(float trauma)
    {
        Log("Shake +" + trauma.ToString("0.00") + " (now " + Mathf.Clamp01(shake.Trauma + trauma).ToString("0.00") + ")");
        shake.Add(trauma);
    }

    void DoFlash(int fighter, Color color, int ticks)
    {
        Log("Flash fighter " + fighter + " " + color + " for " + ticks + " ticks");
        // TODO: needs the fighter's renderers from WorldView; drive _FlashColor / _FlashAmount via MaterialPropertyBlock
    }

    void DoBurst(string effect, Vector2 pos, int facing, float scale, Vector2 offset)
    {
        if (string.IsNullOrEmpty(effect)) return;   // empty slot in a profile: nothing to play
        Log("Burst " + effect + " at " + pos + " facing " + facing);
        particles.Burst(effect, pos, facing, scale, offset);
    }

    void DoBurstOnFighter(string effect, int fighter, JuiceAnchor anchor, Vector2 offset, float scale)
    {
        if (string.IsNullOrEmpty(effect)) return;
        Log("Burst " + effect + " on fighter " + fighter + " (" + anchor + ")");
        particles.BurstOnFighter(effect, fighter, anchor, offset, scale, match != null ? match.Sim : null);
    }

    JuiceHandle DoAttach(string effect, int fighter, int ticks, float scale, JuiceAnchor anchor, Vector2 offset)
    {
        if (string.IsNullOrEmpty(effect)) return JuiceHandle.None;
        Log("Attach " + effect + " to fighter " + fighter + " (" + anchor + ")" + (ticks > 0 ? " for " + ticks + " ticks" : " until stopped"));
        return particles.Attach(effect, fighter, ticks, scale, anchor, offset, match != null ? match.Sim : null);
    }

    void DoImpactFrame(int ticks)
    {
        Log("ImpactFrame for " + ticks + " ticks");
        // TODO: full-screen quad behind the fighters + fighters flashed solid black
    }

    /// <summary>
    /// Example of a persistent effect driven by a condition: starts the character's low-health
    /// aura when HP drops to the threshold, stops it if HP is above it or the fighter is KO'd.
    /// </summary>
    void UpdateCharacterAuras(GameSim sim)
    {
        for (int i = 0; i < 2; i++)
        {
            Fighter f = sim.fighters[i];
            CharacterJuiceProfile cj = profile.ForCharacter(f.characterId);

            bool want = cj != null && !string.IsNullOrEmpty(cj.lowHealthAura)
                        && f.hp > 0 && f.hp * 100 <= f.MaxHp * cj.lowHealthPercent;
            bool has = particles.IsPlaying(lowHealthAura[i]);

            if (want && !has) lowHealthAura[i] = particles.Attach(cj.lowHealthAura, i, 0, 1f, JuiceAnchor.Default, Vector2.zero, sim);
            else if (!want && has) particles.Stop(lowHealthAura[i]);
        }
    }

    void ResetAll()
    {
        Log("ResetAll");
        particles.StopAll();
        shake.Clear();
        lowHealthAura[0] = JuiceHandle.None;
        lowHealthAura[1] = JuiceHandle.None;
        // TODO: clear flashes, impact frame
    }

    void Log(string message)
    {
        if (logCalls) Debug.Log("[Juice] " + message, this);
    }
    #endregion

    #region Testing
    [ContextMenu("Test: Burst test effect at centre")]
    void TestBurst()
    {
        if (Application.isPlaying) DoBurst(testEffect, new Vector2(0f, 1f), 1, 1f, Vector2.zero);
    }

    [ContextMenu("Test: Attach test effect to P1 for 2 seconds (prefab anchor)")]
    void TestAttach()
    {
        if (Application.isPlaying) DoAttach(testEffect, 0, 120, 1f, JuiceAnchor.Default, Vector2.zero);
    }

    [ContextMenu("Test: Burst test effect on P1 at Feet")]
    void TestBurstFeet()
    {
        if (Application.isPlaying) DoBurstOnFighter(testEffect, 0, JuiceAnchor.Feet, Vector2.zero, 1f);
    }

    [ContextMenu("Test: Burst test effect on P1 at Center")]
    void TestBurstCenter()
    {
        if (Application.isPlaying) DoBurstOnFighter(testEffect, 0, JuiceAnchor.Center, Vector2.zero, 1f);
    }

    [ContextMenu("Test: Burst test effect on P1 at Head")]
    void TestBurstHead()
    {
        if (Application.isPlaying) DoBurstOnFighter(testEffect, 0, JuiceAnchor.Head, Vector2.zero, 1f);
    }

    [ContextMenu("Test: Shake (light hit)")]
    void TestShakeLight()
    {
        if (Application.isPlaying) DoShake(profile.lightHitTrauma);
    }

    [ContextMenu("Test: Shake (heavy hit)")]
    void TestShakeHeavy()
    {
        if (Application.isPlaying) DoShake(profile.heavyHitTrauma);
    }

    [ContextMenu("Test: Shake (KO)")]
    void TestShakeKO()
    {
        if (Application.isPlaying) DoShake(profile.koTrauma);
    }
    #endregion
}
