using UnityEngine;
using UnityEngine.UI;

// The start screen: Play, Controls, Quit. Play opens the Online / Offline choice, Controls opens the key bindings page.
public class MainMenuScreen : MonoBehaviour
{
    public Button playButton;
    public Button controlsButton;
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
        if (quitButton != null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            quitButton.gameObject.SetActive(false);   // a browser tab cannot be quit from inside the page
#else
            quitButton.onClick.AddListener(Quit);
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
