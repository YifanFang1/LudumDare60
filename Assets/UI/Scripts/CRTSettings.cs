using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// ============================================================================
//  CRT SETTINGS (global controller for the CRT filter)
//
//  This script is the one place that controls the CRT filter.
//  Every frame, it sends the values from the CRT profile asset to ALL
//  shaders as "global" values. The CRT shader reads those global values,
//  so changing a number in the profile changes the effect on screen.
//
//  To tune it: menu Fight > CRT Settings selects the profile asset
//  (Assets/UI/Resources/CRTProfile.asset). Drag its sliders in the
//  Inspector, before or during Play mode. The values are in CRTProfile.cs.
//
//  It creates itself automatically when the game starts, and it survives
//  scene changes, so the filter is always running. Each frame it adds
//  CRTRenderPass to every game camera, which draws the shader
//  (Assets/UI/Resources/CRT.shader) over the screen. Nothing has to be
//  placed in a scene.
//
//  To change the effect from any other script:
//      CRTSettings.Instance.profile.waveAmount = 0.01f;
//  For a short burst that fades by itself (and never changes the asset):
//      CRTSettings.Instance.Glitch(0.5f);
// ============================================================================

[ExecuteAlways] // also runs in the editor, so you can see changes without pressing Play
public class CRTSettings : MonoBehaviour
{
    // The one active copy of this script. Other scripts use this to reach it.
    public static CRTSettings Instance { get; private set; }

    // The values. Loaded from Resources/CRTProfile; if that asset doesn't
    // exist yet, a temporary one with the default values is used.
    public CRTProfile profile;

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

    // The material made from CRT.shader, and the pass that draws it.
    Material material;
    CRTRenderPass pass;

    // The current glitch: how long it lasts and how much is left (seconds).
    float glitchTime;
    float glitchLeft;

    // ------------------------------------------------------------------------
    //  AUTOMATIC STARTUP
    //  Unity calls this once when the game starts, before the first scene
    //  loads. If there is no CRTSettings yet, it creates one.
    // ------------------------------------------------------------------------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateOnGameStart()
    {
        if (Instance != null) return;
        new GameObject("CRTSettings").AddComponent<CRTSettings>();
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

        if (profile == null) profile = Resources.Load<CRTProfile>("CRTProfile");
        if (profile == null) profile = ScriptableObject.CreateInstance<CRTProfile>();

        Shader shader = Resources.Load<Shader>("CRT");
        if (shader == null)
        {
            Debug.LogWarning("CRTSettings: CRT.shader was not found in a Resources folder, so the CRT filter is off.");
        }
        else
        {
            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new CRTRenderPass(material);
            RenderPipelineManager.beginCameraRendering += AddPassToCamera;
        }

        ApplySettings();
    }

    void OnDisable()
    {
        if (Instance == this) Instance = null;

        RenderPipelineManager.beginCameraRendering -= AddPassToCamera;
        CoreUtils.Destroy(material);
        material = null;
        pass = null;
    }

    // Called by URP before each camera draws. Adding the pass here every frame
    // is how URP lets a script add a pass without editing the renderer asset.
    void AddPassToCamera(ScriptableRenderContext context, Camera cam)
    {
        if (pass == null || profile == null || profile.intensity <= 0f) return;
        if (cam.cameraType != CameraType.Game) return; // not the Scene view or previews

        cam.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(pass);
    }

    // Send the values every frame, so changes in the Inspector or from other
    // scripts show up right away.
    void Update()
    {
        if (glitchLeft > 0f) glitchLeft = Mathf.Max(0f, glitchLeft - Time.unscaledDeltaTime);
        ApplySettings();
    }

    // ------------------------------------------------------------------------
    //  A short burst of extra colour split, wobble and full-strength effect
    //  in the middle of the screen, fading back to normal over `seconds`.
    //  Added on top of the profile's values, so the asset is never changed.
    // ------------------------------------------------------------------------
    public void Glitch(float seconds)
    {
        if (seconds <= 0f) return;
        glitchTime = seconds;
        glitchLeft = seconds;
    }

    // ------------------------------------------------------------------------
    //  Send every value to the shader.
    // ------------------------------------------------------------------------
    public void ApplySettings()
    {
        if (profile == null) return;
        CRTProfile p = profile;
        float glitch = glitchLeft > 0f ? glitchLeft / glitchTime : 0f;   // 1 at the start, fading to 0
        glitch *= glitch;                                                // fast drop, short tail

        Shader.SetGlobalFloat(IntensityID, p.intensity);
        Shader.SetGlobalFloat(InnerIntensityID, Mathf.Lerp(p.innerIntensity, 1f, glitch));
        Shader.SetGlobalFloat(OuterIntensityID, p.outerIntensity);
        Shader.SetGlobalFloat(EdgeFalloffID, p.edgeFalloff);
        Shader.SetGlobalFloat(TintStrengthID, p.tintStrength);
        Shader.SetGlobalFloat(ColorSplitID, p.colorSplit + p.glitchColorSplit * glitch);
        Shader.SetGlobalFloat(WaveAmountID, p.waveAmount + p.glitchWaveAmount * glitch);
        Shader.SetGlobalFloat(WaveFrequencyID, p.waveFrequency);
        Shader.SetGlobalFloat(WaveSpeedID, p.waveSpeed);
        Shader.SetGlobalFloat(ScanlineScrollSpeedID, p.scanlineScrollSpeed);
        Shader.SetGlobalFloat(ScanlineCountID, p.scanlineCount);
        Shader.SetGlobalFloat(ScanlineDarknessID, p.scanlineDarkness);
        Shader.SetGlobalFloat(CurvatureID, p.curvature);
        Shader.SetGlobalFloat(VignetteStrengthID, p.vignetteStrength);

        // Colors you pick in the Inspector are stored in "gamma" space.
        // If the project renders in "linear" space, the shader expects a
        // linear color, so convert it first. Otherwise the tint looks wrong.
        Color colorForShader = QualitySettings.activeColorSpace == ColorSpace.Linear
            ? p.tintColor.linear
            : p.tintColor;
        Shader.SetGlobalVector(TintColorID, colorForShader);
    }
}
