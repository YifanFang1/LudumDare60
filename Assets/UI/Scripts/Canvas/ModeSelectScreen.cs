using TMPro;
using UnityEngine;
using UnityEngine.UI;

// After Play: choose Online (opens the lobby with quick match and rooms) or Offline (starts a match vs the AI).
// Your name is entered here, so it applies to both.
public class ModeSelectScreen : MonoBehaviour
{
    public TMP_InputField nameInput;
    public Button onlineButton;
    public Button offlineButton;
    public Button backButton;

    IMatchSession match;
    MatchScreens screens;
    bool wired;

    void OnEnable()
    {
        match = SessionFinder.Find();
        screens = GetComponentInParent<MatchScreens>();
        if (match == null || screens == null) return;
        if (!wired) { Wire(); wired = true; }
        if (nameInput != null) nameInput.SetTextWithoutNotify(match.LocalName);
        if (onlineButton != null) onlineButton.interactable = match.SupportsRooms;   // a build without online play greys it out
    }

    void Wire()
    {
        if (nameInput != null)
        {
            nameInput.characterLimit = PlayerNames.MaxLength;
            nameInput.onValueChanged.AddListener(value =>
            {
                string clean = PlayerNames.Clean(value);
                if (clean != value) nameInput.SetTextWithoutNotify(clean);
                match.LocalName = clean;
            });
        }
        Hook(onlineButton, () => screens.GoTo(MenuPage.Play));
        Hook(offlineButton, () => match.PlayVsAI());
        Hook(backButton, () => screens.GoTo(MenuPage.Main));
    }

    void Hook(Button button, System.Action action)
    {
        if (button == null) return;
        button.onClick.AddListener(() =>
        {
            AudioManager.Click();
            action();
        });
    }
}
