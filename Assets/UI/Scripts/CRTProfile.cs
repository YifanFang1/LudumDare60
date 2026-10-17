using UnityEngine;

// The CRT filter's values, saved as an asset: Assets/UI/Resources/CRTProfile.asset.
// Open it with the menu Fight > CRT Settings and drag the sliders. The screen
// updates live, also in Play mode, and changes made in Play mode are kept
// (unlike changes to objects in the scene).
//
// Changing a value from a script also changes this asset while in the Editor,
// so for a temporary effect (e.g. a flash on KO) set the old value back after.
[CreateAssetMenu(menuName = "Fight/CRT Profile", fileName = "CRTProfile")]
public class CRTProfile : ScriptableObject
{
    [Header("Overall")]
    [Range(0f, 1f)] public float intensity = 1f;

    [Header("Center vs Edges")]
    [Range(0f, 1f)] public float innerIntensity = 0.4f;
    [Range(0f, 1f)] public float outerIntensity = 1f;
    [Range(0.1f, 4f)] public float edgeFalloff = 1.5f;

    [Header("Color")]
    public Color tintColor = new Color(0.85f, 1f, 0.9f, 1f);
    [Range(0f, 1f)] public float tintStrength = 0.3f;
    [Range(0f, 0.02f)] public float colorSplit = 0.003f;

    [Header("Movement")]
    [Range(0f, 0.02f)] public float waveAmount = 0.002f;
    [Range(0f, 100f)] public float waveFrequency = 30f;
    [Range(0f, 20f)] public float waveSpeed = 3f;
    [Range(-20f, 20f)] public float scanlineScrollSpeed = 2f;

    [Header("Scanlines")]
    [Range(50f, 1080f)] public float scanlineCount = 300f;
    [Range(0f, 1f)] public float scanlineDarkness = 0.5f;

    [Header("Screen Shape")]
    [Range(0f, 0.5f)] public float curvature = 0.1f;
    [Range(0f, 1f)] public float vignetteStrength = 0.5f;
}
