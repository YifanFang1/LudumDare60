using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Impact frame for JuiceManager: for a split second the screen goes flat black, the fighters turn
/// into solid white silhouettes and the sparks invert whatever is behind them (white on the black
/// background, black on the white fighters). The colours are set in JuiceProfile. View only.
/// <para>
/// It starts with a short inverted beat (white background, black fighters) and then flips to the
/// normal colours, the classic anime impact flicker. JuiceProfile.impactInvertedTicks sets how long.
/// </para>
/// <para>
/// How it works: a big quad in the background colour is parented to the camera and drawn on top of
/// the stage. Every SpriteRenderer on the two fighters, and every particle effect that is playing, is
/// drawn above that quad with a flat-colour material (Assets/Juice/Resources/ImpactSilhouette.shader).
/// When time is up their own materials and sorting orders are put back. It only uses each sprite's
/// outline, so it works for every art style.
/// </para>
/// Hitbox overlays are drawn below the quad, so they are hidden while it shows.
/// </summary>
public class JuiceImpactFrame
{
    #region State
    const int BackgroundOrder = 1000;   // above the stage, the parallax layers and hitboxes
    const int SilhouetteOrder = 2000;   // added to each fighter sprite's own order, so limbs keep their layering
    const int SparkOrder = 3000;        // sparks above the fighters, so they cut into the silhouettes

    // A renderer as it was before we took it over.
    struct Saved
    {
        public Renderer renderer;
        public Material material;
        public int sortingOrder;
    }

    readonly JuiceProfile profile;
    readonly Transform effects;         // JuiceParticles' pool: every spark lives under here
    Camera cam;

    Shader shader;
    Material backgroundMaterial;
    Material silhouetteMaterial;
    // Particles take their texture from their material (sprites get theirs from the SpriteRenderer),
    // so each particle material gets its own spark material with the same texture.
    readonly Dictionary<Material, Material> sparkMaterials = new Dictionary<Material, Material>();
    SpriteRenderer background;
    bool missingShader;

    readonly List<Saved> saved = new List<Saved>();
    readonly HashSet<Renderer> taken = new HashSet<Renderer>();
    GameObject fighter0, fighter1;
    float timeLeft;
    float invertedLeft;                 // seconds left of the inverted first beat
    #endregion

    /// <summary>Uses <paramref name="camera"/>, or Camera.main when it is null.</summary>
    public JuiceImpactFrame(JuiceProfile profile, Camera camera, Transform effects)
    {
        this.profile = profile;
        this.effects = effects;
        cam = camera;
    }

    #region API
    /// <summary>True while the impact frame is showing.</summary>
    public bool Active { get { return timeLeft > 0f; } }

    /// <summary>
    /// Shows the impact frame for <paramref name="seconds"/>. Pass the fighters' art (WorldView.FighterRoot).
    /// Calling it again while it is showing just makes it last longer.
    /// </summary>
    public void Show(float seconds, GameObject fighter0, GameObject fighter1)
    {
        if (seconds <= 0f || !Setup()) return;

        this.fighter0 = fighter0;
        this.fighter1 = fighter1;
        if (timeLeft <= 0f) invertedLeft = Mathf.Min(profile.impactInvertedTicks / 60f, seconds);
        timeLeft = Mathf.Max(timeLeft, seconds);

        // Read the colours every time, so they can be tweaked in the profile while playing.
        ApplyColors();
        foreach (Material m in sparkMaterials.Values) StyleSpark(m);

        TakeOver();
        FitBackground();
        background.enabled = true;
    }

    /// <summary>Hides the impact frame now and gives everything its own look back.</summary>
    public void Stop()
    {
        timeLeft = 0f;
        for (int i = 0; i < saved.Count; i++)
        {
            Saved s = saved[i];
            if (s.renderer == null) continue;   // already destroyed (e.g. WorldView removed the fighter)
            s.renderer.sharedMaterial = s.material;
            s.renderer.sortingOrder = s.sortingOrder;
        }
        saved.Clear();
        taken.Clear();
        fighter0 = null;
        fighter1 = null;
        if (background != null) background.enabled = false;
    }
    #endregion

