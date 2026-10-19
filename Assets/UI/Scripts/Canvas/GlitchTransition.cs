using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The screen-change effect: the CRT's full-screen glitch ramps up over the old screen, the page is switched
// at the peak, then the glitch fades out over the new one (see CRTSettings.SetFillGlitch and the
// "Full-screen glitch" values on the CRT Profile). MatchScreens calls Run() whenever the screen that should
// be showing changes. It needs the CRT filter to be running; without it the screens just switch.
// Build it with  Fight > Rebuild Default Canvas  (the TransitionOverlay object).
public class GlitchTransition : MonoBehaviour
{
    public float rampUpTime = 0.14f;
    public float peakHold = 0.03f;
    public float fadeOutTime = 0.18f;

    [Tooltip("A see-through full-screen Image that swallows clicks while the glitch plays (made by the builder).")]
    public Graphic blocker;

    public bool Busy { get; private set; }

    void Awake()
    {
        if (blocker != null) blocker.gameObject.SetActive(false);
    }

    // `onPeak` runs at the height of the glitch (the moment to switch screens).
    public void Run(Action onPeak)
    {
        if (Busy) return;
        StartCoroutine(Play(onPeak));
    }

    IEnumerator Play(Action onPeak)
    {
        Busy = true;
        AudioManager.Glitch();
        if (blocker != null) blocker.gameObject.SetActive(true);

        for (float t = 0f; t < rampUpTime; t += Time.unscaledDeltaTime)
        {
            SetGlitch(t / rampUpTime);
            yield return null;
        }
        SetGlitch(1f);
        yield return new WaitForSecondsRealtime(peakHold);

        AudioManager.PageSwitch();
        if (onPeak != null) onPeak();
        yield return null;

        for (float t = 0f; t < fadeOutTime; t += Time.unscaledDeltaTime)
        {
            SetGlitch(1f - t / fadeOutTime);
            yield return null;
        }
        SetGlitch(0f);
        if (blocker != null) blocker.gameObject.SetActive(false);
        Busy = false;
    }

    void OnDisable()
    {
        SetGlitch(0f);
    }

    static void SetGlitch(float level)
    {
        if (CRTSettings.Instance != null) CRTSettings.Instance.SetFillGlitch(level);
    }
}
