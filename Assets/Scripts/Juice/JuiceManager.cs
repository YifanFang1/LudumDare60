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

    // effect is a name from the JuiceProfile's effect list (see JuiceFx for the built-in names).
    // pos is in Unity units (use SimToWorld for sim positions). facing: 1 = right, -1 = left.
    public static void Burst(JuiceParticleType effect, Vector2 pos, int facing = 1)
    {
        if (instance) instance.DoBurst(effect, pos, facing);
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

    void DoBurst(JuiceParticleType effect, Vector2 pos, int facing)
    {
        Log("Burst " + effect + " at " + pos + " facing " + facing);
        // TODO: look up the prefab in profile.effects, take one from a pool, place, flip by facing, play
    }

    void DoImpactFrame(int ticks)
    {
        Log("ImpactFrame for " + ticks + " ticks");
        // TODO: full-screen quad behind the fighters + fighters flashed solid black
    }

    void ResetAll()
    {
        Log("ResetAll");
        // TODO: clear trauma, flashes, impact frame; return pooled particles
    }

    void Log(string message)
    {
        if (logCalls) Debug.Log("[Juice] " + message, this);
    }
    #endregion
}
