using UnityEngine;

/// <summary>
/// The ParticleSystem decides how the effect LOOKS: sprite, lifetime, speed, direction, colour...
/// This component decides how JuiceManager PLACES it, and forces the few settings every effect
/// must share so pooling works no matter how the ParticleSystem was set up.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public class JuiceEffect : MonoBehaviour
{
    public enum DrawLayer { InFrontOfFighters, BehindFighters }

    [Tooltip("Where the effect sits relative to the point it is played at, in Unity units. X is forward (the way the fighter faces).")]
    public Vector2 offset;

    [Tooltip("Mirror the effect when the fighter faces left.")]
    public bool flipWithFacing = true;

    [Tooltip("Sparks usually go in front of the fighters, auras behind.")]
    public DrawLayer drawLayer = DrawLayer.InFrontOfFighters;

    [Tooltip("Safety net for LOOPING effects played with Burst: they stop emitting after this many ticks (60 = 1 second). Non-looping effects end on their own.")]
    [Min(1)] public int burstMaxTicks = 60;

    // WorldView draws the ground at -1, fighters at 0, hurtbox overlays at 5 and hitbox overlays at 10.
    const int FrontOrder = 20;
    const int BackOrder = -5;

    ParticleSystem root;
    ParticleSystemRenderer[] renderers;
    Quaternion baseRotation;
    Vector3 baseScale;

    public bool IsAlive { get { return root.IsAlive(true); } }

    // Called once by JuiceParticles right after the prefab is instantiated.
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

    public void Place(Vector2 pos, int facing, float scale)
    {
        float forward = facing < 0 ? -1f : 1f;
        bool flip = flipWithFacing && facing < 0;

        transform.position = new Vector3(pos.x + offset.x * forward, pos.y + offset.y, 0f);
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

    public void Play() { root.Play(true); }

    // Stop making new particles; the ones already out finish their lifetime.
    public void StopEmitting() { root.Stop(true, ParticleSystemStopBehavior.StopEmitting); }

    // Remove everything immediately.
    public void Kill() { root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
}
