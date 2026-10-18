using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// "PRESS ANY KEY": the arcade-style start screen. The prompt fades in and out in coarse steps.
// Any key, click, tap or gamepad button opens the main menu (MatchScreens breaks the screen like glass over the change).
public class TitleScreen : MonoBehaviour
{
    public Graphic promptText;   // the blinking prompt: a RawImage / Image with your picture, or a TMP text

    [Header("Prompt blink")]
    public float blinkPeriod = 1.6f;                // seconds for one fade in + out
    [Range(2, 16)] public int fadeSteps = 8;        // fewer steps = more arcade
    [Range(0f, 0.5f)] public float dimmest = 0.08f;

    [Tooltip("Ignore input for this long after the screen appears, so a key still held from before does not skip it.")]
    public float inputDelay = 0.4f;

    MatchScreens screens;
    float shown;
    bool triggered;

    void OnEnable()
    {
        screens = GetComponentInParent<MatchScreens>();
        shown = 0f;
        triggered = false;
        SetPromptAlpha(dimmest);
    }

    void Update()
    {
        shown += Time.unscaledDeltaTime;
        if (triggered) return;

        float wave = 0.5f + 0.5f * Mathf.Sin(shown * Mathf.PI * 2f / blinkPeriod - Mathf.PI * 0.5f);   // starts dark, fades up
        float stepped = Mathf.Round(wave * fadeSteps) / fadeSteps;
        SetPromptAlpha(Mathf.Lerp(dimmest, 1f, stepped));

        Vector2 point;
        if (shown >= inputDelay && AnyPressed(out point)) Trigger(point);
    }

    void Trigger(Vector2 point)
    {
        triggered = true;
        SetPromptAlpha(1f);
        AudioManager.Confirm();
        if (screens != null)
        {
            screens.shatterPoint = point;   // the glass breaks where you pressed
            screens.GoTo(MenuPage.Main);
        }
    }

    void SetPromptAlpha(float a)
    {
        if (promptText == null) return;
        Color c = promptText.color;
        c.a = a;
        promptText.color = c;
    }

    // Where the glass gets hit: the click or touch point, or the middle of the screen for a key.
    static bool AnyPressed(out Vector2 point)
    {
        point = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;

        Mouse mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
        {
            point = mouse.position.ReadValue();
            return true;
        }

        Touchscreen touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
        {
            point = touch.primaryTouch.position.ReadValue();
            return true;
        }

        Gamepad pad = Gamepad.current;
        if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame)) return true;
        return false;
#else
        if (Input.anyKeyDown)
        {
            if (Input.GetMouseButtonDown(0)) point = Input.mousePosition;
            return true;
        }
        return false;
#endif
    }
}
