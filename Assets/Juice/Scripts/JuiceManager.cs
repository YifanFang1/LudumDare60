using System;
using System.Collections.Generic;
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
///                       JuiceManager.Trigger("PoisonCloud", fighter);   // plays that character's rows
/// Every call does nothing if there is no JuiceManager in the scene, so it can never crash
/// another scene or tool.
/// </para>
/// <para>
/// Most juice needs no code: each character's CharacterJuiceProfile has rows of
/// "when TRIGGER happens, play EFFECT". Triggers come from sim events (Hit, Block, Dash, ...),
/// from watching the fighters (EnterHitstun, WhileLowHealth, ...) and from code (Trigger above).
/// </para>
/// Hit stop is NOT here: it changes gameplay timing, so it has to live in the sim (see Sim/).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(75)]   // shake after FightCamera (50) moves the camera, or it would overwrite the shake
public class JuiceManager : MonoBehaviour
{
    #region Settings
    [Tooltip("Tuning values. Leave empty to use the defaults.")]
    [SerializeField] JuiceProfile profile;

    [Tooltip("Log every juice call to the console. Handy while the effects are still stubs.")]
    [SerializeField] bool logCalls = true;

    [Tooltip("Log EVERY moment trigger as it fires, even ones no profile reacts to, e.g. \"[Juice] Trigger EnterHitstun (P2)\". " +
             "Use it to discover trigger names; it is chatty, so leave it off normally.")]
    [SerializeField] bool logAllTriggers = false;

    [Tooltip("Camera to shake. Leave empty to use the main camera (the one WorldView frames).")]
    [SerializeField] Camera shakeCamera;

    [Header("Testing (while playing: right click this component's title)")]
    [Tooltip("Effect name used by the Test menu items.")]
    [SerializeField] string testEffect = "Sparks";
    [Tooltip("Trigger name used by \"Test: Fire test trigger on P1\" (plays P1's reaction rows for it).")]
    [SerializeField] string testTrigger = "Hit";
    #endregion

    #region State
    static JuiceManager instance;

    IMatchSession match;
    JuiceParticles particles;
    JuiceScreenShake shake;

