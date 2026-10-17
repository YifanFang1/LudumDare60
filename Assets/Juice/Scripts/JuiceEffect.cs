using UnityEngine;

/// <summary>
/// Which point on a fighter an effect is attached to or played at. Measured from the fighter's
/// body (hurtbox), so it adapts to tall and short characters.
/// </summary>
public enum JuiceAnchor
{
    /// <summary>Use the anchor set on the effect's prefab (Feet if that is Default too).</summary>
    Default = 0,
    /// <summary>On the ground under the fighter. Dust, landing puffs.</summary>
    Feet = 1,
    /// <summary>Middle of the body. Auras, power-ups.</summary>
    Center = 2,
    /// <summary>Top of the body. Status effects, stun stars.</summary>
    Head = 3,
}

/// <summary>
/// Put this on the ROOT of every particle effect prefab, next to its ParticleSystem, and save the
/// prefab in Assets/Juice/Resources so JuiceManager finds it automatically.
/// <para>
/// The ParticleSystem decides how the effect LOOKS: sprite, lifetime, speed, direction, colour...
/// This component gives the effect its name and decides how JuiceManager PLACES it. It also forces
/// the few settings every effect must share, so pooling works no matter how the ParticleSystem was set up.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public class JuiceEffect : MonoBehaviour
{
    /// <summary>Whether the effect draws over or under the fighters.</summary>
    public enum DrawLayer { InFrontOfFighters, BehindFighters }

    #region Settings
    [Tooltip("The name code and profiles use to play this effect, e.g. \"Sparks\". Not case sensitive. Leave empty to use the prefab's file name.")]
    public string effectName;

    [Tooltip("Where on a fighter this effect sits when it is attached to one (Attach, BurstOnFighter, profile slots). Calls can override it. Default = Feet.")]
    public JuiceAnchor anchor = JuiceAnchor.Feet;

    [Tooltip("Extra nudge from the point it is played at (after the anchor), in Unity units. X is forward (the way the fighter faces). Calls can add their own offset on top.")]
    public Vector2 offset;

    [Tooltip("Mirror the effect when the fighter faces left.")]
    public bool flipWithFacing = true;

    [Tooltip("Sparks usually go in front of the fighters, auras behind.")]
    public DrawLayer drawLayer = DrawLayer.InFrontOfFighters;

    [Tooltip("Safety net for LOOPING effects played with Burst: they stop emitting after this many ticks (60 = 1 second). Non-looping effects end on their own.")]
    [Min(1)] public int burstMaxTicks = 60;
    #endregion

    #region State
    // WorldView draws the ground at -1, fighters at 0, hurtbox overlays at 5 and hitbox overlays at 10.
    const int FrontOrder = 20;
    const int BackOrder = -5;

    ParticleSystem root;
    ParticleSystemRenderer[] renderers;
    Quaternion baseRotation;
    Vector3 baseScale;
    #endregion

    #region Properties
    /// <summary>
    /// The name this effect is played by: Effect Name, or the prefab's file name when that is empty.
    /// Read it on the prefab; pooled copies are renamed "FX ...".
    /// </summary>
    public string EffectName { get { return string.IsNullOrEmpty(effectName) ? name : effectName; } }

    /// <summary>True while the effect is emitting or still has particles on screen.</summary>
    public bool IsAlive { get { return root.IsAlive(true); } }
    #endregion

    #region Control (used by JuiceParticles)
    /// <summary>
    /// Called once by JuiceParticles right after the prefab is instantiated. Forces the shared
    /// settings on every ParticleSystem in the prefab and leaves the effect stopped and empty.
    /// </summary>
    public void Init()
    {
        root = GetComponent<ParticleSystem>();
        baseRotation = transform.localRotation;
        baseScale = transform.localScale;

        ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.MainModule main = systems[i].main;
            main.playOnAwake = false;                                 // only JuiceManager starts effects
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // so the scale argument works
            main.useUnscaledTime = true;                              // same clock as the rest of juice
            main.stopAction = ParticleSystemStopAction.None;          // pooled: never destroy or disable itself
        }

        int baseOrder = drawLayer == DrawLayer.InFrontOfFighters ? FrontOrder : BackOrder;
        renderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sortingOrder += baseOrder;   // keeps child systems ordered relative to each other
        }

        Kill();
    }

    /// <summary>Moves, mirrors and sizes the effect.</summary>
    /// <param name="pos">Where it is played, in Unity units. The prefab's offset and <paramref name="extraOffset"/> are added on top.</param>
    /// <param name="facing">1 = right, -1 = left.</param>
    /// <param name="scale">Size multiplier on top of the prefab's own scale.</param>
    /// <param name="extraOffset">Per-call nudge in Unity units, X forward. Added to the prefab's offset.</param>
    public void Place(Vector2 pos, int facing, float scale, Vector2 extraOffset)
    {
        float forward = facing < 0 ? -1f : 1f;
        bool flip = flipWithFacing && facing < 0;
        Vector2 total = offset + extraOffset;

        transform.position = new Vector3(pos.x + total.x * forward, pos.y + total.y, 0f);
        transform.rotation = (flip ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity) * baseRotation;
        transform.localScale = baseScale * scale;

        // Rotating mirrors where particles fly; billboard sprites also need their texture mirrored.
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].renderMode != ParticleSystemRenderMode.Billboard) continue;
            Vector3 f = renderers[i].flip;
            f.x = flip ? 1f : 0f;
            renderers[i].flip = f;
        }
    }

    /// <summary>Starts the effect, including child systems.</summary>
    public void Play() { root.Play(true); }

    /// <summary>Stops making new particles; the ones already out finish their lifetime.</summary>
    public void StopEmitting() { root.Stop(true, ParticleSystemStopBehavior.StopEmitting); }

    /// <summary>Removes every particle immediately.</summary>
    public void Kill() { root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
    #endregion
}