    #region Tick
    /// <summary>Call once per frame.</summary>
    /// <param name="dt">Real (unscaled) seconds since last frame.</param>
    public void Tick(float dt)
    {
        if (timeLeft <= 0f) return;
        timeLeft -= dt;
        if (timeLeft <= 0f)
        {
            Stop();
            return;
        }
        if (invertedLeft > 0f)
        {
            invertedLeft -= dt;
            ApplyColors();
        }
        TakeOver();   // sparks that started after the frame began (a KO right after a heavy hit)
        FitBackground();
    }
    #endregion

    #region Internals
    // Makes the materials and the background quad the first time they are needed.
    bool Setup()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || missingShader) return false;

        if (shader == null)
        {
            shader = Resources.Load<Shader>("ImpactSilhouette");
            if (shader == null)
            {
                Debug.LogWarning("[Juice] ImpactSilhouette.shader is missing from Assets/Juice/Resources, so impact frames are off.");
                missingShader = true;
                return false;
            }
            backgroundMaterial = new Material(shader);
            silhouetteMaterial = new Material(shader);
        }

        if (background == null)   // also when the camera it hung from was destroyed
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();

            GameObject go = new GameObject("ImpactFrameBackground");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 1f);   // just in front of the camera
            background = go.AddComponent<SpriteRenderer>();
            background.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            background.sharedMaterial = backgroundMaterial;
            background.sortingOrder = BackgroundOrder;
            background.enabled = false;
        }
        return true;
    }

    // The inverted first beat swaps the background and silhouette colours.
    void ApplyColors()
    {
        bool inverted = invertedLeft > 0f;
        backgroundMaterial.color = inverted ? profile.impactSilhouetteColor : profile.impactBackgroundColor;
        silhouetteMaterial.color = inverted ? profile.impactBackgroundColor : profile.impactSilhouetteColor;
    }

    // Covers the whole view, with room to spare for the camera's roll (shake and hill tilt).
    void FitBackground()
    {
        if (cam == null || background == null) return;
        float height = cam.orthographicSize * 2f;
        float size = Mathf.Max(height, height * cam.aspect) * 2f;
        background.transform.localScale = new Vector3(size, size, 1f);
    }

    // Turns the fighters into silhouettes and the playing sparks black and white. Skips anything
    // already taken over, so it is safe to call every frame.
    void TakeOver()
    {
        if (fighter0 != null)
            foreach (SpriteRenderer sr in fighter0.GetComponentsInChildren<SpriteRenderer>()) Take(sr, silhouetteMaterial, SilhouetteOrder);
        if (fighter1 != null)
            foreach (SpriteRenderer sr in fighter1.GetComponentsInChildren<SpriteRenderer>()) Take(sr, silhouetteMaterial, SilhouetteOrder);
        if (effects != null)
            foreach (ParticleSystemRenderer pr in effects.GetComponentsInChildren<ParticleSystemRenderer>()) Take(pr, SparkMaterial(pr.sharedMaterial), SparkOrder);
    }

    void Take(Renderer r, Material material, int orderOffset)
    {
        if (!taken.Add(r)) return;
        saved.Add(new Saved { renderer = r, material = r.sharedMaterial, sortingOrder = r.sortingOrder });
        r.sharedMaterial = material;
        r.sortingOrder = orderOffset + r.sortingOrder;
    }

    Material SparkMaterial(Material original)
    {
        Material m;
        if (original == null) return silhouetteMaterial;
        if (sparkMaterials.TryGetValue(original, out m)) return m;

        m = new Material(shader);
        m.mainTexture = original.mainTexture;   // the particle's shape (white square if it has none)
        StyleSpark(m);
        sparkMaterials[original] = m;
        return m;
    }

    // Invert: white over the black background, black over the white fighters.
    // Otherwise: a flat colour, like the fighters.
    void StyleSpark(Material m)
    {
        if (profile.impactInvertSparks)
        {
            m.color = Color.white;
            m.SetFloat("_SrcBlend", (float)BlendMode.OneMinusDstColor);
        }
        else
        {
            m.color = profile.impactSparkColor;
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
        }
    }
    #endregion
}
