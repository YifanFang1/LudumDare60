using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The old TV switching off: the picture squashes into a bright horizontal line, the line shrinks to a dot, then
// everything is black. Built from plain UI Images at runtime, so
// there is nothing to set up in the scene. Used by Quit:  TvOffEffect.Play(canvasTransform, 0f, quitAction);
public class TvOffEffect : MonoBehaviour
{
    public float waitBefore = 0f;     // seconds to wait after the button press (0 = start at once)
    public float squashTime = 0.28f;  // picture collapses to a line
    public float lineTime = 0.2f;     // line shrinks to a dot
    public float blackHold = 0.25f;   // black before `done` is called

    static Sprite white;

    RectTransform root;
    Image top, bottom, gap, black, line;
    Action done;

    // Starts the effect under `canvas` (a full-screen canvas transform). `done` runs at the very end.
    public static GameObject Play(Transform canvas, float wait, Action done)
    {
        GameObject go = new GameObject("TvOffEffect", typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.transform.SetAsLastSibling();

        TvOffEffect fx = go.AddComponent<TvOffEffect>();
        fx.waitBefore = wait;
        fx.root = rt;
        fx.done = done;
        fx.Build();
        fx.StartCoroutine(fx.Run());
        return go;
    }

    Image Make(string name, Color color, bool raycast = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);
        Image img = go.GetComponent<Image>();
        if (white == null)
        {
            Texture2D t = Texture2D.whiteTexture;
            white = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
        }
        img.sprite = white;
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    static void CentreBox(Image img, float w, float h)
    {
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(w, h);
    }

    void Build()
    {
        // swallows clicks from the moment Quit is pressed
        Image blocker = Make("Blocker", new Color(0f, 0f, 0f, 0f), true);
        blocker.rectTransform.anchorMin = Vector2.zero;
        blocker.rectTransform.anchorMax = Vector2.one;
        blocker.rectTransform.offsetMin = blocker.rectTransform.offsetMax = Vector2.zero;

        black = Make("Black", Color.black);
        black.rectTransform.anchorMin = Vector2.zero;
        black.rectTransform.anchorMax = Vector2.one;
        black.rectTransform.offsetMin = black.rectTransform.offsetMax = Vector2.zero;
        black.enabled = false;

        top = Make("Top", Color.black);
        top.rectTransform.anchorMin = new Vector2(0f, 1f);
        top.rectTransform.anchorMax = new Vector2(1f, 1f);
        top.rectTransform.pivot = new Vector2(0.5f, 1f);
        top.rectTransform.anchoredPosition = Vector2.zero;
        top.rectTransform.sizeDelta = Vector2.zero;

        bottom = Make("Bottom", Color.black);
        bottom.rectTransform.anchorMin = new Vector2(0f, 0f);
        bottom.rectTransform.anchorMax = new Vector2(1f, 0f);
        bottom.rectTransform.pivot = new Vector2(0.5f, 0f);
        bottom.rectTransform.anchoredPosition = Vector2.zero;
        bottom.rectTransform.sizeDelta = Vector2.zero;

        // the bright strip that fills the gap between the bars
        gap = Make("Gap", new Color(1f, 1f, 1f, 0f));
        line = Make("Line", Color.white);
        gap.enabled = line.enabled = false;
        // the bars must stay on top of the gap strip
        top.transform.SetAsLastSibling();
        bottom.transform.SetAsLastSibling();
    }

    IEnumerator Run()
    {
        if (waitBefore > 0f) yield return new WaitForSecondsRealtime(waitBefore);

        float w = root.rect.width > 1f ? root.rect.width : 1920f;
        float h = root.rect.height > 1f ? root.rect.height : 1080f;
        float lineH = 5f;
        if (CRTSettings.Instance != null) CRTSettings.Instance.Glitch(squashTime);

        // 1: the picture is squashed from top and bottom into a line; the leftover gap turns white
        gap.enabled = true;
        for (float t = 0f; t < squashTime; t += Time.unscaledDeltaTime)
        {
            float k = t / squashTime;
            float open = Mathf.Lerp(h, lineH, k * k * k);       // slow start, snaps shut
            float bar = (h - open) * 0.5f;
            top.rectTransform.sizeDelta = new Vector2(0f, bar);
            bottom.rectTransform.sizeDelta = new Vector2(0f, bar);
            CentreBox(gap, w, open);
            gap.color = new Color(1f, 1f, 1f, Mathf.Clamp01((k - 0.45f) * 1.9f));
            yield return null;
        }
        top.rectTransform.sizeDelta = new Vector2(0f, (h - lineH) * 0.5f);
        bottom.rectTransform.sizeDelta = top.rectTransform.sizeDelta;
        CentreBox(gap, w, lineH);
        gap.color = Color.white;

        // 2: the line shrinks sideways to a dot, on black
        black.enabled = true;
        top.enabled = bottom.enabled = gap.enabled = false;
        line.enabled = true;
        for (float t = 0f; t < lineTime; t += Time.unscaledDeltaTime)
        {
            float k = t / lineTime;
            float width = Mathf.Lerp(w, 0f, k * k);                // shrinks to nothing, no square left over
            CentreBox(line, width, Mathf.Lerp(lineH, 3f, k));
            yield return null;
        }

        line.enabled = false;   // the dot is gone: straight to black

        yield return new WaitForSecondsRealtime(blackHold);
        if (done != null) done();
    }
}
