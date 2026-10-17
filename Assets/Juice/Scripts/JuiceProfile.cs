using UnityEngine;

/// <summary>
/// Tuning values for JuiceManager, editable in the inspector so the feel can be adjusted during
/// playtests without touching code.
/// <para>
/// Create: Project window > right click > Create > Fight > Juice Profile.
/// Use: drag it into the Profile slot on the JuiceManager component.
/// With no profile assigned, JuiceManager uses these default values.
/// </para>
/// Particle effects are NOT listed here: they are prefabs found automatically in
/// Assets/Juice/Resources (see JuiceParticles).
/// </summary>
[CreateAssetMenu(menuName = "Fight/Juice Profile", fileName = "JuiceProfile")]
public class JuiceProfile : ScriptableObject
{
    #region Settings
    [Header("Hits")]
    [Tooltip("Hits at or above this damage count as heavy hits.")]
    [Min(0f)] public float heavyThreshold = 10.0f;

    [Header("Screen shake: how much trauma (0..1) each event adds")]
    [Range(0f, 1f)] public float lightHitTrauma = 0.2f;
    [Range(0f, 1f)] public float heavyHitTrauma = 0.5f;
    [Range(0f, 1f)] public float blockTrauma = 0.1f;
    [Range(0f, 1f)] public float koTrauma = 1f;

    [Header("Screen shake: feel")]
    [Tooltip("Furthest the camera moves at full trauma, in Unity units (a fighter is about 1 unit wide).")]
    [Range(0f, 2f)] public float maxShakeOffset = 0.35f;
    [Tooltip("Most the camera rolls at full trauma, in degrees. 0 = no roll.")]
    [Range(0f, 10f)] public float maxShakeRoll = 1.5f;
    [Tooltip("How fast the shake wobbles. Higher = more jittery, lower = more of a sway.")]
    [Range(1f, 60f)] public float shakeFrequency = 25f;
    [Tooltip("Trauma lost per second. 1.5 means a full-trauma shake lasts about 0.7 seconds.")]
    [Range(0.1f, 5f)] public float traumaDecay = 1.5f;
    [Tooltip("Strength = trauma ^ this. 2 makes small hits subtle and big hits punchy; 1 is linear.")]
    [Range(1f, 3f)] public float traumaExponent = 2f;
    [Tooltip("Scales every shake. Set to 0 to turn screen shake off (e.g. an accessibility option).")]
    [Range(0f, 2f)] public float shakeMultiplier = 1f;

    [Header("Hill tilt: the attacker's side of the screen goes uphill on each hit")]
    [Tooltip("Degrees the hill gets steeper per hit. 0 = no hill tilt.")]
    [Range(0f, 30f)] public float hillTiltPerHit = 4f;
    [Tooltip("The hill never gets steeper than this many degrees. Set it equal to Hill Tilt Per Hit " +
             "to snap to one fixed angle instead of building up.")]
    [Range(0f, 90f)] public float maxHillTilt = 20f;
    [Tooltip("How fast the camera swings to the new hill angle. Higher = snappier.")]
    [Range(0.5f, 30f)] public float hillTiltSpeed = 6f;
    [Tooltip("Seconds without a hit before the hill levels out again. 0 = it stays tilted until the match ends.")]
    [Range(0f, 10f)] public float hillTiltHoldTime = 2f;

    [Header("Durations (ticks, 60 = 1 second)")]
    public int flashTicks = 4;
    public int impactTicks = 3;
    public int koImpactTicks = 6;

    [Header("Characters")]
    [Tooltip("Used by any character without its own profile below.")]
    public CharacterJuiceProfile defaultCharacter;
    public CharacterJuiceProfile[] characters = new CharacterJuiceProfile[0];
    #endregion

    #region Lookups
    /// <summary>
    /// The juice for a character id (see CharacterList). Works for inspector characters and code
    /// characters alike. Falls back to <see cref="defaultCharacter"/>, which may be null.
    /// </summary>
    public CharacterJuiceProfile ForCharacter(int characterId)
    {
        for (int i = 0; i < characters.Length; i++)
        {
            CharacterJuiceProfile c = characters[i];
            if (c != null && c.character != null && c.character.CharacterId == characterId) return c;
        }
        return defaultCharacter;
    }
    #endregion
}
