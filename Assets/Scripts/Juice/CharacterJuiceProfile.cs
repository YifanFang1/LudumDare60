using UnityEngine;

/// <summary>
/// Which particle effects one character uses. Effects are referred to by name (the JuiceEffect
/// prefab's Effect Name, not case sensitive); an empty name means "no effect".
/// <para>
/// Add it to the JuiceProfile's Characters list, or set it as the profile's Default Character to
/// cover every character that has no profile of its own.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "CharacterJuiceProfile", menuName = "Scriptable Objects/CharacterJuiceProfile")]
public class CharacterJuiceProfile : ScriptableObject
{
    #region Settings
    [Tooltip("The character this juice belongs to: its CharacterData, or the Code Character Art of a character written in code. Leave empty on the default profile.")]
    public CharacterLook character;

    [Header("Hits this character lands (effect names)")]
    public string lightAttack = "Sparks";
    public string heavyAttack = "Sparks";

    [Header("When this character blocks (effect name)")]
    public string block = "";

    [Header("Low health aura (plays while HP is at or below the threshold)")]
    public string lowHealthAura = "";
    [Range(0, 100)] public int lowHealthPercent = 25;
    #endregion
}
