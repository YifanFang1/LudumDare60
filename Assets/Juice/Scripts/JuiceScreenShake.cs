using UnityEngine;

/// <summary>
/// Trauma-based screen shake for JuiceManager. View only.
/// <para>
/// Hits ADD trauma (0..1), which decays over time. The shake strength is trauma squared, so small
/// hits barely move the camera and big ones kick hard, and rapid hits stack instead of restarting.
/// The camera is offset (and slightly rolled) by smooth Perlin noise, scaled by that strength.
/// </para>
/// <para>
/// No camera rig is needed. Each frame the shake removes last frame's offset and applies a new one.
/// If something else moved the camera in between (WorldView frames it when a match starts), that
/// new position simply becomes the resting point.
/// </para>
/// </summary>
public class JuiceScreenShake
{
    #region State
    // Fixed noise seeds so each axis wobbles differently. Visual only, so no sim randomness involved.
    const float SeedX = 13.7f;
    const float SeedY = 71.3f;
    const float SeedRoll = 137.9f;

    readonly JuiceProfile profile;
    Camera cam;

    float trauma;
    float time;

    Vector3 appliedOffset;      // what we added to the camera last frame
    float appliedRoll;          // degrees we rolled the camera last frame
    Vector3 lastPosition;       // where the camera was right after we moved it
    Quaternion lastRotation;
    bool hasApplied;
    #endregion

    /// <summary>Uses <paramref name="camera"/>, or Camera.main when it is null.</summary>
    public JuiceScreenShake(JuiceProfile profile, Camera camera)
    {
        this.profile = profile;
        cam = camera;
    }

    #region API
    /// <summary>Current trauma, 0..1.</summary>
    public float Trauma { get { return trauma; } }

    /// <summary>Adds trauma (clamped to 0..1). Rapid hits stack.</summary>
    public void Add(float amount)
    {
        if (amount <= 0f) return;
        trauma = Mathf.Clamp01(trauma + amount);
    }

    /// <summary>Stops shaking. The camera returns to rest on the next Tick.</summary>
    public void Clear()
    {
        trauma = 0f;
    }

    /// <summary>Puts the camera back where it would be without shake (call when the manager is disabled).</summary>
    public void Restore()
    {
        trauma = 0f;
        Apply(Vector3.zero, 0f);
    }
    #endregion

    #region Tick
    /// <summary>Call once per frame from LateUpdate.</summary>
    /// <param name="dt">Real (unscaled) seconds since last frame.</param>
    public void Tick(float dt)
    {
        time += dt;
        trauma = Mathf.Max(0f, trauma - profile.traumaDecay * dt);

        float strength = Mathf.Pow(trauma, profile.traumaExponent) * profile.shakeMultiplier;
        if (strength <= 0f)
        {
            Apply(Vector3.zero, 0f);
            return;
        }

        float t = time * profile.shakeFrequency;
        Vector3 offset = new Vector3(
            profile.maxShakeOffset * strength * Noise(SeedX, t),
            profile.maxShakeOffset * strength * Noise(SeedY, t),
            0f);
        float roll = profile.maxShakeRoll * strength * Noise(SeedRoll, t);

        Apply(offset, roll);
    }
    #endregion

    #region Internals
    // Smooth noise in -1..1.
    static float Noise(float seed, float t)
    {
        return Mathf.PerlinNoise(seed, t) * 2f - 1f;
    }

    void Apply(Vector3 offset, float roll)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Transform tr = cam.transform;

        // Where the camera rests without our shake. If someone else moved it since our last frame,
        // their position is the new rest point; otherwise take our own offset back off.
        bool untouched = hasApplied
                         && (tr.position - lastPosition).sqrMagnitude < 1e-8f
                         && Quaternion.Angle(tr.rotation, lastRotation) < 0.001f;
        Vector3 restPosition = untouched ? tr.position - appliedOffset : tr.position;
        Quaternion restRotation = untouched ? tr.rotation * Quaternion.Euler(0f, 0f, -appliedRoll) : tr.rotation;

        if (!hasApplied && offset == Vector3.zero && roll == 0f) return;   // nothing to undo, nothing to do

        tr.position = restPosition + offset;
        tr.rotation = restRotation * Quaternion.Euler(0f, 0f, roll);

        appliedOffset = offset;
        appliedRoll = roll;
        lastPosition = tr.position;
        lastRotation = tr.rotation;
        hasApplied = offset != Vector3.zero || roll != 0f;
    }
    #endregion
}
