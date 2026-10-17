using System;
using UnityEngine;

public enum JuiceParticle
{
    // --- general particles ---
    Spawn = 0,
    Triumph = 1,
    Defeat = 2,

    // --- specific particles --- 
    Dust = 10,
    Sparks = 11,
    Flames = 12,
    Smoke = 13,
    Lightning = 14,
    Wind = 15,
    Magic = 16,
    LightRays = 17,
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

    [Serializable]
    public class ParticleEntry
    {
        public JuiceParticle id;
        public GameObject prefab;
    }

    public GameObject GetParticle(JuiceParticle id)
    {
        for (int i = 0; i < effects.Length; i++)
        {
            if (effects[i] != null && effects[i].id == id) return effects[i].prefab;
        }
        return null;
    }
}
