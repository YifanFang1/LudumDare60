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
/// Every effect is a prefab (ParticleSystem + JuiceEffect) inside Assets/Resources/Juice.
/// They are found automatically and played by name (JuiceEffect.EffectName, not case sensitive).
/// To add an effect: make the prefab, drop it in that folder, call it by name. No code changes.
/// </para>
/// The same prefab can be played three ways:
/// <code>
///   Burst        at a point; plays once and cleans itself up    (hit sparks, dust)
///   Attach(n)    follows a fighter for n ticks, then fades out  (a timed aura)
///   Attach(0)    follows a fighter until Stop(handle)           (low-health sparks, a stance)
/// </code>
/// Finished effects are switched off and reused instead of destroyed.
/// </summary>
public class JuiceParticles
{
    /// <summary>The folder inside Resources that effect prefabs are loaded from (Assets/Resources/Juice).</summary>
    public const string ResourcesFolder = "Juice";

    #region State
    class Active
    {
        public int serial;
        public string id;
        public JuiceEffect effect;
        public int fighter;     // -1 = stays where it was placed
        public float stopAt;    // when to stop emitting, on our clock; -1 = never
        public bool stopping;   // no longer emitting, waiting for its last particles to fade
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
    public JuiceHandle Burst(string effect, Vector2 pos, int facing, float scale)
    {
        JuiceEffect fx = Take(effect);
        if (fx == null) return JuiceHandle.None;

        fx.Place(pos, facing, scale);
        fx.Play();
        return Track(effect, fx, -1, clock + JuiceManager.TicksToSeconds(fx.burstMaxTicks), scale);
    }

    /// <summary>Plays an effect that follows a fighter, flipping with the way they face.</summary>
    /// <param name="effect">Effect name. Empty = play nothing.</param>
    /// <param name="fighter">Sim index (0 or 1).</param>
    /// <param name="ticks">How long it emits (60 = 1 second). 0 = until Stop is called.</param>
    /// <param name="scale">Size multiplier.</param>
    /// <param name="sim">Used to place it on the fighter straight away. May be null.</param>
    public JuiceHandle Attach(string effect, int fighter, int ticks, float scale, GameSim sim)
    {
        if (fighter < 0 || fighter > 1)
        {
            Debug.LogWarning("[Juice] Attach: fighter must be 0 or 1 (got " + fighter + ").");
            return JuiceHandle.None;
        }

        JuiceEffect fx = Take(effect);
        if (fx == null) return JuiceHandle.None;

        if (sim != null) PlaceOnFighter(fx, sim.fighters[fighter], scale);
        fx.Play();
        float stopAt = ticks > 0 ? clock + JuiceManager.TicksToSeconds(ticks) : -1f;
        return Track(effect, fx, fighter, stopAt, scale);
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

    /// <summary>True if an effect with this name exists in Resources/Juice.</summary>
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

            if (a.fighter >= 0 && sim != null) PlaceOnFighter(a.effect, sim.fighters[a.fighter], a.scale);
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

    void PlaceOnFighter(JuiceEffect fx, Fighter f, float scale)
    {
        fx.Place(JuiceManager.SimToWorld(f.x, f.y), f.facing, scale);
    }

    JuiceHandle Track(string effect, JuiceEffect fx, int fighter, float stopAt, float scale)
    {
        Active a = new Active();
        a.serial = nextSerial++;
        a.id = effect;
        a.effect = fx;
        a.fighter = fighter;
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
