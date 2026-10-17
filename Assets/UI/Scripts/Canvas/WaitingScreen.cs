using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Waiting for an opponent": shows the room code and a cancel button.
public class WaitingScreen : MonoBehaviour
{
    public TMP_Text roomCodeText;
    public TMP_Text hintText;
    public TMP_Text statusText;
    public Button cancelButton;

    IMatchSession match;
    bool wired;

    void OnEnable()
    {
        match = SessionFinder.Find();
        if (match == null) return;
        if (!wired && cancelButton != null)
        {
            cancelButton.onClick.AddListener(() =>
            {
                AudioManager.Click();
                match.Leave();
            });
            wired = true;
        }
    }

    void Update()
    {
        if (match == null) return;
        if (roomCodeText != null) roomCodeText.text = match.RoomCode;
        if (hintText != null) hintText.text = "Give your opponent this code";
        if (statusText != null) statusText.text = match.Status;
    }
}
