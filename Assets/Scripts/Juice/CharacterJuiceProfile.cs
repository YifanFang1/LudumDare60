using UnityEngine;

[CreateAssetMenu(fileName = "CharacterJuiceProfile", menuName = "Scriptable Objects/CharacterJuiceProfile")]
public class CharacterJuiceProfile : ScriptableObject
{
    [Tooltip("The character this juice belongs to. Leave empty on the default profile.")]
    public CharacterData character;

    [Header("Hits this character lands")]
    public JuiceParticleType lightAttack = JuiceParticleType.Sparks;
    public JuiceParticleType heavyAttack = JuiceParticleType.Sparks;
    public JuiceParticleType block = JuiceParticleType.None;   // the sim has no blocking yet

    [Header("Low health aura (plays while HP is at or below the threshold)")]
    public JuiceParticleType lowHealthAura = JuiceParticleType.None;
    [Range(0, 100)] public int lowHealthPercent = 25;
}
