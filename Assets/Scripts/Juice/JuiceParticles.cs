using System.Collections.Generic;
using UnityEngine;

// Lets you stop an effect started with JuiceManager.Attach. Safe to keep after the effect has
// ended: Stop then does nothing and IsPlaying returns false. JuiceHandle.None means "nothing".
public struct JuiceHandle
{
    internal readonly int serial;
    internal JuiceHandle(int serial) { this.serial = serial; }
    public bool IsValid { get { return serial != 0; } }
    public static readonly JuiceHandle None = new JuiceHandle(0);
}

/// <summary>
/// Plays, positions, times and pools particle effects for JuiceManager. View only.
/// <para>
/// Every effect is a prefab (ParticleSystem + JuiceEffect) listed in the JuiceProfile.
/// The same prefab can be played three ways:
/// </para>
/// <code>
///   Burst        at a point; plays once and cleans itself up    (hit sparks, dust)
///   Attach(n)    follows a fighter for n ticks, then fades out  (a timed aura)
///   Attach(0)    follows a fighter until Stop(handle)           (low-health sparks, a stance)
/// </code>
/// Finished effects are switched off and reused instead of destroyed.
/// </summary>
public class JuiceParticles
{
    class Active
    {
        public int serial;
        public JuiceParticleType id;
        public JuiceEffect effect;
        public int fighter;     // -1 = stays where it was placed
        public float stopAt;    // when to stop emitting, on our clock; -1 = never
        public bool stopping;   // no longer emitting, waiting for its last particles to fade
        public float scale;
    }

    readonly JuiceProfile profile;
    readonly Transform root;
    readonly Dictionary<JuiceParticleType, Stack<JuiceEffect>> pool = new Dictionary<JuiceParticleType, Stack<JuiceEffect>>();
    readonly List<Active> active = new List<Active>();
    readonly HashSet<JuiceParticleType> warned = new HashSet<JuiceParticleType>();
    float clock;
    int nextSerial = 1;

    public JuiceParticles(JuiceProfile profile, Transform parent)
    {
        this.profile = profile;
        root = new GameObject("JuiceEffects").transform;
        root.SetParent(parent, false);
    }

    #region API
    public JuiceHandle Burst(JuiceParticleType id, Vector2 pos, int facing, float scale)
    {
        JuiceEffect fx = Take(id);
        if (fx == null) return JuiceHandle.None;

        fx.Place(pos, facing, scale);
        fx.Play();
        return Track(id, fx, -1, clock + JuiceManager.TicksToSeconds(fx.burstMaxTicks), scale);
    }

    public JuiceHandle Attach(JuiceParticleType id, int fighter, int ticks, float scale, GameSim sim)
    {
        if (fighter < 0 || fighter > 1)
        {
            Debug.LogWarning("[Juice] Attach: fighter must be 0 or 1 (got " + fighter + ").");
            return JuiceHandle.None;
        }

        JuiceEffect fx = Take(id);
        if (fx == null) return JuiceHandle.None;

        if (sim != null) PlaceOnFighter(fx, sim.fighters[fighter], scale);
        fx.Play();
        float stopAt = ticks > 0 ? clock + JuiceManager.TicksToSeconds(ticks) : -1f;
        return Track(id, fx, fighter, stopAt, scale);
    }

    // Stops emitting; particles already out fade normally, then the effect returns to the pool.
    public void Stop(JuiceHandle handle)
    {
        Active a = Find(handle);
        if (a != null) BeginStop(a);
    }

    public bool IsPlaying(JuiceHandle handle)
    {
        Active a = Find(handle);
        return a != null && !a.stopping;
    }

    // Removes every effect instantly (new match, back to lobby).
    public void StopAll()
    {
        for (int i = active.Count - 1; i >= 0; i--) Release(i);
    }
    #endregion

    #region Tick
    // sim is null when no match is running; attached effects then stay where they are.
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
    void PlaceOnFighter(JuiceEffect fx, Fighter f, float scale)
    {
        fx.Place(JuiceManager.SimToWorld(f.x, f.y), f.facing, scale);
    }

    JuiceHandle Track(JuiceParticleType id, JuiceEffect fx, int fighter, float stopAt, float scale)
    {
        Active a = new Active();
        a.serial = nextSerial++;
        a.id = id;
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

    JuiceEffect Take(JuiceParticleType id)
    {
        if (id == JuiceParticleType.None) return null;

        Stack<JuiceEffect> free;
        if (pool.TryGetValue(id, out free) && free.Count > 0)
        {
            JuiceEffect reused = free.Pop();
            reused.gameObject.SetActive(true);
            return reused;
        }

        JuiceEffect prefab = profile.GetParticle(id);
        if (prefab == null)
        {
            if (warned.Add(id)) Debug.LogWarning("[Juice] No prefab for particle '" + id + "' in the JuiceProfile, so nothing plays.");
            return null;
        }

        JuiceEffect fx = Object.Instantiate(prefab, root);
        fx.name = "FX " + id;
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
