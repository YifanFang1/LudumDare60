using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Executioner-only treatment using the existing TMP font. No external font or texture assets.
[DefaultExecutionOrder(120)]
public class BloodyDialogueText : MonoBehaviour
{
    TMP_Text text;
    Material originalMaterial, bloodMaterial;
    FontStyles originalStyle;
    Color originalColor;
    bool originalGradient;
    VertexGradient originalColors;
    BloodDripGraphic drips;
    bool bloody;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
        originalMaterial = text.fontSharedMaterial;
        originalStyle = text.fontStyle;
        originalColor = text.color;
        originalGradient = text.enableVertexGradient;
        originalColors = text.colorGradient;
        var go = new GameObject("AnimatedBloodDrips", typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.pivot = text.rectTransform.pivot;
        drips = go.AddComponent<BloodDripGraphic>();
        drips.target = text;
        drips.raycastTarget = false;
        go.SetActive(false);
    }

    public void SetBloody(bool value)
    {
        if (bloody == value) return;
        bloody = value;
        if (value)
        {
            if (bloodMaterial == null)
            {
                bloodMaterial = new Material(originalMaterial) { name = "Executioner Blood Lettering", hideFlags = HideFlags.DontSave };
                bloodMaterial.SetFloat("_FaceDilate", 0.12f);
                bloodMaterial.SetFloat("_OutlineWidth", 0.18f);
                bloodMaterial.SetColor("_OutlineColor", new Color(0.12f, 0.005f, 0.015f, 1f));
                bloodMaterial.EnableKeyword("UNDERLAY_ON");
                bloodMaterial.SetColor("_UnderlayColor", new Color(0.01f, 0f, 0f, 0.95f));
                bloodMaterial.SetFloat("_UnderlayOffsetY", -0.8f);
                bloodMaterial.SetFloat("_UnderlaySoftness", 0.15f);
            }
            text.fontSharedMaterial = bloodMaterial;
            text.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            text.color = Color.white;
            text.enableVertexGradient = true;
            text.colorGradient = new VertexGradient(new Color(0.96f, 0.25f, 0.27f), new Color(0.86f, 0.17f, 0.19f),
                new Color(0.46f, 0.025f, 0.055f), new Color(0.57f, 0.04f, 0.065f));
        }
        else Restore();
        drips.gameObject.SetActive(value);
        text.UpdateMeshPadding();
    }

    void LateUpdate()
    {
        if (!bloody || !text.isActiveAndEnabled) return;
        // Rebuild from the original layout each frame so the motion cannot accumulate.
        text.ForceMeshUpdate();
        TMP_TextInfo info = text.textInfo;
        float time = Time.unscaledTime;
        for (int i = 0; i < info.characterCount && i < text.maxVisibleCharacters; i++)
        {
            TMP_CharacterInfo c = info.characterInfo[i];
            if (!c.isVisible) continue;
            Vector3[] vertices = info.meshInfo[c.materialReferenceIndex].vertices;
            float pulse = Mathf.Sin(time * 2.2f + i * 1.7f);
            float sag = (pulse + 1f) * text.fontSize * 0.018f;
            int v = c.vertexIndex;
            vertices[v].y -= sag;
            vertices[v + 3].y -= sag * 0.65f;
            vertices[v + 1].x += pulse * 0.45f;
            vertices[v + 2].x += pulse * 0.45f;
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        drips.SetVerticesDirty();
    }

    void Restore()
    {
        if (text == null) return;
        text.fontSharedMaterial = originalMaterial;
        text.fontStyle = originalStyle;
        text.color = originalColor;
        text.enableVertexGradient = originalGradient;
        text.colorGradient = originalColors;
    }
    void OnDisable()
    {
        bloody = false;
        Restore();
        if (drips != null) drips.gameObject.SetActive(false);
    }
    void OnDestroy()
    {
        Restore();
        if (bloodMaterial != null) Destroy(bloodMaterial);
    }
}

// Small tapered rivulets grow from visible glyphs, then shed a falling drop.
public class BloodDripGraphic : UnityEngine.UI.Graphic
{
    public TMP_Text target;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (target == null) return;
        TMP_TextInfo info = target.textInfo;
        int count = Mathf.Min(info.characterCount, target.maxVisibleCharacters);
        float time = Time.unscaledTime;
        for (int i = 0; i < count; i++)
        {
            TMP_CharacterInfo c = info.characterInfo[i];
            if (!c.isVisible || i % 3 != 1) continue;
            Vector3[] vertices = info.meshInfo[c.materialReferenceIndex].vertices;
            Vector3 left = vertices[c.vertexIndex], right = vertices[c.vertexIndex + 3];
            float x = Mathf.Lerp(left.x, right.x, 0.55f);
            float y = Mathf.Min(left.y, right.y) + 1f;
            float phase = Mathf.Repeat(time * 0.38f + i * 0.173f, 1f);
            float length = target.fontSize * (0.08f + 0.2f * Mathf.SmoothStep(0, 1, Mathf.Min(phase / 0.8f, 1)));
            float width = Mathf.Clamp(target.fontSize * 0.035f, 0.9f, 2.5f);
            Color32 blood = new Color(0.58f + 0.12f * Mathf.Sin(time + i), 0.025f, 0.055f, phase > 0.92f ? (1 - phase) / 0.08f : 1f);
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(x - width, y), blood, Vector2.zero);
            vh.AddVert(new Vector3(x + width, y), blood, Vector2.zero);
            vh.AddVert(new Vector3(x + width * 0.45f, y - length), blood, Vector2.zero);
            vh.AddVert(new Vector3(x - width * 0.45f, y - length), blood, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
            float fall = phase > 0.8f ? (phase - 0.8f) / 0.2f : 0f;
            float dropY = y - length - fall * fall * target.fontSize * 0.35f;
            start = vh.currentVertCount;
            vh.AddVert(new Vector3(x, dropY + width), blood, Vector2.zero);
            vh.AddVert(new Vector3(x + width, dropY - width), blood, Vector2.zero);
            vh.AddVert(new Vector3(x, dropY - width * 2f), blood, Vector2.zero);
            vh.AddVert(new Vector3(x - width, dropY - width), blood, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
