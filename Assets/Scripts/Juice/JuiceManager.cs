using UnityEngine;

/// <summary>
/// Game juice: screen shake, hit flash, particles, impact frames.
/// <para>
/// VIEW ONLY. It reacts to IMatchSession events and never writes to the sim, the fighters or
/// Time.timeScale. Both machines raise the same sim events on the same tick, so every effect
/// shows up for both players with no networking code.
/// </para>
/// <para>
/// Call from anywhere:   JuiceManager.Shake(0.5f);
/// Every call does nothing if there is no JuiceManager in the scene, so it can never crash
/// another scene or tool.
/// </para>
/// Hit stop is NOT here: it changes gameplay timing, so it has to live in the sim (see Sim/).
/// </summary>
[DisallowMultipleComponent]
public class JuiceManager : MonoBehaviour
{
    static JuiceManager instance;

    [Tooltip("Tuning values. Leave empty to use the defaults.")]
    [SerializeField] JuiceProfile profile;

    [Tooltip("Log every juice call to the console. Handy while the effects are still stubs.")]
    [SerializeField] bool logCalls = true;

    IMatchSession match;

    JuiceParticles particles;
    readonly JuiceHandle[] lowHealthAura = new JuiceHandle[2];

    [Header("Testing (while playing: right click this component's title)")]
    [SerializeField] JuiceParticleType testEffect = JuiceParticleType.Sparks;

