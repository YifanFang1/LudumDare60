using UnityEngine;
using UnityEngine.UI;

// The credits page: just a Back button for now (put the names in the Text on the page).
public class CreditsScreen : MonoBehaviour
{
    public Button backButton;

    MatchScreens screens;
    bool wired;

    void OnEnable()
    {
        screens = GetComponentInParent<MatchScreens>();
        if (screens == null || wired || backButton == null) return;
        backButton.onClick.AddListener(() => { AudioManager.Click(); screens.GoBack(); });
        wired = true;
    }
}
