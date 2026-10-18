using UnityEngine;
using UnityEngine.UI;

// The start screen: Play, Options (the key bindings), Credits, Quit. Play opens the Online / Offline choice, Controls opens the key bindings page.
public class MainMenuScreen : MonoBehaviour
{
    public Button playButton;
    public Button controlsButton;     // "Options" in the new layout
    public Button creditsButton;
    public Button quitButton;

    MatchScreens screens;
    bool wired;

    void OnEnable()
    {
        screens = GetComponentInParent<MatchScreens>();
        if (screens == null) return;
        if (!wired) { Wire(); wired = true; }
    }

    void Wire()
    {
        if (playButton != null) playButton.onClick.AddListener(() => { AudioManager.Click(); screens.GoTo(MenuPage.Mode); });
        if (controlsButton != null) controlsButton.onClick.AddListener(() => { AudioManager.Click(); screens.GoTo(MenuPage.Controls); });
        if (creditsButton != null) creditsButton.onClick.AddListener(() => { AudioManager.Click(); screens.GoTo(MenuPage.Credits); });
        if (quitButton != null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            quitButton.gameObject.SetActive(false);   // a browser tab cannot be quit from inside the page
#else
            quitButton.onClick.AddListener(() =>
            {
                AudioManager.Click();
                quitButton.interactable = false;
                // switch off like an old TV right away, then the game closes
                TvOffEffect.Play(screens.transform, 0f, Quit);
            });
#endif
        }
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
