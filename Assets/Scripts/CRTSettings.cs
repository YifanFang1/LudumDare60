using UnityEngine;

// ============================================================================
//  CRT SETTINGS (global controller for the CRT filter)
//
//  This script is the one place that controls the CRT filter.
//  Every frame, it sends its values to ALL shaders as "global" values.
//  The CRT shader reads those global values, so changing a number here
//  changes the effect on screen.
//
//  It also creates itself automatically when the game starts, and it
//  survives scene changes, so the filter is always running.
//
//  To change the effect from any other script:
//      CRTSettings.Instance.waveAmount = 0.01f;
// ============================================================================

[ExecuteAlways] // also runs in the editor, so you can see changes without pressing Play
public class CRTSettings : MonoBehaviour
{
    // The one active copy of this script. Other scripts use this to reach it.
    public static CRTSettings Instance { get; private set; }

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

    // Unity looks up shader values by a number ID. Looking up the ID once
    // here is faster than looking up the name every frame.
    static readonly int IntensityID           = Shader.PropertyToID("_Intensity");
    static readonly int InnerIntensityID      = Shader.PropertyToID("_InnerIntensity");
    static readonly int OuterIntensityID      = Shader.PropertyToID("_OuterIntensity");
    static readonly int EdgeFalloffID         = Shader.PropertyToID("_EdgeFalloff");
    static readonly int TintColorID           = Shader.PropertyToID("_TintColor");
    static readonly int TintStrengthID        = Shader.PropertyToID("_TintStrength");
    static readonly int ColorSplitID          = Shader.PropertyToID("_ColorSplit");
    static readonly int WaveAmountID          = Shader.PropertyToID("_WaveAmount");
    static readonly int WaveFrequencyID       = Shader.PropertyToID("_WaveFrequency");
    static readonly int WaveSpeedID           = Shader.PropertyToID("_WaveSpeed");
    static readonly int ScanlineScrollSpeedID = Shader.PropertyToID("_ScanlineScrollSpeed");
    static readonly int ScanlineCountID       = Shader.PropertyToID("_ScanlineCount");
    static readonly int ScanlineDarknessID    = Shader.PropertyToID("_ScanlineDarkness");
    static readonly int CurvatureID           = Shader.PropertyToID("_Curvature");
    static readonly int VignetteStrengthID    = Shader.PropertyToID("_VignetteStrength");

    // ------------------------------------------------------------------------
    //  AUTOMATIC STARTUP
    //  Unity calls this once when the game starts, before the first scene
    //  loads. If there is no CRTSettings yet, it creates one from the prefab
    //  at Assets/Resources/CRTSettings.prefab.
    // ------------------------------------------------------------------------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateOnGameStart()
    {
        if (Instance != null) return;

        GameObject prefab = Resources.Load<GameObject>("CRTSettings");
        if (prefab == null)
        {
            Debug.LogWarning("CRTSettings: no prefab found at Assets/Resources/CRTSettings.prefab, so the CRT filter has no settings.");
            return;
        }

        Instantiate(prefab).name = "CRTSettings";
    }

    void OnEnable()
    {
        if (Application.isPlaying)
        {
            // Only one CRTSettings is allowed while playing.
            // If another one already exists, remove this extra one.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Keep this object alive when a new scene loads.
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Instance = this;
        }

        ApplySettings();
    }

    void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    // Send the values every frame, so changes from other scripts show up right away.
    void Update()
    {
        ApplySettings();
    }

    // Unity calls this when you move a slider in the Inspector.
    void OnValidate()
    {
        ApplySettings();
    }

    // ------------------------------------------------------------------------
    //  Send every value to the shader.
    // ------------------------------------------------------------------------
    public void ApplySettings()
    {
        Shader.SetGlobalFloat(IntensityID, intensity);
        Shader.SetGlobalFloat(InnerIntensityID, innerIntensity);
        Shader.SetGlobalFloat(OuterIntensityID, outerIntensity);
        Shader.SetGlobalFloat(EdgeFalloffID, edgeFalloff);
        Shader.SetGlobalFloat(TintStrengthID, tintStrength);
        Shader.SetGlobalFloat(ColorSplitID, colorSplit);
        Shader.SetGlobalFloat(WaveAmountID, waveAmount);
        Shader.SetGlobalFloat(WaveFrequencyID, waveFrequency);
        Shader.SetGlobalFloat(WaveSpeedID, waveSpeed);
        Shader.SetGlobalFloat(ScanlineScrollSpeedID, scanlineScrollSpeed);
        Shader.SetGlobalFloat(ScanlineCountID, scanlineCount);
        Shader.SetGlobalFloat(ScanlineDarknessID, scanlineDarkness);
        Shader.SetGlobalFloat(CurvatureID, curvature);
        Shader.SetGlobalFloat(VignetteStrengthID, vignetteStrength);

        // Colors you pick in the Inspector are stored in "gamma" space.
        // If the project renders in "linear" space, the shader expects a
        // linear color, so convert it first. Otherwise the tint looks wrong.
        Color colorForShader = QualitySettings.activeColorSpace == ColorSpace.Linear
            ? tintColor.linear
            : tintColor;
        Shader.SetGlobalVector(TintColorID, colorForShader);
    }
}