    // Per fighter (0 and 1): the reaction rows its character uses, which character they were built
    // for, the state it was in last frame (for Enter... triggers), the While... triggers true right
    // now, and the effect each While... row is playing.
    readonly List<JuiceReaction>[] reactions = { new List<JuiceReaction>(), new List<JuiceReaction>() };
    readonly int[] reactionsFor = { int.MinValue, int.MinValue };
    readonly FighterStateId?[] lastState = new FighterStateId?[2];
    readonly HashSet<string>[] whileActive =
    {
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
    };
    readonly Dictionary<JuiceReaction, JuiceHandle>[] whilePlaying =
    {
        new Dictionary<JuiceReaction, JuiceHandle>(),
        new Dictionary<JuiceReaction, JuiceHandle>(),
    };
    readonly Dictionary<JuiceReaction, int>[] whileNextTick =   // Every Ticks rows: sim tick of the next burst
    {
        new Dictionary<JuiceReaction, int>(),
        new Dictionary<JuiceReaction, int>(),
    };
    readonly List<JuiceReaction> matched = new List<JuiceReaction>();   // scratch list, reused
    JuiceImpactFrame impactFrame;
    WorldView world;            // where the fighters' art lives, for the impact frame

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
        impactFrame = new JuiceImpactFrame(profile, shakeCamera, particles.Root);
        world = GetComponent<WorldView>();
        if (world == null) world = FindAnyObjectByType<WorldView>();

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
        if (impactFrame != null) impactFrame.Stop();   // or the fighters stuck as silhouettes

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
        impactFrame.Tick(dt);
    }

    /// <summary>
    /// Runs after the sim has ticked (in Update), so attached effects sit exactly where WorldView
    /// draws the fighters, and the camera shake is applied on top of wherever the camera rests.
    /// </summary>
    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        GameSim sim = match != null && match.Phase == MatchPhase.Playing ? match.Sim : null;
        if (sim != null) UpdateStateTriggers(sim);
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

    /// <summary>
    /// Fires a moment trigger for a fighter: every reaction row for <paramref name="trigger"/> in that
    /// character's juice profile plays. Use it for character-specific moments the sim doesn't report.
    /// <code>JuiceManager.Trigger("PoisonCloud", fighter);</code>
    /// Rows set to At Event play at the fighter's anchor, since there is no event point.
    /// </summary>
    /// <param name="trigger">Any name. Not case sensitive. "While..." names are reserved for conditions and ignored here.</param>
    /// <param name="fighter">Sim index (0 or 1).</param>
    public static void Trigger(string trigger, int fighter)
    {
        if (instance) instance.FireFromCode(trigger, fighter, false, Vector2.zero, 0);
    }

    /// <summary>
    /// Fires a moment trigger for a fighter at a specific point. Rows set to At Event play at
    /// <paramref name="point"/>; On Self / On Opponent rows play on the fighters as usual.
    /// </summary>
    /// <param name="trigger">Any name. Not case sensitive.</param>
    /// <param name="fighter">Sim index (0 or 1).</param>
    /// <param name="point">Unity units (use SimToWorld or FighterPoint).</param>
    /// <param name="facing">1 = right, -1 = left, 0 = the way the fighter faces.</param>
    public static void Trigger(string trigger, int fighter, Vector2 point, int facing = 0)
    {
        if (instance) instance.FireFromCode(trigger, fighter, true, point, facing);
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
    /// <para>
    /// Two parts: the GLOBAL feel every fight gets (hit shake, hill tilt, KO impact), tuned on the
    /// JuiceProfile, then the TRIGGERS that each character's reaction rows respond to.
    /// </para>
    /// </summary>
    void OnSimEvent(SimEvent e)
    {
        GameSim sim = match.Sim;
        ApplyGlobalFeel(e, sim);
        FireEventTriggers(e, sim);
    }

    // The shake, hill tilt and impact frames every fight gets, whatever the characters' profiles say.
    void ApplyGlobalFeel(SimEvent e, GameSim sim)
    {
        switch (e.type)
        {
            case SimEventType.Grab:   // a landed grab: same fields as a hit, so the same feel
            case SimEventType.Hit:
            {
                // a = attacker, b = victim, (x, y) = centre of the overlap, amount = damage
                bool heavy = e.amount >= profile.heavyThreshold;
                int facing = AwayFrom(sim, e.a, e.b);
                Shake(heavy ? profile.heavyHitTrauma : profile.lightHitTrauma);
                if (IsUltimate(sim.fighters[e.a])) UltimateImpact(SimToWorld(e.x, e.y));
                shake.AddHillTilt(-facing);   // facing is +1 when the attacker is on the left
                break;
            }

            case SimEventType.Block:
                Shake(profile.blockTrauma);
                if (profile.impactOnBlockedUltimate && IsUltimate(sim.fighters[e.a])) UltimateImpact(SimToWorld(e.x, e.y));
                break;

            case SimEventType.KO:
                Shake(profile.koTrauma);
                break;
        }
    }

    /// <summary>
    /// Turns one sim event into named triggers for the fighters involved. Every trigger is
    /// relative to the fighter it fires for: the attacker gets "Hit", the victim gets "GotHit".
    /// SimEventTypes added to the sim later still fire, as their own name for fighter a and
    /// "Got" + name for fighter b.
    /// </summary>
    void FireEventTriggers(SimEvent e, GameSim sim)
    {
        Vector2 point = SimToWorld(e.x, e.y);
        bool heavy = e.amount >= profile.heavyThreshold;

        switch (e.type)
        {
            case SimEventType.Hit:
            {
                int facing = AwayFrom(sim, e.a, e.b);
                FireAt(JuiceTriggers.Hit, e.a, point, facing, sim);
                FireAt(heavy ? JuiceTriggers.HeavyHit : JuiceTriggers.LightHit, e.a, point, facing, sim);
                string move = MeleeMoveName(sim, e.a);   // null for projectiles
                if (move != null) FireAt(JuiceTriggers.ForMove(JuiceTriggers.Hit, move), e.a, point, facing, sim);

                FireAt(JuiceTriggers.GotHit, e.b, point, facing, sim);
                FireAt(heavy ? JuiceTriggers.GotHeavyHit : JuiceTriggers.GotLightHit, e.b, point, facing, sim);
                break;
            }

            case SimEventType.Grab:
            {
                int facing = AwayFrom(sim, e.a, e.b);
                FireAt(JuiceTriggers.Grab, e.a, point, facing, sim);
                FireAt(JuiceTriggers.GotGrabbed, e.b, point, facing, sim);
                break;
            }

            case SimEventType.Block:
            {
                // a = attacker, b = blocker
                int facing = AwayFrom(sim, e.a, e.b);
                FireAt(JuiceTriggers.Block, e.b, point, facing, sim);
                FireAt(JuiceTriggers.AttackBlocked, e.a, point, facing, sim);
                break;
            }

            case SimEventType.KO:
            {
                // a = the fighter who was KO'd (see Fighter.TakeHit), b = the attacker
                int facing = IsFighter(e.b) ? AwayFrom(sim, e.b, e.a) : FacingOf(sim, e.a);
                FireAt(JuiceTriggers.KnockedOut, e.a, point, facing, sim);
                FireAt(JuiceTriggers.ScoredKO, e.b, point, facing, sim);
                break;
            }

            case SimEventType.AttackStart:
            {
                // a = attacker, (x, y) = attacker's feet. The move has started, so it is readable now.
                int facing = FacingOf(sim, e.a);
                FireAt(JuiceTriggers.AttackStart, e.a, point, facing, sim);
                if (IsFighter(e.a) && sim.fighters[e.a].move != null)
                {
                    FireAt(JuiceTriggers.ForMove(JuiceTriggers.AttackStart, sim.fighters[e.a].move.name), e.a, point, facing, sim);
                }
                break;
            }

            case SimEventType.Dash:
            {
                int facing = FacingOf(sim, e.a);
                FireAt(JuiceTriggers.Dash, e.a, point, facing, sim);
                FireAt(e.amount == 1 ? JuiceTriggers.DashForward : JuiceTriggers.DashBack, e.a, point, facing, sim);
                break;
            }

            case SimEventType.SuperFlash:
                FireAt(JuiceTriggers.SuperFlash, e.a, point, FacingOf(sim, e.a), sim);
                break;

            case SimEventType.RoundStart:
                // No meaningful point: At Event rows play at each fighter's anchor.
                FireOnFighter(JuiceTriggers.RoundStart, 0, sim);
                FireOnFighter(JuiceTriggers.RoundStart, 1, sim);
                break;

            case SimEventType.RoundEnd:
                // a = round winner: 0 or 1, or 2 for a double KO
                for (int i = 0; i < 2; i++)
                {
                    FireOnFighter(JuiceTriggers.RoundEnd, i, sim);
                    string result = e.a == 2 ? JuiceTriggers.RoundDraw
                                  : e.a == i ? JuiceTriggers.RoundWin : JuiceTriggers.RoundLose;
                    FireOnFighter(result, i, sim);
                }
                break;

            default:
            {
                // An event type the sim added after this was written: still usable as a trigger.
                string name = e.type.ToString();
                int facing = IsFighter(e.a) && IsFighter(e.b) ? AwayFrom(sim, e.a, e.b) : FacingOf(sim, e.a);
                FireAt(name, e.a, point, facing, sim);
                FireAt("Got" + name, e.b, point, facing, sim);
                break;
            }
        }
    }

    /// <summary>
    /// Fires the triggers that come from watching the fighters (once per frame, after the sim ticks):
    /// Enter&lt;State&gt; when a fighter's state changes, and starts / stops the effects of While... rows
    /// as their conditions become true / false. Only reads the sim.
    /// <para>
    /// Checked once per frame, so a state that lasts less than a frame can be missed by Enter... triggers.
    /// </para>
    /// </summary>
    void UpdateStateTriggers(GameSim sim)
    {
        for (int i = 0; i < 2; i++)
        {
            Fighter f = sim.fighters[i];
            List<JuiceReaction> rows = ReactionsFor(i, f);

            // Enter<State> (moment). The first frame of a match only records the state.
            if (lastState[i].HasValue && lastState[i].Value != f.state)
            {
                FireOnFighter(JuiceTriggers.Enter(f.state), i, sim);
            }
            lastState[i] = f.state;

            // What is true right now.
            HashSet<string> active = whileActive[i];
            active.Clear();
            active.Add(JuiceTriggers.While(f.state));
            if (f.state == FighterStateId.Attack && f.move != null)
            {
                active.Add(JuiceTriggers.ForMove(JuiceTriggers.While(FighterStateId.Attack), f.move.name));
            }
            if (f.hp > 0 && f.hp * 100 <= f.MaxHp * profile.lowHealthPercent) active.Add(JuiceTriggers.WhileLowHealth);
            if (f.meter >= Fighter.MeterMax) active.Add(JuiceTriggers.WhileMeterFull);
            if (f.IsBlocking) active.Add(JuiceTriggers.WhileBlocking);
            if (f.IsPoweredUp) active.Add(JuiceTriggers.WhilePoweredUp);
            if (f.charging && f.state == FighterStateId.Attack) active.Add(JuiceTriggers.WhileCharging);
            if (f.vx != 0) active.Add(JuiceTriggers.WhileMoving);

            // Start or stop each While... row's effect.
            Dictionary<JuiceReaction, JuiceHandle> playing = whilePlaying[i];
            for (int r = 0; r < rows.Count; r++)
            {
                JuiceReaction row = rows[r];
                if (!row.IsWhile || string.IsNullOrEmpty(row.effect)) continue;

                bool want = active.Contains(row.trigger.Trim());

                // Every Ticks rows don't keep one effect running: they replay it on a rhythm.
                if (row.everyTicks > 0)
                {
                    UpdateRepeating(row, i, want, sim);
                    continue;
                }

                JuiceHandle handle;
                // An invalid handle means "tried, but the effect name doesn't exist": don't retry every
                // frame. A non-looping effect that finished on its own is started again while true.
                bool has = playing.TryGetValue(row, out handle) && (!handle.IsValid || particles.IsPlaying(handle));

                if (want && !has)
                {
                    playing[row] = StartWhile(row, i);
                }
                else if (!want && has)
                {
                    if (handle.IsValid) Log("While " + row.trigger + " ended for P" + (i + 1));
                    particles.Stop(handle);
                    playing.Remove(row);
                }
            }
        }
    }
    #endregion

    #region Triggers
    // A trigger with a point and a facing: At Event rows play there.
    void FireAt(string trigger, int fighter, Vector2 point, int facing, GameSim sim)
    {
        Fire(trigger, fighter, true, point, facing, sim);
    }

    // A trigger with no point: At Event rows play at the fighter's anchor.
    void FireOnFighter(string trigger, int fighter, GameSim sim)
    {
        Fire(trigger, fighter, false, Vector2.zero, FacingOf(sim, fighter), sim);
    }

    void FireFromCode(string trigger, int fighter, bool hasPoint, Vector2 point, int facing)
    {
        if (match == null || string.IsNullOrEmpty(trigger) || !IsFighter(fighter)) return;
        string name = trigger.Trim();
        if (name.StartsWith(JuiceTriggers.WhilePrefix, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("[Juice] Trigger(\"" + trigger + "\"): names starting with \"While\" are conditions the manager " +
                             "checks itself, so they can't be fired from code. Use a different name.", this);
            return;
        }
        GameSim sim = match.Sim;
        Fire(name, fighter, hasPoint, point, facing != 0 ? facing : FacingOf(sim, fighter), sim);
    }

    // Plays every moment row for this trigger in the fighter's reactions.
    void Fire(string trigger, int fighter, bool hasPoint, Vector2 point, int facing, GameSim sim)
    {
        if (!IsFighter(fighter) || sim == null) return;

        matched.Clear();
        List<JuiceReaction> rows = ReactionsFor(fighter, sim.fighters[fighter]);
        for (int r = 0; r < rows.Count; r++)
        {
            if (!rows[r].IsWhile && rows[r].Matches(trigger)) matched.Add(rows[r]);
        }

        if (logAllTriggers || (logCalls && matched.Count > 0))
        {
            Debug.Log("[Juice] Trigger " + trigger + " (P" + (fighter + 1) + ")" +
                      (matched.Count > 0 ? ": " + matched.Count + " reaction(s)" : ""), this);
        }

        for (int r = 0; r < matched.Count; r++) PlayMoment(matched[r], fighter, hasPoint, point, facing);
    }

    void PlayMoment(JuiceReaction row, int owner, bool hasPoint, Vector2 point, int facing)
    {
        float scale = row.scale > 0f ? row.scale : 1f;   // rows added in the inspector can start at 0
        if (!string.IsNullOrEmpty(row.effect))
        {
            int target = row.place == JuicePlace.OnOpponent ? 1 - owner : owner;
            if (row.place == JuicePlace.AtEvent && hasPoint) DoBurst(row.effect, point, facing, scale, row.offset);
            else if (row.ticks > 0)
            {
                JuiceHandle h = DoAttach(row.effect, target, row.ticks, scale, row.anchor, row.offset);
                if (row.sticky) particles.SetSticky(h, true);
            }
            else if (row.sticky)
            {
                // A one-off effect that rides along with the fighter until it has finished.
                JuiceHandle h = particles.BurstOnFighter(row.effect, target, row.anchor, row.offset, scale, match != null ? match.Sim : null, true);
                particles.SetSticky(h, true);
            }
            else DoBurstOnFighter(row.effect, target, row.anchor, row.offset, scale);
        }
        if (row.shake > 0f) DoShake(row.shake);
    }

    JuiceHandle StartWhile(JuiceReaction row, int owner)
    {
        if (string.IsNullOrEmpty(row.effect)) return JuiceHandle.None;
        Log("While " + row.trigger + " started for P" + (owner + 1));
        int target = row.place == JuicePlace.OnOpponent ? 1 - owner : owner;
        float scale = row.scale > 0f ? row.scale : 1f;
        JuiceHandle h = DoAttach(row.effect, target, 0, scale, row.anchor, row.offset);   // follows until stopped
        if (row.sticky) particles.SetSticky(h, true);
        return h;
    }

    /// <summary>
    /// A While... row with Every Ticks: plays its effect once as soon as the condition becomes true,
    /// then again every N ticks while it stays true. Each one is a burst that stays where it was played
    /// (footstep dust), or rides along with the fighter if the row is Sticky.
    /// <para>
    /// Counted in SIM ticks, so the rhythm follows the game: it pauses during hit stop and network
    /// stalls, and both players see the same rhythm.
    /// </para>
    /// </summary>
    void UpdateRepeating(JuiceReaction row, int owner, bool want, GameSim sim)
    {
        Dictionary<JuiceReaction, int> next = whileNextTick[owner];
        if (!want)
        {
            if (next.Remove(row)) Log("While " + row.trigger + " stopped repeating for P" + (owner + 1));
            return;
        }

        int due;
        bool running = next.TryGetValue(row, out due);
        // Not due yet. (A due tick far in the future means the sim's tick counter was reset, e.g. a new round.)
        if (running && sim.tick < due && due - sim.tick <= row.everyTicks) return;

        if (!running) Log("While " + row.trigger + " started for P" + (owner + 1) + ": " + row.effect + " every " + row.everyTicks + " ticks");
        next[row] = sim.tick + row.everyTicks;

        // Straight to the particle player, so the console doesn't get a line per footstep.
        int target = row.place == JuicePlace.OnOpponent ? 1 - owner : owner;
        float scale = row.scale > 0f ? row.scale : 1f;
        JuiceHandle h = particles.BurstOnFighter(row.effect, target, row.anchor, row.offset, scale, sim, row.sticky);
        if (row.sticky) particles.SetSticky(h, true);   // this pulse rides along with the fighter
    }

    // The fighter's reaction rows, rebuilt whenever the character in that slot changes.
    List<JuiceReaction> ReactionsFor(int fighter, Fighter f)
    {
        if (reactionsFor[fighter] != f.characterId)
        {
            StopWhile(fighter);
            profile.CollectReactions(f.characterId, reactions[fighter]);
            reactionsFor[fighter] = f.characterId;
        }
        return reactions[fighter];
    }

    void StopWhile(int fighter)
    {
        foreach (KeyValuePair<JuiceReaction, JuiceHandle> pair in whilePlaying[fighter]) particles.Stop(pair.Value);
        whilePlaying[fighter].Clear();
        whileNextTick[fighter].Clear();
    }

    // Name of the melee move the attacker is in, or null (projectile hits, or the attack already ended).
    static string MeleeMoveName(GameSim sim, int attacker)
    {
        if (!IsFighter(attacker)) return null;
        Fighter f = sim.fighters[attacker];
        return f.state == FighterStateId.Attack && f.move != null ? f.move.name : null;
    }

    static bool IsFighter(int index) { return index == 0 || index == 1; }

    static int FacingOf(GameSim sim, int fighter)
    {
        return IsFighter(fighter) ? sim.fighters[fighter].facing : 1;
    }

    // Facing pointing from fighter `from` toward fighter `to` (e.g. sparks flying away from the attacker).
    static int AwayFrom(GameSim sim, int from, int to)
    {
        if (!IsFighter(from) || !IsFighter(to)) return FacingOf(sim, from);
        return sim.fighters[to].x >= sim.fighters[from].x ? 1 : -1;
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
        if (world == null) return;
        impactFrame.Show(ticks / 60f, world.FighterRoot(0), world.FighterRoot(1));
    }

    // The attacker is in the middle of their Ultimate. (A projectile Ultimate that lands after
    // its thrower has recovered won't count; none of the characters have one yet.)
    static bool IsUltimate(Fighter attacker)
    {
        return attacker.state == FighterStateId.Attack && attacker.move != null && attacker.move.ultimate;
    }

    // Everything an Ultimate hit gets: impact frame (with its inverted flicker), a zoom punch towards
    // the hit that holds for the impact frame, and a CRT glitch.
    void UltimateImpact(Vector2 pos)
    {
        Log("UltimateImpact at " + pos);
        float seconds = profile.impactTicks / 60f;
        DoImpactFrame(profile.impactTicks);
        if (profile.zoomPunchAmount > 0f) shake.ZoomPunch(pos, seconds);
        if (CRTSettings.Instance != null) CRTSettings.Instance.Glitch(profile.crtGlitchTime);
    }

    void ResetAll()
    {
        Log("ResetAll");
        particles.StopAll();
        shake.Clear();
        for (int i = 0; i < 2; i++)
        {
            whilePlaying[i].Clear();          // their effects were just removed by StopAll
            whileActive[i].Clear();
            whileNextTick[i].Clear();
            lastState[i] = null;              // the first frame of the next match only records the state
            reactionsFor[i] = int.MinValue;   // re-read the profiles: the characters may have changed
        }
        impactFrame.Stop();
        // TODO: clear flashes
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

    [ContextMenu("Test: Fire test trigger on P1")]
    void TestTrigger()
    {
        if (Application.isPlaying) FireFromCode(testTrigger, 0, false, Vector2.zero, 0);
    }

    [ContextMenu("Log built-in trigger names")]
    void LogTriggerNames()
    {
        string states = string.Join(", ", Enum.GetNames(typeof(FighterStateId)));
        Debug.Log("[Juice] Built-in triggers (each one is relative to the character whose profile the row is in):\n" +
                  "Moments: Hit, LightHit, HeavyHit, Hit:<Move>, GotHit, GotLightHit, GotHeavyHit, Grab, GotGrabbed, " +
                  "Block, AttackBlocked, AttackStart, AttackStart:<Move>, Dash, DashForward, DashBack, SuperFlash, " +
                  "KnockedOut, ScoredKO, RoundStart, RoundEnd, RoundWin, RoundLose, RoundDraw, Enter<State>\n" +
                  "While: WhileMoving, WhileLowHealth, WhileMeterFull, WhileBlocking, WhilePoweredUp, WhileCharging, While<State>, WhileAttack:<Move>\n" +
                  "(a While row with Every Ticks > 0 replays its effect on that rhythm instead of holding one effect)\n" +
                  "States: " + states + "\n" +
                  "Moves (standard moveset): Light, Heavy, Grab, Special, Ultimate\n" +
                  "Plus any name code fires with JuiceManager.Trigger(name, fighter). " +
                  "Tick Log All Triggers to watch them fire live.", this);
    }

    [ContextMenu("Test: Impact frame (1 second, easier to see)")]
    void TestImpactFrame()
    {
        if (Application.isPlaying) DoImpactFrame(60);
    }

    [ContextMenu("Test: Ultimate impact (frame + zoom punch + CRT glitch) on P1")]
    void TestUltimateImpact()
    {
        if (!Application.isPlaying || match == null || match.Phase != MatchPhase.Playing) return;
        Fighter f = match.Sim.fighters[0];
        UltimateImpact(SimToWorld(f.x, f.y + 1000));
    }
    #endregion
}
