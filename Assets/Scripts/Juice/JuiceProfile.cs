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
/// Assets/Resources/Juice (see JuiceParticles).
/// </summary>
[CreateAssetMenu(menuName = "Fight/Juice Profile", fileName = "JuiceProfile")]
public class JuiceProfile : ScriptableObject
{
    #region Settings
    [Header("Hits")]
    [Tooltip("Hits at or above this damage count as heavy hits.")]
    [Min(0f)] public float heavyThreshold = 10.0f;

    [Header("Screen shake (trauma, 0..1)")]
    [Range(0f, 1f)] public float lightHitTrauma = 0.2f;
    [Range(0f, 1f)] public float heavyHitTrauma = 0.5f;
    [Range(0f, 1f)] public float koTrauma = 1f;

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
