using System;
using UnityEngine;

public enum JuiceParticleType
{
    // --- general particles ---
    None = 0,
    Spawn = 1,
    Triumph = 2,
    Defeat = 3,

    // --- specific particles --- 
    Dust = 10,
    Sparks = 11,
    Flames = 12,
    Smoke = 13,
    Lightning = 14,
    Wind = 15,
    Magic = 16,
    LightRays = 17,
    Sludge = 18
}

[CreateAssetMenu(menuName = "Fight/Juice Profile", fileName = "JuiceProfile")]
public class JuiceProfile : ScriptableObject
{
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

    [Header("Particle effects")]
    public ParticleEntry[] effects = new ParticleEntry[0];

    [Header("Characters")]
    [Tooltip("Used by Brawler (written in code) and any character without its own profile below.")]
    public CharacterJuiceProfile defaultCharacter;
    public CharacterJuiceProfile[] characters = new CharacterJuiceProfile[0];

    // The juice for a character id (see CharacterList). Falls back to the default profile.
    public CharacterJuiceProfile ForCharacter(int characterId)
    {
        for (int i = 0; i < characters.Length; i++)
        {
            CharacterJuiceProfile c = characters[i];
            if (c != null && c.character != null && c.character.def.id == characterId) return c;
        }
        return defaultCharacter;
    }

    [Serializable]
    public class ParticleEntry
    {
        public JuiceParticleType id;
        public JuiceEffect prefab;
    }

    public JuiceEffect GetParticle(JuiceParticleType id)
    {
        for (int i = 0; i < effects.Length; i++)
        {
            if (effects[i] != null && effects[i].id == id) return effects[i].prefab;
        }
        return null;
    }
}