    // Same as FightControls: "Enter Play Mode without domain reload" keeps statics alive.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
    }

    #region Lifecycle
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[JuiceManager] More than one JuiceManager in the scene. Only the first one is used.", this);
            enabled = false;
            return;
        }
        instance = this;

        if (profile == null) profile = ScriptableObject.CreateInstance<JuiceProfile>();
        particles = new JuiceParticles(profile, transform);

        // Found the same way FightView and LobbyUI find it. Without it the API still works,
        // there are just no automatic reactions to the fight.
        match = GetComponent<IMatchSession>();
        if (match == null)
        {
            Debug.LogWarning("JuiceManager found no IMatchSession on this object, so it will not react to fights.", this);
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
        if (match == null) return;
        match.MatchStarted -= OnMatchStarted;
        match.SimEventRaised -= OnSimEvent;
        match.MatchEnded -= OnMatchEnded;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Effects run on real time, not sim ticks: they are visual only, and they keep playing
    // while the sim is stalled waiting for the other player or frozen in hit stop.
    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // TODO: decay shake trauma and move the camera rig
        // TODO: count down active hit flashes
        // TODO: count down the impact frame
    }

    // After the sim has ticked (in Update), so attached effects sit exactly where WorldView draws the fighters.
    void LateUpdate()
    {
        GameSim sim = match != null && match.Phase == MatchPhase.Playing ? match.Sim : null;
        if (sim != null) UpdateCharacterAuras(sim);
        particles.Tick(Time.unscaledDeltaTime, sim);
    }

    #endregion

    #region Public API
    /// <summary>
    /// Hits add trauma, which decays over time, so rapid hits stack instead of restarting.
    /// </summary>
    /// <param name="trauma">Intensity of the screen shake that decays over time, values range within 0-1.</param>
    public static void Shake(float trauma)
    {
        if (instance) instance.DoShake(trauma);
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
    /// <param name="pos">Unity units (use SimToWorld for sim positions).</param>
    /// <param name="facing">1 = right, -1 = left. Mirrors effects with Flip With Facing ticked.</param>
    /// <param name="scale">Size multiplier.</param>
    public static void Burst(JuiceParticleType effect, Vector2 pos, int facing = 1, float scale = 1f)
    {
        if (instance) instance.DoBurst(effect, pos, facing, scale);
    }

    /// <summary>Plays a particle effect that follows a fighter.</summary>
    /// <param name="fighter">Sim index (0 or 1).</param>
    /// <param name="ticks">How long it emits (60 = 1 second). 0 = until Stop is called.</param>
    /// <returns>A handle for Stop / IsPlaying. Safe to keep after the effect has ended.</returns>
    public static JuiceHandle Attach(JuiceParticleType effect, int fighter, int ticks = 0, float scale = 1f)
    {
        return instance ? instance.DoAttach(effect, fighter, ticks, scale) : JuiceHandle.None;
    }

    /// <summary>Stops an attached effect. Particles already out fade normally.</summary>
    public static void Stop(JuiceHandle handle)
    {
        if (instance) instance.particles.Stop(handle);
    }

    public static bool IsPlaying(JuiceHandle handle)
    {
        return instance && instance.particles.IsPlaying(handle);
    }

    // Full-screen contrast flash for big hits. ticks: 60 = 1 second.
    public static void ImpactFrame(int ticks)
    {
        if (instance) instance.DoImpactFrame(ticks);
    }
    #endregion

    #region Helpers
    // Sim positions are in milli-units (1000 = 1 Unity unit).
    public static Vector2 SimToWorld(int x, int y)
    {
        return new Vector2(x * WorldView.Scale, y * WorldView.Scale);
    }

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

    // Events arrive right after the tick that produced them. Several ticks can run in one
    // frame, so several events can arrive at once.
    void OnSimEvent(SimEvent e)
    {
        GameSim sim = match.Sim;

        switch (e.type)
        {
            case SimEventType.Hit:
            {
                // a = attacker, b = victim, (x, y) = centre of the overlap, amount = damage
                bool heavy = e.amount >= profile.heavyThreshold;
                int facing = sim.fighters[e.b].x >= sim.fighters[e.a].x ? 1 : -1;   // away from the attacker
                CharacterJuiceProfile attacker = profile.ForCharacter(sim.fighters[e.a].characterId);
                JuiceParticleType spark = attacker == null ? JuiceParticleType.Sparks
                                    : heavy ? attacker.heavyAttack : attacker.lightAttack;
                Burst(spark, SimToWorld(e.x, e.y), facing);
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
                Burst(JuiceParticleType.Spawn, SimToWorld(e.x, e.y), sim.fighters[e.a].facing);
                break;
            }
        }
    }
    #endregion

    #region Implementation
    void DoShake(float trauma)
    {
        Log("Shake " + trauma.ToString("0.00"));
        // TODO: trauma = Mathf.Min(1, trauma + amount); offset = trauma^2 * Perlin noise on a camera rig parent
    }

    void DoFlash(int fighter, Color color, int ticks)
    {
        Log("Flash fighter " + fighter + " " + color + " for " + ticks + " ticks");
        // TODO: needs the fighter's renderers from WorldView; drive _FlashColor / _FlashAmount via MaterialPropertyBlock
    }

    void DoBurst(JuiceParticleType effect, Vector2 pos, int facing, float scale)
    {
        Log("Burst " + effect + " at " + pos + " facing " + facing);
        particles.Burst(effect, pos, facing, scale);
    }

    JuiceHandle DoAttach(JuiceParticleType effect, int fighter, int ticks, float scale)
    {
        Log("Attach " + effect + " to fighter " + fighter + (ticks > 0 ? " for " + ticks + " ticks" : " until stopped"));
        return particles.Attach(effect, fighter, ticks, scale, match != null ? match.Sim : null);
    }

    // Example of a persistent effect driven by a condition: start the character's low-health
    // aura when HP drops to the threshold, stop it if HP is above it or the fighter is KO'd.
    void UpdateCharacterAuras(GameSim sim)
    {
        for (int i = 0; i < 2; i++)
        {
            Fighter f = sim.fighters[i];
            CharacterJuiceProfile cj = profile.ForCharacter(f.characterId);

            bool want = cj != null && cj.lowHealthAura != JuiceParticleType.None
                        && f.hp > 0 && f.hp * 100 <= f.MaxHp * cj.lowHealthPercent;
            bool has = particles.IsPlaying(lowHealthAura[i]);

            if (want && !has) lowHealthAura[i] = particles.Attach(cj.lowHealthAura, i, 0, 1f, sim);
            else if (!want && has) particles.Stop(lowHealthAura[i]);
        }
    }

    [ContextMenu("Test: Burst test effect at centre")]
    void TestBurst()
    {
        if (Application.isPlaying) DoBurst(testEffect, new Vector2(0f, 1f), 1, 1f);
    }

    [ContextMenu("Test: Attach test effect to P1 for 2 seconds")]
    void TestAttach()
    {
        if (Application.isPlaying) DoAttach(testEffect, 0, 120, 1f);
    }

    void DoImpactFrame(int ticks)
    {
        Log("ImpactFrame for " + ticks + " ticks");
        // TODO: full-screen quad behind the fighters + fighters flashed solid black
    }

    void ResetAll()
    {
        Log("ResetAll");
        particles.StopAll();
        lowHealthAura[0] = JuiceHandle.None;
        lowHealthAura[1] = JuiceHandle.None;
        // TODO: clear trauma, flashes, impact frame; return pooled particles
    }

    void Log(string message)
    {
        if (logCalls) Debug.Log("[Juice] " + message, this);
    }
    #endregion
}
