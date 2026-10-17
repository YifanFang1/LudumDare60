using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lets you stop an effect started with JuiceManager.Attach. Safe to keep after the effect has
/// ended: Stop then does nothing and IsPlaying returns false. JuiceHandle.None means "nothing".
/// </summary>
public struct JuiceHandle
{
    internal readonly int serial;
    internal JuiceHandle(int serial) { this.serial = serial; }

    /// <summary>False for JuiceHandle.None (nothing was started).</summary>
    public bool IsValid { get { return serial != 0; } }

    /// <summary>A handle that refers to nothing.</summary>
    public static readonly JuiceHandle None = new JuiceHandle(0);
}

/// <summary>
/// Plays, positions, times and pools particle effects for JuiceManager. View only.
/// <para>
/// Every effect is a prefab (ParticleSystem + JuiceEffect) inside Assets/Juice/Resources.
/// They are found automatically and played by name (JuiceEffect.EffectName, not case sensitive).
/// To add an effect: make the prefab, drop it in that folder, call it by name. No code changes.
/// </para>
/// The same prefab can be played these ways:
/// <code>
///   Burst            at a point; plays once and cleans itself up            (hit sparks)
///   BurstOnFighter   at a point on a fighter (feet, centre, head); once     (footstep dust)
///   Attach(n)        follows a point on a fighter for n ticks, then fades   (a timed aura)
///   Attach(0)        follows a point on a fighter until Stop(handle)        (low-health sparks)
/// </code>
/// Where on the fighter comes from the anchor (the prefab's, unless the call gives one), plus
/// the prefab's offset, plus the call's offset.
/// Finished effects are switched off and reused instead of destroyed.
/// </summary>
public class JuiceParticles
{
    /// <summary>The path inside Resources that effect prefabs are loaded from. "" means every Resources
    /// folder in the project; the effects live in Assets/Juice/Resources.</summary>
    public const string ResourcesFolder = "";

    #region State
    class Active
    {
        public int serial;
        public string id;
        public JuiceEffect effect;
        public int fighter;          // -1 = stays where it was placed
        public JuiceAnchor anchor;   // resolved (never Default) when following a fighter
        public Vector2 offset;       // per-call offset, Unity units, X forward
        public float stopAt;         // when to stop emitting, on our clock; -1 = never
        public bool stopping;        // no longer emitting, waiting for its last particles to fade
        public float scale;
    }

