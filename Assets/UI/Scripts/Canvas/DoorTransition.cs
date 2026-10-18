using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The sliding metal door between screens. A full-screen shutter slides down from the top and covers
// everything, the page underneath is switched while it is closed, then it slides back up.
// MatchScreens calls Run() whenever the screen that should be showing changes.
// Build it with  Fight > Rebuild Default Canvas  (the Door child holds the picture).
public class DoorTransition : MonoBehaviour
{
    [Tooltip("The shutter picture: a full-screen Image, a child of this object.")]
    public RectTransform door;

    public float closeTime = 0.28f;
    public float holdTime = 0.06f;
    public float openTime = 0.34f;

    public bool Busy { get; private set; }

    Image doorImage;

    void Awake()
    {
        if (door != null)
        {
            doorImage = door.GetComponent<Image>();
            door.gameObject.SetActive(false);
        }
    }

    // `onCovered` runs once the door is fully closed (the moment to switch screens).
    public void Run(Action onCovered)
    {
        if (Busy) return;
        if (door == null) { if (onCovered != null) onCovered(); return; }
        StartCoroutine(Play(onCovered));
    }

    IEnumerator Play(Action onCovered)
    {
        Busy = true;
        float height = ((RectTransform)transform).rect.height;
        if (height <= 1f) height = 1080f;
        door.gameObject.SetActive(true);   // blocks clicks while it moves
        if (doorImage != null) doorImage.raycastTarget = true;

        // closing: gravity-like, speeds up as it falls
        for (float t = 0f; t < closeTime; t += Time.unscaledDeltaTime)
        {
            float k = t / closeTime;
            SetY(Mathf.Lerp(height, 0f, k * k));
            yield return null;
        }
        SetY(0f);
        yield return new WaitForSecondsRealtime(holdTime);

        if (onCovered != null) onCovered();
        yield return null;

        // opening: a motor winding it up, quick then easing out
        for (float t = 0f; t < openTime; t += Time.unscaledDeltaTime)
        {
            float k = t / openTime;
            SetY(Mathf.Lerp(0f, height, 1f - (1f - k) * (1f - k)));
            yield return null;
        }
        door.gameObject.SetActive(false);
        Busy = false;
    }

    void SetY(float y)
    {
        door.anchoredPosition = new Vector2(0f, y);
    }
}
