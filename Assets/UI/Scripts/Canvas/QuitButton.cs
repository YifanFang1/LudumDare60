using UnityEngine;
using UnityEngine.UI;

// A Quit button for any menu page: the picture switches off like an old TV, then the game closes.
// In a browser a tab can't be closed from the page, so the TV switches back on at the title screen instead.
// Put it on a GameObject with a Button (the canvas builder adds one to every menu page).
[RequireComponent(typeof(Button))]
public class QuitButton : MonoBehaviour
{
    MatchScreens screens;
    Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        screens = GetComponentInParent<MatchScreens>();
        if (screens == null) return;
        AudioManager.Click();
        button.interactable = false;
#if UNITY_WEBGL && !UNITY_EDITOR
        GameObject tv = null;
        tv = TvOffEffect.Play(screens.transform, 0f, () =>
        {
            if (tv != null) Destroy(tv);
            button.interactable = true;
            screens.GoTo(MenuPage.Title, true);   // no glitch, straight back to "press any button"
        });
#else
        TvOffEffect.Play(screens.transform, 0f, Quit);
#endif
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
