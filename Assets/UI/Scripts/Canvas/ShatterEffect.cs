using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The "screen cracks like glass" transition (Persona 5 style).
//
// Play() grabs the current screen, crushes it to high contrast (black, red, white), flashes, grows
// cracks out from the impact point, then splits the picture into shards that fall away and reveal
// whatever is underneath. Put this on its own always-active, full-screen object that sits LAST in the
// canvas so it draws on top (the builder does this: "ShatterOverlay"). Everything it draws is created
// at run time and removed again afterwards.
public class ShatterEffect : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Crush the captured screen to black / red / white before it breaks.")]
    public bool highContrast = true;
    public Color crackColor = Color.white;
    public Color crackOutline = Color.black;
    public Color flashColor = Color.white;
    [Tooltip("Gap between shards, in pixels, so the picture underneath shows through the cracks.")]
    public float gap = 2.5f;

    [Header("Timing (seconds)")]
    public float flashTime = 0.14f;
    [Tooltip("The cracks snap in as four hard frames. This is how long each one stays before the next.")]
    public float crackFrame = 0.07f;
    public float holdTime = 0.1f;
    public float fallTime = 1.7f;

    [Header("Cracks")]
    [Tooltip("Long cracks that run from the impact point to the edge of the screen. More rays = more shards.")]
    [Range(6, 16)] public int rays = 10;
    [Tooltip("1 = normal. Lower = fewer cross cracks (bigger shards), higher = more of them.")]
    [Range(0f, 1.5f)] public float crossCracks = 1f;

    [Header("Shards")]
    public float gravity = 3200f;     // canvas units per second squared
    public float kick = 380f;         // outward speed of the shards near the impact
    public float spin = 220f;         // degrees per second, at most
    public float shake = 14f;         // jolt each time the cracks snap in

    public bool IsPlaying { get; private set; }

    // Starts the effect. screenPoint = where the glass is hit (pixels). onCovered runs once the cracked
    // picture hides the screen, so you can switch the page underneath. onDone runs when the last shard is gone.
    public void Play(Vector2 screenPoint, Action onCovered, Action onDone)
    {
        if (IsPlaying)
        {
            if (onCovered != null) onCovered();
            if (onDone != null) onDone();
            return;
        }
        StartCoroutine(Run(screenPoint, onCovered, onDone));
    }

    class Shard
    {
        public RectTransform rt;
        public ShardGraphic graphic;
        public Vector2 pos;
        public Vector2 vel;
        public float rot;
        public float angVel;
        public float delay;
    }

    IEnumerator Run(Vector2 screenPoint, Action onCovered, Action onDone)
    {
        IsPlaying = true;
        yield return new WaitForEndOfFrame();   // the screen can only be read once the frame is drawn

        Texture2D shot = Capture();
        RectTransform area = (RectTransform)transform;
        Vector2 size = area.rect.size;
        if (size.x < 1f || size.y < 1f) size = new Vector2(1920f, 1080f);

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 impact;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screenPoint, uiCamera, out impact)) impact = Vector2.zero;

        // ---- the stage everything is drawn on ----
        RectTransform stage = NewRect("ShatterStage", area);
        Stretch(stage);

        RawImage still = NewRect("Still", stage).gameObject.AddComponent<RawImage>();
        Stretch(still.rectTransform);
        still.texture = shot;
        still.color = shot != null ? Color.white : new Color(0.04f, 0.03f, 0.05f, 1f);
        still.raycastTarget = true;   // the effect swallows clicks until the shards start to fall

        // ---- crack pattern: straight cracks from the impact point, cut into a few big angular shards ----
        Fracture fracture = Fracture.Build(size, impact, rays, crossCracks);
        impact = fracture.impact;

        CrackGraphic cracks = NewRect("Cracks", stage).gameObject.AddComponent<CrackGraphic>();
        Stretch(cracks.rectTransform);
        cracks.raycastTarget = false;
        cracks.fill = crackColor;
        cracks.outline = crackOutline;
        cracks.segments.AddRange(fracture.cracks);
        cracks.step = -1;

        Image flash = NewRect("Flash", stage).gameObject.AddComponent<Image>();
        Stretch(flash.rectTransform);
        flash.color = flashColor;
        flash.raycastTarget = false;

        if (onCovered != null) onCovered();

        // ---- 1. flash, and the cracks snap in as hard frames ----
        float elapsed = 0f;
        for (int step = 0; step < CrackGraphic.Steps; step++)
        {
            cracks.step = step;
            cracks.SetVerticesDirty();
            stage.anchoredPosition = UnityEngine.Random.insideUnitCircle * shake;   // a jolt on every snap
            float held = 0f;
            while (held < crackFrame)
            {
                float dt = Time.unscaledDeltaTime;
                held += dt;
                elapsed += dt;
                Color f = flashColor;
                f.a = Mathf.Clamp01(1f - elapsed / Mathf.Max(0.01f, flashTime));
                flash.color = f;
                yield return null;
            }
        }
        flash.gameObject.SetActive(false);

        // ---- 2. a beat of stillness before it breaks ----
        float t = 0f;
        while (t < holdTime)
        {
            t += Time.unscaledDeltaTime;
            stage.anchoredPosition = UnityEngine.Random.insideUnitCircle * shake * 0.2f;
            yield return null;
        }
        stage.anchoredPosition = Vector2.zero;

        // ---- 3. split into shards ----
        float diag = size.magnitude;
        List<Shard> shards = new List<Shard>();
        for (int i = 0; i < fracture.pieces.Count; i++)
        {
            Shard s = MakeShard(stage, fracture.pieces[i], size, shot, impact, diag);
            if (s != null) shards.Add(s);
        }
        still.gameObject.SetActive(false);
        cracks.gameObject.SetActive(false);

        // ---- 4. the shards fall away ----
        t = 0f;
        while (t < fallTime)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            float fade = Mathf.Clamp01((fallTime - t) / 0.3f);
            for (int n = 0; n < shards.Count; n++)
            {
                Shard s = shards[n];
                if (t < s.delay) continue;
                s.vel.y -= gravity * dt;
                s.pos += s.vel * dt;
                s.rot += s.angVel * dt;
                s.rt.anchoredPosition = s.pos;
                s.rt.localEulerAngles = new Vector3(0f, 0f, s.rot);
                if (fade < 1f)
                {
                    Color c = s.graphic.color;
                    c.a = fade;
                    s.graphic.color = c;
                }
            }
            yield return null;
        }

        Destroy(stage.gameObject);
        if (shot != null) Destroy(shot);
        IsPlaying = false;
        if (onDone != null) onDone();
    }

    Shard MakeShard(RectTransform stage, Vector2[] poly, Vector2 area, Texture2D shot, Vector2 impact, float diag)
    {
        Vector2 centroid = Vector2.zero;
        for (int i = 0; i < poly.Length; i++) centroid += poly[i];
        centroid /= poly.Length;

        // pull the corners in a little: that is the crack you see between shards
        Vector2[] p = new Vector2[poly.Length];
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 toCentre = centroid - poly[i];
            float len = toCentre.magnitude;
            p[i] = len > 0.001f ? poly[i] + toCentre / len * Mathf.Min(gap, len * 0.4f) : poly[i];
            min = Vector2.Min(min, p[i]);
            max = Vector2.Max(max, p[i]);
        }
        Vector2 mid = (min + max) * 0.5f;
        for (int i = 0; i < p.Length; i++) p[i] -= mid;

        RectTransform rt = NewRect("Shard", stage);
        rt.anchoredPosition = mid;
        rt.sizeDelta = new Vector2(Mathf.Max(1f, max.x - min.x), Mathf.Max(1f, max.y - min.y));
        ShardGraphic g = rt.gameObject.AddComponent<ShardGraphic>();
        g.raycastTarget = false;
        g.points = p;
        g.areaSize = area;
        g.areaOffset = mid;
        g.texture = shot;
        float v = UnityEngine.Random.Range(0.8f, 1f);   // slight brightness change per shard so the facets read as glass
        g.color = shot != null ? new Color(v, v, v, 1f) : new Color(0.05f * v, 0.04f * v, 0.06f * v, 1f);

        Shard s = new Shard();
        s.rt = rt;
        s.graphic = g;
        s.pos = mid;
        Vector2 away = centroid - impact;
        float dist01 = Mathf.Clamp01(away.magnitude / Mathf.Max(1f, diag));
        Vector2 dir = away.sqrMagnitude > 1f ? away.normalized : UnityEngine.Random.insideUnitCircle.normalized;
        float speed = kick * (1f - dist01 * 0.6f) * UnityEngine.Random.Range(0.6f, 1.2f);
        s.vel = dir * speed + new Vector2(0f, UnityEngine.Random.Range(40f, 160f));
        s.angVel = UnityEngine.Random.Range(-spin, spin) * (1f - dist01 * 0.5f);
        s.delay = dist01 * 0.35f + UnityEngine.Random.Range(0f, 0.1f);
        return s;
    }

    // ---------- helpers ----------

    Texture2D Capture()
    {
        try
        {
            Texture2D t = ScreenCapture.CaptureScreenshotAsTexture();
            if (t == null) return null;
            if (highContrast) Crush(t);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            return t;
        }
        catch (Exception)
        {
            return null;   // no capture on this platform: the effect still plays, on a dark picture
        }
    }

        static void Crush(Texture2D t)
    {
        Color32[] px = t.GetPixels32();
        Color32 black = new Color32(10, 8, 12, 255);
        Color32 red = new Color32(222, 18, 40, 255);
        Color32 white = new Color32(250, 250, 250, 255);
        for (int i = 0; i < px.Length; i++)
        {
            int lum = (px[i].r * 54 + px[i].g * 183 + px[i].b * 19) >> 8;
            px[i] = lum < 40 ? black : (lum < 160 ? red : white);
        }
        t.SetPixels32(px);
        t.Apply();
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
