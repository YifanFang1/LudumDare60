using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Waiting for an opponent": shows the room code and a cancel button.
// After Quick match there is no code to share, so the code and its heading are hidden.
public class WaitingScreen : MonoBehaviour
{
    public TMP_Text headingText;     // "Room code" label above the code (found under Panel/Heading if left empty)
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
        bool showCode = !match.IsQuickMatch;
        if (headingText == null)
        {
            Transform found = transform.Find("Panel/Heading");
            if (found != null) headingText = found.GetComponent<TMP_Text>();
        }
        SetShown(headingText, showCode);
        SetShown(roomCodeText, showCode);
        if (roomCodeText != null) roomCodeText.text = match.RoomCode;
        if (hintText != null) hintText.text = showCode ? "Give your opponent this code" : "Searching for an opponent...";
        if (statusText != null) statusText.text = match.Status;
    }

    static void SetShown(TMP_Text text, bool on)
    {
        if (text != null && text.gameObject.activeSelf != on) text.gameObject.SetActive(on);
    }
}