    readonly Transform root;
    readonly Dictionary<string, JuiceEffect> prefabs = new Dictionary<string, JuiceEffect>(System.StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, Stack<JuiceEffect>> pool = new Dictionary<string, Stack<JuiceEffect>>(System.StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> warned = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    readonly List<Active> active = new List<Active>();
    float clock;
    int nextSerial = 1;
    #endregion

    /// <summary>Creates the pool under <paramref name="parent"/> and loads every effect prefab.</summary>
    public JuiceParticles(Transform parent)
    {
        root = new GameObject("JuiceEffects").transform;
        root.SetParent(parent, false);
        LoadPrefabs();
    }

    #region API
    /// <summary>Plays an effect once at a point. It returns to the pool by itself when done.</summary>
    /// <param name="effect">Effect name. Empty = play nothing.</param>
    /// <param name="pos">Unity units.</param>
    /// <param name="facing">1 = right, -1 = left.</param>
    /// <param name="scale">Size multiplier.</param>
    /// <param name="offset">Extra nudge in Unity units, X forward, added to the prefab's offset.</param>
    public JuiceHandle Burst(string effect, Vector2 pos, int facing, float scale, Vector2 offset)
    {
        JuiceEffect fx = Take(effect);
        if (fx == null) return JuiceHandle.None;

        fx.Place(pos, facing, scale, offset);
        fx.Play();
        return Track(effect, fx, -1, JuiceAnchor.Feet, offset, clock + JuiceManager.TicksToSeconds(fx.burstMaxTicks), scale);
    }

    /// <summary>
    /// Plays an effect once at a point on a fighter, facing the way they face. It does NOT follow
    /// them afterwards (use Attach for that).
    /// </summary>
    /// <param name="effect">Effect name. Empty = play nothing.</param>
    /// <param name="fighter">Sim index (0 or 1).</param>
    /// <param name="anchor">Where on the fighter. Default = the prefab's anchor.</param>
    /// <param name="offset">Extra nudge in Unity units, X forward, added to the prefab's offset.</param>
    /// <param name="scale">Size multiplier.</param>
    /// <param name="sim">The running sim, to read the fighter from. Nothing plays if null.</param>
    public JuiceHandle BurstOnFighter(string effect, int fighter, JuiceAnchor anchor, Vector2 offset, float scale, GameSim sim)
    {
        if (!CheckFighter(fighter, "BurstOnFighter") || sim == null) return JuiceHandle.None;

        JuiceEffect fx = Take(effect);
        if (fx == null) return JuiceHandle.None;

        JuiceAnchor resolved = Resolve(anchor, fx);
        PlaceOnFighter(fx, sim.fighters[fighter], resolved, offset, scale);
        fx.Play();
        return Track(effect, fx, -1, resolved, offset, clock + JuiceManager.TicksToSeconds(fx.burstMaxTicks), scale);
    }

    /// <summary>Plays an effect that follows a point on a fighter, flipping with the way they face.</summary>
    /// <param name="effect">Effect name. Empty = play nothing.</param>
    /// <param name="fighter">Sim index (0 or 1).</param>
    /// <param name="ticks">How long it emits (60 = 1 second). 0 = until Stop is called.</param>
    /// <param name="scale">Size multiplier.</param>
    /// <param name="anchor">Where on the fighter. Default = the prefab's anchor.</param>
    /// <param name="offset">Extra nudge in Unity units, X forward, added to the prefab's offset.</param>
    /// <param name="sim">Used to place it on the fighter straight away. May be null.</param>
    public JuiceHandle Attach(string effect, int fighter, int ticks, float scale, JuiceAnchor anchor, Vector2 offset, GameSim sim)
    {
        if (!CheckFighter(fighter, "Attach")) return JuiceHandle.None;

        JuiceEffect fx = Take(effect);
        if (fx == null) return JuiceHandle.None;

        JuiceAnchor resolved = Resolve(anchor, fx);
        if (sim != null) PlaceOnFighter(fx, sim.fighters[fighter], resolved, offset, scale);
        fx.Play();
        float stopAt = ticks > 0 ? clock + JuiceManager.TicksToSeconds(ticks) : -1f;
        return Track(effect, fx, fighter, resolved, offset, stopAt, scale);
    }

    /// <summary>
    /// A point on a fighter in Unity units. Feet = their position on the ground; Center and Head
    /// are the middle and top of their body (hurtbox), so they follow tall and short characters.
    /// </summary>
    public static Vector2 FighterPoint(Fighter f, JuiceAnchor anchor)
    {
        if (anchor == JuiceAnchor.Default || anchor == JuiceAnchor.Feet) return JuiceManager.SimToWorld(f.x, f.y);

        Box body = f.HurtboxWorld();
        int midX = body.x + body.w / 2;
        int y = anchor == JuiceAnchor.Head ? body.y + body.h : body.y + body.h / 2;
        return JuiceManager.SimToWorld(midX, y);
    }

    /// <summary>Stops emitting. Particles already out fade normally, then the effect returns to the pool.</summary>
    public void Stop(JuiceHandle handle)
    {
        Active a = Find(handle);
        if (a != null) BeginStop(a);
    }

    /// <summary>True while the effect behind this handle is still emitting.</summary>
    public bool IsPlaying(JuiceHandle handle)
    {
        Active a = Find(handle);
        return a != null && !a.stopping;
    }

    /// <summary>Removes every effect instantly (new match, back to lobby).</summary>
    public void StopAll()
    {
        for (int i = active.Count - 1; i >= 0; i--) Release(i);
    }

    /// <summary>True if an effect with this name exists in Assets/Juice/Resources.</summary>
    public bool Has(string effect)
    {
        return !string.IsNullOrEmpty(effect) && prefabs.ContainsKey(effect);
    }
    #endregion

    #region Tick
    /// <summary>Call once per frame: moves attached effects, ends timed ones, recycles finished ones.</summary>
    /// <param name="dt">Real (unscaled) seconds since last frame.</param>
    /// <param name="sim">Null when no match is running; attached effects then stay where they are.</param>
    public void Tick(float dt, GameSim sim)
    {
        clock += dt;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            Active a = active[i];
            if (a.effect == null) { active.RemoveAt(i); continue; }   // destroyed from outside

            if (a.fighter >= 0 && sim != null) PlaceOnFighter(a.effect, sim.fighters[a.fighter], a.anchor, a.offset, a.scale);
            if (!a.stopping && a.stopAt >= 0f && clock >= a.stopAt) BeginStop(a);
            if (!a.effect.IsAlive) Release(i);
        }
    }
    #endregion

    #region Internals
    void LoadPrefabs()
    {
        JuiceEffect[] all = Resources.LoadAll<JuiceEffect>(ResourcesFolder);
        for (int i = 0; i < all.Length; i++)
        {
            string key = all[i].EffectName;
            JuiceEffect existing;
            if (prefabs.TryGetValue(key, out existing))
            {
                Debug.LogWarning("[Juice] Two particle effects are both called '" + key + "' (" + existing.name + " and " + all[i].name + "). Using " + existing.name + ".", all[i]);
                continue;
            }
            prefabs[key] = all[i];
        }

        if (all.Length == 0)
        {
            Debug.LogWarning("[Juice] No particle effects found. Put JuiceEffect prefabs in Assets/Resources/" + ResourcesFolder + ".");
        }
    }

    string AvailableNames()
    {
        return prefabs.Count == 0 ? "(none)" : string.Join(", ", prefabs.Keys);
    }

    void PlaceOnFighter(JuiceEffect fx, Fighter f, JuiceAnchor anchor, Vector2 offset, float scale)
    {
        fx.Place(FighterPoint(f, anchor), f.facing, scale, offset);
    }

    // The call's anchor wins; Default falls back to the prefab's anchor, then to Feet.
    static JuiceAnchor Resolve(JuiceAnchor requested, JuiceEffect fx)
    {
        if (requested != JuiceAnchor.Default) return requested;
        return fx.anchor != JuiceAnchor.Default ? fx.anchor : JuiceAnchor.Feet;
    }

    static bool CheckFighter(int fighter, string caller)
    {
        if (fighter == 0 || fighter == 1) return true;
        Debug.LogWarning("[Juice] " + caller + ": fighter must be 0 or 1 (got " + fighter + ").");
        return false;
    }

    JuiceHandle Track(string effect, JuiceEffect fx, int fighter, JuiceAnchor anchor, Vector2 offset, float stopAt, float scale)
    {
        Active a = new Active();
        a.serial = nextSerial++;
        a.id = effect;
        a.effect = fx;
        a.fighter = fighter;
        a.anchor = anchor;
        a.offset = offset;
        a.stopAt = stopAt;
        a.scale = scale;
        active.Add(a);
        return new JuiceHandle(a.serial);
    }

    Active Find(JuiceHandle handle)
    {
        if (!handle.IsValid) return null;
        for (int i = 0; i < active.Count; i++)
        {
            if (active[i].serial == handle.serial) return active[i];
        }
        return null;
    }

    void BeginStop(Active a)
    {
        a.effect.StopEmitting();
        a.stopping = true;
    }

    // Empty name = "no effect" (e.g. an unused slot in a CharacterJuiceProfile): plays nothing, no warning.
    JuiceEffect Take(string effect)
    {
        if (string.IsNullOrEmpty(effect)) return null;

        Stack<JuiceEffect> free;
        if (pool.TryGetValue(effect, out free) && free.Count > 0)
        {
            JuiceEffect reused = free.Pop();
            reused.gameObject.SetActive(true);
            return reused;
        }

        JuiceEffect prefab;
        if (!prefabs.TryGetValue(effect, out prefab))
        {
            if (warned.Add(effect)) Debug.LogWarning("[Juice] No particle effect called '" + effect + "'. Available: " + AvailableNames());
            return null;
        }

        JuiceEffect fx = Object.Instantiate(prefab, root);
        fx.name = "FX " + prefab.EffectName;
        fx.Init();
        return fx;
    }

    void Release(int index)
    {
        Active a = active[index];
        active.RemoveAt(index);
        if (a.effect == null) return;

        a.effect.Kill();
        a.effect.gameObject.SetActive(false);

        Stack<JuiceEffect> free;
        if (!pool.TryGetValue(a.id, out free))
        {
            free = new Stack<JuiceEffect>();
            pool[a.id] = free;
        }
        free.Push(a.effect);
    }
    #endregion
}
